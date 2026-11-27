---
description: >-
  Review of T-240
---
# RV-94 — Review of T-240

**Status:** Answered

**Reviews:** T-240

**Round:** 1

**Previous:** —

**Diff:** f790a6e8c45c5a1f12a2713bd746a62f8483068e against origin/main

**Reviewer:** claude-opus-5-5 (two fresh agents)

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings: Patina.Graphs (reader, node, validator, their tests)

### Bilinear below half size skips texels, so the Notes' "does not alias" does not hold

`src/Patina.Graphs/Nodes/ImageNode.cs:184-220`. Rubric 1 (the Goal and Notes). The Scope asks for bilinear, and the code is bilinear, but the Notes give the reason as "so a 1024 bake read into a 256 graph does not alias". Bilinear reads a 2x2 footprint at each texel's centre, so at a 4:1 reduction texel x reads only image columns 4x + 1 and 4x + 2. Columns 4x and 4x + 3 are never read at all. A probe (throwaway, `artifacts/rv94-probe/probe.cs`) gives a 1024 x 1 image with a 1-texel line in every column 4k: read at 256, every texel's R is 0, so the lines vanish. Failure scenario: carve's 1024 bevel-normal bake has 1-2 texel bevel edges. When it is read into a 256 graph for `normal_blend`, the edges drop out or flicker depending on which column they fall on. That is the aliasing the Notes say bilinear avoids. Either average the footprint (a box over the n/size texels) when the image is more than 2x the graph, or state the limit in the Notes, the README and the node's description.

**Outcome:** skipped: the Scope asks for bilinear sampled at each texel's centre, and a box or mip filter is a change of the Scope only the person can make; the limit (below half size a texel reads a 2x2 footprint, so a 1-texel line can drop out) is now stated in As built and README.md, and raised in the hand-back for T-224.

### The same PNG is decoded and held once per node and once per nested use

`src/Patina.Graphs/GraphValidator.cs:148-150` and `:265-300` (`ReadImage`), with `:411` (`Analyse` of each expansion). Rubric 1. `ReadImage` has no per-analysis cache by resolved path. Each own image node decodes its file, and each `graph` node analyses its file again, which decodes that file's PNGs again. Every decoded `PngImage` stays alive on its `NodeParameters` for the analysis's lifetime, which is the evaluator's lifetime too (`GraphEvaluator.cs:50`). Nothing counts that memory against the evaluator's cache bound. Failure scenario: a wall graph whose `wear.texgraph` reads an 8192 bake (256 MiB decoded) and uses `wear` as a node four times holds 1 GiB of identical texels for one export. The same happens with four `image` nodes on one PNG (say `kind: normal` and `kind: grey` of the same bake twice). The 2 GB peak that `ExportBoundedMemoryTests` holds for exports is then gone before a tile is made. Keying the decode on the full path within one `Analyse` (and its nested analyses) would make it one decode and one copy.

**Outcome:** fixed: `NodeBudget.Images` keys each decode by full path for one validation and its nested analyses, so every node and use of one PNG shares one `PngImage` (`One_png_is_decoded_once_however_many_nodes_and_uses_read_it`).

### Two of the reader's damage checks have no test

`src/Patina.Graphs/Imaging/PngReader.cs:118` (`inflate.ReadByte()`, which makes zlib check the Adler-32) and `:62` (an unknown critical chunk refused). Rubric 2 and 7. As built says the reader refuses "an unknown ... critical chunk" and that the stream is read to its end so the checksum is checked. No case in `PngReaderTests.Refused` covers either. Failure scenario: delete line 118, or turn line 62 into a skip, and all 60 tests still pass. A probe confirms line 118 works today: a PNG whose IDAT has a corrupted Adler-32 and a correct CRC is refused. But the refusal text is .NET's "The archive entry was compressed using an unsupported compression method", after the reader's "does not inflate (the file is damaged)", so a test should pin only the reader's part. Two rows in `Refused` would cover both checks: an IDAT with a bad Adler-32 (CRC fixed up) and a chunk `ZZZZ`.

**Outcome:** fixed: two rows in `PngReaderTests.Refused`: a wrong Adler-32 with the CRC right, pinned on the reader's own words only, and a critical chunk `ZZZZ`.

