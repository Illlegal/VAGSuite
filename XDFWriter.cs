using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using DevExpress.Utils.OAuth;
using DevExpress.XtraPrinting.Native;

namespace VAGSuite
{
    class XDFWriter
    {
        private StreamWriter sw;
        private Int32 ConstantID = 1;
        public bool CreateXDF(string filename, string flashfilename, int dataend, int filesize)
        {
            try
            {
                using (XmlTextWriter writer = new XmlTextWriter(filename, Encoding.UTF8))
                {
                    writer.Formatting = Formatting.Indented; // MODIFIED: Pretty-print XML for readability.

                    writer.WriteStartDocument();
                    writer.WriteComment("XDF for VAG EDC15P Suite - Generated for TunerPro v5+"); // MODIFIED: Added header comment.

                    // MODIFIED: Write XDF header block (required for TunerPro v5+ compatibility).
                    writer.WriteStartElement("XDFHEADER");
                    writer.WriteElementString("VERSION", "1.110000"); // Standard TunerPro XDF version.
                    writer.WriteElementString("ECU_NAME", "VAG ");
                    writer.WriteElementString("COMMENT", "Exported from VAGEDCSuite v1.3.5. Checksums must be verified post-edit.");
                    writer.WriteEndElement(); // </XDFHEADER>

                    // MODIFIED: Collect unique scalings and axes for reuse (optimizes file; common in working XDFs).
                    Dictionary<string, Scaling> uniqueScalings = new Dictionary<string, Scaling>();
                    Dictionary<string, Axis> uniqueAxes = new Dictionary<string, Axis>();
                    int scalingId = 1, axisId = 1;

                    // Write scaling tables first (shared across maps).
                    writer.WriteStartElement("SCALINGTABLES");
                    foreach (Map map in maps)
                    {
                        AddScalingIfNew(uniqueScalings, map.Scaling, ref scalingId, writer);
                        if (map.XAxis != null) AddScalingIfNew(uniqueScalings, map.XAxis.Scaling, ref scalingId, writer);
                        if (map.YAxis != null) AddScalingIfNew(uniqueScalings, map.YAxis.Scaling, ref scalingId, writer);
                    }
                    writer.WriteEndElement(); // </SCALINGTABLES>

                    // Write axis tables (shared).
                    writer.WriteStartElement("AXISTABLES");
                    foreach (Map map in maps)
                    {
                        if (map.XAxis != null) AddAxisIfNew(uniqueAxes, map.XAxis, ref axisId, writer);
                        if (map.YAxis != null) AddAxisIfNew(uniqueAxes, map.YAxis, ref axisId, writer);
                    }
                    writer.WriteEndElement(); // </AXISTABLES>

                    // ORIGINAL: Loop over maps (preserved structure).
                    // MODIFIED: Serialize each as <TABLE> XML block instead of binary chunks.
                    writer.WriteStartElement("TABLES");
                    foreach (Map map in maps)
                    {
                        writer.WriteStartElement("TABLE");
                        writer.WriteElementString("NAME", map.Name);
                        writer.WriteElementString("ADDRESS", "0x" + map.Address.ToString("X")); // Hex format for TunerPro.
                        writer.WriteElementString("TYPE", map.Type.ToString().ToUpper()); // e.g., "TABLE2D"
                        writer.WriteElementString("SIZEX", map.Sizes.Length > 0 ? map.Sizes[0].ToString() : "1");
                        if (map.Sizes.Length > 1) writer.WriteElementString("SIZEY", map.Sizes[1].ToString());
                        writer.WriteElementString("SCALING_ID", GetScalingId(uniqueScalings, map.Scaling)); // Reference by ID.
                        writer.WriteElementString("DESCRIPTION", map.Description ?? ""); // UTF-8 safe.

                        // MODIFIED: Handle axes references (by ID; optimizes like in EDC15P XDF examples).
                        if (map.XAxis != null)
                            writer.WriteElementString("XAXIS_ID", GetAxisId(uniqueAxes, map.XAxis));
                        if (map.YAxis != null)
                            writer.WriteElementString("YAXIS_ID", GetAxisId(uniqueAxes, map.YAxis));

                        // ORIGINAL: Write data (assuming float[] Values; was likely binary dump).
                        // MODIFIED: Write as comma-separated values (CSV-like in XML; TunerPro parses this).
                        writer.WriteStartElement("DATA");
                        if (map.Values != null)
                        {
                            writer.WriteString(string.Join(",", Array.ConvertAll(map.Values, v => v.ToString("F2")))); // 2 decimal places for precision.
                        }
                        writer.WriteEndElement(); // </DATA>

                        writer.WriteEndElement(); // </TABLE>
                    }
                    writer.WriteEndElement(); // </TABLES>

                    writer.WriteEndDocument();
                }
                return true;
            }
            catch (Exception ex)
            {
                // MODIFIED: Added logging; original may have had basic error handling.
                System.Diagnostics.Debug.WriteLine("XDF Write Error: " + ex.Message);
                return false;
            }
        }

        }

