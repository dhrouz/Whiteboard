// Board: optional frame-time log. Start "Whiteboard.exe --perf" and the app writes perf.log next to the settings (%APPDATA%\Whiteboard).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace WhiteboardApp
{
    partial class Board : Control
    {
        public static bool PerfOn;                         // set by Program when started with --perf
        Dictionary<string, double[]> perf = new Dictionary<string, double[]>();   // tag -> { count, sum ms, max ms, frames over 16 ms }
        long perfFlushAt;
        Dictionary<string, int> perfWhy = new Dictionary<string, int>();         // who kept the app busy (ticks)

        void PerfWhy(string tag)
        {
            if (!PerfOn) return;
            int c; perfWhy.TryGetValue(tag, out c); perfWhy[tag] = c + 1;
        }

        long PerfStart() { return PerfOn ? Stopwatch.GetTimestamp() : 0L; }

        void PerfAdd(string tag, long t0)
        {
            if (!PerfOn) return;
            double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
            double[] a;
            if (!perf.TryGetValue(tag, out a)) { a = new double[4]; perf[tag] = a; }
            a[0] += 1; a[1] += ms; if (ms > a[2]) a[2] = ms; if (ms > 16.0) a[3] += 1;
            long now = Stopwatch.GetTimestamp();
            if (perfFlushAt == 0L) perfFlushAt = now + 3L * Stopwatch.Frequency;
            if (now >= perfFlushAt) { PerfFlush(); perfFlushAt = now + 3L * Stopwatch.Frequency; }
        }

        string PerfState()
        {
            if (helpT > 0.01f) return helpT > 0.995f ? "help-open" : "help-fade";
            if (aboutT > 0.01f) return aboutT > 0.995f ? "about-open" : "about-fade";
            if (ctxOpen) return "context-menu";
            return "board";
        }

        void PerfFlush()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("--- " + DateTime.Now.ToString("HH:mm:ss") + "  window " + Width + "x" + Height + "  ui scale " + ui.ToString("0.00") + "  lang " + (En ? "en" : "ar"));
                foreach (KeyValuePair<string, double[]> kv in perf)
                    sb.AppendLine(kv.Key.PadRight(22) + " frames " + ((int)kv.Value[0]).ToString().PadLeft(4) + "   avg " + (kv.Value[1] / kv.Value[0]).ToString("0.0").PadLeft(6)
                        + " ms   max " + kv.Value[2].ToString("0.0").PadLeft(6) + " ms   over 16ms: " + (int)kv.Value[3]);
                foreach (KeyValuePair<string, int> kv in perfWhy) sb.AppendLine("busy-by: " + kv.Key.PadRight(32) + " " + kv.Value + " ticks");
                perf.Clear(); perfWhy.Clear();
                File.AppendAllText(Path.Combine(DataDir(), "perf.log"), sb.ToString(), Encoding.UTF8);
            }
            catch (Exception) { }
        }
    }
}
