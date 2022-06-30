using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Linq;

namespace BombermanObjectCompiler
{
    /*
     * TODO
     * ----
     * - Generate Basic Header without DL offset (just make it 0)
     * - Add in textures and fill in textureoffsets
     * - Add in vertices and fill in verticeoffsets
     * - Parse every displaylist
     * - Add them to the byte array
     * - Write byte array and be happy :)
     */

    internal class Program
    {
        public struct VTX
        {
            public VTX_Item[] Vertex;
            public UInt64 VertexOffset;
            public string Identifier;
        }
        public struct VTX_Item
        {
            public Vector3 Pos;
            public ushort flag;
            public Vector2 Coords;
            public Vector4 Colours;
        }

        public struct Tex
        {
            public UInt64[] Texture;
            public UInt64 TexOffset;
            public string Identifier;
        }

        public struct DL
        {
            public int Offset;
            public byte[] Data;
        }

        public static int MainDLOffset;
        public static int CurDLOffset;
        public static Dictionary<string, byte> ImageFormats = new Dictionary<string, byte>();
        public static Dictionary<string, byte> SizeFormats = new Dictionary<string, byte>();

        static void Main(string[] args)
        {
            if(args.Length < 1)
            {
                Console.WriteLine("Please give the path of the folder containing the model.inc.c and header.h file in the arguments.");
                Environment.Exit(1);
            }

            Console.WriteLine("Compiling...");
            try
            {
                string[] Header = File.ReadAllLines((args[0] + "\\header.h"));
                string[] MainFile = File.ReadAllLines((args[0] + "\\model.inc.c"));

                List<byte> EndResult = new List<byte>();
                /*
                 * u64 = texture 
                 * VTX = vertices
                 * GFX = DL
                 * main DL = last one
                */
                Dictionary<string, Tex> TexturePairs = new Dictionary<string, Tex>();
                Dictionary<string, VTX> Vertices = new Dictionary<string, VTX>();
                List<byte> OutData = new List<byte>();
                int Index = 0;

                foreach(string line in Header)
                {
                    switch(line.Split(' ')[1])
                    {
                        case "u64":
                            {
                                ParseTexture(MainFile, line, ref TexturePairs);
                                break;
                            }
                        case "Vtx":
                            {
                                ParseVTX(MainFile, line, ref Vertices);
                                break;
                            }
                        case "Gfx":
                            {
                                MainDLOffset = FindResourceLine(MainFile, TrimExcess(line));
                                MainDLOffset = MainDLOffset + 1;
                                break;
                            }
                    }
                    Index++;
                }

                MainDLOffset = FindResourceLine(MainFile, "Gfx " +TrimExcess(Header[Header.Count() - 1].Split(' ')[2]) + "[] =");
                MainDLOffset++;

                Console.WriteLine("Textures & Vertices parsed...");

                OutData.AddRange(new byte[] { 0x36, 0x34, 0x00, 0x38 });
                OutData.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x01 }); //1 length DL for now
                OutData.AddRange(new byte[] { 0x02, 0x02, 0x02, 0x02 });

