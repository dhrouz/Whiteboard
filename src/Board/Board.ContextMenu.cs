// Board: right-click context menu (custom-drawn so it matches the theme, the font and RTL layout).
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
        bool ctxOpen; float ctxT; int ctxHover = -1; RectangleF ctxRect;
        List<CtxRow> ctxRows = new List<CtxRow>();

        void AddCtx(string id, string label, string key, string icon, bool danger)
        {
            CtxRow r = new CtxRow(); r.Id = id; r.Label = label; r.Key = key; r.Icon = icon; r.Danger = danger;
            ctxRows.Add(r);
        }

        void AddCtxSep() { CtxRow r = new CtxRow(); r.Sep = true; ctxRows.Add(r); }

        void ShowContextMenu(Point sp, PointF wp)
        {
            ctxRows.Clear(); ctxHover = -1; hover = null; tipVisible = false;
            Item hit = TopHit(wp);
            if (hit != null)
            {
                if (!sel.Contains(hit.Id)) { SetTool(Tool.Select); sel.Clear(); sel.Add(hit.Id); }
                Item one = SingleSel();
                bool canRot = false;
                foreach (Item it in items) if (sel.Contains(it.Id) && it.K != Kind.Axes) canRot = true;
                if (one != null && (one.K == Kind.Text || one.K == Kind.Note)) { AddCtx("edit", Loc.Tr("تعديل النص"), "", "pen", false); AddCtxSep(); }
                AddCtx("cut", Loc.Tr("قص"), "Ctrl+X", "cut", false);
                AddCtx("copy", Loc.Tr("نسخ"), "Ctrl+C", "copy", false);
                AddCtx("dup", Loc.Tr("تكرار"), "Ctrl+D", "dup", false);
                AddCtx("del", Loc.Tr("حذف"), "Del", "clear", true);
                AddCtxSep();
                AddCtx("front", Loc.Tr("إلى الأمام"), "Ctrl+]", "front", false);
                AddCtx("back", Loc.Tr("إلى الخلف"), "Ctrl+[", "back", false);
                if (canRot) AddCtx("rot", Loc.Tr("تدوير 90°"), "Ctrl+R", "rot", false);
            }
            else
            {
                sel.Clear();
                AddCtx("paste", Loc.Tr("لصق"), "Ctrl+V", "paste", false);
                AddCtx("selall", Loc.Tr("تحديد الكل"), "Ctrl+A", "selall", false);
            }
            float u = ui, pad = 6f * u, rowH = 34f * u, sepH = 9f * u;
            float need = 0f, h = pad * 2f;
            foreach (CtxRow r in ctxRows)
            {
                if (r.Sep) { h += sepH; continue; }
                h += rowH;
                need = Math.Max(need, TWf(r.Label, uiFont) + (r.Key.Length > 0 ? TWf(r.Key, uiSmall) + 28f * u : 0f));
            }
            float w = Math.Max(200f * u, need + 2f * pad + 12f * u + 20f * u + 12f * u + 14f * u);
            float x = En ? sp.X : sp.X - w, y = sp.Y;
            if (y + h > Height - 6f * u) y = sp.Y - h;
            x = Clamp(x, 6f * u, Width - w - 6f * u);
            y = Clamp(y, tabBar.Bottom + 4f * u, Math.Max(tabBar.Bottom + 4f * u, Height - h - 6f * u));
            ctxRect = new RectangleF(x, y, w, h);
            float cy = y + pad;
            foreach (CtxRow r in ctxRows)
            {
                float rh = r.Sep ? sepH : rowH;
                r.R = new RectangleF(x + pad, cy, w - 2f * pad, rh);
                cy += rh;
            }
            ctxOpen = true;
            Invalidate();
        }

        void CloseCtx() { if (!ctxOpen) return; ctxOpen = false; ctxHover = -1; Invalidate(); }

        int CtxRowAt(Point p)
        {
            if (!ctxOpen) return -1;
            for (int i = 0; i < ctxRows.Count; i++)
                if (!ctxRows[i].Sep && ctxRows[i].R.Contains(p.X, p.Y)) return i;
            return -1;
        }

        void RunCtx(string id)
        {
            switch (id)
            {
                case "edit": { Item one = SingleSel(); if (one != null) StartEdit(one, false); break; }
                case "cut": Copy(); DeleteSel(); Toast(Loc.Tr("تم القص")); break;
                case "copy": Copy(); break;
                case "dup": Duplicate(); break;
                case "del": DeleteSel(); break;
                case "front": Reorder(true); break;
                case "back": Reorder(false); break;
                case "rot": RotateQuick(); break;
                case "paste": Paste(); break;
                case "selall": SelectAll(); break;
            }
        }

        void DrawCtxCore(Graphics g)
        {
            float u = ui;
            DrawPanel(g, ctxRect);
            Color danger = Color.FromArgb(220, 38, 38);
            using (StringFormat kf = MakeUiFmt(StringAlignment.Far, !En))
                for (int i = 0; i < ctxRows.Count; i++)
                {
                    CtxRow r = ctxRows[i];
                    if (r.Sep)
                    {
                        using (SolidBrush sb = new SolidBrush(th.Border)) g.FillRectangle(sb, r.R.X + 10f * u, r.R.Y + r.R.Height / 2f, r.R.Width - 20f * u, 1f);
                        continue;
                    }
                    if (r.Hv > 0.01f)
                        using (GraphicsPath hp = RoundRect(RectangleF.Inflate(r.R, -1f * u, -1f * u), 9f * u))
                        using (SolidBrush hb = new SolidBrush(Color.FromArgb((int)(255f * r.Hv), r.Danger ? Color.FromArgb(254, 226, 226) : th.Hover))) g.FillPath(hb, hp);
                    Color fg = r.Danger ? danger : th.Ink;
                    float icx = En ? r.R.X + 12f * u + 10f * u : r.R.Right - 12f * u - 10f * u;
                    DrawIcon(g, r.Icon, icx, r.R.Y + r.R.Height / 2f, r.Danger ? danger : MixC(th.Muted, th.Ink, r.Hv));
                    float tx = En ? icx + 10f * u + 12f * u : r.R.X + 14f * u;
                    float tr = En ? r.R.Right - 14f * u : icx - 10f * u - 12f * u;
                    using (SolidBrush tb = new SolidBrush(fg)) UiText(g, r.Label, uiFont, tb, new RectangleF(tx, r.R.Y, tr - tx, r.R.Height), fmtRN);
                    if (r.Key.Length > 0)
                        using (SolidBrush kb = new SolidBrush(th.Muted)) UiText(g, r.Key, uiSmall, kb, new RectangleF(tx, r.R.Y, tr - tx, r.R.Height), kf);
                }
        }

        void DrawCtx(Graphics g)
        {
            if (ctxT <= 0.01f || ctxRows.Count == 0) return;
            float ce = Ease(ctxT);
            if (ctxT > 0.995f) { DrawCtxCore(g); return; }
            DrawFaded(g, RectangleF.Inflate(ctxRect, 44f * ui, 44f * ui), ce, (ce - 1f) * 8f * ui, 0.96f + 0.04f * ce, delegate (Graphics gg) { DrawCtxCore(gg); });
        }
    }
}
