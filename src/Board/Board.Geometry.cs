// Board: geometry helpers, bounds, hit-testing.
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
        // ---------- helpers ----------
        static string F(float v) { return v.ToString("0.##", CultureInfo.InvariantCulture); }
        static float Pf(string s) { return float.Parse(s, CultureInfo.InvariantCulture); }
        // Hand-drawn strokes: mouse positions are whole pixels, so the raw line has tiny steps. Two passes of a 1-2-1 average
        // (end points fixed) remove them without changing the shape, and the curve drawn through the result is smooth.
        static List<PointF> SmoothPts(List<PointF> src)
        {
            if (src.Count < 4) return src;
            List<PointF> a = new List<PointF>(src);
            for (int pass = 0; pass < 2; pass++)
            {
                List<PointF> b = new List<PointF>(a.Count);
                b.Add(a[0]);
                for (int i = 1; i < a.Count - 1; i++)
                    b.Add(new PointF(a[i - 1].X * 0.25f + a[i].X * 0.5f + a[i + 1].X * 0.25f, a[i - 1].Y * 0.25f + a[i].Y * 0.5f + a[i + 1].Y * 0.25f));
                b.Add(a[a.Count - 1]);
                a = b;
            }
            return a;
        }

        static float Clamp(float v, float a, float b) { return v < a ? a : (v > b ? b : v); }
        static float Dist(PointF a, PointF b) { float dx = a.X - b.X, dy = a.Y - b.Y; return (float)Math.Sqrt(dx * dx + dy * dy); }
        static float SegDist(PointF p, PointF a, PointF b)
        {
            float dx = b.X - a.X, dy = b.Y - a.Y, l = dx * dx + dy * dy;
            float t = l > 0 ? ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / l : 0f;
            t = Clamp(t, 0f, 1f);
            return Dist(p, new PointF(a.X + t * dx, a.Y + t * dy));
        }
        public static GraphicsPath RoundRect(RectangleF r, float rad)
        {
            GraphicsPath p = new GraphicsPath();
            float d = Math.Min(rad * 2f, Math.Min(r.Width, r.Height));
            if (d < 1f) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        string DocName { get { return docName; } }
        PointF ToWorld(Point p) { return new PointF((p.X - vx) / vz, (p.Y - vy) / vz); }
        PointF ToScreen(PointF p) { return new PointF(p.X * vz + vx, p.Y * vz + vy); }
        PointF ViewCenter() { return ToWorld(new Point(Width / 2, Height / 2)); }
        Color Ink(int ci)
        {
            if (ci >= 1000) return Color.FromArgb(255, Color.FromArgb(ci - 1000));
            return ci == 0 ? th.Ink : Pal[ci % Pal.Length];
        }

        static PointF[] ShapePoints(Item it)
        {
            if (it.K == Kind.Triangle)
                return new PointF[] { new PointF(it.X + it.W / 2f, it.Y), new PointF(it.X + it.W, it.Y + it.H), new PointF(it.X, it.Y + it.H) };
            PointF[] u = new PointF[10];
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            for (int i = 0; i < 10; i++)
            {
                double a = -Math.PI / 2.0 + i * Math.PI / 5.0;
                double r = (i % 2 == 0) ? 1.0 : 0.4;
                u[i] = new PointF((float)(r * Math.Cos(a)), (float)(r * Math.Sin(a)));
                if (u[i].X < minX) minX = u[i].X; if (u[i].X > maxX) maxX = u[i].X;
                if (u[i].Y < minY) minY = u[i].Y; if (u[i].Y > maxY) maxY = u[i].Y;
            }
            float dx = Math.Max(maxX - minX, 0.0001f), dy = Math.Max(maxY - minY, 0.0001f);
            PointF[] res = new PointF[10];
            for (int i = 0; i < 10; i++)
                res[i] = new PointF(it.X + (u[i].X - minX) / dx * it.W, it.Y + (u[i].Y - minY) / dy * it.H);
            return res;
        }

        Font GetFont(string fam, float px) { return GetFont(fam, px, FontStyle.Regular); }

        Font GetFont(string fam, float px, FontStyle st)
        {
            string k = fam + "|" + F(px) + "|" + (int)st;
            Font f;
            if (fonts.TryGetValue(k, out f)) return f;
            if (fonts.Count > 150) fonts.Clear();
            f = new Font(fam, Math.Max(1f, px), st, GraphicsUnit.Pixel);
            fonts[k] = f;
            return f;
        }

        // Text style bits (Item.Style) -> FontStyle
        static FontStyle FS(int style)
        {
            FontStyle r = FontStyle.Regular;
            if ((style & 1) != 0) r |= FontStyle.Bold;
            if ((style & 2) != 0) r |= FontStyle.Italic;
            if ((style & 4) != 0) r |= FontStyle.Underline;
            if ((style & 8) != 0) r |= FontStyle.Strikeout;
            return r;
        }

        static float LineH(Font f, float px)
        {
            FontFamily ff = f.FontFamily;
            return px * ff.GetLineSpacing(FontStyle.Regular) / (float)ff.GetEmHeight(FontStyle.Regular);
        }

        SizeF MeasureText(Item it)
        {
            string mk = F(it.Size) + "|" + it.Style + "|" + (it.Text ?? "");
            if (it.TextSzKey == mk) return it.TextSz;
            Font f = GetFont(TextFamily, it.Size, FS(it.Style));
            string[] lines = (it.Text ?? "").Split('\n');
            float w = 0f;
            for (int i = 0; i < lines.Length; i++)
            {
                SizeF s = mg.MeasureString(lines[i].Length == 0 ? " " : lines[i], f, new SizeF(100000f, 100000f), TypoFmt);
                if (s.Width > w) w = s.Width;
            }
            SizeF res = new SizeF(Math.Max(w, it.Size * 0.5f) + (HasArabic(it.Text) ? 12f : 0f), lines.Length * LineH(f, it.Size));
            it.TextSz = res; it.TextSzKey = mk;
            return res;
        }

        static bool IsBox(Item it)
        {
            return it.K == Kind.Rect || it.K == Kind.Ellipse || it.K == Kind.Triangle || it.K == Kind.Star || it.K == Kind.Text || it.K == Kind.Note || it.K == Kind.Image || it.K == Kind.Ruler || it.K == Kind.Protractor;
        }

        static PointF RotPt(PointF p, PointF c, double rad)
        {
            double cs = Math.Cos(rad), sn = Math.Sin(rad), dx = p.X - c.X, dy = p.Y - c.Y;
            return new PointF((float)(c.X + dx * cs - dy * sn), (float)(c.Y + dx * sn + dy * cs));
        }

        PointF LocalCenter(Item it)
        {
            RectangleF b = LocalBounds(it);
            return new PointF(b.X + b.Width / 2f, b.Y + b.Height / 2f);
        }

        RectangleF BoundsOf(Item it)
        {
            RectangleF b = LocalBounds(it);
            if (it.Rot == 0f || !IsBox(it)) return b;
            PointF c = new PointF(b.X + b.Width / 2f, b.Y + b.Height / 2f);
            double r = it.Rot * Math.PI / 180.0;
            PointF[] q = new PointF[] { RotPt(new PointF(b.Left, b.Top), c, r), RotPt(new PointF(b.Right, b.Top), c, r), RotPt(new PointF(b.Right, b.Bottom), c, r), RotPt(new PointF(b.Left, b.Bottom), c, r) };
            float x1 = q[0].X, y1 = q[0].Y, x2 = x1, y2 = y1;
            for (int i = 1; i < 4; i++)
            {
                if (q[i].X < x1) x1 = q[i].X; if (q[i].X > x2) x2 = q[i].X;
                if (q[i].Y < y1) y1 = q[i].Y; if (q[i].Y > y2) y2 = q[i].Y;
            }
            return new RectangleF(x1, y1, x2 - x1, y2 - y1);
        }

        RectangleF LocalBounds(Item it)
        {
            switch (it.K)
            {
                case Kind.Stroke:
                    {
                        float a = float.MaxValue, b = float.MaxValue, c = float.MinValue, d = float.MinValue;
                        foreach (PointF p in it.Pts)
                        {
                            if (p.X < a) a = p.X; if (p.Y < b) b = p.Y;
                            if (p.X > c) c = p.X; if (p.Y > d) d = p.Y;
                        }
                        return new RectangleF(a - it.Sw / 2f, b - it.Sw / 2f, c - a + it.Sw, d - b + it.Sw);
                    }
                case Kind.Line:
                case Kind.Arrow:
                    {
                        float pad = it.Sw / 2f + (it.K == Kind.Arrow ? 6f : 0f);
                        float x = Math.Min(it.X, it.X2), y = Math.Min(it.Y, it.Y2);
                        return new RectangleF(x - pad, y - pad, Math.Abs(it.X2 - it.X) + 2 * pad, Math.Abs(it.Y2 - it.Y) + 2 * pad);
                    }
                case Kind.Axes:
                    {
                        float ax1 = it.Ox + it.XMin * it.Unit, ax2 = it.Ox + it.XMax * it.Unit, ay1 = it.Oy - it.YMax * it.Unit, ay2 = it.Oy - it.YMin * it.Unit;
                        return new RectangleF(ax1 - 28f, ay1 - 28f, ax2 - ax1 + 56f, ay2 - ay1 + 56f);
                    }
                case Kind.Text:
                    {
                        SizeF s = MeasureText(it);
                        return new RectangleF(it.X, it.Y, s.Width, s.Height);
                    }
                default:
                    return new RectangleF(it.X, it.Y, it.W, it.H);
            }
        }

        RectangleF UnionBounds(IEnumerable<Item> list)
        {
            bool first = true; RectangleF u = RectangleF.Empty;
            foreach (Item it in list)
            {
                RectangleF b = BoundsOf(it);
                if (first) { u = b; first = false; } else u = RectangleF.Union(u, b);
            }
            return u;
        }

        bool Hit(Item it, PointF p, float tol, bool eraser)
        {
            if (it.Rot != 0f && IsBox(it)) p = RotPt(p, LocalCenter(it), -it.Rot * Math.PI / 180.0);
            switch (it.K)
            {
                case Kind.Stroke:
                    if (it.Pts.Count == 1) return Dist(p, it.Pts[0]) <= it.Sw / 2f + tol;
                    for (int i = 1; i < it.Pts.Count; i++)
                        if (SegDist(p, it.Pts[i - 1], it.Pts[i]) <= it.Sw / 2f + tol) return true;
                    return false;
                case Kind.Line:
                case Kind.Arrow:
                    return SegDist(p, new PointF(it.X, it.Y), new PointF(it.X2, it.Y2)) <= it.Sw / 2f + tol;
                case Kind.Rect:
                    {
                        RectangleF b = new RectangleF(it.X, it.Y, it.W, it.H);
                        RectangleF outer = RectangleF.Inflate(b, tol, tol);
                        if (!eraser) return outer.Contains(p);
                        RectangleF inner = RectangleF.Inflate(b, -tol, -tol);
                        return outer.Contains(p) && !inner.Contains(p);
                    }
                case Kind.Ellipse:
                    {
                        float cx = it.X + it.W / 2f, cy = it.Y + it.H / 2f, rx = it.W / 2f + tol, ry = it.H / 2f + tol;
                        float o = ((p.X - cx) * (p.X - cx)) / (rx * rx) + ((p.Y - cy) * (p.Y - cy)) / (ry * ry);
                        if (!eraser) return o <= 1f;
                        float irx = it.W / 2f - tol, iry = it.H / 2f - tol;
                        if (irx <= 0 || iry <= 0) return o <= 1f;
                        float inn = ((p.X - cx) * (p.X - cx)) / (irx * irx) + ((p.Y - cy) * (p.Y - cy)) / (iry * iry);
                        return o <= 1f && inn >= 1f;
                    }
                case Kind.Axes:
                    {
                        RectangleF ab = LocalBounds(it);
                        if (!eraser) return RectangleF.Inflate(ab, tol, tol).Contains(p);
                        return (Math.Abs(p.X - it.Ox) <= tol && p.Y >= ab.Top && p.Y <= ab.Bottom) || (Math.Abs(p.Y - it.Oy) <= tol && p.X >= ab.Left && p.X <= ab.Right);
                    }
                case Kind.Triangle:
                case Kind.Star:
                    {
                        if (!eraser) return RectangleF.Inflate(new RectangleF(it.X, it.Y, it.W, it.H), tol, tol).Contains(p);
                        PointF[] pts = ShapePoints(it);
                        for (int k = 0; k < pts.Length; k++)
                            if (SegDist(p, pts[k], pts[(k + 1) % pts.Length]) <= it.Sw / 2f + tol) return true;
                        return false;
                    }
                default:
                    {
                        RectangleF b = RectangleF.Inflate(LocalBounds(it), tol, tol);
                        return b.Contains(p);
                    }
            }
        }

        Item TopHit(PointF p)
        {
            float tol = 6f * ui / vz;
            for (int i = items.Count - 1; i >= 0; i--)
                if (!items[i].Editing && Hit(items[i], p, tol, false)) return items[i];
            return null;
        }

        Item NewItem(Kind k) { Item it = new Item(); it.Id = nextId++; it.K = k; it.Col = col; return it; }

        Item SingleSel()
        {
            if (sel.Count != 1) return null;
            foreach (Item it in items) if (sel.Contains(it.Id)) return it;
            return null;
        }


    }
}
