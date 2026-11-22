---
description: >-
  Review of M-28, round 2
---
# RV-77 — Review of M-28, round 2

**Status:** Open

**Reviews:** M-28

**Round:** 2

**Previous:** RV-76

**Diff:** 25f1b49f14cf3f77e4ebfbe8036e6f75052dc237 against origin/main

**Reviewer:** two reviewers, each claude-opus-5-5 (Claude Opus 5.5): the engine, nodes and checks (T-173, T-174); the format, server and scenarios (T-175, T-176, T-177), with a cross-check of files two fix members changed

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings: round 2, the engine, nodes and checks (claude-opus-5-5)

Reviewed on Claude Opus 5.5 (`claude-opus-5-5`), from `git diff b8df3fd..HEAD` in `D:\Repos\carve\branches\milestone\M-28`, read only. I ran tests in Debug, with logs in `C:\Users\andre\AppData\Local\Temp\m28r2-*.log`. The targeted classes passed: `EvaluationEngineAnswersTests`, `CheckRulesTests`, `WorleyFbmWarpTests`, `PatinaGraphsIsEngineFreeTests`, `CheckSamplesTests`, `NodeToolTests` (the 3,686 limit), `CheckToolTests` and `MetalPlateTests`. The whole `unit` tier also passed: Patina.Graphs.Tests 431 of 431, and Patina.Server.Tests 161 passed with 6 skipped. I did not run the 8192 regression tier.

### Blur's 8192 peak rose by 366 MB after the scratch fix, and nobody looked into why
- **Where:** `docs/tasks/T-173-rv-76-answers-the-evaluation-engine.md:50`. The code is `src/Patina.Graphs/Nodes/BlurNode.cs:143-153` and `src/Patina.Graphs/Evaluation/TileCache.cs:124-158` (`TakeScratch` and `ReleaseScratch`).
- **Severity:** Minor
- **Follows:** RV-76's "Blur and distance's scratch buffers are large objects allocated per tile" and "Kept tile arrays are outside the bound".
- **What is wrong:**
  - Round 1's case was the blur's Debug peak at 8192, which was 1,226 MB. It said the peak moved with large-object garbage that scales with the radius. The fix's whole aim was to remove that garbage.
  - T-173's own regression run puts the blur at **1,592 MB** after the fix, 30% higher. It says "It is not looked into here".
  - The only proof that the fix works is the allocation count at 1024 (`A_separable_blur_allocates_little_past_its_cache`, 397 to 18 MiB). The one measurement at the size the finding was about points the other way.
  - Arithmetic on the code rules out the kept scratch as the cause. At 8192 it is two arrays of about 4 MB each, since a row tile and a column tile ask for the same length, `3 x 128 x (128 + 2 x reach)`.
  - So the rise has no explanation. The margin under the 2,048 MB milestone bound shrank from 822 MB to 456 MB.
  - The answer "fixed" holds for allocation. It is not shown for the peak working set the finding cited. Until the 1,592 MB is explained, or reproduced as noise by a rerun on main, the claim that the bound is unaffected is not supported.

**Outcome:** —

### The open question on putting waiting arrays and scratch under the bound is left nowhere the person will rule on it
- **Where:** `docs/reviews/RV-76-review-of-m-28.md:40` (the Fix: "left as a question for the person rather than landed") and `docs/tasks/T-173-...md:40` (As built).
- **Severity:** Nit
- **Follows:** RV-76's "Kept tile arrays are outside the bound".
- **What is wrong:**
  - The answer is "fixed", but its own text says the policy half is a question for the person.
  - The question sits only in a Fix line and an As built bullet. There is no Notes entry asking it, and no line in `docs/milestones/M-28-a-node-library.md`. A grep for `LastHeldBytes`, `12,640` and "question for the person" over the milestone and T-173 to T-177 finds only those two lines.
  - Once RV-76 closes, nothing carries the question forward. It should be filed as a question to the person, or the Fix should say the contract change is the whole answer.

**Outcome:** —

