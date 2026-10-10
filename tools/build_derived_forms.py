#!/usr/bin/env python3
"""Build data/derived-forms.jsonl: every empty 1996 headword that is a derived form of a
headword that has a Sakhr meaning. Wiktionary inflection table first, then validated suffix rules.
Output is shipped inside the app so lookup works with the Wiktionary toggle on, off or data absent."""
import json, sys
AR = lambda s: any('\u0600' <= c <= '\u06ff' for c in s)
d = {}
for l in open('data/dictionary.jsonl', encoding='utf-8'):
    j = json.loads(l); d[j['word']] = [m for m in j['meanings'] if AR(m)]
inf = {}
for l in open('data/inflections-en.jsonl', encoding='utf-8'):
    j = json.loads(l); inf.setdefault(j['form'], j)
def rel_from_note(n):
    n = n.lower()
    for k, v in (('plural','plural'),('past','past'),('participle','participle'),('comparative','comparative'),('superlative','superlative'),('third-person','third-person'),('present','present participle'),('gerund','gerund')):
        if k in n: return v
    return 'form'
RULES = [('IES','Y','plural'),('VES','F','plural'),('VES','FE','plural'),('ES','','plural'),('S','','plural'),
 ('IED','Y','past'),('ED','','past'),('ED','E','past'),('ING','','gerund'),('ING','E','gerund'),
  ('ILY','Y','adverb'),('LY','','adverb'),('NESS','','noun'),('MEN','MAN','plural')]
def has(w): return bool(d.get(w))
def resolve(w, depth=0):
    """-> (lemma, relation, source) or None; lemma always has a Sakhr meaning."""
    l = inf.get(w, {}).get('lemma')
    if l and has(l):
        rel = rel_from_note(inf[w].get('note',''))
        if rel != 'form': return l, rel, 'wiktionary'
    for suf, rep, rel in RULES:
        if w.endswith(suf) and len(w) - len(suf) >= 2:
            c = w[:-len(suf)] + rep
            if c != w and has(c): return c, rel, 'rule'
    return None
out = []
empty = [w for w, m in d.items() if not m]
for w in sorted(empty):
    r = resolve(w)
    if r: out.append({'word': w, 'lemma': r[0], 'relation': r[1], 'source': r[2]})
# second pass: forms of forms (e.g. MAOISTS -> MAOIST -> MAOIST?) resolved against first-pass lemmas
with open('data/derived-forms.jsonl', 'w', encoding='utf-8') as f:
    for o in out: f.write(json.dumps(o, ensure_ascii=False) + '\n')
done = {o['word'] for o in out}
tr = {json.loads(l)['word'] for l in open('data/translations-en-ar.jsonl', encoding='utf-8')}
rest = sorted(w for w in empty if w not in done and w not in tr)
open('data/unresolved-words.txt', 'w').write('\n'.join(rest) + '\n')
print('empty', len(empty), 'derived', len(out), 'translations-file', len(tr), 'unresolved', len(rest), file=sys.stderr)
