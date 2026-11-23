---
description: >-
  Png.Encode and export write through PngWriter, so an 8192 output encodes in well under a second; every sample PNG under samples/graphs/ is written again with its texels unchanged, and Patina.Graphs drops StbImageWriteSharp
---
# T-188 — patina's PNGs through the new writer, and the samples written again

**Status:** Open

**Pri:** P1

**Milestone:** M-33

**Batch:** — (standalone)

**Depends on:** T-187

**Verify:** patina-unit, patina-regression, scenario-patina, scenario-patina-library-1, scenario-patina-library-2, scenario-planks

## Goal

Every PNG patina writes goes through T-187's `PngWriter`: `export`'s files, `swatch`'s images and the sample tool's
output. The committed sample PNGs are written again by it with every texel unchanged, and every byte-for-byte check
holds against the new files with no check loosened.

## Scope

- **Read T-187's `### As built` first**, for the writer's signatures and the canary.
- **The switch:**
  - `Png.Encode` (`src/Patina.Graphs/Imaging/OutputPacking.cs`) calls `PngWriter`; its summary no longer names stb.
  - `ExportTool.cs` line 194 writes `rgba` through `PngWriter.Write` into the same temporary `FileStream`. Leave
    the forced collections around it, the temporary file and the move as they are; T-189 measures the peak.
  - `src/Patina.Graphs/Patina.Graphs.csproj` drops its `StbImageWriteSharp` reference, so `Patina.Graphs` has no
    package at all. `Directory.Build.props` keeps the pin, since carve still uses it.
  - No code under `src/Carve.*`, `tools/Carve.SampleTextures` or a carve scenario changes. T-190 owns those.
- **The samples.** `dotnet run --project tools/Patina.SampleGraphs` remakes all 35 output PNGs under
  `samples/graphs/` (eight samples and `bricks/variants/grey/`).
  - **Texels unchanged, shown twice.** The existing sample tests hold each PNG equal to its graph texel for texel.
    Also decode each old file (`git show HEAD:<path>`) and each new one and compare every byte of RGBA. Keep the
    table (file, old bytes, new bytes, equal) in `artifacts/M-33/T-188/reencode.tsv` under the container's
    `branches/main`, and name it in the As built.
  - **The eight `swatch.png` files** are copies of scenario-patina's sheets at tiling 3, which `swatch` now encodes
    with the new writer. Copy them again from this task's scenario-patina library runs, fetched by job id, and hold
    each pixel-equal to the file it replaces by the same decode. If one is not equal, stop and ask.
- **Checks that compare PNG bytes. Find every one, and change none of their rules:**
  - Known: `ExportToolTests` (lines 44 to 45), `MetalPlateTests`, `NestedGraphToolTests` (167), `NestedSamplesTests`,
    `RolesAndNamedOutputsTests`, `SwatchTests`, and the scenario's `ExportStage.cs` (31 to 33), `NestedGraphStage.cs`
    (29 to 30), `SwatchStage.cs` (439) and `LibraryStage.cs`. Search for more.
  - Each should pass on the new files as written. One that needs its comparison changed, or a pinned digest taken
    again, goes to the person first.
  - Frame hashes in a run's `run.json` are of PNG bytes, so every `swatch` frame's hash moves while its pixels do
    not. Say so in the As built. No frame needs opening for this task: pixel equality is the check.
- **Text that names the old encoder:**
  - `CLAUDE.md`'s architecture rule for `Patina.Graphs` ("only `StbImageWriteSharp` for PNGs") becomes: no package;
    PNGs by its own `PngWriter` on .NET's zlib, whose bytes are .NET 10's (the canary, T-187). Add `PngWriter` to the
    Layout row's `Imaging/` part.
  - `README.md`: wherever it says how export's PNGs are made, or promises the same bytes, add that the bytes are
    those of .NET 10's zlib and that another runtime version may write other bytes for the same texels. The
    paragraph under "What is slow or bounded" is T-189's. Say in the As built what changed in the README.
  - `export`'s description (`ExportTool.Text`) is T-189's.
- **Tests added:**
  - `export` at 256 and `Png.Encode` give the same bytes for the same texels.
  - A guard beside `PatinaGraphsIsEngineFreeTests` that `Patina.Graphs` references no package assembly.

## Notes

- **Mostly mechanical, but wide:** one small code change, then many files and five regression suites. It decides
  nothing others build on.
- **Decided at planning by the person (2026-11-22):** the byte-for-byte checks stay as they are and the committed
  PNGs become .NET 10's bytes; carve's own writers stay on stb.
- **Goes back to the person, not into the code:** any sample texel that differs; a `swatch.png` that differs; a
  byte check that cannot pass unchanged; the canary failing on a runner.
- `patina-regression` took 1,162 s in M-28's last verify. Give `verify` a long limit and say so in the brief.
  Run `cairn lint` as a command of its own and read it before `finish`.
- If M-29 has added sample graphs by the time this starts, they are remade here too. Read this scope against what
  has landed before starting.
- **Write down in `### As built` for T-189:** each remade file's old and new size, and anything seen of the encode
  time.
- Paths and C# strings written through a shell lose backslashes. Write files with the file tools.
