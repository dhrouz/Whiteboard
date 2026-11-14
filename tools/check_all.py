"""Heuristic checks for the multi-file project (no compiler needed).
Usage:  python tools/check_all.py            (run from the project root)
Checks: BOM + CRLF, brace/paren balance, forbidden C# 6 syntax, Loc.Tr coverage, scopecheck per file."""
import glob, os, re, subprocess, sys
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
files = sorted(glob.glob(os.path.join(ROOT, 'src', '**', '*.cs'), recursive=True))
files += sorted(glob.glob(os.path.join(ROOT, 'installer', '**', '*.cs'), recursive=True))
problems = 0

def strip(s):
    out = []; i = 0; n = len(s)
    while i < n:
        c = s[i]
        if s.startswith('//', i):
            j = s.find('\n', i); i = n if j < 0 else j; continue
        if s.startswith('/*', i):
            i = s.find('*/', i) + 2; continue
        if c == "'":
            j = i + 1
            while s[j] != "'": j += 2 if s[j] == '\\' else 1
            i = j + 1; out.append("'x'"); continue
        if c == '"':
            j = i + 1
            while s[j] != '"': j += 2 if s[j] == '\\' else 1
            i = j + 1; out.append('"s"'); continue
        out.append(c); i += 1
    return ''.join(out)

def unescape(lit):
    lit = re.sub(r'\\u([0-9a-fA-F]{4})', lambda m: chr(int(m.group(1), 16)), lit)
    return lit.replace('\\n', '\n').replace('\\t', '\t').replace('\\"', '"').replace('\\\\', '\\')

STR = r'"((?:[^"\\]|\\.)*)"'
tr_used = {}; map_keys = set(); all_src = ''
for f in files:
    raw = open(f, 'rb').read()
    rel = os.path.relpath(f, ROOT)
    if raw[:3] != b'\xef\xbb\xbf': print('NO BOM', rel); problems += 1
    if raw.count(b'\n') != raw.count(b'\r\n'): print('LF-only lines', rel); problems += 1
    txt = raw.decode('utf-8-sig')
    c = strip(txt)
    for a, b in ('{}', '()', '[]'):
        if c.count(a) != c.count(b): print('UNBALANCED', a, b, rel); problems += 1
    for pat, why in ((r'\?\.', '?. operator'), (r'\$"', 'interpolated string'), (r'\bnameof\s*\(', 'nameof'),
                     (r'\bout\s+(?:var|int|float|string|double)\s+\w+\s*[,)]', 'inline out declaration'),
                     (r'\)\s*=>\s*[^{]', 'expression-bodied lambda/member'), (r'\bawait\b', 'await')):
        for m in re.finditer(pat, c):
            line = c[:m.start()].count('\n') + 1
            src_line = c.split('\n')[line - 1]
            if why == 'inline out declaration' and re.match(r'^\s*(?:public |private |static |protected )*[\w<>\[\],\. ]+\s+\w+\(.*\)\s*$', src_line): continue  # method declaration
            print('C#6?', why, rel, 'line', line); problems += 1
    for m in re.finditer(r'Loc\.Tr\(' + STR + r'\)', txt):
        tr_used.setdefault(unescape(m.group(1)), rel)
    if rel.endswith('Loc.cs'):
        for m in re.finditer(r'm\[' + STR + r'\]\s*=', txt): map_keys.add(unescape(m.group(1)))

nowrap = set()
missing = [k for k in tr_used if k not in map_keys and k not in nowrap]
for k in missing: print('Loc entry missing:', repr(k), 'used in', tr_used[k]); problems += 1

sc = os.path.join(ROOT, 'tools', 'scopecheck.py')
for f in files:
    out = subprocess.run([sys.executable, '-I', sc, f], capture_output=True, text=True).stdout
    lines = [l for l in out.strip().split('\n') if l and 'potential issues' not in l]
    for l in lines: print('scope:', os.path.relpath(f, ROOT), l)
# version: AppInfo.Version must equal the newest heading in CHANGELOG.md
ai = open(os.path.join(ROOT, 'src', 'App', 'AppInfo.cs'), encoding='utf-8-sig').read()
cv = re.search(r'Version\s*=\s*"([\d\.]+)"', ai).group(1)
cl = open(os.path.join(ROOT, 'CHANGELOG.md'), encoding='utf-8').read()
top = re.search(r'^## (\d+\.\d+\.\d+)', cl, re.M)
if not top or top.group(1) != cv:
    print('VERSION MISMATCH: AppInfo.Version = %s but top CHANGELOG entry = %s' % (cv, top.group(1) if top else 'none')); problems += 1
else: print('Version %s matches CHANGELOG' % cv)
print('Loc.Tr literals checked: %d' % len(tr_used))
print('\n%d files checked, %d problem(s)%s' % (len(files), problems, '' if problems else ' - clean (scopecheck output above is advisory)'))
sys.exit(1 if problems else 0)
