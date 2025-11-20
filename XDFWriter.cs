using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using DevExpress.Utils.OAuth;
using DevExpress.XtraPrinting.Native;
using static DevExpress.Utils.Drawing.Helpers.NativeMethods;
using System.Globalization;
using DevExpress.XtraPrinting.Export.Pdf;
using Microsoft.Office.Interop.Excel;
using static Nevron.Interop.Win32.NGdi32;
using System.Drawing;
using System.Security.Cryptography;
using System.Collections.ObjectModel;

namespace VAGSuite
{
    //class to categorize XDF. Based on class in MapViewerEx.cs
    internal class XDFCategory
    {
        public XDFCategories Category { get; set; }
        public int Id { get; set; }
        public string Name { get; set; }

        public XDFCategory(int id, string name, XDFCategories category)
        {
            Category = category;
            Id = id;
            Name = name;
        }
    };
    class XDFWriter 
    {
        private XmlWriter xw;
        private int UniqueID = 1;
        //private XmlWriter writer;
        private string filePath;
        // private int uniqueId = 100;
        private bool lsbfirst;//very fuckin important
        private XDFCategory[] XDFcategories = new XDFCategory[] //To show category in XDF
            {
                //Main categories
                new XDFCategory(0,"", XDFCategories.Undocumented),
                new XDFCategory(1,"Fuel", XDFCategories.Fuel),
                new XDFCategory(2,"Turbo", XDFCategories.Turbo),
                new XDFCategory(3,"Torque", XDFCategories.Torque),
                new XDFCategory(4,"Misc", XDFCategories.Misc),
                //Subcategories
                new XDFCategory(5,"Limiters", XDFCategories.Limiters),
                new XDFCategory(6,"Correction", XDFCategories.Correction),
                new XDFCategory(7,"Target Boost", XDFCategories.Undocumented),
                new XDFCategory(8,"Target torque", XDFCategories.Undocumented),
                new XDFCategory(9,"EGR", XDFCategories.Undocumented),
                new XDFCategory(10,"Injector duration", XDFCategories.Undocumented)



            };
        public int rtnCategoryIndexByName(string Category)
        {
            foreach (XDFCategory xdfcategories in XDFcategories)
            {
                if (Category == xdfcategories.Name)
                {
                    return xdfcategories.Id+1;//Weird
                }
            }
            return 0;
        }
        public void CreateXDF(string filename, string flashfilename, int dataend, int filesize, bool lsb)
        {
            var settings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "  ",
                NewLineOnAttributes = false,
                Encoding = new UTF8Encoding(false)
                //Encoding = System.Text.Encoding.UTF8
            };

            lsbfirst = lsb; //important, for later. like in addTable func etc.

            xw = XmlWriter.Create(filename, settings);
            xw.WriteStartDocument();
            xw.WriteComment("XDF for VAG");
            xw.WriteStartElement("XDFFORMAT"); // Standard TunerPro XDF version.  "1.80"
            xw.WriteAttributeString("version", "1.80");

            xw.WriteStartElement("XDFHEADER");
            xw.WriteElementString("flags", "0x1");
            xw.WriteElementString("author", "Dilemma");

            xw.WriteStartElement("BASEOFFSET");
            xw.WriteAttributeString("offset", "0");
            xw.WriteAttributeString("subtract", "0");
            xw.WriteEndElement();

            xw.WriteStartElement("DEFAULTS");
            xw.WriteAttributeString("datasizeinbits", "16");
            xw.WriteAttributeString("sigdigits", "2");
            xw.WriteAttributeString("outputtype", "1");
            xw.WriteAttributeString("signed", "1");
            xw.WriteAttributeString("lsbfirst", lsbfirst ? "1" : "0");
            xw.WriteAttributeString("float", "0");
            xw.WriteEndElement();

            xw.WriteStartElement("REGION");
            xw.WriteAttributeString("type", "0xFFFFFF");
            xw.WriteAttributeString("startaddress", "0x0");
            xw.WriteAttributeString("size", "0xFFFFFF");
            xw.WriteAttributeString("regioncolor", "0x0");
            xw.WriteAttributeString("regionflags", "0x0");
            xw.WriteAttributeString("name", "Binary File");
            xw.WriteAttributeString("desc", "This region describes the bin file edited by this XDF");
            xw.WriteEndElement();

