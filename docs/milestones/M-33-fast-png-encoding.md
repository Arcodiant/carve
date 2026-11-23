---
description: >-
  patina's export encodes an 8192 output's PNG in under a second, not the 6 s StbImageWriteSharp takes, with a small PNG writer on .NET's zlib
---
# M-33 — Fast PNG encoding

**Status:** Planned

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

## Required tasks

Not planned yet.

## Retrospective
