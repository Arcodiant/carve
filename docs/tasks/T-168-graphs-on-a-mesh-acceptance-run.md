---
description: >-
  Fresh agents weather the wrench with carve and patina from the tool descriptions alone, and recover from a planted wrong graph path
---
# T-168 — Graphs on a mesh acceptance run

**Status:** In Progress

**Pri:** P1

**Milestone:** M-29

**Batch:** — (standalone)

**Depends on:** T-167, T-207

**Verify:** server-unit, patina-unit, scenario-patina

## Goal

Fresh agents, given only the two servers' tool descriptions, weather a model: they author a mesh-reading graph in
patina, put it on the wrench in carve, judge it by renders, export it, and recover from a starter file whose material
names a graph that is not there.

## Scope

- **The round folder** (prepared by a script, `scripts/prepare-weathering-round.ps1`): `wrench.carve` (a copy of
  `samples/wrench.carve`: 120 faces all mapped, no overlap at 1024, groups `rubber`, `steel`, `dark`, `red`, `black`),
  and the plant, `wrench-start.carve`, a version 10 file whose material `steel` names `graphs/steel-worn.texgraph`
  while the folder holds `graphs/worn-steel.texgraph` (a small valid graph with a `mesh_curvature` node). Both servers'
  working folder is the round folder.
- **The prompt** (in this file, fixed before round 1): open `wrench-start.carve`; get it rendering; then make the
  steel look used, worn bright along its edges and dirty where the square drive meets the socket, leave the rubber
  grip clean; show the wear mask on the layout; save as `wrench-weathered.carve` and export
  `game/Content/models/wrench.glb`. It gives no path of the graph that exists, no node name and no parameter value.
  - Work the prompt against the wrench before round 1: every hard edge is convex and there is no jaw opening
    (M-24's retrospective), so "worn edges" is reachable on every steel edge and "dirty where the drive meets the
    socket" is the one crevice occlusion finds (M-24: at 32 rays and reach 0.035 every crevice texel is at least 50
    below every open texel). A request the wrench cannot satisfy is a planning fault, to be fixed in the prompt.
- **Run conditions**: a fresh headless session per round with `--tools ""` and
  `--allowedTools "mcp__carve__*" "mcp__patina__*"`, both servers started from this build (not the published copy in
  `%LOCALAPPDATA%\carve-mcp`), no file tools, the default model (expect Opus 5.5; record what the session reports). Two
  rounds at least; a round changes nothing but the descriptions between it and the next.
- **What a round must show** (`tests/Carve.WeatheringAcceptance.Tests`, reading `CARVE_WEATHERING_RUN`, skipped when
  unset, a `dotnet-closure` input of `server-unit` as the other acceptance checks are):
  - the transcript: the missing-graph message was received, and the next `define_material` for `steel` names a graph
    that exists (the planted one or one the agent saved), with no file tool used;
  - `wrench-weathered.carve` is version 10 and opens with no warning; its steel faces wear a material with a
    mesh-reading base colour slot;
  - on the bake of that slot (made by the check through `Carve.Graphs`): the mean luma of texels within 2 texels of a
    steel hard edge is above the mean of steel texels more than 12 from any, and the drive crevice's texels are
    darker than the open steel texels around it, each by a margin the person rules after round 1's numbers are seen
    (reported as `a - b`, both named);
  - rubber texels: the base colour slot's standard deviation there is no more than that of the round's own clean
    stand-in (the graph evaluated in patina's neutral mode), so no wear or dirt reached the grip;
  - `wrench.glb` loads through `IModelGeometryLoader.Load` with no warning, and `render_glb` agrees with `render`
    front and three-quarter under D-6's frame rule (the Acceptance scenario's step, extended to this folder).
- **The person looks** at each round's renders (front, three-quarter, close on the drive, the `uvs` frame of the wear
  mask) beside the transcript's summary, and rules pass or fail on the look; a review agent opens the frames first.
- **Recorded in this file per round**: the model reported, calls made, where the agent hesitated or went wrong, each
  description change made in answer, and the times of its bakes.
- A product fault found by a round (not a description fault) is filed as a bug on the milestone and fixed before the
  next round.

### The prompt (fixed before round 1; the copy the rounds use is `scripts/weathering-round/prompt.txt`)

```text
Open wrench-start.carve and get it rendering. Then make the steel look used: worn bright along its edges and dirty where the square drive meets the socket. Leave the rubber grip clean. Show me the wear mask laid out flat on the UV layout, look at your result with renders, and when you are happy save it as wrench-weathered.carve and export it to game/Content/models/wrench.glb. Tell me briefly what you did.
```

