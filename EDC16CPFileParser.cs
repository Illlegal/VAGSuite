using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows.Forms;
using DevExpress.Utils.About;
using Nevron.Chart;
using static VAGSuite.EDC16FileParser;
using DevExpress.XtraCharts.Native;
using static VAGSuite.MapViewerEx;
using System.Reflection;

namespace VAGSuite
{

    public class EDC16CPFileParser : IEDCFileParser
    {
        public enum AxisType { RPM, Pedal, Boost, Atm, Torque, IQ, Temperature, Angle, MAF, MAP, PWM, SOI, DOI, BIP, Lambda, BatteryVoltage}

        public AxisDefinition AxisMan(AxisType type)
        {
            AxisDefinition axis = new AxisDefinition();

            switch (type)
            {
                case AxisType.RPM:
                    axis.Description = "Engine speed (rpm)";
                    axis.Correction = 1;
                    axis.Units = "rpm";
                    axis.Offset = 0;
                    break;

                case AxisType.Pedal:
                    axis.Description = "Throttle  position";
                    axis.Correction = 0.01f;
                    axis.Units = "%";
                    axis.Offset = 0;
                    break;

                case AxisType.Boost:
                    axis.Description = "Maximum boost pressure(mbar)";
                    axis.Correction = 1f;
                    axis.Units = "mBar";
                    axis.Offset = 0;
                    break;

                case AxisType.Atm:
                    axis.Description = "Atmospheric pressure (mbar)";
                    axis.Correction = 1f;
                    axis.Units = "mBar";
                    axis.Offset = 0;
                    break;


                case AxisType.Torque:
                    axis.Description = "Torque (nM)";
                    axis.Correction = 0.1f;
                    axis.Units = "nM";
                    axis.Offset = 0;
                    break;

                case AxisType.IQ:
                    axis.Description = "Injection Quantity (mg/stroke)";
                    axis.Correction = 0.01f;
                    axis.Units = "mg";
                    axis.Offset = 0;
                    break;

                case AxisType.Temperature:
                    axis.Description = "Temperature in Celsius Degree";
                    axis.Correction = 0.1f;
                    axis.Units = "*C";
                    axis.Offset = -273.1f;
                    break;
                
                 case AxisType.Angle:
                    axis.Description = "Crankshaft angle (crankshaft degrees)";
                    axis.Correction = 0.0234375f;
                    axis.Units = "*";
                    axis.Offset = 0;
                    break;

                case AxisType.MAF:
                    axis.Description = "Measured Airflow (mg/s)";
                    axis.Correction = 0.1f;
                    axis.Units = "mg/s";
                    axis.Offset = 0;
                    break;

                case AxisType.MAP:
                    axis.Description = "Measured pressure (mbar)";
                    axis.Correction = 1f;
                    axis.Units = "mBar";
                    axis.Offset = 0;
                    break;

                case AxisType.PWM:
                    axis.Description = "PWM (%)";
                    axis.Correction = 0.01f;
                    axis.Units = "%";
                    axis.Offset = 0;
                    break;

                case AxisType.SOI:
                    axis.Description = "Start position (degrees BTDC)";
                    axis.Correction = 0.0234375f;
                    axis.Units = "*";
                    axis.Offset = 0;
                    break;

                case AxisType.DOI:
                    axis.Description = "Duration (crankshaft degrees)";
                    axis.Correction = 0.0234375f;
                    axis.Units = "*";
                    axis.Offset = 0;
                    break;

                case AxisType.BIP:
                    axis.Description = "Resolution of BIP calculations";
                    axis.Correction = 0.0002441406f;
                    axis.Units = "*";
                    axis.Offset = 0;
                    break;

                case AxisType.Lambda:
                    axis.Description = "Target Lambda (factor)";
                    axis.Correction = 0.001f;
                    axis.Units = "";
                    axis.Offset = 0;
                    break;

                case AxisType.BatteryVoltage://for BIP maps
                    axis.Description = "Battery Voltage";
                    axis.Correction = 0.0203147605083f;
                    axis.Units = "Volts";
                    axis.Offset = 0;
                    break;

                default:
                    axis.Description = "";
                    axis.Correction = 1f;
                    axis.Units = "";
                    axis.Offset = 0;
                    break;
                    //AxisType.RPM => 
            }
            return axis;
        }
        public override SymbolCollection parseFile(string filename, out List<CodeBlock> newCodeBlocks, out List<AxisHelper> newAxisHelpers)
        {
            newCodeBlocks = new List<CodeBlock>();
            SymbolCollection newSymbols = new SymbolCollection();
            newAxisHelpers = new List<AxisHelper>();
            // Bosch EDC16 style mapdetection LL LL AXIS AXIS MAPDATA
            byte[] allBytes = File.ReadAllBytes(filename);

            for (int i = 0; i < allBytes.Length - 32; i+=2)
            {
                int len2Skip = CheckMap(i, allBytes, newSymbols, newCodeBlocks);
                if ((len2Skip % 2) > 0)
                {
                    if (len2Skip > 2) len2Skip--;
                    else len2Skip++;
                }
                i += len2Skip;
            }
            newSymbols.SortColumn = "Flash_start_address";
            newSymbols.SortingOrder = GenericComparer.SortOrder.Ascending;
            newSymbols.Sort();
            NameKnownMaps(allBytes, newSymbols, newCodeBlocks);
            FindSVBL(allBytes, filename, newSymbols, newCodeBlocks);
            FindSVRL(allBytes, filename, newSymbols, newCodeBlocks);
            SymbolTranslator strans = new SymbolTranslator();
            foreach (SymbolHelper sh in newSymbols)
            {
                sh.Description = strans.TranslateSymbolToHelpText(sh.Varname);
            }
            return newSymbols;       
        }