### The new role `metallic_roughness` is also a legal name for a named output, so check can read that output's file as packed
- **Where:** `src/Patina.Graphs/Checks/CheckSet.cs:103, 118-126`, `src/Patina.Server/CheckTool.cs:212` and `src/Patina.Graphs/GraphValidator.cs:584-585`.
- **Severity:** Nit
- **Follows:** RV-76's "check on files reads the blue channel of any roughness PNG as metallic".
- **What is wrong:**
  - The validator refuses a named output only when its name is a role (`OutputRoles.TryParse`). `metallic_roughness` is not a role, so a graph can have a named output called that.
  - export writes it as `<folder>/metallic_roughness.png` (ExportTool.cs:43).
  - check's schema says a file's role is "a role or a named output's name". Giving that file with its own name as the role makes `OfPngs` read it as the packed image: an output named `roughness` from G, plus a derived `metallic` from B. That gives metallic-not-binary and base-colour-range faults for an output that is neither.
  - The validator should treat `metallic_roughness` as reserved, as it does the role names.

**Outcome:** —

### worley's ring bound is tested only at jitter 1, where its jitter margin is zero
- **Where:** `src/Patina.Graphs/Nodes/WorleyNode.cs:69-73` (`edge = ... + (1 - jitter) / 2`) and `tests/Patina.Graphs.Tests/WorleyFbmWarpTests.cs` (`WorleyFindsTheTrueNearestTwoPointsAsASearchOfEveryCellDoes`, jitter fixed at 1).
- **Severity:** Nit
- **Follows:** RV-76's "worley's 3 x 3 search misses the true second-nearest point at the default jitter 1".
- **What is wrong:**
  - The early stop uses a margin of `(1 - jitter) / 2`. That is right for `Point`'s placement in `[(1-j)/2, 1-(1-j)/2]`.
  - The brute-force test runs only at jitter 1, where the margin is 0. A wrong margin would pass it, and it would stop the search too early at every other jitter, for example `(1 + jitter) / 2`, or `jitter / 2` at jitter 0.5.
  - The committed samples, at 0.85 and 0.9, catch a miss only if one happens to fall in their images.
  - One more jitter in the theory, such as 0.5, would hold the bound.

**Outcome:** —

## Findings: round 2, the format, server and scenarios (claude-opus-5-5)

Reviewed on Claude Opus 5.5 (`claude-opus-5-5`), read only in `D:\Repos\carve\branches\milestone\M-28` at `4dc1b60`, fix diff `b8df3fd..HEAD`. Probes: `%TEMP%\m28-review2\probe\r2.fsx` (`r2.log`, `r2b.log`) against a copy of the worktree's `Patina.Graphs.dll` built after HEAD, and `old.fsx` (`old.log`) against `b8df3fd`'s `src/Patina.Graphs`, extracted with `git archive` and built in `%TEMP%\m28-review2\old`.

### Fan-out through parameter drives is still exponential: the node cap bounds only expansion
- **Where:** `src/Patina.Graphs/GraphValidator.cs:403-417` (`NestedParameters` reads, parses and re-runs `GraphParameters.Analyse` on the nested file for each drive) and `src/Patina.Graphs/GraphParameters.cs:142` (called per drive, before the "drives twice" / "drives already" faults). `GraphValidator.cs:73` runs this analysis again at every level of expansion.
- **Severity:** Minor
- **Follows:** RV-76 "Nested expansion has no bound but depth: fan-out makes validation exponential". Round 1 named `GraphParameters.Analyse`/`NestedParameters` as a second path ("re-read and re-analyse the nested file on each override, on `Of`, and on each drive").
- **What is wrong:**
  - `MaxNodes` is charged only in `Expand`. The drive path reads files outside the budget and with no cache.
  - Probe `r2.fsx`: nine files of 2 nodes each. File k has one `graph` node `g` on file k-1 and one parameter `p` whose `drives` names `g.p` F times. Each level expands once, so the budget never comes near 4096.
  - Results: F = 2 validated in 0.54 s, and F = 4 in 38.7 s. F = 6 had not finished when `timeout` stopped it after about 260 s. In every case the only fault was an unrelated kind fault in f0.
  - So a small hostile file still hangs `open`, which is what the answer says is now a fault. `Fan_out_past_the_node_cap_is_a_fault_not_a_hang` tests only graph-node fan-out.
  - Fix: charge the budget for each `NestedParameters` read, or cache nested files per validation. Separately, the "twice" and "already" checks could run before the read.

**Outcome:** —

