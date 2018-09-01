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

        private void btnUn_Click(object sender, EventArgs e)
        {
            try
            {
                OpenFileDialog op = new OpenFileDialog();
                op.Filter = "All Nier Volume Files (*.2DV;*.MDV;*.VIR;*.EFV)|*.2DV;*.MDV;*.VIR;*.EFV|Nier Font File (FONT_MAIN.PS3.BIN;FONT_MAIN_JP.PS3.BIN)|FONT_MAIN.PS3.BIN;FONT_MAIN_JP.PS3.BIN";
                if (op.ShowDialog() == DialogResult.OK)
                {
                    string definizioni = op.FileName;
                    string datiimmagini = op.FileName.Substring(0,definizioni.Length-4); //This will handle file with double extension
                    if (op.FileName.Split('.').Last() == "2DV") //This just assign the corresponding package to each one
                    {
                        datiimmagini += ".2DP";
                    }
                    else if (op.FileName.Split('.').Last() == "MDV")
                    {
                        datiimmagini += ".MDP";
                    }
                    else if (op.FileName.Split('.').Last() == "VIR")
                    {
                        datiimmagini += ".PHY";
                    }
                    else if (op.FileName.Split('.').Last() == "EFV")
                    {
                        datiimmagini += ".EFP";
                    }
                    else if (op.FileName.Split('.').Last() == "BIN")
                    {
                        int index =definizioni.LastIndexOf("MAIN");
                        datiimmagini = definizioni.Substring(0, index);
                        datiimmagini += "VRAM";
                        datiimmagini += definizioni.Substring(index+4,definizioni.Length-index-4);
                    }
                    else
                    {
                        throw new Exception("Invalid or unknown file selected");
                    }
                    if (File.Exists(datiimmagini))
                    {
                        byte[] def = File.ReadAllBytes(definizioni);
                        List<int> heappos = new List<int>(); //There are files with more than 1 heap header
                        int i = 0;
                        while (i < def.Length-4)
                        {
                            if (System.Text.Encoding.UTF8.GetString(def, i, 4) == "HEAP") //Check for presence of one HEAP segment inside descriptor
                            {
                                heappos.Add(i);
                            }
                            i++;
                        }
                        if (heappos.Count()==0)
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
                            byte[] dati = File.ReadAllBytes(datiimmagini);
                            bool lzo = false;
                            if (System.Text.Encoding.UTF8.GetString(dati, 0, 3) == "lzo")
                            {
                                lzo = true;
                                Stream temp = File.OpenWrite(datiimmagini+".tmp");
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
                                read = read & 0xfff0000; //This will 99.99% of the time result in a simple 0x20000 increment from before, but we need to be sure
                                read = read + 0x10000;
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
                                    read += 0xC + compressedchuncklength;
                                    read = read & 0xfff0000;
                                    read = read + 0x10000;
                                   /* if (ms.Length >= size) //This seemed to be required at some point, now i may have fixed enough things that it isn't needed anymore
                                    {
                                        i = nchuncks;
                                        ms.SetLength(size);
                                    }*/
                                }
                                ms.WriteTo(temp);
                                temp.Close();
                                dati = File.ReadAllBytes(datiimmagini + ".tmp"); 
                                MessageBox.Show("LZO decompression done");
                            }
                            List<int> heapoffset = new List<int>();
                            int next=0x0;
                            if (System.Text.Encoding.UTF8.GetString(dati, 0, 4) == "KPKy") //Some files have this KPKy header with some unknown data, every dataoffset specified starts from the end of them
                            {
                                Array.Copy(dati, next+0x4, punt, 0, 4);
                                Array.Reverse(punt);
                                int volte=BitConverter.ToInt32(punt, 0);
                                int k=0;
                                for (i=0;i<volte;i++)
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
                                NierTextureDesc.Add("Extracted from: "+datiimmagini);
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
                                        if (compressiontype == 0x5)
                                        { //uncompressed (must calculate pitch)
                                            //Array.Copy(def, list[i].propertyaddress + 18, punt, 0, 2); it's already specified in some files, but better be sure                   
                                            pitch = (width * 32 + 7) / 8;
                                            punt = BitConverter.GetBytes(pitch);
                                            header[20] = punt[0];
                                            header[21] = punt[1];
                                            header[22] = punt[2];
                                            header[23] = punt[3];
                                            header[8] = 0x0F;
                                            header[80] = 0x41;
                                            header[88] = 0x20;
                                            header[94] = 0xFF;
                                            header[97] = 0xFF;
                                            header[100] = 0xFF;
                                            header[107] = 0xFF;
                                            header[10] = 0x0;
                                            header[34] = 0x0;
                                            header[84] = 0x0;
                                            header[85] = 0x0;
                                            header[86] = 0x0;
                                            header[87] = 0x0;
                                            header[108] = 0x0;
                                            header[110] = 0x0;
                                            if (!deswizzle)
                                                compression = "Uncompressed";
                                            else
                                                compression = "Un. Swizzled";
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
                                            if (compressiontype == 0x8)
                                            {
                                                header[87] = 0x35; //DXT5
                                                compression = "DXT5";
                                            }
                                            else if (compressiontype == 0x6)
                                            {
                                                header[87] = 0x31; //DXT1
                                                compression = "DXT1";
                                            }
                                            else
                                            {
                                                if (compressiontype != 0x7)
                                                    MessageBox.Show(name + " " + (list[i].propertyaddress + 8) + " uses an unknown compression format");
                                                header[87] = 0x33; //DXT3, most common one
                                                compression = "DXT3";
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
                                        myfile.Write(header, 0, header.Length);
                                        filedata = new byte[list[i].datalenght];
                                        Array.Copy(dati, list[i].dataoffset, filedata, 0, list[i].datalenght);
                                        if (deswizzle)
                                        {
                                            List<byte> swizzled = new List<byte>();
                                            int index = -1;
                                            for (int t = 0; t < width; t++)
                                            {
                                                for (int y = 0; y < height; y++)
                                                {
                                                    index = calcZOrder(y, t);
                                                    swizzled.Add(filedata[index * 4]);
                                                    swizzled.Add(filedata[index * 4 + 1]);
                                                    swizzled.Add(filedata[index * 4 + 2]);
                                                    swizzled.Add(filedata[index * 4 + 3]);
                                                }
                                            }
                                            filedata = swizzled.ToArray();
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
                                if (File.Exists(datiimmagini+".tmp"))
                                {
                                    File.Delete(datiimmagini + ".tmp");
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
                        op.Filter = "All Nier Package Files (*.2DP;*.MDP;*.PHY;*.EFP)|*.2DP;*.MDP;*.PHY;*.EFP";
                        if (op.ShowDialog() == DialogResult.OK)
                        {
                            string datiimmagini = op.FileName;                        
                            string definizioni = op.FileName.Substring(0, datiimmagini.Length - 4); //This will handle file with double extension
                            if (op.FileName.Split('.').Last() == "2DP") //This just assign the corresponding volume to each one
                            {
                                definizioni += ".2DV";
                            }
                            else if (op.FileName.Split('.').Last() == "MDP")
                            {
                                definizioni += ".MDV";
                            }
                            else if (op.FileName.Split('.').Last() == "PHY")
                            {
                                definizioni += ".VIR";
                            }
                            else if (op.FileName.Split('.').Last() == "EFP")
                            {
                                definizioni += ".EFV";
                            }
                            else
                            {
                                throw new Exception("Invalid or unknown file selected");
                            }
                            if (File.Exists(definizioni))
                            {
                                byte[] def = File.ReadAllBytes(definizioni);
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
                                    if (System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(datiimmagini), 0, 3) == "lzo")
                                    {
                                        lzo = true;
                                        punt = new byte[4];
                                        byte[] lzodata = File.ReadAllBytes(datiimmagini);
                                        Stream temp = File.OpenWrite(datiimmagini + ".tmp");
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
                                        datiimmagini = datiimmagini + ".tmp";
                                    }
                                    var dati = File.ReadAllBytes(datiimmagini);
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
                                    Stream stream = File.OpenWrite(datiimmagini);
                                    List<string> nomi = new List<string>();
                                    for (int j = 0; j < list.Count(); j++)
                                    {
                                        string nome = "";
                                        int k = list[j].nameaddress;
                                        while (def[k] != 0) 
                                        {
                                            nome += System.Text.Encoding.UTF8.GetString(def, k, 1);
                                            k++;
                                        }
                                        /*if(list[j].istexture) //Will fix when we can let everything to be reinjected back
                                        nomi.Add(nome);
                                        else nomi.Add(nome+"_"+System.Text.Encoding.UTF8.GetString(def, list[j].pointer, 4));*/
                                        if (nomi.Contains(nome))
                                        {
                                            k = 1;
                                            while (nomi.Contains(nome+"_"+k))
                                            {
                                                k++;
                                            }
                                            nomi.Add(nome + "_" + k);
                                        }
                                        else
                                            nomi.Add(nome);
                                    }
                                    int fileoffset=0;
                                    for (i = 0; i < files.Length; i++)
                                    {
                                        try
                                        {
                                            int modifyindex;
                                            if (nomi.IndexOf(files[i].Substring(0, files[i].Length - 4)) != -1) //Get index of the TX2D description by checking if filename corresponds to any of them
                                            {
                                                modifyindex = nomi.IndexOf(files[i].Substring(0, files[i].Length - 4));
                                            }
                                            else
                                            {
                                                throw new Exception("No matching file found for "+files[i]);
                                            }
                                            byte[] imgdata = File.ReadAllBytes(folder+"\\"+files[i]);
                                            byte[] noheader = new byte[imgdata.Length - 128];
                                            Array.Copy(imgdata,128, noheader,0,imgdata.Length-128);
                                            if (noheader.Length % 0x80 != 0) //data must be 128-byte aligned, so it will probably need padding (expecially after being modified)
                                            {
                                                byte[] noheaderpad = new byte[noheader.Length];
                                                Array.Copy(noheader,noheaderpad,noheader.Length);
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
                                            if (noheader.Length != list[modifyindex].datalenght) //if file to inject is bigger or smaller than the original
                                            {
                                                MessageBox.Show("File size of " + files[i] + " doesn't match the original. Attempting to reaarrange pointers");
                                                Stream deffile = File.OpenWrite(definizioni);
                                                punt = new byte[2];
                                                deffile.Position = list[modifyindex].propertyaddress + 1; //Number of mipmaps
                                                if(imgdata[0x1C]!=0)
                                                deffile.Write(imgdata, 0x1C, 1);
                                                Array.Copy(imgdata, 16, punt, 0, 2); //Width
                                                Array.Reverse(punt);
                                                deffile.Position = list[modifyindex].propertyaddress + 8;
                                                deffile.Write(punt, 0, punt.Length);
                                                Array.Copy(imgdata, 12, punt, 0, 2); //Height
                                                Array.Reverse(punt);
                                                deffile.Position = list[modifyindex].propertyaddress + 10; 
                                                deffile.Write(punt, 0, punt.Length);
                                                punt = BitConverter.GetBytes(noheader.Length);
                                                Array.Reverse(punt);
                                                deffile.Position = list[modifyindex].pointer + 24;//Data lenght
                                                deffile.Write(punt, 0, punt.Length);
                                                int offsetpos = noheader.Length -list[modifyindex].datalenght;
                                                fileoffset+=offsetpos;
                                                list[modifyindex].datalenght = noheader.Length;                                               
                                                for (int j = 0; j < list.Count(); j++)
                                                {
                                                    if (list[j].pointer > list[modifyindex].pointer) //Adjust Data Offset for consequent pointers
                                                    {
                                                        if (list[j].onvolumefile==0x0) //files with no data on Package file will have 1 as value
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
                                                Stream str2 = File.OpenRead(datiimmagini);
                                                str2.Position = (list[modifyindex].dataoffset+list[modifyindex].datalenght-offsetpos);
                                                MemoryStream ms = new MemoryStream();
                                                byte[] buffer = new Byte[2048];
                                                int length;
                                                while ((length = str2.Read(buffer, 0, buffer.Length)) > 0)
                                                ms.Write(buffer, 0, length);
                                                byte[] nextdata = ms.ToArray();
                                                str2.Close();
                                                stream = File.OpenWrite(datiimmagini);
                                                stream.Position = list[modifyindex].dataoffset;
                                                stream.Write(noheader, 0, list[modifyindex].datalenght);
                                                stream.Position = list[modifyindex].dataoffset+list[modifyindex].datalenght;
                                                stream.Write(nextdata, 0, nextdata.Length);
                                            }
                                            else
                                            {
                                                stream.Position = list[modifyindex].dataoffset;
                                                stream.Write(noheader, 0, noheader.Length);
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            stream.Close();
                                            throw new Exception(ex.Message);
                                        }
                                    }
                                    if (fileoffset<0) //Should happen only when you inject texture smaller than the original, helps removing garbage data
                                    {
                                        stream.SetLength(stream.Length+fileoffset);
                                    }
                                    stream.Close();
                                    if (lzo)
                                    {
                                        dati = File.ReadAllBytes(datiimmagini.Substring(0, datiimmagini.Length - 4));
                                        byte[] datinoncomp = File.ReadAllBytes(datiimmagini);
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
                                        } while (result.Length >= 0x20000);
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
                                        while (uncompressedchunckpointer < datinoncomp.Length)
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
                                        File.Delete(datiimmagini.Substring(0, datiimmagini.Length - 4));
                                        Stream lzofile = File.Create(datiimmagini.Substring(0, datiimmagini.Length - 4));
                                        ms.WriteTo(lzofile);
                                        lzofile.Close();
                                        File.Delete(datiimmagini);
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
        public Int32 calcZOrder(int xPos, int yPos)
        {
            Int32[] MASKS = { 0x55555555, 0x33333333, 0x0F0F0F0F, 0x00FF00FF, };
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
    }
}
