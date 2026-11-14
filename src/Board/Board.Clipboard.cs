// Board: images, copy/paste/duplicate/delete/z-order.
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
        // ---------- clipboard / images ----------
        void AddImage(Bitmap bmp, PointF center)
        {
            if (Math.Max(bmp.Width, bmp.Height) > 2400)
            {
                float kk = 2400f / Math.Max(bmp.Width, bmp.Height);
                Bitmap small = new Bitmap(Math.Max(1, (int)(bmp.Width * kk)), Math.Max(1, (int)(bmp.Height * kk)));
                using (Graphics sg = Graphics.FromImage(small))
                {
                    sg.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    sg.DrawImage(bmp, 0, 0, small.Width, small.Height);
                }
                bmp.Dispose(); bmp = small;
            }
            int id = nextImg++;
            images[id] = bmp;
            float s = Math.Min(1f, 480f / Math.Max(bmp.Width, bmp.Height));
            Item it = NewItem(Kind.Image);
            it.ImgId = id; it.W = bmp.Width * s; it.H = bmp.Height * s;
            it.X = center.X - it.W / 2f; it.Y = center.Y - it.H / 2f;
            items.Add(it); sel.Clear(); sel.Add(it.Id);
            SetTool(Tool.Select);
            Commit();
        }

        static bool IsImageExt(string path)
        {
            string e = Path.GetExtension(path).ToLowerInvariant();
            return e == ".png" || e == ".jpg" || e == ".jpeg" || e == ".bmp" || e == ".gif" || e == ".tif" || e == ".tiff";
        }

        void InsertImages()
        {
            CancelEditQuiet();
            using (OpenFileDialog d = new OpenFileDialog())
            {
                d.Title = Loc.Tr("إدراج صورة");
                d.Filter = Loc.Tr("الصور|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|كل الملفات (*.*)|*.*");
                d.Multiselect = true;
                if (d.ShowDialog(FindForm()) != DialogResult.OK) return;
                PointF c = ViewCenter(); int k = 0;
                foreach (string f in d.FileNames) { AddImageFile(f, new PointF(c.X + k * 30f, c.Y + k * 30f)); k++; }
            }
        }

        void AddImageFile(string path, PointF center)
        {
            try
            {
                using (Image tmp = Image.FromFile(path)) AddImage(new Bitmap(tmp), center);
            }
            catch (Exception) { Toast(Loc.Tr("تعذّر تحميل الصورة")); }
        }

        void Copy()
        {
            if (sel.Count == 0) return;
            clip = Serialize(items.Where(delegate(Item i) { return sel.Contains(i.Id); }).ToList(), false, null);
            pasteN = 0;
            try { Clipboard.Clear(); } catch (Exception) { }
            Toast(Loc.Tr("تم النسخ"));
        }

        void Paste()
        {
            try
            {
                if (Clipboard.ContainsFileDropList())
                {
                    PointF pc = ViewCenter(); int pk = 0;
                    foreach (string pf in Clipboard.GetFileDropList())
                        if (IsImageExt(pf)) { AddImageFile(pf, new PointF(pc.X + pk * 30f, pc.Y + pk * 30f)); pk++; }
                    if (pk > 0) return;
                }
                if (Clipboard.ContainsImage())
                {
                    Image img = Clipboard.GetImage();
                    if (img != null) { AddImage(new Bitmap(img), ViewCenter()); return; }
                }
                if (Clipboard.ContainsText())
                {
                    string t = Clipboard.GetText();
                    if (t.Trim().Length > 0)
                    {
                        Item it = NewItem(Kind.Text);
                        it.Text = t.Replace("\r\n", "\n").Replace("\r", "\n"); it.Size = TextSizes[sizeIdx];
                        PointF c = ViewCenter(); it.X = c.X; it.Y = c.Y;
                        items.Add(it); sel.Clear(); sel.Add(it.Id); SetTool(Tool.Select); Commit();
                        return;
                    }
                }
            }
            catch (Exception) { }
            if (clip != null) PasteItems(clip, 24f * (++pasteN));
        }

        void PasteItems(string data, float off)
        {
            string nm;
            List<Item> list = Parse(data, null, out nm);
            if (list.Count == 0) return;
            sel.Clear();
            foreach (Item it in list) { it.Id = nextId++; it.Move(off, off); items.Add(it); sel.Add(it.Id); }
            SetTool(Tool.Select);
            Commit();
        }

        void Duplicate()
        {
            if (sel.Count == 0) return;
            PasteItems(Serialize(items.Where(delegate(Item i) { return sel.Contains(i.Id); }).ToList(), false, null), 24f);
        }

        void DeleteSel()
        {
            if (sel.Count == 0) return;
            items.RemoveAll(delegate(Item i) { return sel.Contains(i.Id); });
            sel.Clear(); Commit();
        }

        void SelectAll() { SetTool(Tool.Select); sel.Clear(); foreach (Item it in items) sel.Add(it.Id); Invalidate(); }

        void Reorder(bool toFront)
        {
            if (sel.Count == 0) return;
            List<Item> chosen = items.Where(delegate(Item i) { return sel.Contains(i.Id); }).ToList();
            List<Item> rest = items.Where(delegate(Item i) { return !sel.Contains(i.Id); }).ToList();
            items = toFront ? rest.Concat(chosen).ToList() : chosen.Concat(rest).ToList();
            Commit();
        }


    }
}
