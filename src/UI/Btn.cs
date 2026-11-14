// Btn: a custom-drawn button descriptor.
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
    class Btn
    {
        public string Id;
        public string Icon;
        public RectangleF R;
        public string Tip;
        public string Label;
        public bool Enabled = true;
        public bool Primary;
    }
}
