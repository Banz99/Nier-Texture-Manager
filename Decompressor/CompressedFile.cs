using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Decompressor
{
    class Compressedfile
    {
        public bool istexture;
        public int pointer;
        public int nameaddress;
        public int propertyaddress;
        public int datatype;
        public int dataoffset;
        public int datalenght;
        public int heap;
        public byte onvolumefile;
    }
}
