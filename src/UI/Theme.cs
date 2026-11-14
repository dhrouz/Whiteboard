// Theme: colour set with animated cross-fade support.
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
    class Theme
    {
        public bool Dark;
        public Color Bg, Dot, Panel, Border, Ink, Muted, Hover, Accent, AccentSoft, TabBar;

        public void Set(Theme o)
        {
            Dark = o.Dark; Bg = o.Bg; Dot = o.Dot; Panel = o.Panel; Border = o.Border; Ink = o.Ink;
            Muted = o.Muted; Hover = o.Hover; Accent = o.Accent; AccentSoft = o.AccentSoft; TabBar = o.TabBar;
        }

        static Color Move(Color a, Color b, float t, ref bool moving)
        {
            if (Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B) < 3) return b;
            moving = true;
            return Color.FromArgb(255, (int)Math.Round(a.R + (b.R - a.R) * t), (int)Math.Round(a.G + (b.G - a.G) * t), (int)Math.Round(a.B + (b.B - a.B) * t));
        }

        // Moves every color a step toward the target theme; returns true while still moving.
        public bool Step(Theme o, float t)
        {
            bool mv = false;
            Bg = Move(Bg, o.Bg, t, ref mv); Dot = Move(Dot, o.Dot, t, ref mv); Panel = Move(Panel, o.Panel, t, ref mv);
            Border = Move(Border, o.Border, t, ref mv); Ink = Move(Ink, o.Ink, t, ref mv); Muted = Move(Muted, o.Muted, t, ref mv);
            Hover = Move(Hover, o.Hover, t, ref mv); Accent = Move(Accent, o.Accent, t, ref mv);
            AccentSoft = Move(AccentSoft, o.AccentSoft, t, ref mv); TabBar = Move(TabBar, o.TabBar, t, ref mv);
            return mv;
        }
    }
}
