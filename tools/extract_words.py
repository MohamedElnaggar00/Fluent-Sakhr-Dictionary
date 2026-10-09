#!/usr/bin/env python3
"""Extract the English lemma list from the legacy Sakhr Dictionary data.

GROUP.ENG layout (reverse-engineered 2026-10-09):
  - Lemma records live in the region from ~0x200000 to EOF.
  - Each record: 28-byte word field (uppercase word + 0x0A + zero padding),
    then a 3-byte little-endian pointer into the meaning region at the
    front of the file. Some records carry two extra zero bytes after the
    pointer (observed on CAT); the 28-byte field offset is stable.
Usage: python3 extract_words.py <GROUP.ENG> <output words.txt>
"""
import re, sys

def main(src, dst):
    data = open(src, 'rb').read()
    region = data[0x1f0000:]  # lemma records start at 0x1faf00 (ABACI); scan headroom included
    words = re.findall(rb"([A-Z][A-Z0-9 '\-\.]{1,24})\x0a", region)
    out, seen = [], set()
    for w in words:
        w = w.decode('ascii')
        if w not in seen:
            seen.add(w); out.append(w)
    with open(dst, 'w', encoding='ascii') as f:
        f.write('\n'.join(out) + '\n')
    print(f"{len(out)} unique lemmas -> {dst}")
    print("first:", out[:5])
    print("last:", out[-5:])

if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2])
