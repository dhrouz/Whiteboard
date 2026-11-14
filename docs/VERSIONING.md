# Versioning

Format: **MAJOR.MINOR.PATCH** (e.g. `2.1.2`). Single source of truth: `src/App/AppInfo.cs` → `AppInfo.Version`.
It is shown in the About card and stamped into `Whiteboard.exe` (right-click → Properties → Details).

| Bump | When | Example |
|------|------|---------|
| **PATCH** (2.1.**2**) | bug fix, performance, text/translation tweak — nothing new for the user | smoother Help/About |
| **MINOR** (2.**2**.0) | a new feature the user can see or use (reset PATCH to 0) | new right-click menu, new math tool |
| **MAJOR** (**3**.0.0) | big redesign or a change that makes old files/settings incompatible | new file format |

## Not every edit needs a bump
Bump **when you hand a build to someone** (a release), not on every experiment. Internal refactors with no behaviour change (renaming files, comments, docs) don't need one.

## Release checklist
1. Edit `AppInfo.Version`.
2. Add a `## x.y.z — name` section at the top of `CHANGELOG.md` (what was added / changed / fixed).
3. `python tools/check_all.py` — it fails if steps 1 and 2 disagree.
4. `build.bat`, smoke-test, zip.
