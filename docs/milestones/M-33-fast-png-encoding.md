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

Written 2026-11-23, before the person's go. The rebase onto `main`, the refactor pass and the review are still to come.

**As planned.**

- **The claim holds on what was measured.** The planks at 8192, four outputs, .NET 10.0.15, through the new writer:

  | | Whole call | Encode, four outputs | Peak | Slot |
  |---|---|---|---|---|
  | Release, run 1 | 27.8 s | 1.13 s | 711 MB | shared |
  | Release, run 2 | 31.3 s | 0.59 s | 665 MB | shared |
  | Debug, run 1 | 233.1 s | 1.40 s | 683 MB | shared |
  | Debug, run 2 | 248.4 s | 1.37 s | 684 MB | shared |
  | Debug, once | 168.0 s | not measured apart | 703 MB | exclusive |

  - **One output** encodes in 0.09 to 0.51 s in Release and 0.23 to 0.46 s in Debug, against about 6 s before.
  - **Before:** Release was 19.6 s of evaluation and 24.2 s of encode; Debug was 154.7 s and 1,565 MB (T-115).
  - **Each PNG** is 0.28 to 0.97 MB, the same bytes in every run, against 2.6 to 3.2 MB.
  - Evaluation is now nearly all of the call, and is M-40's.
- **The members, one at a time in the planned order,** each by one agent with no hand-off:
  - **T-187**, `PngWriter` with its tests and the canary, called by nothing. Every existing byte check stayed green.
  - **T-188**, `Png.Encode` and `export` moved onto it, `Patina.Graphs` left with no package, and the samples remade.
    All 43 PNGs under `samples/graphs/` (35 outputs and 8 swatches) decode to the RGBA they had. Their total fell
    from 8,183,231 to 6,072,404 bytes. No byte check had its rule changed and no digest was taken again.
  - **T-189**, the figures above in `export`'s description, the README and T-115's record.
- **The format was not changed** from the one decided at planning.
- **M-29 had not landed,** so the re-encode covered the eight samples of the plan and nothing else.
- **`cairn milestone M-33 verify`** (2026-11-23, at `6e3e655`): every check passes but `review`, which comes after the
  person's go. 3 of 3 members done and merged; 26 suites passed, 21 ran and 5 carried, `server-regression`,
  `patina-regression` and every scenario among them.
- No engine need was recorded. Nothing was drawn, and no frame was opened by anyone.

**Not as planned.**

- **The writer is slower than the planning bench.** The bench gave 0.15 s for four outputs; the writer took 0.59 to
  1.16 s. In T-187's one run the bench's own encoder took 0.71 s in the same process, so the writer was about 1.6
  times it. T-187 puts the gap down to the format's batches of 32 bands (the pool waits for the slowest band of each)
  and one copy of each batch's rows, since a span cannot go to a pool thread. That is inference, not measured.
- **Every time was taken on a loaded machine.** Other repos' test hosts held it at 85 to 100% CPU throughout. The
  figures are upper bounds, with a run-to-run spread of 12% in Release and 7% in Debug.
- **An exclusive run, asked for by the person mid-milestone.** T-189 ran `patina-regression`'s command once under the
  `exclusive` slot class with `cairn test submit --command ... --resources exclusive --wait`, with no change to
  `tests.json`'s class. It passed in 1,236.9 s after about 35 minutes queued. The machine was still not quiet: CPU
  averaged 58%, and other repos' jobs showed as running at times.
- **`patina-regression`'s time follows the machine's load.** 3,505 s in T-188's verify, 1,920 and 1,852 s in T-189's
  shared runs, 1,237 s exclusive, 1,162 s at the end of M-28. `tests.json`'s `est-seconds` went from 200 to 1,240.
- **`ZLibStream` does not notice a missing final block.** It inflated such a stream and threw nothing. T-187's stream
  check therefore also checks that the stream ends in `03 00` and four bytes.
- **One suite failed once and passed on a fresh run.** `scenario-patina-library-2` failed at startup in T-188's first
  verify with "Method 'initialize' is not available on protocol version '2026-07-28'", before any stage, while
  `patina-regression` ran beside it. It did not recur in T-189's runs or in the milestone's verify. The cause is not
  known.
