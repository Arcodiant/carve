---
description: >-
  Patina.Graphs gains PngWriter: RGBA8 PNGs with the up filter, bands of 64 rows deflated in parallel on .NET's zlib and stitched, the same bytes at any thread count, with a canary test that pins them to .NET 10's zlib
---
# T-187 — A PNG writer on .NET's zlib in Patina.Graphs

**Status:** Open

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
