using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using Lzo64;

namespace Decompressor
{
    public partial class Form1 : Form
    {
        private readonly LZOCompressor lzocomp = new LZOCompressor();
        public Form1()
        {
            InitializeComponent();
        }
        /*
         punt usage is strictly related to the fact that x86 machines are little endian (while the PS3 is big endian), so each time i get and set data on the file i need to rearrange bytes position accordingly.
         The code is a bit messy, i should've created a separate class to handle the actual code and stay out of the UI things, will do it later          
         */
        private void btnUn_Click(object sender, EventArgs e)
        {
           try
           {
                OpenFileDialog op = new OpenFileDialog();
                op.Filter = "All Nier Volume Files (*.2DV;*.MDV;*.VIR;*.EFV)|*.2DV;*.MDV;*.VIR;*.EFV|Nier Font File|FONT_MAIN.*.BIN;FONT_MAIN_JP.*.BIN";
                if (op.ShowDialog() == DialogResult.OK)
                {
                    bool isfont = false;
                    string definitions = op.FileName;
                    string imagedatacontainer = op.FileName.Substring(0,definitions.Length-4); //This will handle file with double extension
                    if (op.FileName.Split('.').Last() == "2DV") //This just assign the corresponding package to each one
                    {
                        imagedatacontainer += ".2DP";
                    }
                    else if (op.FileName.Split('.').Last() == "MDV")
                    {
                        imagedatacontainer += ".MDP";
                    }
                    else if (op.FileName.Split('.').Last() == "VIR")
                    {
                        imagedatacontainer += ".PHY";
                    }
                    else if (op.FileName.Split('.').Last() == "EFV")
                    {
                        imagedatacontainer += ".EFP";
                    }
                    else if (op.FileName.Split('.').Last() == "BIN")
                    {
                        int index =definitions.LastIndexOf("MAIN"); //filename basically hardcoded, can't let pick random bin files
                        imagedatacontainer = definitions.Substring(0, index);
                        imagedatacontainer += "VRAM";
                        imagedatacontainer += definitions.Substring(index+4,definitions.Length-index-4);
                        isfont = true;
                    }
                    else
                    {
                        throw new Exception("Invalid or unknown file selected"); //No valid file selected (I have no idea of how you can arrive here)
                    }
                    if (File.Exists(imagedatacontainer))
                    {
                        byte[] def = File.ReadAllBytes(definitions);
                        List<int> heappos = new List<int>(); //There are files with more than 1 heap header
                        int i = 0;
                        while (i < def.Length-4)
                        {
                            if (System.Text.Encoding.UTF8.GetString(def, i, 4) == "HEAP") //Check for presence of one or more HEAP segment inside descriptor
                            {
                                heappos.Add(i);
                            }
                            i++;
                        }
                        if (heappos.Count()==0)
                        {
                            throw new Exception("Heap not found, invalid file"); //Can't work without an heap header, in the game there aren't any files without it
                        }
                        else
                        {
                            List<Compressedfile> list = new List<Compressedfile>();
                            byte[] punt = new byte[4];
                            for (int k = 0; k < heappos.Count; k++)
                            {
                                Array.Copy(def, heappos[k] + 12, punt, 0, 4);
                                Array.Reverse(punt);
                                int nametablepointer = BitConverter.ToInt32(punt, 0);
                                Array.Copy(def, heappos[k] + 16, punt, 0, 4);
                                Array.Reverse(punt);
                                int infotablepointer = BitConverter.ToInt32(punt, 0);

                                for (i = 1; i < (nametablepointer / 32); i++) //Get all elements stored between the heap and the nametable (they're all 32 bytes)
                                {
                                    Compressedfile found = new Compressedfile();
                                    if (System.Text.Encoding.UTF8.GetString(def, i * 32 + heappos[k], 4) == "TX2D")
                                    {
                                        found.istexture = true;
                                    }
                                    else
                                    {
                                        found.istexture = false;
                                    }
                                    found.pointer = i * 32 + heappos[k]; //All of the following pointers are expressed as offsets. I prefer to work with real addresses
                                    Array.Copy(def, found.pointer + 4, punt, 0, 4);
                                    Array.Reverse(punt);
                                    found.nameaddress = BitConverter.ToInt32(punt, 0)+nametablepointer+heappos[k];
                                    Array.Copy(def, found.pointer + 12, punt, 0, 4);
                                    Array.Reverse(punt);
                                    found.propertyaddress = BitConverter.ToInt32(punt, 0)+infotablepointer+heappos[k];
                                    Array.Copy(def, found.pointer + 16, punt, 0, 4);
                                    Array.Reverse(punt);
                                    found.datatype = BitConverter.ToInt32(punt, 0);
                                    Array.Copy(def, found.pointer + 20, punt, 0, 4);
                                    Array.Reverse(punt);
                                    found.dataoffset = BitConverter.ToInt32(punt, 0);
                                    Array.Copy(def, found.pointer + 24, punt, 0, 4);
                                    Array.Reverse(punt);
                                    found.datalenght = BitConverter.ToInt32(punt, 0);
                                    found.heap = k;
                                    if (found.istexture || (!found.istexture && !chkonlytext.Checked))
                                    list.Add(found);
                                }
                            }
                            MessageBox.Show("Found " + list.Count() + " texture files in "+heappos.Count()+" heap headers");
                            FolderBrowserDialog fold = new FolderBrowserDialog();
                            byte[] dati = File.ReadAllBytes(imagedatacontainer);
                            bool lzo = false;
                            if (System.Text.Encoding.UTF8.GetString(dati, 0, 3) == "lzo") //Some files are lzo compressed, with a description of the size of the chunk compressed, uncompressed and how many there are, variables name should be explicative enough
                            {
                                lzo = true;
                                Stream temp = File.OpenWrite(imagedatacontainer+".tmp");
                                MemoryStream ms = new MemoryStream();                               
                                Array.Copy(dati, 0x0c, punt, 0, 4);
                                Array.Reverse(punt);
                                int nchuncks = BitConverter.ToInt32(punt, 0);
                                Array.Copy(dati, 0x10, punt, 0, 4);
                                Array.Reverse(punt);
                                int size = BitConverter.ToInt32(punt, 0);
                                Array.Copy(dati, 0x24, punt, 0, 4);
                                Array.Reverse(punt);
                                int uncompressedchuncklength = BitConverter.ToInt32(punt, 0);
                                Array.Copy(dati, 0x28, punt, 0, 4);
                                Array.Reverse(punt);
                                int compressedchuncklength = BitConverter.ToInt32(punt, 0);
                                byte[] chunk = new byte[compressedchuncklength];
                                Array.Copy(dati, 0x2C, chunk, 0, compressedchuncklength);
                                ms.Write(lzocomp.Decompress(chunk, uncompressedchuncklength),0,uncompressedchuncklength);
                                int read = 0x2C + compressedchuncklength;
                                read = 0x20000;
                                for (i = 1; i < nchuncks; i++)
                                {

                                    Array.Copy(dati, read+0x4, punt, 0, 4);
                                    Array.Reverse(punt);
                                    uncompressedchuncklength = BitConverter.ToInt32(punt, 0);
                                    Array.Copy(dati, read+0x8, punt, 0, 4);
                                    Array.Reverse(punt);
                                    compressedchuncklength = BitConverter.ToInt32(punt, 0);
                                    chunk = new byte[compressedchuncklength];
                                    Array.Copy(dati, read+0xC, chunk, 0, compressedchuncklength);
                                    ms.Write(lzocomp.Decompress(chunk, uncompressedchuncklength), 0, uncompressedchuncklength);
                                    read += 0x20000;
                                   /* if (ms.Length >= size) //This seemed to be required at some point, now i may have fixed enough things that it isn't needed anymore
                                    {
                                        i = nchuncks;
                                        ms.SetLength(size);
                                    }*/
                                }
                                ms.WriteTo(temp);
                                temp.Close();
                                dati = File.ReadAllBytes(imagedatacontainer + ".tmp"); 
                                MessageBox.Show("LZO decompression done");
                            }
                            List<int> heapoffset = new List<int>();
                            int next=0x0;
                            if (System.Text.Encoding.UTF8.GetString(dati, 0, 4) == "KPKy") //Some files have this KPKy header with some unknown data, every dataoffset specified starts from the end of them
                            {
                                Array.Copy(dati, next+0x4, punt, 0, 4);
                                Array.Reverse(punt);
                                int timestosearch=BitConverter.ToInt32(punt, 0);
                                int k=0;
                                for (i=0;i<timestosearch;i++)
                                {
                                    Array.Copy(dati, next+0xC+(0x4*k), punt, 0, 4);
                                    Array.Reverse(punt);
                                    int temp=BitConverter.ToInt32(punt, 0);
                                    if (temp != 0x0)
                                    {
                                        if ((System.Text.Encoding.UTF8.GetString(dati, temp, 4) == "KPKy")&&(temp!=next)) //In theory, the next KPK pointer should be the last, but possibly need a rewrite to be sure
                                        {
                                            next = temp;
                                            Array.Copy(dati, next + 0x4, punt, 0, 4);
                                            Array.Reverse(punt);
                                            timestosearch += BitConverter.ToInt32(punt, 0);
                                            k = 0;
                                        }
                                        else
                                        {
                                            heapoffset.Add(temp + next);
                                            k++;
                                        }
                                    }
                                    else
                                    {
                                        k++;
                                    }
                                }
                                for (i = 0; i < heappos.Count(); i++)
                                {
                                    for (k = 0; k < list.Count(); k++)
                                    {
                                        if (list[k].heap == i)
                                        {
                                            list[k].dataoffset+=heapoffset[i];
                                        }
                                    }
                                }
                            }
                            fold.Description = "Choose a directory where the files will be decompressed";
                            int compressiontype, width, height, pitch;
                            bool deswizzle;
                            byte[] filedata;
                            if (fold.ShowDialog() == DialogResult.OK)
                            {
                                string directory = fold.SelectedPath;
                                string name = "";
                                int j;
                                byte[] header = new byte[128];
                                for (int k = 0; k < header.Length; k++)
                                header[k] = 0;
                                header[0] = 0x44; //building general dds header properties
                                header[1] = 0x44;
                                header[2] = 0x53;
                                header[3] = 0x20;
                                header[4] = 0x7C;
                                header[9] = 0x10;                              
                                //header[28] = 0x04;
                                header[76] = 0x20;
                                header[109] = 0x10;
                                List<string> NierTextureDesc = new List<string>();
                                NierTextureDesc.Add("Extracted from: "+imagedatacontainer);
                                if (lzo)
                                {
                                    NierTextureDesc[0] += " Decompressed with LZO";
                                }
                                NierTextureDesc.Add(String.Format("{0,-50}  {1,-13} {2,-12}  {3,-8}  {4,-12}  {5,-12}  {6,-12}  {7,-12}", "Name", "Compression", "Mipmaps", "Heap N", "File point.", "Prop point.", "Data start", "Data end"));
                                string textdesc;
                                string compression;
                                string mipmapnum;
                                string heapnum;
                                string filepointer;
                                string propertypointer;
                                string startdatapointer;
                                string enddatapointer;
                                for (i = 0; i < list.Count(); i++)
                                {
                                    name = "";
                                    j = list[i].nameaddress;
                                    while (def[j] != 0) //the name doesn't have a fixed length and is only terminated by 0x00
                                    {
                                        name += System.Text.Encoding.UTF8.GetString(def, j, 1);
                                        j++;
                                    }
                                    if (!list[i].istexture)
                                    name+= "_" + System.Text.Encoding.UTF8.GetString(def, list[i].pointer, 4);
                                    bool newfile = false;
                                    j = 0;
                                    if (((!list[i].istexture)&&(File.Exists(directory + "\\" + name))) || (File.Exists(directory + "\\" + name + ".dds")))
                                    {
                                        while (!newfile)
                                        {
                                            j++;
                                            if (((!list[i].istexture)&&(!File.Exists(directory + "\\" + name+"_"+j)))||(!File.Exists(directory + "\\" + name + "_" + j + ".dds")))
                                            {
                                                name = name + "_" + j;
                                                newfile = true;
                                            }
                                        }
                                    }
                                    if (list[i].istexture)
                                    {
                                        
                                        var myfile= File.Create(directory + "\\" + name + ".dds");
                                        if (!chkXbox.Checked)
                                        {
                                            Array.Copy(def, list[i].propertyaddress + 0x8, punt, 0, 2); //Width
                                            header[16] = punt[1];
                                            header[17] = punt[0];
                                            width = punt[0] * 0x100 + punt[1];
                                            Array.Copy(def, list[i].propertyaddress + 0xA, punt, 0, 2); //Height
                                            header[12] = punt[1];
                                            header[13] = punt[0];
                                            height = punt[0] * 0x100 + punt[1];
                                            compressiontype = def[list[i].propertyaddress];
                                            if (compressiontype == 0x85)
                                            {
                                                deswizzle = true;
                                            }
                                            else deswizzle = false;
                                            compressiontype = compressiontype & 0x0f;
                                        }
                                        else
                                        {
                                            compressiontype = def[list[i].propertyaddress + 0x23];
                                            width = 0;
                                            height = 0;
                                            deswizzle = false;
                                        }
                                        if ((compressiontype == 0x5)||(compressiontype==0x86)||(compressiontype == 0x7C))
                                        { //uncompressed (must calculate pitch)
                                            //Array.Copy(def, list[i].propertyaddress + 18, punt, 0, 2); it's already specified in some files, but better be sure                   
                                            if (!chkXbox.Checked)
                                            {
                                                pitch = (width * 32 + 7) / 8;
                                                punt = BitConverter.GetBytes(pitch);
                                                header[20] = punt[0];
                                                header[21] = punt[1];
                                                header[22] = punt[2];
                                                header[23] = punt[3];
                                            }
                                            header[8] = 0x0F;
                                            header[80] = 0x41;
                                            header[88] = 0x20;          
                                            header[10] = 0x0;
                                            header[34] = 0x0;
                                            header[84] = 0x0;
                                            header[85] = 0x0;
                                            header[86] = 0x0;
                                            header[87] = 0x0;
                                            header[108] = 0x0;
                                            header[110] = 0x0;
                                            if (def[list[i].propertyaddress + 0x7] == 0xE4) //B8G8R8A8 (unsure about it, if anyone has got better ideas, feel free to change it)
                                            {
                                                 header[93] = 0xFF;
                                                 header[98] = 0xFF;
                                                 header[103] = 0xFF;
                                                 header[104] = 0xFF;
                                                 header[92] = 0x00;
                                                 header[97] = 0x00;
                                                 header[102] = 0x00;
                                                 header[107] = 0x00;
                                            }
                                            else if ((def[list[i].propertyaddress + 0x7] == 0x93) || (compressiontype == 0x86) || (compressiontype == 0x7C)) //A8B8G8R8
                                            {
                                                header[92] = 0xFF;
                                                header[97] = 0xFF;
                                                header[102] = 0xFF;
                                                header[107] = 0xFF;
                                                header[93] = 0x00;
                                                header[98] = 0x00;
                                                header[103] = 0x00;
                                                header[104] = 0x00;
                                            }
                                            else //Never happened, need more tests
                                            {
                                                MessageBox.Show("Unknown RGBA mask for" + name+". BGRA will be applied");
                                                header[93] = 0xFF;
                                                header[98] = 0xFF;
                                                header[103] = 0xFF;
                                                header[104] = 0xFF;
                                                header[92] = 0x00;
                                                header[97] = 0x00;
                                                header[102] = 0x00;
                                                header[107] = 0x00;
                                            }
                                            if (deswizzle)
                                                compression = "Un. Swizzled";                                                
                                            else if (compressiontype != 0x7C)
                                                compression = "Uncompressed";
                                            else
                                                compression = "CTX1->RG(BA)";
                                        }
                                        else
                                        { //compressed (put dxt*)
                                            header[8] = 0x0;
                                            header[20] = 0x0;
                                            header[21] = 0x0;
                                            header[88] = 0x0;
                                            header[94] = 0x0;
                                            header[97] = 0x0;
                                            header[100] = 0x0;
                                            header[107] = 0x0;
                                            header[80] = 0x04;
                                            header[34] = 0x0A;
                                            header[10] = 0x0A;
                                            header[84] = 0x44;
                                            header[85] = 0x58;
                                            header[86] = 0x54;
                                            if ((compressiontype == 0x8)||(compressiontype==0x54))
                                            {
                                                header[87] = 0x35; //DXT5
                                                compression = "DXT5";
                                            }
                                            else if ((compressiontype == 0x6)||(compressiontype==0x52))
                                            {
                                                header[87] = 0x31; //DXT1
                                                compression = "DXT1";
                                            }
                                            else
                                            {
                                                header[87] = 0x33; //DXT3, most common one
                                                if ((compressiontype != 0x7)&&(compressiontype != 0x53))
                                                {
                                                    MessageBox.Show(name + " " + (list[i].propertyaddress + 8) + " uses an unknown compression format");
                                                    compression = "Unknown";
                                                }
                                                else
                                                {
                                                    compression = "DXT3";
                                                }
                                            }
                                            header[108] = 0x07;
                                            header[110] = 0x40;
                                        }
                                        int mipmaps = def[list[i].propertyaddress + 1];
                                        if (mipmaps > 1) //Check if mipmaps are present
                                            mipmapnum = mipmaps + " Mipmaps";
                                        else
                                            mipmapnum = "No Mipmaps";
                                        header[27] = Convert.ToByte(mipmaps);
                                        heapnum = "Heap:"+list[i].heap;
                                        filepointer="0x"+list[i].pointer.ToString("X");
                                        propertypointer = "0x"+list[i].propertyaddress.ToString("X");
                                        startdatapointer=   "0x" + (list[i].dataoffset).ToString("X");
                                        enddatapointer = "0x"+(list[i].dataoffset + list[i].datalenght).ToString("X");
                                        textdesc = string.Format("{0,-50}  {1,-13} {2,-12}  {3,-8}  {4,-12}  {5,-12}  {6,-12}  {7,-12}", name, compression, mipmapnum, heapnum, filepointer, propertypointer, startdatapointer, enddatapointer);
                                        NierTextureDesc.Add(textdesc);
                                        filedata = new byte[list[i].datalenght];
                                        myfile.Write(header, 0, header.Length);
                                        Array.Copy(dati, list[i].dataoffset, filedata, 0, list[i].datalenght);
                                        if (deswizzle)
                                        {
                                            int index = -1;
                                            List<byte> swizzled = new List<byte>();
                                            int square = 0;
                                            if (width > height)
                                            {
                                                square = width;
                                            }
                                            else //you always have to pick the biggest of the two to create the square, and then eventually discard some data
                                            {
                                                square = height;
                                            }
                                            for (int y = 0; y < square; y++)
                                            {
                                                for (int x = 0; x < square; x++)
                                                {
                                                    index = calcZOrder(x, y); 
                                                    if (filedata.Length > index * 4) //check that the index is available (might not be when we have a square bigger than the original width or height)
                                                    { 
                                                        swizzled.Add(filedata[index * 4]);
                                                        if (filedata.Length > index * 4 + 1)
                                                        {
                                                            swizzled.Add(filedata[index * 4 + 1]);
                                                            if (filedata.Length > index * 4 + 2)
                                                            {
                                                                swizzled.Add(filedata[index * 4 + 2]);
                                                                if (filedata.Length > index * 4 + 3)
                                                                {
                                                                    swizzled.Add(filedata[index * 4 + 3]);
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                            filedata = swizzled.ToArray();
                                            
                                        }
                                        if (chkXbox.Checked) //should be renamed to crazycheckbox
                                        {
                                            if (compressiontype != 0x86)
                                            {
                                                byte tr;
                                                for (int k = 0; k < filedata.Length / 2; k++)
                                                {
                                                    tr = filedata[k * 2];
                                                    filedata[k * 2] = filedata[k * 2 + 1];
                                                    filedata[k * 2 + 1] = tr;
                                                }
                                            }
                                            int tilingwidth = (def[list[i].propertyaddress + 0x1C] - 0x80)*128;
                                            if (def[list[i].propertyaddress + 0x1D] != 0X0)
                                            {
                                                tilingwidth += 32 * def[list[i].propertyaddress + 0x1D] / 0X40;
                                            }
                                            if (tilingwidth > 0)
                                            {
                                                int tilingheight = filedata.Length / tilingwidth;
                                                if (compressiontype == 0x53)
                                                {
                                                    filedata = ConvertToLinearTexture(filedata, tilingwidth, tilingheight, "DXT3");
                                                }
                                                else if (compressiontype == 0x54)
                                                {
                                                    filedata = ConvertToLinearTexture(filedata, tilingwidth, tilingheight, "DXT5");
                                                }
                                                else if (compressiontype == 0x52)
                                                {
                                                    tilingheight = filedata.Length*2 / tilingwidth;
                                                    filedata = ConvertToLinearTexture(filedata, tilingwidth, tilingheight, "DXT1");
                                                }
                                                else if (compressiontype == 0x86)
                                                {
                                                    tilingwidth *= 2;
                                                    tilingheight /= 2;
                                                    filedata = ConvertToLinearTexture(filedata, tilingwidth, tilingheight, "UNC");
                                                    tilingheight /= 2;
                                                    tilingwidth /= 2;
                                                }
                                                else if (compressiontype == 0x7C)                                               
                                                {
                                                    
                                                    tilingheight = filedata.Length * 2 / tilingwidth;
                                                    filedata = ConvertToLinearTexture(filedata, tilingwidth, tilingheight, "CTX1");
                                                    List<byte> rgbdata = new List<byte>();
                                                    byte[] cr = new byte[4];
                                                    byte[] cg = new byte[4];
                                                    List<byte> chred = new List<byte>();
                                                    List<byte> chgre = new List<byte>();
                                                    int xx;
                                                    for (int s = 0; s < filedata.Length / 8; s++) //Rebuilding RGBA file (credits to Xenia Emulator)
                                                    {
                                                        cr[0] = filedata[s * 8];
                                                        cr[1] = filedata[s * 8 + 2];
                                                        cr[2] = Convert.ToByte(cr[0] * 2 / 3 + cr[1] * 1 / 3);
                                                        cr[3] = Convert.ToByte(cr[0] * 1 / 3 + cr[1] * 2 / 3);
                                                        cg[0] = filedata[s * 8 + 1];
                                                        cg[1] = filedata[s * 8 + 3];
                                                        cg[2] = Convert.ToByte(cg[0] * 2 / 3 + cg[1] * 1 / 3);
                                                        cg[3] = Convert.ToByte(cg[0] * 1 / 3 + cg[1] * 2 / 3);
                                                        xx = filedata[s * 8 + 4] + filedata[s * 8 + 5] * 0x100 + filedata[s * 8 + 6] * 0x10000 + filedata[s * 8 + 7] * 0x1000000;
                                                        for (int oy = 0; oy < 4; ++oy)
                                                        {
                                                            for (int ox = 0; ox < 4; ++ox)
                                                            {
                                                                int blockxx = (xx >> (((ox + (oy * 4)) * 2))) & 3;
                                                                chred.Add(cr[blockxx]);
                                                                chgre.Add(cg[blockxx]);
                                                            }
                                                        }
                                                    }
                                                    int koffset = 0;
                                                    int offset = 0;
                                                    int k = 0;
                                                    while (koffset < chred.Count() / 16 - 1)
                                                    {
                                                        rgbdata.AddRange(chred.GetRange(k * 16 + offset, 1));
                                                        rgbdata.AddRange(chgre.GetRange(k * 16 + offset, 1));
                                                        rgbdata.Add(0XFF);
                                                        rgbdata.Add(0XFF);
                                                        rgbdata.AddRange(chred.GetRange(k * 16 + 1 + offset, 1));
                                                        rgbdata.AddRange(chgre.GetRange(k * 16 + 1 + offset, 1));
                                                        rgbdata.Add(0XFF);
                                                        rgbdata.Add(0XFF);
                                                        rgbdata.AddRange(chred.GetRange(k * 16 + 2 + offset, 1));
                                                        rgbdata.AddRange(chgre.GetRange(k * 16 + 2 + offset, 1));
                                                        rgbdata.Add(0XFF);
                                                        rgbdata.Add(0XFF);
                                                        rgbdata.AddRange(chred.GetRange(k * 16 + 3 + offset, 1));
                                                        rgbdata.AddRange(chgre.GetRange(k * 16 + 3 + offset, 1));
                                                        rgbdata.Add(0XFF);
                                                        rgbdata.Add(0XFF);
                                                        if (((k % (tilingwidth / 4) == 0) || ((k + 1) * 16 == chred.Count())) && (k != koffset)) //Might truncate a little bit of data, but it would be padding anyway
                                                        {
                                                            if (offset != 12)
                                                            {
                                                                k = koffset;
                                                                offset += 4;
                                                            }
                                                            else
                                                            {
                                                                koffset = k;
                                                                offset = 0;
                                                            }
                                                        }
                                                        k++;
                                                    }
                                                    filedata = rgbdata.ToArray();
                                                }
                                                List<byte> arrayed = new List<byte>(filedata);
                                                List<byte> trimmed = new List<byte>();
                                                int byt = (int)Math.Floor((double)((def[list[i].propertyaddress + 0x1C] - 0x80) - 1) / 2);
                                                if (byt < 0) byt = 0;
                                                width = byt * 256 + def[list[i].propertyaddress + 0x27]+1;
                                                if (def[list[i].propertyaddress + 0x1D] != 0X0) //Workaround until I find a better method
                                                {
                                                    if((def[list[i].propertyaddress + 0x1C] - 0x80)>0)
                                                    width += 256;
                                                }
                                                if (width != tilingwidth)
                                                {
                                                    int blockoffsetstart=0;
                                                    if (def[list[i].propertyaddress + 0x1C] - 0x80 > def[list[i].propertyaddress + 0x25]) //Not sure about this, only a few files fall in this category, but it seems to work so...
                                                    {
                                                        blockoffsetstart=4;
                                                    }
                                                    int paddedwidth = width;
                                                    if ((width % 4 != 0) && (compressiontype != 0x86))
                                                    {
                                                        paddedwidth += 4 - width % 4; //DXTn and CTX1 work with a 4x4 pixel grid, so this is needed
                                                    }
                                                    int blocksize = tilingwidth * 4;
                                                    int blocks = filedata.Length/blocksize; 
                                                    int divider = 4;
                                                    if ((compressiontype == 0x52)||(compressiontype == 0x7C))
                                                    {
                                                        blocksize = tilingwidth * 2;
                                                        blocks = filedata.Length / blocksize;
                                                        divider = 8;
                                                    }
                                                    if (compressiontype != 0x86)
                                                    {
                                                        for (int k = 0; k < blocks; k++)
                                                        {
                                                            if (k >= blockoffsetstart)
                                                            {
                                                                for (int s = 0; s < paddedwidth / divider; s++)
                                                                {
                                                                    trimmed.AddRange(arrayed.GetRange(k * blocksize + s * 16, 16));
                                                                }
                                                            }
                                                        }
                                                    }
                                                    else
                                                    {
                                                        for (int k = 0; k < blocks; k++)
                                                        {
                                                            if (k >= blockoffsetstart)
                                                            {
                                                                for (int s = 0; s < paddedwidth; s++)
                                                                {
                                                                    trimmed.AddRange(arrayed.GetRange(k * blocksize + s * 4, 4));
                                                                }
                                                            }
                                                        }
                                                    }                                           
                                                }
                                                else{
                                                    trimmed = arrayed;
                                                }
                                                height = (def[list[i].propertyaddress + 0x25] + 1) * 8 - ((0XFF - def[list[i].propertyaddress + 0x26]) / 0X20);
                                                punt = BitConverter.GetBytes((short)width);
                                                myfile.Position = 16;
                                                myfile.Write(punt, 0, 2);                                            
                                                punt = BitConverter.GetBytes((short)height);
                                                myfile.Position = 12;
                                                myfile.Write(punt, 0, 2);                                              
                                                pitch = (width * 32 + 7) / 8;
                                                punt = BitConverter.GetBytes(pitch);
                                                myfile.Position = 20;
                                                myfile.Write(punt, 0, 4);
                                                myfile.Position = 128;
                                                filedata = trimmed.ToArray();
                                            }
                                        }
                                        myfile.Write(filedata,0,filedata.Length);
                                        myfile.Close();         
                                    }
                                    else
                                    {
                                        filepointer = "0x" + list[i].pointer.ToString("X");
                                        propertypointer = "0x" + list[i].propertyaddress.ToString("X");
                                        startdatapointer = "0x" + (list[i].dataoffset).ToString("X");
                                        enddatapointer = "0x" + (list[i].dataoffset + list[i].datalenght).ToString("X");
                                        heapnum = "Heap:" + list[i].heap;
                                        textdesc = string.Format("{0,-78}  {1,-8}  {2,-12}  {3,-12}  {4,-12}  {5,-12}", name, heapnum, filepointer, propertypointer, startdatapointer, enddatapointer);
                                        NierTextureDesc.Add(textdesc);
                                        var myfile = File.Create(directory + "\\" + name);
                                        if(list[i].datalenght>1)
                                        myfile.Write(dati, list[i].dataoffset, list[i].datalenght);
                                        myfile.Close();
                                    }
                                }
                                MessageBox.Show("Files extracted with success");
                                if ((chkDetails.Checked) && (NierTextureDesc.Count() > 0))
                                {
                                    File.WriteAllLines(directory + "\\NierTextureDetails.txt", NierTextureDesc);
                                }
                                if (File.Exists(imagedatacontainer+".tmp"))
                                {
                                    File.Delete(imagedatacontainer + ".tmp"); //Delete temporary lzo decompressed file
                                }
                                if (isfont)
                                {
                                    MessageBox.Show("To modify the font file, you need to split each color channel with either GIMP (with DDS plugin) or Photoshop and recombine them once done");
                                }
                            }
                        }
                    }
                    else
                    {
                        throw new Exception("Unable to find the associated Package file");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

        }

        private void btnRep_Click(object sender, EventArgs e)
        {
            try
            {
                if (chkXbox.Checked)
                {
                    throw new Exception("Coming soon™"); //This needs a lot of things to be taken into account, for now it's disabled
                }
                bool lzo = false;
                FolderBrowserDialog fold = new FolderBrowserDialog();
                fold.Description = "Choose a directory where the texture are stored";
                if (fold.ShowDialog() == DialogResult.OK)
                {
                    string folder = fold.SelectedPath;
                    string[] files = Directory.GetFiles(folder, "*.dds").Select(Path.GetFileName).Cast<string>().ToArray();
                    if (files.Length == 0)
                    {
                        throw new Exception("No file available found. DDS files are the only ones repackable");
                    }
                    else
                    {
                        OpenFileDialog op = new OpenFileDialog();
                        op.Filter = "All Nier Volume Files (*.2DV;*.MDV;*.VIR;*.EFV)|*.2DV;*.MDV;*.VIR;*.EFV|Nier Font File|FONT_MAIN.PS3.BIN;FONT_MAIN_JP.PS3.BIN";
                        if (op.ShowDialog() == DialogResult.OK)
                        {
                            string definitions = op.FileName;
                            string imagedatacontainer = op.FileName.Substring(0, definitions.Length - 4); //This will handle file with double extension
                            if (op.FileName.Split('.').Last() == "2DV") //This just assign the corresponding package to each one
                            {
                                imagedatacontainer += ".2DP";
                            }
                            else if (op.FileName.Split('.').Last() == "MDV")
                            {
                                imagedatacontainer += ".MDP";
                            }
                            else if (op.FileName.Split('.').Last() == "VIR")
                            {
                                imagedatacontainer += ".PHY";
                            }
                            else if (op.FileName.Split('.').Last() == "EFV")
                            {
                                imagedatacontainer += ".EFP";
                            }
                            else if (op.FileName.Split('.').Last() == "BIN")
                            {
                                int index = definitions.LastIndexOf("MAIN");
                                imagedatacontainer = definitions.Substring(0, index);
                                imagedatacontainer += "VRAM";
                                imagedatacontainer += definitions.Substring(index + 4, definitions.Length - index - 4);
                            }
                            else
                            {
                                throw new Exception("Invalid or unknown file selected");
                            }
                            if (File.Exists(imagedatacontainer))
                            {
                                byte[] def = File.ReadAllBytes(definitions);
                                List<int> heappos = new List<int>(); //There are files with more than 1 heap header
                                int i = 0;
                                while (i < def.Length - 4)
                                {
                                    if (System.Text.Encoding.UTF8.GetString(def, i, 4) == "HEAP") //Check for presence of one HEAP segment inside descriptor
                                    {
                                        heappos.Add(i);
                                    }
                                    i++;
                                }
                                if (heappos.Count() == 0)
                                {
                                    throw new Exception("Heap not found, invalid file");
                                }
                                else
                                {
                                    List<Compressedfile> list = new List<Compressedfile>();
                                    byte[] punt = new byte[4];
                                    for (int k = 0; k < heappos.Count; k++)
                                    {
                                        Array.Copy(def, heappos[k] + 12, punt, 0, 4);
                                        Array.Reverse(punt);
                                        int nametablepointer = BitConverter.ToInt32(punt, 0);
                                        Array.Copy(def, heappos[k] + 16, punt, 0, 4);
                                        Array.Reverse(punt);
                                        int infotablepointer = BitConverter.ToInt32(punt, 0);

                                        for (i = 1; i < (nametablepointer / 32); i++) //Get all elements stored between the heap and the nametable (they're all 32 bytes)
                                        {
                                            Compressedfile found = new Compressedfile();
                                            if (System.Text.Encoding.UTF8.GetString(def, i * 32 + heappos[k], 4) == "TX2D")
                                            {
                                                found.istexture = true;
                                            }
                                            else
                                            {
                                                found.istexture = false;
                                            }
                                            found.pointer = i * 32 + heappos[k];
                                            Array.Copy(def, found.pointer + 4, punt, 0, 4);
                                            Array.Reverse(punt);
                                            found.nameaddress = BitConverter.ToInt32(punt, 0) + nametablepointer + heappos[k];
                                            Array.Copy(def, found.pointer + 12, punt, 0, 4);
                                            Array.Reverse(punt);
                                            found.propertyaddress = BitConverter.ToInt32(punt, 0) + infotablepointer + heappos[k];
                                            Array.Copy(def, found.pointer + 16, punt, 0, 4);
                                            Array.Reverse(punt);
                                            found.datatype = BitConverter.ToInt32(punt, 0);
                                            Array.Copy(def, found.pointer + 20, punt, 0, 4);
                                            Array.Reverse(punt);
                                            found.dataoffset = BitConverter.ToInt32(punt, 0);
                                            Array.Copy(def, found.pointer + 24, punt, 0, 4);
                                            Array.Reverse(punt);
                                            found.datalenght = BitConverter.ToInt32(punt, 0);
                                            found.heap = k;
                                            found.onvolumefile = def[found.pointer + 0X1F];
                                            list.Add(found); //we need to add all files in any case
                                        }
                                    }
                                    if (System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(imagedatacontainer), 0, 3) == "lzo")
                                    {
                                        lzo = true;
                                        punt = new byte[4];
                                        byte[] lzodata = File.ReadAllBytes(imagedatacontainer);
                                        Stream temp = File.OpenWrite(imagedatacontainer + ".tmp");
                                        MemoryStream ms = new MemoryStream();
                                        Array.Copy(lzodata, 0x0c, punt, 0, 4);
                                        Array.Reverse(punt);
                                        int nchuncks = BitConverter.ToInt32(punt, 0);
                                        Array.Copy(lzodata, 0x10, punt, 0, 4);
                                        Array.Reverse(punt);
                                        int size = BitConverter.ToInt32(punt, 0);
                                        Array.Copy(lzodata, 0x24, punt, 0, 4);
                                        Array.Reverse(punt);
                                        int uncompressedchuncklength = BitConverter.ToInt32(punt, 0);
                                        Array.Copy(lzodata, 0x28, punt, 0, 4);
                                        Array.Reverse(punt);
                                        int compressedchuncklength = BitConverter.ToInt32(punt, 0);
                                        byte[] chunk = new byte[compressedchuncklength];
                                        Array.Copy(lzodata, 0x2C, chunk, 0, compressedchuncklength);
                                        ms.Write(lzocomp.Decompress(chunk, uncompressedchuncklength), 0, uncompressedchuncklength);
                                        int read = 0x2C + compressedchuncklength;
                                        read = read & 0xfff0000; //This will 99.99% of the time result in a simple 0x20000 increment from before, but we need to be sure
                                        read = read + 0x10000;
                                        for (i = 1; i < nchuncks; i++)
                                        {

                                            Array.Copy(lzodata, read + 0x4, punt, 0, 4);
                                            Array.Reverse(punt);
                                            uncompressedchuncklength = BitConverter.ToInt32(punt, 0);
                                            Array.Copy(lzodata, read + 0x8, punt, 0, 4);
                                            Array.Reverse(punt);
                                            compressedchuncklength = BitConverter.ToInt32(punt, 0);
                                            chunk = new byte[compressedchuncklength];
                                            Array.Copy(lzodata, read + 0xC, chunk, 0, compressedchuncklength);
                                            ms.Write(lzocomp.Decompress(chunk, uncompressedchuncklength), 0, uncompressedchuncklength);
                                            read += 0xC + compressedchuncklength;
                                            read = read & 0xfff0000;
                                            read = read + 0x10000;
                                        }
                                        ms.WriteTo(temp);
                                        temp.Close();
                                        imagedatacontainer = imagedatacontainer + ".tmp";
                                    }
                                    var dati = File.ReadAllBytes(imagedatacontainer);
                                    List<int> heapoffset = new List<int>();
                                    int next = 0x0;
                                    if (System.Text.Encoding.UTF8.GetString(dati, 0, 4) == "KPKy") //Some files have this KPKy header with some unknown data, every dataoffset specified starts from the end of them
                                    {
                                        Array.Copy(dati, next + 0x4, punt, 0, 4);
                                        Array.Reverse(punt);
                                        int volte = BitConverter.ToInt32(punt, 0);
                                        int k = 0;
                                        for (i = 0; i < volte; i++)
                                        {
                                            Array.Copy(dati, next + 0xC + (0x4 * k), punt, 0, 4);
                                            Array.Reverse(punt);
                                            int temp = BitConverter.ToInt32(punt, 0);
                                            if (temp != 0x0)
                                            {
                                                if ((System.Text.Encoding.UTF8.GetString(dati, temp, 4) == "KPKy") && (temp != next)) //In theory, the next KPK pointer should be the last, but possibly need a rewrite to be sure
                                                {
                                                    next = temp;
                                                    Array.Copy(dati, next + 0x4, punt, 0, 4);
                                                    Array.Reverse(punt);
                                                    volte += BitConverter.ToInt32(punt, 0);
                                                    k = 0;
                                                }
                                                else
                                                {
                                                    heapoffset.Add(temp + next);
                                                    k++;
                                                }
                                            }
                                            else
                                            {
                                                k++;
                                            }
                                        }
                                        for (i = 0; i < heappos.Count(); i++)
                                        {
                                            for (k = 0; k < list.Count(); k++)
                                            {
                                                if (list[k].heap == i)
                                                {
                                                    list[k].dataoffset += heapoffset[i];
                                                }
                                            }
                                        }
                                    }
                                    Stream stream = File.OpenWrite(imagedatacontainer);
                                    List<string> namelist = new List<string>();
                                    for (int j = 0; j < list.Count(); j++)
                                    {
                                        string newname = "";
                                        int k = list[j].nameaddress;
                                        while (def[k] != 0) 
                                        {
                                            newname += System.Text.Encoding.UTF8.GetString(def, k, 1);
                                            k++;
                                        }
                                        /*if(list[j].istexture) //Will fix when we can let everything to be reinjected back
                                        namelist.Add(newname);
                                        else namelist.Add(nome+"_"+System.Text.Encoding.UTF8.GetString(def, list[j].pointer, 4));*/
                                        if (namelist.Contains(newname))
                                        {
                                            k = 1;
                                            while (namelist.Contains(newname+"_"+k))
                                            {
                                                k++;
                                            }
                                            namelist.Add(newname + "_" + k);
                                        }
                                        else
                                            namelist.Add(newname);
                                    }
                                    int fileoffset=0,width,height;
                                    string notfound = "";
                                    for (i = 0; i < files.Length; i++)
                                    {
                                        try
                                        {
                                            int modifyindex;
                                            if (namelist.IndexOf(files[i].Substring(0, files[i].Length - 4)) != -1) //Get index of the TX2D description by checking if filename corresponds to any of them
                                            {
                                                modifyindex = namelist.IndexOf(files[i].Substring(0, files[i].Length - 4));
                                                byte[] imgdata = File.ReadAllBytes(folder + "\\" + files[i]);
                                                byte[] noheader = new byte[imgdata.Length - 128];
                                                Array.Copy(imgdata, 128, noheader, 0, imgdata.Length - 128);
                                                if (noheader.Length % 0x80 != 0) //data must be 128-byte aligned, so it will probably need padding (expecially after being modified)
                                                {
                                                    byte[] noheaderpad = new byte[noheader.Length];
                                                    Array.Copy(noheader, noheaderpad, noheader.Length);
                                                    noheader = new byte[noheaderpad.Length + (0x80 - noheader.Length % 0x80)];
                                                    for (int j = 0; j < noheader.Length; j++)
                                                    {
                                                        if (j < noheaderpad.Length)
                                                        {
                                                            noheader[j] = noheaderpad[j];
                                                        }
                                                        else
                                                        {
                                                            noheader[j] = 0xEE; //This seems to be the padding data that the game uses
                                                        }
                                                    }
                                                }
                                                Array.Copy(imgdata, 16, punt, 0, 2); //Width
                                                width = BitConverter.ToInt16(punt, 0);
                                                Array.Copy(imgdata, 12, punt, 0, 2); //Height
                                                height = BitConverter.ToInt16(punt, 0);                                                                                                      
                                                if (noheader.Length != list[modifyindex].datalenght) //if file to inject is bigger or smaller than the original (not working properly on many files)
                                                {
                                                    MessageBox.Show("File size of " + files[i] + " doesn't match the original. Attempting to reaarrange pointers");
                                                    Stream deffile = File.OpenWrite(definitions);
                                                    punt = new byte[2];
                                                    deffile.Position = list[modifyindex].propertyaddress + 1; //Number of mipmaps
                                                    if (imgdata[0x1C] != 0)
                                                        deffile.Write(imgdata, 0x1C, 1);
                                                    punt = BitConverter.GetBytes((Int16)width);
                                                    Array.Reverse(punt);                                                  
                                                    deffile.Position = list[modifyindex].propertyaddress + 8;
                                                    deffile.Write(punt, 0, punt.Length);
                                                    punt = BitConverter.GetBytes((Int16)height);
                                                    Array.Reverse(punt);
                                                    deffile.Position = list[modifyindex].propertyaddress + 10;
                                                    deffile.Write(punt, 0, punt.Length);
                                                    punt = BitConverter.GetBytes(noheader.Length);
                                                    Array.Reverse(punt);
                                                    deffile.Position = list[modifyindex].pointer + 24;//Data lenght
                                                    deffile.Write(punt, 0, punt.Length);
                                                    int offsetpos = noheader.Length - list[modifyindex].datalenght;
                                                    fileoffset += offsetpos;
                                                    list[modifyindex].datalenght = noheader.Length;
                                                    for (int j = 0; j < list.Count(); j++)
                                                    {
                                                        if (list[j].pointer > list[modifyindex].pointer) //Adjust Data Offset for consequent pointers
                                                        {
                                                            if (list[j].onvolumefile == 0x0) //files with no data on Package file will have 1 as value
                                                            {
                                                                list[j].dataoffset += offsetpos;
                                                                deffile.Position = list[j].pointer + 20;
                                                                punt = BitConverter.GetBytes(list[j].dataoffset);
                                                                Array.Reverse(punt);
                                                                deffile.Write(punt, 0, punt.Length);
                                                            }
                                                        }
                                                    }
                                                    deffile.Close();
                                                    stream.Close();
                                                    Stream str2 = File.OpenRead(imagedatacontainer);
                                                    str2.Position = (list[modifyindex].dataoffset + list[modifyindex].datalenght - offsetpos);
                                                    MemoryStream ms = new MemoryStream();
                                                    byte[] buffer = new Byte[2048];
                                                    int length;
                                                    while ((length = str2.Read(buffer, 0, buffer.Length)) > 0)
                                                        ms.Write(buffer, 0, length);
                                                    byte[] nextdata = ms.ToArray();
                                                    str2.Close();
                                                    stream = File.OpenWrite(imagedatacontainer);
                                                    stream.Position = list[modifyindex].dataoffset;
                                                    if (def[list[modifyindex].propertyaddress] == 0x85)
                                                    {
                                                        int index = -1, arraypos;
                                                        byte[] swizzled = new byte[noheader.Length];
                                                        int square = 0;
                                                        if (width > height)
                                                        {
                                                            square = width;
                                                        }
                                                        else //you always have to pick the biggest of the two to create the square, and then eventually discard some data
                                                        {
                                                            square = height;
                                                        }
                                                        arraypos = 0;
                                                        for (int y = 0; y < square; y++)
                                                        {
                                                            for (int x = 0; x < square; x++)
                                                            {
                                                                index = calcZOrder(x, y);
                                                                if (swizzled.Length > index * 4)
                                                                {
                                                                    swizzled[index * 4] = noheader[arraypos];
                                                                    arraypos++;
                                                                    if (swizzled.Length > index * 4 + 1)
                                                                    {
                                                                        swizzled[index * 4 + 1] = noheader[arraypos];
                                                                        arraypos++;
                                                                        if (swizzled.Length > index * 4 + 2)
                                                                        {
                                                                            swizzled[index * 4 + 2] = noheader[arraypos];
                                                                            arraypos++;
                                                                            if (swizzled.Length > index * 4 + 3)
                                                                            {
                                                                                swizzled[index * 4 + 3] = noheader[arraypos];
                                                                                arraypos++;
                                                                            }
                                                                        }
                                                                    }
                                                                }
                                                            }
                                                        }
                                                        noheader = swizzled.ToArray();
                                                    }
                                                    stream.Write(noheader, 0, list[modifyindex].datalenght);
                                                    stream.Position = list[modifyindex].dataoffset + list[modifyindex].datalenght;
                                                    stream.Write(nextdata, 0, nextdata.Length);
                                                }
                                                else
                                                {
                                                    stream.Position = list[modifyindex].dataoffset;
                                                    if (def[list[modifyindex].propertyaddress] == 0x85)
                                                    {
                                                        int index = -1,arraypos;
                                                        byte[] swizzled = new byte[noheader.Length];
                                                        int square = 0;
                                                        if (width > height)
                                                        {
                                                            square = width;
                                                        }
                                                        else //you always have to pick the biggest of the two to create the square, and then eventually discard some data
                                                        {
                                                            square = height;
                                                        }
                                                        arraypos = 0;
                                                        for (int y = 0; y < square; y++)
                                                        {
                                                            for (int x = 0; x < square; x++)
                                                            {
                                                                index = calcZOrder(x, y);
                                                                if (swizzled.Length > index*4)
                                                                {
                                                                    swizzled[index * 4] = noheader[arraypos];
                                                                    arraypos++;
                                                                    if (swizzled.Length > index * 4 + 1)
                                                                    {
                                                                        swizzled[index * 4 + 1] = noheader[arraypos];
                                                                        arraypos++;
                                                                        if (swizzled.Length > index * 4 + 2)
                                                                        {
                                                                            swizzled[index * 4 + 2] = noheader[arraypos];
                                                                            arraypos++;
                                                                            if (swizzled.Length > index * 4 + 3)
                                                                            {
                                                                                swizzled[index * 4 + 3] = noheader[arraypos];
                                                                                arraypos++;
                                                                            }
                                                                        }
                                                                    }
                                                                }
                                                            }
                                                        }
                                                        noheader = swizzled.ToArray();
                                                    }
                                                    stream.Write(noheader, 0, noheader.Length);
                                                }
                                            }
                                            else
                                            {
                                                notfound+=",\n"+files[i];
                                            }
                                         }
                                         catch (Exception ex)
                                         {
                                             stream.Close();
                                             throw new Exception(ex.Message);
                                         }
                                    }
                                    if (notfound != "")
                                    {
                                        MessageBox.Show("No matching data found for: "+notfound.Substring(1)+".");
                                    }
                                    if (fileoffset<0) //Should happen only when you inject texture smaller than the original, helps removing garbage data
                                    {
                                        stream.SetLength(stream.Length+fileoffset);
                                    }
                                    stream.Close();
                                    if (lzo) //rebuild lzo file (not strictly necessary, but it's better to remain as close as possible to the game structure)
                                    {
                                        dati = File.ReadAllBytes(imagedatacontainer.Substring(0, imagedatacontainer.Length - 4));
                                        byte[] datinoncomp = File.ReadAllBytes(imagedatacontainer);
                                        byte[] padding = new byte[1];
                                        padding[0] = 0x0;

                                        MemoryStream ms = new MemoryStream();
                                        ms.Write(dati, 0, 0x24);
                                        int nchuncks = 1;
                                        int uncompressedchunckpointer = 0;
                                        int expectedchunksize = 0x32000; //Best approximation of lzo compression ratio, with good enough performance
                                        byte[] chunk;
                                        byte[] result;
                                        do
                                        {
                                            chunk = new byte[expectedchunksize];
                                            Array.Copy(datinoncomp, uncompressedchunckpointer, chunk, 0, expectedchunksize);
                                            result = lzocomp.Compress(chunk);
                                            expectedchunksize -= 0x1000;
                                        } while (result.Length >= 0x20000); //Attempt to compress the first block of 0x32000 in size to less than 0x20000. If not possible reduce data lenght by 0x1000 each time
                                        uncompressedchunckpointer += chunk.Length;
                                        punt = BitConverter.GetBytes(chunk.Length);
                                        Array.Reverse(punt);
                                        ms.Write(punt, 0, 4);
                                        punt = BitConverter.GetBytes(result.Length);
                                        Array.Reverse(punt);
                                        ms.Write(punt, 0, 4);
                                        ms.Write(result, 0, result.Length);
                                        int write = 0x20000;
                                        while (ms.Length < write)
                                        {
                                            ms.Write(padding, 0, 1);
                                        } 
                                        while (uncompressedchunckpointer < datinoncomp.Length) //same procedure as before, but with pointers to keep track of where we are
                                        {
                                            nchuncks++;
                                            punt = BitConverter.GetBytes(uncompressedchunckpointer);
                                            Array.Reverse(punt);
                                            ms.Write(punt, 0, 4);
                                            if (uncompressedchunckpointer < datinoncomp.Length - 0x32000)
                                                expectedchunksize = 0x32000;
                                            else
                                                expectedchunksize = datinoncomp.Length - uncompressedchunckpointer;
                                            do
                                            {
                                                chunk = new byte[expectedchunksize];
                                                Array.Copy(datinoncomp, uncompressedchunckpointer, chunk, 0, expectedchunksize);
                                                result = lzocomp.Compress(chunk);
                                                expectedchunksize -= 0x1000;
                                            } while (result.Length >= 0x20000);
                                            uncompressedchunckpointer += chunk.Length;
                                            punt = BitConverter.GetBytes(chunk.Length);
                                            Array.Reverse(punt);
                                            ms.Write(punt, 0, 4);
                                            punt = BitConverter.GetBytes(result.Length);
                                            Array.Reverse(punt);
                                            ms.Write(punt, 0, 4);
                                            ms.Write(result, 0, result.Length);
                                            write += 0x20000;
                                            while (ms.Length < write)
                                            {
                                                ms.Write(padding, 0, 1);
                                            }                                         
                                        }
                                        ms.Position=0x0c;
                                        punt = BitConverter.GetBytes(nchuncks);
                                        Array.Reverse(punt);
                                        ms.Write(punt, 0, 4);
                                        ms.Position = 0x10;
                                        punt = BitConverter.GetBytes(datinoncomp.Length);
                                        Array.Reverse(punt);
                                        ms.Write(punt, 0, 4);
                                        File.Delete(imagedatacontainer.Substring(0, imagedatacontainer.Length - 4));
                                        Stream lzofile = File.Create(imagedatacontainer.Substring(0, imagedatacontainer.Length - 4));
                                        ms.WriteTo(lzofile);
                                        lzofile.Close();
                                        File.Delete(imagedatacontainer);
                                        MessageBox.Show("LZO recompression done");
                                    }
                                    MessageBox.Show("File repacked succesfully");
                                    
                                }
                            }
                            else
                            {
                                throw new Exception("Unable to find the associated Volume file");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
        public Int32 calcZOrder(int xPos, int yPos) //Credit to user aggsol from https://stackoverflow.com/questions/12157685/z-order-curve-coordinates, whose solution is based on http://graphics.stanford.edu/~seander/bithacks.html
        {
            Int32[] MASKS = { 0x55555555, 0x33333333, 0x0F0F0F0F, 0x00FF00FF};
            Int32[] SHIFTS = { 1, 2, 4, 8 };

            Int32 x = xPos;
            Int32 y = yPos;

            x = (x | (x << SHIFTS[3])) & MASKS[3];
            x = (x | (x << SHIFTS[2])) & MASKS[2];
            x = (x | (x << SHIFTS[1])) & MASKS[1];
            x = (x | (x << SHIFTS[0])) & MASKS[0];

            y = (y | (y << SHIFTS[3])) & MASKS[3];
            y = (y | (y << SHIFTS[2])) & MASKS[2];
            y = (y | (y << SHIFTS[1])) & MASKS[1];
            y = (y | (y << SHIFTS[0])) & MASKS[0];

            int result = x | (y << 1);
            return result;
        }
        internal static byte[] ConvertToLinearTexture(byte[] data, int width, int height, string texture) //Credits to the GTA XTD texture viewer (http://forum.xentax.com/blog/?p=302)
        {
            byte[] destData = new byte[data.Length];

            int blockSize;
            int texelPitch;

            switch (texture)
            {
                case "DXT1":
                    blockSize = 4;
                    texelPitch = 8;
                    break;
                case "DXT3":
                case "DXT5":
                    blockSize = 4;
                    texelPitch = 16;
                    break;
                case "UNC":
                    blockSize = 2;
                    texelPitch = 4;
                    break;
                case "CTX1":
                    blockSize = 4;
                    texelPitch = 8;
                    break;
                default:
                    throw new ArgumentOutOfRangeException("Bad dxt type!");
            }

            int blockWidth = width / blockSize;
            int blockHeight = height / blockSize;

            for (int j = 0; j < blockHeight; j++)
            {
                for (int i = 0; i < blockWidth; i++)
                {
                    int blockOffset = j * blockWidth + i;

                    int x = XGAddress2DTiledX(blockOffset, blockWidth, texelPitch);
                    int y = XGAddress2DTiledY(blockOffset, blockWidth, texelPitch);

                    int srcOffset = j * blockWidth * texelPitch + i * texelPitch;
                    int destOffset = y * blockWidth * texelPitch + x * texelPitch;
                    if(destOffset<data.Length)
                    Array.Copy(data, srcOffset, destData, destOffset, texelPitch);
                }
            }

            return destData;
        }
        internal static int XGAddress2DTiledX(int Offset, int Width, int TexelPitch)
        {
            int AlignedWidth = (Width + 31) & ~31;

            int LogBpp = (TexelPitch >> 2) + ((TexelPitch >> 1) >> (TexelPitch >> 2));
            int OffsetB = Offset << LogBpp;
            int OffsetT = ((OffsetB & ~4095) >> 3) + ((OffsetB & 1792) >> 2) + (OffsetB & 63);
            int OffsetM = OffsetT >> (7 + LogBpp);

            int MacroX = ((OffsetM % (AlignedWidth >> 5)) << 2);
            int Tile = ((((OffsetT >> (5 + LogBpp)) & 2) + (OffsetB >> 6)) & 3);
            int Macro = (MacroX + Tile) << 3;
            int Micro = ((((OffsetT >> 1) & ~15) + (OffsetT & 15)) & ((TexelPitch << 3) - 1)) >> LogBpp;

            return Macro + Micro;
        }

        internal static int XGAddress2DTiledY(int Offset, int Width, int TexelPitch)
        {
            int AlignedWidth = (Width + 31) & ~31;

            int LogBpp = (TexelPitch >> 2) + ((TexelPitch >> 1) >> (TexelPitch >> 2));
            int OffsetB = Offset << LogBpp;
            int OffsetT = ((OffsetB & ~4095) >> 3) + ((OffsetB & 1792) >> 2) + (OffsetB & 63);
            int OffsetM = OffsetT >> (7 + LogBpp);

            int MacroY = ((OffsetM / (AlignedWidth >> 5)) << 2);
            int Tile = ((OffsetT >> (6 + LogBpp)) & 1) + (((OffsetB & 2048) >> 10));
            int Macro = (MacroY + Tile) << 3;
            int Micro = ((((OffsetT & (((TexelPitch << 6) - 1) & ~31)) + ((OffsetT & 15) << 1)) >> (3 + LogBpp)) & ~1);

            return Macro + Micro + ((OffsetT & 16) >> 4);
        }
    }
}
