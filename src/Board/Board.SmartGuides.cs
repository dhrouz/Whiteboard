// Board: smart guides - alignment lines + magnetic snapping while moving, angle badge + magnetic snapping while rotating.
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
        // ---- state ----
        List<float[]> guides = new List<float[]>();      // world-space segments {x1, y1, x2, y2}
        List<RectangleF> snapRects = new List<RectangleF>();
        float[] snapX, snapY;                            // candidate lines: left / centre / right (top / centre / bottom) of every nearby object, + view centre last
        RectangleF moveBase, viewRect;
        PointF moveStart, moveApplied;
        bool snapReady, rotSnapHit;
        const int MaxGuides = 16;

        // Called once when a drag-move starts: collects the lines of the other (visible) objects so that every mouse move is cheap.
        void BeginSnap(PointF p)
        {
            moveStart = p; moveApplied = PointF.Empty; guides.Clear(); snapReady = false; snapRects.Clear();
            if (!SelUnion(out moveBase)) return;
            float vl = -vx / vz, vt = (tabBar.Bottom - vy) / vz, vw = Width / vz, vh = Math.Max(1f, Height - tabBar.Bottom) / vz;
            viewRect = new RectangleF(vl, vt, vw, vh);
            RectangleF wide = RectangleF.Inflate(viewRect, vw * 0.5f, vh * 0.5f);
            foreach (Item it in items)
            {
                if (sel.Contains(it.Id) || it.Editing) continue;
                RectangleF b = BoundsOf(it);
                if (!RectangleF.Inflate(b, 1f, 1f).IntersectsWith(wide)) continue;
                snapRects.Add(b);
                if (snapRects.Count >= 300) break;
            }
            int n = snapRects.Count;
            snapX = new float[n * 3 + 1]; snapY = new float[n * 3 + 1];
            for (int i = 0; i < n; i++)
            {
                RectangleF r = snapRects[i];
                snapX[i * 3] = r.Left; snapX[i * 3 + 1] = r.Left + r.Width / 2f; snapX[i * 3 + 2] = r.Right;
                snapY[i * 3] = r.Top; snapY[i * 3 + 1] = r.Top + r.Height / 2f; snapY[i * 3 + 2] = r.Bottom;
            }
            snapX[n * 3] = viewRect.Left + viewRect.Width / 2f;     // centre of the visible canvas
            snapY[n * 3] = viewRect.Top + viewRect.Height / 2f;
            snapReady = true;
        }

        static bool BestOffset(float[] mine, float[] cand, float thr, out float off)
        {
            off = 0f; float best = thr + 1f;
            for (int i = 0; i < mine.Length; i++)
                for (int j = 0; j < cand.Length; j++)
                {
                    float d = cand[j] - mine[i];
                    if (Math.Abs(d) < best) { best = Math.Abs(d); off = d; }
                }
            return best <= thr;
        }

        // ox/oy: how far the selection has been dragged from where it started (raw, un-snapped). Adjusted in place.
        void SnapMove(ref float ox, ref float oy)
        {
            guides.Clear();
            if (!snapReady) return;
            float thr = 7f * ui / vz;
            float bw = moveBase.Width, bh = moveBase.Height;
            float[] mx = new float[] { moveBase.X + ox, moveBase.X + ox + bw / 2f, moveBase.X + ox + bw };
            float[] my = new float[] { moveBase.Y + oy, moveBase.Y + oy + bh / 2f, moveBase.Y + oy + bh };
            float offX, offY;
            bool hx = BestOffset(mx, snapX, thr, out offX);
            bool hy = BestOffset(my, snapY, thr, out offY);
            if (hx) ox += offX;
            if (hy) oy += offY;
            float bl = moveBase.X + ox, bt = moveBase.Y + oy;
            float eps = 0.6f / vz;
            int n = snapRects.Count;
            for (int i = 0; i < 3; i++)
            {
                float mxv = bl + bw / 2f * i, myv = bt + bh / 2f * i;
                for (int j = 0; j < snapX.Length && guides.Count < MaxGuides; j++)
                {
                    bool view = j >= n * 3;
                    RectangleF o = view ? viewRect : snapRects[j / 3];
                    if (Math.Abs(snapX[j] - mxv) < eps)
                        guides.Add(new float[] { snapX[j], Math.Min(bt, o.Top), snapX[j], Math.Max(bt + bh, o.Bottom) });
                }
                for (int j = 0; j < snapY.Length && guides.Count < MaxGuides; j++)
                {
                    bool view = j >= n * 3;
                    RectangleF o = view ? viewRect : snapRects[j / 3];
                    if (Math.Abs(snapY[j] - myv) < eps)
                        guides.Add(new float[] { Math.Min(bl, o.Left), snapY[j], Math.Max(bl + bw, o.Right), snapY[j] });
                }
            }
        }

        // Rotation: magnetic stops every 45 degrees (3 degrees of pull). Shift = 15 degree steps, Ctrl = free.
        double SnapRotation(double rotTotalNow, bool shift, bool free)
        {
            double step45 = Math.PI / 4.0, target = rotTotalNow;
            if (shift) target = Math.Round((rotBase + rotTotalNow) / (Math.PI / 12.0)) * (Math.PI / 12.0) - rotBase;
            else if (!free)
            {
                double ab = rotBase + rotTotalNow, nr = Math.Round(ab / step45) * step45;
                if (Math.Abs(ab - nr) < 3.0 * Math.PI / 180.0) target = nr - rotBase;
            }
            double fin = rotBase + target, n45 = Math.Round(fin / step45) * step45;
            rotSnapHit = Math.Abs(fin - n45) < 0.0005;
            return target;
        }

        void ClearGuides() { guides.Clear(); rotSnapHit = false; snapReady = false; snapRects.Clear(); }

        // Drawn inside the world transform (under the selection handles).
        void DrawGuides(Graphics g)
        {
            bool rot = mode == Mode.Rotate && moved && rotSnapHit;
            if (guides.Count == 0 && !rot) return;
            Color pink = Color.FromArgb(236, 72, 153);
            using (Pen gp = new Pen(pink, 1.3f * ui / vz))
            {
                gp.DashPattern = new float[] { 4f, 3f };
                foreach (float[] s in guides) g.DrawLine(gp, s[0], s[1], s[2], s[3]);
                if (rot)
                {
                    PointF hp, rc, ed; RectangleF u;
                    if (GetRotHandle(out hp, out rc, out ed) && SelUnion(out u))
                    {
                        double a = rotBase + rotApplied;
                        float L = (float)Math.Sqrt(u.Width * u.Width + u.Height * u.Height) * 0.5f + 40f * ui / vz;
                        float cx = (float)Math.Cos(a) * L, cy = (float)Math.Sin(a) * L;
                        g.DrawLine(gp, rc.X - cx, rc.Y - cy, rc.X + cx, rc.Y + cy);
                        g.DrawLine(gp, rc.X + cy, rc.Y - cx, rc.X - cy, rc.Y + cx);
                    }
                }
            }
        }

        // Drawn in screen space (after the world transform is restored): the angle pill next to the rotate handle.
        void DrawRotBadge(Graphics g)
        {
            if (mode != Mode.Rotate || !moved) return;
            PointF hp, rc, ed;
            if (!GetRotHandle(out hp, out rc, out ed)) return;
            Item one = SingleSel();
            bool box = one != null && IsBox(one);
            double deg = (rotBase + rotApplied) * 180.0 / Math.PI;
            deg = ((deg % 360.0) + 360.0) % 360.0;
            if (!box && deg > 180.0) deg -= 360.0;
            int shown = (int)Math.Round(deg);
            if (box && shown >= 360) shown = 0;
            string t = shown.ToString(CultureInfo.InvariantCulture) + "\u00B0";
            float u = ui;
            float sx = hp.X * vz + vx, sy = hp.Y * vz + vy, dx = hp.X - rc.X, dy = hp.Y - rc.Y;
            float dl = (float)Math.Sqrt(dx * dx + dy * dy); if (dl < 0.001f) { dx = 0f; dy = -1f; dl = 1f; }
            float bw = TWf(t, uiBold) + 24f * u, bh = 28f * u;
            float bx = sx + dx / dl * 36f * u - bw / 2f, by = sy + dy / dl * 36f * u - bh / 2f;
            bx = Clamp(bx, 4f * u, Width - bw - 4f * u); by = Clamp(by, tabBar.Bottom + 4f * u, Height - bh - 4f * u);
            RectangleF r = new RectangleF(bx, by, bw, bh);
            using (GraphicsPath p = RoundRect(r, bh / 2f))
            using (SolidBrush fb = new SolidBrush(rotSnapHit ? Color.FromArgb(236, 72, 153) : th.Accent)) g.FillPath(fb, p);
            using (SolidBrush wb = new SolidBrush(Color.White)) UiText(g, t, uiBold, wb, r, fmtC);
        }
    }
}
