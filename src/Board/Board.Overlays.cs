// Board: About card, hover-reveal tools menu, symbol bar host.
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
        // ---------- about ----------
        void BuildAbout(float W, float H, float u)
        {
            obtns.Clear();
            if (!showAbout && aboutT <= 0.01f) return;
            float cw = 470f * u, ch = 440f * u;
            aboutCard = new RectangleF((W - cw) / 2f, Math.Max(52f * u, (H - ch) / 2f), cw, ch);
            aboutIconRect = RectangleF.Inflate(new RectangleF(aboutCard.X + aboutCard.Width / 2f, aboutCard.Y + 92f * u, 0f, 0f), 104f * u, 104f * u);
            float by = aboutCard.Bottom - 22f * u - 42f * u, bw = 180f * u;
            Btn mail = new Btn();
            mail.Id = "ab:mail"; mail.R = new RectangleF(aboutCard.Right - 26f * u - bw, by, bw, 42f * u); mail.Icon = "mail"; mail.Label = Loc.Tr("مراسلة"); mail.Primary = true; mail.Tip = "os.dh@outlook.com";
            obtns.Add(mail);
            Btn copy = new Btn();
            copy.Id = "ab:copy"; copy.R = new RectangleF(aboutCard.X + 26f * u, by, bw, 42f * u); copy.Icon = "copy"; copy.Label = Loc.Tr("نسخ البريد"); copy.Tip = Loc.Tr("نسخ البريد");
            obtns.Add(copy);
            Btn close = new Btn();
            close.Id = "ab:close"; close.R = new RectangleF(En ? aboutCard.Right - 16f * u - 34f * u : aboutCard.X + 16f * u, aboutCard.Y + 16f * u, 34f * u, 34f * u); close.Icon = "close"; close.Tip = Loc.Tr("إغلاق");
            obtns.Add(close);
        }

        // The About card = static part (panel + texts) + live part (animated icon + buttons).
        // The static part is the expensive one (Arabic text layout), so it is rendered once into a bitmap that the
        // fade-in reuses; while the card is fully open only the rectangles that are really dirty are redrawn.
        void DrawAboutCard(Graphics g)
        {
            DrawAboutStatic(g);
            DrawAboutLive(g);
        }

        void DrawAboutLive(Graphics g)
        {
            float u = ui;
            float cx = aboutCard.X + aboutCard.Width / 2f;
            DrawTeachIcon(g, cx, aboutCard.Y + 24f * u + 68f * u, 136f * u, clock.Elapsed.TotalSeconds, 255);
            foreach (Btn b in obtns) DrawBtn(g, b);
        }

        static bool Dirty(RectangleF clip, RectangleF r, float pad)
        {
            return clip.IntersectsWith(RectangleF.Inflate(r, pad, pad));
        }

        void DrawAboutStatic(Graphics g)
        {
            float u = ui;
            RectangleF cb = g.ClipBounds;
            DrawPanel(g, aboutCard);
            float cx = aboutCard.X + aboutCard.Width / 2f;
            using (SolidBrush ink = new SolidBrush(th.Ink))
            using (SolidBrush mut = new SolidBrush(th.Muted))
            using (SolidBrush acc = new SolidBrush(th.Accent))
            {
                RectangleF r1 = new RectangleF(aboutCard.X, aboutCard.Y + 164f * u, aboutCard.Width, 44f * u);
                RectangleF r2 = new RectangleF(aboutCard.X, aboutCard.Y + 206f * u, aboutCard.Width, 22f * u);
                RectangleF r3 = new RectangleF(aboutCard.X, aboutCard.Y + 232f * u, aboutCard.Width, 28f * u);
                RectangleF r4 = new RectangleF(aboutCard.X, aboutCard.Y + 284f * u, aboutCard.Width, 22f * u);
                if (Dirty(cb, r1, 4f * u)) UiText(g, Loc.Tr("السبورة"), uiTitle, ink, r1, fmtC);
                if (Dirty(cb, r2, 4f * u)) UiText(g, Loc.Tr("الإصدار") + " " + AppInfo.Version, uiSmall, mut, r2, fmtC);
                if (Dirty(cb, r3, 4f * u)) UiText(g, Loc.Tr("تم بواسطة أســامة"), uiBold, acc, r3, fmtC);
                if (Dirty(cb, new RectangleF(aboutCard.X, aboutCard.Y + 272f * u, aboutCard.Width, 1f), 2f * u))
                    using (SolidBrush dv = new SolidBrush(th.Border)) g.FillRectangle(dv, aboutCard.X + 44f * u, aboutCard.Y + 272f * u, aboutCard.Width - 88f * u, 1f);
                if (Dirty(cb, r4, 4f * u)) UiText(g, Loc.Tr("للتواصل والاقتراحات"), uiSmall, mut, r4, fmtC);
                string email = "os.dh@outlook.com";
                float ew = TWf(email, uiBold) + 40f * u;
                RectangleF pill = new RectangleF(cx - ew / 2f, aboutCard.Y + 312f * u, ew, 38f * u);
                if (Dirty(cb, pill, 4f * u))
                {
                    using (GraphicsPath pp = RoundRect(pill, 19f * u))
                    using (SolidBrush pb = new SolidBrush(th.AccentSoft)) g.FillPath(pb, pp);
                    UiText(g, email, uiBold, acc, pill, fmtC);
                }
            }
        }

        Bitmap aboutStatic; string aboutStaticKey; Rectangle aboutStaticRect;

        string AboutKey()
        {
            return "about|" + (En ? 1 : 0) + "|" + F(ui) + "|" + F(aboutCard.X) + "," + F(aboutCard.Y) + "," + F(aboutCard.Width) + "," + F(aboutCard.Height)
                + "|" + th.Panel.ToArgb() + "|" + th.Ink.ToArgb() + "|" + th.Muted.ToArgb() + "|" + th.Accent.ToArgb() + "|" + th.Border.ToArgb() + "|" + th.AccentSoft.ToArgb();
        }

        // Used inside the fade layer (grayscale text, same as every other faded layer): blit the cached static part, draw the live part.
        void DrawAboutFadeContent(Graphics g)
        {
            RectangleF area = RectangleF.Inflate(aboutCard, 44f * ui, 44f * ui);
            Rectangle ar = new Rectangle((int)Math.Floor(area.X), (int)Math.Floor(area.Y), (int)Math.Ceiling(area.Width) + 1, (int)Math.Ceiling(area.Height) + 1);
            string key = AboutKey();
            if (aboutStatic == null || aboutStaticKey != key || aboutStatic.Width != ar.Width || aboutStatic.Height != ar.Height)
            {
                if (aboutStatic != null) aboutStatic.Dispose();
                aboutStatic = new Bitmap(ar.Width, ar.Height, PixelFormat.Format32bppPArgb);
                using (Graphics sg = Graphics.FromImage(aboutStatic))
                {
                    sg.Clear(Color.Transparent);
                    SetQuality(sg);
                    sg.TranslateTransform(-ar.X, -ar.Y);
                    DrawAboutStatic(sg);
                }
                aboutStaticKey = key; aboutStaticRect = ar;
            }
            g.DrawImage(aboutStatic, ar, 0, 0, ar.Width, ar.Height, GraphicsUnit.Pixel);
            DrawAboutLive(g);
        }

        void AboutAction(string a)
        {
            if (a == "close") { showAbout = false; Invalidate(); }
            else if (a == "copy")
            {
                try { Clipboard.SetText("os.dh@outlook.com"); Toast(Loc.Tr("تم نسخ البريد الإلكتروني")); }
                catch (Exception) { Toast(Loc.Tr("تعذّر نسخ البريد")); }
            }
            else if (a == "mail")
            {
                try { System.Diagnostics.Process.Start("mailto:os.dh@outlook.com"); }
                catch (Exception) { Toast(Loc.Tr("تعذّر فتح تطبيق البريد")); }
            }
        }

        // ---------- hover-reveal tools menu ----------
        void ComputeToolsZone(float W, float H, float u)
        {
            RectangleF tf = En ? Mir(toolsFull, W) : toolsFull;
            toolsZone = RectangleF.Inflate(tf, 16f * u, 16f * u);
            toolsStrip = En ? new RectangleF(0f, tf.Y - 24f * u, 40f * u, tf.Height + 48f * u) : new RectangleF(W - 40f * u, tf.Y - 24f * u, 40f * u, tf.Height + 48f * u);
            float cy = tf.Y + tf.Height / 2f;
            toolsGrip = En ? new RectangleF(0f, cy - 36f * u, 16f * u, 72f * u) : new RectangleF(W - 16f * u, cy - 36f * u, 16f * u, 72f * u);
        }

        void DrawToolsGrip(Graphics g)
        {
            if (toolsT > 0.97f || toolsGrip.Width < 1f) return;
            double ts = clock.Elapsed.TotalSeconds;
            float pulse = ts < 16.0 ? 0.5f + 0.5f * (float)Math.Sin(ts * 3.2) : 0.3f;
            float a = (1f - Ease(toolsT)) * (0.55f + 0.45f * pulse);
            int al = (int)(255f * Math.Max(0f, Math.Min(1f, a)));
            float u = ui;
            RectangleF r = En
                ? new RectangleF(-14f * u, toolsGrip.Y, toolsGrip.Width + 14f * u, toolsGrip.Height)
                : new RectangleF(toolsGrip.X, toolsGrip.Y, toolsGrip.Width + 14f * u, toolsGrip.Height);
            using (GraphicsPath gp = RoundRect(r, 9f * u))
            {
                using (SolidBrush fb = new SolidBrush(Color.FromArgb(al, th.Panel))) g.FillPath(fb, gp);
                using (Pen bp = new Pen(Color.FromArgb(al, th.Border), 1f)) g.DrawPath(bp, gp);
            }
            float dx = En ? toolsGrip.X + 7f * u : toolsGrip.X + 8f * u, cy = toolsGrip.Y + toolsGrip.Height / 2f;
            using (SolidBrush db = new SolidBrush(Color.FromArgb(al, th.Accent)))
                for (int i = -1; i <= 1; i++) g.FillEllipse(db, dx - 1.8f * u, cy + i * 8f * u - 1.8f * u, 3.6f * u, 3.6f * u);
        }

        void ShowSymBar(TextBox tb)
        {
            if (symBar != null) { Controls.Remove(symBar); symBar.Dispose(); symBar = null; }
            SymbolBar bar = new SymbolBar(ui, th);
            bar.Picked = delegate (string sym) { if (edit != null) { edit.SelectedText = sym; } };
            int x = Math.Max(4, Math.Min(Width - bar.Width - 4, tb.Left));
            // The B I U S bar sits below the text, so the symbols go above it (and the other way round when it flips).
            bool fmtOn = editItem != null && editItem.K == Kind.Text, fmtBelow = true;
            if (fmtOn) FmtBarY(new RectangleF(tb.Left, tb.Top, tb.Width, tb.Height), 46f * ui, out fmtBelow);
            int y;
            if (fmtOn && !fmtBelow) y = tb.Bottom + (int)(8f * ui);
            else
            {
                y = tb.Top - bar.Height - (int)(8f * ui);
                if (y < (int)tabBar.Bottom + 4) y = tb.Bottom + (int)(8f * ui) + (fmtOn ? (int)(22f * ui + 46f * ui) : 0);
            }
            bar.Location = new Point(x, Math.Max(2, Math.Min(Height - bar.Height - 2, y)));
            symBar = bar;
            Controls.Add(bar);
            bar.BringToFront();
        }

        int MenuRowAt(Point p)
        {
            if (!saveMenu) return -1;
            for (int i = 0; i < menuRows.Count; i++) if (menuRows[i].Contains(p.X, p.Y)) return i;
            return -1;
        }


    }
}
