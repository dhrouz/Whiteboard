# دليل رفع المشروع على GitHub بالأوامر (من الصفر)

مجلد العمل: `C:\Users\Ezy\Documents\gitProjects` — الأوامر تُكتب في Terminal داخل VS Code (الاختصار: Ctrl + ` ).

## المفاهيم
* **Repository:** مجلد المشروع مع تاريخ تعديلاته. **Commit:** لقطة محفوظة مع جملة. **Push:** رفع إلى GitHub. **Remote (origin):** عنوان المستودع على GitHub.

## الإعداد (مرة واحدة)
1. ثبّت **Git for Windows** (git-scm.com) بالخيارات الافتراضية، و**VS Code**. أعد فتح VS Code بعد التثبيت.
2. افتح Terminal وتأكد: `git --version`
3. عرّف نفسك (يظهر على كل Commit):
```
git config --global user.name "dhrouz"
git config --global user.email "91943322+dhrouz@users.noreply.github.com"
git config --global init.defaultBranch main
```
4. في GitHub: Settings > Emails فعّل **Keep my email addresses private** و**Block command line pushes that expose my email** (لا يظهر إيميلك الحقيقي أبداً).

## أول رفع
5. فك ضغط المشروع بحيث يصير المسار `C:\Users\Ezy\Documents\gitProjects\Whiteboard\build.bat`.
6. في VS Code: File > Open Folder واختر `Whiteboard`، ثم افتح Terminal.
7. `git init`  (ينشئ مستودعاً محلياً)
8. `git status`  (تأكد أن `bin` غير مذكور، و`.gitignore` يمنعه)
9. `git add .`  ثم `git status` (الملفات صارت خضراء = جاهزة)
10. `git commit -m "Initial commit"`
11. في github.com: **New repository** > الاسم `Whiteboard` > **Private** > **لا تفعّل** Add README ولا .gitignore ولا license (عندك أصلاً) > Create.
12. اربط المستودع المحلي بالبعيد:
```
git remote add origin https://github.com/dhrouz/Whiteboard.git
git push -u origin main
```
(يفتح المتصفح لتسجيل الدخول مرة واحدة عبر Git Credential Manager.)
13. حدّث صفحة المستودع على GitHub: يجب أن ترى الملفات.

## العادة اليومية
```
git status
git add .
git commit -m "جملة قصيرة بالإنجليزية تشرح التغيير"
git push
```
أوامر مفيدة: `git log --oneline` (التاريخ) · `git diff` (ما تغيّر قبل add).

## من Private إلى Public
Settings > General > آخر الصفحة **Danger Zone > Change visibility**. قبلها راجع: لا مفاتيح ولا شهادات ولا معلومات شخصية، **وكل التاريخ يصير عاماً** (حتى ما حذفته سابقاً). إن شككت، أنشئ مستودعاً جديداً نظيفاً.

## نسخة جاهزة للتنزيل (Release)
بعد `build.bat`: صفحة المستودع > Releases > Create a new release > Tag `v2.3.1` > أرفق `bin\Whiteboard.exe` > Publish.

## بعدها
Branches (`git switch -c اسم`) ثم Pull Requests ثم Issues ثم GitHub Actions. ولاحقاً GUI (GitHub Desktop) عندما تتقن الأوامر.
