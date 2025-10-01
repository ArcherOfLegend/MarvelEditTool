# Format Inventory

This document summarizes the binary assets currently handled by the legacy C# tooling in this repository. Each section records the read/write entry points, container layout, and field-level references so that the upcoming Rust port can mirror the exact semantics.

## Shared Marvel table container (`*.CHS`, `*.CAC`, `*.ATI`, ...)

| Aspect | Notes |
| --- | --- |
| Reader / writer entry points | `TableFile.LoadFile` deserializes the table shell while dispatching entry payloads, and `TableFile.WriteFile` performs the inverse serialization.【F:MarvelData/TableFile.cs†L57-L225】【F:MarvelData/TableFile.cs†L747-L867】 |
| Endianness | The code relies on `BinaryReader`/`BinaryWriter` defaults (little-endian) for all integer and pointer fields.【F:MarvelData/TableFile.cs†L85-L225】 |
| Record table layout | Header A (8 bytes) → entry count (`uint32`) → header B (4 bytes) → pointer table of `count` records (`uint32 index`, `uint32 offset`) pointing to payload blobs → optional name chunk gated by `0x0043564D` (`"MVC\0"`) magic → contiguous payload blobs → 16-byte footer.【F:MarvelData/TableFile.cs†L85-L225】 |
| Pointer semantics | Offsets in the pointer table are absolute file positions; payload lengths are inferred from the next populated pointer or the file footer. The write path rebuilds the same layout while recomputing offsets sequentially.【F:MarvelData/TableFile.cs†L170-L224】【F:MarvelData/TableFile.cs†L807-L856】 |
| Alignment / padding | Entries are packed back-to-back; there is no automatic alignment beyond the implicit 4-byte granularity of index/offset pairs. The only fixed padding is the trailing 16-byte footer preserved verbatim.【F:MarvelData/TableFile.cs†L191-L225】【F:MarvelData/TableFile.cs†L858-L867】 |
| String encoding | Optional entry names are stored as null-terminated strings parsed with `SSFIVAEDataTools.SlurpString`, which reads `BinaryReader.ReadChar()` until `0x00`. This effectively treats the metadata as UTF-8 / ASCII with `MVC\0` magic.【F:MarvelData/TableFile.cs†L155-L167】【F:MarvelData/AEDataTools.cs†L16-L34】 |

## AnmChr command tables (`*.CAC`)

### Entry points

- Table shell: `TableFile.LoadFile(..., entryType: typeof(AnmChrEntry))` materializes individual animation records from the shared container.【F:MarvelData/TableFile.cs†L113-L225】  
- Payload parsing: `AnmChrEntry.SetData` reads the animation header and per-time-slice command tables; `AnmChrEntry.GetData` reconstructs the byte-perfect payload.【F:MarvelData/AnmChrEntry.cs†L65-L143】  
- Sub-record handling: `AnmChrSubEntry.SetData` and `WriteData` manage the per-frame command blocks referenced from each entry.【F:MarvelData/AnmChrSubEntry.cs†L161-L236】

### Entry header

| Field | Type | Description |
| --- | --- | --- |
| `subcount_minus_one` | `int32` | Number of timeline entries minus one; reader adds one to recover the actual count before iterating pointer pairs.【F:MarvelData/AnmChrEntry.cs†L75-L101】 |
| `animTime` | `int32` | Duration metadata copied verbatim; retained for round-trip fidelity.【F:MarvelData/AnmChrEntry.cs†L76-L79】 |
| `unk08`, `unk0C` | `int32` each | Unknown control words preserved without modification.【F:MarvelData/AnmChrEntry.cs†L78-L79】 |
| Pointer table | `subcount` × (`int32 index`, `uint32 offset`) | Each record stores the timeline index followed by a relative offset to a sub-entry blob. Offsets are relative to the start of the AnmChr payload (`pointer` starts at `16 + count * 8`).【F:MarvelData/AnmChrEntry.cs†L81-L132】 |

### Sub-entry (`AnmChrSubEntry`) layout

| Field | Type | Notes |
| --- | --- | --- |
| `localindex` | `int32` | Frame index; validated against `tableindex` when parsing.【F:MarvelData/AnmChrSubEntry.cs†L165-L206】 |
| `subcount` | `int32` | Number of command blocks referenced by this timeline entry.【F:MarvelData/AnmChrSubEntry.cs†L170-L206】 |
| `unk08`, `unk0C` | `int32` | Reserved words copied verbatim.【F:MarvelData/AnmChrSubEntry.cs†L170-L212】 |
| Command pointer table | `subcount` × (`uint32 offset`, `int32 id`) | Offsets are relative to the start of the sub-entry block; IDs label each command stream.【F:MarvelData/AnmChrSubEntry.cs†L177-L218】 |
| Command payloads | `byte[]` | Raw command streams read in-place; lengths are inferred from consecutive offsets or the caller-provided `nextPointer`. Data is emitted byte-for-byte on save.【F:MarvelData/AnmChrSubEntry.cs†L183-L236】 |

