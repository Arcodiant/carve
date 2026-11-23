---
description: >-
  Review of M-33
---
# RV-80 — Review of M-33

**Status:** Open

**Reviews:** M-33

**Round:** 1

**Previous:** —

**Diff:** d884a9d89497ee3cfe70f773ac525d955172320c against origin/main

**Reviewer:** two reviewers, both claude-opus-5-5 (Claude Opus 5.5): one on `PngWriter` and its tests, one on the whole diff

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings: PngWriter and its tests (claude-opus-5-5)

### No test reaches the Adler-32 combine at a band length of 65521 bytes or more
- **Where:** `tests/Patina.Graphs.Tests/PngWriterTests.cs:46-58,142-167` (against `src/Patina.Graphs/Imaging/PngWriter.cs:205-218`)
- **Severity:** Minor
- **What is wrong:** A band is 64 x (4 x width + 1) bytes, so `length2 % Base` and the wraps after it only matter from width 256 up. The widest image in the tests with more than one band is 97 x 211 (bands of 24,896 bytes). The canary is 96 x 200 (24,640). The only wide image is 300 x 1, which is one band combined onto 1. The As built says the 8192 run was checked through `StbImageSharp`, which does not check the Adler-32. So nothing pins the combine at the sizes T-188 commits. The writer is correct now: I tested a copy at 256 x 200, 300 x 130, 1024 x 300, 2000 x 2100 and 8192 x 200 in noise, and each inflated through `ZLibStream` to independently filtered rows. But a broken copy goes unnoticed. In a scratch copy I replaced `(uint)(length2 % Base)` with `(uint)length2`. Every image in the tests' `Images` list, plus 64 x 130, the canary's 96 x 200 and 40 x 4200, still inflated through `ZLibStream`, and the canary's bytes do not change at that width. Only 1024 x 130 and 8192 x 130 threw `InvalidDataException`. Rubric 2 (tests cover the Scope's "combined, as zlib's `adler32_combine` does"). Tested.

**Outcome:** —

### T-187's As built still says `InternalsVisibleTo` is in `PngWriter.cs`
- **Where:** `docs/tasks/T-187-a-png-writer-on-net-s-zlib-in-patina-graphs.md:110-111`
- **Severity:** Minor
- **What is wrong:** The As built says "the file carries `[assembly: InternalsVisibleTo("Patina.Graphs.Tests")]`, since the csproj is T-188's". Refactor commit `8b5eba1` moved it into `src/Patina.Graphs/Patina.Graphs.csproj:11`, and `PngWriter.cs` no longer carries it. The As built also says nothing of `AdlerCombine` becoming private or of the running Adler-32 now starting at 1. Rubric 7. Tested by reading the file and `git show 8b5eba1`.

**Outcome:** —

### The table of broken copies accepts any exception, so it cannot show each break failing for its named reason
- **Where:** `tests/Patina.Graphs.Tests/PngWriterTests.cs:142-167`
- **Severity:** Nit
- **What is wrong:** `Assert.ThrowsAny<Exception>` passes on any failure inside `CheckDecodes` or `CheckSound`: an `IndexOutOfRangeException` in `Chunks`, a wrong IDAT count, a CRC mismatch, and so on. The `check` column names only the helper ("decodes" or "sound"), not the assertion. For example, `WrongAdlerCombine` is meant to be caught by `ZLibStream`. If the fault broke the stream in some other way, the row would pass just the same. The As built says which part tripped on each fault, but it read that from a separate measurement program, not from this test. Asserting the exception type or a message fragment per row (`InvalidDataException`; "CRC is wrong"; "empty final block") would make the table say what the Scope asks. Rubric 2. Inferred from reading.

**Outcome:** —

