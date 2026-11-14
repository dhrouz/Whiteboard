// AppFonts: the single UI font (Segoe UI, ships with Windows).
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
    // Segoe UI ships with Windows, so no font files are embedded or licensed by this app.
    static class AppFonts
    {
        public const string Family = "Segoe UI";

        public static Font Make(float px, FontStyle style)
        {
            return new Font(Family, px, style, GraphicsUnit.Pixel);
        }
    }
}
