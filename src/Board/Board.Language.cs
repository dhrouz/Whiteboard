// Board: language toggle, UI mirroring, dialogs.
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
        // ---------- language ----------
        static bool En { get { return Loc.En; } }

        string DispName(string n) { return n == Doc.DefaultName ? (En ? "Untitled board" : Doc.DefaultName) : n; }

        void ApplyLang()
        {
            fmtRN = MakeUiFmt(StringAlignment.Near, !En);
            fmtRC = MakeUiFmt(StringAlignment.Center, !En);
            fmtTab = MakeTabFmt();
        }

        void ToggleLang()
        {
            Loc.En = !Loc.En;
            baseDirty = true;
            ApplyLang();
            saveMenu = false; tipVisible = false; hover = null;
            UpdateTitle();
            Invalidate();
            Toast(En ? "Language: English" : Loc.Tr("اللغة: العربية"));
        }

        static RectangleF Mir(RectangleF r, float W) { return new RectangleF(W - r.X - r.Width, r.Y, r.Width, r.Height); }

        // The layout is built right-to-left; for English it is mirrored left-to-right.
        void MirrorUi(float W)
        {
            float zdx = 0f;
            for (int i = 0; i < panels.Count; i++)
            {
                RectangleF mp = Mir(panels[i], W);
                if (i == zoomPanelIdx) zdx = mp.X - panels[i].X;
                panels[i] = mp;
            }
            for (int i = 0; i < seps.Count; i++) seps[i] = Mir(seps[i], W);
            for (int i = 0; i < lbls.Count; i++) lbls[i] = new KeyValuePair<string, RectangleF>(lbls[i].Key, Mir(lbls[i].Value, W));
            foreach (Btn bt in btns)
            {
                if (bt.Id == "zout" || bt.Id == "zreset" || bt.Id == "zin" || bt.Id == "zfit")
                    bt.R = new RectangleF(bt.R.X + zdx, bt.R.Y, bt.R.Width, bt.R.Height);
                else bt.R = Mir(bt.R, W);
            }
            for (int i = 0; i < tabRects.Count; i++) { tabRects[i] = Mir(tabRects[i], W); tabClose[i] = Mir(tabClose[i], W); }
            menuRect = Mir(menuRect, W); menuCaret = Mir(menuCaret, W);
            for (int i = 0; i < menuRows.Count; i++) menuRows[i] = Mir(menuRows[i], W);
        }

        // ---------- Arabic dialogs ----------
        Button MakeDlgButton(string t, int x, int y, int w, int h, bool primary, Font font)
        {
            Button b = new Button();
            b.Text = t; b.SetBounds(x, y, w, h); b.Font = font;
            b.FlatStyle = FlatStyle.Flat; b.UseVisualStyleBackColor = false;
            b.BackColor = primary ? th.Accent : th.Hover; b.ForeColor = primary ? Color.White : th.Ink;
            b.FlatAppearance.BorderSize = 0;
            return b;
        }

        bool Ask(string text, string yes, string no)
        {
            float u = ui;
            using (Form f = new Form())
            {
                f.AutoScaleMode = AutoScaleMode.None;
                f.Text = Loc.Tr("السبورة");
                f.FormBorderStyle = FormBorderStyle.FixedDialog;
                f.StartPosition = FormStartPosition.CenterParent;
                f.MaximizeBox = false; f.MinimizeBox = false; f.ShowInTaskbar = false;
                int cw = (int)(440f * u), ch = (int)(168f * u);
                f.ClientSize = new Size(cw, ch);
                f.BackColor = th.Panel;
                Font font = new Font(UiFamily, 15f * u, FontStyle.Regular, GraphicsUnit.Pixel);
                Color ink = th.Ink;
                Rectangle tr = new Rectangle((int)(28f * u), (int)(26f * u), (int)(384f * u), (int)(80f * u));
                f.Paint += delegate (object sender, PaintEventArgs pe)
                {
                    TextRenderer.DrawText(pe.Graphics, text, font, tr, ink, (En ? TextFormatFlags.Left : (TextFormatFlags.Right | TextFormatFlags.RightToLeft)) | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
                };
                int bw = (int)(112f * u), bh = (int)(38f * u), by = ch - bh - (int)(20f * u);
                int right = cw - (int)(28f * u);
                Button b1 = MakeDlgButton(yes, right - bw, by, bw, bh, true, font);
                b1.DialogResult = DialogResult.OK; f.Controls.Add(b1); f.AcceptButton = b1;
                if (no != null)
                {
                    Button b2 = MakeDlgButton(no, right - bw * 2 - (int)(10f * u), by, bw, bh, false, font);
                    b2.DialogResult = DialogResult.Cancel; f.Controls.Add(b2); f.CancelButton = b2;
                }
                else f.CancelButton = b1;
                return f.ShowDialog(FindForm()) == DialogResult.OK;
            }
        }

        void Info(string text) { Ask(text, Loc.Tr("حسناً"), null); }


    }
}