### `AnImageOfMoreThan2048RowsIsTwoIdatChunks` was never shown failing and repeats a check in `TheStreamIsSound`
- **Where:** `tests/Patina.Graphs.Tests/PngWriterTests.cs:84-89`
- **Severity:** Nit
- **What is wrong:** The Scope says each new check is shown failing once on a broken copy. The As built lists "*Two `IDAT` chunks* past 2048 rows" with no break beside it. `CheckSound` already asserts one `IDAT` a batch for the same 5 x 2100 image (line 194-195), so the fact remains unproven rather than unchecked. Either name a break for it or drop it as redundant. Rubrics 2 and 7. Inferred from reading.

**Outcome:** —

### Very wide images near 2 GiB fail with `OverflowException` instead of encoding or refusing
- **Where:** `src/Patina.Graphs/Imaging/PngWriter.cs:73,108`
- **Severity:** Nit
- **What is wrong:** The batch copy is `checked((min(height, 2048) + 1) * stride)`. The extra row above the batch can overflow `int` even when `rgba` itself fits. For example, at width 262,144 (stride 1 MiB) and height 2047, `rgba` is 2047 MiB but the copy is 2^31 bytes. That image passes the length check, and `Write` then throws `OverflowException` after the signature and `IHDR` are already on the stream. Similarly, `checked((uint)chunkLength)` allows an `IDAT` length up to 4 GiB, but PNG caps a chunk at 2^31 - 1. A batch of incompressible rows close to 2 GiB could pass that check and still produce an invalid chunk. Neither size is reached by Carve's 8192 x 8192 (raw copy 64 MiB, batch at most about 64 MiB). Validating these limits before writing would keep `Write` from leaving a partial PNG on the stream. Rubric 1 (sizes). Inferred, not run: it needs a 2 GiB buffer.

**Outcome:** —

## Findings: the whole diff (claude-opus-5-5)

### Debug time "3 to 4 min" leaves out two of the three measured runs
- **Where:** `src/Patina.Server/ExportTool.cs:48`, `README.md:350-351`
- **Severity:** Minor
- **What is wrong:** `ExportTool.Text` says the planks' four outputs at 8192 take "3 to 4 min in debug", and the README says "3 to 4 minutes in a debug build". T-189's `### As built` table and T-115's new dated line record three Debug runs: 233.1 s (3.9 min), 248.4 s (4.1 min) and 168.0 s (2.8 min, the exclusive run, the only one on a less loaded machine). Only one of the three falls inside "3 to 4 min". The run nearest an idle machine is below the range and one shared run is above it. The measured range is about 2.8 to 4.1 min, so the text does not agree with its own table (rubric 1 and 7: what T-189's Goal calls "what it now measures"). Tested: I did the arithmetic against the As built table.

**Outcome:** —

### The encode's share is given as part of the export call, but it was timed in a separate harness
- **Where:** `README.md:351-352`, `docs/tasks/T-115-png-export-at-a-given-size.md:78`
- **Severity:** Minor
- **What is wrong:** The README says the release and debug calls took so long "with ... the encode 0.6 to 1.4 s of that in all four". T-115's new line says "The encode is 1.4 s (Debug) and 0.6 to 1.1 s (Release) of the four outputs, so evaluation is nearly all of it". Both present the figure as a share of the measured export call. T-189's As built ("Not checked") and the retrospective ("Never verified") both say the encode was never timed inside export's streamed path. It came from the `share/` harness, a separate process writing a whole in-memory image. That harness's own figures show the two do not add up: its evaluation alone took 28.0 s and 32.4 s in Release, longer than the whole export calls of 27.8 s and 31.3 s. Neither user-facing text says the encode was measured apart, and the README's single range also blends the Release and Debug encode figures after naming both builds. The README and T-115 claim more than was measured (rubric 1 and 7). Inferred from the tracker's own figures and wording; I re-measured nothing.

**Outcome:** —