Worked against the wrench before round 1 (agent 1, Sonnet 5.5): every request is reachable. Steel edges are all hard and convex, so "worn bright along its edges" has texels on every steel face; "where the square drive meets the socket" is the drive (faces 100 to 105, z -0.042 to -0.020) standing between the dark ring under the head (face 99) and the dark socket top (face 118), the wrench's one crevice (M-24). "Leave the rubber grip clean" is reachable by masking the wear to the steel group. The prompt names no graph path, node, parameter or value. No clash; the plant is `graphs/steel-worn.texgraph` against the folder's `graphs/worn-steel.texgraph` (curvature wear only: the dirt and the grip are the agent's to add).

## Notes

- **From the milestone:** fresh agents weather a model and recover from a planted wrong graph path.
- **The orchestrator's calls, each a stop if it proves wrong:** the wrench as the model; the plant (a path one word
  order away from the file that exists); the checks above. The margins are the person's, after round 1.
- The plant cannot be routed around: `open` succeeds with a warning (T-160), `render` and `export` fail on the steel
  material until it is redefined (T-166), and the agent has no file tool to rename the graph.
- **Run conditions are on the stop list:** a flag, a tool allowance or a model other than those above is asked for,
  not taken (M-27: `--allowedTools` for both servers was approved then; the default model was Opus, not Sonnet).
- Makes a real run: Sonnet for the outer agent, planned as two agents (the rounds, then the record and `finish`).
  Evidence under `D:\repos\carve\artifacts\T-168\round-N\`, an absolute path outside the member's worktree.
- The exported file is read back by the checks (M-27 left its acceptance `.glb` unread).
- **Stop and ask:** before round 1 if the prompt's requests clash with the wrench; after each round for the look; any
  check that would have to loosen.

### Rulings of the person (2026-11-24), recorded by agent 2 (Claude Sonnet 5.5 `claude-sonnet-5-5`)

1. **Model:** every round runs with `--model claude-opus-5-5`.
2. **Margins** (asked with round 1's 93.8 and 22.3): texels within 2 of a steel hard edge are brighter than steel texels more than 12 from any by at least 30 luma; the drive crevice is darker than the open steel by at least 10. Declined: half of round 1's differences, and any positive difference. The sets the margins were ruled on (the first agent's choices, not put to the person): both sets leave out drive faces 100 to 105 from the edge sets; the crevice is the whole drive (faces 100 to 105) more than 2 texels from a hard edge. Not to be changed.
3. **A refused tool call:** the no-file-tool check fails only on a tool call outside the two servers that succeeded; a refused attempt passes and is counted and listed (`acceptance/numbers.tsv`, row `refused-attempts`). Declined: failing on any attempt.
4. **The grip:** "leave the rubber grip clean" is held to what the grip's faces wear in the saved file (their own material's base colour slot, baked; else that material's flat base colour; else the face colour; else glTF's white) against the starter file's grip. Statistic: standard deviation of luma (0.2126 R + 0.7152 G + 0.0722 B, 0 to 255) over the grip's texels (group `rubber`); bound: no more than the starter's grip plus 0.01 (the starter's grip is flat white, spread 0, so no usable looser bound: "no more spread than the starter's grip"). Replaces the check of the bake of the steel slot over the rubber texels.

**Checks after the rulings (agent 2).** Round 1 measured again: all 6 pass (hard-edge 93.778 >= 30; crevice 22.268 >= 10; grip std 0.000 vs starter 0.000; refused attempts 1, Bash at transcript line 15; model, message and next `define_material`, saved file, glb pass). Negatives (`scripts/run-weathering-negatives.ps1`, all caught, reference passes with its refused Bash): no-wear by the hard-edge/crevice test (firm now, not by noise), grip-wear (the steel material given to the rubber faces and the mask widened to `all`) by the grip test, no-dirt by the crevice test, unresolved by the saved-file test, file-tool (a succeeded Bash) by the no-file-tool test, next-define-missing (the next steel `define_material` names `graphs/nothere.texgraph`) by the message test.

### The person's ruling on the crevice check (2026-11-25), recorded by agent 6 (Claude Sonnet 5.5 `claude-sonnet-5-5`)

Shown the table of the junction sets (both rounds, the dirty reference and the no-dirt variant, at 0.004 m and at 0.012 m and wider), and told that the 0.004 m distance had been proposed after seeing that round 2 passes there, the person ruled: **the crevice set is the drive's texels within 0.004 m of the 8 edges of the drive's two ends, all texels of that set, and the check is `open steel mean − that band's mean >= 10` luma.** (Measured: round 1 49.1, round 2 24.9, the reference 60.5, no-dirt −3.5.) Declined: 0.012 m with a margin of 10 (round 2 would fail, and a third round follow), and the 0.004 m band with a margin of 20. The socket's texels are not in the set (those faces do not wear the steel material), and the open-steel set is unchanged. Caveat: the 0.004 m distance was chosen with round 2's number in view.

