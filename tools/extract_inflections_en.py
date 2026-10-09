#!/usr/bin/env python3
"""Extract English inflection pairs (form -> lemma) from the raw kaikki.org
Wiktextract extract (enwiktionary), filtered to lemmas present in the Sakhr wordlist.

Reads the dump from stdin (pre-filtered by grep for speed), writes
data/inflections-en.jsonl: {"form":"ABANDONS","lemma":"ABANDON","note":"third-person singular simple present indicative"}

Two directions, merged:
  1. Lemma entries (word in our list) with a "forms" array - clean tags.
  2. Form entries whose senses carry "form_of"/"alt_of" back-links - covers
     irregulars (went -> go) and alternative spellings (colour -> color).

Source data: kaikki.org / Wiktextract, CC BY-SA 4.0.
"""
import json
import re
import sys

def load_wordlist(path):
    words = set()
    with open(path, encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if line:
                words.add(json.loads(line)["word"].upper())
    return words

WORDLIST = load_wordlist("data/dictionary.jsonl")
pairs = {}

SKIP_TAGS = {
    "inflection-template", "table-tags", "romanization", "no-gloss",
    "form-of", "alt-of", "class-col-2", "class-col-3",
}

def note_from_tags(tags):
    keep = [t for t in tags if t not in SKIP_TAGS and not t.startswith("form-of-")]
    return " ".join(keep)

FORM_OF_RE = re.compile(r"^(.*?)\s+form of\s+\S+.*$", re.IGNORECASE)
FORM_OK = re.compile(r"^[A-Z][A-Z0-9 '\.\-]*$")

def add(form, lemma, note):
    form = form.replace("\u2019", "'").strip().upper()
    lemma = lemma.replace("\u2019", "'").strip().upper()
    if not form or not lemma or form == lemma:
        return
    if lemma not in WORDLIST or form in WORDLIST:
        return
    if not FORM_OK.match(form):
        return
    if form not in pairs:
        pairs[form] = (lemma, note)

for line in sys.stdin.buffer:
    if b'"form_of"' not in line and b'"alt_of"' not in line and b'"forms"' not in line:
        continue
    try:
        obj = json.loads(line)
    except Exception:
        continue
    if obj.get("lang_code") != "en":
        continue
    word = obj.get("word", "")
    if word.upper() in WORDLIST:
        for f in obj.get("forms") or []:
            form = f.get("form", "")
            tags = f.get("tags") or []
            if not form or "romanization" in tags or "table-tags" in tags:
                continue
            add(form, word, note_from_tags(tags))
    for sense in obj.get("senses") or []:
        for key in ("form_of", "alt_of"):
            for fo in sense.get(key) or []:
                lemma = fo.get("word", "")
                if not lemma:
                    continue
                note = ""
                for g in sense.get("glosses") or []:
                    m = FORM_OF_RE.match(g)
                    if m:
                        note = m.group(1).strip()
                        break
                if not note:
                    note = note_from_tags(sense.get("tags") or [])
                add(word, lemma, note)

with open("data/inflections-en.jsonl", "w", encoding="utf-8") as out:
    for form in sorted(pairs):
        lemma, note = pairs[form]
        out.write(json.dumps({"form": form, "lemma": lemma, "note": note}, ensure_ascii=False) + "\n")
print(f"{len(pairs)} form->lemma pairs", file=sys.stderr)
