---
name: evtxecmd-maps
description: Understand, author, and validate EvtxECmd map files that normalize Windows event log EventData into first-class CSV/JSON columns.
---

# EvtxECmd Maps

This skill describes how an agent should understand, create, and validate
EvtxECmd map files in this repository (the files under `evtx/Maps/` ending in
`.map`). Map files are YAML documents that tell EvtxECmd how to extract values
from an event's `EventData` (and optionally `System`) elements and project them
into a small set of standardized columns (`UserName`, `RemoteHost`,
`ExecutableInfo`, `PayloadData1` … `PayloadData6`).

A starter template is included alongside this skill at
`assets/!Channel-Name_Provider-Name_EventID.template`. **Always start a new
map by copying that template** rather than writing one from scratch.

---

## 1. What a map file is

A map is a YAML file whose filename and four header fields together identify a
specific event:

| Header field   | Required | Meaning |
| -------------- | -------- | ------- |
| `Author`       | optional | Name / contact of the map author. |
| `Description`  | required | Human description of the event. No trailing period. |
| `EventId`      | required | The integer Event ID (matches `<EventID>` in the XML). |
| `Channel`      | required | The exact value from the `<Channel>` element. |
| `Provider`     | **required** | The exact value of `<Provider Name="…">`. Mandatory since December 2020 to disambiguate event IDs that are reused by multiple providers. |

Below the header is a `Maps:` sequence describing how to build each output
column, and an optional `Lookups:` sequence defining value-translation tables.

The map is matched to an event when **all of** `Channel`, `Provider`, and
`EventId` match the event's XML. The filename does not select the map, but it
must follow the naming rules below so duplicates can be detected.

### Filename rules

Format:

```
<Channel-Name>_<Provider-Name>_<EventID>.map
```

- Underscores (`_`) separate the three elements.
- Hyphens (`-`) replace **any** spaces, slashes, or special characters within
  Channel and Provider names.
- The extension must be `.map` (lowercase is used throughout the repo).
- Filenames may be long; that is expected.

Example — for `Channel = Microsoft-Windows-TaskScheduler/Operational`,
`Provider = Microsoft-Windows-TaskScheduler`, `EventID = 201`:

```
Microsoft-Windows-TaskScheduler-Operational_Microsoft-Windows-TaskScheduler_201.map
```

To override an existing default map without losing your changes on update,
prepend `1_` to the filename. Maps load alphabetically, so the `1_…` copy wins:

```
1_Security_Microsoft-Windows-Security-Auditing_4624.map
```

---

## 2. Authoring workflow

1. **Get real XML for the event.** Run EvtxECmd against a sample log to dump
   the records to XML:

   ```
   EvtxECmd.exe -f <your eventlog.evtx> --xml c:\temp\xml
   ```

   Open the resulting XML and locate the event of interest. Note the values of
   `<Channel>`, `<Provider Name="…">`, and `<EventID>`, and inspect the
   `<EventData>` block for the `<Data Name="…">` fields you want to surface.

2. **Copy the template.** Start from
   `assets/!Channel-Name_Provider-Name_EventID.template` (also located at
   `evtx/Maps/!Channel-Name_Provider-Name_EventID.template`). Save it under
   `evtx/Maps/` using the filename rules above.

3. **Fill in the header.** Set `Author`, `Description`, `EventId`, `Channel`,
   `Provider`. There must be **no blank line between `Provider:` and `Maps:`**.