## Findings: integration (Carve.Graphs, Patina.Server, README, CLAUDE.md, scenarios, server tests, task file)

### carve's cache-key path now decodes every PNG on every render

`src/Carve.Graphs/GraphSlotBake.cs:51`. Rubric 1 (correctness, what it breaks elsewhere) and 7 (As built). `Read` takes its file list from `GraphValidator.Files`, which runs the whole analysis, and the analysis inflates, unfilters and keeps each image node's PNG in full (up to 8192 square, 256 MiB). `Read` only needs the paths and bytes, but it is called on every graph slot of every `render` before the cache is even asked (`src/Carve.Server/GraphBakes.cs:100`, `:211`), again by `Unchanged` after each bake (`GraphBakes.cs:142`, `:219`), by carve's `check` (`GraphSlotProblem.cs:79`, `:144`) and by `MaterialTools.cs:43`. Before this change `Read` parsed only JSON. Failure scenario: a material whose graph reads a 4096 baked normal re-decodes that 64 MiB image on every `render` call, including cache hits, and twice on a miss. As built's "Not verified" lists patina's analyses that decode but not carve's key path, which is the hot one.

**Outcome:** fixed: `GraphValidator.Files` runs the analysis with `NodeBudget.ListOnly`, which lists an image node's PNG once it exists and never reads or decodes it, so `GraphSlotBake.Read` (and `open`) decode nothing (`Files_lists_a_png_without_decoding_it`). As built's Not verified now names carve's key path.

### save's description does not say image nodes are judged in the new folder

`src/Patina.Server/FileTools.cs:114`. Rubric 1. As built decision (5) adds a new refusal (`image node 'x' would read ..., which differs from ...`), and a missing PNG in the target folder is now a save fault. But save's description still says only "A graph node's path resolves against the file's folder, so the graph is judged there ... Only the open graph is written, never a file it uses as a graph node." Failure scenario: an agent saves a graph with an `image` node into `materials/`. From the description it cannot tell that it must copy the PNG there too, and it meets a refusal the tool never described. `open`'s and `get_graph`'s descriptions were updated; this one was missed.

**Outcome:** fixed: save's description says a graph or image node's path resolves against the file's folder and that a nested file or PNG not found or differing there is a fault or a refusal; still under the limit (`NodeToolTests`).

### Save comparing a nested graph's PNG by bytes has no test

`src/Patina.Server/FileTools.cs:171-173` with `:184-187`. Rubric 2. As built decision (5) says "a nested graph's PNG is compared by bytes rather than parsed as a graph". That path runs through the `Uses` loop, because a nested graph's PNGs are in `NestedGraphUse.Files`. The new `An_image_nodes_png_is_named_by_get_graph_and_open_and_judged_by_save` covers only an image node of the open graph itself, through the `Images` loop at `:175-179`. Failure scenario: revert `SameGraph` to its old graph-only form. `TexGraphFile.ReadFile` on a PNG then gives no graph, so an identical nested PNG copy reads as "differs" and every move of a graph that nests an image-reading graph is refused. No test fails.

**Outcome:** fixed: `A_nested_graphs_png_is_judged_by_its_bytes_when_save_moves_the_graph` refuses a differing nested PNG and saves an identical one, which the old graph-only `SameGraph` would refuse.

### GraphBakes.Costliest still parses image PNGs as .texgraph

`src/Carve.Server/GraphBakes.cs:304`. Rubric 1 (other consumers of the file list). `Read` and `MaskNames` now filter `Files` with `IsGraphFile` (`GraphSlotBake.cs:110`, `:191`), but `Costliest` still runs `TexGraphFile.ReadFile` over every entry. After each slot bake it reads each PNG whole with `File.ReadAllText` and fails a JSON parse on it. The result is still correct (a null graph is skipped), but the work is wasted per bake, and this one consumer was left out of the filtering the diff applies elsewhere. `IsGraphFile` is private to `GraphSlotBake`, so the fix needs it exposed or the filter repeated.

**Outcome:** fixed: `Costliest` reads only the `.texgraph` files among `Files`.

## Summary

Two reviewers, one a part. Seven findings: six fixed, one (bilinear below half size) skipped as a Scope change for the person.