- **Seven byte-comparing checks were not in T-188's list:** `CobblestonesGraphTests`, `PlanksGraphTests`,
  `PaintedMetalTests`, `NestedSamplesTests` (line 59), `AcceptanceRoundTests`, `ExposedParameterToolTests` and
  `MetalPlateTests` (line 55). All pass on the new files as written.
- **Smaller slips:**
  - T-187 put `InternalsVisibleTo` in `PngWriter.cs`, the csproj being T-188's. The broken copies its tests use are an
    internal enum and an internal `Write` overload in the product assembly.
  - T-188's first sample commit did not build (a nullable warning in its new guard test), fixed in a commit of its own.
  - T-189's first wording of `export`'s description was 48 characters over `NodeToolTests`' limit and failed a verify.
  - T-189 made 77 tool calls against the checkpoint of about 60, and did not hand off.
  - The `waystone` MCP server did not connect in the orchestrator's session. No review was needed.

**Never verified.**

- **The canary on another machine, runtime or runner.** Every run was on Xellotath on .NET 10.0.15. Planning named
  the canary's first run on the runners as that test; it has not happened.
- **Any time on an idle machine,** in either build.
- **The encode inside `export`'s own streamed path.** The encode figures come from a harness that times
  `PngWriter.Write` to a file apart from evaluation. The whole-call figures are the real `patina`'s.
- **The cause of the gap to the bench.**
- **T-188's two new tests** (export at 256 equal to `Png.Encode`; no package assembly referenced) were not seen
  failing on a deliberate break. T-187's checks each were.
- **Test images** wider than 300 or taller than 4,200 rows. 8192 was covered by T-187's measurement, which decoded
  each PNG, and not by a test.
- **`Write` to a stream that cannot seek.**
- **Peak memory in Release** is the process's peak working set read by the harness, not the test's own reading.

**Known and left.**

- **T-190**, off-milestone: carve's own writers (`bake`, `render`'s frames, `tools/Carve.SampleTextures`) stay on
  `StbImageWriteSharp`.
- **T-129** (a heavy tier for the 8192 tests) and **M-40** (evaluation), as before. The test report makes T-129's
  case again.
- `CLAUDE.md` and `README.md` are inputs of no suite (verify warns), as in M-27 and M-28.
- **After it lands,** the published `patina` MCP copy wants refreshing.
- **Files for the person to delete:** the unregistered worktree folders `branches/task/T-187`, `T-188` and `T-189`,
  and scratch under `%TEMP%` (`t187-*`, `t188-*`, `t189-*`).
- **Kept under `branches/main/artifacts/M-33/`** (ignored): T-187's measurement and hand-break log, T-188's
  `reencode.tsv`, and T-189's two harnesses with `HOWTO.txt` and their results.

**Models.**

| Member | Model | Agents |
|---|---|---|
| T-187 | Opus 5.5 | 1 |
| T-188 | Sonnet 5.5 | 1 |
| T-189 | Sonnet 5.5 | 1 |

The orchestrator ran on Fable 5.2. No rework was traced to a tier. T-187 was put on Opus and not the session's own
model because the format was decided in full at planning and a working copy existed.

**Test report** (`cairn test report --since 2d`, 2026-11-23):

- **`patina-regression` is most of the slot time:** 14,894 slot-seconds in the window, about 52,000 a week at this
  rate, ahead of `patina-unit` at 4,099. Almost all of it is the 8192 tests, which T-129 already owns. Nothing new was
  filed.
- **`scenario-patina-library-2`:** one flake, the `initialize` failure above. Not filed; put to the person.
- **`patina-unit`:** one failure, from a member's own work in progress and fixed in the same task.
- The exclusive run shows as `adhoc`, 1,237 s after 2,106 s waiting.

**For the next plan.**

- **Bench the format that will be built, not one near it.** The planning bench held the whole stream and wrote one
  `IDAT`; the format batches 32 bands to an `IDAT`, and planning named the streamed form as not probed. The 0.15 s
  figure was for the first.
- **Take a time on a machine that is known to be quiet, or say that none was.** A slot class keeps cairn's own jobs
  off and nothing else. A measuring task should record the load beside each figure from its first brief, as T-189's
  did, and plan the wait for an exclusive slot.