4. **Define each `Maps:` entry.** Each entry produces one output column:
   - `Property:` — must be **exactly one of**
     `UserName`, `RemoteHost`, `ExecutableInfo`,
     `PayloadData1`, `PayloadData2`, `PayloadData3`,
     `PayloadData4`, `PayloadData5`, `PayloadData6`.
     Use this exact casing (e.g. `UserName`, never `Username`).
   - `PropertyValue:` — the rendered string, with `%name%` placeholders for
     every variable referenced in `Values`.
   - `Values:` — list of `{ Name, Value }` pairs. `Name` must match a
     `%name%` placeholder. `Value` is an XPath expression, normally
     `"/Event/EventData/Data[@Name=\"<FieldName>\"]"`. You can also index by
     position: `/Event/EventData/Data[1]` (first `<Data>` node), `[2]`, etc.
   - `Refine:` (optional, on a `Values` entry) — a regex applied to the XPath
     result to extract a substring. Example:
     `Refine: "IPv4 address: [0-9]{1,3}.[0-9]{1,3}.[0-9]{1,3}.[0-9]{1,3}"`.
   - **Delete any `Property` blocks you don't need.** Not every event has
     enough data to fill all six `PayloadDataN` columns.
   - When organizing `PayloadData1`…`PayloadData6` for an event that's similar
     to existing maps (e.g. Sysmon), follow the column order used by those
     maps for analyst consistency.

5. **Quoting and escaping.** XPath strings must be wrapped in double quotes,
   and embedded quotes inside the XPath escaped with `\"`. `PropertyValue`
   strings that contain spaces, colons, or backslashes should be quoted, and a
   literal backslash is written as `\\` (e.g. `"%domain%\\%user%"`).

6. **Lookups (optional).** Use a `Lookups:` block to translate raw values
   (often numeric codes) into human-readable strings. The lookup is applied
   when its `Name` matches the `Name` of a `Values` entry. To keep both raw
   and translated values in the same column, reference the field twice with
   different `Name`s — only the one whose name equals a `Lookups` `Name` is
   translated:

   ```yaml
   Lookups:
     -
       Name: WakeSourceType
       Default: Unknown code
       Values:
           0: Unknown
           1: Power button
           3: Waking from sleep to hibernate
   ```

   If the map has multiple lookup tables, **nest them all under a single
   `Lookups:` key** (see `Security_Microsoft-Windows-Security-Auditing_4769.map`
   and `…_4771.map` for examples).

7. **Documentation footer.** End the file with two commented blocks:
   - `# Documentation:` — one URL per line (Microsoft docs, blogs, research),
     each line commented with `#`. Use `# N/A` if there is none.
   - `# Example Event Data:` — a sanitized copy of a real `<Event>…</Event>`
     XML block, every line prefixed with `#`. Remove any sensitive data.
   - The file must end with a single trailing newline (the `.yamllint` rule
     `new-line-at-end-of-file` is enabled).

8. **Test against real data.** Run EvtxECmd on the source log and confirm the
   target columns are populated as expected before submitting.

---

## 3. Reference structure

This is the canonical shape of a map. The full template is in
`assets/!Channel-Name_Provider-Name_EventID.template`.

```yaml
Author: Your name <you@example.com>
Description: Short description of the event
EventId: 4624
Channel: Security
Provider: Microsoft-Windows-Security-Auditing
Maps:
  -
    Property: UserName
    PropertyValue: "%domain%\\%user%"
    Values:
      -
        Name: domain
        Value: "/Event/EventData/Data[@Name=\"SubjectDomainName\"]"
      -
        Name: user
        Value: "/Event/EventData/Data[@Name=\"SubjectUserName\"]"
  -
    Property: PayloadData1
    PropertyValue: "LogonType %LogonType%"
    Values:
      -
        Name: LogonType
        Value: "/Event/EventData/Data[@Name=\"LogonType\"]"
Lookups:
  -
    Name: LogonType
    Default: Unknown
    Values:
        2: Interactive
        3: Network
        4: Batch
        5: Service

# Documentation:
# https://learn.microsoft.com/...
#
# Example Event Data:
#  <Event xmlns="http://schemas.microsoft.com/win/2004/08/events/event">
#  ...
#  </Event>
```

---

## 4. Validating a map file

Before committing a new or modified map, an agent **must** validate it. CI
runs the same check via `.github/workflows/verify.yml`, which invokes
`yamllint evtx/Maps` using the rules in the repository's `.yamllint`. The
`.yamllint` `yaml-files` list explicitly includes `*.map`, so map files are
linted as YAML.

