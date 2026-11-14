// Board (1/22): constants, theme palettes, state fields, constructor.
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
        // ---------- constants ----------
        static readonly Color[] Pal = new Color[] {
            Color.Empty, Color.FromArgb(239, 68, 68), Color.FromArgb(249, 115, 22), Color.FromArgb(234, 179, 8),
            Color.FromArgb(34, 197, 94), Color.FromArgb(6, 182, 212), Color.FromArgb(99, 102, 241), Color.FromArgb(217, 70, 239) };
        static readonly Color[] NoteBg = new Color[] {
            Color.FromArgb(253, 230, 138), Color.FromArgb(187, 247, 208), Color.FromArgb(191, 219, 254),
            Color.FromArgb(251, 207, 232), Color.FromArgb(254, 215, 170) };
        static readonly float[] Sizes = new float[] { 2f, 4f, 8f, 14f };
        static readonly float[] TextSizes = new float[] { 18f, 24f, 34f, 48f };
        const string UiFamily = "Segoe UI";
        const string TextFamily = "Segoe UI";
        const string NoteFamily = "Segoe UI";

        static readonly Graphics mg = Graphics.FromImage(new Bitmap(1, 1));
        static readonly Graphics mgUi = MakeMgUi();
        static Graphics MakeMgUi()
        {
            Graphics g = Graphics.FromImage(new Bitmap(1, 1));
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            return g;
        }
        static readonly StringFormat TypoFmt = MakeTypoFmt();
        static readonly StringFormat TypoRtl = MakeTypoRtl();
        static StringFormat MakeTypoRtl()
        {
            StringFormat f = (StringFormat)StringFormat.GenericTypographic.Clone();
            f.FormatFlags = f.FormatFlags | StringFormatFlags.MeasureTrailingSpaces | StringFormatFlags.NoWrap | StringFormatFlags.DirectionRightToLeft;
            f.Alignment = StringAlignment.Near;
            return f;
        }
        static bool HasArabic(string t)
        {
            if (t == null) return false;
            foreach (char c in t)
                if ((c >= '\u0600' && c <= '\u06FF') || (c >= '\u0750' && c <= '\u077F') || (c >= '\uFB50' && c <= '\uFDFF') || (c >= '\uFE70' && c <= '\uFEFF')) return true;
            return false;
        }
        static StringFormat MakeUiFmt(StringAlignment al, bool rtl)
        {
            StringFormat f = new StringFormat(StringFormat.GenericDefault);
            f.FormatFlags = StringFormatFlags.NoClip | StringFormatFlags.NoWrap | (rtl ? StringFormatFlags.DirectionRightToLeft : (StringFormatFlags)0);
            f.Alignment = al; f.LineAlignment = StringAlignment.Center; f.Trimming = StringTrimming.None;
            return f;
        }
        static StringFormat MakeTabFmt()
        {
            StringFormat f = MakeUiFmt(StringAlignment.Near, !Loc.En);
            f.Trimming = StringTrimming.EllipsisCharacter;
            return f;
        }
        static StringFormat MakeTypoFmt()
        {
            StringFormat f = (StringFormat)StringFormat.GenericTypographic.Clone();
            f.FormatFlags = f.FormatFlags | StringFormatFlags.MeasureTrailingSpaces | StringFormatFlags.NoWrap;
            return f;
        }

        static readonly Theme Light = new Theme {
            Dark = false, Bg = Color.FromArgb(247, 247, 248), Dot = Color.FromArgb(212, 212, 216), Panel = Color.FromArgb(255, 255, 255),
            Border = Color.FromArgb(228, 228, 231), Ink = Color.FromArgb(24, 24, 27), Muted = Color.FromArgb(113, 113, 122),
            Hover = Color.FromArgb(241, 241, 243), Accent = Color.FromArgb(99, 102, 241), AccentSoft = Color.FromArgb(224, 231, 255), TabBar = Color.FromArgb(232, 232, 238) };
        static readonly Theme Dark = new Theme {
            Dark = true, Bg = Color.FromArgb(24, 24, 27), Dot = Color.FromArgb(63, 63, 70), Panel = Color.FromArgb(39, 39, 42),
            Border = Color.FromArgb(63, 63, 70), Ink = Color.FromArgb(244, 244, 245), Muted = Color.FromArgb(161, 161, 170),
            Hover = Color.FromArgb(58, 58, 64), Accent = Color.FromArgb(129, 140, 248), AccentSoft = Color.FromArgb(49, 46, 129), TabBar = Color.FromArgb(12, 12, 14) };

        // ---------- state ----------
        MainForm form;
        List<Item> items = new List<Item>();
        HashSet<int> sel = new HashSet<int>();
        Dictionary<int, Bitmap> images = new Dictionary<int, Bitmap>();
        Dictionary<int, string> b64 = new Dictionary<int, string>();
        int[] customColors = null; int lastCustom = 0; bool hasCustom = false;
        List<string> undo = new List<string>();
        List<string> redo = new List<string>();
        int nextId = 1, nextImg = 1;
        Tool tool = Tool.Pen;
        int col = 0, sizeIdx = 1;
        bool grid = true, dirty = false, spaceDown = false, showHelp = false, needFit = true;
        Theme th = MakeLive();
        static Theme MakeLive() { Theme t = new Theme(); t.Set(Light); return t; }
        float vx, vy, vz = 1f, ui = 1f;
        string curPath = null, docName = Doc.DefaultName;
        string clip = null; int pasteN = 0;
        Random rnd = new Random();

        Mode mode = Mode.None;
        Item cur = null;
        PointF anchor, lastW, marqA;
        Point panStart; PointF panV;
        RectangleF marquee;
        bool moved = false, eraseChanged = false;
        Item clicked = null;
        int handle = -1; RectangleF rsOrig; Item rsItem;

        // UI
        List<Btn> btns = new List<Btn>();
        List<RectangleF> panels = new List<RectangleF>();
        List<RectangleF> seps = new List<RectangleF>();
        RectangleF chip;
        string chipText = "";
        string pressed = null, hover = null;
        DateTime hoverSince = DateTime.Now;
        bool tipVisible = false;
        string toastText = null; DateTime toastUntil = DateTime.MinValue; bool toastActive = false;
        Font uiFont, uiBold; float uiFontU = 0f;
        Timer ticker = new Timer();
        Timer autoTimer = new Timer();
        StringFormat fmtRN = MakeUiFmt(StringAlignment.Near, true);
        StringFormat fmtRC = MakeUiFmt(StringAlignment.Center, true);
        StringFormat fmtC = MakeUiFmt(StringAlignment.Center, false);
        List<KeyValuePair<string, RectangleF>> lbls = new List<KeyValuePair<string, RectangleF>>();
        bool compact = false;
        bool warmed = false, warmQueued = false;
        const float CM = 32f;
        Item snapRuler; int snapEdge; bool rulerLocked;
        string popKind = "save"; bool popSide;      // popSide: the popup sits beside the side menu (Math), not under a top button
        Item axItem; float axT = 0f; RectangleF axPanel;
        List<Btn> abtns = new List<Btn>(), obtns = new List<Btn>();
        List<KeyValuePair<string, RectangleF>> axTexts = new List<KeyValuePair<string, RectangleF>>(), axVals = new List<KeyValuePair<string, RectangleF>>();
        bool showAbout = false; float aboutT = 0f; RectangleF aboutCard, aboutIconRect;
        bool toolsPinned = false; float toolsT = 0f, toolsShift = 0f; bool toolsWant = false; double toolsLeave = 0.0;
        RectangleF toolsFull, toolsZone, toolsStrip, toolsGrip; Point mousePos = new Point(-1000, -1000);
        SymbolBar symBar;
        Font uiTitle;
        static readonly StringFormat NumFmt = MakeNumFmt(StringAlignment.Center);
        static readonly StringFormat NumFmtFar = MakeNumFmt(StringAlignment.Far);
        static StringFormat MakeNumFmt(StringAlignment al)
        {
            StringFormat f = (StringFormat)StringFormat.GenericTypographic.Clone();
            f.Alignment = al; f.LineAlignment = StringAlignment.Center;
            return f;
        }
        static Dictionary<string, MathFn> fnCache = new Dictionary<string, MathFn>();
        List<Doc> docs = new List<Doc>(); int curTab = 0; bool initLoading = false; int zoomPanelIdx = -1;
        List<RectangleF> tabRects = new List<RectangleF>(), tabClose = new List<RectangleF>();
        RectangleF tabBar; int hoverTab = -1; bool hoverClose = false;
        bool saveMenu = false; int menuHover = -1; RectangleF menuRect, menuCaret; List<RectangleF> menuRows = new List<RectangleF>();
        PointF rotCenter; double rotLast, rotTotal, rotApplied, rotBase;
        float editRot;
        bool splashActive = true, splashStarted = false; DateTime splashStart = DateTime.Now;
        Font uiSmall, uiSplash;
        System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
        double lastTick = 0.0;
        Dictionary<string, float> hovAmt = new Dictionary<string, float>(), actAmt = new Dictionary<string, float>(), pressAmt = new Dictionary<string, float>();
        float menuT = 0f, helpT = 0f, tipT = 0f, toastT = 0f, canvasFade = 0f, introT = 0f;
        float[] menuHov = new float[3];
        RectangleF indRect; bool indInit = false;
        float tgx, tgy, tgz; bool viewAnim = false;
        Theme thTarget = Light; bool themeAnim = false;
        Btn tipBtn;
        StringFormat fmtTab = MakeTabFmt();

        // text editing overlay
        TextBox edit; Item editItem; bool editNew; string editOld; int editOldStyle;

        Dictionary<string, Font> fonts = new Dictionary<string, Font>();

        public Board(MainForm f)
        {
            form = f;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            TabStop = true;
            AllowDrop = true;
            ticker.Interval = 60;
            ticker.Tick += delegate { OnTick(); };
            ticker.Start();
            autoTimer.Interval = 1500;
            autoTimer.Tick += delegate { autoTimer.Stop(); SaveAutosave(); };
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            using (Graphics g = CreateGraphics()) ui = g.DpiX / 96f;
            if (ui < 1f) ui = 1f;
        }


    }
}