- **A member that needs a test seam and may not touch the csproj** should have the seam's place named in its scope.
- **List the byte-comparing checks by a search at planning,** not from memory: seven were missed.
- **A serial milestone of three concrete members ran with one agent each.** The split into a writer nothing calls,
  then the switch with the samples, kept every check green between members.

**After the person's go (2026-11-23).**

- **Rebase onto `main` (Opus 5.5):** `origin/main` had not moved, and there were no conflicts. The rebase still
  rewrote the branch: a plain rebase dropped the three `Merge task/T-18x` commits and left a straight line, with the
  same tree (`git diff ORIG_HEAD HEAD` empty). The local branch then read ahead of and behind
  `origin/milestone/M-33`. The agent tried `git reset --keep` to put the merges back; a hook refused it, and it was
  left.
- **Refactor pass (the same agent, `8b5eba1`):** three cuts, with the canary and every byte check passing:
  - `InternalsVisibleTo` moved from `PngWriter.cs` into the csproj;
  - `AdlerCombine` made private, and the running Adler-32 started at 1 so the first band is no special case;
  - `Png.Encode`'s length guard removed, `PngWriter.Encode` repeating it with the same message.

  Named and not made: the CRC runs a byte at a time over the compressed data on the writing thread.
- **Review round 1, RV-80:** two Opus 5.5 reviewers, split by concern (`PngWriter` and its tests; the whole diff).
  - **Findings:** 9, with 5 Minor and 4 Nit.
  - **What held:** the writer on every image run in a scratch copy, up to 8192 wide and 6,200 rows, on a stream that
    cannot seek, and at one thread; all 43 sample PNGs decoding to the RGBA they had on `main`, by the reviewer's own
    decoder; no byte check, tolerance or digest changed.
  - **The Minors:** no test reached the Adler-32 combine at a band of 65,521 bytes or more, which every image from
    256 wide has; "3 to 4 min" in Debug left out the 168 s run; the encode's time read as measured inside `export`;
    and two As built lines were stale after the refactor pass.
  - **Answers (one Opus 5.5 agent, `f9f1ec4` and `71075cc`):** all 9 fixed. `PngWriter.Write` now refuses a size it
    cannot encode before a byte is written, where it threw `OverflowException` after the header.
- **Review round 2, RV-81:** one Opus 5.5 reviewer checking the answers against the code.
  - **All nine hold.** The writer at `8b5eba1` and after the fixes gave the same bytes for 26 images, 8192 square
    among them.
  - **Findings:** 3 Nits: the no-package guard misses a lower-case or imported `PackageReference`; the refusal's test
    does not pin `Array.MaxLength`; and the `IDAT` bound is called zlib's `deflateBound` though .NET 10.0.15 bundles
    zlib-ng 2.2.5.
  - **The person chose to file them and finish.** They are **T-196**, off-milestone, and each is answered `skipped`.
- **The person's other rulings:**
  - **The two startup failures are not filed.** The second came in the refactor agent's verify: `scenario-patina`
    crashed once with an access violation in Vulkan's `CreateGraphicsPipeline` during the engine's own start
    (`EnvironmentBaker.BuildBrdfLut`), with `patina-regression` running beside it, and passed on the next run. Like
    the `initialize` failure it came before any patina or PNG code. Neither recurred in the two verifies after.
  - **`finish` does not wait for the canary on a second machine.** It has still run only on Xellotath.
- **`patina-regression` ran in 1,095 s** in the verify after the fixes, with little else running.
- **Not as planned after the go:**
  - The reviewers' sections were written to files in the temp folder and put into the review items by the
    orchestrator, so two reviewers never wrote one file at once.
  - The fix agent made 76 tool calls against the checkpoint of about 60, and did not hand off.
  - The whole-diff reviewer wrote that the only verify had run before the refactor. One had passed at `8b5eba1`.
- **Left:** T-196; scratch under `%TEMP%` (`m33-*`); and ignored build output a probe left in
  `src/Patina.Server/bin` and `obj` of the milestone's worktree.
- **Next time:**
  - **Tell the rebase agent what a no-op looks like.** With `main` unmoved, the brief said to expect nothing, and the
    rebase still flattened the merges. That is what `finish` does as well, so the brief should say to leave it.
  - **Checking answers against the code found nothing broken this time,** and three small gaps in the fixes' own
    tests. A fix's new test wants the same "shown failing at its boundary" as the first ones.
