# Data

- `legacy/sakhr.7z` - the original Sakhr Dictionary (قاموس صخر الجديد, "New
  Version 2006/2007") program archive, kept untouched as the source of truth.
- `words.txt` - the 59,427 English lemmas extracted from `GROUP.ENG` by
  `tools/extract_words.py` (stage 1 of the data pipeline).

The legacy data files use Windows-1256 for Arabic. The English word list is
stored as plain text; the Arabic meanings are materialized at lookup time by
the original program's engine, so they are extracted dynamically on a Windows
runner (see `extraction/`).
