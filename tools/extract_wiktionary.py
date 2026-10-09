#!/usr/bin/env python3
"""Filter the kaikki.org Arabic dump (English Wiktionary, CC BY-SA 4.0) into a compact
Arabic->English JSONL for the app: one line per Arabic entry with word, pos, glosses.

Source: https://kaikki.org/dictionary/Arabic/kaikki.org-dictionary-Arabic.jsonl
License of the source data and of the output file: CC BY-SA 4.0 (Wiktionary), attribution
to "English Wiktionary via kaikki.org / wiktextract" - see data/LICENSE-wiktionary-ar.txt.

Usage: python3 tools/extract_wiktionary.py /path/to/kaikki.org-dictionary-Arabic.jsonl data/wiktionary-ar.jsonl
"""
import json
import sys


def main(src: str, dst: str) -> None:
    kept = gloss_count = 0
    with open(src, encoding="utf-8") as f, open(dst, "w", encoding="utf-8") as out:
        for line in f:
            line = line.strip()
            if not line:
                continue
            try:
                d = json.loads(line)
            except json.JSONDecodeError:
                continue  # tail of a range-cut download, or a blank chunk
            if d.get("lang_code") != "ar":
                continue
            word = (d.get("word") or "").strip()
            pos = (d.get("pos") or "").strip()
            glosses = []
            for sense in d.get("senses", []):
                for g in sense.get("glosses", []):
                    g = g.strip()
                    if g and g not in glosses:
                        glosses.append(g)
            if not word or not glosses:
                continue
            out.write(json.dumps({"word": word, "pos": pos, "glosses": glosses}, ensure_ascii=False) + "\n")
            kept += 1
            gloss_count += len(glosses)
    print(f"kept {kept} Arabic entries with {gloss_count} English glosses")


if __name__ == "__main__":
    if len(sys.argv) != 3:
        sys.exit("usage: extract_wiktionary.py <kaikki-dump.jsonl> <output.jsonl>")
    main(sys.argv[1], sys.argv[2])
