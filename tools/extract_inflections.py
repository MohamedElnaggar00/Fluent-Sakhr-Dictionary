#!/usr/bin/env python3
"""Extract inflection pairs (form -> lemma) for BOTH English and Arabic from the
raw kaikki.org Wiktextract extract (enwiktionary covers both languages).

Reads the dump from stdin (pre-filtered by grep for speed). Writes:
  data/inflections-en.jsonl: {"form":"ABANDONS","lemma":"ABANDON","note":"third-person singular simple present indicative"}
  data/inflections-ar.jsonl: {"form":"يكتب","lemma":"كتب","note":"third-person masculine singular present"}

English lemmas are filtered to the Sakhr wordlist; Arabic lemmas are filtered to
the app's Arabic search set (normalized Wiktionary headwords + Sakhr 1996
meanings, full strings and tokens - mirrors DictionaryService.EnsureLoaded).
Forms that already resolve directly in the app are skipped.

Source data: kaikki.org / Wiktextract, CC BY-SA 4.0.
"""
import json
import re
import sys

# ---------------- English side ----------------

def load_en_found(path):
    """English words the app actually resolves: records with at least one
    Arabic-containing meaning (miss-records like ABANDONS stay redirectable)."""
    words = set()
    with open(path, encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            obj = json.loads(line)
            if any(m and has_arabic(m) for m in obj.get("meanings", [])):
                words.add(obj["word"].upper())
    return words

# ---------------- Arabic side ----------------

TASHKEEL = set("ًٌٍَُِّْٰـ")
HAMZA_ALEFS = {"أ", "إ", "آ", "ٱ"}

def normalize_arabic(s):
    """Mirror of DictionaryService.NormalizeArabic."""
    out = []
    pending_space = False
    for c in s:
        if c.isspace():
            if out:
                pending_space = True
            continue
        if c in TASHKEEL:
            continue
        if c in HAMZA_ALEFS:
            c = "ا"
        elif c in ("ى", "ئ"):
            c = "ي"
        elif c == "ؤ":
            c = "و"
        if "ء" <= c <= "ي":
            if pending_space:
                out.append(" ")
                pending_space = False
            out.append(c)
    return "".join(out)

def has_arabic(s):
    return any("؀" <= c <= "ۿ" for c in s)

def load_ar_set(wik_path, sakhr_path):
    keys = set()
    with open(wik_path, encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            k = normalize_arabic(json.loads(line)["word"])
            if k:
                keys.add(k)
    with open(sakhr_path, encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            for m in json.loads(line).get("meanings", []):
                if not m or not has_arabic(m):
                    continue
                k = normalize_arabic(m)
                if k:
                    keys.add(k)
                for tok in m.split():
                    if len(tok) > 1:
                        tk = normalize_arabic(tok)
                        if tk:
                            keys.add(tk)
    return keys

EN_WORDS = load_en_found("data/dictionary.jsonl")
AR_KEYS = load_ar_set("data/wiktionary-ar.jsonl", "data/dictionary.jsonl")
print(f"English found-words {len(EN_WORDS)}, Arabic search set {len(AR_KEYS)}", file=sys.stderr)

en_pairs = {}
ar_pairs = {}

SKIP_TAGS = {
    "inflection-template", "table-tags", "romanization", "no-gloss",
    "form-of", "alt-of", "class-col-2", "class-col-3",
}

def note_from_tags(tags):
    keep = [t for t in tags if t not in SKIP_TAGS and not t.startswith("form-of-")]
    return " ".join(keep)

FORM_OF_RE = re.compile(r"^(.*?)\s+form of\s+\S+.*$", re.IGNORECASE)
AR_FORM_OK = re.compile(r"^[\u0621-\u064A\u0671-\u06D3\u064B-\u0652\u0640 ]+$")
AR_HEAD_NOTE = re.compile(r"^:?\s*(.*?)\s+of\s+\S")
EN_FORM_OK = re.compile(r"^[A-Z][A-Z0-9 '\.\-]*$")

def add_en(form, lemma, note):
    form = form.replace("’", "'").strip().upper()
    lemma = lemma.replace("’", "'").strip().upper()
    if not form or not lemma or form == lemma:
        return
    if lemma not in EN_WORDS or form in EN_WORDS:
        return
    if not EN_FORM_OK.match(form):
        return
    if form not in en_pairs:
        en_pairs[form] = (lemma, note)

def add_ar(form, lemma, note):
    # Arabic conjugation-table rows from direction 1 embed the real form after
    # the last "# ": ": first-person singular ... of X # أُسْتَكْتَبُ".
    if "# " in form:
        head, _, tail = form.rpartition("# ")
        m = AR_HEAD_NOTE.match(head.strip())
        if m:
            note = m.group(1).strip()
        form = tail
    form = form.strip()
    lemma = lemma.strip()
    if not form or not lemma or form == lemma:
        return
    if not AR_FORM_OK.match(form):
        return
    lf = normalize_arabic(form)
    ll = normalize_arabic(lemma)
    if not lf or not ll or lf == ll:
        return
    if ll not in AR_KEYS or lf in AR_KEYS:
        return
    if note.strip() == "canonical":
        return
    if lf not in ar_pairs:
        ar_pairs[lf] = (form, lemma, note)

def sense_note(sense):
    for g in sense.get("glosses") or []:
        m = FORM_OF_RE.match(g)
        if m:
            return m.group(1).strip()
    return note_from_tags(sense.get("tags") or [])

for line in sys.stdin.buffer:
    if b'"form_of"' not in line and b'"alt_of"' not in line and b'"forms"' not in line:
        continue
    try:
        obj = json.loads(line)
    except Exception:
        continue
    lang = obj.get("lang_code")
    word = obj.get("word", "")
    if lang == "en":
        add, lemma_ok = add_en, word.upper() in EN_WORDS
    elif lang == "ar":
        add, lemma_ok = add_ar, normalize_arabic(word) in AR_KEYS
    else:
        continue
    # Direction 2 first: form entries with form_of / alt_of links carry real glosses.
    for sense in obj.get("senses") or []:
        for key in ("form_of", "alt_of"):
            for fo in sense.get(key) or []:
                lemma = fo.get("word", "")
                if lemma:
                    add(word, lemma, sense_note(sense))
    # Direction 1: lemma entries with a forms array fill in what direction 2 missed.
    if lemma_ok:
        for f in obj.get("forms") or []:
            form = f.get("form", "")
            tags = f.get("tags") or []
            if not form or "romanization" in tags or "table-tags" in tags:
                continue
            add(form, word, note_from_tags(tags))

with open("data/inflections-en.jsonl", "w", encoding="utf-8") as out:
    for form in sorted(en_pairs):
        lemma, note = en_pairs[form]
        out.write(json.dumps({"form": form, "lemma": lemma, "note": note}, ensure_ascii=False) + "\n")
with open("data/inflections-ar.jsonl", "w", encoding="utf-8") as out:
    for lf in sorted(ar_pairs):
        form, lemma, note = ar_pairs[lf]
        out.write(json.dumps({"form": form, "lemma": lemma, "note": note}, ensure_ascii=False) + "\n")
print(f"{len(en_pairs)} en pairs, {len(ar_pairs)} ar pairs", file=sys.stderr)