        private int CheckMap(int t, byte[] allBytes, SymbolCollection newSymbols, List<CodeBlock> newCodeBlocks)
        {
            int retval = 0;
            // read LL LL 
            int len1 = Convert.ToInt32(allBytes[t]) * 256 + Convert.ToInt32(allBytes[t + 1]);
            int len2 = Convert.ToInt32(allBytes[t + 2]) * 256 + Convert.ToInt32(allBytes[t + 3]);
            if (len1 < 32 && len2 < 32 && len1 > 0 && len2 > 0)
            {
                SymbolHelper sh = new SymbolHelper();
                sh.X_axis_address = t + 4;
                sh.X_axis_length = len1;
                sh.Y_axis_address = sh.X_axis_address + sh.X_axis_length * 2;
                sh.Y_axis_length = len2;
                sh.Flash_start_address = sh.Y_axis_address + sh.Y_axis_length * 2;
                sh.Length = sh.X_axis_length * sh.Y_axis_length * 2;
                if (sh.X_axis_length > 1 && sh.Y_axis_length > 1)
                {
                    sh.Varname = "3D " + sh.Flash_start_address.ToString("X8");
                }
                else
                {
                    sh.Varname = "2D " + sh.Flash_start_address.ToString("X8");
                }
                
                AddToSymbolCollection(newSymbols, sh, newCodeBlocks);
                retval = (len1 + len2) * 2 + sh.Length ;

            }
            return retval;
        }

        private bool AddToSymbolCollection(SymbolCollection newSymbols, SymbolHelper newSymbol, List<CodeBlock> newCodeBlocks)
        {
            if (newSymbol.Length >= 800) return false;
            foreach (SymbolHelper sh in newSymbols)
            {
                if (sh.Flash_start_address == newSymbol.Flash_start_address)
                {
                    //   Console.WriteLine("Already in collection: " + sh.Flash_start_address.ToString("X8"));
                    return false;
                }
            }
            newSymbols.Add(newSymbol);
            newSymbol.CodeBlock = DetermineCodeBlockByByAddress(newSymbol.Flash_start_address, newCodeBlocks);
            return true;
        }


        private int DetermineCodeBlockByByAddress(long address, List<CodeBlock> currBlocks)
        {
            foreach (CodeBlock cb in currBlocks)
            {
                if (cb.StartAddress <= address && cb.EndAddress >= address)
                {
                //../int block = (address >= 0x1C0000) ? 2 : 1;
                }
            }
            int block = (address >= 0x1C0000) ? 2 : 1;
            return block;
        }

        public override string ExtractInfo(byte[] allBytes)
        {
            // assume info will be @ 0x53452 12 bytes
            string retval = string.Empty;
            try
            {
                int partnumberAddress = Tools.Instance.findSequence(allBytes, 0, new byte[5] { 0x45, 0x44, 0x43, 0x20, 0x20 }, new byte[5] { 1, 1, 1, 1, 1 });
                if (partnumberAddress > 0)
                {
                    retval = System.Text.ASCIIEncoding.ASCII.GetString(allBytes, partnumberAddress - 8, 12).Trim();
                }
            }
            catch (Exception)
            {
            }
            return retval;
        }

        public override string ExtractPartnumber(byte[] allBytes)
        {
            // assume info will be @ 0x53446 12 bytes
            string retval = string.Empty;
            try
            {
                int partnumberAddress = Tools.Instance.findSequence(allBytes, 0, new byte[5] { 0x45, 0x44, 0x43, 0x20, 0x20 }, new byte[5] { 1, 1, 1, 1, 1 });
                if (partnumberAddress > 0)
                {
                    retval = System.Text.ASCIIEncoding.ASCII.GetString(allBytes, partnumberAddress - 20, 12).Trim();
                }
            }
            catch (Exception)
            {
            }
            return retval;
        }

        public override string ExtractSoftwareNumber(byte[] allBytes)
        {
            string retval = string.Empty;
            try
            {
                int partnumberAddress = Tools.Instance.findSequence(allBytes, 0, new byte[5] { 0x45, 0x44, 0x43, 0x20, 0x20 }, new byte[5] { 1, 1, 1, 1, 1 });
                if (partnumberAddress > 0)
                {
                    retval = System.Text.ASCIIEncoding.ASCII.GetString(allBytes, partnumberAddress + 5, 8).Trim();
                    retval = retval.Replace(" ", "");
                }
            }
            catch (Exception)
            {
            }
            return retval;
        }

