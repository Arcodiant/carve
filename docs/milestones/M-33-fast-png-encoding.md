---
description: >-
  patina's export encodes an 8192 output's PNG in under a second, not the 6 s StbImageWriteSharp takes, with a small PNG writer on .NET's zlib
---
# M-33 — Fast PNG encoding

**Status:** Active

**Depends on:** M-28

**One-liner:** *patina's export encodes an 8192 output's PNG in under a second, not the 6 s StbImageWriteSharp takes, with a small PNG writer on .NET's zlib*

## What this milestone delivers

`export` at 8192 spends more time encoding PNGs than evaluating the graph. This milestone replaces the encoder, so
the encode stops being the larger half of an export.

- A small PNG writer in `Patina.Graphs`: the header, one fixed row filter (`up`), a zlib stream and the CRCs, in place
  of `StbImageWriteSharp`'s `ImageWriter`. `Png.Encode` and `export`'s streamed write both go through it.
- Every committed sample PNG under `samples/graphs/` written again by the new encoder, with the texels unchanged.
- `export`'s description and T-115's numbers measured again, in Release and Debug.

Content: the sample graphs' own PNGs, re-encoded, and an 8192 export of the planks timed before and after.

## Estimated size

About **4 tasks**:

1. Whether .NET's zlib gives the same bytes on every machine and runtime this project uses, probed before anything is
   built on it. Its answer decides between `ZLibStream` and a deflate of our own.
2. The writer, with `Png.Encode` and `export` moved onto it, and tests that every PNG decodes to the texels it was given.
3. The samples re-encoded, and every byte-for-byte check moved onto the new files.
4. The numbers: `export`'s description, T-115's As built and the bounded-memory test.

## Why

Measured on 2026-11-22 on `main` at `d45ed52` (a Release build, 16 cores, .NET 10.0.15), the planks at 8192:

| | All four outputs | PNG size, each |
|---|---|---|
| Evaluation | 19.6 s | |
| Encode as today (`StbImageWriteSharp`, RGBA, adaptive filter, level 8) | 24.2 s | 2.6 to 3.2 MB |
| stb with its filter forced to `up`, level 5, and 1 or 3 channels | 7.4 s | 0.7 to 2.5 MB |
| `ZLibStream`, RGBA, `up`, `CompressionLevel.Optimal` | 0.95 s | 0.3 to 1.0 MB |
| The same with 1 or 3 channels | 0.58 s | 0.08 to 0.9 MB |
| The same, bands of 64 rows deflated in parallel | 0.15 s | 0.09 to 0.9 MB |

- Every variant decoded to the texels it was given (`StbImageSharp`, at 2048 and 8192).
- `Optimal` cost about what `Fastest` did and gave files 3 to 10 times smaller.
- T-115 put the encode at about 10 s an output and T-130 took evaluation for most of an export. That was a Debug
  build; in Release the encode is the larger part.
- The figures are the first of two runs. The second ran 10 to 30% slower throughout, with the same ranking and every
  PNG the same size to the byte. In the first, two stb variants on roughness took about 10 s; in the second they took
  about 6 s, so that was noise.

The benchmark and its results are kept in `artifacts/png-bench/` (ignored): `Program.cs`, `bench.csproj` and the
`run2-*.tsv` of a second run.

## Open at planning

- **Whether the bytes may depend on the .NET runtime.** `Patina.Graphs` promises the same bytes every time, and the
  committed PNGs are compared byte for byte (`scenario-patina`'s export stage, `export`'s description). .NET's zlib is
  native, so its output can change with a runtime version, and perhaps between machines. Not tested. If that is not
  acceptable, the writer needs a managed deflate of its own, which is more work and slower. This changes a baseline,
  so it is the person's.
- **Fewer channels.** Occlusion as 1 channel and the others as 3 is faster again and smaller. Whether the engine's
  loader and `define_material` take such files was not checked, and it changes what a texture slot is given.
- **Parallel bands.** The fastest variant, but more code, and at 0.95 s the serial encode is already far below
  evaluation. Leaning: leave it out.
- **Evaluation is the other half of a slow 8192 export**, and is
  [M-40](M-40-parallel-graph-evaluation.md)'s. [T-130](../tasks/T-130-faster-texture-graph-evaluation-at-8192.md)'s
  premise (evaluation is most of the time) holds for the planks only in a Debug build; what becomes of T-130 is open
  in M-40.
- **Carve's own PNG writers.** `bake` (`BakeTool.cs`), `render`'s frames (`RenderTool.cs`) and
  `tools/Carve.SampleTextures` use the same package. Whether they move too, and where a shared writer would live.
