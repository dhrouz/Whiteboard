# Changelog

Versioning: MAJOR.MINOR.PATCH — see `docs/VERSIONING.md`. The number lives only in `src/App/AppInfo.cs`.

## 2.5.3 — Safer saving
### Fixed
- Saving a board now writes to a temporary file, reads it back and checks that every item survived before replacing the real file; if anything is wrong you get a clear message instead of a silent empty file.
- Numbers in .wboard files are now always written and read in a culture-independent way (so regional settings can never break a file).
- Opening a file now reports lines that could not be read, with the reason, instead of silently showing fewer items.

## 2.5.2 — Purple window pixel scene
### Changed
- New, much livelier pixel animation (start-up screen and About card): a lavender "window" whiteboard on a breathing round backdrop with orbiting dots; a big marker draws a squiggle while gold sparks flicker, confetti bursts when it is done, a smiling sticky note hops and blinks, the title-bar dots light up one after another, sparkles twinkle, and finally an eraser wipes the line away before the loop restarts.

## 2.5.1 — New pixel board, instant splash
### Changed
- The pixel animation now shows a **green chalkboard in a wooden frame** with a chalk tray: chalk draws the pink graph, the board blinks and smiles, the cap hops.
### Fixed
- The start-up screen is opaque from the very first frame (the board used to be visible for about half a second before the splash appeared).

## 2.5.0 — Pixel art, side menu reorder
### Changed
- **New animation in pixel-art style** on the start-up screen and in the About card: an easel whiteboard where a marker draws a graph pixel by pixel, a graduation cap hops, sparkles blink and a heart pops up. The gradient app icon is no longer shown at start-up.
- **Side menu order:** Text, Note, Math, Image first; then Line, Arrow, Rectangle, Ellipse, Triangle, Star.
- **Math** moved from the top bar to the side menu; its popup opens to the left of the side menu and the side menu stays out while it is open.
- The words "Color" and "Thickness" were removed from the bottom style bar (more room for the buttons).
- The **text format bar** (B I U S A- A+) now sits below the text, so it no longer covers the rotate handle, and the math-symbols bar no longer overlaps it (symbols above, format below; they swap when there is no room).

## 2.4.1 — Idle CPU and Help open
### Fixed
- Removed the "start-up pulse": for the first 16 seconds the app kept repainting ~64 times per second even when nothing moved (the idle repaint seen in perf logs after maximising).
- Opening Help / About: the buttons underneath now settle at once instead of repainting the whole snapshot for ~12 frames.

## 2.4.0 — Smoother UI, text size, nicer pen
### Performance
- **Hover / side menu:** the animation timer no longer sleeps 60 ms before reacting, so hovering is immediate and fast passes no longer skip items.
- Menu slide, button hover and tooltips repaint only the small area they change instead of the whole window.
- The laser repaints only the two small spots (old and new) instead of the whole window on every mouse move.
- Panel shadows were drawn through a slow GDI+ path every frame; fixed.
- **Light/dark switch:** the old frame now fades with one native GDI AlphaBlend (falls back to the old method if unavailable).
### Changed
- "New" icon is now a blank page with a "+" (different from the "Open" folder).
- The image button in the side menu is called "Image" (tooltip: "Image (Ctrl+I)").
- Hand-drawn strokes are smoothed when you lift the pen (and slightly more while drawing), so handwriting is smooth, not stair-stepped.
### Added
- Text format bar: **A-** / **A+** (smaller / bigger text) next to **B I U S**.

## 2.3.1 — Simpler laser, instant Help/About
### Changed
- **Laser** is now just a soft round red spotlight with a thin ring (like the cursor highlight in OBS): no motion trail, no centre dot, no fade.
- **Help** and **About** open and close instantly (no fade/slide animation).

## 2.3.0 — Laser pointer
### Added
- **Laser pointer** (on by default): a glowing red dot with a short fading trail follows the mouse so students can see where the teacher is pointing. Deeper red on the light board, brighter red on the dark board, with a white ring so it reads over any colour of ink.
- **Laser** button in the bottom bar (next to the theme button) and **Ctrl+L** (same as PowerPoint) to turn it off / on. The choice is saved (`laser=` in settings.txt). It hides itself while Help / About are open and when the mouse leaves the window.
- Help sheet line (Arabic + English).
### Notes
- The laser only repaints while the mouse moves or the trail fades (~0.4 s); a still mouse costs nothing.