        public override string ExtractBoschPartnumber(byte[] allBytes)
        {
            return Tools.Instance.ExtractBoschPartnumber(allBytes);
            string retval = string.Empty;
            try
            {
                int partnumberAddress = Tools.Instance.findSequence(allBytes, 0, new byte[5] { 0x45, 0x44, 0x43, 0x20, 0x20 }, new byte[5] { 1, 1, 1, 1, 1 });
                if (partnumberAddress > 0)
                {
                    retval = System.Text.ASCIIEncoding.ASCII.GetString(allBytes, partnumberAddress + 23, 10).Trim();
                }
            }
            catch (Exception)
            {
            }
            return retval;
        }
        public override void FindSVBL(byte[] allBytes, string filename, SymbolCollection newSymbols, List<CodeBlock> newCodeBlocks)
        {
            bool found = true;
            int offset = 0;
            while (found)
            {//06 06 06 40 06 61 06 B8 07 3A 08 CA 08 CA 08 CA 08 CA 08 CA ? ? 00 00
                int SVBLAddress = Tools.Instance.findSequence(allBytes, offset, new byte[24] { 0x06, 0x06, 0x06, 0x40, 0x06, 0x61, 0x06, 0xB8, 0x07, 0x3A, 0x08, 0xCA, 0x08, 0xCA, 0x08, 0xCA, 0x08, 0xCA, 0x08, 0xCA, 0x00, 0x00, 0x00, 0x00, }, new byte[24] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 1, 1});
                ///int SVBLAddress = Tools.Instance.findSequence(allBytes, offset, new byte[16] { 0xD2, 0x00, 0xFC, 0x03, 0x00, 0x00, 0x00, 0x00, 0x04, 0x00, 0xFF, 0xFF, 0xFF, 0xC3, 0x00, 0x00 }, new byte[16] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 1, 1, 1 });

                if (SVBLAddress > 0)
                {
                    SymbolHelper shsvbl = new SymbolHelper();
                    shsvbl.Category = "Turbo";
                    shsvbl.Subcategory = "Limiters";
                    shsvbl.Flash_start_address = SVBLAddress +20;
                    shsvbl.Varname = "Single Value Boost Limiter (SVBL)";
                    shsvbl.Length = 2;
                    shsvbl.Size = "1x1";
                    shsvbl.CodeBlock = DetermineCodeBlockByByAddress(shsvbl.Flash_start_address, newCodeBlocks);
                    newSymbols.Add(shsvbl);

                    int MAPMAFSwitch = Tools.Instance.findSequence(allBytes, SVBLAddress - 0x100, new byte[8] { 0x41, 0x02, 0xFF, 0xFF, 0x00, 0x01, 0x01, 0x00 }, new byte[8] { 1, 1, 0, 0, 1, 1, 1, 1 });
                    if (MAPMAFSwitch > 0)
                    {
                        MAPMAFSwitch += 2;
                        SymbolHelper mapmafsh = new SymbolHelper();
                        //mapmafsh.BitMask = 0x0101;
                        mapmafsh.Category = "Detected maps";
                        mapmafsh.Subcategory = "Switches";
                        mapmafsh.Flash_start_address = MAPMAFSwitch;
                        mapmafsh.Varname = "MAP/MAF switch (0 = MAF, 257/0x101 = MAP)" + DetermineNumberByFlashBank(shsvbl.Flash_start_address, newCodeBlocks);
                        mapmafsh.Length = 2;
                        mapmafsh.CodeBlock = DetermineCodeBlockByByAddress(mapmafsh.Flash_start_address, newCodeBlocks);
                        newSymbols.Add(mapmafsh);
                        Console.WriteLine("Found MAP MAF switch @ " + MAPMAFSwitch.ToString("X8"));
                    }
                    offset = SVBLAddress + 1;
                }
                else found = false;
            }
        }
        public void FindSVRL(byte[] allBytes, string filename, SymbolCollection newSymbols, List<CodeBlock> newCodeBlocks)
        {
            bool found = true;
            int offset = 0;
            while (found)
            { //00 00 00 01 00 01 0C 00 00 32 14 B4
                int SVRLAddress = Tools.Instance.findSequence(allBytes, offset, new byte[10] { 0x00, 0x00, 0x00, 0x01, 0x00, 0x01, 0x0C, 0x00, 0x00, 0x32 }, new byte[10] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 });
                if (SVRLAddress > 0)
                {
                    SymbolHelper shsvrl = new SymbolHelper();
                    shsvrl.Category = "Misc";
                    shsvrl.Subcategory = "Limiters";
                    shsvrl.Flash_start_address = SVRLAddress + 10;
                    shsvrl.Varname = "Single Value Rev Limiter (SVRL)";
                    shsvrl.Length = 2;
                    shsvrl.Size = "1x1";
                    shsvrl.CodeBlock = DetermineCodeBlockByByAddress(shsvrl.Flash_start_address, newCodeBlocks);
                    newSymbols.Add(shsvrl);
                    offset = SVRLAddress + 1;
                }
                else found = false;
            }
        }
        private string DetermineNumberByFlashBank(long address, List<CodeBlock> currBlocks)
        {
            /// Determines the codeblock number (1 or 2) for a given flash address in VAG EDC16 ECUs.

            /*foreach (CodeBlock cb in currBlocks)
            {
                if (cb.StartAddress <= address && cb.EndAddress >= address)
                {
                    if (cb.CodeID == 1) return "codeblock 1";// - MAN";
                    if (cb.CodeID == 2) return "codeblock 2";// - AUT (hydr)";
                    if (cb.CodeID == 3) return "codeblock 3";// - AUT (elek)";
                    return cb.CodeID.ToString();
                }
            }
            */
            // Safety: code area is usually > 0x40000, below that it's usually bootloader/code
            if (address < 0x40000) return "Codeblock 0"; // or return 1 if you prefer

            // Classic Bosch TC1766 mirroring: bit 19 selects the mirrored calibration block
            // Lower block (bit 19 = 0) → usually codeblock 1 in most map packs
            // Higher block (bit 19 = 1) → codeblock 2
            int block = (address >= 0x1C0000) ? 2 : 1;
            return "Codeblock " + block;

            long bankNumber = address / 0x10000;
            return "flashbank " + bankNumber.ToString();
        }

        public string ExtractEDC16Version(byte[] allBytes)
        {
            // assume info will be @ 0x53452 12 bytes
            string retval = string.Empty;
            try
            {
                int EDC16Version = Tools.Instance.findSequence(allBytes, 0, new byte[8] { 0x45, 0x44, 0x43, 0x31, 0x36, 0x55, 0x33, 0x34}, new byte[8] { 1, 1, 1, 1, 1, 1, 1, 1 });
                if (EDC16Version > 0)
                {
                    retval = System.Text.ASCIIEncoding.ASCII.GetString(allBytes, EDC16Version, 8).Trim();
                }
            }
            catch (Exception)
            {
            }
            return retval;
        }

        public override void NameKnownMaps(byte[] allBytes, SymbolCollection newSymbols, List<CodeBlock> newCodeBlocks)
        {
            SymbolAxesTranslator st = new SymbolAxesTranslator();

            AxisDefinition rpm = AxisMan(AxisType.RPM);
            AxisDefinition tq = AxisMan(AxisType.Torque);
            AxisDefinition atm = AxisMan(AxisType.Atm);
            AxisDefinition tmp = AxisMan(AxisType.Temperature);
            AxisDefinition tps = AxisMan(AxisType.Pedal);
            AxisDefinition iq = AxisMan(AxisType.IQ);
            AxisDefinition ang = AxisMan(AxisType.Angle);
            AxisDefinition bst = AxisMan(AxisType.Boost);
            AxisDefinition maf = AxisMan(AxisType.MAF);
            AxisDefinition map = AxisMan(AxisType.MAP);
            AxisDefinition pwm = AxisMan(AxisType.PWM);
            AxisDefinition soi = AxisMan(AxisType.SOI);
            AxisDefinition doi = AxisMan(AxisType.DOI);
            AxisDefinition bip = AxisMan(AxisType.BIP);
            AxisDefinition lmb = AxisMan(AxisType.Lambda);
            AxisDefinition bat = AxisMan(AxisType.BatteryVoltage);

            foreach (SymbolHelper sh in newSymbols)
            {
                // sh.X_axis_descr = st.TranslateAxisID(sh.X_axis_ID);
                // sh.Y_axis_descr = st.TranslateAxisID(sh.Y_axis_ID);
                if (sh.X_axis_length == 4 && sh.Y_axis_length == 20)
                {
                    sh.Category = "Torque";
                    sh.Subcategory = "Limiters";
                    sh.Size = "[" + sh.Y_axis_length + "x" + sh.X_axis_length + "]";
                    //int lmCount = GetMapNameCountForCodeBlock("Torque limiter", sh.CodeBlock, newSymbols, false);
                    sh.Varname = "Torque limiter";
                    sh.X_axis_descr = rpm.Description;
                    sh.Y_axis_descr = atm.Description;
                    sh.Z_axis_descr = tq.Description;
                    sh.XaxisUnits = rpm.Units;
                    sh.YaxisUnits = atm.Units;
                    sh.Correction = tq.Correction;

                }

                if (sh.X_axis_length == 4 && sh.Y_axis_length == 21)
                {
                    sh.Category = "Torque";
                    sh.Subcategory = "Limiters";
                    sh.Size = "[" + sh.Y_axis_length + "x" + sh.X_axis_length + "]";
                    //int lmCount = GetMapNameCountForCodeBlock("Torque limiter", sh.CodeBlock, newSymbols, false);
                    sh.Varname = "Torque limiter";
                    sh.X_axis_descr = rpm.Description;
                    sh.Y_axis_descr = atm.Description;
                    sh.Z_axis_descr = tq.Description;
                    sh.XaxisUnits = rpm.Units;
                    sh.YaxisUnits = atm.Units;
                    sh.Correction = tq.Correction;
                }


                else if (sh.X_axis_length == 3 && sh.Y_axis_length == 21)
                {
                    if (!CollectionContainsMapInSize(newSymbols, 21, 4))
                    {
                        sh.Category = "Torque";
                        sh.Subcategory = "Limiters";
                        sh.Size = "[" + sh.Y_axis_length + "x" + sh.X_axis_length + "]";
                        //int lmCount = GetMapNameCountForCodeBlock("Torque limiter", sh.CodeBlock, newSymbols, false);
                        sh.Varname = "Torque limiter";

                        sh.X_axis_descr = rpm.Description;
                        sh.Y_axis_descr = atm.Description;
                        sh.Z_axis_descr = tq.Description;
                        sh.XaxisUnits = rpm.Units;
                        sh.YaxisUnits = atm.Units;
                        sh.Correction = tq.Correction;
                    }
                }

                else if ((sh.X_axis_length == 16 && sh.Y_axis_length == 8))//&& (sh.X_axis_length == 8 && sh.Y_axis_length == 16)
                {
                    sh.Category = "Torque";
                    sh.Subcategory = "Target torque";
                    sh.CodeBlock = DetermineCodeBlockByByAddress(sh.Flash_start_address, newCodeBlocks);
                    sh.Size = "[" + sh.Y_axis_length + "x" + sh.X_axis_length + "]";
                    int dwCount = GetMapNameCountForCodeBlock("Driver wish ", sh.CodeBlock, newSymbols, false);

                    sh.Varname = "Driver wish 0" + dwCount.ToString();

                    sh.X_axis_descr = tps.Description;
                    sh.Y_axis_descr = rpm.Description;
                    sh.Z_axis_descr = tq.Description;

                    sh.XaxisUnits = tps.Units;
                    sh.YaxisUnits = rpm.Units;
                    sh.X_axis_correction = tps.Correction;
                    sh.Correction = tq.Correction;

                }
                else if ((sh.X_axis_length == 15 && sh.Y_axis_length == 16) || (sh.X_axis_length == 15 && sh.Y_axis_length == 18))
                {
                    sh.Category = "Misc";
                    sh.Subcategory = "Conversions/Linearizations";
                    sh.Size = "[" + sh.Y_axis_length + "x" + sh.X_axis_length + "]";
                    sh.Varname = "IQ to Torque conversion";

                    sh.X_axis_descr = tq.Description;
                    sh.Z_axis_descr = iq.Description;
                    sh.Y_axis_descr = rpm.Description;
                    sh.YaxisUnits = rpm.Units;
                    sh.XaxisUnits = tq.Units;
                    sh.Correction = iq.Correction;
                    sh.X_axis_correction = tq.Correction;
                }
                else if ((sh.X_axis_length == 11 && sh.Y_axis_length == 10) || (sh.X_axis_length == 10 && sh.Y_axis_length == 10))
                {   //11x10 or 10x10 map
                    if (GetMaxAxisValue(allBytes, sh, MapViewerEx.AxisIdent.Z_Axis) != 0 && !CheckMapFlat(allBytes, sh))
                    {   //Check if map is not zero-ed and flat
                        if (GetMaxAxisValue(allBytes, sh, MapViewerEx.AxisIdent.Z_Axis) < (3000 / map.Correction) && GetMaxAxisValue(allBytes, sh, MapViewerEx.AxisIdent.Z_Axis) > (1930 / map.Correction))
                        {   //map with 3000 mBar max and min 1930mbar
                            sh.Category = "Turbo";
                            sh.Subcategory = "Limiters";
                            sh.Size = "[" + sh.Y_axis_length + "x" + sh.X_axis_length + "]";
                            sh.Varname = "Boost limit map";// " + sh.Flash_start_address.ToString("X8") + " " + sh.X_axis_ID.ToString("X4") + " " + sh.Y_axis_ID.ToString("X4");
                            sh.X_axis_descr = atm.Description;
                            sh.Z_axis_descr = bst.Description;
                            sh.Y_axis_descr = rpm.Description;
                            sh.YaxisUnits = rpm.Units;
                            sh.XaxisUnits = bst.Units;
                        }
                    }
                }
                else if (sh.X_axis_length == 16 && sh.Y_axis_length == 10)
                {   //16x10 map
                    if ((GetMaxAxisValue(allBytes, sh, MapViewerEx.AxisIdent.Z_Axis) < 3000) && GetMaxAxisValue(allBytes, sh, MapViewerEx.AxisIdent.Z_Axis) > (1930 / map.Correction))
                    {   //map with 3000 mBar max and min 1930mbar
                        sh.Category = "Turbo";
                        sh.Subcategory = "Target Boost";
                        sh.Size = "[" + sh.Y_axis_length + "x" + sh.X_axis_length + "]";
                        sh.Varname = "Turbo Boost"; // " + sh.Flash_start_address.ToString("X8") + " " + sh.X_axis_ID.ToString("X4") + " " + sh.Y_axis_ID.ToString("X4");

                        sh.X_axis_descr = iq.Description;
                        sh.Y_axis_descr = rpm.Description;
                        sh.Z_axis_descr = bst.Description;
                        sh.XaxisUnits = iq.Units;
                        sh.YaxisUnits = rpm.Units;
                        sh.X_axis_correction = iq.Correction;
                    }
                }
                else if (sh.X_axis_length == 16 && sh.Y_axis_length == 13 && GetMaxAxisValue(allBytes, sh, MapViewerEx.AxisIdent.Z_Axis) < 3000)
                {
                    sh.Category = "Fuel";
                    sh.Subcategory = "Limiters";
                    sh.Size = "[" + sh.Y_axis_length + "x" + sh.X_axis_length + "]";
                    sh.Varname = "IQ Limit by Lambda";// " + sh.Flash_start_address.ToString("X8") + " " + sh.X_axis_ID.ToString("X4") + " " + sh.Y_axis_ID.ToString("X4");

                    sh.X_axis_descr = maf.Description;
                    sh.Y_axis_descr = rpm.Description;
                    sh.Z_axis_descr = lmb.Description;
                    sh.XaxisUnits = maf.Units;
                    sh.YaxisUnits = rpm.Units;
                    sh.X_axis_correction = maf.Correction;
                    sh.Correction = lmb.Correction;
                }
                else if (sh.X_axis_length == 16 && sh.Y_axis_length == 13 && GetMaxAxisValue(allBytes, sh, MapViewerEx.AxisIdent.Z_Axis) > 3000 && GetMaxAxisValue(allBytes, sh, MapViewerEx.AxisIdent.Y_Axis) > 6000)
                //13x16 map with value bigger than 30iq and maf bigger than 600 airflow
                {
                    sh.Category = "Fuel";
                    sh.Subcategory = "Limiters";
                    sh.Size = "[" + sh.Y_axis_length + "x" + sh.X_axis_length + "]";
                    sh.Varname = "IQ Limit by MAF";// " + sh.Flash_start_address.ToString("X8") + " " + sh.X_axis_ID.ToString("X4") + " " + sh.Y_axis_ID.ToString("X4");

                    sh.X_axis_descr = maf.Description;
                    sh.Y_axis_descr = rpm.Description;
                    sh.Z_axis_descr = iq.Description;
                    sh.XaxisUnits = iq.Units;
                    sh.YaxisUnits = rpm.Units;
                    sh.X_axis_correction = maf.Correction;
                    sh.Correction = iq.Correction;
                }
                else if ((sh.X_axis_length == 16 && sh.Y_axis_length == 12) || (sh.X_axis_length == 16 && sh.Y_axis_length == 11))
                //12x16 map, 11x16
                {
                    if (GetMaxAxisValue(allBytes, sh, MapViewerEx.AxisIdent.Z_Axis) > (30/iq.Correction) && GetMaxAxisValue(allBytes, sh, MapViewerEx.AxisIdent.Y_Axis) > (1000/map.Correction) && GetMaxAxisValue(allBytes, sh, MapViewerEx.AxisIdent.Y_Axis) < (4001/map.Correction))
                    {   //with value bigger than 30iq and IAP in 1000 - 4000 mbar area :)
                        sh.Category = "Fuel";
                        sh.Subcategory = "Limiters";
                        sh.Size = "[" + sh.Y_axis_length + "x" + sh.X_axis_length + "]";
                        sh.Varname = "IQ Limit by MAP";// " + sh.Flash_start_address.ToString("X8") + " " + sh.X_axis_ID.ToString("X4") + " " + sh.Y_axis_ID.ToString("X4");

                        sh.X_axis_descr = map.Description;
                        sh.Y_axis_descr = rpm.Description;
                        sh.Z_axis_descr = iq.Description;
                        sh.XaxisUnits = map.Units;
                        sh.YaxisUnits = rpm.Units;
                        sh.Correction = iq.Correction;
                    }
                }
                else if ((sh.X_axis_length == 10 && sh.Y_axis_length == 10) || (sh.X_axis_length == 19 && sh.Y_axis_length == 15))
                {
                    if (CheckAxisValues(allBytes, sh, MapViewerEx.AxisIdent.Y_Axis, (0.5f / iq.Correction), (10 / iq.Correction)) && GetMaxAxisValue(allBytes, sh, MapViewerEx.AxisIdent.Y_Axis) < (90 / iq.Correction))
                    //check for 15x19 map or 10x10 with step in iq axis between 0.5 to 10 mg/str and max 90mg value
                    {
                        if (GetMaxAxisValue(allBytes, sh, MapViewerEx.AxisIdent.Z_Axis) != 0 && !CheckMapFlat(allBytes, sh))
                        //Check if map is not zero-ed and flat
                        {
                            if (GetMaxMapValue(allBytes, sh) > (10.0f / doi.Correction))//
                            //Check if map values (duration) isn't bigger than 45 degrees, which will be stupid to be
                            {
                                sh.Category = "Fuel";
                                sh.Subcategory = "Injector duration";
                                int injDurCount = GetMapNameCountForCodeBlock("Injector duration", sh.CodeBlock, newSymbols, false);
                                sh.Size = "[" + sh.Y_axis_length + "x" + sh.X_axis_length + "]";
                                sh.Varname = "Injector duration " + injDurCount.ToString("D2");// " + sh.Flash_start_address.ToString("X8") + " " + sh.X_axis_ID.ToString("X4") + " " + sh.Y_axis_ID.ToString("X4");

                                sh.X_axis_descr = iq.Description;
                                sh.Y_axis_descr = rpm.Description;
                                sh.Z_axis_descr = doi.Description;
                                sh.XaxisUnits = iq.Units;
                                sh.YaxisUnits = rpm.Units;
                                sh.X_axis_correction = iq.Correction; // TODO: Check for x or y
                                sh.Correction = doi.Correction;
                            }
                        }
                    }
                }
                else if (sh.X_axis_length == 16 && sh.Y_axis_length == 11)
                {
                    sh.Category = "Turbo"; 
                    sh.Subcategory = "N75";
                    int injDurCount = GetMapNameCountForCodeBlock("Injector duration", sh.CodeBlock, newSymbols, false);
                    sh.Size = "[" + sh.Y_axis_length + "x" + sh.X_axis_length + "]";
                    sh.Varname = "N75";// " + sh.Flash_start_address.ToString("X8") + " " + sh.X_axis_ID.ToString("X4") + " " + sh.Y_axis_ID.ToString("X4");

                    sh.Y_axis_descr = rpm.Description;
                    sh.X_axis_descr = iq.Description;
                    sh.Z_axis_descr = pwm.Description;
                    sh.XaxisUnits = iq.Units;
                    sh.YaxisUnits = rpm.Units;
                    sh.X_axis_correction = iq.Correction; // TODO: Check for x or y
                    sh.Correction = pwm.Correction;
                }
                else if ((sh.X_axis_length == 16 && sh.Y_axis_length == 13) || (sh.X_axis_length == 16 && sh.Y_axis_length == 14)) 
                {   //typical 13x16, 14x16 egr map
                    //int xmax = GetMaxAxisValue(allBytes, sh, MapViewerEx.AxisIdent.X_Axis);
                    //int ymax = GetMaxAxisValue(allBytes, sh, MapViewerEx.AxisIdent.Y_Axis);
                    //Console.WriteLine(xmax.ToString() + " " + ymax.ToString() + " " + sh.Flash_start_address.ToString("X8"));
                    if (GetMaxAxisValue(allBytes, sh, MapViewerEx.AxisIdent.Z_Axis) < (1500 / maf.Correction) && GetMaxAxisValue(allBytes, sh, MapViewerEx.AxisIdent.Y_Axis) < (50/iq.Correction))
                    {   //iq to 40 max, 
                        sh.Category = "Misc";
                        sh.Subcategory = "EGR";
                        sh.Size = "[" + sh.Y_axis_length + "x" + sh.X_axis_length + "]";
                        sh.Varname = "EGR";

                        sh.Z_axis_descr = maf.Description;
                        sh.Y_axis_descr = rpm.Description;
                        sh.X_axis_descr = iq.Description;
                        sh.YaxisUnits = rpm.Units;
                        sh.XaxisUnits = iq.Units;
                        sh.Correction = maf.Correction;
                        sh.X_axis_correction = iq.Correction;

                    }
                }
                else if ((sh.X_axis_length == 1 && sh.Y_axis_length == 20))//|| (sh.X_axis_length == 16 && sh.Y_axis_length == 14)
                {   //typical 13x16, 14x16 egr map
                    //int xmax = GetMaxAxisValue(allBytes, sh, MapViewerEx.AxisIdent.X_Axis);
                    //int ymax = GetMaxAxisValue(allBytes, sh, MapViewerEx.AxisIdent.Y_Axis);
                    //Console.WriteLine(xmax.ToString() + " " + ymax.ToString() + " " + sh.Flash_start_address.ToString("X8"));
                    if (true)//GetMaxAxisValue(allBytes, sh, MapViewerEx.AxisIdent.Z_Axis) < (15000 / maf.Correction) && GetMaxAxisValue(allBytes, sh, MapViewerEx.AxisIdent.Y_Axis) < (50 / iq.Correction)
                    {   //iq to 40 max, 
                        sh.Category = "Misc";
                        sh.Subcategory = "EGR";
                        sh.Size = "[" + sh.Y_axis_length + "x" + sh.X_axis_length + "]";
                        sh.Varname = "EGR Hysteresis";

                        sh.Z_axis_descr = maf.Description;
                        sh.Y_axis_descr = rpm.Description;
                        sh.X_axis_descr = iq.Description;
                        sh.YaxisUnits = rpm.Units;
                        sh.XaxisUnits = iq.Units;
                        sh.Correction = maf.Correction;
                        sh.X_axis_correction = iq.Correction;

                    }
                }
                else if (sh.X_axis_length == 14 && sh.Y_axis_length == 16)
                {
                    // SOI
                    sh.Category = "Fuel"; 
                    sh.Subcategory = "SOI";
                    sh.Size = "[" + sh.Y_axis_length + "x" + sh.X_axis_length + "]";

                    //Console.WriteLine("Temperature switch for SOI " + injDurCount.ToString() + " " + tempRange.ToString());
                    //sh.Varname = "Start of injection (SOI) " + injDurCount.ToString("D2") + " [" + DetermineNumberByFlashBank(sh.Flash_start_address, newCodeBlocks) + "]";// " + sh.Flash_start_address.ToString("X8") + " " + sh.X_axis_ID.ToString("X4") + " " + sh.Y_axis_ID.ToString("X4");
                    sh.Varname = "Start of injection (SOI)";// " + sh.Flash_start_address.ToString("X8") + " " + sh.X_axis_ID.ToString("X4") + " " + sh.Y_axis_ID.ToString("X4");

                    sh.X_axis_descr = iq.Description;
                    sh.Y_axis_descr = rpm.Description;
                    sh.Z_axis_descr = soi.Description;
                    sh.XaxisUnits = iq.Units;
                    sh.YaxisUnits = rpm.Units;
                    sh.X_axis_correction = iq.Correction; // TODODONE : Check for x or y
                    sh.Correction = soi.Correction;
                }
                else if ((sh.X_axis_length == 9 && sh.Y_axis_length == 9) || (sh.X_axis_length == 8 && sh.Y_axis_length == 9))
                {
                    if (GetMaxAxisValue(allBytes, sh, MapViewerEx.AxisIdent.X_Axis) < (130 - tmp.Offset)/tmp.Correction)//
                    {   //start iq map 9x9 with no more than 130*C (120*10+273.1)
                        if (GetMaxAxisValue(allBytes, sh, MapViewerEx.AxisIdent.Z_Axis) != 0 && !CheckMapFlat(allBytes, sh))
                        {      //Check if map is not zero-ed and flat
                            
                            sh.Category = "Fuel";
                            sh.Subcategory = "Start IQ";
                            sh.Size = "[" + sh.Y_axis_length + "x" + sh.X_axis_length + "]";
                            sh.Varname = "Start IQ";// " + sh.Flash_start_address.ToString("X8") + " " + sh.X_axis_ID.ToString("X4") + " " + sh.Y_axis_ID.ToString("X4");
                            sh.X_axis_descr = tmp.Description;
                            sh.Y_axis_descr = rpm.Description;
                            sh.Z_axis_descr = tq.Description;
                            sh.XaxisUnits = tmp.Units;
                            sh.YaxisUnits = rpm.Units;
                            sh.X_axis_correction = tmp.Correction; // TODODONE : Check for x or y
                            sh.X_axis_offset = tmp.Offset;
                            sh.Correction = tq.Correction;
                        }
                    }
                }
                else if (sh.X_axis_length == 10 && sh.Y_axis_length == 8)
                {
                    if (GetMaxAxisValue(allBytes, sh, MapViewerEx.AxisIdent.Z_Axis) != 0 && !CheckMapFlat(allBytes, sh))//GetMaxAxisValue(allBytes, sh, MapViewerEx.AxisIdent.X_Axis) < (130 - tmp.Offset) / tmp.Correction
                    {   //Check if map is not zero-ed and flat
                        sh.Category = "Fuel";
                        sh.Subcategory = "Corrections";
                        sh.Size = "[" + sh.Y_axis_length + "x" + sh.X_axis_length + "]";

                        sh.Varname = "BIP Multiple Correction";// " + sh.Flash_start_address.ToString("X8") + " " + sh.X_axis_ID.ToString("X4") + " " + sh.Y_axis_ID.ToString("X4");

                        sh.X_axis_descr = ang.Description;
                        sh.Y_axis_descr = rpm.Description;
                        sh.Z_axis_descr = bip.Description;
                        sh.XaxisUnits = ang.Units;
                        sh.YaxisUnits = rpm.Units;
                        sh.X_axis_correction = ang.Correction; // TODODONE : Check for x or y
                        //sh.Correction = tq.Correction;*/
                        sh.Correction = bip.Correction;
                    }
                }
                else if (sh.X_axis_length == 1 && sh.Y_axis_length == 10)
                {
                    if (true)//GetMaxAxisValue(allBytes, sh, MapViewerEx.AxisIdent.X_Axis) < (130 - tmp.Offset) / tmp.Correction
                    {
                        // start iq map 9x9 with no more than 130*C (120*10+273.1)
                        sh.Category = "Fuel";
                        sh.Subcategory = "Corrections";
                        sh.Size = "[" + sh.Y_axis_length + "x" + sh.X_axis_length + "]";

                        sh.Varname = "BIP Basic Characteristic";// " + sh.Flash_start_address.ToString("X8") + " " + sh.X_axis_ID.ToString("X4") + " " + sh.Y_axis_ID.ToString("X4");

                        sh.X_axis_descr = ang.Description;
                        sh.Y_axis_descr = rpm.Description;
                        sh.Z_axis_descr = bip.Description;
                        sh.XaxisUnits = ang.Units;
                        sh.YaxisUnits = rpm.Units;
                        sh.X_axis_correction = ang.Correction; // TODODONE : Check for x or y
                        //sh.Correction = tq.Correction;*/
                        sh.Correction = bip.Correction;
                    }
                }
            }
        }
        private int GetMaxAxisValue(byte[] allBytes, SymbolHelper sh, MapViewerEx.AxisIdent axisIdent)
        {
            int retval = 0;
            if (axisIdent == MapViewerEx.AxisIdent.X_Axis)
            {
                //read x axis values
                int offset = sh.X_axis_address;
                for (int i = 0; i < sh.X_axis_length; i++)
                {
                    int val = Convert.ToInt32(allBytes[offset+1]) + Convert.ToInt32(allBytes[offset]) * 256;
                    if (val > retval) retval = val;
                    offset += 2;
                }
            }
            else if (axisIdent == MapViewerEx.AxisIdent.Y_Axis)
            {
                //read y axis values
                int offset = sh.Y_axis_address;
                for (int i = 0; i < sh.Y_axis_length; i++)
                {
                    int val = Convert.ToInt32(allBytes[offset+1]) + Convert.ToInt32(allBytes[offset]) * 256;
                    if (val > retval) retval = val;
                    offset += 2;
                }
            }
            else if (axisIdent == MapViewerEx.AxisIdent.Z_Axis)
            {
                //read Z axis values
                int offset = (int)sh.Flash_start_address;
                for (int i = 0; i < sh.Length/2 ; i++)
                {
                    int val = Convert.ToInt32(allBytes[offset + 1]) + Convert.ToInt32(allBytes[offset]) * 256;
                    if (val > retval) retval = val;
                    offset += 2;
                }
            }
            return retval;
        }
        private float GetMaxMapValue(byte[] allBytes, SymbolHelper sh)
        {
            float retval = 0;
            //read Z axis values
            int offset = (int)sh.Flash_start_address;
            for (int i = 0; i < sh.Length/2; i++)
            {
                float val = Convert.ToInt32(allBytes[offset + 1]) + Convert.ToInt32(allBytes[offset]) * 256;
                if (val > retval) retval = val;
                offset += 2;
            }
            return retval;
        }
        private bool CheckAxisValues(byte[] allBytes, SymbolHelper sh, MapViewerEx.AxisIdent axisIdent, float minStep = 0, float maxStep=1000)
        {
            int lastval = 0;
            if (axisIdent == MapViewerEx.AxisIdent.X_Axis)
            {
                //read x axis values
                int offset = sh.X_axis_address;
                for (int i = 0; i < sh.X_axis_length; i++)
                {
                    int val = Convert.ToInt32(allBytes[offset + 1]) + Convert.ToInt32(allBytes[offset]) * 256;
                    if (i == 0) { lastval = val; offset += 2; continue; }
                    if (lastval > val) return false;
                    if (val - lastval < minStep || val - lastval > maxStep) return false;
                    lastval = val;
                    offset += 2;
                }
            }
            else if (axisIdent == MapViewerEx.AxisIdent.Y_Axis)
            {
                //read y axis values
                int offset = sh.Y_axis_address;
                for (int i = 0; i < sh.Y_axis_length; i++)
                {
                    int val = Convert.ToInt32(allBytes[offset + 1]) + Convert.ToInt32(allBytes[offset]) * 256;
                    if (i == 0) { lastval = val; offset += 2; continue; }
                    if (lastval > val) return false;
                    if (val - lastval < minStep || val - lastval > maxStep) return false;
                    lastval = val;
                    offset += 2;
                }
            }     
            else if (axisIdent == MapViewerEx.AxisIdent.Z_Axis)
            {
                //read y axis values
                int offset = (int)sh.Flash_start_address;
                for (int i = 0; i < sh.Length / 2; i++)
                {
                    int val = Convert.ToInt32(allBytes[offset + 1]) + Convert.ToInt32(allBytes[offset]) * 256;
                    if (i == 0) { lastval = val; offset += 2; continue; }
                    if (lastval > val) return false;
                    if (val - lastval < minStep || val - lastval > maxStep) return false;
                    lastval = val;
                    offset += 2;
                }
            }     
            return true;
        }
        private bool CheckMapFlat(byte[] allBytes, SymbolHelper sh)
        {
            int retval = 0;
            //read Z axis values
            int offset = (int)sh.Flash_start_address;
            for (int i = 0; i < sh.Length / 2; i++)
            {
                int val = Convert.ToInt32(allBytes[offset + 1]) + Convert.ToInt32(allBytes[offset]) * 256;
                if (i == 0) retval = val;
                if (val != retval) return false;
                offset += 2;
            }
            return true;
        }
        private bool CollectionContainsMapInSize(SymbolCollection newSymbols, int ysize, int xsize)
        {
            foreach (SymbolHelper sh in newSymbols)
            {
                if (sh.Y_axis_length == ysize && sh.X_axis_length == xsize) return true;
            }
            return false;
        }
        private int GetMapNameCountForCodeBlock(string varName, int codeBlock, SymbolCollection newSymbols, bool debug)
        {
            int count = 0;
            if (debug) Console.WriteLine("Check " + varName + " " + codeBlock);

            foreach (SymbolHelper sh in newSymbols)
            {
                if (debug)
                {
                    if (!sh.Varname.StartsWith("2D") && !sh.Varname.StartsWith("3D"))
                    {
                        Console.WriteLine(sh.Varname + " " + sh.CodeBlock);
                    }
                }
                if (sh.Varname.StartsWith(varName) && sh.CodeBlock == codeBlock)
                {

                    if (debug) Console.WriteLine("Found " + sh.Varname + " " + sh.CodeBlock);

                    count++;
                }
            }
            count++;
            return count;
        }

    }
}
