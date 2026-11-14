// CtxRow: one row of the right-click context menu.
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
    class CtxRow
    {
        public string Id, Label, Key, Icon;
        public bool Sep;
        public bool Danger;
        public float Hv;          // animated hover amount
        public RectangleF R;
    }
}
