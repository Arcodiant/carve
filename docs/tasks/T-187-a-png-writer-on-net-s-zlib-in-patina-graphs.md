---
description: >-
  Patina.Graphs gains PngWriter: RGBA8 PNGs with the up filter, bands of 64 rows deflated in parallel on .NET's zlib and stitched, the same bytes at any thread count, with a canary test that pins them to .NET 10's zlib
---
# T-187 — A PNG writer on .NET's zlib in Patina.Graphs

**Status:** Done

**Pri:** P1

**Milestone:** M-33

**Batch:** — (standalone)

**Depends on:** — (none)

**Verify:** patina-unit

## Goal

`Patina.Graphs` has a PNG writer of its own on `System.IO.Compression`, tested and pinned, that nothing calls yet.
T-188 moves `Png.Encode` and `export` onto it. It is kept apart so that this task lands with every existing
byte-for-byte check still green.

## Scope

- **A new file, `src/Patina.Graphs/Imaging/PngWriter.cs`**, a public static `PngWriter`:
  - `Write(Stream target, ReadOnlySpan<byte> rgba, int width, int height)`, and `Encode(...)` returning a `byte[]`
    that is the same bytes `Write` puts on any stream (a `MemoryStream`, a `FileStream`).
  - It refuses a length that is not `width * height * 4`, as `Png.Encode` does today.
  - **Do not touch `Png.Encode`, `OutputPacking.cs`, `ExportTool.cs` or the csproj.** They are T-188's.
- **The format, decided at planning. Every item changes the bytes, so none is the implementer's to change:**
  - RGBA8: bit depth 8, colour type 6, no interlace. Chunks `IHDR`, `IDAT`, `IEND` only.
  - Every row has filter type 2 (`up`). Row 0 is filtered against a row of zeros. Row `y` is filtered against row
    `y - 1` of the image, also where `y` starts a band.
  - Rows go in **bands of 64** (the last band may be shorter). Each band is deflated by a `DeflateStream` of its own
    at `CompressionLevel.Optimal`, with **one `Write` call per filtered row** (the filter byte and the row together)
    and then one `Flush()`, so the band ends on a byte boundary with no final block.
  - The zlib stream is the two bytes `78 01`, each band's bytes in order, the empty final block `03 00`, and the
    Adler-32 of all the filtered rows, big-endian. The Adler-32 is worked per band and combined, as zlib's
    `adler32_combine` does.
  - Bands are deflated on the thread pool in **batches of 32 bands** and written in order. Each batch is **one `IDAT`
    chunk**: the first also carries `78 01`, the last also carries `03 00` and the Adler-32. So the writer holds one
    batch's compressed bands and never the whole PNG.
  - A CRC-32 on every chunk, from a table made in code.
- **A working copy to start from:** `artifacts/png-bench/Program.cs` in the container's `branches/main` (ignored by
  git, so not in a task worktree; read it by its absolute path). Its `Fast.Encode` with `parallel: true` is this
  format, except that it holds the whole stream and writes one `IDAT`. Its `FilterRow`, `Adler`, `AdlerCombine`,
  `Crc` and `Chunk` are correct: every variant decoded to its texels at 2048 and 8192.
- **Tests, a new `tests/Patina.Graphs.Tests/PngWriterTests.cs`** (`unit`). Each new check is shown failing once on a
  broken copy of the writer, and the As built says which break each one caught:
  - **Decodes to its texels** through `StbImageSharp`: 1 x 1, 1 x 300, 300 x 1, 64 x 63, 64 x 64, 64 x 65, a
    non-square image, all zeros, hash noise that does not compress, and one image of more than 2048 rows, so two
    `IDAT` chunks are written.
  - **The stream is sound where `StbImageSharp` does not look** (it checks neither the CRCs nor the Adler-32): walk
    the chunks, check each CRC against a CRC-32 written in the test, join the `IDAT` data and inflate it with a
    decompressing `ZLibStream`, which checks the Adler-32, and compare the bytes with the filtered rows.
  - **The same bytes** from `Encode` and from `Write` into a `FileStream`; from two calls; and with the pool held to
    one thread and to many. If that needs a parameter, make it an optional thread limit on `Write`.
  - **The canary:** the SHA-256 of the PNG of one fixed image worked from integers (about 96 x 200, so several bands)
    is pinned in the test. Its failure message says that this runtime's zlib writes bytes other than those of the
    committed sample PNGs, names `Environment.Version`, and says not to pin the hash again but to stop and ask.
  - **A table of broken copies** each failing a test: filter byte 0 with `up` data, a band's first row filtered
    against zeros, a wrong Adler combine, a missing final block, a wrong CRC.
