// Doc: per-tab document state.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace WhiteboardApp
{
    class Doc
    {
        public List<Item> Items = new List<Item>();
        public HashSet<int> Sel = new HashSet<int>();
        public Dictionary<int, Bitmap> Images = new Dictionary<int, Bitmap>();
        public Dictionary<int, string> B64 = new Dictionary<int, string>();
        public List<string> Undo = new List<string>();
        public List<string> Redo = new List<string>();
        public int NextId = 1, NextImg = 1;
        public float Vx, Vy, Vz = 1f;
        public bool ViewSet;
        public string Path;
        public const string DefaultName = "لوحة بدون عنوان"; // nowrap
        public string Name = DefaultName;
        public bool Dirty;
        public float AX, AW, Hv, Act;
        public bool AInit;
    }
}