### 4.1 Structural / lint validation

Run from the repository root:

```bash
pip install yamllint
yamllint evtx/Maps
```

To target only the file you are editing:

```bash
yamllint evtx/Maps/<your-file>.map
```

The repo's `.yamllint` enforces (failures, not warnings):

- `braces`, `brackets`, `colons`, `commas`, `hyphens`, `indentation` — correct
  YAML punctuation and 2-space block indentation consistent with neighbors.
- `empty-lines` — no stray blank lines (in particular, **no blank line between
  `Provider:` and `Maps:`**).
- `key-duplicates` — every key in a mapping must be unique. In a map file,
  this means each `Property:` value (`UserName`, `PayloadData1`, …) may appear
  **at most once** in `Maps:`, and each `Name:` inside a single `Values:` list
  must be unique.
- `trailing-spaces` — no spaces at end of line.
- `new-line-at-end-of-file` — file must end with exactly one `\n`.

`comments`, `comments-indentation`, and `truthy` are configured as warnings
and will not fail CI, but should still be addressed when easy.

If `yamllint evtx/Maps` exits 0, the structural check passes.

### 4.2 Semantic checks the linter cannot catch

After `yamllint` is clean, also confirm by inspection:

- **Filename matches headers.** Filename is
  `<Channel>_<Provider>_<EventId>.map` with `/`, spaces, and special chars in
  Channel/Provider replaced by `-`. The Channel/Provider/EventId in the
  filename match the corresponding header fields exactly.
- **All required headers are present and non-placeholder:** `Description`,
  `EventId`, `Channel`, `Provider`. `Description` has no trailing period.
- **`Property` values are from the allowed set** with exact casing:
  `UserName`, `RemoteHost`, `ExecutableInfo`, `PayloadData1`–`PayloadData6`.
- **No duplicate `Property` entries** within `Maps:`.
- **Every `%name%` placeholder in `PropertyValue`** has a matching `Name`
  entry in that block's `Values:`, and vice versa (no orphan variables, no
  unresolved placeholders).
- **XPaths are quoted** with `"…"` and embedded `"` escaped as `\"`.
  Backslashes in `PropertyValue` strings are doubled (`\\`).
- **`Lookups:` is a single top-level key** (all lookup tables are nested
  under it), each with `Name`, `Default`, and `Values` of integer/string
  pairs. A lookup `Name` matching a `Values` `Name` is what triggers
  translation.
- **Footer is present:** a `# Documentation:` block (URLs or `N/A`) and a
  `# Example Event Data:` block containing a real, sanitized XML sample.
- **File ends with a trailing newline.**
- **Test on real data:** run EvtxECmd against a sample `.evtx` and confirm
  the produced CSV/JSON columns are populated correctly.

If any of the above fails, fix the map and re-run `yamllint evtx/Maps`
until it is clean.

---

## 5. Quick checklist for an agent creating a new map

1. Dump the source `.evtx` to XML and locate the target event.
2. Copy `assets/!Channel-Name_Provider-Name_EventID.template` to
   `evtx/Maps/<Channel>_<Provider>_<EventId>.map`.
3. Set `Author`, `Description`, `EventId`, `Channel`, `Provider`.
4. Replace the template's `Maps:` blocks with `Property` entries that match
   the actual event; delete unused `PayloadDataN` blocks.
5. Add `Lookups:` only if codes need translating; keep all tables under one
   `Lookups:` key.
6. Replace the template footer with real `# Documentation:` URLs and a
   sanitized `# Example Event Data:` XML sample.
7. Ensure the file ends with a single newline and contains no trailing
   whitespace and no blank line between `Provider:` and `Maps:`.
8. Run `yamllint evtx/Maps` and fix any errors.
9. Run the map against real data with EvtxECmd and confirm output columns.
