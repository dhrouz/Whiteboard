// SymbolBar: floating math-symbol palette shown while editing text.
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
    class SymbolBar : Control
    {
        public static readonly string[] Syms = new string[] {
            "\u03C0", "\u221A", "\u00B2", "\u00B3", "\u00BD", "\u00B1", "\u00D7", "\u00F7",
            "\u2264", "\u2265", "\u2260", "\u2248", "\u221E", "\u00B0", "\u2220", "\u22A5",
            "\u2211", "\u222B", "\u03B8", "\u03B1", "\u03B2", "\u0394", "\u2192", "\u2208" };
        const int Cols = 8;
        public Theme Th;
        public Action<string> Picked;
        float U;
        int hot = -1, cell;
        Font font;

        public SymbolBar(float u, Theme th)
        {
            U = u; Th = th;
            SetStyle(ControlStyles.Selectable, false);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            TabStop = false;
            cell = (int)(34f * u);
            Size = new Size(Cols * cell + (int)(16f * u), 3 * cell + (int)(16f * u));
            font = new Font("Segoe UI Symbol", 17f * u, FontStyle.Regular, GraphicsUnit.Pixel);
            Cursor = Cursors.Hand;
        }

        Rectangle Cell(int i)
        {
            int pad = (int)(8f * U);
            return new Rectangle(pad + (i % Cols) * cell, pad + (i / Cols) * cell, cell, cell);
        }

        int HitCell(Point p)
        {
            for (int i = 0; i < Syms.Length; i++) if (Cell(i).Contains(p)) return i;
            return -1;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAlias;
            g.Clear(Th.Bg);
            RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            using (GraphicsPath p = Board.RoundRect(r, 12f * U))
            using (SolidBrush fb = new SolidBrush(Th.Panel))
            using (Pen bp = new Pen(Th.Border, 1f)) { g.FillPath(fb, p); g.DrawPath(bp, p); }
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center; sf.LineAlignment = StringAlignment.Center;
                for (int i = 0; i < Syms.Length; i++)
                {
                    Rectangle c = Cell(i);
                    if (i == hot)
                        using (GraphicsPath hp = Board.RoundRect(new RectangleF(c.X + 2, c.Y + 2, c.Width - 4, c.Height - 4), 8f * U))
                        using (SolidBrush hb = new SolidBrush(Th.AccentSoft)) g.FillPath(hb, hp);
                    using (SolidBrush tb = new SolidBrush(i == hot ? Th.Accent : Th.Ink)) g.DrawString(Syms[i], font, tb, new RectangleF(c.X, c.Y, c.Width, c.Height), sf);
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int h = HitCell(e.Location);
            if (h != hot) { hot = h; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hot != -1) { hot = -1; Invalidate(); }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            int h = HitCell(e.Location);
            if (h >= 0 && Picked != null) Picked(Syms[h]);
        }
    }
}
