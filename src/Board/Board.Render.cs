// Board: paint pipeline, grid, items, selection.
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
        // ---------- rendering ----------
        static void SetQuality(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (!warmed)
            {
                e.Graphics.Clear(th.Bg);
                if (!warmQueued) { warmQueued = true; BeginInvoke(new Action(Warmup)); }
                return;
            }
            long pt0 = PerfStart();
            PaintFrame(e.Graphics);
            PerfAdd("paint:" + PerfState(), pt0);
        }

        // Renders the first frames off-screen once so JIT compilation and font setup never hitch the visible start-up animation.
        void Warmup()
        {
            try
            {
                int w = Math.Max(320, Width), h = Math.Max(320, Height);
                using (Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppPArgb))
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    splashStarted = true;
                    splashStart = DateTime.Now - TimeSpan.FromMilliseconds(1500);
                    PaintFrame(g);
                    splashStart = DateTime.Now - TimeSpan.FromMilliseconds(3250);
                    PaintFrame(g);
                    WarmCards(g);
                }
            }
            catch (Exception) { }
            warmed = true; splashStarted = true; splashStart = DateTime.Now;
            lastTick = clock.Elapsed.TotalSeconds;
            Invalidate();
        }

        // Pre-renders the Help and About cards off-screen: font/glyph caches, Arabic shaping and the cached layers are
        // ready before the first click, so the first open is as smooth as every later one.
        void WarmCards(Graphics g)
        {
            bool oldAbout = showAbout; float oldT = aboutT;
            try
            {
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                RectangleF hr = HelpRect();
                DrawHelpCard(g, hr);
                DrawFaded(g, RectangleF.Inflate(hr, 44f * ui, 44f * ui), 0.01f, 0f, 1f, delegate (Graphics gg) { DrawHelpCard(gg, hr); }, HelpCacheKey());
                showAbout = true; aboutT = 1f; BuildUi();
                DrawAboutCard(g);
                DrawFaded(g, RectangleF.Inflate(aboutCard, 44f * ui, 44f * ui), 0.01f, 0f, 1f, delegate (Graphics gg) { DrawAboutFadeContent(gg); });
            }
            catch (Exception) { }
            showAbout = oldAbout; aboutT = oldT;
            BuildUi();
        }

        bool SplashOpaque()
        {
            if (!splashActive) return false;
            double t = (DateTime.Now - splashStart).TotalMilliseconds;
            return t < 3050.0;
        }

        void PaintFrame(Graphics g)
        {
            if (!splashStarted) { splashStarted = true; splashStart = DateTime.Now; }
            if (needFit && Width > 200) { needFit = false; if (items.Count > 0) FitView(); else ResetView(); }
            if (SplashOpaque()) { BuildUi(); g.Clear(th.Bg); SetQuality(g); DrawSplash(g); return; }
            BuildUi();
            if (!splashActive && (helpT > 0.01f || aboutT > 0.01f))
            {
                // Help / About: the canvas + UI underneath does not change while the overlay fades in, so it is
                // rendered once into a snapshot and only the overlay is drawn on top each frame.
                EnsureBase();
                if (helpT > 0.995f && aboutT <= 0.01f)
                {
                    // Help is fully open and nothing moves: the whole frame is a single cached bitmap.
                    EnsureHelpFrame();
                    g.DrawImage(helpFrame, new Rectangle(0, 0, helpFrame.Width, helpFrame.Height), 0, 0, helpFrame.Width, helpFrame.Height, GraphicsUnit.Pixel);
                    return;
                }
                g.DrawImage(baseBmp, new Rectangle(0, 0, baseBmp.Width, baseBmp.Height), 0, 0, baseBmp.Width, baseBmp.Height, GraphicsUnit.Pixel);
                SetQuality(g);
                DrawOverlays(g);
                return;
            }
            // The canvas (grid, items, selection) is cached as a bitmap and only re-rendered when something on it changed.
            // UI-only animations (menus, hover, tooltips, laser) therefore cost one blit + the small UI on top.
            EnsureCanvas();
            g.DrawImage(canvasBmp, new Rectangle(0, 0, canvasBmp.Width, canvasBmp.Height), 0, 0, canvasBmp.Width, canvasBmp.Height, GraphicsUnit.Pixel);
            SetQuality(g);
            DrawUi(g);
            DrawOverlays(g);
            DrawThemeSnap(g);
            DrawLaser(g);
            if (splashActive) DrawSplash(g);
        }

        // ---- canvas cache ----
        Bitmap canvasBmp; bool canvasDirty = true; bool uiInval;

        // Every Invalidate() marks the cached canvas stale, except the ones made through InvalidateUi().
        protected override void OnInvalidated(InvalidateEventArgs e)
        {
            if (!uiInval) canvasDirty = true;
            if (ticker.Interval != 15) ticker.Interval = 15;     // wake the animation timer: it idles at 60 ms
            base.OnInvalidated(e);
        }

        // Called when the mouse changes what it hovers: makes the animation timer run at full speed right away.
        void Wake() { if (ticker.Interval != 15) ticker.Interval = 15; }

        // Same as InvalidateUi() but only for a rectangle (the paint then clips to it).
        void InvalidateUiRect(RectangleF r)
        {
            if (r.IsEmpty) return;
            Rectangle q = Rectangle.Intersect(Rectangle.Ceiling(RectangleF.Inflate(r, 2f, 2f)), ClientRectangle);
            if (q.Width <= 0 || q.Height <= 0) return;
            uiInval = true;
            try { Invalidate(q); } finally { uiInval = false; }
        }

        // Repaint request for changes that only touch the UI layer on top of the canvas (hover, menus, laser...).
        void InvalidateUi()
        {
            uiInval = true;
            try { Invalidate(); } finally { uiInval = false; }
        }

        void EnsureCanvas()
        {
            int w = Math.Max(1, Width), h = Math.Max(1, Height);
            bool sizeOk = canvasBmp != null && canvasBmp.Width == w && canvasBmp.Height == h;
            if (sizeOk && !canvasDirty) return;
            if (!sizeOk)
            {
                if (canvasBmp != null) canvasBmp.Dispose();
                canvasBmp = new Bitmap(w, h, PixelFormat.Format32bppRgb);
            }
            long ct0 = PerfStart();
            using (Graphics cg = Graphics.FromImage(canvasBmp)) DrawCanvas(cg);
            PerfAdd("  canvas render", ct0);
            canvasDirty = false;
        }

        // ---- theme switch: the old frame dissolves into the new one (one cheap alpha blit per frame) ----
        Bitmap themeSnap; float themeSnapT; int themeSnapVer;

        Bitmap CaptureFrame()
        {
            EnsureCanvas();
            BuildUi();
            Bitmap b = new Bitmap(canvasBmp.Width, canvasBmp.Height, PixelFormat.Format32bppPArgb);
            using (Graphics fg = Graphics.FromImage(b))
            {
                fg.DrawImage(canvasBmp, new Rectangle(0, 0, b.Width, b.Height), 0, 0, b.Width, b.Height, GraphicsUnit.Pixel);
                SetQuality(fg);
                DrawUi(fg);
            }
            return b;
        }

        void StartThemeSwitch()
        {
            Bitmap snap = null;
            try { if (warmed && !splashActive) snap = CaptureFrame(); } catch (Exception) { snap = null; }
            ReleaseThemeSnap();
            themeSnap = snap; themeSnapT = 1f; themeSnapVer++;
            if (snap != null) { try { themeHbm = snap.GetHbitmap(); } catch (Exception) { themeHbm = IntPtr.Zero; } }
            thTarget = thTarget.Dark ? Light : Dark;
            th.Set(thTarget);
            themeAnim = false;
            Invalidate();
        }

        // GDI AlphaBlend: one native constant-alpha blit of the (opaque) old frame, far cheaper than alpha-scaling 2 million pixels in managed code.
        [StructLayout(LayoutKind.Sequential)]
        struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }
        [DllImport("msimg32.dll")]
        static extern bool AlphaBlend(IntPtr hdcDest, int xDest, int yDest, int wDest, int hDest, IntPtr hdcSrc, int xSrc, int ySrc, int wSrc, int hSrc, BLENDFUNCTION bf);
        [DllImport("gdi32.dll")]
        static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll")]
        static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
        [DllImport("gdi32.dll")]
        static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")]
        static extern bool DeleteObject(IntPtr obj);
        IntPtr themeHbm = IntPtr.Zero;

        void ReleaseThemeSnap()
        {
            if (themeHbm != IntPtr.Zero) { try { DeleteObject(themeHbm); } catch (Exception) { } themeHbm = IntPtr.Zero; }
            if (themeSnap != null) { themeSnap.Dispose(); themeSnap = null; }
        }

        bool BlendThemeSnap(Graphics g, int alpha)
        {
            IntPtr hdc = IntPtr.Zero, mdc = IntPtr.Zero, old = IntPtr.Zero;
            bool ok = false;
            try
            {
                hdc = g.GetHdc();
                mdc = CreateCompatibleDC(hdc);
                old = SelectObject(mdc, themeHbm);
                BLENDFUNCTION bf = new BLENDFUNCTION();
                bf.SourceConstantAlpha = (byte)Math.Max(0, Math.Min(255, alpha));
                ok = AlphaBlend(hdc, 0, 0, themeSnap.Width, themeSnap.Height, mdc, 0, 0, themeSnap.Width, themeSnap.Height, bf);
            }
            catch (Exception) { ok = false; }
            finally
            {
                if (mdc != IntPtr.Zero) { if (old != IntPtr.Zero) SelectObject(mdc, old); DeleteDC(mdc); }
                if (hdc != IntPtr.Zero) g.ReleaseHdc(hdc);
            }
            return ok;
        }

        void DrawThemeSnap(Graphics g)
        {
            if (themeSnap == null || themeSnapT <= 0.01f) return;
            if (themeHbm != IntPtr.Zero && BlendThemeSnap(g, (int)(Ease(themeSnapT) * 255f))) return;
            Bitmap sn = themeSnap;     // fallback if GDI blending is not available
            DrawFaded(g, new RectangleF(0, 0, sn.Width, sn.Height), Ease(themeSnapT), 0f, 1f,
                delegate (Graphics gg) { gg.CompositingMode = CompositingMode.SourceCopy; gg.PixelOffsetMode = PixelOffsetMode.None; gg.InterpolationMode = InterpolationMode.NearestNeighbor; gg.DrawImage(sn, 0, 0, sn.Width, sn.Height); },
                "themesnap" + themeSnapVer);
        }

        Bitmap baseBmp; bool baseDirty = true;

        void EnsureBase()
        {
            int w = Math.Max(1, Width), h = Math.Max(1, Height);
            bool sizeOk = baseBmp != null && baseBmp.Width == w && baseBmp.Height == h;
            if (sizeOk && !baseDirty) return;
            if (!sizeOk)
            {
                if (baseBmp != null) baseBmp.Dispose();
                baseBmp = new Bitmap(w, h, PixelFormat.Format32bppRgb);
            }
            long bt0 = PerfStart();
            using (Graphics bg = Graphics.FromImage(baseBmp)) DrawBase(bg);
            PerfAdd("  canvas snapshot", bt0);
            baseDirty = false;
            baseVer++;
        }

        // Finished Help frame (canvas snapshot + dimming + card). Rebuilt only when the snapshot, size, language or theme changes.
        Bitmap helpFrame; string helpFrameKey; int baseVer, helpFrameVer = -1;

        void EnsureHelpFrame()
        {
            int w = baseBmp.Width, h = baseBmp.Height;
            string key = HelpCacheKey();
            bool sizeOk = helpFrame != null && helpFrame.Width == w && helpFrame.Height == h;
            if (sizeOk && helpFrameKey == key && helpFrameVer == baseVer) return;
            if (!sizeOk)
            {
                if (helpFrame != null) helpFrame.Dispose();
                helpFrame = new Bitmap(w, h, PixelFormat.Format32bppRgb);
            }
            using (Graphics fg = Graphics.FromImage(helpFrame))
            {
                fg.DrawImage(baseBmp, new Rectangle(0, 0, w, h), 0, 0, w, h, GraphicsUnit.Pixel);
                SetQuality(fg);
                DrawOverlays(fg);
            }
            helpFrameKey = key; helpFrameVer = baseVer;
        }

        // Everything except the Help / About overlays.
        void DrawBase(Graphics g)
        {
            DrawCanvas(g);
            DrawUi(g);
        }

        // The canvas layer only: background, grid, items, selection, guides.
        void DrawCanvas(Graphics g)
        {
            g.Clear(th.Bg);
            SetQuality(g);
            DrawGrid(g);
            GraphicsState st = g.Save();
            g.TranslateTransform(vx, vy);
            g.ScaleTransform(vz, vz);
            float cm = 80f / vz;
            DrawItems(g, new RectangleF(-vx / vz - cm, -vy / vz - cm, Width / vz + 2f * cm, Height / vz + 2f * cm));
            DrawSelection(g);
            DrawGuides(g);
            g.Restore(st);
            DrawRotBadge(g);
            if (canvasFade > 0.01f)
                using (SolidBrush fbr = new SolidBrush(Color.FromArgb((int)(Math.Min(1f, canvasFade) * 255f), th.Bg)))
                    g.FillRectangle(fbr, 0f, tabBar.Bottom, Width, Math.Max(0f, Height - tabBar.Bottom));
        }


        void DrawGrid(Graphics g)
        {
            if (!grid) return;
            float step = 32f * vz;
            if (step < 9f) return;
            float ox = vx % step; if (ox < 0) ox += step;
            float oy = vy % step; if (oy < 0) oy += step;
            float d = (float)Math.Max(2.0, Math.Round(2.0 * ui));
            SmoothingMode old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.None;
            using (SolidBrush br = new SolidBrush(th.Dot))
                for (float x = ox; x < Width; x += step)
                    for (float y = oy; y < Height; y += step)
                        g.FillRectangle(br, (float)Math.Round(x), (float)Math.Round(y), d, d);
            g.SmoothingMode = old;
        }

        void DrawItems(Graphics g) { DrawItems(g, RectangleF.Empty); }

        // cull: visible area in world units (Empty = draw everything, used by export).
        void DrawItems(Graphics g, RectangleF cull)
        {
            foreach (Item it in items)
            {
                if (cull.Width > 0f && !it.Editing && !BoundsOf(it).IntersectsWith(cull)) continue;
                DrawItem(g, it);
            }
        }

        void DrawItem(Graphics g, Item it)
        {
            if (it.Editing) return;
            if (it.Rot != 0f && IsBox(it))
            {
                GraphicsState gs = g.Save();
                PointF cc = LocalCenter(it);
                g.TranslateTransform(cc.X, cc.Y);
                g.RotateTransform(it.Rot);
                g.TranslateTransform(-cc.X, -cc.Y);
                DrawItemCore(g, it);
                g.Restore(gs);
            }
            else DrawItemCore(g, it);
        }

        void DrawItemCore(Graphics g, Item it)
        {
            if (it.Editing) return;
            Color c = Ink(it.Col);
            switch (it.K)
            {
                case Kind.Stroke:
                    {
                        Color pc = it.Alpha < 1f ? Color.FromArgb((int)(it.Alpha * 255), c) : c;
                        using (Pen pen = new Pen(pc, it.Sw))
                        {
                            pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round; pen.LineJoin = LineJoin.Round;
                            if (it.Pts.Count == 1)
                            {
                                using (SolidBrush br = new SolidBrush(pc)) g.FillEllipse(br, it.Pts[0].X - it.Sw / 2f, it.Pts[0].Y - it.Sw / 2f, it.Sw, it.Sw);
                            }
                            else
                            {
                                if (it.Cache == null || it.Cache.Length != it.Pts.Count) it.Cache = it.Pts.ToArray();
                                if (it.Cache.Length == 2) g.DrawLine(pen, it.Cache[0], it.Cache[1]);
                                else g.DrawCurve(pen, it.Cache, 0.5f);
                            }
                        }
                        break;
                    }
                case Kind.Line:
                case Kind.Arrow:
                    {
                        using (Pen pen = new Pen(c, it.Sw))
                        {
                            pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round; pen.LineJoin = LineJoin.Round;
                            g.DrawLine(pen, it.X, it.Y, it.X2, it.Y2);
                            if (it.K == Kind.Arrow && (Math.Abs(it.X2 - it.X) + Math.Abs(it.Y2 - it.Y)) > 1f)
                            {
                                double a = Math.Atan2(it.Y2 - it.Y, it.X2 - it.X);
                                float hl = 10f + it.Sw * 2.5f;
                                PointF tip = new PointF(it.X2, it.Y2);
                                PointF p1 = new PointF(tip.X - hl * (float)Math.Cos(a - 0.45), tip.Y - hl * (float)Math.Sin(a - 0.45));
                                PointF p2 = new PointF(tip.X - hl * (float)Math.Cos(a + 0.45), tip.Y - hl * (float)Math.Sin(a + 0.45));
                                g.DrawLines(pen, new PointF[] { p1, tip, p2 });
                            }
                        }
                        break;
                    }
                case Kind.Rect:
                    {
                        if (it.W < 0.5f || it.H < 0.5f) break;
                        using (Pen pen = new Pen(c, it.Sw))
                        using (GraphicsPath gp = RoundRect(new RectangleF(it.X, it.Y, it.W, it.H), 8f))
                        {
                            pen.LineJoin = LineJoin.Round;
                            g.DrawPath(pen, gp);
                        }
                        break;
                    }
                case Kind.Ellipse:
                    {
                        if (it.W < 0.5f || it.H < 0.5f) break;
                        using (Pen pen = new Pen(c, it.Sw)) g.DrawEllipse(pen, it.X, it.Y, it.W, it.H);
                        break;
                    }
                case Kind.Ruler: DrawRuler(g, it); break;
                case Kind.Protractor: DrawProtractor(g, it); break;
                case Kind.Axes: DrawAxes(g, it); break;
                case Kind.Triangle:
                case Kind.Star:
                    {
                        if (it.W < 0.5f || it.H < 0.5f) break;
                        using (Pen pen = new Pen(c, it.Sw))
                        {
                            pen.LineJoin = LineJoin.Round;
                            g.DrawPolygon(pen, ShapePoints(it));
                        }
                        break;
                    }
                case Kind.Text:
                    {
                        Font f = GetFont(TextFamily, it.Size, FS(it.Style));
                        float lh = LineH(f, it.Size);
                        string[] lines = (it.Text ?? "").Split('\n');
                        bool rtl = HasArabic(it.Text);
                        float tww = rtl ? MeasureText(it).Width : 0f;
                        using (SolidBrush br = new SolidBrush(c))
                            for (int i = 0; i < lines.Length; i++)
                            {
                                if (rtl) g.DrawString(lines[i], f, br, new RectangleF(it.X, it.Y + i * lh, tww, lh * 1.5f), TypoRtl);
                                else g.DrawString(lines[i], f, br, it.X, it.Y + i * lh, TypoFmt);
                            }
                        break;
                    }
                case Kind.Note:
                    {
                        RectangleF r = new RectangleF(it.X, it.Y, it.W, it.H);
                        for (int i = 4; i >= 1; i--)
                        {
                            RectangleF sr = RectangleF.Inflate(r, i * 1.5f, i * 1.5f); sr.Offset(0, 4f + i);
                            using (GraphicsPath sp = RoundRect(sr, 8f))
                            using (SolidBrush sb = new SolidBrush(Color.FromArgb(14, 0, 0, 0))) g.FillPath(sb, sp);
                        }
                        using (GraphicsPath gp = RoundRect(r, 8f))
                        using (SolidBrush br = new SolidBrush(NoteBg[it.Bg % NoteBg.Length])) g.FillPath(br, gp);
                        Font f = GetFont(NoteFamily, 18f);
                        using (SolidBrush tb = new SolidBrush(Color.FromArgb(39, 39, 42)))
                        using (StringFormat sf = new StringFormat())
                        {
                            sf.Trimming = StringTrimming.None;
                            if (HasArabic(it.Text) || string.IsNullOrEmpty(it.Text)) sf.FormatFlags = StringFormatFlags.DirectionRightToLeft;
                            g.DrawString(it.Text ?? "", f, tb, new RectangleF(it.X + 14f, it.Y + 14f, it.W - 28f, it.H - 28f), sf);
                        }
                        break;
                    }
                case Kind.Image:
                    {
                        Bitmap bmp;
                        if (images.TryGetValue(it.ImgId, out bmp)) g.DrawImage(bmp, new RectangleF(it.X, it.Y, it.W, it.H));
                        break;
                    }
            }
        }

        void DrawSelection(Graphics g)
        {
            float z = vz;
            if (sel.Count > 0)
            {
                using (Pen pen = new Pen(th.Accent, 1.5f * ui / z))
                {
                    pen.DashStyle = DashStyle.Dash;
                    bool single = sel.Count == 1;
                    foreach (Item it in items)
                    {
                        if (!sel.Contains(it.Id) || it.Editing) continue;
                        if (single && (it.K == Kind.Line || it.K == Kind.Arrow)) continue;
                        float pd = 6f * ui / z;
                        if (it.Rot != 0f && IsBox(it))
                        {
                            RectangleF lb = LocalBounds(it); PointF lc = LocalCenter(it); double rr = it.Rot * Math.PI / 180.0;
                            g.DrawPolygon(pen, new PointF[] {
                                RotPt(new PointF(lb.Left - pd, lb.Top - pd), lc, rr), RotPt(new PointF(lb.Right + pd, lb.Top - pd), lc, rr),
                                RotPt(new PointF(lb.Right + pd, lb.Bottom + pd), lc, rr), RotPt(new PointF(lb.Left - pd, lb.Bottom + pd), lc, rr) });
                        }
                        else
                        {
                            RectangleF b = BoundsOf(it);
                            g.DrawRectangle(pen, b.X - pd, b.Y - pd, b.Width + 2 * pd, b.Height + 2 * pd);
                        }
                    }
                }
                Item s1 = SingleSel();
                if (s1 != null && !s1.Editing)
                {
                    float r = 5f * ui / z;
                    using (SolidBrush hb = new SolidBrush(Color.White))
                    using (Pen hp = new Pen(th.Accent, 1.8f * ui / z))
                        foreach (KeyValuePair<int, PointF> kv in HandlePts(s1))
                        {
                            g.FillEllipse(hb, kv.Value.X - r, kv.Value.Y - r, 2 * r, 2 * r);
                            g.DrawEllipse(hp, kv.Value.X - r, kv.Value.Y - r, 2 * r, 2 * r);
                        }
                }
                DrawRotHandle(g);
            }
            if (mode == Mode.Marquee)
            {
                using (SolidBrush mb = new SolidBrush(Color.FromArgb(24, th.Accent)))
                using (Pen mp = new Pen(th.Accent, 1.2f * ui / z))
                {
                    g.FillRectangle(mb, marquee.X, marquee.Y, marquee.Width, marquee.Height);
                    g.DrawRectangle(mp, marquee.X, marquee.Y, marquee.Width, marquee.Height);
                }
            }
        }


    }
}
