# Whiteboard

**English** · [العربية](README.ar.md)

A free, lightweight whiteboard for Windows with an **infinite canvas**, made for teachers. Write, draw, add shapes and images, and use built-in math tools. The interface is Arabic-first (right-to-left) and switches to English with one click.

## What can it do?
- **Infinite canvas:** pan and zoom without limits; pens, shapes, text and images.
- **Math tools:** ruler, protractor, coordinate axes and function plotting.
- **Right-click menu:** copy, cut, delete, duplicate, reorder and rotate.
- **Smart guides:** items snap to the centre and to each other while you drag or rotate, like in Word and PowerPoint.
- **Laser pointer:** a soft red spotlight that shows students where your mouse is (Ctrl+L). It also shows up in screen recordings such as OBS.
- **Light and dark themes**, and export to an image or PDF.

## Quick start (no programming needed)
You only need Windows 10 or 11. Nothing else has to be installed.
1. Click the green **Code** button on this page, then **Download ZIP**, and extract it.
2. Double-click **`build.bat`**. It builds the app in a few seconds using the compiler that already comes with Windows.
3. Open **`bin\Whiteboard.exe`**. That is the app; you can copy it anywhere.

If a window shows an error, copy the text and open an issue on this page.

## Keyboard shortcuts
| Action | Shortcut |
|---|---|
| Help | F1 |
| Undo / Redo | Ctrl+Z / Ctrl+Y |
| Copy / Paste / Duplicate | Ctrl+C / Ctrl+V / Ctrl+D |
| Save / Open | Ctrl+S / Ctrl+O |
| Export image or PDF | Ctrl+E |
| Insert image | Ctrl+I (or paste / drag and drop) |
| Zoom | Ctrl + mouse wheel |
| Fit content / Actual size | F / Ctrl+0 |
| Rotate 90° | Ctrl+R |
| Bring forward / Send backward | Ctrl+] / Ctrl+[ |
| Laser pointer on/off | Ctrl+L |
| Full screen | F11 |
| New window / Close window | Ctrl+N / Ctrl+W |

## For developers
- Language and tools: **C# 5**, WinForms + GDI+ (custom-drawn UI), no external dependencies, built with `csc.exe` from the .NET Framework.
- Everything lives in one `Board` control, split into `partial class` files by responsibility under `src/Board/`.
- The version number is a single constant in `src/App/AppInfo.cs` (MAJOR.MINOR.PATCH); changes are listed in `CHANGELOG.md`.
- `python tools/check_all.py` runs the project's consistency checks.
- More details: `docs/ARCHITECTURE.md`, `docs/CODE_MAP.md`, `docs/VERSIONING.md`.

```
build.bat            one-click build
src/
  App/               entry point, main window, fonts, version info
  Models/            items, document, enums
  UI/                theme, buttons, UI widgets
  Math/              expression parser (function plots)
  Localization/      Arabic <-> English strings
  Board/             the Board class, split by responsibility
tools/               checks and code-map generator
docs/                architecture, code map, versioning
```

## License
MIT. See [`LICENSE`](LICENSE).