                OutData.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 });
                OutData.AddRange(new byte[] { 0x3F, 0x80, 0x00, 0x00 });
                OutData.AddRange(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }); // replace this at the end

                for(int i = 0; i < TexturePairs.Count; i++)
                {
                    Tex tex = TexturePairs.ElementAt(i).Value;
                    tex.TexOffset = (ulong)OutData.Count;
                    TexturePairs[tex.Identifier] = tex;

                    foreach(UInt64 Data in tex.Texture)
                    {
                        byte[] buf = BitConverter.GetBytes(Data);
                        if(BitConverter.IsLittleEndian)
                        {
                            Array.Reverse(buf);
                        }
                        OutData.AddRange(buf);
                    }
                }

                Console.WriteLine("Texture Data parsed");

                for (int i = 0; i < Vertices.Count; i++)
                {
                    VTX vtx = Vertices.ElementAt(i).Value;
                    vtx.VertexOffset = (ulong)OutData.Count;
                    Vertices[vtx.Identifier] = vtx;

                    foreach(VTX_Item item in vtx.Vertex)
                    {
                        short X = (short)item.Pos.X;
                        short Y = (short)item.Pos.Y;
                        short Z = (short)item.Pos.Z;
                        ushort flag = (ushort)item.flag;
                        short TX = (short)item.Coords.X;
                        short TY = (short)item.Coords.Y;
                        byte R = (byte)item.Colours.X;
                        byte G = (byte)item.Colours.Y;
                        byte B = (byte)item.Colours.Z;
                        byte A = (byte)item.Colours.W;

                        byte[] buf = BitConverter.GetBytes(X);
                        if (BitConverter.IsLittleEndian)
                        {
                            Array.Reverse(buf);
                        }
                        OutData.AddRange(buf);

                        buf = BitConverter.GetBytes(Y);
                        if (BitConverter.IsLittleEndian)
                        {
                            Array.Reverse(buf);
                        }
                        OutData.AddRange(buf);

                        buf = BitConverter.GetBytes(Z);
                        if (BitConverter.IsLittleEndian)
                        {
                            Array.Reverse(buf);
                        }
                        OutData.AddRange(buf);

                        buf = BitConverter.GetBytes(flag);
                        if (BitConverter.IsLittleEndian)
                        {
                            Array.Reverse(buf);
                        }
                        OutData.AddRange(buf);

                        buf = BitConverter.GetBytes(TX);
                        if (BitConverter.IsLittleEndian)
                        {
                            Array.Reverse(buf);
                        }
                        OutData.AddRange(buf);

                        buf = BitConverter.GetBytes(TY);
                        if (BitConverter.IsLittleEndian)
                        {
                            Array.Reverse(buf);
                        }
                        OutData.AddRange(buf);

                        OutData.Add(R);
                        OutData.Add(G);
                        OutData.Add(B);
                        OutData.Add(A);
                    }
                }

                Console.WriteLine("Vertex Data parsed");

                #region DEFINES
                ImageFormats.Add("G_IM_FMT_RGBA", 0);
                ImageFormats.Add("G_IM_FMT_YUV", 1);
                ImageFormats.Add("G_IM_FMT_CI", 2);
                ImageFormats.Add("G_IM_FMT_IA", 3);
                ImageFormats.Add("G_IM_FMT_I", 4);

                SizeFormats.Add("G_IM_SIZ_4b", 0);
                SizeFormats.Add("G_IM_SIZ_8b", 1);
                SizeFormats.Add("G_IM_SIZ_16b", 2);
                SizeFormats.Add("G_IM_SIZ_32b", 3);
                SizeFormats.Add("G_IM_SIZ_DD", 5);

                SizeFormats.Add("G_IM_SIZ_4b_BYTES", 0);
                SizeFormats.Add("G_IM_SIZ_4b_TILE_BYTES", 0);
                SizeFormats.Add("G_IM_SIZ_4b_LINE_BYTES", 0);

                SizeFormats.Add("G_IM_SIZ_8b_BYTES", 1);
                SizeFormats.Add("G_IM_SIZ_8b_TILE_BYTES", 1);
                SizeFormats.Add("G_IM_SIZ_8b_LINE_BYTES", 1);

                SizeFormats.Add("G_IM_SIZ_16b_BYTES", 2);
                SizeFormats.Add("G_IM_SIZ_16b_TILE_BYTES", 2);
                SizeFormats.Add("G_IM_SIZ_16b_LINE_BYTES", 2);

                SizeFormats.Add("G_IM_SIZ_32b_BYTES", 4);
                SizeFormats.Add("G_IM_SIZ_32b_TILE_BYTES", 2);
                SizeFormats.Add("G_IM_SIZ_32b_LINE_BYTES", 2);

                SizeFormats.Add("G_IM_SIZ_4b_LOAD_BLOCK", 2);
                SizeFormats.Add("G_IM_SIZ_8b_LOAD_BLOCK", 2);
                SizeFormats.Add("G_IM_SIZ_16b_LOAD_BLOCK", 2);
                SizeFormats.Add("G_IM_SIZ_32b_LOAD_BLOCK", 3);

                SizeFormats.Add("G_IM_SIZ_4b_SHIFT", 2);
                SizeFormats.Add("G_IM_SIZ_8b_SHIFT", 1);
                SizeFormats.Add("G_IM_SIZ_16b_SHIFT", 0);
                SizeFormats.Add("G_IM_SIZ_32b_SHIFT", 0);

                SizeFormats.Add("G_IM_SIZ_4b_INCR", 3);
                SizeFormats.Add("G_IM_SIZ_8b_INCR", 1);
                SizeFormats.Add("G_IM_SIZ_16b_INCR", 0);
                SizeFormats.Add("G_IM_SIZ_32b_INCR", 0);
                #endregion

                ParseDL(MainFile, MainDLOffset, TexturePairs, Vertices, ref OutData);

                byte[] buffer = BitConverter.GetBytes(CurDLOffset);
                if(BitConverter.IsLittleEndian)
                {
                    Array.Reverse(buffer);
                }
                OutData[0x14] = buffer[0];
                OutData[0x15] = buffer[1];
                OutData[0x16] = buffer[2];
                OutData[0x17] = buffer[3];

                File.WriteAllBytes(args[0] + "\\outmodel.bin", OutData.ToArray());

                Console.WriteLine("File written to " + args[0] + "\\outmodel.bin");
                Console.WriteLine("Done!");
            }
            catch(Exception ex)
            { 
                Console.WriteLine(ex.ToString()); 
            }
        }
  
        public static void ParseDL(string[] File, int Line, Dictionary<string, Tex> Textures, Dictionary<string, VTX> Vertices, ref List<byte> Model)
        {
            List<byte> OutData = new List<byte>();
            byte[] buf = new byte[1];
            while(buf[0] != 0xB8)
            {
                buf = ParseLine(File, Line, Textures, Vertices, ref Model);
                Line++;
                if(buf[0] != 0x10)
                {
                    OutData.AddRange(buf);
                }                
            }
            CurDLOffset = Model.Count;

            Model.AddRange(OutData);
        }

        /// <summary>
        /// Parses current DL line
        /// </summary>
        /// <param name="File">Full file</param>
        /// <param name="Line">Current line</param>
        /// <param name="Textures">Copy of all textures, for offsets</param>
        /// <param name="Vertices">Copy of all vertices, for offsets</param>
        /// <param name="Model">DO NOT USE TO EDIT MODEL FROM WITHIN FUNCTION. ONLY PASS FOR gsSPDisplayList</param>
        /// <returns>Data to add to the outfile.</returns>
        public static byte[] ParseLine(string[] File, int Line, Dictionary<string, Tex> Textures, Dictionary<string, VTX> Vertices, ref List<byte> Model)
        {
            List<byte> Outdata = new List<byte>();

            switch(GetCommand(File[Line]))
            {
                case "gsSPClearGeometryMode":
                    {
                        Outdata.AddRange(new byte[] { 0xB6, 0x00, 0x00, 0x00 });
                        byte[] buf = BitConverter.GetBytes(GetGeometryMode(File[Line]));
                        if (BitConverter.IsLittleEndian)
                        {
                            Array.Reverse(buf);
                        }
                        Outdata.AddRange(buf);
                        break;
                    }
                case "gsSPVertex":
                    {
                        string[] Params = GetParams(File[Line]);

                        UInt32 VTXLine = (UInt32)Vertices["Vtx " + Params[0].Split('+')[0].Trim()].VertexOffset;
                        VTXLine += UInt32.Parse(Params[0].Split('+')[1].Trim()); //SS values, load in bank 02

                        VTXLine += 0x02000000; //set bank
                        byte N = byte.Parse(Params[1]);
                        byte II = byte.Parse(Params[2]);

                        UInt16 XXXXX = 0;
                        XXXXX = (UInt16)(N << 10);
                        int L = (N * 0x10) - 1;
                        XXXXX |= (UInt16)L;
                        
                        Outdata.Add(0x04);
                        Outdata.Add(II);
                        byte[] buf = BitConverter.GetBytes(XXXXX);
                        if (BitConverter.IsLittleEndian)
                        {
                            Array.Reverse(buf);
                        }
                        Outdata.AddRange(buf);

                        buf = BitConverter.GetBytes(VTXLine);
                        if (BitConverter.IsLittleEndian)
                        {
                            Array.Reverse(buf);
                        }
                        Outdata.AddRange(buf);

                        break;
                    }
                case "gsSPEndDisplayList":
                    {
                        Outdata.AddRange(new byte[] { 0xB8, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });
                        break;
                    }
                case "gsSPCullDisplayList":
                    {
                        Outdata.Add(0xBE);
                        Outdata.Add(0x00);
                        string[] Params = GetParams(File[Line]);

                        ushort VV = ushort.Parse(Params[0].Trim());
                        byte[] buf = BitConverter.GetBytes(VV * 2);
                        if (BitConverter.IsLittleEndian)
                        {
                            Array.Reverse(buf);
                        }
                        Outdata.Add(buf[2]);
                        Outdata.Add(buf[3]);
                        Outdata.AddRange(new byte[] { 0x00, 0x00 });

                        ushort WW = ushort.Parse(Params[1].Trim());
                        buf = BitConverter.GetBytes(WW * 2);
                        if (BitConverter.IsLittleEndian)
                        {
                            Array.Reverse(buf);
                        }
                        Outdata.Add(buf[2]);
                        Outdata.Add(buf[3]);

                        break;
                    }
                case "gsDPPipeSync":
                    {
                        Outdata.AddRange(new byte[] { 0xE7, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });
                        break;
                    }
                case "gsSPSetGeometryMode":
                    {
                        Outdata.AddRange(new byte[] { 0xB7, 0x00, 0x00, 0x00 });
                        byte[] buf = BitConverter.GetBytes(GetGeometryMode(File[Line]));
                        if (BitConverter.IsLittleEndian)
                        {
                            Array.Reverse(buf);
                        }
                        Outdata.AddRange(buf);
                        break;
                    }
                case "gsDPSetCombineLERP":
                    {
                        //haha fuck this for now
                        Outdata.AddRange(new byte[] { 0xFC, 0x12, 0x7E, 0x24, 0xFF, 0xFF, 0xF3, 0xF9 });
                        break;
                    }
                case "gsSPTexture":
                    {
                        string[] Params = GetParams(File[Line]);
                        ushort TTTT = ushort.Parse(Params[0]);
                        ushort SSSS = ushort.Parse(Params[1]);
                        byte NN = byte.Parse(Params[2]);

                        byte LLL = byte.Parse(Params[3]);
                        byte DDD = byte.Parse(Params[4]);

                        Outdata.AddRange(new byte[] { 0xBB, 0x00 });
                        byte XX = (byte)((LLL << 3) | DDD);
                        Outdata.Add(XX);

                        Outdata.Add(NN);

                        byte[] buf = BitConverter.GetBytes(SSSS);
                        if (BitConverter.IsLittleEndian)
                        {
                            Array.Reverse(buf);
                        }
                        Outdata.AddRange(buf);

                        buf = BitConverter.GetBytes(TTTT);
                        if (BitConverter.IsLittleEndian)
                        {
                            Array.Reverse(buf);
                        }
                        Outdata.AddRange(buf);

                        break;
                    }
                case "gsSPDisplayList":
                    {
                        string obj = GetParams(File[Line])[0];
                        obj = "Gfx " + obj + "[]";
                        int LineToGive = FindResourceLine(File, obj);
                        LineToGive++;

                        ParseDL(File, LineToGive, Textures, Vertices, ref Model);
                        Outdata.Add(0x06);
                        Outdata.Add(0x00);
                        Outdata.Add(0x00);
                        Outdata.Add(0x00);

                        int BufOffset = CurDLOffset + 0x02000000;
                        byte[] buf = BitConverter.GetBytes(BufOffset);
                        if (BitConverter.IsLittleEndian)
                        {
                            Array.Reverse(buf);
                        }
                        Outdata.AddRange(buf);

                        break;
                    }
                case "gsDPSetTextureImage":
                    {
                        //oh deary me, I feel like the W param is just poof here
                        string[] Params = GetParams(File[Line]);
                        byte BFormat = ImageFormats[Params[0].Trim()];
                        byte SFormat = SizeFormats[Params[1].Trim()];

                        byte XX = (byte)((BFormat << 5) | (SFormat << 3));

                        int SegAddr = 0x02000000;
                        int AddrToFind = (int)Textures["u64" + Params[3]].TexOffset;
                        SegAddr += AddrToFind;

                        Outdata.Add(0xFD);
                        Outdata.Add(XX);
                        Outdata.Add(0x00);
                        Outdata.Add(0x00);

                        byte[] buf = BitConverter.GetBytes(SegAddr);
                        if (BitConverter.IsLittleEndian)
                        {
                            Array.Reverse(buf);
                        }
                        Outdata.AddRange(buf);

                        break;
                    }
                case "gsDPTileSync":
                    {
                        Outdata.AddRange(new byte[] { 0xE8, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });
                        break;
                    }
                default:
                    {
                        Console.WriteLine("Unknown command: " + File[Line]);
                        Outdata.Add(0x10);
                        break;
                    }
            }

            return Outdata.ToArray();
        }

        public static string[] GetParams(string FullLine)
        {
            string Line = FullLine.Substring(FullLine.IndexOf('(') + 1);
            Line = Line.Substring(0, Line.IndexOf(')'));

            return Line.Split(',');
        }

        public static void ParseTexture(string[] File, string Resource, ref Dictionary<string, Tex> Dict)
        {
            string ItemToFind = TrimExcess(Resource);

            Tex texture = new Tex();
            texture.TexOffset = 0;

            int FileLine = FindResourceLine(File, ItemToFind);

            FileLine++;
            List<UInt64> Values = new List<ulong>();

            while (File[FileLine] != "};")
            {
                //going through function array thing
                string buf = File[FileLine].Trim();
                string[] Items = buf.Split(",");
                for(int i = 0; i < Items.Length - 1; i++)
                {
                    string Clean = Items[i].Replace("0x", "").Trim();
                    UInt64 CurVal = Convert.ToUInt64(Clean, 16);
                    //Console.WriteLine(CurVal);
                    Values.Add(CurVal);
                }
                FileLine++;
            }

            texture.Texture = Values.ToArray();
            texture.Identifier = ItemToFind;

            Dict.Add(ItemToFind, texture);
        }

        private static int FindResourceLine(string[] File, string ItemToFind)
        {
            int FileLine = -1;
            for (int i = 0; i < File.Length; i++)
            {
                if (File[i].Contains(ItemToFind))
                {
                    FileLine = i; //found line
                    break;
                }                
            }

            if (FileLine == -1)
            {
                throw new Exception("Couldn't find item " + ItemToFind);
            }

            return FileLine;
        }

        public static void ParseVTX(string[] File, string Resource, ref Dictionary<string, VTX> Dict)
        {
            string ItemToFind = TrimExcess(Resource);
            VTX vert = new VTX();
            vert.VertexOffset = 0;

            int FileLine = FindResourceLine(File, ItemToFind);
            FileLine++;

            List<VTX_Item> Items = new List<VTX_Item>();
            while(File[FileLine] != "};")
            {
                string buf = File[FileLine].Replace("{", "").Trim();
                buf = buf.Replace("}", "");
                string[] data = buf.Split(',');


                VTX_Item Cur = new VTX_Item();
                Cur.Pos = new Vector3(short.Parse(data[0]), short.Parse(data[1]), short.Parse(data[2]));
                Cur.flag = ushort.Parse(data[3]);
                Cur.Coords = new Vector2(short.Parse(data[4]), short.Parse(data[5]));
                Cur.Colours = new Vector4(Convert.ToInt32(data[6].Replace("0x","").Trim(),16), Convert.ToInt32(data[7].Replace("0x", "").Trim(), 16), Convert.ToInt32(data[8].Replace("0x", "").Trim(), 16), Convert.ToInt32(data[9].Replace("0x", "").Trim(), 16));

                Items.Add(Cur);
                FileLine++;
            }

            vert.Vertex = Items.ToArray();
            vert.Identifier = ItemToFind;

            Dict.Add(ItemToFind, vert);
        }
        private static string TrimExcess(string Resource)
        {
            string ItemToFind = Resource.Replace("extern ", "");
            ItemToFind = ItemToFind.Substring(0, ItemToFind.IndexOf('['));
            ItemToFind = ItemToFind.Trim(); //remove all unneeded data
            return ItemToFind;
        }

        private static string GetCommand(string FullLine)
        {
            return FullLine.Substring(0, FullLine.IndexOf('(')).Trim();
        }

        private static UInt32 GetGeometryMode(string FullLine)
        {
            UInt32 mode = 0;
            string L = FullLine.ToUpper();

            if(L.Contains("G_ZBUFFER"))
            {
                mode |= 0b00000000000000000000000000000001;
            }
            if(L.Contains("G_SHADE"))
            {
                mode |= 0b00000000000000000000000000000100;
            }
            if (L.Contains("G_CULL_FRONT"))
            {
                mode |= 0b00000000000000000000000100000000;
            }
            if (L.Contains("G_CULL_BACK"))
            {
                mode |= 0b00000000000000000000001000000000;
            }
            if (L.Contains("G_FOG"))
            {
                mode |= 0b00000000000000010000000000000000;
            }
            if (L.Contains("G_LIGHTING"))
            {
                mode |= 0b00000000000000100000000000000000;
            }
            if (L.Contains("G_TEXTURE_GEN"))
            {
                mode |= 0b00000000000001000000000000000000;
            }
            if (L.Contains("G_SHADING_SMOOTH"))
            {
                mode |= 0b00000000000000000000001000000000;
            }
            if (L.Contains("G_CLIPPING"))
            {
                mode |= 0b00000000100000000000000000000000;
            }

            return mode;
        }
    }
}
