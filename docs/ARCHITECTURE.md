# Architecture (v2.1)

## Layers
| Layer | Folder | Notes |
|-------|--------|-------|
| Entry & window | `src/App` | `Program.Main` → `MainForm` (KeyPreview, fullscreen) → one `Board` control |
| Data | `src/Models` | `Item` is one flat class for every object kind; `Doc` is per-tab state |
| UI primitives | `src/UI` | `Theme` (animated colour set), `Btn`, `CtxRow`, `SymbolBar` |
| Logic | `src/Math`, `src/Localization` | pure, no GDI: `ExprParser`, `Loc` |
| Application | `src/Board/*` | everything else, as `partial class Board : Control` |

## Board files (what to open for what)
| You want to change… | Open |
|---|---|
| a new field / constant / palette | `Board.Fields.cs` |
| settings, session autosave, tabs | `Board.Persistence.cs` |
| language / mirroring / dialogs | `Board.Language.cs` |
| hit-testing, bounds, rotation maths | `Board.Geometry.cs` |
| `.wboard` format, undo/redo | `Board.Serialization.cs` |
| open/save/export | `Board.Files.cs` |
| ruler / protractor / axes behaviour + axes side panel | `Board.MathTools.cs` (drawing: `Board.MathDraw.cs`) |
| About card, hover-reveal tools menu | `Board.Overlays.cs` |
| copy/paste/images/z-order | `Board.Clipboard.cs` |
| in-canvas text editing | `Board.TextEdit.cs` |
| zoom/fit/tool/colour/`DoAction` | `Board.View.cs` |
| shortcuts | `Board.Keyboard.cs` |
| mouse, resize, rotate | `Board.Mouse.cs` |
| right-click menu | `Board.ContextMenu.cs` |
| animation state machine (`OnTick`), fade layers | `Board.Animation.cs` |
| paint pipeline, grid, items, selection | `Board.Render.cs` |
| where every button/panel sits | `Board.Layout.cs` |
| panels, tabs, save/math popup | `Board.UiDraw.cs` |
| splash + teaching icon | `Board.Splash.cs` |
| button look, tooltips | `Board.Buttons.cs` |
| help sheet | `Board.Help.cs` |
| icons | `Board.Icons.cs` |

## Performance rules (learned the hard way)
1. `BuildUi()` runs on every paint and mouse move → never measure text directly; use `TW/TWf` (cached).
2. Soft shadows go through `DrawShadow` (cached bitmap). Don't fill 8 paths per frame.
3. Fades go through `DrawFaded` with the **smallest possible area**. Never fade the whole window — dim with a plain alpha rectangle instead.
4. Something that animates forever (like the About icon) must invalidate only its own rectangle.
5. `DrawItems(g, cull)` skips off-screen items; export passes `RectangleF.Empty` (draw all).

## Adding things
New context-menu entry: add a row in `ShowContextMenu`, a case in `RunCtx`, an icon in `DrawIcon`, and a `Loc` entry.
