// Board: BuildUi (layout of every panel/button).
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
        // ---------- UI layout ----------
        static string ToolLabel(string id)
        {
            switch (id)
            {
                case "select": return Loc.Tr("تحديد");
                case "hand": return Loc.Tr("تحريك");
                case "pen": return Loc.Tr("قلم");
                case "highlighter": return Loc.Tr("تمييز");
                case "eraser": return Loc.Tr("ممحاة");
                case "line": return Loc.Tr("خط");
                case "arrow": return Loc.Tr("سهم");
                case "rect": return Loc.Tr("مستطيل");
                case "ellipse": return Loc.Tr("بيضاوي");
                case "triangle": return Loc.Tr("مثلث");
                case "star": return Loc.Tr("نجمة");
                case "text": return Loc.Tr("نص");
                case "note": return Loc.Tr("ملاحظة");
                case "image": return Loc.Tr("صورة");
                case "math": return Loc.Tr("رياضيات");
            }
            return id;
        }

        static string ToolKey(string id)
        {
            switch (id)
            {
                case "select": return "V";
                case "hand": return "H";
                case "pen": return "P";
                case "highlighter": return "M";
                case "eraser": return "E";
                case "line": return "L";
                case "arrow": return "A";
                case "rect": return "R";
                case "ellipse": return "O";
                case "triangle": return "G";
                case "star": return "S";
                case "text": return "T";
                case "note": return "N";
            }
            return "";
        }

        static string ToolTip(string id) { if (id == "image") return Loc.Tr("صورة (Ctrl+I)"); if (id == "math") return Loc.Tr("أدوات الرياضيات"); return ToolLabel(id) + " (" + ToolKey(id) + ")"; }

        // Draws a single line of UI text with its baseline and edges snapped to whole pixels (much sharper than fractional placement).
        void UiText(Graphics g, string text, Font f, Brush br, RectangleF rect, StringFormat fmt)
        {
            if (string.IsNullOrEmpty(text)) return;
            FontFamily ff = f.FontFamily; FontStyle st = f.Style;
            float em = ff.GetEmHeight(st);
            float asc = f.Size * ff.GetCellAscent(st) / em;
            float lh = f.Size * ff.GetLineSpacing(st) / em;
            float baseline = (float)Math.Round(rect.Y + rect.Height / 2f - lh / 2f + asc);
            float y = baseline - asc;
            RectangleF rr;
            if (fmt.Alignment == StringAlignment.Center)
            {
                float w = TWf(text, f);
                float lx = (float)Math.Round(rect.X + rect.Width / 2f - w / 2f);
                rr = new RectangleF(lx, y, Math.Max(1f, w), lh + 2f);
            }
            else
            {
                float x = (float)Math.Round(rect.X), x2 = (float)Math.Round(rect.Right);
                rr = new RectangleF(x, y, Math.Max(1f, x2 - x), lh + 2f);
            }
            using (StringFormat nf = (StringFormat)fmt.Clone())
            {
                nf.LineAlignment = StringAlignment.Near;
                g.DrawString(text, f, br, rr, nf);
            }
        }

        // Text widths are cached: BuildUi runs on every paint and mouse move and used to call MeasureString dozens of times each time.
        Dictionary<string, float> measureCache = new Dictionary<string, float>();

        float TW(string t) { return TWf(t, uiFont); }

        float TWf(string t, Font f)
        {
            string k = f.FontFamily.Name + (f.Bold ? "|b|" : "|r|") + F(f.Size) + "|" + t;
            float v;
            if (measureCache.TryGetValue(k, out v)) return v;
            if (measureCache.Count > 3000) measureCache.Clear();
            v = mgUi.MeasureString(t, f, new SizeF(100000f, 100f), fmtRN).Width;
            measureCache[k] = v;
            return v;
        }

        void AddBtn(string id, float x, float y, float w, float h, string icon, string label, string tip, bool enabled, bool primary)
        {
            Btn b = new Btn();
            b.Id = id; b.R = new RectangleF(x, y, w, h); b.Icon = icon; b.Label = label; b.Tip = tip; b.Enabled = enabled; b.Primary = primary;
            btns.Add(b);
        }

        void BuildUi()
        {
            float u = ui, W = ClientSize.Width, H = ClientSize.Height;
            if (uiFont == null || Math.Abs(uiFontU - u) > 0.01f)
            {
                uiFont = AppFonts.Make(14f * u, FontStyle.Bold);
                uiBold = AppFonts.Make(14f * u, FontStyle.Bold);
                uiSmall = AppFonts.Make(12.5f * u, FontStyle.Bold);
                uiSplash = AppFonts.Make(46f * u, FontStyle.Bold);
                uiTitle = AppFonts.Make(30f * u, FontStyle.Bold);
                measureCache.Clear();
                uiFontU = u;
            }
            btns.Clear(); panels.Clear(); seps.Clear(); lbls.Clear();
            float gp = 2f * u, pad = 6f * u, m = 14f * u, rowH = 34f * u, barH = rowH + 2f * pad;
            float ip = 10f * u, iw = 20f * u;

            // ---- tools panel (right side) ----
            RectangleF mathR = RectangleF.Empty;
            string[] ids = new string[] { "select", "hand", "-", "pen", "highlighter", "eraser", "-", "text", "note", "math", "image", "-", "line", "arrow", "rect", "ellipse", "triangle", "star" };
            int nTools = 0, nSeps = 0;
            foreach (string id0 in ids) { if (id0 == "-") nSeps++; else nTools++; }
            float topY = 40f * u + 10f * u;
            float availH = H - (topY + barH + 6f * u) - (m + barH + 6f * u);
            float tbh = Clamp((availH - 34f * u - (pad * 2f - gp) - nTools * gp - nSeps * 9f * u) / nTools, 24f * u, 34f * u), tlw = 0f;
            foreach (string id in ids) if (id != "-") tlw = Math.Max(tlw, TW(ToolLabel(id)));
            float tbw = Math.Max(tlw + iw + ip * 3f, 124f * u);
            float tph = pad * 2f - gp + 34f * u;
            foreach (string id in ids) tph += id == "-" ? 9f * u : tbh + gp;
            float tpw = tbw + pad * 2f, tpx = W - m - tpw, tpy = Math.Max(topY + barH + 6f * u, (H - tph) / 2f);
            float hideDx = (1f - Ease(toolsT)) * (tpw + m + 10f * u);
            toolsFull = new RectangleF(tpx, tpy, tpw, tph);
            toolsShift = En ? -hideDx : hideDx;
            panels.Add(new RectangleF(tpx + hideDx, tpy, tpw, tph));
            AddBtn("pin", tpx + hideDx + pad, tpy + pad, tbw, 30f * u, "pin", Loc.Tr("تثبيت"), Loc.Tr("تثبيت القائمة (تبقى ظاهرة دائماً)"), true, false);
            float y = tpy + pad + 34f * u;
            foreach (string id in ids)
            {
                if (id == "-") { seps.Add(new RectangleF(tpx + hideDx + pad + 8f * u, y + 4f * u, tbw - 16f * u, 1f)); y += 9f * u; continue; }
                // "image" and "math" are actions (file dialog / tools popup), not drawing tools, so they have no "t:" prefix.
                bool isAct = id == "image" || id == "math";
                AddBtn(isAct ? id : "t:" + id, tpx + hideDx + pad, y, tbw, tbh, id, ToolLabel(id), ToolTip(id), true, false);
                if (id == "math") mathR = new RectangleF(tpx + hideDx + pad, y, tbw, tbh);
                y += tbh + gp;
            }

            // ---- tab bar (top, like a browser) ----
            float tabBarH = 40f * u;
            tabBar = new RectangleF(0f, 0f, W, tabBarH);
            chip = RectangleF.Empty;
            tabRects.Clear(); tabClose.Clear();
            int nt = docs.Count;
            float plusW = 30f * u, edge = 12f * u, tgap = 2f * u;
            float tabW = Clamp((W - edge * 2f - plusW - 14f * u - tgap * nt) / Math.Max(1, nt), 100f * u, 230f * u);
            float trx = W - edge;
            for (int i = 0; i < nt; i++)
            {
                RectangleF trc = new RectangleF(trx - tabW, 6f * u, tabW, tabBarH - 6f * u);
                tabRects.Add(trc);
                tabClose.Add(new RectangleF(trc.X + 8f * u, trc.Y + (trc.Height - 22f * u) / 2f, 22f * u, 22f * u));
                trx -= tabW + tgap;
            }
            AddBtn("newtab", trx - plusW - 2f * u, 6f * u + (tabBarH - 6f * u - 28f * u) / 2f, plusW, 28f * u, "zin", null, Loc.Tr("نافذة جديدة (Ctrl+N)"), true, false);

            // ---- actions (top, centered) ----
            string[] aid = new string[] { "new", "open", "save", "help", "about", "lang" };
            string[] alb = new string[] { Loc.Tr("جديد"), Loc.Tr("فتح"), Loc.Tr("حفظ"), Loc.Tr("مساعدة"), Loc.Tr("حول"), En ? Loc.Tr("العربية") : "English" };
            string[] atp = new string[] { Loc.Tr("نافذة جديدة (Ctrl+N)"), Loc.Tr("فتح ملف (Ctrl+O)"), Loc.Tr("حفظ بصيغ متعددة"), Loc.Tr("المساعدة والاختصارات (F1)"), Loc.Tr("حول البرنامج"), En ? "Switch to Arabic" : Loc.Tr("التبديل إلى الإنجليزية") };
            float[] aw = new float[6];
            float totA = pad * 2f - gp;
            for (int i = 0; i < 6; i++) { aw[i] = TW(alb[i]) + iw + ip * 3f; totA += aw[i] + gp; }
            bool topCompact = totA + 2f * m > W;
            if (topCompact) { totA = pad * 2f - gp; for (int i = 0; i < 6; i++) { aw[i] = rowH; totA += rowH + gp; } }
            float axL = (W - totA) / 2f;
            panels.Add(new RectangleF(axL, topY, totA, barH));
            float acx = axL + totA - pad;
            RectangleF saveR = RectangleF.Empty;
            for (int i = 0; i < 6; i++)
            {
                acx -= aw[i];
                AddBtn(aid[i], acx, topY + pad, aw[i], rowH, aid[i], topCompact ? null : alb[i], atp[i], true, aid[i] == "save");
                if (aid[i] == "save") saveR = new RectangleF(acx, topY + pad, aw[i], rowH);
                acx -= gp;
            }

            // ---- save menu geometry ----
            menuRows.Clear();
            if (saveMenu || menuT > 0.01f)
            {
                float rowM = 68f * u, hdr = 42f * u, pm = 10f * u;
                float mw2 = 390f * u;
                string[] ptt, psub; PopupTexts(out ptt, out psub);
                for (int q = 0; q < 3; q++)
                {
                    float nw = Math.Max(TWf(ptt[q], uiBold), TWf(psub[q], uiSmall)) + 2f * pm + 70f * u + 16f * u;
                    if (q == 0 && popKind != "math") nw += TWf(Loc.Tr("الافتراضي"), uiSmall) + 26f * u;
                    mw2 = Math.Max(mw2, nw);
                }
                mw2 = Math.Min(mw2, W - 2f * m);
                float mh2 = hdr + 3f * rowM + pm;
                popSide = popKind == "math";
                if (popSide)
                {
                    // Math lives in the side menu: its popup opens to the LEFT of the side menu, with the caret pointing at the Math button.
                    float mxs = Math.Max(m, mathR.X - 22f * u - mw2);
                    float cyb = mathR.Y + mathR.Height / 2f;
                    float lo2 = topY + barH + 6f * u, hi2 = H - m - barH - 8f * u - mh2;
                    float mys = hi2 < lo2 ? lo2 : Clamp(cyb - mh2 / 2f, lo2, hi2);
                    menuRect = new RectangleF(mxs, mys, mw2, mh2);
                    menuCaret = new RectangleF(mxs + mw2 - 1f, Clamp(cyb - 9f * u, mys + 22f * u, mys + mh2 - 22f * u - 18f * u), 11f * u, 18f * u);
                }
                else
                {
                    RectangleF anc = saveR;
                    float mxa = Clamp(anc.X + anc.Width / 2f - mw2 / 2f, m, W - m - mw2);
                    float mya = anc.Bottom + pad + 12f * u;
                    menuRect = new RectangleF(mxa, mya, mw2, mh2);
                    menuCaret = new RectangleF(anc.X + anc.Width / 2f - 9f * u, mya - 9f * u, 18f * u, 10f * u);
                }
                float mx = menuRect.X, my = menuRect.Y;
                for (int i = 0; i < 3; i++) menuRows.Add(new RectangleF(mx + pm, my + hdr + i * rowM, mw2 - pm * 2f, rowM - 4f * u));
            }

            // ---- bottom bars: measure full and compact layouts ----
            string[] hid = new string[] { "undo", "redo", "clear" };
            string[] hlb = new string[] { Loc.Tr("تراجع"), Loc.Tr("إعادة"), Loc.Tr("مسح") };
            string[] htp = new string[] { Loc.Tr("تراجع (Ctrl+Z)"), Loc.Tr("إعادة (Ctrl+Y)"), Loc.Tr("مسح اللوحة") };
            float[] hwF = new float[3];
            float histF = pad * 2f - gp;
            for (int i = 0; i < 3; i++) { hwF[i] = TW(hlb[i]) + iw + ip * 3f; histF += hwF[i] + gp; }
            float histC = pad * 2f - gp + 3f * (rowH + gp);
            float fitW = TW(Loc.Tr("ملء الشاشة")) + iw + ip * 3f, zr = 62f * u;
            float zoomF = pad * 2f + rowH + zr + rowH + fitW + 3f * gp;
            float zoomC = pad * 2f + rowH + zr + rowH + rowH + 3f * gp;
            float sw = 30f * u, sepGap = 14f * u;
            float lc = 0f, ls = 0f;     // (the words "Color" / "Thickness" were removed from the style bar)
            string themeLbl = th.Dark ? Loc.Tr("الوضع الفاتح") : Loc.Tr("الوضع الداكن");
            float gridW = TW(Loc.Tr("الشبكة")) + iw + ip * 3f, themeW = TW(themeLbl) + iw + ip * 3f;
            string laserLbl = Loc.Tr("الليزر");
            float laserW = TW(laserLbl) + iw + ip * 3f;
            float styleF = pad * 2f + 6f * u + 9f * sw + sepGap + 4f * sw + sepGap + gridW + gp + themeW + gp + laserW;
            float styleC = pad * 2f + 6f * u + 9f * sw + sepGap + 4f * sw + sepGap + rowH + gp + rowH + gp + rowH;
            compact = histF + styleF + zoomF + 4f * m > W;
            float histW = compact ? histC : histF, zoomW = compact ? zoomC : zoomF, styleW = compact ? styleC : styleF;
            float by = H - m - barH;

            // history (bottom-right)
            float hx = W - m - histW;
            panels.Add(new RectangleF(hx, by, histW, barH));
            float hcx = hx + histW - pad;
            for (int i = 0; i < 3; i++)
            {
                float w = compact ? rowH : hwF[i];
                hcx -= w;
                bool en = hid[i] == "undo" ? undo.Count > 1 : (hid[i] == "redo" ? redo.Count > 0 : true);
                AddBtn(hid[i], hcx, by + pad, w, rowH, hid[i], compact ? null : hlb[i], htp[i], en, false);
                hcx -= gp;
            }

            // zoom (bottom-left)
            zoomPanelIdx = panels.Count;
            panels.Add(new RectangleF(m, by, zoomW, barH));
            float zcx = m + pad;
            AddBtn("zout", zcx, by + pad, rowH, rowH, "zout", null, Loc.Tr("تصغير (Ctrl -)"), true, false); zcx += rowH + gp;
            AddBtn("zreset", zcx, by + pad, zr, rowH, null, ((int)Math.Round(vz * 100)) + "%", Loc.Tr("الحجم الأصلي (Ctrl 0)"), true, false); zcx += zr + gp;
            AddBtn("zin", zcx, by + pad, rowH, rowH, "zin", null, Loc.Tr("تكبير (Ctrl +)"), true, false); zcx += rowH + gp;
            AddBtn("zfit", zcx, by + pad, compact ? rowH : fitW, rowH, "fit", compact ? null : Loc.Tr("ملء الشاشة"), Loc.Tr("ملء الشاشة (Esc للخروج)"), true, false);

            // style bar (bottom-center), laid out right-to-left
            float sx = (W - styleW) / 2f, lo = m + zoomW + m, hi = W - m - histW - m - styleW;
            sx = hi < lo ? lo : Clamp(sx, lo, hi);
            panels.Add(new RectangleF(sx, by, styleW, barH));
            float scx = sx + styleW - pad - 3f * u;
            float sby = by + pad + (rowH - sw) / 2f;
            string[] cn = new string[] { Loc.Tr("لون تلقائي (يتبع الوضع)"), Loc.Tr("أحمر"), Loc.Tr("برتقالي"), Loc.Tr("أصفر"), Loc.Tr("أخضر"), Loc.Tr("سماوي"), Loc.Tr("نيلي"), Loc.Tr("وردي") };
            string[] sn = new string[] { Loc.Tr("رفيع"), Loc.Tr("متوسط"), Loc.Tr("سميك"), Loc.Tr("سميك جداً") };
            for (int i = 0; i < 8; i++) { scx -= sw; AddBtn("c:" + i, scx, sby, sw, sw, null, null, cn[i], true, false); }
            scx -= sw; AddBtn("cc", scx, sby, sw, sw, null, null, Loc.Tr("لون مخصص \u2014 اختر أي لون تريده"), true, false);
            seps.Add(new RectangleF(scx - sepGap / 2f, by + (barH - 22f * u) / 2f, 1f, 22f * u)); scx -= sepGap;
            for (int i = 0; i < 4; i++) { scx -= sw; AddBtn("s:" + i, scx, sby, sw, sw, null, null, sn[i], true, false); }
            seps.Add(new RectangleF(scx - sepGap / 2f, by + (barH - 22f * u) / 2f, 1f, 22f * u)); scx -= sepGap;
            float gw = compact ? rowH : gridW;
            scx -= gw; AddBtn("grid", scx, by + pad, gw, rowH, "grid", compact ? null : Loc.Tr("الشبكة"), Loc.Tr("إظهار أو إخفاء الشبكة"), true, false); scx -= gp;
            float tw2 = compact ? rowH : themeW;
            scx -= tw2; AddBtn("theme", scx, by + pad, tw2, rowH, "theme", compact ? null : themeLbl, th.Dark ? Loc.Tr("التبديل إلى الوضع الفاتح") : Loc.Tr("التبديل إلى الوضع الداكن"), true, false);
            scx -= gp;
            float lw2 = compact ? rowH : laserW;
            scx -= lw2; AddBtn("laser", scx, by + pad, lw2, rowH, "laser", compact ? null : laserLbl, laserOn ? Loc.Tr("إيقاف الليزر (Ctrl+L)") : Loc.Tr("تشغيل الليزر: يظهر للطلاب مكان الفأرة (Ctrl+L)"), true, false);
            BuildAxesPanel(W, H, u, m, topY, barH);
            BuildFmtBar(W, u, m);
            BuildAbout(W, H, u);
            if (En) MirrorUi(W);
            ComputeToolsZone(W, H, u);
        }


    }
}