- `PatinaGraphsIsEngineFreeTests` still passes. `System.IO.Compression` and `System.Threading.Tasks.Parallel` are the
  runtime's, not packages.
- Nothing a user or an agent can reach changes, so `README.md` is not touched. Say so in the As built.

## Notes

- **Design-heavy, and it decides a format the next two tasks build on.** T-188 commits 35 PNGs written by this code.
- **Decided at planning by the person (2026-11-22):** a PNG's bytes may depend on the .NET runtime, held by the
  canary; RGBA stays, so a later alpha output needs no new format; bands are deflated in parallel.
- **What the planning probes found** (`artifacts/m33-probe/` in the container's `branches/main`: `Program.cs`,
  `bands/Program.cs`, `out-*.tsv`):
  - `ZLibStream`'s bytes are the same on 10.0.13 and 10.0.15, and differ between .NET 8, 9 and 10 at every level but,
    for most images, `SmallestSize`.
  - The bytes change with how the stream is fed: one `Write` a row and one `Write` of every row give different
    streams at `Optimal` and `Fastest`. So the feeding rule above is part of the format.
  - The banded form gave the same bytes at 1, 5 and 16 threads and on 10.0.13 and 10.0.15 for 16 images, and each
    inflated to its filtered rows.
  - **Not probed:** another machine. The canary's first run on the runners is that test. If it fails on a runner and
    passes here, stop and ask.
- **Goes back to the person, not into the code:** any change to the format list; the canary failing anywhere; a
  test that can only pass by decoding where the plan says bytes.
- Measure `Encode` on the planks at 8192 once, in Release, and write the time beside the bench's 0.15 s for four
  outputs. T-189 measures the whole export.
- **Write down in `### As built` for T-188:** the exact signatures; whether a thread limit exists; the canary's hash
  and the runtime it was taken on; the measured time.
- Paths and C# strings written through a shell lose backslashes. Write files with the file tools, and check any
  backslash that matters.

### As built

Built by Claude Opus 5.5 (2026-11-22).

- **`src/Patina.Graphs/Imaging/PngWriter.cs`**, a public static `PngWriter`, to the format list as written (nothing in
  it changed). Signatures, for T-188:
  - `public static byte[] Encode(ReadOnlySpan<byte> rgba, int width, int height)` (a `Write` into a `MemoryStream`).
  - `public static void Write(Stream target, ReadOnlySpan<byte> rgba, int width, int height, int maxDegreeOfParallelism = -1)`.
  - `public const int BandRows = 64`, `public const int BandsPerBatch = 32`.
  - It refuses a length other than `width * height * 4` with `ArgumentException("an RGBA8 image is four bytes a
    texel", "rgba")`, as `Png.Encode` does; also a width or height under 1, and a thread limit of 0 or under -1.
- **A thread limit exists:** `Write`'s optional `maxDegreeOfParallelism` (-1, the default, leaves it to the pool), passed
  to `Parallel.For`'s `ParallelOptions`. It never changes the bytes; `Encode` has none.
- **How it holds memory.** A span cannot reach the pool's threads, so each batch's rows (up to 2048) are copied into one
  array after the row above the batch (zeros above row 0), and each band is filtered from that copy into a pooled
  buffer on the thread that deflates it. So `Write` holds one batch's raw rows (64 MB at 8192) and its compressed
  bands, never the whole PNG.
- **Faults for the table of broken copies** are an `internal enum PngWriterFault` and an internal six-argument `Write`
  overload; the file carries `[assembly: InternalsVisibleTo("Patina.Graphs.Tests")]`, since the csproj is T-188's.