            foreach(XDFCategory xdfcategories in XDFcategories){
                if (xdfcategories.Id != 0)
                {
                    xw.WriteStartElement("CATEGORY");
                    xw.WriteAttributeString("index", "0x" + xdfcategories.Id.ToString("X2"));
                    xw.WriteAttributeString("name", xdfcategories.Name);
                    xw.WriteEndElement();
                }
            }

            xw.WriteEndElement(); // XDFHEADER
        }

        public void AddTable(string name, string description, string ACategory, string BCategory, 
            string xunits, string yunits, string zunits, 
            int rows, int columns, int address, bool issixteenbit, 
            int xaxisaddress, int yaxisaddress, 
            bool isxaxissixteenbit, bool isyaxissixteenbit, 
            float x_correctionfactor, float y_correctionfactor, float z_correctionfactor,
            float XFactor, float YFactor, float ZFactor)
        {
            if (xw != null)
            {
                if (description == string.Empty) description = name;
                UniqueID++;
                xw.WriteStartElement("XDFTABLE");
                xw.WriteAttributeString("uniqueid", "0x"+ UniqueID.ToString("X4"));// + 
                xw.WriteAttributeString("flags", "0x0");
                xw.WriteElementString("title", name);
                xw.WriteElementString("description", description);

                if (rtnCategoryIndexByName(ACategory) != 0)
                {
                    xw.WriteStartElement("CATEGORYMEM");
                    xw.WriteAttributeString("index", "0");
                    xw.WriteAttributeString("category", rtnCategoryIndexByName(ACategory).ToString());
                    xw.WriteEndElement();
                }
                if (rtnCategoryIndexByName(BCategory) != 0)
                {
                    xw.WriteStartElement("CATEGORYMEM");
                    xw.WriteAttributeString("index", "1");
                    xw.WriteAttributeString("category", rtnCategoryIndexByName(BCategory).ToString());
                    xw.WriteEndElement();
                }

                

                // X axis
                xw.WriteStartElement("XDFAXIS");
                xw.WriteAttributeString("id", "x");
                xw.WriteAttributeString("uniqueid", "0x0");
                xw.WriteStartElement("EMBEDDEDDATA");
                xw.WriteAttributeString("mmedtypeflags", lsbfirst ? "0x02" : "0x00");     //00/02 or 01/03 for signed
                if (xaxisaddress != 0)
                {
                xw.WriteAttributeString("mmedaddress", "0x" + xaxisaddress.ToString("X6")); //sh.X_axis_address.ToString("X6")
                }
                xw.WriteAttributeString("mmedelementsizebits", isxaxissixteenbit ? "16" : "8");
                xw.WriteAttributeString("mmedcolcount", columns.ToString());
                xw.WriteAttributeString("mmedmajorstridebits", "0");
                xw.WriteAttributeString("mmedminorstridebits", "0");
                xw.WriteEndElement();
                xw.WriteElementString("indexcount", columns.ToString());
                xw.WriteStartElement("embedinfo"); // "1"
                xw.WriteAttributeString("type", "1");
                xw.WriteEndElement();
                //xw.WriteElementString("outputtype", "6");
                xw.WriteElementString("datatype", isxaxissixteenbit ? "6" : "0");
                xw.WriteElementString("unittype", "4");
                xw.WriteStartElement("DALINK");
                xw.WriteAttributeString("index", "0");
                xw.WriteEndElement();
                //xw.WriteElementString("min", "0.000000");
                //xw.WriteElementString("max", "65535.000000");
                //xw.WriteElementString("decplaces", "0");
                xw.WriteStartElement("MATH");
                xw.WriteAttributeString("equation", "X*" + x_correctionfactor.ToString("F6").Replace(",", ".") + "+" + XFactor);//
                xw.WriteStartElement("VAR");
                xw.WriteAttributeString("id", "X");
                xw.WriteEndElement();
                xw.WriteEndElement();
                //xw.WriteElementString("units", xunits);
                if (xaxisaddress == 2137)
                {
                    for (int i = 0; i < columns; i++)
                    {
                        xw.WriteStartElement("LABEL");
                        xw.WriteAttributeString("index", i.ToString());
                        xw.WriteAttributeString("value", (i * 10).ToString());
                        xw.WriteEndElement();
                    }
                }
                xw.WriteEndElement(); // XDFAXIS x

                // Y axis
                xw.WriteStartElement("XDFAXIS");
                xw.WriteAttributeString("id", "y");
                xw.WriteAttributeString("uniqueid", "0x0");
                xw.WriteStartElement("EMBEDDEDDATA");
                xw.WriteAttributeString("mmedtypeflags", lsbfirst ? "0x02" : "0x00");
                if (yaxisaddress != 0)
                {
                    xw.WriteAttributeString("mmedaddress", "0x" + yaxisaddress.ToString("X6"));
                }
                xw.WriteAttributeString("mmedelementsizebits", isyaxissixteenbit ? "16" : "8");
                xw.WriteAttributeString("mmedrowcount", rows.ToString());
                xw.WriteAttributeString("mmedmajorstridebits", "0");
                xw.WriteAttributeString("mmedminorstridebits", "0");
                xw.WriteEndElement();
                xw.WriteElementString("indexcount", rows.ToString());
                xw.WriteStartElement("embedinfo"); // "1"
                xw.WriteAttributeString("type", "1");
                xw.WriteEndElement();
                //xw.WriteElementString("outputtype", "4");
                xw.WriteElementString("datatype", isyaxissixteenbit ? "2" : "0");
                xw.WriteElementString("unittype", "0");
                xw.WriteStartElement("DALINK");
                xw.WriteAttributeString("index", "0");
                xw.WriteEndElement();
                //xw.WriteElementString("min", "0.000000");
                //xw.WriteElementString("max", "65535.000000");
                //xw.WriteElementString("decplaces", "0");
                xw.WriteStartElement("MATH");
                xw.WriteAttributeString("equation", "X*" + y_correctionfactor.ToString("F6").Replace(",", ".") + "+" + YFactor);
                xw.WriteStartElement("VAR");
                xw.WriteAttributeString("id", "X");
                xw.WriteEndElement();
                xw.WriteEndElement();
                //xw.WriteElementString("units", yunits);
                if (yaxisaddress == 2137)
                {
                    for (int i = 0; i < rows; i++)
                    {
                        xw.WriteStartElement("LABEL");
                        xw.WriteAttributeString("index", i.ToString());
                        xw.WriteAttributeString("value", (i * 10).ToString());
                        xw.WriteEndElement();
                    }
                }
                xw.WriteEndElement(); // XDFAXIS y

                // Z axis
                xw.WriteStartElement("XDFAXIS");
                xw.WriteAttributeString("id", "z");
                xw.WriteAttributeString("uniqueid", "0x0");
                xw.WriteStartElement("EMBEDDEDDATA");
                xw.WriteAttributeString("mmedtypeflags", lsbfirst ? "0x03" : "0x01");//signed
                xw.WriteAttributeString("mmedaddress", "0x" + address.ToString("X6"));
                xw.WriteAttributeString("mmedcolcount", columns.ToString());
                xw.WriteAttributeString("mmedrowcount", rows.ToString());
                xw.WriteAttributeString("mmedelementsizebits", issixteenbit ? "16" : "8");
                xw.WriteAttributeString("mmedmajorstridebits", "0");//(columns * (issixteenbit ? 16 : 8)).ToString()
                xw.WriteAttributeString("mmedminorstridebits", "0");
                xw.WriteEndElement();
                xw.WriteElementString("outputtype", "1");
                xw.WriteElementString("datatype", issixteenbit ? "2" : "0");
                xw.WriteElementString("unittype", "0");
                xw.WriteElementString("min", "0.000000");
                xw.WriteElementString("max", "65535.000000");
                xw.WriteElementString("decplaces", "2");
                xw.WriteStartElement("MATH");
                xw.WriteAttributeString("equation", "X*" + z_correctionfactor.ToString("F6").Replace(",", ".") + "+" + ZFactor);
                xw.WriteStartElement("VAR");
                xw.WriteAttributeString("id", "X");
                xw.WriteEndElement();
                xw.WriteEndElement();
                xw.WriteElementString("units", zunits);
                xw.WriteEndElement(); // XDFAXIS z

                xw.WriteEndElement(); // XDFTABLE
            }
        }

