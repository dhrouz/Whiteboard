// Board: drawing of ruler, protractor, axes, function plots.
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
        // ---------- math objects ----------
        static string NumStr(double v)
        {
            if (Math.Abs(v - Math.Round(v)) < 1e-9) return ((long)Math.Round(v)).ToString(CultureInfo.InvariantCulture);
            return v.ToString("0.##", CultureInfo.InvariantCulture);
        }

        static MathFn GetFn(string expr)
        {
            MathFn f;
            if (fnCache.TryGetValue(expr, out f)) return f;
            f = ExprParser.Compile(expr);
            if (fnCache.Count > 200) fnCache.Clear();
            fnCache[expr] = f;
            return f;
        }

        void DrawRuler(Graphics g, Item it)
        {
            RectangleF r = new RectangleF(it.X, it.Y, it.W, it.H);
            for (int i = 3; i >= 1; i--)
            {
                RectangleF sr = RectangleF.Inflate(r, i * 1.5f, i * 1.5f); sr.Offset(0f, 3f + i);
                using (GraphicsPath sp = RoundRect(sr, 7f + i))
                using (SolidBrush sb = new SolidBrush(Color.FromArgb(14, 0, 0, 0))) g.FillPath(sb, sp);
            }
            using (GraphicsPath bp = RoundRect(r, 7f))
            {
                using (LinearGradientBrush lb = new LinearGradientBrush(r, Color.FromArgb(242, 255, 240, 168), Color.FromArgb(242, 248, 210, 100), 90f)) g.FillPath(lb, bp);
                using (Pen op = new Pen(Color.FromArgb(225, 190, 146, 48), 1.4f)) g.DrawPath(op, bp);
            }
            Color tick = Color.FromArgb(235, 66, 50, 14);
            float mm = CM / 10f, x0 = r.X + 12f;
            int n = (int)((r.Width - 24f) / mm);
            Font nf = GetFont("Segoe UI", 11f), uf = GetFont("Segoe UI", 10f);
            using (Pen tp = new Pen(tick, 1f))
            using (SolidBrush tb = new SolidBrush(tick))
            {
                for (int i = 0; i <= n; i++)
                {
                    float x = x0 + i * mm;
                    float len = (i % 10 == 0) ? 20f : (i % 5 == 0 ? 14f : 8f);
                    tp.Width = (i % 10 == 0) ? 1.5f : 1f;
                    g.DrawLine(tp, x, r.Y, x, r.Y + len);
                    if (i % 10 == 0) g.DrawString((i / 10).ToString(CultureInfo.InvariantCulture), nf, tb, new RectangleF(x - 14f, r.Y + 21f, 28f, 16f), NumFmt);
                }
                g.DrawString("cm", uf, tb, new RectangleF(r.Right - 36f, r.Bottom - 20f, 30f, 14f), NumFmt);
            }
        }

        void DrawProtractor(Graphics g, Item it)
        {
            float R = it.W / 2f, baseH = Math.Max(10f, it.H - R);
            float cx = it.X + R, cy = it.Y + R + baseH * 0.5f;
            Color ink = Color.FromArgb(235, 30, 84, 146);
            using (GraphicsPath body = new GraphicsPath())
            {
                body.AddLine(cx - R, cy + baseH * 0.5f, cx - R, cy);
                body.AddArc(cx - R, cy - R, 2f * R, 2f * R, 180f, 180f);
                body.AddLine(cx + R, cy, cx + R, cy + baseH * 0.5f);
                body.CloseFigure();
                using (GraphicsPath sh = (GraphicsPath)body.Clone())
                {
                    using (Matrix mx = new Matrix()) { mx.Translate(0f, 4f); sh.Transform(mx); }
                    using (SolidBrush sb = new SolidBrush(Color.FromArgb(24, 0, 0, 0))) g.FillPath(sb, sh);
                }
                using (SolidBrush fb = new SolidBrush(Color.FromArgb(208, 198, 229, 255))) g.FillPath(fb, body);
                using (Pen op = new Pen(Color.FromArgb(235, 62, 122, 192), 1.6f)) g.DrawPath(op, body);
            }
            float rin = R * 0.60f;
            using (Pen ring = new Pen(Color.FromArgb(120, 62, 122, 192), 1f)) g.DrawArc(ring, cx - rin, cy - rin, 2f * rin, 2f * rin, 180f, 180f);
            using (Pen tp = new Pen(ink, 1f))
            using (SolidBrush tb = new SolidBrush(ink))
            using (SolidBrush mb = new SolidBrush(Color.FromArgb(170, 30, 84, 146)))
            {
                float fs = Math.Max(8f, R * 0.072f);
                Font nf = GetFont("Segoe UI", fs), sf = GetFont("Segoe UI", fs * 0.82f);
                for (int d = 0; d <= 180; d++)
                {
                    double a = d * Math.PI / 180.0;
                    float ca = (float)Math.Cos(a), sa = (float)Math.Sin(a);
                    float len = (d % 10 == 0) ? R * 0.11f : (d % 5 == 0 ? R * 0.08f : R * 0.045f);
                    tp.Width = (d % 10 == 0) ? 1.3f : 0.9f;
                    g.DrawLine(tp, cx + R * ca, cy - R * sa, cx + (R - len) * ca, cy - (R - len) * sa);
                    if (d % 10 == 0)
                    {
                        float ro = R * 0.80f, ri = R * 0.50f;
                        g.DrawString(d.ToString(CultureInfo.InvariantCulture), nf, tb, new RectangleF(cx + ro * ca - fs * 1.5f, cy - ro * sa - fs * 0.7f, fs * 3f, fs * 1.4f), NumFmt);
                        g.DrawString((180 - d).ToString(CultureInfo.InvariantCulture), sf, mb, new RectangleF(cx + ri * ca - fs * 1.4f, cy - ri * sa - fs * 0.6f, fs * 2.8f, fs * 1.2f), NumFmt);
                    }
                }
                tp.Width = 1.2f;
                g.DrawLine(tp, cx - R, cy, cx + R, cy);
                g.DrawLine(tp, cx, cy - R * 0.06f, cx, cy + R * 0.06f);
            }
        }

        void PlotFunc(Graphics g, Item it, MathFn f, Color c)
        {
            float u = it.Unit;
            int n = Math.Max(160, (int)((it.XMax - it.XMin) * u / 2f));
            double span = it.YMax - it.YMin, lo = it.YMin - 1.0, hi = it.YMax + 1.0;
            List<PointF> seg = new List<PointF>();
            using (Pen pen = new Pen(c, 2.6f))
            {
                pen.LineJoin = LineJoin.Round; pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round;
                bool prevOk = false; double prevY = 0.0;
                for (int i = 0; i <= n; i++)
                {
                    double x = it.XMin + (it.XMax - it.XMin) * i / (double)n;
                    double y = f(x);
                    bool ok = !(double.IsNaN(y) || double.IsInfinity(y)) && y >= lo && y <= hi;
                    if (ok && prevOk && Math.Abs(y - prevY) > span * 1.5) ok = false;
                    if (!ok)
                    {
                        if (seg.Count >= 2) g.DrawLines(pen, seg.ToArray());
                        seg.Clear(); prevOk = false;
                        continue;
                    }
                    seg.Add(new PointF(it.Ox + (float)x * u, it.Oy - (float)y * u));
                    prevOk = true; prevY = y;
                }
                if (seg.Count >= 2) g.DrawLines(pen, seg.ToArray());
            }
        }

        void DrawAxes(Graphics g, Item it)
        {
            Color ink = Ink(it.Col);
            float u = it.Unit, ox = it.Ox, oy = it.Oy;
            float x1 = ox + it.XMin * u, x2 = ox + it.XMax * u, y1 = oy - it.YMax * u, y2 = oy - it.YMin * u;
            RectangleF area = new RectangleF(x1, y1, x2 - x1, y2 - y1);
            double stp = it.Step <= 0f ? 1.0 : it.Step;

            // grid (clipped to the plane)
            if ((it.Flags & 1) != 0)
            {
                GraphicsState gs = g.Save();
                g.SetClip(RectangleF.Inflate(area, 1f, 1f));
                using (Pen minor = new Pen(Color.FromArgb(34, ink), 1f))
                using (Pen major = new Pen(Color.FromArgb(80, ink), 1f))
                {
                    double ms = Math.Min(1.0, stp);
                    int j0 = (int)Math.Ceiling(it.XMin / ms - 1e-9), j1 = (int)Math.Floor(it.XMax / ms + 1e-9);
                    for (int j = j0; j <= j1; j++)
                    {
                        double v = j * ms; if (Math.Abs(v) < 1e-9) continue;
                        bool mj = Math.Abs(v / stp - Math.Round(v / stp)) < 1e-6;
                        float gx = ox + (float)v * u;
                        g.DrawLine(mj ? major : minor, gx, y1, gx, y2);
                    }
                    int k0 = (int)Math.Ceiling(it.YMin / ms - 1e-9), k1 = (int)Math.Floor(it.YMax / ms + 1e-9);
                    for (int k = k0; k <= k1; k++)
                    {
                        double v = k * ms; if (Math.Abs(v) < 1e-9) continue;
                        bool mj = Math.Abs(v / stp - Math.Round(v / stp)) < 1e-6;
                        float gy = oy - (float)v * u;
                        g.DrawLine(mj ? major : minor, x1, gy, x2, gy);
                    }
                }
                g.Restore(gs);
            }

            // functions + points (clipped to the plane)
            GraphicsState gs2 = g.Save();
            g.SetClip(RectangleF.Inflate(area, 1f, 1f));
            for (int k = 0; k < it.Funcs.Count; k++)
            {
                MathFn f = GetFn(it.Funcs[k]);
                if (f != null) PlotFunc(g, it, f, Pal[1 + (k % 7)]);
            }
            g.Restore(gs2);

            // axes with arrows
            using (Pen ap = new Pen(ink, Math.Max(1.5f, it.Sw)))
            {
                ap.StartCap = LineCap.Round; ap.EndCap = LineCap.Round; ap.LineJoin = LineJoin.Round;
                g.DrawLine(ap, x1 - 6f, oy, x2 + 12f, oy);
                g.DrawLine(ap, x2 + 2f, oy - 5f, x2 + 12f, oy); g.DrawLine(ap, x2 + 2f, oy + 5f, x2 + 12f, oy);
                g.DrawLine(ap, ox, y2 + 6f, ox, y1 - 12f);
                g.DrawLine(ap, ox - 5f, y1 - 2f, ox, y1 - 12f); g.DrawLine(ap, ox + 5f, y1 - 2f, ox, y1 - 12f);
            }

            // ticks + numbers
            float fs = Clamp(u * 0.36f, 9f, 16f);
            Font nf = GetFont("Segoe UI", fs);
            using (Pen tk = new Pen(ink, 1.3f))
            using (SolidBrush tb = new SolidBrush(ink))
            {
                int j0 = (int)Math.Ceiling(it.XMin / stp - 1e-9), j1 = (int)Math.Floor(it.XMax / stp + 1e-9);
                for (int j = j0; j <= j1; j++)
                {
                    double v = j * stp; if (Math.Abs(v) < 1e-9) continue;
                    float gx = ox + (float)v * u;
                    g.DrawLine(tk, gx, oy - 4f, gx, oy + 4f);
                    if ((it.Flags & 2) != 0) g.DrawString(NumStr(v), nf, tb, new RectangleF(gx - fs * 2f, oy + 6f, fs * 4f, fs * 1.5f), NumFmt);
                }
                int k0 = (int)Math.Ceiling(it.YMin / stp - 1e-9), k1 = (int)Math.Floor(it.YMax / stp + 1e-9);
                for (int k = k0; k <= k1; k++)
                {
                    double v = k * stp; if (Math.Abs(v) < 1e-9) continue;
                    float gy = oy - (float)v * u;
                    g.DrawLine(tk, ox - 4f, gy, ox + 4f, gy);
                    if ((it.Flags & 2) != 0) g.DrawString(NumStr(v), nf, tb, new RectangleF(ox - 8f - fs * 3.4f, gy - fs * 0.75f, fs * 3.4f, fs * 1.5f), NumFmtFar);
                }
                if ((it.Flags & 4) != 0)
                {
                    Font lf = GetFont("Segoe UI", fs * 1.25f);
                    g.DrawString("x", lf, tb, new RectangleF(x2 + 14f, oy - fs * 1.7f, fs * 2f, fs * 1.6f), NumFmt);
                    g.DrawString("y", lf, tb, new RectangleF(ox + 8f, y1 - fs * 2.2f, fs * 2f, fs * 1.6f), NumFmt);
                    g.DrawString("O", nf, tb, new RectangleF(ox - fs * 1.9f, oy + 3f, fs * 1.6f, fs * 1.5f), NumFmt);
                }
            }

            // function legend
            if (it.Funcs.Count > 0)
            {
                Font lg = GetFont("Segoe UI", 12f);
                for (int k = 0; k < it.Funcs.Count; k++)
                    using (SolidBrush lb2 = new SolidBrush(Pal[1 + (k % 7)]))
                        g.DrawString("y = " + it.Funcs[k], lg, lb2, new PointF(x1 + 8f, y1 + 6f + k * 18f), TypoFmt);
            }

            // plotted points with guide lines
            if (it.MPts.Count > 0)
            {
                Font pf = GetFont("Segoe UI", 12f);
                for (int k = 0; k < it.MPts.Count; k++)
                {
                    PointF mp = it.MPts[k];
                    float px = ox + mp.X * u, py = oy - mp.Y * u;
                    using (Pen gd = new Pen(Color.FromArgb(120, 236, 72, 153), 1.2f))
                    {
                        gd.DashStyle = DashStyle.Dash;
                        g.DrawLine(gd, px, py, px, oy); g.DrawLine(gd, px, py, ox, py);
                    }
                    using (SolidBrush pb = new SolidBrush(Color.FromArgb(236, 72, 153))) g.FillEllipse(pb, px - 4.8f, py - 4.8f, 9.6f, 9.6f);
                    using (Pen wp = new Pen(Color.White, 1.4f)) g.DrawEllipse(wp, px - 4.8f, py - 4.8f, 9.6f, 9.6f);
                    using (SolidBrush lb3 = new SolidBrush(ink))
                        g.DrawString("(" + NumStr(mp.X) + ", " + NumStr(mp.Y) + ")", pf, lb3, new PointF(px + 8f, py - 20f), TypoFmt);
                }
            }
        }


    }
}