### The tracker does not record the refactor pass, and two As built lines are now wrong
- **Where:** `docs/tasks/T-188-patina-s-pngs-through-the-new-writer-and-the-samples-written-again.md:86`, `docs/tasks/T-187-a-png-writer-on-net-s-zlib-in-patina-graphs.md:111`, `docs/milestones/M-33-fast-png-encoding.md:131,186`
- **Severity:** Minor
- **What is wrong:** Commit `8b5eba1` (the refactor pass) did four things: it moved `InternalsVisibleTo` into `Patina.Graphs.csproj` as an `ItemGroup`, made `AdlerCombine` private, changed how the running Adler-32 is built (the first band's special case is gone), and dropped `Png.Encode`'s own length guard. After it:
  - T-188's As built still says `Patina.Graphs.csproj` "has no `ItemGroup`", which is no longer true.
  - T-187's As built still says `PngWriter.cs` "carries `[assembly: InternalsVisibleTo(...)]`", which is no longer true.
  - The retrospective was written before the pass ("the refactor pass and the review are still to come"). It lists the attribute's placement as a slip without saying it was fixed.
  - Nothing records the change to byte-producing code.
  - The only recorded `cairn milestone M-33 verify` ran at `6e3e655`. That commit is on `origin/milestone/M-33` but not in HEAD's history (it is before the rebase and the refactor).

  This is tracker hygiene (rubric 7) and an unrecorded check of the change (rubric 4). Tested in part: in a `git archive HEAD` copy under `%TEMP%`, `Patina.Graphs.Tests` passed 496 of 496 at HEAD, including the canary and the sample byte checks in that project. `Patina.Server.Tests` and the scenarios were not run at HEAD. The guard claim itself holds; see the report.

**Outcome:** —

### The no-package guard misses a package that is referenced but not used
- **Where:** `tests/Patina.Graphs.Tests/PatinaGraphsIsEngineFreeTests.cs:23-34`, `CLAUDE.md:43`
- **Severity:** Nit
- **What is wrong:** `ReferencesNoPackageAssembly` reads the compiled assembly's metadata references, so it fails only when code uses a type from a package. Tested in a copy under `%TEMP%`:
  - With `<PackageReference Include="StbImageWriteSharp" .../>` added back to `Patina.Graphs.csproj` and no use of it, the test passed.
  - With one `new StbImageWriteSharp.ImageWriter()` added, it failed (`Actual: ["StbImageWriteSharp"]`).

  So the test can fail on the break T-188 cared about, which answers the "never seen failing" question for it. But CLAUDE.md's rule is now "no package at all", and the guard does not hold an unused reference (or the restore and deploy weight it brings) to that rule (rubric 2).

**Outcome:** —

## Summary

The diff was split by concern, since its code is small (about 600 lines in 7 files) and its one risk is a byte format
that 43 committed PNGs rest on: one reviewer read `PngWriter.cs` and `PngWriterTests.cs` whole, and one read the whole
diff (the switch of `Png.Encode` and `export`, the samples, the measured text and the tracker). Both read the code
after the rebase and the refactor pass (`8b5eba1`).

- **The writer** was run in a scratch copy on wide and tall images (up to 8192 wide and 6200 rows), each inflated
  through `ZLibStream` and compared with rows filtered independently, at 1 thread and at the default, and on a stream
  that cannot seek and throws part-way. All passed. Not run: anything near 2 GiB, an exception on a pool thread.
- **The samples:** all 43 PNGs under `samples/graphs/` decode to the RGBA they had at `origin/main`, by the reviewer's
  own decoder and by Pillow. Each is RGBA8 with `IHDR`, `IDAT`, `IEND` only and valid CRCs.
- **The switch:** no patina PNG write is left on stb, nothing of carve's changed, and no byte check had its rule,
  tolerance or digest changed. `Png.Encode`'s dropped guard is repeated by `PngWriter` with the same message.
- `Patina.Graphs.Tests` passed 496 of 496 in a copy of HEAD. `Patina.Server.Tests` and the scenarios were not run by
  the reviewers.

9 findings: 5 Minor and 4 Nit, none Major.
