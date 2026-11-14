// Board: in-canvas text editing overlay.
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
        // ---------- text editing overlay ----------
        void StartEdit(Item it, bool isNew)
        {
            FinishEdit();
            it.Editing = true; editItem = it; editNew = isNew; editOld = it.Text; editOldStyle = it.Style; editRot = it.Rot; it.Rot = 0f;
            float z = vz;
            TextBox tb = new TextBox();
            tb.Multiline = true; tb.BorderStyle = BorderStyle.None; tb.AcceptsReturn = true; tb.AcceptsTab = false;
            tb.ScrollBars = ScrollBars.None; tb.Text = it.Text;
            tb.RightToLeft = ((isNew && !En) || HasArabic(it.Text)) ? RightToLeft.Yes : RightToLeft.No;
            if (it.K == Kind.Note)
            {
                tb.Font = new Font(NoteFamily, 18f * z, FontStyle.Regular, GraphicsUnit.Pixel);
                tb.BackColor = NoteBg[it.Bg % NoteBg.Length]; tb.ForeColor = Color.FromArgb(39, 39, 42);
                tb.WordWrap = true;
                tb.SetBounds((int)(it.X * z + vx + 14 * z), (int)(it.Y * z + vy + 14 * z), (int)((it.W - 28) * z), (int)((it.H - 28) * z));
            }
            else
            {
                tb.Font = new Font(TextFamily, it.Size * z, FS(it.Style), GraphicsUnit.Pixel);
                tb.BackColor = th.Bg; tb.ForeColor = Ink(it.Col);
                tb.WordWrap = false;
                tb.SetBounds((int)(it.X * z + vx) - 1, (int)(it.Y * z + vy), 40, 20);
                FitEditBox(tb, it);
            }
            tb.TextChanged += delegate { if (editItem != null && editItem.K == Kind.Text) FitEditBox(tb, editItem); };
            tb.KeyDown += delegate (object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape || (e.Control && e.KeyCode == Keys.Return)) { e.Handled = true; e.SuppressKeyPress = true; FinishEdit(); }
                else if (e.Control && !e.Alt && editItem != null && editItem.K == Kind.Text && (e.KeyCode == Keys.B || e.KeyCode == Keys.I || e.KeyCode == Keys.U))
                {
                    e.Handled = true; e.SuppressKeyPress = true;
                    ToggleTextStyle(e.KeyCode == Keys.B ? 1 : (e.KeyCode == Keys.I ? 2 : 4));
                }
            };
            tb.LostFocus += delegate { FinishEdit(); };
            edit = tb;
            Controls.Add(tb);
            tb.Focus();
            tb.SelectionStart = tb.Text.Length;
            ShowSymBar(tb);
            Invalidate();
        }

        void FitEditBox(TextBox tb, Item it)
        {
            string[] lines = tb.Text.Replace("\r\n", "\n").Split('\n');
            int w = 40;
            int padPx = tb.RightToLeft == RightToLeft.Yes ? (int)(12f * vz) : 24;
            foreach (string l in lines)
            {
                Size s = TextRenderer.MeasureText(l.Length == 0 ? " " : l, tb.Font, new Size(100000, 100000), TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
                if (s.Width + padPx > w) w = s.Width + padPx;
            }
            float lh = LineH(tb.Font, tb.Font.Size);
            tb.Width = w;
            tb.Height = (int)Math.Ceiling(lines.Length * lh) + 4;
        }

        void FinishEdit()
        {
            if (editItem == null) return;
            Item it = editItem; TextBox tb = edit; bool isNew = editNew; string old = editOld;
            editItem = null; edit = null;
            it.Text = tb.Text.Replace("\r\n", "\n");
            it.Editing = false; it.Rot = editRot;
            Controls.Remove(tb);
            BeginInvoke(new Action(delegate { tb.Dispose(); }));
            if (symBar != null) { SymbolBar sbr = symBar; symBar = null; Controls.Remove(sbr); BeginInvoke(new Action(delegate { sbr.Dispose(); })); }
            if (it.Text.Trim().Length == 0)
            {
                items.Remove(it); sel.Remove(it.Id);
                if (!isNew) Commit();
            }
            else if (isNew || it.Text != old || it.Style != editOldStyle) Commit();
            if (it.K == Kind.Text && items.Contains(it)) { sel.Clear(); sel.Add(it.Id); }   // stays selected so the B / I / U / S bar can still act on it
            Focus();
            Invalidate();
        }

        void CancelEditQuiet() { FinishEdit(); }

        // ---------- text formatting (bold / italic / underline / strikethrough) ----------
        // The text item the format bar acts on: the one being edited, or the single selected text item.
        Item FmtTarget()
        {
            if (editItem != null && editItem.K == Kind.Text) return editItem;
            if (mode != Mode.None || (tool != Tool.Select && tool != Tool.Text)) return null;
            Item s1 = SingleSel();
            return (s1 != null && s1.K == Kind.Text && !s1.Editing) ? s1 : null;
        }

        static int FmtBit(string id) { return id.EndsWith("b") ? 1 : (id.EndsWith("i") ? 2 : (id.EndsWith("u") ? 4 : 8)); }

        void ToggleTextStyle(int bit)
        {
            Item it = FmtTarget();
            if (it == null) return;
            it.Style ^= bit;
            it.TextSzKey = null;
            if (editItem == it && edit != null)
            {
                edit.Font = new Font(TextFamily, it.Size * vz, FS(it.Style), GraphicsUnit.Pixel);
                FitEditBox(edit, it);
            }
            else Commit();
            Invalidate();
        }

        // Makes the text bigger / smaller (the A- / A+ buttons of the format bar).
        void ChangeTextSize(float factor)
        {
            Item it = FmtTarget();
            if (it == null) return;
            float ns = (float)Math.Round(it.Size * factor);
            if (ns == it.Size) ns += factor > 1f ? 1f : -1f;
            it.Size = Clamp(ns, 10f, 400f);
            it.TextSzKey = null;
            if (editItem == it && edit != null)
            {
                edit.Font = new Font(TextFamily, it.Size * vz, FS(it.Style), GraphicsUnit.Pixel);
                FitEditBox(edit, it);
            }
            else Commit();
            Invalidate();
        }

        // The format bar goes BELOW the text (the rotate handle is above it). If there is no room below, it goes above the rotate handle.
        float FmtBarY(RectangleF sb, float ph, out bool below)
        {
            float u = ui, limit = Height - 14f * u - (34f * u + 12f * u) - 10f * u;     // top of the bottom bars
            float y = sb.Bottom + 22f * u;
            below = y + ph <= limit;
            if (!below) y = Math.Max(tabBar.Bottom + 8f * u, sb.Y - ph - 56f * u);
            return y;
        }

        // Layout of the small floating bar next to the text item: B I U S  A- A+.
        void BuildFmtBar(float W, float u, float m)
        {
            Item it = FmtTarget();
            if (it == null) return;
            RectangleF sb;
            if (editItem == it && edit != null) sb = new RectangleF(edit.Left, edit.Top, edit.Width, edit.Height);
            else { RectangleF b = BoundsOf(it); sb = new RectangleF(b.X * vz + vx, b.Y * vz + vy, b.Width * vz, b.Height * vz); }
            float bw = 34f * u, bh = 34f * u, gp = 2f * u, pad = 6f * u;
            float pw = pad * 2f + 6f * bw + 5f * gp, ph = pad * 2f + bh;
            float px = Clamp(sb.X + sb.Width / 2f - pw / 2f, m, Math.Max(m, W - m - pw));
            bool below;
            float py = FmtBarY(sb, ph, out below);
            panels.Add(new RectangleF(px, py, pw, ph));
            string[] id = new string[] { "fmt:b", "fmt:i", "fmt:u", "fmt:s", "fmt:-", "fmt:+" };
            string[] ic = new string[] { "fb", "fi", "fu", "fs", "fminus", "fplus" };
            string[] tp = new string[] { Loc.Tr("غامق (Ctrl+B)"), Loc.Tr("مائل (Ctrl+I)"), Loc.Tr("تسطير (Ctrl+U)"), Loc.Tr("يتوسطه خط"), Loc.Tr("تصغير الخط"), Loc.Tr("تكبير الخط") };
            for (int i = 0; i < 6; i++)
                AddBtn(id[i], px + pad + i * (bw + gp), py + pad, bw, bh, ic[i], null, tp[i], true, false);
        }


    }
}
