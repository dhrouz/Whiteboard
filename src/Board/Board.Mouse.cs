// Board: mouse input, resize, rotate, drag-drop.
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
        // ---------- mouse ----------
        // ---------- ruler: lock pen / line to the ruler edge ----------
        bool TryRulerLock(PointF p)
        {
            snapRuler = null;
            float tol = 16f * ui / vz, ext = 30f * ui / vz;
            for (int i = items.Count - 1; i >= 0; i--)
            {
                Item it = items[i];
                if (it.K != Kind.Ruler || it.Editing) continue;
                PointF c = LocalCenter(it);
                PointF q = it.Rot != 0f ? RotPt(p, c, -it.Rot * Math.PI / 180.0) : p;
                if (q.X < it.X - ext || q.X > it.X + it.W + ext) continue;
                if (Math.Abs(q.Y - it.Y) < tol) { snapRuler = it; snapEdge = 0; return true; }
                if (Math.Abs(q.Y - (it.Y + it.H)) < tol) { snapRuler = it; snapEdge = 1; return true; }
            }
            return false;
        }

        PointF RulerProject(PointF p)
        {
            Item it = snapRuler;
            PointF c = LocalCenter(it);
            double rad = it.Rot * Math.PI / 180.0;
            PointF q = it.Rot != 0f ? RotPt(p, c, -rad) : p;
            q = new PointF(q.X, snapEdge == 0 ? it.Y : it.Y + it.H);
            return it.Rot != 0f ? RotPt(q, c, rad) : q;
        }

        string BtnAt(Point p)
        {
            if (showAbout)
            {
                foreach (Btn b in obtns) if (b.R.Contains(p.X, p.Y)) return b.Id;
                return null;
            }
            foreach (Btn b in btns)
            {
                if (!b.Enabled || !b.R.Contains(p.X, p.Y)) continue;
                if (toolsT <= 0.45f && (b.Id.StartsWith("t:") || b.Id == "pin")) continue;
                return b.Id;
            }
            if (axT > 0.5f) foreach (Btn b in abtns) if (b.R.Contains(p.X, p.Y)) return b.Id;
            return null;
        }

        bool OverPanel(Point p)
        {
            foreach (RectangleF r in panels) if (r.Contains(p.X, p.Y)) return true;
            if (saveMenu && menuRect.Contains(p.X, p.Y)) return true;
            if (axT > 0.5f && axPanel.Contains(p.X, p.Y)) return true;
            return tabBar.Contains(p.X, p.Y);
        }

        int HitHandle(Item it, Point sp)
        {
            foreach (KeyValuePair<int, PointF> kv in HandlePts(it))
            {
                PointF s = ToScreen(kv.Value);
                if (Math.Abs(s.X - sp.X) <= 10f * ui && Math.Abs(s.Y - sp.Y) <= 10f * ui) return kv.Key;
            }
            return -1;
        }

        List<KeyValuePair<int, PointF>> HandlePts(Item it)
        {
            List<KeyValuePair<int, PointF>> r = new List<KeyValuePair<int, PointF>>();
            if (it.K == Kind.Line || it.K == Kind.Arrow)
            {
                r.Add(new KeyValuePair<int, PointF>(0, new PointF(it.X, it.Y)));
                r.Add(new KeyValuePair<int, PointF>(1, new PointF(it.X2, it.Y2)));
                return r;
            }
            if (it.K != Kind.Rect && it.K != Kind.Ellipse && it.K != Kind.Triangle && it.K != Kind.Star && it.K != Kind.Note && it.K != Kind.Image && it.K != Kind.Ruler && it.K != Kind.Protractor) return r;
            float l = it.X, t = it.Y, rr = it.X + it.W, b = it.Y + it.H, cx = it.X + it.W / 2f, cy = it.Y + it.H / 2f;
            bool cornersOnly = it.K == Kind.Note || it.K == Kind.Image || it.K == Kind.Protractor;
            PointF[] all = new PointF[] { new PointF(l, t), new PointF(cx, t), new PointF(rr, t), new PointF(rr, cy), new PointF(rr, b), new PointF(cx, b), new PointF(l, b), new PointF(l, cy) };
            for (int i = 0; i < 8; i++)
            {
                if (cornersOnly && i % 2 == 1) continue;
                if (it.K == Kind.Ruler && i != 3 && i != 7) continue;
                r.Add(new KeyValuePair<int, PointF>(i, it.Rot != 0f ? RotPt(all[i], new PointF(cx, cy), it.Rot * Math.PI / 180.0) : all[i]));
            }
            return r;
        }

        Cursor HandleCursor(Item it, int h)
        {
            if (it.K == Kind.Line || it.K == Kind.Arrow) return Cursors.SizeAll;
            switch (h)
            {
                case 0: case 4: return Cursors.SizeNWSE;
                case 2: case 6: return Cursors.SizeNESW;
                case 1: case 5: return Cursors.SizeNS;
                default: return Cursors.SizeWE;
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            bool hadEdit = editItem != null;
            Focus(); FinishEdit();
            BuildUi();
            if (splashActive) splashActive = false;
            if (ctxOpen)
            {
                int cr0 = CtxRowAt(e.Location);
                if (cr0 >= 0 && e.Button == MouseButtons.Left) { string cid = ctxRows[cr0].Id; CloseCtx(); RunCtx(cid); return; }
                bool insideCtx = ctxRect.Contains(e.X, e.Y);
                CloseCtx();
                if (insideCtx || e.Button != MouseButtons.Right) return;
            }
            if (showHelp) { showHelp = false; Invalidate(); return; }
            if (showAbout)
            {
                string ab0 = BtnAt(e.Location);
                if (ab0 != null) { pressed = ab0; return; }
                showAbout = false; Invalidate(); return;
            }
            if (saveMenu)
            {
                int mr = MenuRowAt(e.Location);
                if (mr >= 0) { saveMenu = false; Invalidate(); RunSaveOption(mr); return; }
                string pb0 = BtnAt(e.Location);
                if (pb0 == "save" || pb0 == "math") { pressed = pb0; return; }
                saveMenu = false; Invalidate();
                if (!(BtnAt(e.Location) != null || OverPanel(e.Location))) return;
            }
            if (tabBar.Contains(e.X, e.Y))
            {
                for (int ti = 0; ti < tabRects.Count; ti++)
                {
                    if (!tabRects[ti].Contains(e.X, e.Y)) continue;
                    if (e.Button == MouseButtons.Middle || tabClose[ti].Contains(e.X, e.Y)) CloseTab(ti);
                    else if (e.Button == MouseButtons.Left) SwitchTo(ti);
                    return;
                }
            }
            string b = BtnAt(e.Location);
            if (b != null) { if (e.Button == MouseButtons.Left) pressed = b; return; }
            if (OverPanel(e.Location)) return;
            PointF p = ToWorld(e.Location);
            if (e.Button == MouseButtons.Right) { ShowContextMenu(e.Location, p); return; }

            if (e.Button == MouseButtons.Middle || (e.Button == MouseButtons.Left && (tool == Tool.Hand || spaceDown)))
            {
                viewAnim = false; mode = Mode.Pan; panStart = e.Location; panV = new PointF(vx, vy); Cursor = Cursors.SizeAll; return;
            }
            if (e.Button != MouseButtons.Left) return;
            if (hadEdit && (tool == Tool.Text || tool == Tool.Note)) return; // first click just commits the text

            float sw = Sizes[sizeIdx];
            switch (tool)
            {
                case Tool.Pen:
                case Tool.Highlighter:
                    rulerLocked = TryRulerLock(p); if (rulerLocked) p = RulerProject(p);
                    cur = NewItem(Kind.Stroke);
                    cur.Pts.Add(p);
                    if (tool == Tool.Pen) { cur.Sw = sw; cur.Alpha = 1f; }
                    else { cur.Sw = sw * 3f + 10f; cur.Alpha = 0.38f; if (col == 0) cur.Col = 3; }
                    items.Add(cur); mode = Mode.Draw; break;
                case Tool.Eraser:
                    mode = Mode.Erase; eraseChanged = false; lastW = p; EraseAt(p); break;
                case Tool.Line:
                case Tool.Arrow:
                    rulerLocked = TryRulerLock(p); if (rulerLocked) p = RulerProject(p);
                    cur = NewItem(tool == Tool.Line ? Kind.Line : Kind.Arrow);
                    cur.Sw = sw; cur.X = p.X; cur.Y = p.Y; cur.X2 = p.X; cur.Y2 = p.Y;
                    items.Add(cur); mode = Mode.Draw; break;
                case Tool.Rect:
                case Tool.Ellipse:
                case Tool.Triangle:
                case Tool.Star:
                    cur = NewItem(tool == Tool.Rect ? Kind.Rect : (tool == Tool.Ellipse ? Kind.Ellipse : (tool == Tool.Triangle ? Kind.Triangle : Kind.Star)));
                    cur.Sw = sw; cur.X = p.X; cur.Y = p.Y; anchor = p;
                    items.Add(cur); mode = Mode.Draw; break;
                case Tool.Text:
                    {
                        Item hit = TopHit(p);
                        if (hit != null && (hit.K == Kind.Text || hit.K == Kind.Note)) { StartEdit(hit, false); break; }
                        Item t = NewItem(Kind.Text);
                        t.Size = TextSizes[sizeIdx]; t.X = p.X; t.Y = p.Y - t.Size * 0.6f;
                        items.Add(t); StartEdit(t, true); break;
                    }
                case Tool.Note:
                    {
                        Item n = NewItem(Kind.Note);
                        n.Bg = rnd.Next(NoteBg.Length); n.W = 220f; n.H = 220f; n.X = p.X - 110f; n.Y = p.Y - 110f;
                        items.Add(n); StartEdit(n, true); break;
                    }
                case Tool.Select:
                    {
                        if (sel.Count > 0 && HitRotHandle(e.Location))
                        {
                            PointF rh, rcn, red2;
                            GetRotHandle(out rh, out rcn, out red2);
                            Item s0 = SingleSel();
                            rotCenter = rcn; rotLast = Math.Atan2(p.Y - rcn.Y, p.X - rcn.X);
                            rotTotal = 0.0; rotApplied = 0.0;
                            rotBase = (s0 != null && IsBox(s0)) ? s0.Rot * Math.PI / 180.0 : 0.0;
                            mode = Mode.Rotate; moved = false; break;
                        }
                        Item single = SingleSel();
                        if (single != null)
                        {
                            int h = HitHandle(single, e.Location);
                            if (h >= 0)
                            {
                                handle = h; rsItem = single; rsOrig = new RectangleF(single.X, single.Y, single.W, single.H);
                                mode = (single.K == Kind.Line || single.K == Kind.Arrow) ? Mode.Endpoint : Mode.Resize;
                                moved = false; break;
                            }
                        }
                        Item hit = TopHit(p);
                        bool shift = (ModifierKeys & Keys.Shift) == Keys.Shift;
                        if (hit != null)
                        {
                            clicked = hit;
                            if (shift) { if (sel.Contains(hit.Id)) sel.Remove(hit.Id); else sel.Add(hit.Id); }
                            else if (!sel.Contains(hit.Id)) { sel.Clear(); sel.Add(hit.Id); }
                            mode = Mode.Move; lastW = p; moved = false; BeginSnap(p);
                        }
                        else
                        {
                            if (!shift) sel.Clear();
                            mode = Mode.Marquee; marqA = p; marquee = new RectangleF(p.X, p.Y, 0, 0);
                        }
                        break;
                    }
            }
            Invalidate();
        }

        void EraseAt(PointF p)
        {
            float tol = 10f * ui / vz;
            int n = items.RemoveAll(delegate(Item i) { return !i.Editing && Hit(i, p, tol, true); });
            if (n > 0) { eraseChanged = true; Invalidate(); }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            mousePos = e.Location;
            Wake();
            LaserMove(e.Location);
            PointF p = ToWorld(e.Location);
            bool shift = (ModifierKeys & Keys.Shift) == Keys.Shift;

            if (mode == Mode.None)
            {
                BuildUi();
                if (showHelp || showAbout)
                {
                    // A modal card is open: the toolbar, tabs and side menu underneath must not react to the mouse
                    // (their hover animations used to force a full repaint of the canvas on every frame).
                    string oh = showAbout ? BtnAt(e.Location) : null;
                    if (oh != hover || hoverTab != -1 || hoverClose || menuHover != -1)
                    { hover = oh; hoverSince = DateTime.Now; tipVisible = false; hoverTab = -1; hoverClose = false; menuHover = -1; InvalidateUi(); }
                    Cursor = Cursors.Default;
                    return;
                }
                if (ctxOpen)
                {
                    int cxh = CtxRowAt(e.Location);
                    if (cxh != ctxHover) { ctxHover = cxh; Wake(); }
                    Cursor = cxh >= 0 ? Cursors.Hand : Cursors.Default;
                    return;
                }
                int ht = -1; bool hcl = false;
                for (int ti = 0; ti < tabRects.Count; ti++)
                    if (tabRects[ti].Contains(e.X, e.Y)) { ht = ti; hcl = tabClose[ti].Contains(e.X, e.Y); break; }
                int mh = MenuRowAt(e.Location);
                if (ht != hoverTab || hcl != hoverClose || mh != menuHover) { hoverTab = ht; hoverClose = hcl; menuHover = mh; Wake(); }
                if (mh >= 0) { Cursor = Cursors.Hand; return; }
                string h = BtnAt(e.Location);
                if (h != hover) { hover = h; hoverSince = DateTime.Now; tipVisible = false; Wake(); }
                if (h != null || OverPanel(e.Location)) { Cursor = Cursors.Default; return; }
                if (spaceDown) { Cursor = Cursors.Hand; return; }
                if (tool == Tool.Select)
                {
                    if (sel.Count > 0 && HitRotHandle(e.Location)) { Cursor = Cursors.Hand; return; }
                    Item single = SingleSel();
                    if (single != null)
                    {
                        int hh = HitHandle(single, e.Location);
                        if (hh >= 0) { Cursor = HandleCursor(single, hh); return; }
                    }
                    Cursor = TopHit(p) != null ? Cursors.SizeAll : Cursors.Default;
                }
                else Cursor = CursorFor(tool);
                return;
            }

            switch (mode)
            {
                case Mode.Pan:
                    vx = panV.X + e.X - panStart.X; vy = panV.Y + e.Y - panStart.Y; Invalidate(); break;
                case Mode.Draw:
                    if (cur == null) break;
                    if (rulerLocked && snapRuler != null) p = RulerProject(p);
                    if (cur.K == Kind.Stroke)
                    {
                        // Mouse positions are whole pixels, which makes a hand-drawn line wobble. Each new point is pulled
                        // part-way towards the previous one (a light low-pass filter), and points are sampled more densely.
                        PointF last = cur.Pts[cur.Pts.Count - 1];
                        PointF sp = rulerLocked ? p : new PointF(last.X + (p.X - last.X) * 0.5f, last.Y + (p.Y - last.Y) * 0.5f);
                        if (Dist(sp, last) * vz >= 0.9f) { cur.Pts.Add(sp); cur.Cache = null; }
                    }
                    else if (cur.K == Kind.Line || cur.K == Kind.Arrow)
                    {
                        float x = p.X, y = p.Y;
                        if (shift) SnapAngle(cur.X, cur.Y, ref x, ref y);
                        cur.X2 = x; cur.Y2 = y;
                    }
                    else
                    {
                        float w = p.X - anchor.X, h2 = p.Y - anchor.Y;
                        if (shift) { float m = Math.Max(Math.Abs(w), Math.Abs(h2)); w = (w < 0 ? -1 : 1) * m; h2 = (h2 < 0 ? -1 : 1) * m; }
                        cur.X = Math.Min(anchor.X, anchor.X + w); cur.Y = Math.Min(anchor.Y, anchor.Y + h2);
                        cur.W = Math.Abs(w); cur.H = Math.Abs(h2);
                    }
                    Invalidate(); break;
                case Mode.Erase:
                    {
                        float dd = Dist(lastW, p);
                        int steps = Math.Max(1, (int)(dd / (5f * ui / vz)));
                        for (int si = 1; si <= steps; si++)
                            EraseAt(new PointF(lastW.X + (p.X - lastW.X) * si / steps, lastW.Y + (p.Y - lastW.Y) * si / steps));
                        lastW = p; break;
                    }
                case Mode.Move:
                    {
                        float rdx = p.X - moveStart.X, rdy = p.Y - moveStart.Y;
                        if (!moved && Math.Abs(rdx * vz) + Math.Abs(rdy * vz) < 2f) break;
                        float snx = rdx, sny = rdy;
                        if ((ModifierKeys & Keys.Control) == Keys.Control) guides.Clear(); else SnapMove(ref snx, ref sny);
                        float dx = snx - moveApplied.X, dy = sny - moveApplied.Y;
                        if (dx != 0f || dy != 0f) foreach (Item it in items) if (sel.Contains(it.Id)) it.Move(dx, dy);
                        moveApplied = new PointF(snx, sny);
                        lastW = p; moved = true; Invalidate(); break;
                    }
                case Mode.Marquee:
                    {
                        marquee = new RectangleF(Math.Min(marqA.X, p.X), Math.Min(marqA.Y, p.Y), Math.Abs(p.X - marqA.X), Math.Abs(p.Y - marqA.Y));
                        foreach (Item it in items) if (!it.Editing && BoundsOf(it).IntersectsWith(marquee)) sel.Add(it.Id);
                        Invalidate(); break;
                    }
                case Mode.Rotate:
                    {
                        double ang = Math.Atan2(p.Y - rotCenter.Y, p.X - rotCenter.X);
                        double dd = ang - rotLast;
                        while (dd > Math.PI) dd -= 2.0 * Math.PI;
                        while (dd < -Math.PI) dd += 2.0 * Math.PI;
                        rotTotal += dd; rotLast = ang;
                        double target = SnapRotation(rotTotal, shift, (ModifierKeys & Keys.Control) == Keys.Control);
                        double delta = target - rotApplied;
                        if (Math.Abs(delta) > 1e-6) { RotateSelection(rotCenter, delta); rotApplied = target; moved = true; }
                        Invalidate(); break;
                    }
                case Mode.Resize:
                    DoResize(rsItem, p, shift); moved = true; Invalidate(); break;
                case Mode.Endpoint:
                    {
                        float x = p.X, y = p.Y;
                        if (handle == 0) { if (shift) SnapAngle(rsItem.X2, rsItem.Y2, ref x, ref y); rsItem.X = x; rsItem.Y = y; }
                        else { if (shift) SnapAngle(rsItem.X, rsItem.Y, ref x, ref y); rsItem.X2 = x; rsItem.Y2 = y; }
                        moved = true; Invalidate(); break;
                    }
            }
        }

        static void SnapAngle(float ox, float oy, ref float x, ref float y)
        {
            float dx = x - ox, dy = y - oy;
            double a = Math.Round(Math.Atan2(dy, dx) / (Math.PI / 4)) * (Math.PI / 4);
            double d = Math.Sqrt(dx * dx + dy * dy);
            x = ox + (float)(d * Math.Cos(a)); y = oy + (float)(d * Math.Sin(a));
        }

        void DoResize(Item it, PointF p, bool shift)
        {
            PointF c0 = new PointF(rsOrig.X + rsOrig.Width / 2f, rsOrig.Y + rsOrig.Height / 2f);
            float rotDeg = it.Rot;
            if (rotDeg != 0f) p = RotPt(p, c0, -rotDeg * Math.PI / 180.0);
            float l = rsOrig.Left, t = rsOrig.Top, r = rsOrig.Right, b = rsOrig.Bottom;
            switch (handle)
            {
                case 0: l = p.X; t = p.Y; break;
                case 1: t = p.Y; break;
                case 2: r = p.X; t = p.Y; break;
                case 3: r = p.X; break;
                case 4: r = p.X; b = p.Y; break;
                case 5: b = p.Y; break;
                case 6: l = p.X; b = p.Y; break;
                case 7: l = p.X; break;
            }
            bool keep = (it.K == Kind.Image) || (it.K == Kind.Protractor) || (shift && handle % 2 == 0);
            if (keep && handle % 2 == 0)
            {
                float ar = rsOrig.Height / Math.Max(rsOrig.Width, 1f);
                bool left = (handle == 0 || handle == 6), top = (handle == 0 || handle == 2);
                float ax = left ? rsOrig.Right : rsOrig.Left, ay = top ? rsOrig.Bottom : rsOrig.Top;
                float nw = Math.Max(Math.Abs(p.X - ax), 12f), nh = nw * ar;
                l = left ? ax - nw : ax; r = l + nw;
                t = top ? ay - nh : ay; b = t + nh;
            }
            float minS = it.K == Kind.Note ? 90f : (it.K == Kind.Ruler ? 3f * CM : (it.K == Kind.Protractor ? 90f : 4f));
            it.X = Math.Min(l, r); it.Y = Math.Min(t, b);
            it.W = Math.Max(Math.Abs(r - l), minS); it.H = Math.Max(Math.Abs(b - t), minS);
            if (rotDeg != 0f)
            {
                PointF c1 = new PointF(it.X + it.W / 2f, it.Y + it.H / 2f);
                PointF c2 = RotPt(c1, c0, rotDeg * Math.PI / 180.0);
                it.X = c2.X - it.W / 2f; it.Y = c2.Y - it.H / 2f;
            }
        }

        bool SelUnion(out RectangleF u)
        {
            u = RectangleF.Empty; bool first = true;
            foreach (Item it in items)
            {
                if (!sel.Contains(it.Id) || it.Editing) continue;
                RectangleF b = BoundsOf(it);
                if (first) { u = b; first = false; } else u = RectangleF.Union(u, b);
            }
            return !first;
        }

        bool GetRotHandle(out PointF hp, out PointF center, out PointF edge)
        {
            hp = PointF.Empty; center = PointF.Empty; edge = PointF.Empty;
            if (sel.Count == 0) return false;
            bool anyRot = false;
            foreach (Item ri in items) if (sel.Contains(ri.Id) && ri.K != Kind.Axes) anyRot = true;
            if (!anyRot) return false;
            float pd = 6f * ui / vz, gap = 28f * ui / vz;
            Item s1 = SingleSel();
            if (s1 != null && s1.Editing) return false;
            if (s1 != null && IsBox(s1))
            {
                RectangleF lb = LocalBounds(s1);
                PointF lc = new PointF(lb.X + lb.Width / 2f, lb.Y + lb.Height / 2f);
                double r = s1.Rot * Math.PI / 180.0;
                edge = RotPt(new PointF(lc.X, lb.Top - pd), lc, r);
                hp = RotPt(new PointF(lc.X, lb.Top - pd - gap), lc, r);
                center = lc;
                return true;
            }
            RectangleF u;
            if (!SelUnion(out u)) return false;
            center = new PointF(u.X + u.Width / 2f, u.Y + u.Height / 2f);
            edge = new PointF(center.X, u.Top - pd);
            hp = new PointF(center.X, u.Top - pd - gap);
            return true;
        }

        bool HitRotHandle(Point sp)
        {
            PointF hp, c, ed;
            if (!GetRotHandle(out hp, out c, out ed)) return false;
            PointF s2 = ToScreen(hp);
            return Math.Abs(s2.X - sp.X) <= 14f * ui && Math.Abs(s2.Y - sp.Y) <= 14f * ui;
        }

        void RotateSelection(PointF c, double delta)
        {
            double deg = delta * 180.0 / Math.PI;
            foreach (Item it in items)
            {
                if (!sel.Contains(it.Id) || it.Editing) continue;
                if (it.K == Kind.Stroke)
                {
                    for (int i = 0; i < it.Pts.Count; i++) it.Pts[i] = RotPt(it.Pts[i], c, delta);
                    it.Cache = null;
                }
                else if (it.K == Kind.Line || it.K == Kind.Arrow)
                {
                    PointF a = RotPt(new PointF(it.X, it.Y), c, delta), b = RotPt(new PointF(it.X2, it.Y2), c, delta);
                    it.X = a.X; it.Y = a.Y; it.X2 = b.X; it.Y2 = b.Y;
                }
                else if (IsBox(it))
                {
                    PointF lc = LocalCenter(it);
                    PointF nc = RotPt(lc, c, delta);
                    it.X += nc.X - lc.X; it.Y += nc.Y - lc.Y;
                    it.Rot = (float)((it.Rot + deg) % 360.0);
                }
            }
        }

        void RotateQuick()
        {
            RectangleF u;
            if (!SelUnion(out u)) return;
            RotateSelection(new PointF(u.X + u.Width / 2f, u.Y + u.Height / 2f), Math.PI / 2.0);
            Commit();
        }

        void DrawRotHandle(Graphics g)
        {
            PointF hp, rc, ed;
            if (!GetRotHandle(out hp, out rc, out ed)) return;
            float z = vz, r = 11f * ui / z;
            RectangleF u;
            if (sel.Count > 1 && SelUnion(out u))
            {
                float pd = 6f * ui / z;
                using (Pen up = new Pen(Color.FromArgb(130, th.Accent), 1.2f * ui / z))
                    g.DrawRectangle(up, u.X - pd, u.Y - pd, u.Width + 2 * pd, u.Height + 2 * pd);
            }
            using (Pen lp = new Pen(th.Accent, 1.4f * ui / z)) g.DrawLine(lp, ed, hp);
            using (SolidBrush fb = new SolidBrush(Color.White))
            using (Pen ap = new Pen(th.Accent, 1.8f * ui / z))
            {
                g.FillEllipse(fb, hp.X - r, hp.Y - r, 2 * r, 2 * r);
                g.DrawEllipse(ap, hp.X - r, hp.Y - r, 2 * r, 2 * r);
            }
            float ir = r * 0.5f;
            using (Pen iconPen = new Pen(th.Accent, 1.7f * ui / z))
            {
                iconPen.StartCap = LineCap.Round; iconPen.EndCap = LineCap.Round;
                g.DrawArc(iconPen, hp.X - ir, hp.Y - ir, ir * 2f, ir * 2f, -60f, 270f);
                double ang = (-60.0 + 270.0) * Math.PI / 180.0;
                PointF tip = new PointF(hp.X + ir * (float)Math.Cos(ang), hp.Y + ir * (float)Math.Sin(ang));
                double tx = -Math.Sin(ang), ty = Math.Cos(ang);
                double bx = -tx, by2 = -ty, hl = ir * 0.9;
                PointF h1 = new PointF(tip.X + (float)(hl * (bx * Math.Cos(0.6) - by2 * Math.Sin(0.6))), tip.Y + (float)(hl * (bx * Math.Sin(0.6) + by2 * Math.Cos(0.6))));
                PointF h2 = new PointF(tip.X + (float)(hl * (bx * Math.Cos(-0.6) - by2 * Math.Sin(-0.6))), tip.Y + (float)(hl * (bx * Math.Sin(-0.6) + by2 * Math.Cos(-0.6))));
                g.DrawLine(iconPen, tip, h1); g.DrawLine(iconPen, tip, h2);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (pressed != null)
            {
                string id = pressed; pressed = null;
                BuildUi();
                if (BtnAt(e.Location) == id) DoAction(id);
                return;
            }
            Mode m = mode; mode = Mode.None;
            switch (m)
            {
                case Mode.Pan: Cursor = CursorFor(tool); if (spaceDown) Cursor = Cursors.Hand; break;
                case Mode.Draw:
                    rulerLocked = false;
                    if (cur != null)
                    {
                        if (cur.K == Kind.Stroke && cur.Pts.Count >= 2)
                        {
                            // finish exactly where the pointer was released (the filter above lags slightly behind it)
                            PointF endP = ToWorld(e.Location), lp = cur.Pts[cur.Pts.Count - 1];
                            if (Dist(endP, lp) * vz >= 0.5f) { cur.Pts.Add(endP); }
                            cur.Pts = SmoothPts(cur.Pts); cur.Cache = null;
                        }
                        bool tiny = false;
                        if (cur.K == Kind.Rect || cur.K == Kind.Ellipse || cur.K == Kind.Triangle || cur.K == Kind.Star) tiny = cur.W < 3f && cur.H < 3f;
                        else if (cur.K == Kind.Line || cur.K == Kind.Arrow) tiny = Dist(new PointF(cur.X, cur.Y), new PointF(cur.X2, cur.Y2)) < 3f;
                        if (tiny) items.Remove(cur); else Commit();
                        cur = null;
                    }
                    break;
                case Mode.Erase: if (eraseChanged) { sel.Clear(); Commit(); } break;
                case Mode.Move:
                    if (moved) Commit();
                    else if (clicked != null && sel.Count > 1 && (ModifierKeys & Keys.Shift) != Keys.Shift) { sel.Clear(); sel.Add(clicked.Id); }
                    clicked = null; break;
                case Mode.Resize:
                case Mode.Rotate:
                case Mode.Endpoint: if (moved) Commit(); break;
            }
            ClearGuides();
            Invalidate();
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (e.Button != MouseButtons.Left || (tool != Tool.Select && tool != Tool.Text)) return;
            BuildUi();
            if (BtnAt(e.Location) != null || OverPanel(e.Location)) return;
            Item h = TopHit(ToWorld(e.Location));
            if (h != null && (h.K == Kind.Text || h.K == Kind.Note)) { mode = Mode.None; StartEdit(h, false); }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            FinishEdit();
            CloseCtx();
            baseDirty = true;
            if ((ModifierKeys & Keys.Control) == Keys.Control)
                ZoomAt(e.X, e.Y, (float)Math.Exp(e.Delta * 0.0012));
            else if ((ModifierKeys & Keys.Shift) == Keys.Shift) { viewAnim = false; vx += e.Delta * 0.8f; Invalidate(); }
            else { viewAnim = false; vy += e.Delta * 0.8f; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            mousePos = new Point(-1000, -1000);
            LaserLeave();
            if (hover != null || hoverTab != -1) { hover = null; hoverTab = -1; hoverClose = false; tipVisible = false; InvalidateUi(); }
        }

        protected override void OnDragEnter(DragEventArgs e)
        {
            e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        }

        protected override void OnDragDrop(DragEventArgs e)
        {
            string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files == null) return;
            PointF at = ToWorld(PointToClient(new Point(e.X, e.Y)));
            foreach (string f in files)
            {
                string ext = Path.GetExtension(f).ToLowerInvariant();
                if (ext == ".wboard") { if (!(items.Count == 0 && curPath == null) && docs.Count < 12) NewTabInternal(); LoadFile(f, false); break; }
                if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp" || ext == ".gif") { AddImageFile(f, at); at = new PointF(at.X + 30f, at.Y + 30f); }
            }
        }


    }
}
