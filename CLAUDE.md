# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

- **`evtx`**: a `netstandard2.0` NuGet library (package id `evtx`) that parses Windows event logs (MS-EVEN6).
- **`EvtxECmd`**: the command line tool (`net472;net10.0`) that uses it. Its CSV, JSON and XML output is what people compare and script against, so keep column order and formats stable.
- **`evtx/Maps`**: the YAML maps (`Channel_Provider_EventID.map`) that EvtxECmd uses to fill PayloadData1-6 and the other columns. `--sync` downloads them from this repo's master branch. `.github/skills/evtxecmd-maps` describes how to write them, and `verify.yml` runs yamllint on them.

Input may be corrupt or hostile.

## Commands

The SDK is pinned in `global.json` (.NET 10). The library uses `LangVersion 10`. Tests target `net10.0` and use NUnit 5 + FluentAssertions 7 (pinned to `[7.2.2,8.0.0)`, since 8.x requires a commercial license).

```bash
dotnet build -c Release                                          # builds evtx.slnx (EvtxECmd for net472 and net10.0)
dotnet test -c Release                                           # CI; CorpusTests is [Explicit] and skipped
dotnet test -c Release --filter "FullyQualifiedName~CorpusTests" # every .evtx in ../Corpus/EventLogs (or EVTX_CORPUS), slow
dotnet test --filter "FullyQualifiedName~BinXmlTests"            # one fixture
dotnet pack -c Release                                           # only the library is packable
```

## Format and parser

