// Board: laser pointer - a soft round spotlight (like the cursor highlight in OBS) that follows the mouse, so students can see where the teacher is pointing.
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
        bool laserOn = true;                         // user option (button / Ctrl+L), saved in settings.txt
        bool laserIn;                                // mouse is inside the window
        float laserA;                                // 0 or 1 (no fade)
        PointF laserPos; RectangleF laserRect = RectangleF.Empty;     // laserRect = where it was last painted

        RectangleF laserPend = RectangleF.Empty;       // area waiting to be repainted by the next tick
        void LaserDirty(RectangleF r) { laserPend = laserPend.IsEmpty ? r : RectangleF.Union(laserPend, r); }

        RectangleF LaserArea(PointF p) { float r = 26f * ui; return new RectangleF(p.X - r, p.Y - r, r * 2f, r * 2f); }

        bool LaserVisibleNow()
        {
            return laserOn && laserIn && !splashActive && !showHelp && !showAbout;
        }

        void LaserMove(Point p)
        {
            if (!laserOn) return;
            laserIn = true;
            laserPos = new PointF(p.X, p.Y);
            RectangleF now = LaserArea(laserPos);
            // only the old and the new spot are repainted, and at most once per animation tick (not once per mouse event)
            if (LaserVisibleNow()) { LaserDirty(laserRect.IsEmpty ? now : RectangleF.Union(laserRect, now)); Wake(); }
            laserRect = now;
        }

        void LaserLeave() { laserIn = false; Wake(); }

        // Called from OnTick. Returns the area the laser needs repainted (Empty = nothing). No animation: it appears / disappears at once.
        RectangleF LaserTick(float dt)
        {
            float t = LaserVisibleNow() ? 1f : 0f;
            if (laserA != t)
            {
                laserA = t;
                PerfWhy("laser toggle");
                LaserDirty(laserRect.IsEmpty ? LaserArea(laserPos) : laserRect);
            }
            RectangleF r = laserPend;
            laserPend = RectangleF.Empty;
            return r;
        }

        // Screen-space overlay, drawn last: a translucent round spotlight with a thin ring - no trail, no dot.
        // Red on both themes (brighter on the dark board, deeper on the light board).
        void DrawLaser(Graphics g)
        {
            if (laserA <= 0.01f) return;
            float u = ui, x = laserPos.X, y = laserPos.Y, R = 22f * u;
            Color core = th.Dark ? Color.FromArgb(255, 84, 84) : Color.FromArgb(226, 22, 44);
            using (SolidBrush fb = new SolidBrush(Color.FromArgb(105, core))) g.FillEllipse(fb, x - R, y - R, R * 2f, R * 2f);
            using (Pen rp = new Pen(Color.FromArgb(170, core), 1.6f * u)) g.DrawEllipse(rp, x - R, y - R, R * 2f, R * 2f);
        }
    }
}