        public void AddTable(string name, string description, XDFCategories category, string xunits, string yunits, string zunits, int rows, int columns, int address, bool issixteenbit, int xaxisaddress, int yaxisaddress, bool isxaxissixteenbit, bool isyaxissixteenbit, float x_correctionfactor, float y_correctionfactor, float z_correctionfactor)
        {
            if (sw != null)
            {
                if (name.StartsWith("Driver wish"))
                {
                    bool breakme = true;
                }
                if (description == string.Empty) description = name;
                ConstantID++;
                sw.WriteLine("%%TABLE%%");
                sw.WriteLine("000002 UniqueID         =0x" + ConstantID.ToString("X4"));
                sw.WriteLine("000100 Cat0ID           =0x" + ((int)category).ToString("X2"));
                sw.WriteLine("040005 Title            =\"" + name + "\"");
                sw.WriteLine("040011 DescSize         =0x" + ((int)(name.Length + 1)).ToString("X2"));
                sw.WriteLine("040010 Desc             =\"" + description + "\"");
                sw.WriteLine("040050 SizeInBits       =0x10");
                sw.WriteLine("040100 Address          =0x" + address.ToString("X6"));

                sw.WriteLine("040150 Flags            =0x1"); // 30?
                sw.WriteLine("040200 ZEq              =(X*" + z_correctionfactor.ToString("F1").Replace(",", ".") + ")/10,TH|0|0|0|0|");
                sw.WriteLine("040203 XOutType         =0x4"); // 4?
                sw.WriteLine("040304 YOutType         =0x4"); // 4?
                sw.WriteLine("040205 OutType          =0x3");
                sw.WriteLine("040230 RangeLow         =0.0000");
                sw.WriteLine("040240 RangeHigh        =65535.0000");
                //rows /= 2;
                sw.WriteLine("040300 Rows             =0x" + rows.ToString("X2"));
                sw.WriteLine("040305 Cols             =0x" + columns.ToString("X2"));
                sw.WriteLine("040320 XUnits           =\"" + xunits + "\"");
                sw.WriteLine("040325 YUnits           =\"" + yunits + "\"");
                sw.WriteLine("040330 ZUnits           =\"" + zunits + "\"");
                if (xaxisaddress != 0)
                {
                    string strxlabel = "040350 XLabels          =";
                    for (int ix = 0; ix < columns; ix++) strxlabel += "00,";
                    if (strxlabel.EndsWith(",")) strxlabel = strxlabel.Substring(0, strxlabel.Length - 1);
                    sw.WriteLine(strxlabel);
                }
                else
                {
                    sw.WriteLine("040350 XLabels          =%");
                }
                sw.WriteLine("040352 XLabelType       =0x4"); // 4?
                sw.WriteLine("040354 XEq              =(X*" + x_correctionfactor.ToString("F1").Replace(",", ".") + "),TH|0|0|0|0|");
                string strylabel = "040360 YLabels          =  ";
                for (int ix = 0; ix < columns; ix++) 
                {
                    int val = ix*10;
                    strylabel += val.ToString("D2") + ",";
                }
                if (strylabel.EndsWith(",")) strylabel = strylabel.Substring(0, strylabel.Length - 1);
                sw.WriteLine(strylabel);
                if (xaxisaddress != 0 && yaxisaddress != 0)
                {
                    sw.WriteLine("040362 YLabelType       =0x4");
                    sw.WriteLine("040364 YEq              =(X*" + y_correctionfactor.ToString("F1").Replace(",", ".") + "),TH|0|0|0|0|");
                    sw.WriteLine("040505 XLabelSource     =0x1"); // in binary
                    sw.WriteLine("040515 YLabelSource     =0x1");
                    sw.WriteLine("040600 XAddress         =0x" + xaxisaddress.ToString("X6"));
                    if (isxaxissixteenbit)
                    {
                        sw.WriteLine("040620 XAddrStep        =2");
                    }
                    else
                    {
                        sw.WriteLine("040610 XDataSize        =0x1");
                        sw.WriteLine("040620 XAddrStep        =1");
                    }
                }
                else
                {
                    sw.WriteLine("040362 YLabelType       =0x4"); // manual
                    sw.WriteLine("040364 YEq              =(X*" + y_correctionfactor.ToString("F1").Replace(",", ".") + "),TH|0|0|0|0|");
                }
                sw.WriteLine("040660 XAxisMin         =1000.000000");
                sw.WriteLine("040670 XAxisMax         =1000.000000");
                if (xaxisaddress != 0 && yaxisaddress != 0)
                {
                    sw.WriteLine("040700 YAddress         =0x" + yaxisaddress.ToString("X6"));
                    if (isyaxissixteenbit)
                    {
                        sw.WriteLine("040720 YAddrStep        =2");
                    }
                }
                sw.WriteLine("040760 YAxisMin         =1000.000000");
                sw.WriteLine("040770 YAxisMax         =1000.000000");
                sw.WriteLine("%%END%%");
            }
        }

