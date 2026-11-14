// Board: splash screen and the animated teaching icon.
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
        static Color Ac(Color c, int a) { return Color.FromArgb(Math.Max(0, Math.Min(255, c.A * a / 255)), c.R, c.G, c.B); }

        static float EaseOutBack(float t)
        {
            t = Clamp(t, 0f, 1f);
            float c1 = 1.70158f, c3 = c1 + 1f, k = t - 1f;
            return 1f + c3 * k * k * k + c1 * k * k;
        }

        static void DrawStar4(Graphics g, float x, float y, float r, Color c)
        {
            if (r <= 0.2f) return;
            PointF[] pts = new PointF[8];
            for (int i = 0; i < 8; i++)
            {
                double a = -Math.PI / 2.0 + i * Math.PI / 4.0;
                float rr = (i % 2 == 0) ? r : r * 0.3f;
                pts[i] = new PointF(x + rr * (float)Math.Cos(a), y + rr * (float)Math.Sin(a));
            }
            using (SolidBrush b = new SolidBrush(c)) g.FillPolygon(b, pts);
        }

        // ---------- pixel-art scene (a 48 x 44 grid of big "pixels") ----------
        // A lavender "window" whiteboard on a round backdrop. Loop (5.6 s): the title-bar dots blink, a big marker draws a squiggle,
        // gold sparks fly, confetti bursts at the end, the smiling sticky note hops, then an eraser wipes the line away.
        // Sparkles twinkle, the backdrop breathes and little dots orbit it the whole time. Used by the splash screen and the About card.
        const string PxKeys = "OwVvlmrygGnNpWEZ";
        static readonly Color[] PxPal = new Color[] {
            Color.FromArgb(62, 40, 92),      // O outline
            Color.FromArgb(250, 247, 255),   // w canvas
            Color.FromArgb(156, 110, 236),   // V purple
            Color.FromArgb(112, 66, 204),    // v dark purple
            Color.FromArgb(221, 205, 252),   // l backdrop
            Color.FromArgb(190, 170, 244),   // m soft edge
            Color.FromArgb(240, 96, 96),     // r red dot
            Color.FromArgb(250, 200, 64),    // y yellow dot
            Color.FromArgb(112, 212, 72),    // g green dot
            Color.FromArgb(252, 192, 44),    // G gold
            Color.FromArgb(255, 228, 126),   // n note
            Color.FromArgb(240, 198, 92),    // N note shade
            Color.FromArgb(255, 142, 166),   // p blush
            Color.FromArgb(255, 255, 255),   // W white
            Color.FromArgb(222, 208, 244),   // E eraser
            Color.FromArgb(136, 86, 230) };  // Z squiggle

        static List<int[]> pxCurve;
        static List<int[]> PxSquigPts()
        {
            if (pxCurve != null) return pxCurve;
            List<int[]> l = new List<int[]>();
            int lx = -999, ly = -999;
            for (int i = 0; i <= 120; i++)
            {
                float u = i / 120f;
                int sx = 12 + (int)Math.Round(u * 21f), sy = 24 - (int)Math.Round(4.2f * Math.Sin(u * 7.2f));
                if (sx != lx || sy != ly) { l.Add(new int[] { sx, sy }); lx = sx; ly = sy; }
            }
            pxCurve = l;
            return l;
        }

        void DrawTeachIcon(Graphics g, float cx, float cy, float size, double t, int alpha) { DrawTeachIcon(g, cx, cy, size, t, alpha, 1f); }

        void DrawTeachIcon(Graphics g, float cx, float cy, float size, double t, int alpha, float appear)
        {
            if (alpha <= 2 || appear <= 0.001f) return;
            int px = Math.Max(1, (int)Math.Round(size / 44f));
            int A = (int)(alpha * Clamp(appear * 1.6f, 0f, 1f));
            int ox = (int)Math.Round(cx - 24f * px), oy = (int)Math.Round(cy - 22f * px) + (int)Math.Round((1f - Ease(appear)) * 8f) * px;
            double lt = t % 5.6;
            float pc = Ease((float)((lt - 0.6) / 1.9)), pw = Ease((float)((lt - 4.7) / 0.7));

            SmoothingMode osm = g.SmoothingMode; PixelOffsetMode opo = g.PixelOffsetMode;
            g.SmoothingMode = SmoothingMode.None; g.PixelOffsetMode = PixelOffsetMode.None;
            SolidBrush[] pal = new SolidBrush[PxPal.Length];
            for (int i = 0; i < pal.Length; i++) pal[i] = new SolidBrush(Ac(PxPal[i], A));
            try
            {
                Action<int, int, int, int, int> R = delegate (int ci, int rx, int ry, int rw, int rh) { g.FillRectangle(pal[ci], ox + rx * px, oy + ry * px, rw * px, rh * px); };
                int by = Math.Sin(t * 2.2) > 0.4 ? -1 : 0;                 // the board floats

                // round backdrop (breathes) + little orbiting dots
                int rad = 20 + (Math.Sin(t * 1.6) > 0.5 ? 1 : 0);
                for (int yy = 22 - rad; yy <= 22 + rad; yy++)
                {
                    int dy = yy - 22, half = (int)Math.Round(Math.Sqrt(Math.Max(0, rad * rad - dy * dy)));
                    if (half > 0) R(4, 24 - half, yy, half * 2, 1);
                }
                for (int k = 0; k < 3; k++)
                {
                    double oa = t * 0.9 + k * 2.1;
                    R(k == 1 ? 13 : 5, 24 + (int)Math.Round(25.0 * Math.Cos(oa)), 22 + (int)Math.Round(22.0 * Math.Sin(oa)), k == 0 ? 2 : 1, k == 0 ? 2 : 1);
                }

                // window board: purple title bar with three dots, white canvas, rounded outline
                R(2, 9, 10 + by, 29, 4); R(3, 9, 14 + by, 29, 1);
                R(1, 9, 15 + by, 29, 18);
                R(5, 9, 15 + by, 1, 18); R(5, 37, 15 + by, 1, 18); R(5, 9, 32 + by, 29, 1);
                R(0, 9, 9 + by, 29, 1); R(0, 9, 33 + by, 29, 1); R(0, 8, 10 + by, 1, 23); R(0, 38, 10 + by, 1, 23);
                R(6, 11, 11 + by, 2, 2); R(7, 15, 11 + by, 2, 2); R(8, 19, 11 + by, 2, 2);
                int dl = (int)(t * 3.0) % 6;                                   // the dots light up one after another
                if (dl < 3) R(13, 11 + dl * 4, 11 + by, 1, 1);

                // marker tray (+ eraser resting on it)
                bool wiping = lt >= 4.7 && lt < 5.5;
                R(2, 16, 34 + by, 16, 1); R(3, 17, 35 + by, 14, 1);
                if (!wiping) { R(0, 19, 30 + by, 8, 4); R(14, 20, 31 + by, 6, 2); R(13, 20, 31 + by, 6, 1); }

                // the squiggle
                List<int[]> sq = PxSquigPts();
                int cnt, xe = (int)Math.Round(33f - 22f * pw);
                if (pw > 0.001f) { cnt = 0; while (cnt < sq.Count && sq[cnt][0] <= xe) cnt++; }
                else cnt = pc > 0.002f ? Math.Min(sq.Count, (int)Math.Ceiling(pc * sq.Count)) : 0;
                for (int i = 0; i < cnt; i++) R(15, sq[i][0], sq[i][1] + by, 2, 2);

                // eraser wiping the line away (with a little dust)
                if (wiping)
                {
                    int exx = xe - 1, eyy = 21 + by;
                    R(0, exx, eyy, 7, 5); R(14, exx + 1, eyy + 1, 5, 3); R(13, exx + 1, eyy + 1, 5, 1);
                    int df = (int)(t * 14.0) % 3;
                    if (df != 0) R(13, exx + 8, eyy + 1 + df, 1, 1);
                }

                // marker: draws, hops away when done, wipes, glides back to the start
                int endX = sq[sq.Count - 1][0], endY = sq[sq.Count - 1][1], begX = sq[0][0], begY = sq[0][1];
                bool drawing = lt >= 0.6 && lt < 2.5;
                int tx, ty;
                if (drawing) { int[] pt = sq[Math.Max(0, cnt - 1)]; tx = pt[0]; ty = pt[1] + (((int)(t * 12.0) % 2) == 0 ? 0 : 1); }
                else if (lt < 0.6)
                {
                    float ms = Ease((float)(lt / 0.5));
                    tx = (int)Math.Round((endX + 3) + ((begX + 1) - (endX + 3)) * ms); ty = (int)Math.Round((endY - 6) + ((begY - 2) - (endY - 6)) * ms);
                }
                else if (lt < 4.7) { tx = endX + 2; ty = endY - 3 + (Math.Sin(t * 3.0) > 0 ? 0 : -1); }
                else { tx = endX + 3; ty = endY - 6; }
                ty += by;
                for (int mi = 0; mi < 16; mi++)
                {
                    int my = ty - mi, mx = tx + mi;
                    if (mi == 0) { R(0, mx, my, 1, 1); continue; }
                    if (mi <= 2) { R(0, mx - 1, my, 1, 1); R(3, mx, my, 1, 1); R(0, mx + 1, my, 1, 1); continue; }
                    if (mi == 3 || mi == 11) { R(0, mx - 2, my, 5, 1); continue; }
                    int cl, cm, cr;
                    if (mi <= 10) { cl = 13; cm = 13; cr = 14; } else { cl = 2; cm = 2; cr = 3; }
                    R(0, mx - 2, my, 1, 1); R(cl, mx - 1, my, 1, 1); R(cm, mx, my, 1, 1); R(cr, mx + 1, my, 1, 1); R(0, mx + 2, my, 1, 1);
                }
                if (drawing && ((int)(t * 10.0) % 2) == 0)                     // gold sparks at the tip
                {
                    R(9, tx - 5, ty - 3, 2, 1); R(9, tx - 4, ty - 7, 1, 2); R(9, tx - 1, ty - 9, 1, 2);
                }

                // confetti burst when the line is finished
                if (lt >= 2.5 && lt < 3.7)
                {
                    double tt = lt - 2.5;
                    int[] cc = new int[] { 6, 7, 8, 9, 2, 12 };
                    for (int k = 0; k < 12; k++)
                    {
                        double ang = 0.35 + k * 0.5, spd = 7.0 + (k % 3) * 3.0;
                        int fx = endX + 1 + (int)Math.Round(Math.Cos(ang) * spd * tt * 2.0);
                        int fy = endY - (int)Math.Round(Math.Sin(ang) * spd * tt * 2.0) + (int)Math.Round(16.0 * tt * tt) + by;
                        R(cc[k % 6], fx, fy, k % 2 == 0 ? 2 : 1, 1);
                    }
                }

                // smiling sticky note (hops when the line is finished, blinks, blushes)
                int ny = (Math.Sin(t * 2.6) > 0.5 ? -1 : 0);
                if (lt >= 2.5 && lt < 3.1) ny -= (int)Math.Round(4.0 * Math.Sin(Math.PI * (lt - 2.5) / 0.6));
                R(0, 3, 33 + ny, 11, 10); R(10, 4, 34 + ny, 9, 8); R(11, 4, 41 + ny, 9, 1);
                R(9, 7, 31 + ny, 3, 2); R(0, 8, 33 + ny, 1, 1);
                bool blink = (t % 3.1) < 0.12;
                R(0, 6, blink ? 37 + ny : 36 + ny, 1, blink ? 1 : 2); R(0, 10, blink ? 37 + ny : 36 + ny, 1, blink ? 1 : 2);
                R(0, 7, 38 + ny, 1, 1); R(0, 8, 39 + ny, 1, 1); R(0, 9, 38 + ny, 1, 1);
                if (Math.Sin(t * 2.0) > -0.5) { R(12, 4, 38 + ny, 2, 1); R(12, 11, 38 + ny, 2, 1); }

                // twinkling sparkles
                int[] spx = new int[] { 6, 2, 43, 44, 38 }, spy = new int[] { 7, 14, 29, 10, 38 }, spm = new int[] { 3, 2, 2, 1, 2 };
                double[] sph = new double[] { 0.0, 1.3, 2.4, 3.6, 4.9 };
                for (int k = 0; k < 5; k++)
                {
                    int sr = (int)Math.Round((Math.Sin(t * 3.2 + sph[k]) + 1.0) / 2.0 * spm[k]);
                    if (sr < 1) continue;
                    R(9, spx[k], spy[k] - sr, 1, sr * 2 + 1); R(9, spx[k] - sr, spy[k], sr * 2 + 1, 1);
                    if (sr >= 2) R(13, spx[k], spy[k], 1, 1);
                }
            }
            finally
            {
                foreach (SolidBrush b in pal) b.Dispose();
                g.SmoothingMode = osm; g.PixelOffsetMode = opo;
            }
        }

        void DrawSplash(Graphics g)
        {
            double t = (DateTime.Now - splashStart).TotalMilliseconds;
            double total = 3700.0, hold = 3050.0;
            if (t >= total) { splashActive = false; return; }
            double a = t < hold ? 1.0 : 1.0 - (t - hold) / (total - hold);   // opaque from the first frame, fades out at the end
            a = Math.Max(0.0, Math.Min(1.0, a));
            double ease = a * a * (3.0 - 2.0 * a);
            int al = (int)(255.0 * ease);
            int ca = (int)(255.0 * (t < hold ? 1.0 : Math.Max(0.0, 1.0 - (t - hold) / (total - hold))));
            float u = ui, cx = Width / 2f, cy = Height / 2f, tl = (float)(t / 1000.0);
            using (SolidBrush bg = new SolidBrush(Color.FromArgb(al, th.Bg))) g.FillRectangle(bg, 0, 0, Width, Height);
            g.TextRenderingHint = TextRenderingHint.AntiAlias;
            float pScene = Clamp((tl - 0.05f) / 0.70f, 0f, 1f), pText = Clamp((tl - 0.55f) / 0.65f, 0f, 1f);
            DrawTeachIcon(g, cx, cy - 34f * u, 190f * u, tl, ca, pScene);

            string credit = Loc.Tr("تم بواسطة أســامة");
            float tw = TWf(credit, uiSplash), rowY = cy + 112f * u, slide = (1f - Ease(pText)) * 22f * u;
            int ta = (int)(ca * Ease(pText));
            using (SolidBrush tb = new SolidBrush(Color.FromArgb(ta, th.Ink)))
                UiText(g, credit, uiSplash, tb, new RectangleF(cx - tw / 2f, rowY - 32f * u + slide, tw, 64f * u), fmtC);
        }


    }
}
