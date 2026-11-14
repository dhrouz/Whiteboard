// Board: .wboard format, undo/redo history.
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
        // ---------- serialization ----------
        static Kind KindOfCode(string c)
        {
            switch (c)
            {
                case "R": return Kind.Rect;
                case "E": return Kind.Ellipse;
                case "Y": return Kind.Triangle;
                case "Z": return Kind.Star;
                case "U": return Kind.Ruler;
                default: return Kind.Protractor;
            }
        }

        static string Esc(string s) { return (s ?? "").Replace("\\", "\\\\").Replace("\r", "").Replace("\n", "\\n").Replace("\t", "\\t"); }
        static string Unesc(string s)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\\' && i + 1 < s.Length) { char n = s[++i]; sb.Append(n == 'n' ? '\n' : (n == 't' ? '\t' : n)); }
                else sb.Append(c);
            }
            return sb.ToString();
        }
        static void Row(StringBuilder sb, params object[] f)
        {
            for (int i = 0; i < f.Length; i++)
            {
                if (i > 0) sb.Append('\t');
                object o = f[i];
                if (o is float) sb.Append(F((float)o)); else if (o is int) sb.Append(((int)o).ToString(CultureInfo.InvariantCulture)); else sb.Append(o);
            }
            sb.Append('\n');
        }

        static int IntP(string s) { return int.Parse(s.Trim(), CultureInfo.InvariantCulture); }
        int parseBad; string parseBadMsg;   // lines of the last Parse() that could not be read (reported when a file is opened)

        string Serialize(IEnumerable<Item> list, bool withImages, string name) { return SerializeDoc(list, withImages, name, images, b64); }

        string SerializeDoc(IEnumerable<Item> list, bool withImages, string name, Dictionary<int, Bitmap> imgs, Dictionary<int, string> cache)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("WBOARD1\n");
            if (name != null) Row(sb, "NAME", Esc(name));
            HashSet<int> used = new HashSet<int>();
            foreach (Item it in list)
            {
                switch (it.K)
                {
                    case Kind.Stroke:
                        {
                            StringBuilder ps = new StringBuilder();
                            for (int i = 0; i < it.Pts.Count; i++)
                            {
                                if (i > 0) ps.Append(';');
                                ps.Append(F(it.Pts[i].X)).Append(',').Append(F(it.Pts[i].Y));
                            }
                            Row(sb, "S", it.Id, it.Col, it.Sw, it.Alpha, ps.ToString());
                            break;
                        }
                    case Kind.Line: Row(sb, "L", it.Id, it.Col, it.Sw, it.X, it.Y, it.X2, it.Y2); break;
                    case Kind.Arrow: Row(sb, "A", it.Id, it.Col, it.Sw, it.X, it.Y, it.X2, it.Y2); break;
                    case Kind.Rect: Row(sb, "R", it.Id, it.Col, it.Sw, it.X, it.Y, it.W, it.H, it.Rot); break;
                    case Kind.Ellipse: Row(sb, "E", it.Id, it.Col, it.Sw, it.X, it.Y, it.W, it.H, it.Rot); break;
                    case Kind.Triangle: Row(sb, "Y", it.Id, it.Col, it.Sw, it.X, it.Y, it.W, it.H, it.Rot); break;
                    case Kind.Star: Row(sb, "Z", it.Id, it.Col, it.Sw, it.X, it.Y, it.W, it.H, it.Rot); break;
                    case Kind.Ruler: Row(sb, "U", it.Id, it.Col, it.Sw, it.X, it.Y, it.W, it.H, it.Rot); break;
                    case Kind.Protractor: Row(sb, "P", it.Id, it.Col, it.Sw, it.X, it.Y, it.W, it.H, it.Rot); break;
                    case Kind.Axes:
                        {
                            StringBuilder mp = new StringBuilder();
                            for (int i = 0; i < it.MPts.Count; i++)
                            {
                                if (i > 0) mp.Append(';');
                                mp.Append(F(it.MPts[i].X)).Append(',').Append(F(it.MPts[i].Y));
                            }
                            StringBuilder fs2 = new StringBuilder();
                            for (int i = 0; i < it.Funcs.Count; i++)
                            {
                                if (i > 0) fs2.Append('\u001F');
                                fs2.Append(Esc(it.Funcs[i]));
                            }
                            Row(sb, "X", it.Id, it.Col, it.Sw, it.Ox, it.Oy, it.XMin, it.XMax, it.YMin, it.YMax, it.Unit, it.Step, it.Flags, mp.ToString(), fs2.ToString());
                            break;
                        }
                    case Kind.Text: Row(sb, "T", it.Id, it.Col, it.Size, it.X, it.Y, Esc(it.Text), it.Rot, it.Style); break;
                    case Kind.Note: Row(sb, "N", it.Id, it.Bg, it.X, it.Y, it.W, it.H, Esc(it.Text), it.Rot); break;
                    case Kind.Image: Row(sb, "I", it.Id, it.X, it.Y, it.W, it.H, it.ImgId, it.Rot); used.Add(it.ImgId); break;
                }
            }
            if (withImages)
            {
                foreach (int id in used)
                {
                    Bitmap bmp;
                    if (!imgs.TryGetValue(id, out bmp)) continue;
                    string data;
                    if (!cache.TryGetValue(id, out data))
                    {
                        using (MemoryStream ms = new MemoryStream())
                        {
                            bmp.Save(ms, ImageFormat.Png);
                            data = Convert.ToBase64String(ms.ToArray());
                        }
                        cache[id] = data;
                    }
                    Row(sb, "IMG", id, data);
                }
            }
            return sb.ToString();
        }

        // Parses text into items. If imgOut is non-null, embedded images are loaded into it.
        List<Item> Parse(string text, Dictionary<int, Bitmap> imgOut, out string name)
        {
            name = null; parseBad = 0; parseBadMsg = null;
            List<Item> res = new List<Item>();
            string[] lines = text.Split('\n');
            if (lines.Length == 0 || lines[0].Trim() != "WBOARD1") throw new InvalidDataException("Not a whiteboard file");
            for (int li = 1; li < lines.Length; li++)
            {
                string line = lines[li].TrimEnd('\r');
                if (line.Length == 0) continue;
                string[] f = line.Split('\t');
                try
                {
                    Item it = null;
                    switch (f[0])
                    {
                        case "NAME": name = Unesc(f[1]); break;
                        case "IMG":
                            if (imgOut != null)
                            {
                                byte[] bytes = Convert.FromBase64String(f[2]);
                                using (MemoryStream ms = new MemoryStream(bytes))
                                using (Image tmp = Image.FromStream(ms))
                                    imgOut[IntP(f[1])] = new Bitmap(tmp);
                            }
                            break;
                        case "S":
                            it = new Item(); it.K = Kind.Stroke; it.Id = IntP(f[1]); it.Col = IntP(f[2]); it.Sw = Pf(f[3]); it.Alpha = Pf(f[4]);
                            foreach (string pr in f[5].Split(';'))
                            {
                                if (pr.Length == 0) continue;
                                string[] xy = pr.Split(',');
                                it.Pts.Add(new PointF(Pf(xy[0]), Pf(xy[1])));
                            }
                            if (it.Pts.Count == 0) it = null;
                            break;
                        case "L":
                        case "A":
                            it = new Item(); it.K = f[0] == "L" ? Kind.Line : Kind.Arrow; it.Id = IntP(f[1]); it.Col = IntP(f[2]); it.Sw = Pf(f[3]);
                            it.X = Pf(f[4]); it.Y = Pf(f[5]); it.X2 = Pf(f[6]); it.Y2 = Pf(f[7]);
                            break;
                        case "R":
                        case "E":
                        case "Y":
                        case "Z":
                        case "U":
                        case "P":
                            it = new Item(); it.K = KindOfCode(f[0]); it.Id = IntP(f[1]); it.Col = IntP(f[2]); it.Sw = Pf(f[3]);
                            it.X = Pf(f[4]); it.Y = Pf(f[5]); it.W = Pf(f[6]); it.H = Pf(f[7]);
                            if (f.Length > 8) it.Rot = Pf(f[8]);
                            break;
                        case "X":
                            it = new Item(); it.K = Kind.Axes; it.Id = IntP(f[1]); it.Col = IntP(f[2]); it.Sw = Pf(f[3]);
                            it.Ox = Pf(f[4]); it.Oy = Pf(f[5]); it.XMin = Pf(f[6]); it.XMax = Pf(f[7]); it.YMin = Pf(f[8]); it.YMax = Pf(f[9]);
                            it.Unit = Pf(f[10]); it.Step = Pf(f[11]); it.Flags = IntP(f[12]);
                            if (f.Length > 13) foreach (string pr2 in f[13].Split(';'))
                            {
                                if (pr2.Length == 0) continue;
                                string[] xy2 = pr2.Split(',');
                                it.MPts.Add(new PointF(Pf(xy2[0]), Pf(xy2[1])));
                            }
                            if (f.Length > 14 && f[14].Length > 0) foreach (string fx in f[14].Split('\u001F')) if (fx.Length > 0) it.Funcs.Add(Unesc(fx));
                            break;
                        case "T":
                            it = new Item(); it.K = Kind.Text; it.Id = IntP(f[1]); it.Col = IntP(f[2]); it.Size = Pf(f[3]);
                            it.X = Pf(f[4]); it.Y = Pf(f[5]); it.Text = Unesc(f[6]);
                            if (f.Length > 7) it.Rot = Pf(f[7]);
                            if (f.Length > 8) { int st0; if (int.TryParse(f[8], NumberStyles.Integer, CultureInfo.InvariantCulture, out st0)) it.Style = st0 & 15; }
                            break;
                        case "N":
                            it = new Item(); it.K = Kind.Note; it.Id = IntP(f[1]); it.Bg = IntP(f[2]);
                            it.X = Pf(f[3]); it.Y = Pf(f[4]); it.W = Pf(f[5]); it.H = Pf(f[6]); it.Text = f.Length > 7 ? Unesc(f[7]) : "";
                            if (f.Length > 8) it.Rot = Pf(f[8]);
                            break;
                        case "I":
                            it = new Item(); it.K = Kind.Image; it.Id = IntP(f[1]);
                            it.X = Pf(f[2]); it.Y = Pf(f[3]); it.W = Pf(f[4]); it.H = Pf(f[5]); it.ImgId = IntP(f[6]);
                            if (f.Length > 7) it.Rot = Pf(f[7]);
                            break;
                    }
                    if (it != null) res.Add(it);
                }
                catch (Exception ex) { parseBad++; if (parseBadMsg == null) parseBadMsg = ex.Message + " [" + f[0] + "]"; }
            }
            return res;
        }

        // ---------- history ----------
        string Snapshot() { return Serialize(items, false, null); }

        void Commit()
        {
            baseDirty = true;
            undo.Add(Snapshot());
            if (undo.Count > 200) undo.RemoveAt(0);
            redo.Clear();
            dirty = true;
            autoTimer.Stop(); autoTimer.Start();
            UpdateTitle();
            Invalidate();
        }

        void RestoreSnapshot(string s)
        {
            baseDirty = true;
            string nm;
            items = Parse(s, null, out nm);
            sel.Clear();
            RecomputeIds();
            dirty = true; UpdateTitle();
            autoTimer.Stop(); autoTimer.Start();
            Invalidate();
        }

        void RecomputeIds()
        {
            int m = 0; foreach (Item it in items) if (it.Id > m) m = it.Id;
            nextId = m + 1;
        }

        void Undo()
        {
            CancelEditQuiet();
            if (undo.Count < 2) return;
            redo.Add(undo[undo.Count - 1]); undo.RemoveAt(undo.Count - 1);
            RestoreSnapshot(undo[undo.Count - 1]);
        }

        void Redo()
        {
            CancelEditQuiet();
            if (redo.Count == 0) return;
            string s = redo[redo.Count - 1]; redo.RemoveAt(redo.Count - 1);
            undo.Add(s); RestoreSnapshot(s);
        }

        void UpdateTitle() { form.Text = (dirty ? "\u2022 " : "") + DispName(docName) + Loc.Tr(" \u2014 السبورة"); }

        void Toast(string m) { toastText = m; toastUntil = DateTime.Now.AddMilliseconds(1900); toastActive = true; Invalidate(); }


    }
}