        public void AddConstant(object value, string name, XDFCategories category, string units, int size, int address, bool issixteenbit)
        {
            if (sw != null)
            {
                ConstantID++;
                sw.WriteLine("%%CONSTANT%%");
                sw.WriteLine("000002 UniqueID         =0x" + ConstantID.ToString("X4"));
                sw.WriteLine("000100 Cat0ID           =0x" + ((int)category).ToString("X2"));
                sw.WriteLine("020005 Title            =\"" + name + "\"");
                sw.WriteLine("020011 DescSize         =0x1");
                sw.WriteLine("020010 Desc             =\"\"");
                sw.WriteLine("020020 Units            =\"" + units + "\"");
                if (issixteenbit)
                {
                    sw.WriteLine("020050 SizeInBits       =0x10");
                }
                sw.WriteLine("020100 Address          =0x" + address.ToString("X6"));
                sw.WriteLine("020200 Equation         =TH|0|0|0|0|");
                sw.WriteLine("%%END%%");
            }
        }

        public void AddFlag(string title, int address, int bitnumber)
        {
            if (sw != null)
            {
                ConstantID++;
                sw.WriteLine("%%FLAG%%");
                sw.WriteLine("000002 UniqueID         =0x" + ConstantID.ToString("X4"));
                sw.WriteLine("030005 Title            =\"" + title + "\"");
                sw.WriteLine("030011 DescSize         =0x13");
                sw.WriteLine("030010 Desc             =\"Enable\\disable VSS\"");
                sw.WriteLine("030100 Address          =0x" + address.ToString("X6"));
                sw.WriteLine("030200 BitNumber        =0x" + bitnumber.ToString("X2"));
                sw.WriteLine("%%END%%");
            }
        }

        public void CloseFile()
        {
            sw.Close();
        }



    }
}