- **File**: a 4 KB header (`ElfFile`, chunk count, flags, CRC of 0x00-0x77), then 64 KB chunks. `EventLog` reads the chunks and parses them in parallel batches (each chunk has its own string and template tables). `GetEventRecords` yields the records in file order. A chunk without the `ElfChnk` signature is skipped, and a short last chunk is padded with zeros.
- **Chunk** (`ChunkInfo`): a 512 byte header, then records up to the free space offset, or to the end of the chunk when that offset is bad. Parsing stops after the record whose number is the header's last record id; everything after it is slack. The header CRC covers 0x00-0x77 and 0x80-0x1FF; `DataCrc` covers the records. Both are only reported, never enforced.
- **Record**: `**\0\0`, size, record number, FILETIME, BinXml, then the size again. A record whose size does not fit stops the chunk. A record that throws goes to `ErrorRecords` (keyed by record number), and parsing continues with the next one.
- **Hidden records** (DanderSpritz's eventlogedit grows a record over the one it deletes): `ExtraDataOffset` is a `**` within 16 bytes after the BinXml ends (the old heuristic, kept for the CSV column). If a whole record with a different number is there, it is parsed and returned with `HiddenRecord`.
- **BinXml** (`BinXml/`): `BinXmlParser` builds nodes, `Renderer` substitutes template values into an `OutElement` tree, and the outputs are written from that tree.
  - Elements inside template definitions carry a 2 byte dependency id after the token. Record-level elements and nested BinXml (type 0x21) values do not. Forwarded events have no templates, so getting this wrong is what broke them (issue #15).
  - Names and template definitions are either inline (their offset is the current position) or references to an earlier offset in the chunk. Template definitions are cached per chunk by offset.
  - ValueText tokens are a type byte followed by the value (strings are length-prefixed).
  - MS-EVEN6 says an element whose dependency value is NULL is not rendered, but they are (as old EvtxECmd and the Rust parser do). Leaving them out dropped `<Binary></Binary>` and empty `<Data>` from many records.
  - CharRef, EntityRef, CDATA and PI tokens are supported.
- **Values** (`ValueFormatter`) keep the old EvtxECmd text formats: hex for HexInt, `0x..` for size_t, and SIDs read by their subauthority count. A FILETIME or SYSTEMTIME that is not valid is shown as hex. Runs of control characters become ", ". Real64 arrays are supported.

## Output

The old code built XML text and reloaded it into XmlDocument, XPathDocument and Json.NET. The new code writes all of these from the tree, and reproduces the old output byte for byte (except for fixes such as no double escaping of `&amp;`). Keep it that way: the corpus comparisons depend on it.
- `XmlTextOutput` matches XmlDocument's beautified output (`ConvertPayloadToXml`, `--xml`).
- `Payload` is the EventData/UserData XML with LF line breaks, as the old XmlReader extract had them.
- `JsonOutput` matches Json.NET's `SerializeXmlNode`: `@attr`, `#text`, arrays for repeated names, `null` for `<x/>` and `""` for `<x></x>`. The payload uses LF (`PayloadAsJson`); the whole event uses CRLF (`EventAsJson`, for `--fj`).
- `OutNavigator` is an XPathNavigator over the tree. Maps run their XPath queries against it, with the same values an XPathDocument of the event XML had: no xmlns attributes, whitespace-only text dropped, CRLF in text.
- `RenderingInfo` (from forwarded events) is JSON, and EvtxECmd writes it as the last CSV column.
- `EventLog.PreparePayloadJson`/`PrepareXml` build the JSON or XML while chunks are parsed in parallel.

## Maps

- `EventLog.LoadMaps` deserializes every `*.map` in parallel (YamlDotNet is most of a small log's run time), then, in name order, validates it (FluentValidation), and keys it by `EventId-Channel-Provider`. It returns true when a map had errors. A second map for the same key is skipped with a warning (issue #241 was a check that left out the provider, so it threw).
- A map's `Lookups` translate values; `Maps` combine XPath `Values` into a property with `PropertyValue` (`%name%` placeholders).

## Error model

- `EventLogParseException` (`Offset`, `Field`) means the file is not an event log ("Invalid signature! Expected 'ElfFile'").
- Everything else is logged (Serilog) and listed in `EventLog.ErrorRecords`/`ChunkInfo.ErrorRecords`, and parsing continues.
- EvtxECmd returns 1 when an argument is bad, a file fails, or output can't be written, and 0 otherwise.

## Tests

`evtx.Test` (net10.0) references the library and EvtxECmd. `TestFiles/` and the maps are copied to the output.
- `BinXmlTests` (with `EvtxBuilder`/`ChunkBuilder`, which build chunks token by token): templates, nested BinXml, forwarded-style records, references, hidden records, bad input. Add regression tests here.
- `SampleTests`: the gzipped samples from omerbenamram/evtx (MIT) in `TestFiles/rust-evtx` (see the README there). The record counts match the Rust `evtx_dump`.
- `CliTests`: EvtxECmd output and exit codes. `MapTests`: every map in `evtx/Maps` loads, with no duplicates.
- `CorpusTests`: every `.evtx` in a local collection that is not in the repo, `Corpus/EventLogs` next to the repo (or `EVTX_CORPUS`). Never copy its files into the repo.
- Validation against the old EvtxECmd and the Rust parser over the corpus plus the DFIR Artifact Museum (1,039 logs): the counts match Rust (1,614,510 records vs 1,614,500; the 10 extra are in a ForwardedEvents chunk that Rust drops), and payload JSON and map fields match the old Json.NET/XPathDocument results for every record.

## Versioning and release

- The library's version comes from the git tag through MinVer (bare tags like `2026.10.0`). There is no `<Version>` in its csproj. The 2026.5.0 tag is not in master's history, so MinVer ignores it.
- EvtxECmd has `<Version>` in its csproj, bumped by hand. It is not packed.
- To release the library: push to `master`, then `git tag <ver> && git push origin <ver> && gh release create <ver> --verify-tag --generate-notes`.
- `.github/workflows/publish.yml` is identical to GuidMapping's. It packs, validates and tests on PRs and on pushes to `master`. When a GitHub Release is published, it pushes to NuGet.org with trusted publishing (`NuGet/login` with the `NUGET_USER` repo variable, `EricRZimmerman`).
- Dependabot (`.github/dependabot.yml`) opens monthly PRs for GitHub Actions and NuGet updates.