- **Order against M-29.** M-28 changed `export` (named outputs, metallic, height) and added sample graphs with
  committed PNGs; it has landed, so the re-encode covers its samples too. M-29 adds two more sample graphs; whichever
  of the two lands second re-encodes or adds to the other's.

**Decided at planning (2026-11-22).**

Every decision here is the person's, on a question put with the probe results below.

- **A PNG's bytes may depend on the .NET runtime, held by a canary.** Every byte-for-byte check stays as it is, and
  the committed PNGs become .NET 10's bytes. A unit test pins the hash of one small fixed image, so a runtime or a
  runner whose zlib writes other bytes fails there, with a message that says to stop and ask. A move to another .NET
  major means running `tools/Patina.SampleGraphs` again. No managed deflate.
- **RGBA stays.** No output carries alpha today, and the files stay ready for one that does. Fewer channels is not
  planned.
- **Bands deflated in parallel.** The person chose the fastest variant over the serial one the plan leaned to: bands
  of 64 rows, each deflated apart at `CompressionLevel.Optimal` and stitched. T-187's Scope has the whole format.
- **T-130 does not join M-33.** Evaluation is M-40's, and what becomes of T-130 is open there. T-130's note now
  carries the Release figures.
- **Carve's writers stay on `StbImageWriteSharp`.** `bake`, `render`'s frames and `tools/Carve.SampleTextures` are
  not touched. T-190, off-milestone, holds moving them and the question of where a shared writer lives.
- **M-33 runs before M-29.** M-29's sample graphs are then written by the new encoder. If M-29 lands first after
  all, T-188 remakes its PNGs with the rest; whichever lands second runs the sample tool again in its rebase.

**Probed at planning** (2026-11-22, this machine, `artifacts/m33-probe/`, ignored):

- **Runtimes.** The zlib stream of 15 images (the planks, painted metal and cobblestones at 256, and two made from
  integers at 2048 and 4096), at three levels:
  - identical on 10.0.13 and 10.0.15, on 9.0.12 and 9.0.21, and on 8.0.23 and 8.0.32;
  - .NET 8 against 9 or 10: every image differs at `Optimal` and `Fastest`, 13 of 15 at `SmallestSize`;
  - .NET 9 against 10: 7 of 15 differ at `Optimal`, none at `Fastest` or `SmallestSize`.
- **Feeding.** One `Write` a row and one `Write` of every row give different streams at `Optimal` and `Fastest`, on
  .NET 9 and 10. So how the writer feeds the stream is part of the format.
- **`SmallestSize`** took 4.7 s for the planks' four outputs at 8192, against 0.8 s at `Optimal`. Not chosen.
- **Bands.** The banded form gave the same bytes at 1, 5 and 16 threads and on 10.0.13 and 10.0.15, for 16 images,
  and each stream inflated to its filtered rows.
- **Not probed:** another machine, a 1-channel PNG through the engine's loader, the encode in a Debug build, and the
  banded writer streaming one `IDAT` a batch (the bench held the whole stream).

**Nothing is drawn or laid out,** so there is no sketch. `swatch`'s images change their PNG bytes and no pixel.

**Order.** One task at a time: T-187, then T-188, then T-189. T-187 lands the writer with nothing calling it, so
every existing check stays green; T-188 switches patina to it and remakes the samples in one step, since the
byte-for-byte checks fail between the two; T-189 measures. T-188 and T-189 both edit `ExportTool.cs`.

**After it lands,** the published `carve` and `patina` MCP copies want refreshing
(`%LOCALAPPDATA%\carve-mcp\update.ps1 -Server patina`).

## Required tasks

- [ ] [T-187](../tasks/T-187-a-png-writer-on-net-s-zlib-in-patina-graphs.md) — Patina.Graphs gains PngWriter: RGBA8 PNGs with the up filter, bands of 64 rows deflated in parallel on .NET's zlib and stitched, the same bytes at any thread count, with a canary test that pins them to .NET 10's zlib
- [ ] [T-188](../tasks/T-188-patina-s-pngs-through-the-new-writer-and-the-samples-written-again.md) — Png.Encode and export write through PngWriter, so an 8192 output encodes in well under a second; every sample PNG under samples/graphs/ is written again with its texels unchanged, and Patina.Graphs drops StbImageWriteSharp
- [ ] [T-189](../tasks/T-189-export-s-8192-numbers-measured-again.md) — export's description, the README and T-115's record carry the 8192 times, PNG sizes and peak memory measured with the new encoder, in Release and Debug

## Retrospective
