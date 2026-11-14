// Item: the single flat data class for every object on the board.
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
    class Item
    {
        public int Id;
        public Kind K;
        public int Col;
        public float Sw = 4f;
        public float Alpha = 1f;
        public List<PointF> Pts = new List<PointF>();
        public PointF[] Cache;
        public float X, Y, W, H, X2, Y2;
        public string Text = "";
        public float Size = 24f;
        public int Style;                // text style bits: 1 bold, 2 italic, 4 underline, 8 strikethrough
        public int Bg;
        public int ImgId;
        public bool Editing;
        public float Rot;
        public SizeF TextSz; public string TextSzKey;   // cached text measurement (see Board.MeasureText)
        // coordinate-axes data (math)
        public float Ox, Oy, XMin = -8f, XMax = 8f, YMin = -6f, YMax = 6f, Unit = 32f, Step = 1f;
        public int Flags = 7;
        public List<string> Funcs = new List<string>();
        public List<PointF> MPts = new List<PointF>();

        public void Move(float dx, float dy)
        {
            if (K == Kind.Stroke)
            {
                for (int i = 0; i < Pts.Count; i++) Pts[i] = new PointF(Pts[i].X + dx, Pts[i].Y + dy);
                Cache = null;
            }
            else if (K == Kind.Line || K == Kind.Arrow) { X += dx; Y += dy; X2 += dx; Y2 += dy; }
            else if (K == Kind.Axes) { Ox += dx; Oy += dy; }
            else { X += dx; Y += dy; }
        }
    }
}
