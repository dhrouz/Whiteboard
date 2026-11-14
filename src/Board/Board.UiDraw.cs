// Board: panels, tabs, save/math popup.
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
        // ---------- UI drawing ----------
        // Soft shadows are rendered once per (size, radius, theme) into a small bitmap and then blitted;
        // they used to be 8 anti-aliased path fills per panel on every frame.
        Dictionary<string, Bitmap> shadowCache = new Dictionary<string, Bitmap>();

        void DrawShadow(Graphics g, RectangleF r, float rad)
        {
            int rw = Math.Max(1, (int)Math.Round(r.Width)), rh = Math.Max(1, (int)Math.Round(r.Height));
            int padB = (int)Math.Ceiling(10f * ui + 3.5f * ui) + 2;
            string key = rw + "x" + rh + "|" + (int)Math.Round(rad) + "|" + (th.Dark ? "d" : "l") + "|" + (int)Math.Round(ui * 100f);
            Bitmap bmp;
            if (!shadowCache.TryGetValue(key, out bmp))
            {
                if (shadowCache.Count >= 48)
                {
                    foreach (Bitmap old in shadowCache.Values) old.Dispose();
                    shadowCache.Clear();
                }
                bmp = new Bitmap(rw + 2 * padB, rh + 2 * padB, PixelFormat.Format32bppPArgb);
                using (Graphics sg = Graphics.FromImage(bmp))
                {
                    sg.SmoothingMode = SmoothingMode.AntiAlias;
                    RectangleF baseR = new RectangleF(padB, padB, rw, rh);
                    for (int i = 8; i >= 1; i--)
                    {
                        float k = i * 1.25f * ui;
                        RectangleF sr = RectangleF.Inflate(baseR, k, k); sr.Offset(0, 3.5f * ui);
                        using (GraphicsPath p = RoundRect(sr, rad + k))
                        using (SolidBrush b = new SolidBrush(Color.FromArgb(th.Dark ? 13 : 6, 0, 0, 0))) sg.FillPath(b, p);
                    }
                }
                shadowCache[key] = bmp;
            }
            // PixelOffsetMode.HighQuality (set by SetQuality) forces a slow resampling path on every DrawImage, even 1:1.
            InterpolationMode oldIm = g.InterpolationMode; PixelOffsetMode oldPo = g.PixelOffsetMode; CompositingQuality oldCq = g.CompositingQuality;
            g.InterpolationMode = InterpolationMode.NearestNeighbor; g.PixelOffsetMode = PixelOffsetMode.None; g.CompositingQuality = CompositingQuality.HighSpeed;
            g.DrawImage(bmp, new Rectangle((int)Math.Round(r.X) - padB, (int)Math.Round(r.Y) - padB, bmp.Width, bmp.Height), 0, 0, bmp.Width, bmp.Height, GraphicsUnit.Pixel);
            g.InterpolationMode = oldIm; g.PixelOffsetMode = oldPo; g.CompositingQuality = oldCq;
        }

        void DrawPanel(Graphics g, RectangleF r)
        {
            float rad = 14f * ui;
            DrawShadow(g, r, rad);
            using (GraphicsPath p = RoundRect(r, rad))
            using (SolidBrush b = new SolidBrush(th.Panel))
            using (Pen bp = new Pen(th.Border, 1f))
            { g.FillPath(b, p); g.DrawPath(bp, p); }
        }

        void DrawUi(Graphics g)
        {
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            DrawTabs(g);
            foreach (RectangleF p in panels) DrawPanel(g, p);

            using (SolidBrush sb = new SolidBrush(th.Border)) foreach (RectangleF s in seps) g.FillRectangle(sb, s);
            using (SolidBrush mb = new SolidBrush(th.Muted))
                foreach (KeyValuePair<string, RectangleF> kv in lbls) UiText(g, kv.Key, uiFont, mb, kv.Value, fmtRN);

            if (indInit)
                using (GraphicsPath ip = RoundRect(new RectangleF(indRect.X + toolsShift, indRect.Y, indRect.Width, indRect.Height), 10f * ui))
                using (SolidBrush ib = new SolidBrush(th.AccentSoft)) g.FillPath(ib, ip);

            foreach (Btn b in btns) DrawBtn(g, b);
            DrawToolsGrip(g);
            DrawAxesPanel(g);

            if (menuT > 0.01f && menuRect.Width > 1f)
            {
                float me = Ease(menuT);
                if (menuT > 0.995f) DrawSaveMenu(g);
                else
                {
                    RectangleF area = RectangleF.Inflate(RectangleF.Union(menuRect, menuCaret), 44f * ui, 44f * ui);
                    DrawFaded(g, area, me, (me - 1f) * 12f * ui, 0.96f + 0.04f * me, delegate (Graphics gg) { DrawSaveMenu(gg); });
                }
            }

            DrawCtx(g);

            if (tipT > 0.01f && tipBtn != null && !saveMenu)
            {
                float te = Ease(tipT);
                Btn tbn = tipBtn;
                if (tipT > 0.995f) DrawTip(g, tbn);
                else DrawFaded(g, RectangleF.Inflate(TipRect(tbn), 14f * ui, 14f * ui), te, (te - 1f) * 5f * ui, 1f, delegate (Graphics gg) { DrawTip(gg, tbn); });
            }

            if (toastT > 0.01f && toastText != null)
            {
                float ee = Ease(toastT);
                float tw = TW(toastText);
                RectangleF tr0 = new RectangleF((Width - tw) / 2f - 22f * ui, 108f * ui, tw + 44f * ui, 40f * ui);
                string tt0 = toastText;
                Action<Graphics> drawToast = delegate (Graphics gg)
                {
                    using (GraphicsPath tp = RoundRect(tr0, 20f * ui))
                    using (SolidBrush bb = new SolidBrush(th.Ink))
                    using (SolidBrush tb = new SolidBrush(th.Bg))
                    { gg.FillPath(bb, tp); UiText(gg, tt0, uiFont, tb, tr0, fmtRC); }
                };
                if (toastT > 0.995f) drawToast(g);
                else DrawFaded(g, RectangleF.Inflate(tr0, 16f * ui, 16f * ui), ee, (ee - 1f) * 14f * ui, 0.96f + 0.04f * ee, drawToast);
            }
        }

        // Help and About are drawn last, on top of everything else.
        void DrawOverlays(Graphics g)
        {
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            if (helpT > 0.01f)
            {
                float he = Ease(helpT);
                using (SolidBrush dim = new SolidBrush(Color.FromArgb((int)(90f * he), 0, 0, 0))) g.FillRectangle(dim, 0, 0, Width, Height);
                RectangleF hr = HelpRect();
                long ht0 = PerfStart();
                if (helpT > 0.995f) DrawHelpCard(g, hr);
                else DrawFaded(g, RectangleF.Inflate(hr, 44f * ui, 44f * ui), he, (1f - he) * 10f * ui, 1f, delegate (Graphics gg) { DrawHelpCard(gg, hr); }, HelpCacheKey());
                PerfAdd("  help card", ht0);
            }

            if (aboutT > 0.01f)
            {
                float ae = Ease(aboutT);
                using (SolidBrush dim = new SolidBrush(Color.FromArgb((int)(120f * ae), 0, 0, 0))) g.FillRectangle(dim, 0, 0, Width, Height);
                long at0 = PerfStart();
                if (aboutT > 0.995f) DrawAboutCard(g);
                else DrawFaded(g, RectangleF.Inflate(aboutCard, 44f * ui, 44f * ui), ae, (1f - ae) * 12f * ui, 0.98f + 0.02f * ae, delegate (Graphics gg) { DrawAboutFadeContent(gg); });
                PerfAdd("  about card", at0);
            }
        }

        static GraphicsPath TopRound(RectangleF r, float rad)
        {
            GraphicsPath p = new GraphicsPath();
            float d = rad * 2f;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddLine(r.Right, r.Y + rad, r.Right, r.Bottom);
            p.AddLine(r.Right, r.Bottom, r.X, r.Bottom);
            p.CloseFigure();
            return p;
        }

        void DrawTabs(Graphics g)
        {
            float u = ui;
            using (SolidBrush bar = new SolidBrush(th.TabBar)) g.FillRectangle(bar, tabBar);
            using (Pen bl = new Pen(th.Border, 1f)) g.DrawLine(bl, 0f, tabBar.Bottom - 0.5f, Width, tabBar.Bottom - 0.5f);
            int nt = Math.Min(docs.Count, tabRects.Count);
            for (int i = 0; i < nt; i++)
            {
                Doc dc = docs[i];
                RectangleF tgt = tabRects[i];
                RectangleF r = dc.AInit ? new RectangleF(dc.AX, tgt.Y, Math.Max(1f, dc.AW), tgt.Height) : tgt;
                bool hv = i == hoverTab;
                float act = dc.Act, hva = dc.Hv;
                GraphicsState gs = g.Save();
                g.SetClip(r);
                if (hva > 0.01f && act < 0.99f)
                    using (GraphicsPath tp = TopRound(new RectangleF(r.X + 2f * u, r.Y + 5f * u, Math.Max(1f, r.Width - 4f * u), r.Height - 5f * u), 9f * u))
                    using (SolidBrush hb = new SolidBrush(Color.FromArgb((int)(150f * hva * (1f - act)), th.Hover))) g.FillPath(hb, tp);
                if (act > 0.01f)
                {
                    int aa = (int)(255f * act);
                    using (GraphicsPath tp = TopRound(new RectangleF(r.X, r.Y, r.Width, r.Height + 1f), 10f * u))
                    using (SolidBrush fb = new SolidBrush(Color.FromArgb(aa, th.Bg))) g.FillPath(fb, tp);
                    using (SolidBrush ab = new SolidBrush(Color.FromArgb(aa, th.Accent))) g.FillRectangle(ab, r.X + 18f * u, r.Y + 1f * u, Math.Max(1f, r.Width - 36f * u), 2.5f * u);
                    using (SolidBrush sm = new SolidBrush(Color.FromArgb(aa, th.Bg))) g.FillRectangle(sm, r.X + 1f, tabBar.Bottom - 1.5f, Math.Max(1f, r.Width - 2f), 2.5f);
                }
                RectangleF tr = En
                    ? new RectangleF(r.X + 14f * u, r.Y, Math.Max(1f, r.Width - 54f * u), r.Height - 2f * u)
                    : new RectangleF(r.X + 40f * u, r.Y, Math.Max(1f, r.Width - 54f * u), r.Height - 2f * u);
                using (SolidBrush tb = new SolidBrush(MixC(th.Muted, th.Ink, act))) UiText(g, TabName(i), act > 0.5f ? uiBold : uiFont, tb, tr, fmtTab);
                float ca = Math.Max(act, hva);
                if (ca > 0.02f)
                {
                    RectangleF cr = En
                        ? new RectangleF(r.Right - 8f * u - 22f * u, r.Y + (r.Height - 22f * u) / 2f, 22f * u, 22f * u)
                        : new RectangleF(r.X + 8f * u, r.Y + (r.Height - 22f * u) / 2f, 22f * u, 22f * u);
                    float cxm = cr.X + cr.Width / 2f, cym = cr.Y + cr.Height / 2f, d4 = 4.2f * u;
                    bool hc = hv && hoverClose;
                    if (hc) using (SolidBrush cb = new SolidBrush(th.Border)) g.FillEllipse(cb, cr.X, cr.Y, cr.Width, cr.Height);
                    using (Pen xp = new Pen(Color.FromArgb((int)(255f * ca), hc ? th.Ink : th.Muted), 1.7f * u))
                    {
                        xp.StartCap = LineCap.Round; xp.EndCap = LineCap.Round;
                        g.DrawLine(xp, cxm - d4, cym - d4, cxm + d4, cym + d4);
                        g.DrawLine(xp, cxm - d4, cym + d4, cxm + d4, cym - d4);
                    }
                }
                g.Restore(gs);
            }
        }

        // Titles and subtitles of the Save / Math popup (shared by layout and drawing so the width always fits the text).
        void PopupTexts(out string[] tt, out string[] sub)
        {
            if (popKind == "math")
            {
                tt = new string[] { Loc.Tr("مسطرة"), Loc.Tr("منقلة"), Loc.Tr("محور إحداثي") };
                sub = new string[] { Loc.Tr("مسطرة بالسنتيمتر — ارسم على حافتها خطوطاً مستقيمة"), Loc.Tr("لقياس الزوايا من 0° إلى 180°"), Loc.Tr("محاور x و y مع شبكة ولوحة تحكم جانبية") };
            }
            else
            {
                tt = new string[] { Loc.Tr("ملف السبورة"), Loc.Tr("صورة"), Loc.Tr("ملف PDF") };
                sub = new string[] { Loc.Tr("الصيغة الأصلية — لتعديلها لاحقاً داخل البرنامج"), Loc.Tr("بصيغة PNG أو JPG وبجودة عالية"), Loc.Tr("مستند جاهز للمشاركة والطباعة") };
            }
        }

        void DrawSaveMenu(Graphics g)
        {
            float u = ui;
            DrawShadow(g, menuRect, 18f * u);
            using (GraphicsPath p = RoundRect(menuRect, 18f * u))
            using (SolidBrush fb = new SolidBrush(th.Panel))
            using (Pen bp = new Pen(th.Border, 1f)) { g.FillPath(fb, p); g.DrawPath(bp, p); }
            // caret pointing at the button that opened the popup
            PointF c1, c2, c3;
            RectangleF seamR;
            if (popSide)
            {
                if (!En)
                {
                    c1 = new PointF(menuCaret.X, menuCaret.Y); c2 = new PointF(menuCaret.Right, menuCaret.Y + menuCaret.Height / 2f); c3 = new PointF(menuCaret.X, menuCaret.Bottom);
                    seamR = new RectangleF(menuCaret.X - 0.5f, menuCaret.Y + 1.5f, 2f, menuCaret.Height - 3f);
                }
                else
                {
                    c1 = new PointF(menuCaret.Right, menuCaret.Y); c2 = new PointF(menuCaret.X, menuCaret.Y + menuCaret.Height / 2f); c3 = new PointF(menuCaret.Right, menuCaret.Bottom);
                    seamR = new RectangleF(menuCaret.Right - 1.5f, menuCaret.Y + 1.5f, 2f, menuCaret.Height - 3f);
                }
            }
            else
            {
                c1 = new PointF(menuCaret.X, menuCaret.Bottom); c2 = new PointF(menuCaret.X + menuCaret.Width / 2f, menuCaret.Y); c3 = new PointF(menuCaret.Right, menuCaret.Bottom);
                seamR = new RectangleF(menuCaret.X + 1.5f, menuRect.Y - 0.5f, menuCaret.Width - 3f, 2f);
            }
            using (SolidBrush cb = new SolidBrush(th.Panel)) g.FillPolygon(cb, new PointF[] { c1, c2, c3 });
            using (Pen cp = new Pen(th.Border, 1f)) { g.DrawLine(cp, c1, c2); g.DrawLine(cp, c2, c3); }
            using (SolidBrush seam = new SolidBrush(th.Panel)) g.FillRectangle(seam, seamR);

            using (SolidBrush ink = new SolidBrush(th.Ink))
                UiText(g, popKind == "math" ? Loc.Tr("أدوات الرياضيات") : Loc.Tr("احفظ لوحتك كـ"), uiBold, ink, new RectangleF(menuRect.X + 20f * u, menuRect.Y + 6f * u, menuRect.Width - 40f * u, 32f * u), fmtRN);

            string[] tt, sub; PopupTexts(out tt, out sub);
            string[] ic = new string[] { "save", "image", "pdf" };
            Color[][] gr = new Color[][] {
                new Color[] { Color.FromArgb(99, 102, 241), Color.FromArgb(139, 92, 246) },
                new Color[] { Color.FromArgb(249, 115, 22), Color.FromArgb(236, 72, 153) },
                new Color[] { Color.FromArgb(244, 63, 94), Color.FromArgb(225, 29, 72) } };
            if (popKind == "math")
            {
                ic = new string[] { "ruler", "protractor", "axes" };
                gr = new Color[][] {
                    new Color[] { Color.FromArgb(245, 158, 11), Color.FromArgb(234, 88, 12) },
                    new Color[] { Color.FromArgb(14, 165, 233), Color.FromArgb(37, 99, 235) },
                    new Color[] { Color.FromArgb(16, 185, 129), Color.FromArgb(5, 150, 105) } };
            }
            for (int i = 0; i < menuRows.Count; i++)
            {
                RectangleF r = menuRows[i];
                if (menuHov[i] > 0.01f)
                    using (GraphicsPath hp = RoundRect(r, 14f * u))
                    using (SolidBrush hb = new SolidBrush(Color.FromArgb((int)(255f * menuHov[i]), th.Hover))) g.FillPath(hb, hp);
                RectangleF bd = En
                    ? new RectangleF(r.X + 12f * u, r.Y + (r.Height - 44f * u) / 2f, 44f * u, 44f * u)
                    : new RectangleF(r.Right - 12f * u - 44f * u, r.Y + (r.Height - 44f * u) / 2f, 44f * u, 44f * u);
                using (GraphicsPath bp2 = RoundRect(bd, 13f * u))
                using (LinearGradientBrush lb = new LinearGradientBrush(bd, gr[i][0], gr[i][1], 45f)) g.FillPath(lb, bp2);
                DrawIcon(g, ic[i], bd.X + bd.Width / 2f, bd.Y + bd.Height / 2f, Color.White);
                float txL = En ? bd.Right + 14f * u : r.X + 12f * u;
                float txR = En ? r.Right - 12f * u : bd.X - 14f * u;
                if (i == 0 && popKind != "math")
                {
                    string dl = Loc.Tr("الافتراضي");
                    float cw = TWf(dl, uiSmall) + 16f * u;
                    RectangleF pill = En
                        ? new RectangleF(r.Right - 12f * u - cw, r.Y + (r.Height - 22f * u) / 2f, cw, 22f * u)
                        : new RectangleF(r.X + 12f * u, r.Y + (r.Height - 22f * u) / 2f, cw, 22f * u);
                    using (GraphicsPath pp = RoundRect(pill, 11f * u))
                    using (SolidBrush pb = new SolidBrush(th.AccentSoft)) g.FillPath(pb, pp);
                    using (SolidBrush pt = new SolidBrush(th.Accent)) UiText(g, dl, uiSmall, pt, pill, fmtRC);
                    if (En) txR = pill.X - 10f * u; else txL = pill.Right + 10f * u;
                }
                float tw = txR - txL;
                using (SolidBrush t1 = new SolidBrush(th.Ink)) UiText(g, tt[i], uiBold, t1, new RectangleF(txL, r.Y + 8f * u, tw, 26f * u), fmtTab);
                using (SolidBrush t2 = new SolidBrush(th.Muted)) UiText(g, sub[i], uiSmall, t2, new RectangleF(txL, r.Y + 33f * u, tw, 22f * u), fmtTab);
            }
        }


    }
}