## 2.2.3 — Fade blit without resampling + "why busy" diagnostics
### Performance
- Fade layers are now blitted with `PixelOffsetMode.None` / `CompositingQuality.HighSpeed` (and nearest-neighbour for 1:1). `SetQuality()` leaves `PixelOffsetMode.HighQuality` on the Graphics, which makes GDI+ resample every pixel even for a 1:1 copy. (2.2.2's CPU alpha did not change the measured time, so the ColorMatrix was not the cause.)
### Verified
- Owner tested 2.2.3 on Windows: Help and About open without stutter. (2.2.2's CPU-alpha change is kept; it is harmless but was not the cause.)
### Tooling
- `--perf` now also logs `busy-by:` lines (which animation kept the 15 ms ticker running) and the stages of every fade (`fade: render layer`, `alpha copy`, `blit`). Found because the board kept repainting at ~64 fps while idle after the window was maximised.

## 2.2.2 — Faster fades (measured)
### Performance (no visual or animation changes)
- Measured with `--perf`: the Help fade-in cost ~50 ms per frame (budget is 16), About ~33 ms. Cause: GDI+ `ColorMatrix` blit of the off-screen card layer.
- `DrawFaded` now multiplies the layer's alpha on the CPU and draws it without `ImageAttributes` (same pixels, a few ms). This speeds up every faded group (Help, About, Save/Math popup, toast, tooltip, context menu).
- The start-up pulse of the side-menu grip no longer keeps the canvas snapshot dirty while Help/About is open.

## 2.2.1 — Perf log flushes on exit
### Fixed
- `--perf` now writes the remaining samples when the app is closed (before, the last ~3 seconds were lost, which could hide the Help frames).

## 2.2.0 — Smart guides
### Added
- **Alignment guides while moving** (like PowerPoint / Word): dashed lines appear and the selection snaps to the **centre of the visible canvas** and to the left / centre / right and top / centre / bottom of the other objects. Hold **Ctrl** to move freely.
- **Smart rotation:** the rotate handle pulls gently to every 45° (3° of magnetism) and shows a dashed cross-hair through the object when it locks. An **angle badge** (e.g. `45°`) follows the handle and turns pink on a snap. **Shift** = 15° steps, **Ctrl** = free.
- Help sheet line describing the above (Arabic + English).
### Tooling
- `Whiteboard.exe --perf` writes `%APPDATA%\Whiteboard\perf.log` with frame times per state (board, help-fade, help-open, about…), so slowness can be measured instead of guessed. Off by default; no effect on normal use.
### Notes
- The smart-guides code (`Board.SmartGuides.cs`) was already in the project folder but had no changelog entry or version; it is released here.

## 2.1.3 — Quiet under the cards
### Performance (no visual or animation changes)
- While **Help** or **About** is open, the toolbar, tabs and side menu underneath no longer react to the mouse. Their hover / slide animations used to mark the canvas snapshot dirty, so the whole board plus the card text was re-rendered on every frame.
- **Help** fully open: the finished frame (canvas + dimming + card) is one cached bitmap; mouse moves cause no repaint at all.

## 2.1.2 — Smooth overlays
### Performance (no visual or animation changes)
- About: the card's static part (panel, texts, e-mail pill) is rendered once and reused by the fade-in; only the animated icon and the buttons are drawn live.
- About (open): while the icon animates, only the texts/panels that intersect the dirty rectangle are laid out (before, every text was re-laid-out ~65 times a second).
- Start-up warm-up now also pre-renders the Help and About cards (Arabic text shaping, glyph caches, cached layers), so the **first** open is as smooth as later ones.
- Version number now comes from one place (`AppInfo.Version`) and is also stamped into the exe's file properties; `tools/check_all.py` fails if it disagrees with this changelog.

## 2.1.1 — Overlay snapshot
### Performance
- Help/About: the canvas and UI under the overlay are rendered once into a snapshot (`baseBmp`) and re-rendered only when something underneath really changes; Help's faded layer is cached by key.

## 2.1 — Foundation
### Added
- Right-click context menu on objects (Edit text, Cut, Copy, Duplicate, Delete, Bring to front, Send to back, Rotate 90°) and on empty canvas (Paste, Select all). Custom-drawn, animated, RTL/LTR aware.
- `tools/check_all.py`, `tools/gen_codemap.py`, `docs/`.
### Changed
- Source split into `src/{App,Board,Localization,Math,Models,UI}`; `Board` is now a partial class split by responsibility.
- UI font is Segoe UI (Bold). Thmanyah Sans fonts, `otf2ttf.py` and the embedded font resources were removed; Ctrl+Shift+F and the `uifont` setting no longer exist.
- `build.bat` compiles `src\**\*.cs` and writes `bin\Whiteboard.exe`.
- Math/Save popup width follows its text; long lines get an ellipsis instead of overflowing.
### Performance
- Cached text measurement (`TW`/`TWf`), cached panel shadows, reusable off-screen fade layers (bilinear blit), cached text-item size, off-screen items are skipped while painting.
- Help and About fade only their card (the dimming is a plain alpha fill); About repaints only the icon rectangle while open (no more permanent full-window repaint).
### Fixed
- Right-clicking a toolbar button used to trigger it on release; now only the left button does.
