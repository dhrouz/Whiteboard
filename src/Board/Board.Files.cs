// Board: open/save/export (PNG, JPG, PDF), popups.
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
        // ---------- file operations ----------
        bool LoadFile(string path, bool isAutosave)
        {
            try
            {
                string text = File.ReadAllText(path, Encoding.UTF8);
                Dictionary<int, Bitmap> imgs = new Dictionary<int, Bitmap>();
                string nm;
                List<Item> list = Parse(text, imgs, out nm);
                int bad = parseBad; string badMsg = parseBadMsg;
                images = imgs; b64.Clear();
                nextImg = 1; foreach (int k in imgs.Keys) if (k >= nextImg) nextImg = k + 1;
                items = list; sel.Clear(); RecomputeIds();
                undo.Clear(); redo.Clear(); undo.Add(Snapshot());
                if (!isAutosave && bad > 0) Info(Loc.Tr("تعذّرت قراءة بعض أجزاء الملف: ") + bad + "\n" + badMsg);
                if (isAutosave) { docName = nm ?? Doc.DefaultName; curPath = null; }
                else { docName = Path.GetFileNameWithoutExtension(path); curPath = path; }
                dirty = false; UpdateTitle();
                FitView();
                Invalidate();
                return true;
            }
            catch (Exception)
            {
                if (!isAutosave) Info(Loc.Tr("تعذّر فتح هذا الملف. ربما لا يكون ملف سبورة صالحاً."));
                return false;
            }
        }

        void OpenDialog()
        {
            CancelEditQuiet();
            using (OpenFileDialog d = new OpenFileDialog())
            {
                d.Title = Loc.Tr("فتح لوحة"); d.Filter = Loc.Tr("لوحة السبورة (*.wboard)|*.wboard|كل الملفات (*.*)|*.*");
                if (d.ShowDialog(FindForm()) != DialogResult.OK) return;
                if (!(items.Count == 0 && curPath == null))
                {
                    if (docs.Count >= 12) { Toast(Loc.Tr("وصلت إلى الحد الأقصى من النوافذ (12)")); return; }
                    NewTabInternal();
                }
                if (LoadFile(d.FileName, false)) Toast(Loc.Tr("تم فتح: ") + docName);
            }
        }

        void SaveFile(bool saveAs)
        {
            CancelEditQuiet();
            string path = curPath;
            if (path == null || saveAs)
            {
                using (SaveFileDialog d = new SaveFileDialog())
                {
                    d.Title = Loc.Tr("حفظ اللوحة"); d.Filter = Loc.Tr("لوحة السبورة (*.wboard)|*.wboard");
                    d.FileName = DispName(docName) + ".wboard";
                    if (d.ShowDialog(FindForm()) != DialogResult.OK) return;
                    path = d.FileName;
                }
            }
            try
            {
                string data = Serialize(items, true, null);
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, data, new UTF8Encoding(false));
                // read it back and make sure every item survived before replacing the real file
                string nm0; List<Item> chk = Parse(File.ReadAllText(tmp, Encoding.UTF8), null, out nm0);
                if (chk.Count != items.Count) { try { File.Delete(tmp); } catch (Exception) { } throw new IOException(Loc.Tr("فشل التحقق من الملف بعد الحفظ") + " (" + chk.Count + "/" + items.Count + ")" + (parseBadMsg != null ? "\n" + parseBadMsg : "")); }
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
                curPath = path; docName = Path.GetFileNameWithoutExtension(path); dirty = false; UpdateTitle();
                Toast(Loc.Tr("تم الحفظ"));
            }
            catch (Exception ex) { Info(Loc.Tr("تعذّر حفظ الملف:\n") + ex.Message); }
        }

        void NewBoard()
        {
            if (docs.Count >= 12) { Toast(Loc.Tr("وصلت إلى الحد الأقصى من النوافذ (12)")); return; }
            NewTabInternal();
        }

        Bitmap RenderBoard(out RectangleF area)
        {
            RectangleF u = UnionBounds(items);
            u.Inflate(40f, 40f);
            area = u;
            float sc = 2f;
            while ((u.Width * sc > 8000f || u.Height * sc > 8000f) && sc > 0.05f) sc *= 0.8f;
            Bitmap bmp = new Bitmap(Math.Max(1, (int)(u.Width * sc)), Math.Max(1, (int)(u.Height * sc)));
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(th.Bg);
                SetQuality(g);
                g.ScaleTransform(sc, sc);
                g.TranslateTransform(-u.X, -u.Y);
                DrawItems(g);
            }
            return bmp;
        }

        static byte[] JpegBytes(Bitmap bmp)
        {
            ImageCodecInfo codec = null;
            foreach (ImageCodecInfo ci in ImageCodecInfo.GetImageEncoders())
                if (ci.FormatID == ImageFormat.Jpeg.Guid) { codec = ci; break; }
            using (MemoryStream ms = new MemoryStream())
            {
                if (codec == null) { bmp.Save(ms, ImageFormat.Jpeg); return ms.ToArray(); }
                using (EncoderParameters ep = new EncoderParameters(1))
                {
                    ep.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 92L);
                    bmp.Save(ms, codec, ep);
                }
                return ms.ToArray();
            }
        }

        // Minimal single-page PDF containing the board as a high-quality JPEG image.
        static void WritePdf(string path, Bitmap bmp, float pageW, float pageH)
        {
            byte[] jpg = JpegBytes(bmp);
            MemoryStream o = new MemoryStream();
            Action<string> wr = delegate (string t) { byte[] bb = Encoding.ASCII.GetBytes(t); o.Write(bb, 0, bb.Length); };
            List<long> offs = new List<long>();
            string pw = F(pageW), ph = F(pageH);
            wr("%PDF-1.4\n");
            offs.Add(o.Position); wr("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
            offs.Add(o.Position); wr("2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n");
            offs.Add(o.Position); wr("3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 " + pw + " " + ph + "] /Resources << /XObject << /Im0 5 0 R >> >> /Contents 4 0 R >>\nendobj\n");
            string content = "q " + pw + " 0 0 " + ph + " 0 0 cm /Im0 Do Q";
            offs.Add(o.Position); wr("4 0 obj\n<< /Length " + content.Length + " >>\nstream\n" + content + "\nendstream\nendobj\n");
            offs.Add(o.Position); wr("5 0 obj\n<< /Type /XObject /Subtype /Image /Width " + bmp.Width + " /Height " + bmp.Height + " /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length " + jpg.Length + " >>\nstream\n");
            o.Write(jpg, 0, jpg.Length);
            wr("\nendstream\nendobj\n");
            long xref = o.Position;
            wr("xref\n0 6\n0000000000 65535 f \n");
            foreach (long off in offs) wr(off.ToString("0000000000", CultureInfo.InvariantCulture) + " 00000 n \n");
            wr("trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n" + xref + "\n%%EOF\n");
            File.WriteAllBytes(path, o.ToArray());
        }

        void ExportPng() { ExportBoard(false); }

        void ExportBoard(bool pdf)
        {
            CancelEditQuiet();
            if (items.Count == 0) { Toast(Loc.Tr("لا يوجد شيء للتصدير بعد")); return; }
            using (SaveFileDialog d = new SaveFileDialog())
            {
                d.Title = pdf ? Loc.Tr("حفظ كملف PDF") : Loc.Tr("حفظ كصورة");
                d.Filter = pdf ? Loc.Tr("ملف PDF (*.pdf)|*.pdf") : Loc.Tr("صورة PNG (*.png)|*.png|صورة JPEG (*.jpg)|*.jpg");
                d.FilterIndex = 1; d.AddExtension = true; d.DefaultExt = pdf ? "pdf" : "png";
                d.FileName = DispName(docName);
                if (d.ShowDialog(FindForm()) != DialogResult.OK) return;
                try
                {
                    string path = d.FileName;
                    string ext = Path.GetExtension(path).ToLowerInvariant();
                    if (pdf) { if (ext != ".pdf") { ext = ".pdf"; path += ext; } }
                    else if (ext != ".png" && ext != ".jpg" && ext != ".jpeg")
                    {
                        ext = d.FilterIndex == 2 ? ".jpg" : ".png";
                        path += ext;
                    }
                    RectangleF area;
                    using (Bitmap bmp = RenderBoard(out area))
                    {
                        if (pdf)
                        {
                            float k = Math.Min(1f, 14000f / Math.Max(area.Width, area.Height));
                            WritePdf(path, bmp, area.Width * k, area.Height * k);
                        }
                        else if (ext == ".png") bmp.Save(path, ImageFormat.Png);
                        else File.WriteAllBytes(path, JpegBytes(bmp));
                    }
                    Toast(Loc.Tr("تم الحفظ: ") + Path.GetFileName(path));
                }
                catch (Exception ex) { Info(Loc.Tr("تعذّر الحفظ:\n") + ex.Message); }
            }
        }

        void RunSaveOption(int i)
        {
            if (popKind == "math")
            {
                if (i == 0) AddRuler(); else if (i == 1) AddProtractor(); else AddAxes();
                return;
            }
            if (i == 0) SaveFile(false);
            else if (i == 1) ExportBoard(false);
            else ExportBoard(true);
        }

        void TogglePopup(string k)
        {
            if (saveMenu && popKind == k) saveMenu = false;
            else { popKind = k; saveMenu = true; showHelp = false; showAbout = false; }
            menuHover = -1;
            Invalidate();
        }

        void ToggleFullscreen()
        {
            bool on = !form.IsFullscreen;
            form.SetFullscreen(on);
            if (on) Toast(Loc.Tr("اضغط Esc للخروج من ملء الشاشة"));
            Invalidate();
        }

        List<Btn> AllBtns()
        {
            List<Btn> r = new List<Btn>(btns);
            r.AddRange(abtns); r.AddRange(obtns);
            return r;
        }


    }
}
