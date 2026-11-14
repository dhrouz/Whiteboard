// MainForm: window, fullscreen, key forwarding.
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
    class MainForm : Form
    {
        Board board;

        public MainForm(string openPath)
        {
            Text = Loc.Tr("السبورة");
            AutoScaleMode = AutoScaleMode.None;
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            Size = new Size(Math.Max(1040, (int)(wa.Width * 0.84)), Math.Max(660, (int)(wa.Height * 0.88)));
            MinimumSize = new Size(1000, 620);
            StartPosition = FormStartPosition.CenterScreen;
            KeyPreview = true;
            try { Icon = MakeIcon(); } catch (Exception) { }
            board = new Board(this);
            board.Dock = DockStyle.Fill;
            Controls.Add(board);
            board.Init(openPath);
        }

        static Icon MakeIcon()
        {
            using (Bitmap bmp = new Bitmap(32, 32))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (LinearGradientBrush br = new LinearGradientBrush(new Rectangle(0, 0, 32, 32), Color.FromArgb(99, 102, 241), Color.FromArgb(236, 72, 153), 45f))
                using (GraphicsPath gp = Board.RoundRect(new RectangleF(1, 1, 30, 30), 8))
                    g.FillPath(br, gp);
                using (Pen pen = new Pen(Color.White, 3.4f))
                {
                    pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round;
                    g.DrawLine(pen, 9, 23, 23, 9);
                }
                return Icon.FromHandle(bmp.GetHicon());
            }
        }

        bool fs; FormBorderStyle prevStyle; FormWindowState prevState; Rectangle prevBounds;
        public bool IsFullscreen { get { return fs; } }

        public void SetFullscreen(bool on)
        {
            if (on == fs) return;
            if (on)
            {
                prevState = WindowState; prevBounds = Bounds; prevStyle = FormBorderStyle;
                if (WindowState == FormWindowState.Maximized) WindowState = FormWindowState.Normal;
                FormBorderStyle = FormBorderStyle.None;
                WindowState = FormWindowState.Maximized;
                fs = true;
            }
            else
            {
                WindowState = FormWindowState.Normal;
                FormBorderStyle = prevStyle;
                Bounds = prevBounds;
                if (prevState == FormWindowState.Maximized) WindowState = FormWindowState.Maximized;
                fs = false;
            }
            board.Invalidate();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (board.HandleKey(e)) { e.Handled = true; e.SuppressKeyPress = true; }
            else base.OnKeyDown(e);
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            board.HandleKeyUp(e);
            base.OnKeyUp(e);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            board.PersistOnExit();
            base.OnFormClosing(e);
        }
    }
}