        public void AddConstant(object value, string name, XDFCategories category, string units, int size, int address, bool issixteenbit)
        {
            if (xw != null)
            {
                UniqueID++;
                xw.WriteStartElement("XDFCONSTANT");
                xw.WriteAttributeString("uniqueid", "0x" + UniqueID.ToString("X4"));
                xw.WriteAttributeString("flags", "0x0");
                xw.WriteElementString("title", name);
                xw.WriteElementString("description", "");
                xw.WriteStartElement("CATEGORYMEM");
                xw.WriteAttributeString("index", "0");
                xw.WriteAttributeString("category", ((int)category + 1).ToString());
                xw.WriteEndElement();
                xw.WriteStartElement("EMBEDDEDDATA");
                xw.WriteAttributeString("mmedaddress", "0x" + address.ToString("X6"));
                xw.WriteAttributeString("mmedelementsizebits", issixteenbit ? "16" : "8");
                xw.WriteAttributeString("mmedmajorstridebits", "0");
                xw.WriteAttributeString("mmedminorstridebits", "0");
                xw.WriteEndElement();
                xw.WriteElementString("outputtype", "1");
                xw.WriteElementString("datatype", issixteenbit ? "2" : "0");
                xw.WriteElementString("unittype", "0");
                xw.WriteStartElement("DALINK");
                xw.WriteAttributeString("index", "0");
                xw.WriteEndElement();
                xw.WriteElementString("min", "0.000000");
                xw.WriteElementString("max", "65535.000000");
                xw.WriteElementString("decplaces", "0");
                xw.WriteStartElement("MATH");
                xw.WriteAttributeString("equation", "X");
                xw.WriteStartElement("VAR");
                xw.WriteAttributeString("id", "X");
                xw.WriteEndElement();
                xw.WriteEndElement();
                xw.WriteElementString("units", units);
                xw.WriteEndElement(); // XDFCONSTANT
            }
        }

