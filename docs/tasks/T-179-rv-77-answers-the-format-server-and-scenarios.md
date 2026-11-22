---
description: >-
  Every finding of RV-77's `## Findings: round 2, the format, server and scenarios` is answered
---
# T-179 — RV-77 answers: the format, server and scenarios

**Status:** In Progress

**Pri:** P1

**Milestone:** M-28

**Batch:** — (standalone)

**Depends on:** — (none)

**Verify:** patina-unit, scenario-patina, scenario-patina-library-1, scenario-patina-library-2

## Goal

Every finding of RV-77's `## Findings: round 2, the format, server and scenarios` is answered, so that M-28's review can close with a light confirming round.

## Scope

- All ten findings under `## Findings: round 2, the format, server and scenarios (claude-opus-5-5)` in RV-77.
- Each fix with a test that fails without it, shown failing where a test can hold it.
- Fill each finding's `**Outcome:**` line in RV-77, in this task's worktree.

## Notes

- **Decided (2026-11-21, by the person):** three of these follow RV-76 findings whose fixes did not hold, and the person chose to fix them now, with a light round 3 to confirm: fan-out through parameter drives (validation must be bounded, as the 4096-node cap bounds expansion); an outer `graph`-type node colliding with an expanded id (a fault, never a throw); and the swapped metallic break (a break must be able to fail on its own, not only with the check it breaks).
- Owns `GraphValidator.cs`, `GraphParameters.cs`, `FileTools.cs`, `GraphStore.cs`, scenario-patina's stages, `CLAUDE.md`'s rows, the changelog and the tracker lines. T-178 edits the milestone file's decisions; keep both sides on a rebase.
- **Answering RV-77:** for each finding, fix it or answer it `skipped: <reason>` or `no change needed: <reason>`, by filling its `**Outcome:**` line in `docs/reviews/RV-77-review-of-m-28-round-2.md` in this task's worktree. The line is exactly `**Outcome:** fixed` (or the other two forms); put any detail on a following `**Fix:**` line, since `cairn lint` accepts nothing after `fixed`. Run `cairn lint` and read its output before `finish`.
- A fix that would change behaviour beyond the finding, loosen a check, or change a committed output or an approved frame goes to the person first.
- Opus 5.5: Sonnet is under its weekly usage limit until 2026-11-23.

### Hand-off

First agent, Claude Opus 5.5 (`claude-opus-5-5`), handed off at its call checkpoint. Code for all ten findings is in this commit; nothing is verified yet and no `**Outcome:**` line in RV-77 is filled.

**Done (uncommitted work committed here):**
- *Drive fan-out (repeat):* `GraphValidator.NodeBudget` is now internal and carries a per-validation cache of `NestedParameters` reads keyed by the chain of files plus the file; each read on a miss is charged to `MaxNodes` (at least 1). `GraphParameters.Analyse` and an internal `WithOverrides` overload take the chain and budget; `Expand` passes them (its per-override `WithOverrides`, the final one, and `use.Parameters`). Past the cap a drive is a fault on its parameter ("reading X for its range takes the nodes validated past 4096"). Tests `Fan_out_through_drives_is_bounded(2, 6)` (distinct parameters `p1..pF` each driving `g.p<k>`, nine files) and `A_file_read_for_a_drive_is_charged_to_the_node_cap`.
- *Colliding graph-type node (repeat):* `Expand` leaves a use unexpanded (no nodes added, no fault of its own) when an outer `graph` node has an id the expansion makes; the outer node's id fault is the only one. Test `An_outer_graph_node_with_an_expanded_id_is_faulted_not_thrown` (both the Inner shape and the reviewer's probe shape through `ReadFile`).
- *Shown failing without the fix* (src/Patina.Graphs and FileTools.cs stashed, log `%TEMP%\t179-without-fix.log`): fan 6 "validation had not finished after 00:00:20" (the runaway kept the host 11 min); the collision test failed; the charge test failed; fan 2 passed. With the fix all 461 Patina.Graphs tests passed (2 m 22 s). The server project's without-fix run printed nothing in that log: **re-run** `Save_into_a_folder_holding_another_graph_of_the_same_name_is_refused_naming_both_files` without `FileTools.cs`'s change to show it failing.
- *Metallic break (repeat):* `SwatchStage` replaces the swapped break with a darker dielectric (grey 100, metallic 0, same roughness, new frame `swatch-sphere-metallic-0-dark`), judged as the metal against metallic 0; only the rule's clipped half refuses it. Writes `metallic_break_dark` lines to `swatch-checks.tsv`. Last job's numbers: metallic 0 median 192.3, clipped 261; metallic 1 126.9, 1256.
- *Save into a folder holding another file:* decided by save's rule (a saved graph is the graph defined). `SaveTool.Changed` compares each expanded use's nested files now and from the target folder (same count, and each pair at a different path must hold the same graph as `TexGraphFile.Write` gives it); otherwise refused naming both files. A use not resolving now is not compared, so define-then-save-beside-the-files still works. Save's description gained a clause. Test in `NestedGraphToolTests` (seed changed: refused; same graph with other line endings: saved).
- *Changelog:* T-172's old line removed. *CLAUDE.md:* "each 32 labels"; the Patina row names `WideSheetStage.cs`. *Node cap summary:* reworded as a cap on what nesting takes in (behaviour unchanged). *RV-76 How lines:* `1f69cb2` -> `a1ffbd9` (checked an ancestor).
- *WideSheetStage:* names now `mx`, `mxx`, ... (each a letter longer); each column's flat and tiled label is judged against its backing (text ends within `TextSlack` = 4 px of the padding, never in it), and the tiled label must begin with the flat label's pixels; writes `wide-sheet-labels.tsv`. Builds; **never run** — `TextSlack` and the `< 0x18` backing threshold are guesses to check against the tsv. The unit test of `Compose` is left as is (its paging is in `SwatchRendering`, engine-only).

**Left:**
1. Run `cairn task T-179 verify`; fix WideSheetStage's thresholds from `wide-sheet-labels.tsv` if it fails (a looser threshold than the text needs goes to the person).
2. Show the metallic break failing while the check passes: one `base` run with `MetallicReads` reduced to its median half (planted, not committed); expect the check to pass and the dark break to fail.
3. Frames: compare by SHA-256 against the last good scenario-patina job (`J-20261121-184545-5c2ba5` or newer), fetched by job id; only `swatch-sphere-metallic-0-dark` should be new; open a scaled copy of it.
4. Re-run the full patina-unit (the full Patina.Server.Tests run with the fix gave no output here).
5. Fill the ten `**Outcome:**` lines in RV-77 (`**Outcome:** fixed` + `**Fix:**`), `### As built`, `cairn lint`, commit, `finish`.

**Surprising:** backslash escapes in Python heredocs through the Bash tool were consumed into real newlines and tabs twice; fixed with `chr(92)`. A `cairn test artifacts scenario-patina` run from the container root wrote `test-artifacts/` there; moved to `%TEMP%\t179-patina-artifacts`.
