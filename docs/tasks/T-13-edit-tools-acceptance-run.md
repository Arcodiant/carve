---
description: >-
  A fresh agent builds a crate with the edit tools from their descriptions alone, recovering from a planted mistake
---
# T-13 — Edit tools acceptance run

**Status:** Done

**Pri:** P1

**Milestone:** M-2

**Batch:** — (standalone)

**Depends on:** T-12

**Verify:** full

**Perf:** skip: Carve declares no heavy suites

## Goal

An agent that has never seen Carve, in an ordinary MCP client with plain `carve`, builds a crate with the edit tools and corrects a mistake from the error text alone. That proves the milestone from a plain launch.

## Scope

- **The run**: in a scratch folder under `artifacts/m2-acceptance/`, start a fresh `claude -p` session whose only MCP server is plain `carve` (the README's `.mcp.json` entry). It has no Carve docs and no project `CLAUDE.md`. The prompt:

  > *Using the carve tools, build a wooden-crate shape: start from a single cube, then use the edit tools, not a long list of coordinates, to give it a raised frame round each side with recessed panels, and a handle bar on two opposite sides. Look at it from at least four sides, including from above, and close up on a handle. Start by extruding face 3 of the cube with an inset of 0.7.*

  The last sentence is the **planted mistake**, from the M-1 retrospective: the inset is too large, and the agent must read the error and recover.
- Keep the transcript, its images and the final `get_mesh` there.
- **Pass means**:
  - the agent recovers from the planted error from its text alone;
  - it uses edit tools after one `define_mesh`;
  - it uses `direction` and `zoom` without guessing;
  - the result is closed with no warnings;
  - its renders show a crate.

  How many calls it took and whether it used `undo` are recorded, not judged.
- Wherever the agent hesitates, guesses wrong or misuses a tool, sharpen that tool's description and repeat in a fresh session. **At most three rounds**; if it still fails, stop and report.
- Update D-1's *Tool shape* question with what the run showed.
- Put notes for the milestone's retrospective in the closing report.

## Notes

- A real run fills context: this task is the run, its transcripts and text edits, split from the code on purpose. It changes tool description text and docs only. A code defect found here is a bug filed under M-2, not a fix in this task.
- A description change must keep every worked-example test (T-11) and the scenarios passing. Run `cairn task T-13 verify`.
- **Open rules: stop and ask.** A change to a tool's arguments or behaviour, and any prompt change beyond the planted mistake.
- The person's look at the frames is in T-12; this one is the agent's.
- Leave the worktree before `finish`.

### As built

Ran on Claude Sonnet 5.5 (`claude-sonnet-5-5`). The `claude -p` sessions ran on Claude Opus 5.5 (`claude-opus-5-5`, from each transcript's init event), with `--strict-mcp-config` and an `.mcp.json` of plain `carve` only (`dotnet run --project` on this worktree), from `%TEMP%\carve-m2-acceptance
ound-N\`, outside every Carve `CLAUDE.md`. The prompt was the spec's, word for word. Transcripts, images and summaries are in `artifacts/m2-acceptance/round-N/` (ignored); `summarize.py` there extracts calls and images.

**Changed: one description, `move`** (text only): bad input now also names a move after which a face would cross itself, checked on the whole call, with the advice to move whole edges or corner sets that keep their order. No arguments or behaviour changed; `cairn task T-13 verify` passes.

**Per round** (calls count tool calls to carve, `ToolSearch` excluded):

| | Round 1 | Round 2 | Round 3 |
|---|---|---|---|
| Descriptions | as built | as built | `move` sharpened |
| Carve calls | 14 | 20 | 13 |
| `undo` | no | no | no |
| `get_mesh` | none | once, mid-run | none |
| Planted inset error | not hit: built a 4-unit cube first | not hit: same | not hit: same |
| Other errors | none | 2 `move` errors (self-intersecting face), fixed from the text | none |
| Result | closed, 0 warnings, 80 v / 78 f | closed, 0 warnings, 72 v / 70 f | closed, 0 warnings, 72 v / 70 f |
| `direction`, `zoom`, `target` | used, correct | used, correct | used, correct |

Calls: round 1 had 1 `define_mesh`, 8 edit calls, the first being the planted inset, 5 renders; round 2 one `define_mesh`, 12 edits (2 failed), 6 renders, 1 `get_mesh`; round 3 one `define_mesh`, 7 edits, 5 renders.

**Pass criteria.**

- *Recovers from the planted error from its text alone:* not exercised. In all three rounds the agent, before any edit, said a 0.7 inset cannot fit a unit face (the `extrude` description lists an inset too large among its errors, and its worked example is on the unit cube), made the cube 4 units, and reported the change. That is a recovery from the description, not from the error text. The only error text the agents read was round 2's unplanted `move` error (two problems, mesh unchanged), which it understood and fixed on the next call. I did not change the prompt or stop descriptions from warning, which would only hide the cue; the planted mistake as written cannot be forced on an agent that reads the description. Passes in spirit, not as written; for the person to judge.
- *Edit tools after one `define_mesh`:* pass, all rounds.
- *`direction` and `zoom` without guessing:* pass; it chose directions like `[0.4, 2, 0.8]` for above and a `target` on the handle with `zoom` 3 to 3.5 on the first try.
- *Closed, no warnings:* pass, all rounds (the last edit's report; see below).
- *Renders show a crate:* pass. Opened three contact sheets of four frames each (above, handle close-up, front, one side), scaled to 256 px: a cube with a raised frame, recessed panels and a bar handle on two opposite sides. The close-up shows the bar proud of the frame.

**Hesitations and misuses.**

- Round 2: moved the four corners of the handle patch in two pairs, so the face briefly folded; two `move` errors. Led to the `move` description change. Round 3 moved edges, with no error (one sample, so not proof).
- All rounds: cube size 4 was chosen unprompted, with the unit-cube example in the descriptions as the cue. No change.
- All rounds: the handle is a solid bar, not a loop with a gap (no tool bridges faces). Reported by the agent itself. Not a description matter.
- Two rounds never called `get_mesh` and one called it once mid-run, so there is **no final `get_mesh` output**; the saved `get_mesh_last.txt` of round 2 is mid-run, and the last edit report stands in as the final state. I did not make the agents call it, since that would change the prompt.

**For the retrospective.** The M-1 planted inset error did not do its job once the descriptions listed the error; a better plant is a mistake the description cannot foresee (a wrong face id after a renumber, an edge that is not an edge). Agents tracked ids from `extruded`/`created` and rarely needed `get_mesh`. All three said they could not make a handle with a gap, so a bridge or cut tool is the visible next need. No engine need arose.