        public void AddFlag(string title, int address, int bitnumber)
        {
            if (xw != null)
            {
                UniqueID++;
                xw.WriteStartElement("XDFCONSTANT");
                xw.WriteAttributeString("uniqueid", "0x" + UniqueID.ToString("X4"));
                xw.WriteAttributeString("flags", "0x0");
                xw.WriteElementString("title", title);
                xw.WriteElementString("description", "Enable/disable VSS");
                xw.WriteStartElement("EMBEDDEDDATA");
                xw.WriteAttributeString("mmedtypeflags", "0x08");
                xw.WriteAttributeString("mmedaddress", "0x" + address.ToString("X6"));
                xw.WriteAttributeString("mmedelementsizebits", "8");
                xw.WriteAttributeString("mmedmajorstridebits", "0");
                xw.WriteAttributeString("mmedminorstridebits", "0");
                xw.WriteEndElement();
                xw.WriteElementString("outputtype", "5"); // boolean output
                xw.WriteElementString("datatype", "0");
                xw.WriteElementString("unittype", "0");
                xw.WriteStartElement("DALINK");
                xw.WriteAttributeString("index", "0");
                xw.WriteEndElement();
                xw.WriteStartElement("MATH");
                xw.WriteAttributeString("equation", "X." + bitnumber.ToString());
                xw.WriteStartElement("VAR");
                xw.WriteAttributeString("id", "X");
                xw.WriteEndElement();
                xw.WriteEndElement();
                xw.WriteEndElement(); // XDFCONSTANT
            }
        }

        public void CloseFile()
        {
            if (xw != null)
            {
                xw.WriteEndElement(); // XDFFORMAT
                xw.WriteEndDocument();
                xw.Flush();
                xw.Close();
                xw = null;
            }
        }
    }
}