### The record, per round

| | Round 1 | Round 2 |
|---|---|---|
| Run | 2026-11-24 18:39:48 to 18:42:06 (2 m 18 s) | 2026-11-24 23:34:50 to 23:36:25 (1 m 35 s) |
| Model reported | `claude-opus-5-5` (pinned by `--model`) | `claude-opus-5-5` |
| Calls | 31 | 25 |
| `help` / `nodes` | never / yes (`nodes` for `mesh_occlusion` and `mesh_curvature`) | never / yes (calls 5 and 6) |
| `get_mesh` | `{}`: 53,645 characters, over Claude Code's tool-result limit, saved to a file the agent could not read; it tried `Bash` (refused) | `include` of groups, materials, face_materials: answered, nothing refused |
| Refused attempts | 1 (`Bash`, transcript line 15) | 0 |
| Bakes (`ms` in the tool answers; all 1024 unless noted) | 3654, 972, 2392, 2483 (the four `baked` entries found in the transcript) | 471 (512), 810, 1329 (512), 1042 + 114 (512), 1073, 940 + 123 (512), 335 |
| Hard-edge (margin 30) | 93.778 pass | 61.501 pass |
| Crevice (margin 10, the ruled set) | 49.143 pass (open 94.050, band 44.907, 10,357 texels) | 24.862 pass (open 93.315, band 68.453, 2,604 texels) |
| Grip std (starter 0.000, bound +0.01) | 0.000 pass | 0.000 pass |
| Other checks (message and next `define_material`, saved file v10, no file tool, `.glb` load, render vs `render_glb`) | pass (front 0 px, three-quarter 0 px, front-vs-back control 10,451 px) | pass (0 px) |
| Checks | 6 of 6 | 6 of 6 |
| Look (the person) | approved 2026-11-24: "Generally fine but the wear reads like an edge highlight - how do we expect wear to be implemented?" | approved 2026-11-25, no notes |

**Round 1: where the agent hesitated or went wrong.** Open (warning), `get_mesh {}` (oversize), found `worn-steel.texgraph` from the warning's file list, `Bash` refused, `check`, `nodes`, one `edit_graph` of 9 operations, saved its own `graphs/wrench-steel.texgraph`, then tuning renders and one grime edit. It worked out the drive faces by selecting on `y`/`z` (calls 14 to 24), no wrong turn. It did not mask the wear to the steel group: the grip is not on `steel`, so it is never drawn with the graph. The wasted calls were the oversize `get_mesh` and the refused `Bash`.

**Round 2: where it hesitated.** One tuning loop: the dirt first reached only the hidden end of the drive (occlusion reach 0.012), widened to 0.02 after the close renders. It repointed the material at the existing `worn-steel.texgraph` after the open warning, so it never saved a graph of its own name. No wrong turn otherwise.

