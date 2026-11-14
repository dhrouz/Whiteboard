// Board: button rendering and tooltips.
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
    partial class Board : Control
    {
        bool IsActive(Btn b)
        {
            if (b.Id.StartsWith("t:")) return b.Id.Substring(2).Equals(tool.ToString(), StringComparison.OrdinalIgnoreCase);
            if (b.Id == "fmt:+" || b.Id == "fmt:-") return false;
            if (b.Id.StartsWith("fmt:")) { Item ft = FmtTarget(); return ft != null && (ft.Style & FmtBit(b.Id)) != 0; }
            if (b.Id == "grid") return grid;
            if (b.Id == "laser") return laserOn;
            if (b.Id == "math") return saveMenu && popKind == "math";
            if (b.Id == "pin") return toolsPinned;
            if (b.Id == "zfit") return form.IsFullscreen;
            if (b.Id == "ax:tgrid") return axItem != null && (axItem.Flags & 1) != 0;
            if (b.Id == "ax:tnum") return axItem != null && (axItem.Flags & 2) != 0;
            if (b.Id == "ax:tlab") return axItem != null && (axItem.Flags & 4) != 0;
            if (b.Id == "cc") return col >= 1000;
            if (b.Id.StartsWith("c:")) return col == int.Parse(b.Id.Substring(2));
            if (b.Id.StartsWith("s:")) return sizeIdx == int.Parse(b.Id.Substring(2));
            return false;
        }

        void DrawBtn(Graphics g, Btn b)
        {
            float hv = b.Enabled ? Get(hovAmt, b.Id) : 0f, pr = Get(pressAmt, b.Id), ac = Get(actAmt, b.Id);
            RectangleF r = b.R;
            float cxm = r.X + r.Width / 2f, cym = r.Y + r.Height / 2f;
            GraphicsState gs = g.Save();
            if (pr > 0.002f)
            {
                float sc = 1f - 0.05f * pr;
                g.TranslateTransform(cxm, cym); g.ScaleTransform(sc, sc); g.TranslateTransform(-cxm, -cym);
            }
            DrawBtnCore(g, b, r, cxm, cym, hv, pr, ac);
            g.Restore(gs);
        }

        void DrawBtnCore(Graphics g, Btn b, RectangleF r, float cxm, float cym, float hv, float pr, float ac)
        {
            bool isSwatch = b.Id.StartsWith("c:"), isSize = b.Id.StartsWith("s:");
            int ra = (int)(255f * ac);

            if (b.Id == "cc")
            {
                float crad = (10f + 1.5f * hv) * ui;
                Color[] rb = new Color[] { Color.FromArgb(239, 68, 68), Color.FromArgb(249, 115, 22), Color.FromArgb(234, 179, 8), Color.FromArgb(34, 197, 94), Color.FromArgb(6, 182, 212), Color.FromArgb(99, 102, 241), Color.FromArgb(217, 70, 239) };
                for (int k = 0; k < 7; k++)
                    using (SolidBrush rbb = new SolidBrush(rb[k])) g.FillPie(rbb, cxm - crad, cym - crad, crad * 2f, crad * 2f, k * 360f / 7f, 360f / 7f + 1f);
                if (col >= 1000)
                {
                    float ir = crad * 0.58f;
                    using (SolidBrush cb = new SolidBrush(Ink(col))) g.FillEllipse(cb, cxm - ir, cym - ir, ir * 2f, ir * 2f);
                    using (Pen wp = new Pen(Color.White, 1.5f * ui)) g.DrawEllipse(wp, cxm - ir, cym - ir, ir * 2f, ir * 2f);
                }
                else using (Pen bp = new Pen(Color.FromArgb(60, 128, 128, 128), 1f)) g.DrawEllipse(bp, cxm - crad, cym - crad, crad * 2f, crad * 2f);
                if (ra > 3)
                {
                    float rr = crad + (3.5f + (1f - ac) * 2.5f) * ui;
                    using (Pen ap = new Pen(Color.FromArgb(ra, th.Accent), 2.2f * ui)) g.DrawEllipse(ap, cxm - rr, cym - rr, rr * 2f, rr * 2f);
                }
                return;
            }
            if (isSwatch)
            {
                int i = int.Parse(b.Id.Substring(2));
                float rad = (10f + 1.5f * hv) * ui;
                using (SolidBrush br = new SolidBrush(Ink(i))) g.FillEllipse(br, cxm - rad, cym - rad, rad * 2, rad * 2);
                using (Pen bp = new Pen(Color.FromArgb(50, 128, 128, 128), 1f)) g.DrawEllipse(bp, cxm - rad, cym - rad, rad * 2, rad * 2);
                if (ra > 3)
                {
                    float rr = rad + (3.5f + (1f - ac) * 2.5f) * ui;
                    using (Pen ap = new Pen(Color.FromArgb(ra, th.Accent), 2.2f * ui)) g.DrawEllipse(ap, cxm - rr, cym - rr, rr * 2f, rr * 2f);
                }
                return;
            }
            if (isSize)
            {
                int i = int.Parse(b.Id.Substring(2));
                float d = (5f + i * 4f) * ui * (1f + 0.12f * hv);
                if (hv > 0.01f || ac > 0.01f)
                    using (GraphicsPath p = RoundRect(RectangleF.Inflate(r, -1f * ui, -1f * ui), 9f * ui))
                    {
                        if (hv > 0.01f) using (SolidBrush hb = new SolidBrush(Color.FromArgb((int)(255f * hv * (1f - ac)), th.Hover))) g.FillPath(hb, p);
                        if (ac > 0.01f) using (SolidBrush ab = new SolidBrush(Color.FromArgb(ra, th.AccentSoft))) g.FillPath(ab, p);
                    }
                using (SolidBrush br = new SolidBrush(MixC(th.Ink, th.Accent, ac))) g.FillEllipse(br, cxm - d / 2f, cym - d / 2f, d, d);
                return;
            }

            Color fg = th.Ink;
            bool isTool = b.Id.StartsWith("t:");
            if (b.Primary)
            {
                for (int k = 3; k >= 1; k--)
                {
                    RectangleF gr2 = RectangleF.Inflate(r, k * 1.6f * ui, k * 1.6f * ui); gr2.Offset(0, 2f * ui);
                    using (GraphicsPath gp = RoundRect(gr2, 9f * ui + k * 1.6f * ui))
                    using (SolidBrush gb = new SolidBrush(Color.FromArgb((int)(9f + 9f * hv), th.Accent))) g.FillPath(gb, gp);
                }
                Color c1 = MixC(MixC(th.Accent, Color.White, 0.12f * hv), Color.Black, 0.14f * pr);
                Color c2 = MixC(MixC(Color.FromArgb(139, 92, 246), Color.White, 0.12f * hv), Color.Black, 0.14f * pr);
                using (GraphicsPath p = RoundRect(r, 9f * ui))
                using (LinearGradientBrush pb = new LinearGradientBrush(r, c1, c2, 20f)) g.FillPath(pb, p);
                fg = Color.White;
            }
            else
            {
                float hva = hv * (1f - ac);
                if ((!isTool && ac > 0.01f) || hva > 0.01f || pr > 0.01f)
                    using (GraphicsPath p = RoundRect(r, 10f * ui))
                    {
                        if (!isTool && ac > 0.01f) using (SolidBrush ab = new SolidBrush(Color.FromArgb(ra, th.AccentSoft))) g.FillPath(ab, p);
                        if (hva > 0.01f) using (SolidBrush hb = new SolidBrush(Color.FromArgb((int)(255f * hva), th.Hover))) g.FillPath(hb, p);
                        if (pr > 0.01f) using (SolidBrush pb2 = new SolidBrush(Color.FromArgb((int)(150f * pr), th.Border))) g.FillPath(pb2, p);
                    }
                fg = MixC(th.Ink, th.Accent, ac);
            }
            if (!b.Enabled) fg = Color.FromArgb(90, fg);

            float ip = 10f * ui, iw = 20f * ui;
            if (b.Icon != null && b.Label != null)
            {
                float tx, trw;
                if (En)
                {
                    DrawIcon(g, b.Icon, r.X + ip + iw / 2f, cym, fg);
                    tx = r.X + ip + iw + 8f * ui; trw = r.Right - ip * 0.5f - tx;
                }
                else
                {
                    DrawIcon(g, b.Icon, r.Right - ip - iw / 2f, cym, fg);
                    tx = r.X + ip * 0.5f; trw = r.Right - ip - iw - 8f * ui - tx;
                }
                RectangleF tr = new RectangleF(tx, r.Y, trw, r.Height);
                using (SolidBrush tb = new SolidBrush(fg)) UiText(g, b.Label, b.Primary ? uiBold : uiFont, tb, tr, fmtRN);
            }
            else if (b.Label != null)
            {
                using (SolidBrush tb = new SolidBrush(fg)) UiText(g, b.Label, b.Primary ? uiBold : uiFont, tb, r, fmtC);
            }
            else if (b.Icon != null) DrawIcon(g, b.Icon, cxm, cym, fg);
        }

        RectangleF TipRect(Btn b)
        {
            float w = TW(b.Tip) + 26f * ui, h = 30f * ui, gap = 10f * ui;
            float x, y;
            if (b.Id.StartsWith("t:") || b.Id == "image" || b.Id == "math") { x = En ? b.R.Right + gap + 6f * ui : b.R.X - gap - w - 6f * ui; y = b.R.Y + (b.R.Height - h) / 2f; }
            else if (b.R.Y < Height / 2f) { x = b.R.X + (b.R.Width - w) / 2f; y = b.R.Bottom + gap; }
            else { x = b.R.X + (b.R.Width - w) / 2f; y = b.R.Y - gap - h - 6f * ui; }
            x = Clamp(x, 6f * ui, Width - w - 6f * ui);
            return new RectangleF(x, y, w, h);
        }

        void DrawTip(Graphics g, Btn b)
        {
            RectangleF r = TipRect(b);
            using (GraphicsPath p = RoundRect(r, 8f * ui))
            using (SolidBrush bb = new SolidBrush(th.Ink))
            using (SolidBrush tb = new SolidBrush(th.Bg))
            { g.FillPath(bb, p); UiText(g, b.Tip, uiFont, tb, r, fmtRC); }
        }


    }
}