- **`tests/Patina.Graphs.Tests/PngWriterTests.cs`** (`unit`), and which break each check caught:
  - *Decodes to its texels* (`StbImageSharp`; 1 x 1, 1 x 300, 300 x 1, 64 x 63, 64 x 64, 64 x 65, 97 x 211, zeros,
    hash noise, 5 x 2100 with two `IDAT`s): caught `FilterByteNone` (filter byte 0 on `up` data) and
    `BandStartAgainstZeros` (a band's first row filtered against zeros). `StbImageSharp` also refused `NoFinalBlock`
    ("bad huffman code") and accepted the wrong Adler and the wrong CRCs, as the plan said.
  - *The stream is sound* (the same images; chunk walk, a bitwise CRC-32 written in the test, the `IHDR` bytes, one
    `IDAT` a batch, `78 01`, a tail of `03 00` and four bytes, then `ZLibStream` inflating to the test's own filtered
    rows): caught `WrongCrc` (each CRC worked without the chunk type) by its CRC, `WrongAdlerCombine` by `ZLibStream`
    (an `InvalidDataException`, whose message wrongly says "an unsupported compression method"), and `NoFinalBlock` by
    the tail check and the rows' comparison. **`ZLibStream` does not notice a missing final block**: it inflated the
    stream with two bytes of garbage after the rows and threw nothing, so the tail check is not redundant.
  - *Two `IDAT` chunks* past 2048 rows.
  - *The same bytes* from `Encode`, a `Write` into a `FileStream` (a temp file, deleted), a second call, and `Write` at
    1, 2, 5, 16 and the core count's threads: caught, by hand, a copy whose level depended on the thread limit
    (`Fastest` at 1 thread): "the bytes at 1 threads differ".
  - *The refusal* (15, 17 and 8 bytes): caught, by hand, a copy with the length check removed (17 bytes went through).
  - *The canary*: caught, by hand, the same broken copy (`SmallestSize` at the default limit, hash `c7c032bf...`).
  - *The table of broken copies* (`EachBrokenCopyFailsACheck`): each of the five faults fails the check named beside
    it, and the unbroken writer passes that check on the same 64 x 130 image first.
  - The hand breaks were one run of `patina-unit` (three failures, the three named above, nothing else), then
    reverted; its log is kept as `artifacts/M-33/T-187/hand-break-patina-unit.log` in the container's `branches/main`.
    Which part of the stream check tripped on each fault was read from the measurement program below.
- **The canary:** the SHA-256 of the PNG of `Smooth(96, 200)` (four bands; texels from integers in the test) is
  `d0024f48b23b60430272f726cb94afb7cdf80bc443630555ab3859e912380dc4`, taken on **.NET 10.0.15** (`Environment.Version`),
  this machine (Windows 10, 16 cores). Not yet run on another runtime or a runner.
- **Measured** `Encode` on the planks at 8192, Release, .NET 10.0.15, once, beside the bench's `Fast.Encode` (parallel,
  `Optimal`) in the same process: **1.16 s for the four outputs** (base colour 428 ms, normal 253, roughness 248,
  occlusion 229), against **0.71 s** for the bench's encoder in that same run, and the bench's recorded **0.15 s**
  (0.28 s in its second run). The machine was at 100% CPU from other processes throughout (a `patina-unit` run took
  288 s against its ~15 s), so the absolute figures are high; the ratio to the bench's encoder (about 1.6x) is the
  figure to read. The difference is the format's: a batch boundary every 32 bands, where the pool waits for the
  slowest band, and a serial copy of each batch's rows (filtering on the calling thread instead measured no faster).
  Every PNG decoded to its texels and was 36 bytes larger than the bench's (three more `IDAT` headers). Kept:
  `artifacts/M-33/T-187/measure-8192.txt` and `measure-Program.cs` in the container's `branches/main`. T-189 measures
  the whole export.
- `PatinaGraphsIsEngineFreeTests` passes: `System.IO.Compression` and `Parallel` are the runtime's.
- **`README.md` needed no change**: nothing a user or an agent can reach changes; nothing calls the writer yet.
- **Not checked:** the canary on another machine, runtime or a runner (its first run there is the test); the time on
  an idle machine; a Debug-build time; images wider than 300 or taller than 4200 in the tests (8192 only in the
  measurement, through `StbImageSharp`); a `Write` to a stream that cannot seek.
