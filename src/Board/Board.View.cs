// Board: zoom/fit, tools, colours, DoAction.
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
        // ---------- view ----------
        void ZoomAt(float px, float py, float f)
        {
            float bx = viewAnim ? tgx : vx, by = viewAnim ? tgy : vy, bz = viewAnim ? tgz : vz;
            float nz = Clamp(bz * f, 0.1f, 8f), k = nz / bz;
            tgx = px - (px - bx) * k; tgy = py - (py - by) * k; tgz = nz;
            viewAnim = true;
            Invalidate();
        }

        void ZoomReset() { float bz = viewAnim ? tgz : vz; ZoomAt(Width / 2f, Height / 2f, 1f / bz); }

        void FitView(bool animate = false)
        {
            float nz, nx, ny;
            if (items.Count == 0) { nz = 1f; nx = Width / 2f; ny = Height / 2f; }
            else
            {
                RectangleF u = UnionBounds(items);
                float z = Math.Min((Width - 260f * ui) / Math.Max(u.Width, 1f), (Height - 220f * ui) / Math.Max(u.Height, 1f));
                nz = Clamp(z, 0.1f, 2f);
                nx = Width / 2f - (u.X + u.Width / 2f) * nz;
                ny = Height / 2f - (u.Y + u.Height / 2f) * nz;
            }
            if (animate) { tgx = nx; tgy = ny; tgz = nz; viewAnim = true; }
            else { viewAnim = false; vz = nz; vx = nx; vy = ny; }
            Invalidate();
        }

        void SetTool(Tool t)
        {
            FinishEdit();
            tool = t;
            if (t != Tool.Select) sel.Clear();
            Cursor = CursorFor(t);
            Invalidate();
        }

        Cursor CursorFor(Tool t)
        {
            switch (t)
            {
                case Tool.Select: return Cursors.Default;
                case Tool.Hand: return Cursors.Hand;
                case Tool.Text: return Cursors.IBeam;
                default: return Cursors.Cross;
            }
        }

        // ---------- colors ----------
        void SetColor(int c)
        {
            col = c;
            bool ch = false;
            foreach (Item it in items)
                if (sel.Contains(it.Id) && it.K != Kind.Note && it.K != Kind.Image) { it.Col = col; ch = true; }
            if (ch) Commit(); else Invalidate();
        }

        void PickCustomColor()
        {
            using (ColorDialog cd = new ColorDialog())
            {
                cd.FullOpen = true; cd.AnyColor = true;
                if (customColors != null) cd.CustomColors = customColors;
                cd.Color = col >= 1000 ? Ink(col) : (hasCustom ? Color.FromArgb(255, Color.FromArgb(lastCustom)) : Color.FromArgb(255, 99, 102, 241));
                if (cd.ShowDialog(FindForm()) != DialogResult.OK) return;
                customColors = cd.CustomColors;
                lastCustom = cd.Color.ToArgb() & 0xFFFFFF; hasCustom = true;
                SetColor(1000 + lastCustom);
            }
        }

        // ---------- actions ----------
        void DoAction(string id)
        {
            if (id == "fmt:+") { ChangeTextSize(1.15f); return; }
            if (id == "fmt:-") { ChangeTextSize(1f / 1.15f); return; }
            if (id.StartsWith("fmt:")) { ToggleTextStyle(FmtBit(id)); return; }
            if (id.StartsWith("t:"))
            {
                SetTool((Tool)Enum.Parse(typeof(Tool), id.Substring(2), true));
                return;
            }
            if (id.StartsWith("c:")) { SetColor(int.Parse(id.Substring(2))); return; }
            if (id == "cc") { PickCustomColor(); return; }
            if (id.StartsWith("ax:")) { AxesAction(id.Substring(3)); return; }
            if (id.StartsWith("ab:")) { AboutAction(id.Substring(3)); return; }
            if (id.StartsWith("s:"))
            {
                sizeIdx = int.Parse(id.Substring(2));
                bool ch = false;
                foreach (Item it in items)
                {
                    if (!sel.Contains(it.Id)) continue;
                    if (it.K == Kind.Stroke) { it.Sw = it.Alpha < 1f ? Sizes[sizeIdx] * 3f + 10f : Sizes[sizeIdx]; ch = true; }
                    else if (it.K == Kind.Line || it.K == Kind.Arrow || it.K == Kind.Rect || it.K == Kind.Ellipse || it.K == Kind.Triangle || it.K == Kind.Star || it.K == Kind.Axes) { it.Sw = Sizes[sizeIdx]; ch = true; }
                    else if (it.K == Kind.Text) { it.Size = TextSizes[sizeIdx]; ch = true; }
                }
                if (ch) Commit(); else Invalidate();
                return;
            }
            switch (id)
            {
                case "grid": grid = !grid; Invalidate(); break;
                case "laser": laserOn = !laserOn; if (laserOn) { laserIn = true; laserPos = mousePos; } Invalidate(); break;
                case "theme": StartThemeSwitch(); break;
                case "undo": Undo(); break;
                case "redo": Redo(); break;
                case "clear":
                    if (items.Count > 0 && Ask(Loc.Tr("هل تريد مسح اللوحة بالكامل؟"), Loc.Tr("مسح"), Loc.Tr("إلغاء")))
                    { items.Clear(); sel.Clear(); Commit(); }
                    break;
                case "zin": ZoomAt(Width / 2f, Height / 2f, 1.25f); break;
                case "zout": ZoomAt(Width / 2f, Height / 2f, 0.8f); break;
                case "zreset": ZoomReset(); break;
                case "zfit": ToggleFullscreen(); break;
                case "new": case "newtab": NewBoard(); break;
                case "open": OpenDialog(); break;
                case "save": TogglePopup("save"); break;
                case "math": TogglePopup("math"); break;
                case "about": showAbout = !showAbout; if (showAbout) { saveMenu = false; showHelp = false; } Invalidate(); break;
                case "pin": toolsPinned = !toolsPinned; Invalidate(); break;
                case "image": InsertImages(); break;
                case "png": ExportPng(); break;
                case "help": showHelp = !showHelp; Invalidate(); break;
                case "lang": ToggleLang(); break;
            }
        }


    }
}
