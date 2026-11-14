// Board: animation engine (OnTick) and faded layers.
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
        static float Sm(float dt, float speed) { return 1f - (float)Math.Exp(-dt * speed); }

        static bool Approach(ref float v, float target, float a, float eps)
        {
            float d = target - v;
            if (Math.Abs(d) <= eps) { bool ch = v != target; v = target; return ch; }
            v += d * a;
            return true;
        }

        static float Get(Dictionary<string, float> d, string k) { float v; return d.TryGetValue(k, out v) ? v : 0f; }

        static float Ease(float t) { t = Clamp(t, 0f, 1f); float k = 1f - t; return 1f - k * k * k; }

        static Color MixC(Color a, Color b, float t)
        {
            t = Clamp(t, 0f, 1f);
            return Color.FromArgb(255, (int)Math.Round(a.R + (b.R - a.R) * t), (int)Math.Round(a.G + (b.G - a.G) * t), (int)Math.Round(a.B + (b.B - a.B) * t));
        }

        // Everything the side menu can touch while it slides in / out (its panel at any position, shadow included).
        RectangleF ToolsArea()
        {
            if (panels.Count == 0) return RectangleF.Empty;
            RectangleF p = panels[0];
            float ext = p.Width + 60f * ui;
            return new RectangleF(p.X - ext, p.Y - 40f * ui, p.Width + 2f * ext, p.Height + 80f * ui);
        }

        Btn FindBtn(string id)
        {
            foreach (Btn b in AllBtns()) if (b.Id == id) return b;
            return null;
        }

        void OnTick()
        {
            double now = clock.Elapsed.TotalSeconds;
            float dt = (float)(now - lastTick); lastTick = now;
            if (dt > 0.05f) dt = 0.05f;
            if (dt < 0.001f) dt = 0.016f;
            bool busy = false, ovBusy = false, canvasBusy = false;
            // Changes that only touch a small part of the UI (button hover, side-menu slide, tooltips) are collected here and
            // repainted as one small rectangle instead of the whole window.
            bool pAny = false; RectangleF pR = RectangleF.Empty;
            Action<RectangleF> mark = delegate (RectangleF mrc) { if (!pAny) { pR = mrc; pAny = true; } else pR = RectangleF.Union(pR, mrc); };

            // tooltip timing / toast expiry
            DateTime n2 = DateTime.Now;
            bool want = hover != null && mode == Mode.None && !saveMenu && !ctxOpen && (n2 - hoverSince).TotalMilliseconds >= 450;
            if (want != tipVisible)
            {
                tipVisible = want;
                Btn tbm = hover != null ? FindBtn(hover) : tipBtn;
                if (tbm != null) mark(RectangleF.Inflate(TipRect(tbm), 18f * ui, 18f * ui)); else busy = true;
                PerfWhy("tooltip show/hide");
            }
            if (tipVisible && hover != null) { Btn tb2 = FindBtn(hover); if (tb2 != null) tipBtn = tb2; }
            if (toastActive && n2 >= toastUntil) toastActive = false;

            // button hover / press / active amounts
            float shN = Sm(dt, 28f), spN = Sm(dt, 30f), saN = Sm(dt, 14f);
            foreach (Btn b in AllBtns())
            {
                // behind the Help card (and behind About, except its own buttons) the toolbar does not animate: it jumps to its final state once
                bool still = showHelp || (showAbout && !b.Id.StartsWith("ab:"));
                float sh = still ? 1f : shN, sp = still ? 1f : spN, sa = still ? 1f : saN;
                float h0 = Get(hovAmt, b.Id), p0 = Get(pressAmt, b.Id), a0 = Get(actAmt, b.Id);
                bool m1 = Approach(ref h0, (hover == b.Id && b.Enabled) ? 1f : 0f, sh, 0.004f);
                bool m2 = Approach(ref p0, (pressed == b.Id) ? 1f : 0f, sp, 0.004f);
                bool m3 = Approach(ref a0, IsActive(b) ? 1f : 0f, sa, 0.004f);
                hovAmt[b.Id] = h0; pressAmt[b.Id] = p0; actAmt[b.Id] = a0;
                if (m1 || m2 || m3) { mark(RectangleF.Inflate(b.R, 12f * ui, 12f * ui)); if (PerfOn) PerfWhy("button " + b.Id + (m1 ? " hover" : "") + (m2 ? " press" : "") + (m3 ? " active" : "")); }
            }

            // sliding tool indicator
            RectangleF indT = RectangleF.Empty; bool haveInd = false;
            foreach (Btn b in btns) if (b.Id.StartsWith("t:") && IsActive(b)) { indT = new RectangleF(b.R.X - toolsShift, b.R.Y, b.R.Width, b.R.Height); haveInd = true; break; }
            if (haveInd)
            {
                if (!indInit) { indRect = indT; indInit = true; mark(ToolsArea()); PerfWhy("tool indicator init"); }
                else
                {
                    float ai = Sm(dt, 18f);
                    float ix = indRect.X, iy = indRect.Y, iw2 = indRect.Width, ih = indRect.Height;
                    bool k1 = Approach(ref ix, indT.X, ai, 0.3f), k2 = Approach(ref iy, indT.Y, ai, 0.3f);
                    bool k3 = Approach(ref iw2, indT.Width, ai, 0.3f), k4 = Approach(ref ih, indT.Height, ai, 0.3f);
                    indRect = new RectangleF(ix, iy, iw2, ih);
                    if (k1 || k2 || k3 || k4) { mark(ToolsArea()); PerfWhy("tool indicator slide"); }
                }
            }

            // tabs (slide, grow, hover, active)
            int nt = Math.Min(docs.Count, tabRects.Count);
            for (int i = 0; i < nt; i++)
            {
                Doc d = docs[i]; RectangleF t = tabRects[i];
                if (!d.AInit) { d.AX = t.X + t.Width / 2f; d.AW = 0f; d.AInit = true; busy = true; PerfWhy("tab init"); }
                float at = Sm(dt, 14f);
                if (Approach(ref d.AX, t.X, at, 0.3f)) { busy = true; PerfWhy("tab x"); }
                if (Approach(ref d.AW, t.Width, at, 0.3f)) { busy = true; PerfWhy("tab width"); }
                if (Approach(ref d.Hv, i == hoverTab ? 1f : 0f, Sm(dt, 18f), 0.01f)) { busy = true; PerfWhy("tab hover"); }
                if (Approach(ref d.Act, i == curTab ? 1f : 0f, Sm(dt, 14f), 0.01f)) { busy = true; PerfWhy("tab active"); }
            }

            // overlays
            if (Approach(ref menuT, saveMenu ? 1f : 0f, Sm(dt, 16f), 0.01f)) { busy = true; PerfWhy("save/math popup"); }
            for (int i = 0; i < 3; i++) if (Approach(ref menuHov[i], (saveMenu && menuHover == i) ? 1f : 0f, Sm(dt, 20f), 0.01f)) { busy = true; PerfWhy("popup row hover"); }
            { float ht = showHelp ? 1f : 0f; if (helpT != ht) { helpT = ht; ovBusy = true; PerfWhy("help toggle"); } }   // no fade: opens/closes instantly
            if (Approach(ref ctxT, ctxOpen ? 1f : 0f, Sm(dt, 22f), 0.01f)) { busy = true; PerfWhy("context menu fade"); }
            for (int cq = 0; cq < ctxRows.Count; cq++)
            {
                CtxRow cr = ctxRows[cq];
                if (Approach(ref cr.Hv, (ctxOpen && !cr.Sep && cq == ctxHover) ? 1f : 0f, Sm(dt, 24f), 0.01f)) { busy = true; PerfWhy("context row hover"); }
            }
            if (!ctxOpen && ctxT <= 0.01f && ctxRows.Count > 0) ctxRows.Clear();
            if (Approach(ref tipT, tipVisible ? 1f : 0f, Sm(dt, 20f), 0.01f)) { if (tipBtn != null) mark(RectangleF.Inflate(TipRect(tipBtn), 18f * ui, 18f * ui)); else busy = true; PerfWhy("tooltip fade"); }
            if (Approach(ref toastT, toastActive ? 1f : 0f, Sm(dt, 14f), 0.01f)) { busy = true; PerfWhy("toast fade"); }
            if (Approach(ref canvasFade, 0f, Sm(dt, 8f), 0.01f)) { busy = true; canvasBusy = true; PerfWhy("canvas fade"); }
            bool introGo = !splashActive || (splashStarted && (n2 - splashStart).TotalMilliseconds > 1500);
            if (introGo && Approach(ref introT, 1f, Sm(dt, 5f), 0.004f)) { busy = true; canvasBusy = true; PerfWhy("intro"); }

            // hover-reveal tools menu
            bool inZone = toolsZone.Contains(mousePos.X, mousePos.Y) || toolsStrip.Contains(mousePos.X, mousePos.Y);
            if (mode != Mode.None && toolsT < 0.5f) inZone = false;
            if (showHelp || showAbout) inZone = false;   // the side menu does not slide in underneath a modal card
            else if (saveMenu && popKind == "math") inZone = true;   // the Math popup belongs to the side menu: keep it out while the popup is open
            if (inZone) { toolsWant = true; toolsLeave = now; }
            else if (now - toolsLeave > 0.45) toolsWant = false;
            if (Approach(ref toolsT, (toolsPinned || toolsWant) ? 1f : 0f, Sm(dt, 11f), 0.004f)) { mark(ToolsArea()); PerfWhy("side menu slide"); }

            // axes side panel / about card
            Item sa1 = SingleSel();
            bool axOn = sa1 != null && sa1.K == Kind.Axes && tool == Tool.Select && editItem == null;
            if (axOn) axItem = sa1;
            if (Approach(ref axT, axOn ? 1f : 0f, Sm(dt, 14f), 0.01f)) { busy = true; PerfWhy("axes panel fade"); }
            if (axT <= 0.01f && !axOn) axItem = null;
            { float at = showAbout ? 1f : 0f; if (aboutT != at) { aboutT = at; ovBusy = true; PerfWhy("about toggle"); } }   // no fade: opens/closes instantly

            // theme cross-fade
            if (themeSnap != null)
            {
                if (Approach(ref themeSnapT, 0f, Sm(dt, 10f), 0.01f)) { busy = true; PerfWhy("theme fade"); }
                if (themeSnapT <= 0.01f) { ReleaseThemeSnap(); busy = true; }
            }

            // smooth zoom / fit
            if (viewAnim)
            {
                float av = Sm(dt, 14f);
                vx += (tgx - vx) * av; vy += (tgy - vy) * av;
                vz = (float)Math.Exp(Math.Log(vz) + (Math.Log(tgz) - Math.Log(vz)) * av);
                if (Math.Abs(tgx - vx) < 0.3f && Math.Abs(tgy - vy) < 0.3f && Math.Abs(tgz / vz - 1f) < 0.0015f) { vx = tgx; vy = tgy; vz = tgz; viewAnim = false; }
                busy = true; canvasBusy = true; PerfWhy("zoom/pan animation");
            }

            { RectangleF lzr = LaserTick(dt); if (!lzr.IsEmpty) mark(lzr); }

            // anything other than the overlay itself animating means the snapshot under the overlay is stale
            if (busy || pAny || (!showHelp && !showAbout && helpT <= 0.01f && aboutT <= 0.01f)) baseDirty = true;
            if (ovBusy) { busy = true; canvasBusy = true; }
            bool live = false;
            if (PerfOn && (busy || splashActive)) { PerfWhy(splashActive ? "splash active" : "TICK BUSY"); }
            if (busy || splashActive)
            {
                // Only the UI layer changed (menus, hover, tooltip, laser, theme dissolve): the cached canvas is reused.
                if (canvasBusy || splashActive) Invalidate(); else InvalidateUi();
                Update();
            }
            else if (pAny)
            {
                InvalidateUiRect(pR);
                Update();
            }
            else if (showAbout && aboutT > 0.995f)
            {
                // the About icon is animated continuously: repaint only its small rectangle instead of the whole window
                Invalidate(Rectangle.Round(aboutIconRect)); Update(); live = true;
            }
            int wantInt = (busy || pAny || splashActive || live) ? 15 : 60;
            if (ticker.Interval != wantInt) ticker.Interval = wantInt;
        }

        // Draws via an off-screen layer so a whole group can fade / slide / scale together.
        void DrawFaded(Graphics g, RectangleF area, float alpha, float dy, float scale, Action<Graphics> draw)
        {
            DrawFaded(g, area, alpha, dy, scale, draw, null);
        }

        // cacheKey != null: the layer content is only re-rendered when the key (or the layer size) changes.
        void DrawFaded(Graphics g, RectangleF area, float alpha, float dy, float scale, Action<Graphics> draw, string cacheKey)
        {
            area = new RectangleF((float)Math.Floor(area.X), (float)Math.Floor(area.Y), (float)Math.Ceiling(area.Width) + 1f, (float)Math.Ceiling(area.Height) + 1f);
            int w = (int)area.Width, h = (int)area.Height;
            if (w <= 0 || h <= 0 || alpha <= 0.003f) return;
            Bitmap bmp = GetLayer(w, h);
            long lk = ((long)w << 20) | (long)h;
            string prevKey;
            bool reuse = cacheKey != null && layerKeys.TryGetValue(lk, out prevKey) && prevKey == cacheKey;
            {
                if (!reuse)
                {
                    long lr0 = PerfStart();
                    using (Graphics bg = Graphics.FromImage(bmp))
                    {
                        bg.Clear(Color.Transparent);
                        SetQuality(bg);
                        bg.TranslateTransform(-area.X, -area.Y);
                        draw(bg);
                    }
                    if (cacheKey != null) layerKeys[lk] = cacheKey; else layerKeys.Remove(lk);
                    PerfAdd("    fade: render layer", lr0);
                }
                // The layer is multiplied by the fade alpha on the CPU and then drawn WITHOUT ImageAttributes.
                // GDI+'s ColorMatrix path costs ~45 ms for a Help-sized layer; this costs a few ms and gives the same pixels.
                long fc0 = PerfStart();
                Bitmap faded = FadedCopy(bmp, w, h, alpha);
                PerfAdd("    fade: alpha copy", fc0);
                float cx = area.X + area.Width / 2f, cy = area.Y + area.Height / 2f;
                float sw2 = area.Width * scale, sh2 = area.Height * scale;
                Rectangle dst = Rectangle.Round(new RectangleF(cx - sw2 / 2f, cy - sh2 / 2f + dy, sw2, sh2));
                // SetQuality() leaves PixelOffsetMode.HighQuality on the Graphics: with it GDI+ shifts the image by half a pixel and
                // RESAMPLES every pixel even for a 1:1 copy (very slow for a Help-sized layer). Draw with neutral settings instead.
                bool oneToOne = dst.Width == w && dst.Height == h;
                InterpolationMode oldIm = g.InterpolationMode; PixelOffsetMode oldPo = g.PixelOffsetMode; CompositingQuality oldCq = g.CompositingQuality;
                g.PixelOffsetMode = PixelOffsetMode.None;
                g.CompositingQuality = CompositingQuality.HighSpeed;
                g.InterpolationMode = oneToOne ? InterpolationMode.NearestNeighbor : InterpolationMode.Bilinear;
                long bl0 = PerfStart();
                g.DrawImage(faded, dst, 0, 0, w, h, GraphicsUnit.Pixel);
                if (PerfOn) PerfAdd("    fade: blit " + w + "x" + h + (oneToOne ? " 1:1" : " scaled"), bl0);
                g.InterpolationMode = oldIm; g.PixelOffsetMode = oldPo; g.CompositingQuality = oldCq;
            }
        }

        // Scratch buffers for FadedCopy (reused between frames).
        int[] fadeBuf = new int[0];
        Dictionary<long, Bitmap> fadeTmp = new Dictionary<long, Bitmap>();

        // Returns a premultiplied-ARGB copy of src with every channel multiplied by alpha (0..1).
        Bitmap FadedCopy(Bitmap src, int w, int h, float alpha)
        {
            int n = w * h;
            if (fadeBuf.Length < n) fadeBuf = new int[n];
            int[] buf = fadeBuf;
            Rectangle full = new Rectangle(0, 0, w, h);
            BitmapData sd = src.LockBits(full, ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            try { Marshal.Copy(sd.Scan0, buf, 0, n); } finally { src.UnlockBits(sd); }
            uint a = (uint)Math.Max(0, Math.Min(256, (int)Math.Round(Math.Min(1f, alpha) * 256f)));
            if (a < 256u)
            {
                for (int i = 0; i < n; i++)
                {
                    uint p = (uint)buf[i];
                    if (p == 0u) continue;
                    uint rb = p & 0x00FF00FFu, ag = (p >> 8) & 0x00FF00FFu;
                    buf[i] = (int)((((rb * a) >> 8) & 0x00FF00FFu) | ((ag * a) & 0xFF00FF00u));
                }
            }
            long k = ((long)w << 20) | (long)h;
            Bitmap tmp;
            if (!fadeTmp.TryGetValue(k, out tmp))
            {
                if (fadeTmp.Count >= 6) { foreach (Bitmap old in fadeTmp.Values) old.Dispose(); fadeTmp.Clear(); }
                tmp = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
                fadeTmp[k] = tmp;
            }
            BitmapData dd = tmp.LockBits(full, ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
            try { Marshal.Copy(buf, 0, dd.Scan0, n); } finally { tmp.UnlockBits(dd); }
            return tmp;
        }

        // Off-screen layers are cached by size so a fade animation does not allocate a new bitmap every frame.
        Dictionary<long, Bitmap> layers = new Dictionary<long, Bitmap>();
        Dictionary<long, string> layerKeys = new Dictionary<long, string>();

        Bitmap GetLayer(int w, int h)
        {
            long k = ((long)w << 20) | (long)h;
            Bitmap b;
            if (layers.TryGetValue(k, out b)) return b;
            if (layers.Count >= 6)
            {
                foreach (Bitmap old in layers.Values) old.Dispose();
                layers.Clear(); layerKeys.Clear();
            }
            b = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
            layers[k] = b;
            return b;
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); baseDirty = true; Invalidate(); }
        protected override void OnPaintBackground(PaintEventArgs e) { }


    }
}