### An outer graph node whose id expansion also makes still throws, not a fault
- **Where:** `src/Patina.Graphs/GraphValidator.cs:505-512` (`Kinds`'s `Visit` reads `a.Nodes[id]`), with `:85` (a graph node is never put in `nodes`) and `:353` (expansion now skips an id the graph has).
- **Severity:** Minor
- **Follows:** RV-76 "An outer node whose id collides with an expanded id replaces the nested node, with no fault".
- **What is wrong:**
  - The fix and its test cover an outer node of a plain type (`colour`). Take instead an outer `"g/c": { "type": "graph", ... }` beside graph node `g`, whose file has `c`.
  - `TexGraphFile.ReadFile` throws `KeyNotFoundException: The given key 'g/c' was not present in the dictionary` from `GraphValidator.Kinds` (probe `r2b.log`). So `open`, `define_graph` and `edit_graph` give the SDK's generic error, not a fault.
  - `b8df3fd`'s build throws the same (`old.log`), so this is not a regression. The hole the finding named is only closed for non-graph outer nodes, though.
  - A graph node with a plain bad id (`"Bad Id"`) is faulted correctly. Only a collision throws.

**Outcome:** —

### The swapped metallic break is implied by the check it breaks, so it cannot fail while that check passes
- **Where:** `scenarios/Carve.Scenario.Patina/SwatchStage.cs:397-400`, `MetallicReads` at `:476`.
- **Severity:** Minor
- **Follows:** RV-76 "The metallic break cannot fail".
- **What is wrong:**
  - `MetallicReads(d, m)` is `d.Median - m.Median >= 40 && m.Clipped > d.Clipped`.
  - The check needs `lit[0].Median - lit[1].Median >= 40`. The break fails only if `lit[1].Median - lit[0].Median >= 40`. Both cannot hold.
  - So on every run where the check passes, the break passes by arithmetic, whatever patina drew. It can fail only when the check fails too, which is what T-177's As built shows ("failed the rule and the swapped break").
  - The self-comparison it replaces was constant in the same way, so the break still never shows the rule catching a wrong frame that the check alone would not. Round 1 suggested the swap, but it does not meet that finding's own test.
  - A real break judges metallic 0 against a different frame that is wrong but not swapped. Examples: a dielectric sphere that is darker or glossier at metallic 0, which shows the clipped-pixel half of the rule matters; or the metallic-1 material drawn with metallic dropped.

**Outcome:** —

### A save into a folder holding a different file of the same name changes the graph silently
- **Where:** `src/Patina.Server/FileTools.cs:126-136`.
- **Severity:** Minor
- **Follows:** RV-76 "A save to another folder breaks the graph's graph nodes, after the save is reported as a success".
- **What is wrong:**
  - Save now judges the graph in the target folder and refuses it when a nested file is missing there. When a file of the same name exists there, the graph validates and is written, and `store.Saved` re-places it.
  - From then on its `graph` nodes are that other file. The outputs, its exposed parameters and its inputs all change.
  - The result is still `saved ...: no faults`, describing the graph as it was before the move. Nothing tells the agent that the nested file changed.
  - T-176's judgement call ("rewriting the paths would change what the graph names") applies here the other way: not rewriting changes what the graph names.
  - The new test covers this setup, with `bricks.texgraph` copied into `materials/` and then saved, but only with an identical copy. So nothing would see a difference.
  - At least, the result should name each nested file that resolves to a different full path than it did before the save.

**Outcome:** —

### The changelog still carries T-172's old line beside the corrected one
- **Where:** `docs/changelog/UNRELEASED.md:22` (old) and `:20` (new).
- **Severity:** Minor
- **Follows:** RV-76 "T-155's line in the changelog, the milestone and its own description claims the 10 s it did not meet" (its T-172 part).
- **What is wrong:**
  - The fix added a corrected T-172 line at `:20` but left the original at `:22`. The original still says "by smaller textures where evaluation is the cost, otherwise by splitting the library stage into a suite of its own". That is the goal's wording the finding objected to, and it disagrees with line 20 (the split into two halves).
  - The changelog is what ships. The milestone (`:181`) and T-172's description (`:3`) are corrected.

**Outcome:** —

### CLAUDE.md says the label atlas adds a page for each 16 labels; it is each 32
- **Where:** `CLAUDE.md:73` ("a label atlas of 32 rows a page, a page more for each 16 labels past 32 (T-176)"), against `src/Patina.Server/SwatchImages.cs:154` (`Pages(labels) = ceil(labels / 32)`).
- **Severity:** Nit
- **Follows:** RV-76 "A sheet of more than 15 outputs throws instead of drawing or refusing".
- **What is wrong:** a page holds `LabelAtlas.Rows` = 32 labels, so 33 to 64 labels take two pages and 65 take three (`SwatchTests` asserts `[1, 1, 2, 2, 3]` for 0, 32, 33, 64, 65). "16" would be right only if it meant columns, at two labels each.

**Outcome:** —

### CLAUDE.md's Patina scenario row does not name the new WideSheetStage
- **Where:** `CLAUDE.md:88`, against `scenarios/Carve.Scenario.Patina/PatinaStages.cs:20` (`new WideSheetStage()`, in `base`).
- **Severity:** Nit
- **Follows:** RV-76 "CLAUDE.md's Patina scenario row leaves out the split, the nested stage and the cube".
- **What is wrong:**
  - The row names each stage file and was rewritten by T-177 to add the ones it left out. T-176, in parallel, added `WideSheetStage.cs` (18 columns, the second atlas page) and its line in `PatinaStages.cs`.
  - `grep -c WideSheet CLAUDE.md` gives 0. This is the same omission the finding fixed, recreated by the parallel members.

**Outcome:** —

### The node cap counts the graph's own nodes only once it uses a graph
- **Where:** `src/Patina.Graphs/GraphValidator.cs:28-33` (the summary: "the graph's own and every nested file's"), `:60` (the budget starts at the own count) and `:321` (the only check).
- **Severity:** Nit
- **Follows:** RV-76 "Nested expansion has no bound but depth".
- **What is wrong:**
  - Probe `r2b.log`: a flat graph of 5,000 nodes loads with 0 faults. A graph of 4,095 own nodes with one use of a 2-node file is refused, with the fault on `g`'s `path` ("takes the nodes validated past 4096").
  - So the cap is not "how many nodes one validation may take in", as its summary and the T-175 As built say. It is a cap on nesting that the graph's own size eats into.
  - Real graphs are far from it: the eight samples take in 9 to 33 nodes (old-wall 33, measured the validator's way), and T-143's acceptance graphs have 28 to 34. So 4096 costs nothing today. Either word the summary as a cap on what nesting adds, or check the own count too.

**Outcome:** —

### WideSheetStage would pass with the wrong labels on the second page, and the unit test does not exercise the paging
- **Where:** `scenarios/Carve.Scenario.Patina/WideSheetStage.cs:48-63`, and `tests/Patina.Server.Tests/SwatchTests.cs` (`A_sheet_of_more_than_15_outputs_reads_its_labels_from_a_second_atlas_page`).
- **Severity:** Nit
- **Follows:** RV-76 "A sheet of more than 15 outputs throws instead of drawing or refusing".
- **What is wrong:**
  - `Lettered` passes if any pixel in a 40 x 18 band has R > 200. So page 2 drawn with page 1's labels again, or with both pages' text overlaid, would pass. So would labels offset by a row. No frame is captured, and nobody has looked at one.
  - The unit test hands `Compose` a two-page atlas it made itself, with 38 extents. `Compose` did not change, so this test passes on the code before the fix too.
  - The paging the fix added is in `SwatchRendering.cs`: `Page(p)` draws into the same surface, followed by `OffscreenFrame.ReadAsync`, which renders a frame. Only the scenario reaches it, and the scenario cannot tell the right text.
  - Comparing one label's band with the label's extent (or with the text's width, from `MeasureText`) would make the check specific.

**Outcome:** —

### T-175's How lines name a commit that is not on the milestone
- **Where:** `docs/reviews/RV-76-review-of-m-28.md`, the six `**How:** 1f69cb2 (T-175, ...)` lines under `## Findings: the file format and graph model`.
- **Severity:** Nit
- **What is wrong:**
  - `git merge-base --is-ancestor 1f69cb2 HEAD` fails. The fix landed as `a1ffbd9`, which has the same `src` and `tests`.
  - Commit 622a7b6 ("the How lines name the rebased commit") changed them from `4263e7c` to `1f69cb2`. `cairn finish` then rebased again.

**Outcome:** —

## Summary

Round 2 of M-28's review: it checks RV-76's 36 answers against the fixes (`b8df3fd..HEAD`, members T-173 to T-177) and reviews the fix code. Split in two by area, matching the fix members. 14 findings: 0 Major, 6 Minor, 8 Nit. Of RV-76's answers, 18 in the engine, nodes and checks held, and every answer in the format, server and scenarios held except where a finding below follows it (fan-out through parameter drives, a colliding outer `graph` node, the swapped metallic break). The first reviewer ran the whole unit tier (431 and 161 passed, 6 skipped); the second proved its findings with probes against built code, and ran no suites.
