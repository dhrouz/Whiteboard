// Board: start-up, settings, session autosave, tabs.
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
        // ---------- init & persistence ----------
        static string DataDir()
        {
            string d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Whiteboard");
            try { Directory.CreateDirectory(d); } catch (Exception) { }
            return d;
        }

        public void Init(string openPath)
        {
            try
            {
                string sp = Path.Combine(DataDir(), "settings.txt");
                if (File.Exists(sp))
                    foreach (string line in File.ReadAllLines(sp))
                    {
                        if (line == "dark=1") { th.Set(Dark); thTarget = Dark; }
                        if (line == "grid=0") grid = false;
                        if (line == "laser=0") laserOn = false;
                        if (line == "lang=en") Loc.En = true;
                        if (line == "pin=1") { toolsPinned = true; toolsT = 1f; }
                        if (line.StartsWith("cc="))
                        {
                            try
                            {
                                string[] parts = line.Substring(3).Split(',');
                                int[] arr = new int[parts.Length];
                                for (int q = 0; q < parts.Length; q++) arr[q] = int.Parse(parts[q]);
                                customColors = arr;
                            }
                            catch (Exception) { }
                        }
                        if (line.StartsWith("last=")) { try { lastCustom = int.Parse(line.Substring(5)); hasCustom = true; } catch (Exception) { } }
                    }
            }
            catch (Exception) { }
            if (En) ApplyLang();
            initLoading = true;
            docs.Clear(); docs.Add(new Doc()); curTab = 0;
            bool loaded = LoadSession();
            if (!loaded)
            {
                try
                {
                    string ap = Path.Combine(DataDir(), "autosave.wboard");
                    if (File.Exists(ap)) loaded = LoadFile(ap, true);
                }
                catch (Exception) { }
            }
            if (openPath != null && File.Exists(openPath))
            {
                if (loaded) NewTabInternal();
                if (LoadFile(openPath, false)) loaded = true;
            }
            initLoading = false;
            if (undo.Count == 0) undo.Add(Snapshot());
            StoreCurrent();
            needFit = true;
            UpdateTitle();
        }

        public void PersistOnExit()
        {
            CancelEditQuiet();
            SaveAutosave();
            if (PerfOn) PerfFlush();
            try
            {
                string st = "dark=" + (th.Dark ? "1" : "0") + "\ngrid=" + (grid ? "1" : "0") + "\nlaser=" + (laserOn ? "1" : "0") + "\nlang=" + (En ? "en" : "ar") + "\npin=" + (toolsPinned ? "1" : "0") + "\n";
                if (customColors != null) st += "cc=" + string.Join(",", customColors.Select(delegate (int v) { return v.ToString(); }).ToArray()) + "\n";
                if (hasCustom) st += "last=" + lastCustom + "\n";
                File.WriteAllText(Path.Combine(DataDir(), "settings.txt"), st);
            }
            catch (Exception) { }
        }

        public void SaveAutosave()
        {
            try
            {
                StoreCurrent();
                string dir = Path.Combine(DataDir(), "session");
                Directory.CreateDirectory(dir);
                foreach (string old in Directory.GetFiles(dir, "tab*.wboard")) { try { File.Delete(old); } catch (Exception) { } }
                StringBuilder idx = new StringBuilder();
                idx.Append(curTab).Append('\n');
                for (int i = 0; i < docs.Count; i++)
                {
                    Doc d = docs[i];
                    File.WriteAllText(Path.Combine(dir, "tab" + i + ".wboard"), SerializeDoc(d.Items, true, d.Name, d.Images, d.B64), new UTF8Encoding(false));
                    idx.Append(d.Path ?? "").Append('\n');
                }
                File.WriteAllText(Path.Combine(dir, "index.txt"), idx.ToString(), new UTF8Encoding(false));
            }
            catch (Exception) { }
        }

        bool LoadSession()
        {
            try
            {
                string dir = Path.Combine(DataDir(), "session");
                string idxFile = Path.Combine(dir, "index.txt");
                if (!File.Exists(idxFile)) return false;
                string[] lines = File.ReadAllLines(idxFile, Encoding.UTF8);
                int cur0 = 0; int.TryParse(lines[0], out cur0);
                bool any = false;
                for (int i = 1; i < lines.Length; i++)
                {
                    string file = Path.Combine(dir, "tab" + (i - 1) + ".wboard");
                    if (!File.Exists(file)) continue;
                    if (any) NewTabInternal();
                    if (LoadFile(file, true))
                    {
                        any = true;
                        string pth = lines[i].Trim();
                        if (pth.Length > 0 && File.Exists(pth)) curPath = pth;
                    }
                }
                if (!any) return false;
                SwitchTo(Math.Max(0, Math.Min(cur0, docs.Count - 1)));
                return true;
            }
            catch (Exception) { return false; }
        }

        void StoreCurrent()
        {
            if (curTab < 0 || curTab >= docs.Count) return;
            Doc d = docs[curTab];
            d.Items = items; d.Sel = sel; d.Images = images; d.B64 = b64; d.Undo = undo; d.Redo = redo;
            d.NextId = nextId; d.NextImg = nextImg; d.Vx = vx; d.Vy = vy; d.Vz = vz; d.ViewSet = !initLoading;
            d.Path = curPath; d.Name = docName; d.Dirty = dirty;
        }

        void LoadCurrent(Doc d)
        {
            viewAnim = false;
            items = d.Items; sel = d.Sel; images = d.Images; b64 = d.B64; undo = d.Undo; redo = d.Redo;
            nextId = d.NextId; nextImg = d.NextImg; curPath = d.Path; docName = d.Name; dirty = d.Dirty;
            cur = null; mode = Mode.None; handle = -1; pressed = null; saveMenu = false; clicked = null; rsItem = null;
            vx = d.Vx; vy = d.Vy; vz = d.Vz;
            if (undo.Count == 0) undo.Add(Snapshot());
            if (!d.ViewSet) { if (items.Count > 0) FitView(); else ResetView(); d.ViewSet = true; }
            canvasFade = initLoading ? 0f : 1f;
            UpdateTitle();
            Invalidate();
        }

        void NewTabInternal()
        {
            CancelEditQuiet(); StoreCurrent();
            docs.Add(new Doc()); curTab = docs.Count - 1;
            LoadCurrent(docs[curTab]);
            autoTimer.Stop(); autoTimer.Start();
        }

        void SwitchTo(int i)
        {
            if (i < 0 || i >= docs.Count || i == curTab) return;
            CancelEditQuiet(); StoreCurrent();
            curTab = i; LoadCurrent(docs[i]);
            autoTimer.Stop(); autoTimer.Start();
        }

        void CloseTab(int i)
        {
            if (i < 0 || i >= docs.Count) return;
            CancelEditQuiet();
            bool isCur = i == curTab;
            int cnt = isCur ? items.Count : docs[i].Items.Count;
            bool dr = isCur ? dirty : docs[i].Dirty;
            if (cnt > 0 && dr && !Ask(Loc.Tr("هذه النافذة تحتوي على تغييرات غير محفوظة. هل تريد إغلاقها؟"), Loc.Tr("إغلاق"), Loc.Tr("إلغاء"))) return;
            if (docs.Count == 1) { docs[0] = new Doc(); curTab = 0; LoadCurrent(docs[0]); }
            else if (isCur) { docs.RemoveAt(i); curTab = Math.Min(i, docs.Count - 1); LoadCurrent(docs[curTab]); }
            else { docs.RemoveAt(i); if (i < curTab) curTab--; }
            hoverTab = -1; hoverClose = false;
            autoTimer.Stop(); autoTimer.Start();
            Invalidate();
        }

        string TabName(int i)
        {
            string n = DispName(i == curTab ? docName : docs[i].Name);
            bool dr = i == curTab ? dirty : docs[i].Dirty;
            return n + (dr ? " \u2022" : "");
        }

        void ResetView() { viewAnim = false; vz = 1f; vx = Width / 2f; vy = Height / 2f; }


    }
}