### Format traits

| Aspect | Details |
| --- | --- |
| Endianness | All integers use little-endian encoding via `BinaryReader` / `BinaryWriter`. Pointer math assumes 4-byte granularity but accepts arbitrary payload lengths.【F:MarvelData/AnmChrEntry.cs†L75-L138】 |
| Alignment | Sub-entry offsets are computed by summing serialized sizes, so command payloads are packed without extra padding. The enclosing table container ensures 4-byte alignment of entry offsets.【F:MarvelData/AnmChrEntry.cs†L124-L138】【F:MarvelData/TableFile.cs†L807-L856】 |
| Strings | No inline strings; names come from the shared metadata block or are derived externally. Command pretty-printing relies on dictionary lookups, not serialized text.【F:MarvelData/AnmChrSubEntry.cs†L294-L335】 |
| Magic values | Disabled entries are detected via `localindex >= 21011`, matching the editor’s semantics. No explicit signature inside the payload beyond counts.【F:MarvelData/AnmChrSubEntry.cs†L275-L292】 |

## MSD message tables (`*.MSD`)

| Aspect | Notes |
| --- | --- |
| Entry points | `MSDFile.LoadFile`/`WriteFile` own the full binary read/write cycle for MSD resources.【F:MarvelData/MSDFile.cs†L14-L135】 |
| Endianness | Header words (length, count) are read using the little-endian `BinaryReader` defaults.【F:MarvelData/MSDFile.cs†L26-L40】 |
| Header | 4-byte signature preserved verbatim (`header`), followed by a little-endian `int32` string count.【F:MarvelData/MSDFile.cs†L38-L40】 |
| Record layout | For each string: `int16` length, then `length` repetitions of `(byte valueMinus0x20, byte zeroPad)`, followed by sentinel `0xFFFF`. Byte `0x1E` maps to CRLF when inflating.【F:MarvelData/MSDFile.cs†L43-L80】 |
| String encoding | Characters are stored as `ASCII code - 0x20` with a trailing zero byte per glyph; newline is encoded as `0x1E`. Writer reverses the transform before emitting.【F:MarvelData/MSDFile.cs†L45-L128】 |
| Magic numbers / padding | Each entry terminates with `0xFFFF`, and there is no global footer. No additional alignment beyond the 2-byte fields.【F:MarvelData/MSDFile.cs†L75-L129】 |

## Status parameter blocks (`*.CHS`)

| Aspect | Notes |
| --- | --- |
| Entry points | The shared table loader instantiates `StructEntry<StatusChunk>` for `.CHS` assets, relying on `structTypes`/`structSizes` for size validation.【F:MarvelData/TableFile.cs†L49-L214】 |
| Layout | `StatusChunk` is a packed `StructLayout(LayoutKind.Sequential)` value with a fixed serialized size of `0x350` bytes (validated via `VerifySizes`).【F:MarvelData/TableFile.cs†L49-L214】【F:MarvelData/TableFile.cs†L1238-L1264】 |
| Fields | The struct enumerates ~0xD5 32-bit integers/floats covering health, meter, X-Factor tuning, face groups, etc. All fields serialize in declaration order without padding beyond natural 4-byte alignment.【F:MarvelData/MVC3DataStructures.cs†L2106-L2321】 |
| Endianness | Interpreted as little-endian integers/floats via .NET marshaling on read/write (`StructEntry<T>`).【F:MarvelData/StructEntry.cs†L23-L118】 |
| Strings / magic | No embedded strings or signatures; integrity checks rely on the surrounding table header. |

## BinaryReader / BinaryWriter usage overview

| File | Method(s) | Parsed / serialized records |
| --- | --- | --- |
| `TableFile.cs` | `LoadFile`, `WriteFile`, `ReadShotFile` | Container headers, pointer tables, optional name blocks, payload blobs (incl. SHT special cases).【F:MarvelData/TableFile.cs†L57-L867】 |
| `TableEntry.cs` | `Import`, `Export` | Generic helpers that read/write entire payload blobs for individual entries.【F:MarvelData/TableEntry.cs†L568-L607】 |
| `AnmChrEntry.cs` | `SetData`, `GetData` | Animation headers, sub-entry pointer tables, and per-frame command payloads.【F:MarvelData/AnmChrEntry.cs†L65-L143】 |
| `AnmChrSubEntry.cs` | `SetData`, `WriteData`, `Copy` | Timeline sub-entry headers and command byte arrays, including offset tables.【F:MarvelData/AnmChrSubEntry.cs†L161-L260】 |
| `MSDFile.cs` | `LoadFile`, `WriteFile` | Message table headers, per-string records with custom 0x20-offset encoding.【F:MarvelData/MSDFile.cs†L14-L135】 |
| `TableEntry.cs` | `ImportBytes` (via overrides) | Delegates to format-specific readers after pulling raw buffers from disk.【F:MarvelData/TableEntry.cs†L572-L590】 |

