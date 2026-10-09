# Fluent Sakhr Dictionary

The classic Sakhr Dictionary (قاموس صخر) rebuilt as a modern Windows app:
native WinUI 3, Mica, instant search, full RTL support - the same design DNA
as Fluent Prayer Times and Fluent Vantage Toolbar.

## The app

- WinUI 3, native Fluent: Mica backdrop, smooth animations, light/dark theme
- Instant prefix search over all 59,427 lemmas, fully offline
- 125,896 Arabic meanings with full diacritics, rendered correctly
  (the original 1996 program could not even display them properly)
- Two-pane layout echoing the original: live word list on the left,
  red Arabic meanings (RTL) on the right
- English interface with formal Arabic content

### Download

Grab the latest release:
[Releases](https://github.com/MohamedElnaggar00/Fluent-Sakhr-Dictionary/releases)

Three flavors: setup (recommended), portable zip, and a small
.NET-runtime-dependent installer.

### Build from source

```
dotnet publish src/FluentSakhrDictionary/FluentSakhrDictionary.csproj -c Release -r win-x64
```

Requires .NET 8 SDK and the Windows App SDK 1.6 workload. The app runs
framework-dependent: the Windows App Runtime installer is bundled in setup.

## The data

`data/dictionary.jsonl` holds the full extracted dataset: 59,427 records
aligned 1:1 with the original program's English word list, with the Arabic
meanings captured from the original 1996 program itself (see `extraction/`).
45,693 lemmas resolve to meanings; the remaining 13,734 are rare inflections
and proper nouns absent from the original program's own search index.
The original program lives on untouched in `data/legacy/sakhr.7z`.

## Credits

Developer: Mohamed Elnaggar

Contributors: app.instinct

brought to you by app.instinct AI

## License

MIT

## Data sources

- **English -> Arabic:** the original 1996 Sakhr dictionary data, extracted from the user's own copy (59,427 lemmas, 125,896 meanings). See `extraction/` and `tools/`.
- **Arabic -> English:** Arabic entries from English Wiktionary, machine-extracted by wiktextract and published at [kaikki.org](https://kaikki.org/dictionary/Arabic/) (36,627 entries, 60,395 glosses). License: [CC BY-SA 4.0](https://creativecommons.org/licenses/by-sa/4.0/) - see `data/LICENSE-wiktionary-ar.txt`. Regenerate with `python3 tools/extract_wiktionary.py <kaikki-dump.jsonl> data/wiktionary-ar.jsonl`.

The application source code is MIT licensed; the Wiktionary-derived data file ships under CC BY-SA 4.0 with attribution.