**Changes made in answer.**
- Before round 1: none (the descriptions went in as the milestone left them).
- Between the rounds, a product fix, not a description change: B-10's `get_mesh` change (an `include` list so a 120-face model can be read under Claude Code's result limit). Round 1's oversize answer found it (the fault, 53,645 characters by section: vertices 8,694, faces 13,642, edges 11,427, uvs 16,129, seams 1,954, groups 899).
- Round 2's description change, in answer to the look on round 1 ("the wear reads like an edge highlight"): patina's `nodes` worked example for `mesh_curvature` (`NodeCatalogue.WorkedMesh` and its caption in `src/Patina.Server/NodeCatalogue.cs`; `MeshNodeToolTests` follows, 6 nodes to 9). Before: `edges` (mesh_curvature, width 0.02) -> `wear` (levels 0.6..0.9) -> mask of `worn` (blend mix iron #303236 to steel #c4c6c8), so an agent following it got a thin, even rim. After: `edges` -> `band` (levels 0.6..0.9); `grain` (fbm, cells 12x12) -> `chips` (levels 0.45..0.6); `wear` = blend multiply of `band` and `chips`, the mask of `worn`; the caption adds "wear that comes and goes along an edge reads as wear, an even band as an outline". Why: the person chose that the taught example breaks the mask up with noise. Declined for M-29: retuning `weathered-iron`, leaving the example. The stand-in's 0.5 still gives band 0, so mask 0 and the base colour. patina-unit and scenario-patina passed.

**Frames**: the person's looks used montages of the round's `front`, `three-quarter`, the wear mask on `uvs` and the drive closes; the other session frames and the `glb-*` frames beyond the review set were not opened.

## As built

Agent 6, Claude Sonnet 5.5 (`claude-sonnet-5-5`), carried out the crevice ruling, recorded the rounds and closed the member. Agents 1 to 5 were also Claude Sonnet 5.5. Both rounds' inner sessions ran `claude -p --model claude-opus-5-5` (reported `claude-opus-5-5`).

**Built.** `scripts/prepare-weathering-round.ps1` (+ `scripts/weathering-round/`: `worn-steel.texgraph`, `prompt.txt`, `reference-steel.texgraph`) makes a round folder (the wrench, the plant `wrench-start.carve` naming `graphs/steel-worn.texgraph`, `.mcp.json` for carve and patina published Release from this worktree into `%TEMP%\carve-m29-t168`, `run-session.cmd`). `tests/Carve.WeatheringAcceptance.Tests` is six checks on a round folder (reading `CARVE_WEATHERING_RUN`, skipped when unset, a `dotnet-closure` input of `server-unit`); the Acceptance scenario's `WeatheringStep.cs` compares `render` with `render_glb` front and three-quarter plus a front-vs-back control; `scripts/run-acceptance-check.ps1 -Weathering -Round <folder>` runs both; `scripts/run-weathering-negatives.ps1 -Plant <round>\wrench-start.carve` shows the checks can fail. Numbers go to `<round>\acceptance\numbers.tsv`.

**Run conditions as ruled:** fresh headless session per round, `--tools ""`, `--allowedTools "mcp__carve__*" "mcp__patina__*"`, servers from this build, no file tool, `--model claude-opus-5-5` pinned (ruling 1, 2026-11-24). Prompt in this file, fixed before round 1; a round changes nothing but the descriptions (and B-10's product fix) between it and the next.

**Rulings of the person.**
- 2026-11-24: model pinned; hard-edge margin 30, crevice margin 10 (asked with round 1's 93.8 and 22.3; declined half of round 1's differences and any positive difference); a refused tool call passes and is listed (declined: failing on any attempt); the grip held to the saved file's own wear, std of luma no more than the starter's plus 0.01 (replaced the steel slot's bake over the rubber texels). The first agent's sets (drive faces left out of the edge sets; crevice = the whole drive) were not put to the person.
- 2026-11-25, the crevice set (made with round 2's number in view, which should be weighed): the drive's texels within 0.004 m of the 8 edges of its two ends, all texels of that set; `open steel mean - band mean >= 10` luma. The distance 0.004 m was proposed after seeing that round 2 passes there (24.9; at 0.012 m round 2 reads -1.6 and would have failed). Declined: 0.012 m with margin 10 (a third round would follow) and 0.004 m with margin 20. The socket's texels are not in the set (faces 118 and 99 are group `dark` and do not wear the steel material); the open-steel set is unchanged. The rows for the other distances (0.012, 0.035; with and without the texels within 2 of a hard edge; with the socket) stay in `numbers.tsv` as notes that assert nothing.
- 2026-11-24 and 2026-11-25: both rounds' looks approved; round 1 with the note above, which led to round 2's description change. The other number ruled with a round's numbers in view is the 2026-11-24 margins (round 1's 93.8 and 22.3).

**Comparison** (table above): both rounds pass all six checks under the final rules. Crevice, old set (whole drive over 2 texels from a hard edge): round 1 22.268, round 2 2.144 (fail); ruled set: 49.143, 24.862.

**Negatives** (`run-weathering-negatives.ps1` on round 2's plant, after the switch; reference passes, each break caught by the test it should fail):

| variant | caught by |
|---|---|
| reference | passes (hard-edge 49.785, crevice 60.521, one refused Bash) |
| no-wear (stand-in graph) | hard-edge/crevice test (-0.567, -0.597) |
| no-dirt (occlusion reach 0) | hard-edge/crevice test, by the crevice check: -3.544 against 10 (hard-edge 42.814 still passes) |
| grip-wear | the grip test |
| unresolved (saved file names the planted path) | the saved-file test (and hard-edge/crevice) |
| file-tool (a succeeded Bash) | the no-file-tool test |
| next-define-missing | the missing-graph-message test |

**Not checked.** Round 1's bake times beyond the four `baked` entries found in its transcript (the journal was not read). The frames no one opened (the other session frames, `glb-*` beyond the review set, `render-front-control`), and no `uvs` picture of the crevice sets. No round's agent called `help`, so what `help` teaches is untested. Two rounds on one model and one prompt are the whole evidence; the crevice band's 0.004 m was chosen with round 2 in view. The scripts and round starter files are inputs of no suite (coverage warning expected).

**README.md and CLAUDE.md.** README.md: nothing needed (it does not quote the `mesh_curvature` example, and the acceptance scripts are contributor tools). CLAUDE.md: the checks project's row names the ruled crevice set and the `-Weathering` run; the negatives row said four broken copies and now says six.

**Evidence** under `G:\My Drive\artifacts\carve\M-29\T-168\round-N\` (`acceptance/` regenerated under the final rules for both rounds); the round folders on disk are `artifacts\T-168\round-N\` in this worktree (git-ignored).
