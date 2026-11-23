---
description: >-
  Png.Encode and export write through PngWriter, so an 8192 output encodes in well under a second; every sample PNG under samples/graphs/ is written again with its texels unchanged, and Patina.Graphs drops StbImageWriteSharp
---
# T-188 — patina's PNGs through the new writer, and the samples written again

**Status:** Done

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

### As built

Built by Claude Sonnet 5.5 (2026-11-23).

- **The switch.** `Png.Encode` calls `PngWriter.Encode` (its summary names the writer, not stb); `ExportTool` writes `rgba`
  with `PngWriter.Write(stream, rgba, pixels, pixels)` into the same temporary `FileStream`, the forced collections, the
  temporary file and the move untouched; `Patina.Graphs.csproj` has no `ItemGroup` and no package (`Directory.Build.props`
  keeps the pin for carve). Nothing under `src/Carve.*`, `tools/Carve.SampleTextures` or a carve scenario changed.
  - *Since the refactor pass (`8b5eba1`, 2026-11-23, recorded for RV-80):* `Patina.Graphs.csproj` now has one
    `ItemGroup`, holding `InternalsVisibleTo` for `Patina.Graphs.Tests` (moved there from `PngWriter.cs`), and still no
    package. `Png.Encode`'s own length guard was removed, because `PngWriter.Encode` repeats it with the same message;
    `Png.Encode` is now `PngWriter.Encode`. The bytes did not change.
- **Tests added:** `Export_at_256_and_Png_Encode_give_the_same_bytes_for_the_same_texels` (`ExportToolTests`: export's four
  planks files against `Png.Encode` of `OutputImage.FileRgba8` at 256) and `ReferencesNoPackageAssembly`
  (`PatinaGraphsIsEngineFreeTests`: every assembly `Patina.Graphs` references is a file in the runtime's folder). Neither
  was seen to fail on a deliberate break (not done; the guard's first run only proved it compiles and passes).
  - *For RV-80 (2026-11-23):* RV-80's reviewer saw `ReferencesNoPackageAssembly` fail on a used `StbImageWriteSharp`
    and pass on one referenced but not used. `TheProjectFileDeclaresNoPackage` (`PatinaGraphsIsEngineFreeTests`) was
    added: no `PackageReference` in `Patina.Graphs.csproj`. In a scratch copy under `%TEMP%` with an unused
    `<PackageReference Include="StbImageWriteSharp" .../>` added, it failed (`["StbImageWriteSharp"]`) while
    `ReferencesNoPackageAssembly` passed.
- **Samples.** `dotnet run --project tools/Patina.SampleGraphs` remade the 35 output PNGs. Each of the 43 PNGs under
  `samples/graphs/` (35 outputs and 8 swatches) was decoded from `git show ef319d7:<path>` (the start commit) and from the
  working file (Pillow, RGBA, size included) and compared byte for byte: **43 of 43 equal, no texel differs.** No byte of
  any file is equal to its old file. Table (file, old bytes, new bytes, decoded equal, bytes equal):
  `artifacts/M-33/T-188/reencode.tsv` in the container's `branches/main`. M-29 had added no sample graph beyond the Scope's;
  planks has no swatch, so the 8 swatches are bricks, its grey variant, cobblestones, fabric, floor-tiles, grime, old-wall and painted-metal.
- **The eight `swatch.png`** are copies of `library-<name>-sheet.png` from this task's own scenario-patina-library-1
  (J-20261123-045217-c1265f) and -2 (J-20261123-045425-7e35b5), fetched by job id; each decodes pixel-equal to the file it
  replaces. Every `swatch` frame's hash in a run's `run.json` is of PNG bytes, so it moves while its pixels do not
  (bricks sheet: sha256 4f9ad6b7... before, 742775bf... after).
- **Checks that compare PNG bytes, none with its rule changed** (each passed on the new files as written): the Scope's
  `ExportToolTests`, `MetalPlateTests`, `NestedGraphToolTests` (167), `NestedSamplesTests`, `RolesAndNamedOutputsTests`,
  `SwatchTests`, and scenario `ExportStage`, `NestedGraphStage`, `SwatchStage` (440), `LibraryStage` (146); found beyond
  the list: `CobblestonesGraphTests` (69), `PlanksGraphTests` (135), `PaintedMetalTests` (63), `NestedSamplesTests` (59),
  `AcceptanceRoundTests` (49, 72), `ExposedParameterToolTests` (139 to 140), `MetalPlateTests` (55). No pinned digest was
  taken again; the canary (`PngWriterTests`) passed in `patina-unit`.
- **Text.** `CLAUDE.md`: the `Patina.Graphs` rule now says no package, PNGs by its own `PngWriter` on .NET's zlib whose
  bytes are .NET 10's (the canary); the Layout row's `Imaging/` part names `PngWriter`. `README.md`: under "Outputs and
  export", one sentence saying the same graph and size give the same bytes on one runtime, those of .NET 10's zlib, and
  another runtime version may write other bytes for the same texels. The "What is slow or bounded" paragraph and
  `ExportTool.Text` are left to T-189.
- **For T-189, sizes old > new, bytes:** bricks base_colour 108468>74615, normal 12947>8924, occlusion 5331>3939, roughness
  73329>42802, swatch 793006>627058; bricks/variants/grey base_colour 95031>62991, normal 13109>9056, occlusion 5221>3948,
  roughness 72992>42727, swatch 745206>563901; cobblestones base_colour 111475>77850, normal 187238>148417, occlusion
  68364>41104, roughness 70710>42757, swatch 1415659>1078600; fabric base_colour 149377>107482, normal 9122>5923, occlusion
  7202>4318, roughness 87968>51812, swatch 1032441>798087; floor-tiles base_colour 24428>22345, height 3548>2113, normal
  4992>3174, occlusion 3322>1746, roughness 41552>27769, swatch 432988>358086; grime base_colour 56770>35873, swatch
  326701>211578; old-wall base_colour 111985>76708, normal 13071>9034, occlusion 5346>3993, roughness 72444>42564, swatch
  819369>629085; painted-metal base_colour 101178>62921, normal 61458>45835, occlusion 24769>14493, roughness 52046>31544,
  swatch 922487>672467, wear 4661>2810; planks base_colour 11118>7906, normal 2863>988, occlusion 2850>966, roughness
  19089>12095. All 43 together: 8,183,231 > 6,072,404. Encode time: not measured here (all files are 256 or 384 to 1536
  square; no 8192 export was run).
- **Verify.** The first `verify` ran `patina-regression` in 3,505 s (with another repo's suites on the machine) and passed
  it, `patina-unit`, `scenario-planks`, `scenario-patina` and library-1; `scenario-patina-library-2` failed at start with
  "Method 'initialize' is not available on protocol version '2026-07-28'" (a handshake error in the client under load,
  before any stage, unrelated to PNGs), and passed on a fresh `cairn test run --fresh` rerun.
- **Not checked:** the canary on a runner or another runtime; the encode time of an 8192 export; the two new tests were not
  seen to fail on a break; no frame was opened.
