// Board: help sheet.
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
        string[] helpLines; bool helpEn; float helpUi, helpMw, helpLh;

        void BuildHelp()
        {
            if (helpLines != null && helpEn == En && Math.Abs(helpUi - ui) < 0.01f) return;
            helpEn = En; helpUi = ui;
            helpLines = new string[] {
                Loc.Tr("الأدوات"),
                Loc.Tr("V تحديد     H تحريك     P قلم     M تمييز     E ممحاة"),
                Loc.Tr("L خط     A سهم     R مستطيل     O بيضاوي     G مثلث     S نجمة     T نص     N ملاحظة"),
                "",
                Loc.Tr("اللوحة"),
                Loc.Tr("عجلة الفأرة للتمرير     Ctrl + العجلة للتكبير والتصغير"),
                Loc.Tr("Space + سحب، أو زر الفأرة الأوسط، لتحريك اللوحة"),
                Loc.Tr("F لملاءمة المحتوى     Ctrl+0 للحجم الأصلي"),
                "",
                Loc.Tr("النوافذ"),
                Loc.Tr("Ctrl+N نافذة جديدة     Ctrl+W إغلاق النافذة     Ctrl+PageUp / PageDown للتنقل"),
                "",
                Loc.Tr("التحرير"),
                Loc.Tr("حدّد أي عنصر ثم اسحب دائرة التدوير فوقه (Shift لزوايا ثابتة، Ctrl+R لتدوير 90°)"),
                Loc.Tr("اضغط Shift أثناء الرسم لخطوط مستقيمة وأشكال متناسقة"),
                Loc.Tr("انقر مرتين على النص أو الملاحظة لتعديلها     Shift + نقر لتحديد عدة عناصر"),
                Loc.Tr("أثناء سحب أي عنصر تظهر خطوط محاذاة وتلتصق بمنتصف الشاشة وبحواف العناصر الأخرى (Ctrl لتعطيل الالتصاق)"),
                Loc.Tr("التدوير يلتصق عند 0° و45° و90° وهكذا مع عرض الزاوية (Shift لخطوات 15°، Ctrl بلا التصاق)"),
                Loc.Tr("اختر لوناً (أو الدائرة الملونة لأي لون تريده) أو سماكة أثناء التحديد لتغييرها"),
                Loc.Tr("انقر بالزر الأيمن على أي عنصر لنسخه أو قصه أو تكراره أو حذفه أو ترتيبه"),
                Loc.Tr("Ctrl+C / V / D نسخ ولصق وتكرار     Ctrl+] / [ للأمام / للخلف"),
                Loc.Tr("الصور: Ctrl+I للإدراج، أو الصقها بـ Ctrl+V، أو اسحبها وأفلتها داخل النافذة"),
                Loc.Tr("انقر على الصورة لتحديدها: اسحبها لتحريكها، واسحب المقابض لتكبيرها أو تصغيرها، و Delete للحذف"),
                "",
                Loc.Tr("الملفات"),
                Loc.Tr("Ctrl+L ليزر: دائرة مضيئة تتبع الفأرة ليعرف الطلاب أين تشير (زر الليزر يشغّله أو يطفئه)"),
                Loc.Tr("Ctrl+Z / Y تراجع وإعادة     Ctrl+S حفظ     Ctrl+O فتح     Ctrl+E تصدير صورة أو PDF"),
                Loc.Tr("زر حفظ يعرض الخيارات: ملف السبورة أو صورة أو PDF"),
                Loc.Tr("زر رياضيات (في القائمة اليمنى): مسطرة (ابدأ الرسم قرب حافتها ليلتصق الخط بها) ومنقلة ومحور إحداثي"),
                Loc.Tr("المحور: حدّده لتظهر لوحة جانبية لزيادة عناصر x و y وإضافة نقاط ورسم دوال"),
                Loc.Tr("القائمة الجانبية تظهر عند الاقتراب من الحافة، وزر التثبيت يبقيها ظاهرة  •  F11 ملء الشاشة"),
                Loc.Tr("تُحفظ لوحاتك تلقائياً وتُستعاد عند فتح التطبيق من جديد") };
            helpMw = 0f;
            foreach (string l in helpLines) helpMw = Math.Max(helpMw, TW(l));
        }

        // The help card never changes while it is fading, so its off-screen layer is rendered once per language/size/theme.
        string HelpCacheKey()
        {
            return "help|" + (En ? 1 : 0) + "|" + Width + "x" + Height + "|" + F(ui) + "|" + th.Panel.ToArgb() + "|" + th.Ink.ToArgb();
        }

        RectangleF HelpRect()
        {
            BuildHelp();
            helpLh = Math.Min(22f * ui, (Height - 130f * ui) / helpLines.Length);
            float w = Math.Min(Math.Max(helpMw + 90f * ui, 560f * ui), Width - 40f * ui), h = helpLines.Length * helpLh + 100f * ui;
            return new RectangleF((Width - w) / 2f, Math.Max(10f * ui, (Height - h) / 2f), w, h);
        }

        void DrawHelpCard(Graphics g, RectangleF r)
        {
            float lh = helpLh;
            DrawPanel(g, r);
            float tx = r.X + 30f * ui, tww = r.Width - 60f * ui;
            string[] heads = new string[] { Loc.Tr("الأدوات"), Loc.Tr("اللوحة"), Loc.Tr("التحرير"), Loc.Tr("الملفات"), Loc.Tr("النوافذ") };
            using (SolidBrush ink = new SolidBrush(th.Ink))
            using (SolidBrush mut = new SolidBrush(th.Muted))
            using (SolidBrush acc = new SolidBrush(th.Accent))
            {
                float y = r.Y + 24f * ui;
                UiText(g, Loc.Tr("السبورة \u2014 الاختصارات"), uiBold, ink, new RectangleF(tx, y, tww, lh), fmtRN);
                y += lh + 12f * ui;
                foreach (string l in helpLines)
                {
                    bool head = Array.IndexOf(heads, l) >= 0;
                    if (l.Length > 0) UiText(g, l, head ? uiBold : uiFont, head ? acc : ink, new RectangleF(tx, y, tww, lh), fmtRN);
                    y += lh;
                }
                UiText(g, Loc.Tr("انقر في أي مكان أو اضغط Esc للإغلاق"), uiFont, mut, new RectangleF(tx, r.Bottom - 34f * ui, tww, lh), fmtRN);
            }
        }
    }
}
