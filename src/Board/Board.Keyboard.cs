// Board: keyboard shortcuts.
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
        // ---------- keyboard ----------
        public bool HandleKey(KeyEventArgs e)
        {
            if (editItem != null) return false;
            if (splashActive) splashActive = false;
            Keys k = e.KeyCode;
            if (showHelp && (k == Keys.Escape)) { showHelp = false; Invalidate(); return true; }
            if (showAbout && k == Keys.Escape) { showAbout = false; Invalidate(); return true; }
            if (ctxOpen && k == Keys.Escape) { CloseCtx(); return true; }
            if (saveMenu && k == Keys.Escape) { saveMenu = false; Invalidate(); return true; }
            if (k == Keys.Space && !e.Control)
            {
                if (!spaceDown) { spaceDown = true; Cursor = Cursors.Hand; }
                return true;
            }
            if (e.Control)
            {
                switch (k)
                {
                    case Keys.Z: if (e.Shift) Redo(); else Undo(); return true;
                    case Keys.Y: Redo(); return true;
                    case Keys.S: SaveFile(e.Shift); return true;
                    case Keys.O: OpenDialog(); return true;
                    case Keys.N: NewBoard(); return true;
                    case Keys.D: Duplicate(); return true;
                    case Keys.A: SelectAll(); return true;
                    case Keys.C: Copy(); return true;
                    case Keys.X: Copy(); DeleteSel(); return true;
                    case Keys.V: Paste(); return true;
                    case Keys.E: ExportPng(); return true;
                    case Keys.I: InsertImages(); return true;
                    case Keys.F: return false;
                    case Keys.L: DoAction("laser"); return true;
                    case Keys.W: CloseTab(curTab); return true;
                    case Keys.R: RotateQuick(); return true;
                    case Keys.PageDown: SwitchTo((curTab + 1) % docs.Count); return true;
                    case Keys.PageUp: SwitchTo((curTab - 1 + docs.Count) % docs.Count); return true;
                    case Keys.D0: case Keys.NumPad0: ZoomReset(); return true;
                    case Keys.Oemplus: case Keys.Add: ZoomAt(Width / 2f, Height / 2f, 1.25f); return true;
                    case Keys.OemMinus: case Keys.Subtract: ZoomAt(Width / 2f, Height / 2f, 0.8f); return true;
                    case Keys.OemCloseBrackets: Reorder(true); return true;
                    case Keys.OemOpenBrackets: Reorder(false); return true;
                }
                return false;
            }
            switch (k)
            {
                case Keys.Delete: case Keys.Back: DeleteSel(); return true;
                case Keys.Escape: if (form.IsFullscreen) { form.SetFullscreen(false); return true; } sel.Clear(); Invalidate(); return true;
                case Keys.F11: ToggleFullscreen(); return true;
                case Keys.F1: showHelp = !showHelp; Invalidate(); return true;
                case Keys.OemQuestion: showHelp = !showHelp; Invalidate(); return true;
                case Keys.F: FitView(true); return true;
                case Keys.V: SetTool(Tool.Select); return true;
                case Keys.H: SetTool(Tool.Hand); return true;
                case Keys.P: SetTool(Tool.Pen); return true;
                case Keys.M: SetTool(Tool.Highlighter); return true;
                case Keys.E: SetTool(Tool.Eraser); return true;
                case Keys.L: SetTool(Tool.Line); return true;
                case Keys.A: SetTool(Tool.Arrow); return true;
                case Keys.R: SetTool(Tool.Rect); return true;
                case Keys.O: SetTool(Tool.Ellipse); return true;
                case Keys.G: SetTool(Tool.Triangle); return true;
                case Keys.S: SetTool(Tool.Star); return true;
                case Keys.T: SetTool(Tool.Text); return true;
                case Keys.N: SetTool(Tool.Note); return true;
            }
            return false;
        }

        public void HandleKeyUp(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space && spaceDown) { spaceDown = false; Cursor = CursorFor(tool); }
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            if (spaceDown) { spaceDown = false; Cursor = CursorFor(tool); }
        }


    }
}
