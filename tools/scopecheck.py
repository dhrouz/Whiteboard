import re, sys
src = open(sys.argv[1] if len(sys.argv) > 1 else 'w.cs', encoding='utf8').read()
# blank strings / chars / comments (keep newlines)
out = []; i = 0; n = len(src)
while i < n:
    c = src[i]
    if src.startswith('//', i):
        j = src.find('\n', i); j = n if j < 0 else j; i = j; continue
    if src.startswith('/*', i):
        j = src.find('*/', i) + 2; out.append('\n' * src[i:j].count('\n')); i = j; continue
    if c == "'":
        j = i + 1
        while src[j] != "'": j += 2 if src[j] == '\\' else 1
        out.append("'x'"); i = j + 1; continue
    if c == '"':
        j = i + 1
        while src[j] != '"': j += 2 if src[j] == '\\' else 1
        out.append('"s"'); i = j + 1; continue
    out.append(c); i += 1
clean = ''.join(out)

TYPES = r'(?:int|float|double|bool|string|char|long|byte|object|var|[A-Z]\w*(?:<[^<>]*(?:<[^<>]*>[^<>]*)*>)?(?:\[\])*)'
DECL = re.compile(r'(?<![\w\.])' + TYPES + r'\s+([A-Za-z_]\w*)\s*(?==|;|,|\)|\s+in\b)')
KEYW = {'return','new','else','case','throw','in','is','as','out','ref','get','set','goto','await'}

def decls(text):
    names = []
    # skip generic header junk like "case Kind.X:" labels
    text = re.sub(r'\bcase\s+[\w\.]+\s*:', ' ', text)
    text = re.sub(r'\bdefault\s*:', ' ', text)
    for m in DECL.finditer(text):
        pre = text[:m.start()].rstrip()
        tail = pre.split()[-1] if pre.split() else ''
        if tail in ('new', 'return', 'throw', 'else') : continue
        # type token must not be a keyword like 'return'
        t = text[m.start():m.start(1)].split()[0]
        if t in KEYW or m.group(1) in KEYW: continue
        names.append(m.group(1))
    return names

class Block:
    def __init__(self, parent, line):
        self.parent = parent; self.names = []; self.line = line; self.children = []
        if parent: parent.children.append(self)

problems = []
# find method bodies: signature line at 8 spaces followed by '{' at 8 spaces
for m in re.finditer(r'^( {8})(?:(?:public|private|protected|internal|static|override|virtual|sealed)\s+)*[\w<>\[\],\.\? ]+?\s+(\w+)\s*\(([^;{}]*)\)\s*\n {8}\{', clean, re.M):
    name = m.group(2)
    if name in ('if','for','foreach','while','switch','using','lock','catch'): continue
    start = clean.index('{', m.end() - 3)
    depth = 0; j = start
    while True:
        ch = clean[j]
        if ch == '{': depth += 1
        elif ch == '}':
            depth -= 1
            if depth == 0: break
        j += 1
    body = clean[start + 1:j]
    base_line = clean[:start].count('\n') + 1
    root = Block(None, base_line)
    root.names += [pm for pm in re.findall(r'(\w+)\s*(?:,|$)', re.sub(r'\([^)]*\)', '', m.group(3)))] if False else []
    for p in m.group(3).split(','):
        p = p.strip()
        if p:
            root.names.append(re.split(r'\s+', re.sub(r'=.*', '', p).strip())[-1])
    cur = root; pending = []; seg = []; line = base_line
    allblocks = [root]
    for ch in body:
        if ch == '\n': line += 1
        if ch == '{':
            text = ''.join(seg); seg = []
            b = Block(cur, line); allblocks.append(b)
            # header declarations (for/foreach/using/catch) + any plain decls in the segment preceding '{'
            b.names += decls(text) if re.search(r'\b(for|foreach|using|catch)\b', text) else []
            if not re.search(r'\b(for|foreach|using|catch)\b', text):
                cur.names += decls(text)
            cur = b
        elif ch == '}':
            text = ''.join(seg); seg = []
            cur.names += decls(text)
            cur = cur.parent if cur.parent else cur
        elif ch == ';':
            text = ''.join(seg); seg = []
            # header-only transient declarations (single-statement for/foreach/using) go to a throwaway block
            if re.search(r'\b(for|foreach|using)\s*\(', text):
                tb = Block(cur, line)
                tb.names += decls(text)
                allblocks.append(tb)
            else:
                cur.names += decls(text)
        else:
            seg.append(ch)
    for b in allblocks:
        seen = set()
        for nme in b.names:
            if nme in seen: problems.append((b.line, name, 'duplicate in same scope', nme))
            seen.add(nme)
        anc = b.parent
        while anc:
            for nme in set(b.names):
                if nme in anc.names:
                    problems.append((b.line, name, 'shadows outer', nme))
            anc = anc.parent
for p in sorted(set(problems)): print(p)
print(len(set(problems)), 'potential issues')
