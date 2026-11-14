// Program: entry point.
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
    static class Program
    {
        [DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();

        [STAThread]
        static void Main(string[] args)
        {
            try { SetProcessDPIAware(); } catch (Exception) { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            string openPath = null;
            foreach (string a in args)
            {
                if (a == "--perf") Board.PerfOn = true;      // writes perf.log (frame times) - see Board.PerfLog.cs
                else if (openPath == null) openPath = a;
            }
            Application.Run(new MainForm(openPath));
        }
    }
}
