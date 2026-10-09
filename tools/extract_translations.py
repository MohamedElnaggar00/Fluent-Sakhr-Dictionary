#!/usr/bin/env python3
"""Fill the 1996 Sakhr index's empty records with Arabic translations from English
Wiktionary (kaikki.org raw Wiktextract extract, CC BY-SA 4.0).

Reads the raw dump from stdin (pre-filtered by grep for speed). For every English
entry, collects the Arabic words from its translation tables. Keeps only the
Sakhr 1996 records that have no Arabic meaning of their own (the "miss-records").

Writes data/translations-en-ar.jsonl:
  {"word":"WABBLE","meanings":["يتذبذب","يهتز"]}

Usage: zcat raw.jsonl.gz | grep --line-buffered -F '"translations"' | python3 tools/extract_translations.py
"""
import json
import re
import sys

ARABIC_RE = re.compile(r"[؀-ۿ]")

def load_sakhr_empty(path):
    """Uppercased Sakhr 1996 index words whose record carries no Arabic meaning."""
    empty = set()
    with open(path, encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            obj = json.loads(line)
            if not any(m and ARABIC_RE.search(m) for m in obj.get("meanings", [])):
                empty.add(obj["word"].upper())
    return empty

def main():
    wanted = load_sakhr_empty("data/dictionary.jsonl")
    found = {}  # word -> list of unique Arabic translations
    for line in sys.stdin:
        line = line.strip()
        if not line:
            continue
        try:
            d = json.loads(line)
        except json.JSONDecodeError:
            continue
        if d.get("lang_code") != "en":
            continue
        word = (d.get("word") or "").strip().upper()
        if word not in wanted:
            continue
        for t in d.get("translations", []) or []:
            if t.get("lang_code") != "ar":
                continue
            tw = (t.get("word") or "").strip()
            if not tw or not ARABIC_RE.search(tw):
                continue
            slot = found.setdefault(word, [])
            if tw not in slot and len(slot) < 12:
                slot.append(tw)
    with open("data/translations-en-ar.jsonl", "w", encoding="utf-8") as out:
        for word in sorted(found):
            out.write(json.dumps({"word": word, "meanings": found[word]}, ensure_ascii=False) + "\n")
    print(f"empty Sakhr records: {len(wanted)}", file=sys.stderr)
    print(f"filled from Wiktionary translations: {len(found)}", file=sys.stderr)

if __name__ == "__main__":
    main()
