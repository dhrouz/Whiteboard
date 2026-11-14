// Board: vector icons (24x24 grid).
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
        // ---------- icons (24x24 design grid) ----------
        static void L(Graphics g, Pen p, float x1, float y1, float x2, float y2) { g.DrawLine(p, x1, y1, x2, y2); }
        static void PL(Graphics g, Pen p, params float[] xy)
        {
            PointF[] pts = new PointF[xy.Length / 2];
            for (int i = 0; i < pts.Length; i++) pts[i] = new PointF(xy[i * 2], xy[i * 2 + 1]);
            g.DrawLines(p, pts);
        }
        static void PG(Graphics g, Pen p, params float[] xy)
        {
            PointF[] pts = new PointF[xy.Length / 2];
            for (int i = 0; i < pts.Length; i++) pts[i] = new PointF(xy[i * 2], xy[i * 2 + 1]);
            g.DrawPolygon(p, pts);
        }

        void DrawIcon(Graphics g, string id, float cx, float cy, Color c)
        {
            float s = 20f * ui / 24f;
            GraphicsState st = g.Save();
            g.TranslateTransform(cx - 12f * s, cy - 12f * s);
            g.ScaleTransform(s, s);
            using (Pen p = new Pen(c, 2f))
            using (SolidBrush br = new SolidBrush(c))
            {
                p.StartCap = LineCap.Round; p.EndCap = LineCap.Round; p.LineJoin = LineJoin.Round;
                switch (id)
                {
                    case "select": PG(g, p, 5, 3, 19, 10, 13, 12, 11, 18); break;
                    case "hand":
                        L(g, p, 12, 3, 12, 21); L(g, p, 3, 12, 21, 12);
                        PL(g, p, 9, 6, 12, 3, 15, 6); PL(g, p, 9, 18, 12, 21, 15, 18);
                        PL(g, p, 6, 9, 3, 12, 6, 15); PL(g, p, 18, 9, 21, 12, 18, 15); break;
                    case "pen": PG(g, p, 4, 20, 5, 15, 16, 4, 20, 8, 9, 19); L(g, p, 13, 7, 17, 11); break;
                    case "highlighter": PG(g, p, 14, 4, 20, 10, 12, 18, 6, 18, 6, 12); L(g, p, 4, 21, 12, 21); break;
                    case "eraser": PG(g, p, 4, 15, 12, 7, 19, 14, 13, 20, 8, 20); L(g, p, 9, 11, 16, 18); break;
                    case "line": L(g, p, 5, 19, 19, 5); break;
                    case "arrow": L(g, p, 5, 19, 19, 5); PL(g, p, 10, 5, 19, 5, 19, 14); break;
                    case "rect": using (GraphicsPath gp = RoundRect(new RectangleF(4, 6, 16, 12), 2.5f)) g.DrawPath(p, gp); break;
                    case "ellipse": g.DrawEllipse(p, 4f, 6f, 16f, 12f); break;
                    case "text": L(g, p, 5, 6, 19, 6); L(g, p, 12, 6, 12, 19); L(g, p, 9, 19, 15, 19); break;
                    case "note": PG(g, p, 5, 4, 19, 4, 19, 15, 14, 20, 5, 20); PL(g, p, 14, 20, 14, 15, 19, 15); break;
                    case "undo": PL(g, p, 9, 5, 4, 10, 9, 15); g.DrawBezier(p, 4f, 10f, 14f, 8f, 21f, 12f, 17f, 19f); break;
                    case "redo": PL(g, p, 15, 5, 20, 10, 15, 15); g.DrawBezier(p, 20f, 10f, 10f, 8f, 3f, 12f, 7f, 19f); break;
                    case "clear":
                        L(g, p, 5, 7, 19, 7); PL(g, p, 9, 7, 9, 4, 15, 4, 15, 7); PL(g, p, 7, 7, 8, 20, 16, 20, 17, 7); break;
                    case "laser":
                        g.FillEllipse(br, 9f, 9f, 6f, 6f);
                        L(g, p, 12, 2.5f, 12, 5.5f); L(g, p, 12, 18.5f, 12, 21.5f); L(g, p, 2.5f, 12, 5.5f, 12); L(g, p, 18.5f, 12, 21.5f, 12);
                        L(g, p, 5.3f, 5.3f, 7.4f, 7.4f); L(g, p, 16.6f, 16.6f, 18.7f, 18.7f); L(g, p, 18.7f, 5.3f, 16.6f, 7.4f); L(g, p, 7.4f, 16.6f, 5.3f, 18.7f);
                        break;
                    case "grid":
                        for (int ix = 0; ix < 3; ix++) for (int iy = 0; iy < 3; iy++) g.FillEllipse(br, 6f + ix * 6f - 1.5f, 6f + iy * 6f - 1.5f, 3f, 3f);
                        break;
                    case "theme": g.DrawEllipse(p, 4f, 4f, 16f, 16f); g.FillPie(br, 4f, 4f, 16f, 16f, -90f, 180f); break;
                    case "ruler": PG(g, p, 2, 8, 22, 8, 22, 16, 2, 16); L(g, p, 6, 8, 6, 12); L(g, p, 10, 8, 10, 11); L(g, p, 14, 8, 14, 12); L(g, p, 18, 8, 18, 11); break;
                    case "protractor": g.DrawArc(p, 3f, 6f, 18f, 18f, 180f, 180f); L(g, p, 3, 15, 21, 15); L(g, p, 12, 15, 12, 11); L(g, p, 7, 15, 8.5f, 12.5f); L(g, p, 17, 15, 15.5f, 12.5f); break;
                    case "axes": L(g, p, 5, 3, 5, 20); L(g, p, 4, 19, 21, 19); PL(g, p, 3, 6, 5, 3, 7, 6); PL(g, p, 18, 17, 21, 19, 18, 21); g.DrawBezier(p, 7f, 17f, 10f, 6f, 15f, 6f, 20f, 11f); break;
                    case "math": PG(g, p, 4, 20, 4, 5, 19, 20); L(g, p, 4, 11, 7, 11); L(g, p, 4, 15, 7, 15); L(g, p, 9, 20, 9, 17); L(g, p, 13, 20, 13, 17); break;
                    case "pin": PG(g, p, 8, 3, 16, 3, 15, 9, 18, 13, 6, 13, 9, 9); L(g, p, 12, 13, 12, 21); break;
                    case "about": g.DrawEllipse(p, 4f, 4f, 16f, 16f); L(g, p, 12, 11, 12, 16.5f); g.FillEllipse(br, 10.9f, 7.2f, 2.3f, 2.3f); break;
                    case "copy": g.DrawRectangle(p, 9f, 9f, 11f, 11f); PL(g, p, 6, 15, 4, 15, 4, 4, 15, 4, 15, 6); break;
                    case "mail": PG(g, p, 3, 6, 21, 6, 21, 18, 3, 18); PL(g, p, 3, 7, 12, 14, 21, 7); break;
                    case "func": L(g, p, 3, 20, 3, 4); L(g, p, 3, 20, 21, 20); g.DrawBezier(p, 5f, 17f, 9f, 3f, 14f, 3f, 20f, 15f); break;
                    case "close": L(g, p, 6, 6, 18, 18); L(g, p, 6, 18, 18, 6); break;
                    case "lang": g.DrawEllipse(p, 4f, 4f, 16f, 16f); g.DrawEllipse(p, 9f, 4f, 6f, 16f); L(g, p, 4, 12, 20, 12); break;
                    case "pdf": PG(g, p, 6, 3, 14, 3, 19, 8, 19, 21, 6, 21); PL(g, p, 14, 3, 14, 8, 19, 8); L(g, p, 9, 13, 16, 13); L(g, p, 9, 16.5f, 16, 16.5f); break;
                    case "triangle": PG(g, p, 12, 4, 21, 19, 3, 19); break;
                    case "star":
                        {
                            PointF[] spt = new PointF[10];
                            for (int q = 0; q < 10; q++)
                            {
                                double ang = -Math.PI / 2.0 + q * Math.PI / 5.0, rad2 = (q % 2 == 0) ? 9.5 : 4.0;
                                spt[q] = new PointF(12f + (float)(rad2 * Math.Cos(ang)), 13f + (float)(rad2 * Math.Sin(ang)));
                            }
                            g.DrawPolygon(p, spt); break;
                        }
                    case "image":
                        using (GraphicsPath ipath = RoundRect(new RectangleF(3, 5, 18, 14), 2.5f)) g.DrawPath(p, ipath);
                        g.FillEllipse(br, 7f, 8.5f, 3f, 3f);
                        PL(g, p, 3, 17, 9, 12, 13, 16, 16, 13, 21, 18);
                        break;
                    case "new":
                        // a blank page with a "+" (clearly different from the "open" folder)
                        PG(g, p, 6, 3, 14, 3, 18, 7, 18, 21, 6, 21); PL(g, p, 14, 3, 14, 7, 18, 7);
                        L(g, p, 12, 11, 12, 17); L(g, p, 9, 14, 15, 14); break;
                    case "open": PG(g, p, 3, 7, 9, 7, 11, 9, 21, 9, 21, 19, 3, 19); break;
                    case "save": PG(g, p, 4, 4, 17, 4, 20, 7, 20, 20, 4, 20); PL(g, p, 8, 4, 8, 9, 15, 9, 15, 4); PL(g, p, 7, 20, 7, 14, 17, 14, 17, 20); break;
                    case "png": PL(g, p, 12, 15, 12, 4); PL(g, p, 8, 8, 12, 4, 16, 8); PL(g, p, 5, 14, 5, 20, 19, 20, 19, 14); break;
                    case "help": g.DrawEllipse(p, 4f, 4f, 16f, 16f); UiText(g, "?", GetFont(UiFamily, 13f), br, new RectangleF(0f, 0f, 24f, 24.5f), fmtC); break;
                    case "fit": PL(g, p, 4, 9, 4, 4, 9, 4); PL(g, p, 15, 4, 20, 4, 20, 9); PL(g, p, 20, 15, 20, 20, 15, 20); PL(g, p, 9, 20, 4, 20, 4, 15); break;
                    case "cut": g.DrawEllipse(p, 3.5f, 14.5f, 6f, 6f); g.DrawEllipse(p, 14.5f, 14.5f, 6f, 6f); L(g, p, 8, 15, 17, 3); L(g, p, 16, 15, 7, 3); break;
                    case "dup": g.DrawRectangle(p, 3f, 3f, 12f, 12f); g.DrawRectangle(p, 9f, 9f, 12f, 12f); break;
                    case "front": L(g, p, 5, 3, 19, 3); L(g, p, 12, 20, 12, 8); PL(g, p, 6.5f, 13, 12, 7.5f, 17.5f, 13); break;
                    case "back": L(g, p, 5, 21, 19, 21); L(g, p, 12, 4, 12, 16); PL(g, p, 6.5f, 11, 12, 16.5f, 17.5f, 11); break;
                    case "rot": g.DrawArc(p, 4f, 4f, 16f, 16f, -60f, 270f); PL(g, p, 3.5f, 3.5f, 5f, 8.5f, 10f, 7f); break;
                    case "paste": g.DrawRectangle(p, 5f, 5f, 14f, 16f); g.DrawRectangle(p, 9f, 3f, 6f, 4f); L(g, p, 8, 12, 16, 12); L(g, p, 8, 16, 14, 16); break;
                    case "selall": p.DashStyle = DashStyle.Dash; g.DrawRectangle(p, 4f, 4f, 16f, 16f); p.DashStyle = DashStyle.Solid; break;
                    case "fb": case "fi": case "fu": case "fs":
                        {
                            FontStyle fst = id == "fb" ? FontStyle.Bold : (id == "fi" ? FontStyle.Italic : (id == "fu" ? FontStyle.Underline : FontStyle.Strikeout));
                            UiText(g, id == "fb" ? "B" : (id == "fi" ? "I" : (id == "fu" ? "U" : "S")), GetFont(UiFamily, 19f, fst | (id == "fb" ? FontStyle.Bold : FontStyle.Regular)), br, new RectangleF(0f, 0f, 24f, 24.5f), fmtC);
                            break;
                        }
                    case "fplus": case "fminus":
                        {
                            UiText(g, "A", GetFont(UiFamily, 17f, FontStyle.Bold), br, new RectangleF(0f, 0f, 17f, 24.5f), fmtC);
                            L(g, p, 16.5f, 12, 22.5f, 12);
                            if (id == "fplus") L(g, p, 19.5f, 9, 19.5f, 15);
                            break;
                        }
                    case "zin": L(g, p, 6, 12, 18, 12); L(g, p, 12, 6, 12, 18); break;
                    case "zout": L(g, p, 6, 12, 18, 12); break;
                }
            }
            g.Restore(st);
        }

    }
}
