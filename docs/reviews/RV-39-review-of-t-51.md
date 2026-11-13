---
description: >-
  Review of T-51
---
# RV-39 — Review of T-51

**Status:** Answered

**Reviews:** T-51

**Round:** 1

**Previous:** —

**Diff:** `b0cb8a4c67fa9c4287114c9d85ef3ad1b811c44c against origin/main` (at 83242a4)

**Reviewer:** two fresh general-purpose agents on Opus 5.5, one on the product code, one on the tests and docs

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings

## Findings: tests and docs

### Tests that search upward can modify a real carve-limits.json outside the temp folder

`tests/Carve.Server.Tests/ExportLimitsToolTests.cs:51` (`Write_limits_with_no_file_found_creates_it_beside_the_glb`) calls `export` with `write_limits: true` and no `limits` argument, so `LimitsSource.Find` (`src/Carve.Server/GameLimits.cs:188-192`) searches from the temp folder all the way to the drive root. Suppose any ancestor of `%TEMP%` (`C:\Users\<user>\AppData\Local`, `C:\Users\<user>`, `C:\Users`, `C:\`) holds a `carve-limits.json` with a key missing. The tool then fills that key and writes the real file, and only after that does the test's assertion fail. `GameLimitsTests.cs:132` does the same search, but its path assertion fails before `source.Write` at `:138`. The same stray file would make `GameLimitsTests.cs:100` (`No_file_means_no_limits_and_the_result_says_so`) and the exact `Assert.Equal` at `tests/Carve.Server.Tests/ExportToolTests.cs:26-27` fail, and would add limit warnings to every other export test. The T-51 *Not verified* note (`docs/tasks/T-51-*.md:48`) records the assumption, but not that a broken assumption makes the tests write outside their temp folder. Rubric 6 (tests placed), and "tests that depend on the machine". Nothing in the tests pins down where the search stops. They could name a `limits` path inside `Root`, or check first that no ancestor holds the file.

**Outcome:** fixed: `ExportLimitsToolTests.NoLimitsFileAboveRoot` runs first in every test that searches upward with the tool, and fails, writing nothing, when a carve-limits.json sits in any folder above the temp root. The *Not verified* note already records the assumption.

### No tool-level test of limits on a skinned export or in clips mode

`ExportLimitsToolTests` (`tests/Carve.Server.Tests/ExportLimitsToolTests.cs`) only exports the static cube. The `Skinned` and `Clips` changes in `src/Carve.Server/ExportTool.cs` are not run through the tool with a limits file anywhere: the `source.Describe` clause on the skinned line and the clips line, the warnings fed from `glb.Triangles` and `glb.Materials.Count`, and clips mode reading and filling the file, which its description promises. `SkinnedExportToolTests.cs:85` and `ClipLibraryToolTests.cs:35` use `Contains` on a prefix, so they pass whether the limits clause is there or not. Three spec points have no test that would fail if they broke:
- "With no file there are no limit warnings" on a skinned model. Nothing exports a skinned model past 5000 triangles or 8 submeshes with no file, so a regression that brought `GameLimits.Starting` back as a default for skinned exports would pass. The gardener's 7 primitives sit under 8.
- The static submesh warning through the tool, which counts with `GlbExport.MaterialNames(mesh).Length`. `ExportLimitsToolTests.cs:20` sets `max_submeshes: 1` on a 1-material cube, so it never trips. Only the unit test checks it, with hand-passed counts.
- `write_limits` with `limits` naming a `.json` file that does not exist yet (creating it at the named path).

Rubric 2.

**Outcome:** fixed: `A_skinned_export_warns_only_on_the_games_file_and_clips_mode_reads_and_writes_it` covers a skinned export with no file (no warnings) and with a file (triangles and submeshes), and clips mode filling the file with no warnings. The static test now triggers `max_submeshes` through a coloured face, and `Write_limits_creates_a_named_json_file_that_is_not_there_yet` covers a named `.json` that does not exist yet.

### Height test loosened so a swapped bound would pass

`tests/Carve.Server.Tests/SkinnedExportTests.cs:351`: the theory now matches `(below the game's min_height 1\.4|above the game's max_height 2\.4)` for both 1.39 m and 2.41 m. A model that is too short reported as "above max_height", or a too-tall one as "below min_height", passes. Before the change, both cases matched one fixed text. The case knows which bound it crosses, so it can assert the exact one (`below ... min_height` for 1.39, `above ... max_height` for 2.41). `GameLimitsTests.cs:146-147` does check each direction separately on a 1 m model, so the loss is confined to this theory. The As built does not mention it. Rubric 2 ("a test that would pass even if the behaviour broke").

**Outcome:** fixed: each height case now names its own message (1.39 is below min_height 1.4, 2.41 is above max_height 2.4).

### subdivide's description, "warns on no limit", reads two ways

`src/Carve.Server/SubdivideTool.cs:24`: "subdivide itself warns on no limit." An agent can read this as "subdivide warns when there is no limit", which is the opposite of what is meant (it never warns on any limit). Plain wording would remove the misreading, for example "subdivide gives no limit warnings". The test name at `tests/Carve.Server.Tests/SubdivideToolTests.cs:100` (`..._warns_on_no_limit`) has the same wording.

**Outcome:** fixed: the description now says "subdivide gives no game-limit warning of its own", and the test is renamed `..._gives_the_count_and_no_limit_warning`.

## Findings: product code

### The `limits` argument skips the path rules, so some bad arguments throw and others fail only after the .glb is written

`LimitsSource.Find` (src/Carve.Server/GameLimits.cs:171-179) checks only for an empty string and `GetInvalidPathChars`, then calls `Path.GetFullPath` outside any try. It does not use the rules `ToolPaths.Resolve` applies to `path` (whitespace-only, `*`/`?`/a second `:` in a segment, too long). That causes two problems (rubric 1, correctness and error paths):

- `limits: "   "` or a path over the length limit makes `GetFullPath` throw `ArgumentException` / `PathTooLongException` (checked on this machine's .NET). The exception escapes `Find` and `Run` (ExportTool.cs:115), so the caller gets an unhandled exception instead of a one-line `export: limits: ...` tool error. Nothing is written, but the error does not follow ToolPaths' rule that every error is one line starting with the tool's name.
- With `write_limits: true`, an argument that can never be written passes `Find`: `C:\game\lim?ts.json`, an existing non-`.json` file such as `limits.txt` (it becomes `limits.txt\carve-limits.json`), or a folder named `carve-limits.json` inside the named folder. The `.glb` is then written, and only `source.Write` (ExportTool.cs:124) fails. The result is a tool error *and* a written `.glb`, which goes against the ruling that a bad limits argument is a tool error that writes nothing. All of these could be refused in `Find` before anything is written: resolve with the same checks as `ToolPaths.Resolve`, and reject a write target that is an existing folder or whose parent is an existing file.

**Outcome:** fixed: `LimitsSource.Find` refuses a blank argument, sends the path through `ToolPaths.Resolve` (bad characters and invalid paths become one-line tool errors), and refuses a non-.json file and a path that is a folder, all before the .glb is written. Covered by `A_limits_argument_no_file_could_be_at_is_refused_even_with_write_limits` and a tool-level case in `ExportLimitsToolTests`. The description lists these refusals.

### `write_limits` rewrites a hand-edited limits file from the parsed values, dropping comments and changing number text

`GameLimits.Parse` accepts comments and trailing commas on purpose (GameLimits.cs:90, `JsonCommentHandling.Skip`, `AllowTrailingCommas`), which invites a person to annotate the game's file. When `write_limits` fills a missing key, `LimitsSource.Write` (GameLimits.cs:229-235) writes `ToFileText()` (GameLimits.cs:124-128) built from the parsed record. Every comment, the key order and the spacing are silently lost. Number text is also normalised: `2.5e0` becomes `2.5`, `2.40` becomes `2.4`, and a height with more than about 7 significant digits is re-rounded through `float` (`1.41234567891` becomes `1.4123457`). With duplicate keys, only the last one survives. The ruling says a fill never changes a key that is set. The value is changed only in that last precision case, but the person's file content is changed in every case, and neither the result line nor the tool description says so (rubric 1). Possible fixes: refuse to fill a file that holds comments; add the missing keys to the original text; or at least say in the description that a fill rewrites the file in canonical form.

**Outcome:** fixed: `Parse` no longer accepts comments or trailing commas, so no comment can be lost (`A_comment_is_refused_since_write_limits_would_lose_it`). The description now says write_limits rewrites the file with the keys in a fixed order. Number text being normalised (2.40 to 2.4) and heights held as float are by design, matching how coordinates are handled.

## Summary

Six findings, all fixed in the commit after 83242a4: one in the `limits` path handling and one in the file format (product code), and four in test coverage and wording. Nothing was found in the warnings logic, the float comparisons at the limits, clips mode, or the refused-export paths.
