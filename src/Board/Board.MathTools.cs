// Board: math objects (ruler, protractor, axes) and the axes side panel.
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
        // ---------- math items ----------
        void AddMathItem(Item it)
        {
            items.Add(it); sel.Clear(); sel.Add(it.Id);
            SetTool(Tool.Select);
            Commit();
        }

        void AddRuler()
        {
            PointF c = ViewCenter();
            Item it = NewItem(Kind.Ruler);
            it.W = 20f * CM; it.H = 56f; it.X = c.X - it.W / 2f; it.Y = c.Y - it.H / 2f;
            AddMathItem(it);
        }

        void AddProtractor()
        {
            PointF c = ViewCenter();
            Item it = NewItem(Kind.Protractor);
            it.W = 320f; it.H = 160f + 26f; it.X = c.X - it.W / 2f; it.Y = c.Y - it.H / 2f;
            AddMathItem(it);
        }

        void AddAxes()
        {
            PointF c = ViewCenter();
            Item it = NewItem(Kind.Axes);
            it.Ox = c.X; it.Oy = c.Y; it.Sw = Math.Max(2f, Sizes[sizeIdx] * 0.75f);
            AddMathItem(it);
        }

        static float NextStep(float cur, bool up)
        {
            float[] st = new float[] { 0.1f, 0.2f, 0.25f, 0.5f, 1f, 2f, 5f, 10f };
            int best = 0; float bd = float.MaxValue;
            for (int i = 0; i < st.Length; i++) { float d = Math.Abs(st[i] - cur); if (d < bd) { bd = d; best = i; } }
            best = Math.Max(0, Math.Min(st.Length - 1, best + (up ? 1 : -1)));
            return st[best];
        }

        string Prompt(string title, string msg, string okText)
        {
            float u = ui; string result = null;
            using (Form f = new Form())
            {
                f.AutoScaleMode = AutoScaleMode.None;
                f.Text = title;
                f.FormBorderStyle = FormBorderStyle.FixedDialog;
                f.StartPosition = FormStartPosition.CenterParent;
                f.MaximizeBox = false; f.MinimizeBox = false; f.ShowInTaskbar = false;
                int cw = (int)(470f * u), ch = (int)(204f * u);
                f.ClientSize = new Size(cw, ch);
                f.BackColor = th.Panel;
                Font font = new Font(UiFamily, 15f * u, FontStyle.Regular, GraphicsUnit.Pixel);
                Color ink = th.Ink;
                Rectangle tr = new Rectangle((int)(26f * u), (int)(20f * u), (int)(418f * u), (int)(50f * u));
                f.Paint += delegate (object sender, PaintEventArgs pe)
                {
                    TextRenderer.DrawText(pe.Graphics, msg, font, tr, ink, (En ? TextFormatFlags.Left : (TextFormatFlags.Right | TextFormatFlags.RightToLeft)) | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
                };
                TextBox tb = new TextBox();
                tb.Font = font; tb.BackColor = th.Bg; tb.ForeColor = th.Ink; tb.BorderStyle = BorderStyle.FixedSingle;
                tb.SetBounds((int)(26f * u), (int)(78f * u), (int)(418f * u), (int)(32f * u));
                f.Controls.Add(tb);
                int bw = (int)(112f * u), bh = (int)(38f * u), by = ch - bh - (int)(20f * u), right = cw - (int)(26f * u);
                Button b1 = MakeDlgButton(okText, right - bw, by, bw, bh, true, font);
                b1.DialogResult = DialogResult.OK; f.Controls.Add(b1); f.AcceptButton = b1;
                Button b2 = MakeDlgButton(Loc.Tr("إلغاء"), right - bw * 2 - (int)(10f * u), by, bw, bh, false, font);
                b2.DialogResult = DialogResult.Cancel; f.Controls.Add(b2); f.CancelButton = b2;
                f.Shown += delegate { tb.Focus(); };
                if (f.ShowDialog(FindForm()) == DialogResult.OK) result = tb.Text;
            }
            return result;
        }

        void AddAxesPoints(Item ax)
        {
            string txt = Prompt(Loc.Tr("إضافة نقطة"), Loc.Tr("اكتب الإحداثيات مثل: 3,4  (يمكنك كتابة عدة نقاط)"), Loc.Tr("إضافة"));
            if (txt == null) return;
            System.Text.RegularExpressions.MatchCollection mc = System.Text.RegularExpressions.Regex.Matches(txt, @"(-?\d+(?:\.\d+)?)\s*[,;\s]\s*(-?\d+(?:\.\d+)?)");
            int added = 0;
            foreach (System.Text.RegularExpressions.Match m in mc)
            {
                float px = float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), py = float.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
                ax.MPts.Add(new PointF(px, py)); added++;
            }
            if (added == 0) { Toast(Loc.Tr("صيغة غير صحيحة")); return; }
            Commit();
        }

        void AddAxesFunction(Item ax)
        {
            string txt = Prompt(Loc.Tr("رسم دالة"), Loc.Tr("اكتب الدالة مثل: 2x+1 أو x^2 أو sin(x)"), Loc.Tr("إضافة"));
            if (txt == null) return;
            txt = txt.Trim();
            if (ExprParser.Compile(txt) == null) { Toast(Loc.Tr("تعذّر فهم الدالة")); return; }
            ax.Funcs.Add(txt);
            Commit();
        }

        void AxesAction(string a)
        {
            Item it = axItem;
            if (it == null) return;
            bool ch = true;
            switch (a)
            {
                case "xmax+": it.XMax = Math.Min(60f, it.XMax + 1f); break;
                case "xmax-": it.XMax = Math.Max(1f, it.XMax - 1f); break;
                case "xmin+": it.XMin = Math.Max(-60f, it.XMin - 1f); break;
                case "xmin-": it.XMin = Math.Min(0f, it.XMin + 1f); break;
                case "ymax+": it.YMax = Math.Min(60f, it.YMax + 1f); break;
                case "ymax-": it.YMax = Math.Max(1f, it.YMax - 1f); break;
                case "ymin+": it.YMin = Math.Max(-60f, it.YMin - 1f); break;
                case "ymin-": it.YMin = Math.Min(0f, it.YMin + 1f); break;
                case "step+": it.Step = NextStep(it.Step, true); break;
                case "step-": it.Step = NextStep(it.Step, false); break;
                case "unit+": it.Unit = Math.Min(120f, (float)Math.Round(it.Unit * 1.2f)); break;
                case "unit-": it.Unit = Math.Max(10f, (float)Math.Round(it.Unit / 1.2f)); break;
                case "tgrid": it.Flags ^= 1; break;
                case "tnum": it.Flags ^= 2; break;
                case "tlab": it.Flags ^= 4; break;
                case "pt": ch = false; AddAxesPoints(it); break;
                case "fn": ch = false; AddAxesFunction(it); break;
                case "clr": it.MPts.Clear(); it.Funcs.Clear(); break;
                default: ch = false; break;
            }
            if (ch) Commit();
            Invalidate();
        }

        void AddAbtn(string id, float x, float y, float w, float h, string icon, string label, string tip)
        {
            Btn b = new Btn();
            b.Id = id; b.R = new RectangleF(x, y, w, h); b.Icon = icon; b.Label = label; b.Tip = tip;
            abtns.Add(b);
        }

        void BuildAxesPanel(float W, float H, float u, float m, float topY, float barH)
        {
            abtns.Clear(); axTexts.Clear(); axVals.Clear();
            Item it = axItem;
            if (it == null || axT <= 0.01f) { axPanel = RectangleF.Empty; return; }
            float pw = 250f * u, pad = 10f * u;
            float yTop = topY + barH + 12f * u, yBot = H - m - barH - 14f * u;
            float avail = Math.Max(120f * u, yBot - yTop);
            float stH = 38f * u, titleH = 30f * u, tgH = 34f * u, acH = 36f * u, gap = 5f * u;
            float need = pad + titleH + 6f * (stH + gap) + 8f * u + tgH + 8f * u + 3f * (acH + gap) + pad;
            float k = Math.Min(1f, avail / need);
            if (k < 0.72f) k = 0.72f;
            stH *= k; titleH *= k; tgH *= k; acH *= k; gap *= k;
            float ph = pad + titleH + 6f * (stH + gap) + 8f * u + tgH + 8f * u + 3f * (acH + gap) + pad;
            float px = En ? W - m - pw : m;
            float py = Math.Max(yTop - 6f * u, yTop + (avail - ph) / 2f);
            axPanel = new RectangleF(px, py, pw, ph);
            float y = py + pad;
            axTexts.Add(new KeyValuePair<string, RectangleF>("T:" + Loc.Tr("لوحة المحور"), new RectangleF(px + pad, y, pw - 2f * pad, titleH)));
            y += titleH;
            string[] sid = new string[] { "xmax", "xmin", "ymax", "ymin", "step", "unit" };
            string[] slb = new string[] { Loc.Tr("امتداد x الموجب"), Loc.Tr("امتداد x السالب"), Loc.Tr("امتداد y الموجب"), Loc.Tr("امتداد y السالب"), Loc.Tr("الخطوة"), Loc.Tr("حجم الوحدة") };
            string[] sv = new string[] { NumStr(it.XMax), NumStr(it.XMin), NumStr(it.YMax), NumStr(it.YMin), NumStr(it.Step), NumStr(it.Unit) };
            float bw = 32f * u, vw = 48f * u, ctl = bw * 2f + vw;
            for (int i = 0; i < 6; i++)
            {
                float cx0 = En ? px + pw - pad - ctl : px + pad;
                RectangleF lab = En
                    ? new RectangleF(px + pad, y, pw - 2f * pad - ctl - 6f * u, stH)
                    : new RectangleF(px + pad + ctl + 6f * u, y, pw - 2f * pad - ctl - 6f * u, stH);
                axTexts.Add(new KeyValuePair<string, RectangleF>(slb[i], lab));
                float ch = Math.Min(30f * u, stH - 4f), cy0 = y + (stH - ch) / 2f;
                AddAbtn("ax:" + sid[i] + "-", cx0, cy0, bw, ch, "zout", null, Loc.Tr("إنقاص"));
                axVals.Add(new KeyValuePair<string, RectangleF>(sv[i], new RectangleF(cx0 + bw, cy0, vw, ch)));
                AddAbtn("ax:" + sid[i] + "+", cx0 + bw + vw, cy0, bw, ch, "zin", null, Loc.Tr("زيادة"));
                y += stH + gap;
            }
            y += 8f * u;
            float tw3 = (pw - 2f * pad - 2f * gap) / 3f;
            string[] tid = new string[] { "tgrid", "tnum", "tlab" };
            string[] tlb = new string[] { Loc.Tr("الشبكة"), Loc.Tr("الأرقام"), Loc.Tr("التسميات") };
            for (int i = 0; i < 3; i++)
            {
                int idx = En ? i : 2 - i;
                AddAbtn("ax:" + tid[i], px + pad + idx * (tw3 + gap), y, tw3, tgH, null, tlb[i], tlb[i]);
            }
            y += tgH + 8f * u;
            string[] aid2 = new string[] { "pt", "fn", "clr" };
            string[] alb2 = new string[] { Loc.Tr("إضافة نقطة"), Loc.Tr("رسم دالة"), Loc.Tr("مسح النقاط والدوال") };
            string[] aic = new string[] { "zin", "func", "clear" };
            for (int i = 0; i < 3; i++)
            {
                AddAbtn("ax:" + aid2[i], px + pad, y, pw - 2f * pad, acH, aic[i], alb2[i], alb2[i]);
                y += acH + gap;
            }
        }

        void DrawAxesPanelCore(Graphics g)
        {
            DrawPanel(g, axPanel);
            using (SolidBrush ink = new SolidBrush(th.Ink))
            using (SolidBrush mut = new SolidBrush(th.Muted))
            {
                foreach (KeyValuePair<string, RectangleF> kv in axTexts)
                {
                    if (kv.Key.StartsWith("T:")) UiText(g, kv.Key.Substring(2), uiBold, ink, kv.Value, fmtRN);
                    else UiText(g, kv.Key, uiSmall, mut, kv.Value, fmtRN);
                }
                foreach (KeyValuePair<string, RectangleF> kv2 in axVals) UiText(g, kv2.Key, uiBold, ink, kv2.Value, fmtC);
            }
            foreach (Btn b in abtns) DrawBtn(g, b);
        }

        void DrawAxesPanel(Graphics g)
        {
            if (axT <= 0.01f || axPanel.Width < 1f) return;
            if (axT > 0.995f) { DrawAxesPanelCore(g); return; }
            float e2 = Ease(axT);
            RectangleF area = RectangleF.Inflate(axPanel, 44f * ui, 44f * ui);
            DrawFaded(g, area, e2, (1f - e2) * 10f * ui, 0.97f + 0.03f * e2, delegate (Graphics gg) { DrawAxesPanelCore(gg); });
        }


    }
}
