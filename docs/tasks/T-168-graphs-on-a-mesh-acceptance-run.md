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

**Verify:** server-unit

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

### Hand-off (agent 1, Claude Sonnet 5.5 `claude-sonnet-5-5`; checkpoint reached after round 1 was run and measured)

**Done** (commits ac91659 and 5216de2 on `task/T-168`): `scripts/prepare-weathering-round.ps1` (+ `scripts/weathering-round/`: `worn-steel.texgraph`, `prompt.txt`, `reference-steel.texgraph`); `tests/Carve.WeatheringAcceptance.Tests` (6 checks; margins are constants `HardEdgeMargin`, `CreviceMargin` = 0 until the person rules; numbers to `acceptance/numbers.tsv`); the Acceptance scenario's `WeatheringStep.cs` (render vs render_glb front and three-quarter plus a front-vs-back control; `run-acceptance-check.ps1 -Weathering -Round <folder>` runs it and the checks); `scripts/run-weathering-negatives.ps1`; CLAUDE.md Layout rows, `Carve.slnx`, `tests.json`; the prompt in this file. Not yet run: `cairn task verify`, `cairn lint`.

**Round command.** `scripts/prepare-weathering-round.ps1 -Round artifacts\T-168\round-N` (publishes both servers Release from this worktree into `%TEMP%\carve-m29-t168\{carve,patina}`; `-NoPublish` reuses them), then in that folder `cmd /c ".\run-session.cmd"` (note the `.\`): `claude -p --model claude-opus-5-5 --allowedTools "mcp__carve__*" "mcp__patina__*" --tools "" --mcp-config .mcp.json --strict-mcp-config --output-format stream-json --verbose < prompt.txt > transcript.jsonl`. Round 1 folder: `D:\Repos\carve\branches\task\T-168\artifacts\T-168\round-1\`, copied whole (with `frames/`, `summary.txt`) to `G:\My Drive\artifacts\carve\M-29\T-168\round-1\` (re-copy `acceptance/` after any re-run of the checks). `artifacts\T-168\summarise-transcript.py` (copy on G:) lists a transcript's calls. Checks: `scripts\run-acceptance-check.ps1 -Round <folder> -Weathering` (its output is noisy: filter it).

**Negatives** (`run-weathering-negatives.ps1 -Plant <round>\wrench-start.carve`, a hand-written reference graph, glb test left out): reference passes all; no-wear (stand-in graph) caught by the hard-edge/crevice test only through the sign of grain noise (-0.567 at margin 0; a ruled margin makes that firm); grip-wear caught by the grip test; no-dirt by the crevice test; unresolved (saved file names the planted path) by the saved-file, steel-bake and grip tests. Transcript tests have no negative yet (not checked failing). The plant file first lacked `parts` (version 10 needs them): fixed.

**Round 1** (started 18:39:48, ended 18:42:06, 2 m 18 s; model reported `claude-opus-5-5`; 31 calls; never called `help`; one `Bash` call attempted at call 5 (disabled, refused: it tried to read an oversized `get_mesh` result from a file) which makes the "no file tool" check fail as written, a decision for the person: attempted-and-refused vs used). Order: open (warning), get_mesh (too large, saved to file), patina open `worn-steel.texgraph` (found it from the warning's file list), get_graph, [Bash refused], `check` (the T-166 message with the folder's file list), patina `nodes` for mesh_occlusion and mesh_curvature, one `edit_graph` of 9 operations, save as `graphs/wrench-steel.texgraph` (its own new file), `define_material steel` (graph, size 1024), 4-view render, bake of masks for `uvs` looks, selects, `uvs` renders of the `wear` and `dirt` outputs, close renders of the drive, one more edit (grime colour) and save, render, check clean, save, export, render_glb. Hesitation: worked out the drive faces by selecting by `y`/`z` and highlights (calls 14 to 24), no wrong turn found. Did not mask wear to the steel group: it reasoned the grip is not on `steel` so is never drawn with the graph (its final note: grip, socket and caps render near-white, "nothing in your brief covered"). Bake times were not recorded in the transcript; carve's own journal under the round's carve server has them if wanted (not read).

**Checks on round 1** (4 of 6 pass): missing-graph message and next steel `define_material` pass; saved file v10, no warning, steel base colour a mesh-reading graph, passes; `.glb` loads with no warning, passes; Acceptance step: front 0 px differ, three-quarter 0 px, control front-vs-back 10451 px fails as it must (`acceptance/differences.tsv`). Fail: "no file tool" (the refused Bash, above); grip: `rubber std of luma 29.907 (the bake) - 3.649 (the clean stand-in) = 26.257` (the wear reaches the grip's texels in the bake though the grip never wears that material in a render; the spec's check reads the bake of the slot over the rubber faces).

**Numbers awaiting the person's margins** (1024 bake, luma 0 to 255): hard-edge: `mean luma of steel texels within 2 of a hard edge (16,830 texels) 187.828 - mean of steel texels more than 12 from any (162,126 texels) 94.050 = 93.778`. Crevice: `open steel mean 94.050 - drive crevice mean (drive faces 100 to 105 over 2 texels from a hard edge, 11,515 texels) 71.782 = 22.268` (the test prints it as crevice minus open, -22.268). Two choices of mine to confirm: both exclude drive faces 100 to 105 from the edge sets; the crevice is the whole drive (it stands between the ring and the socket top), not a band at its foot.

**Frames for a review agent** (all under the round folder, also on G:): `frames/scenario/frames/render-front.png`, `render-three-quarter.png` (and `glb-front.png`, `glb-three-quarter.png`); the agent's own frames, in call order, `frames/session/*/tool-results/mcp-carve-blob-*.png` (12 files by timestamp: 4-view render; masks uvs; wear uvs 1024 (call 16); dirt uvs (call 17); drive closes (calls 18, 24, 27); final render_glb): the close on the drive is the last pair of close renders (call 27, after the grime edit); the wear mask on `uvs` is call 16's image, the third or fourth file. Look at them by hash and crop; I opened none.

**Surprises:** the prompt's two requests were both met by the agent; the dirt it drew is by occlusion at the ruled 32 rays and 0.035. The missing-graph message reaches an agent by `check` and `open`'s warning ("no file at ...; the .texgraph files in ... are: worn-steel.texgraph"), not "is not there" (that is the exception text): the check now matches the former. No product fault seen. No description changed yet.

### Hand-off (agent 2, Claude Sonnet 5.5 `claude-sonnet-5-5`): STOPPED at part 2 on a product fault

Done: the rulings recorded and built into the checks (section above, commit 3ff79ce), round 1 re-measured (6 of 6 pass), negatives for every ruling and both transcript checks. Not done: round 1's look (part 3), description changes, round 2. Nothing has been sent to the person.

**The oversized `get_mesh` answer.** Round 1's call 5 was `get_mesh {}` (transcript line 7). The answer was 53,645 characters on one line; Claude Code refused it as over its maximum tool-result size and saved it to `...	ool-results\mcp-carve-get_mesh-...txt`, telling the agent to read that file, which a no-file-tool session cannot do; the agent then tried `Bash` (refused). By section (compact JSON characters): vertices 8,694; faces 13,642; edges 11,427; uvs 16,129; seams 1,954; groups 899; the rest under 500 each. `get_mesh`'s arguments are `mesh`, `target` (adds positions) and `level` (reads a kept level); none trims the answer, and its description says "The output is never trimmed". No description can steer an agent to a smaller answer: the wrench's mesh cannot be read by an agent with no file tool through `get_mesh`. By the brief this is a product fault, not fixed here: `get_mesh` has no way to ask for part of a mesh (for instance without edges or uvs, or a range of faces) so a model of 120 faces is unreadable under Claude Code's limit. The round-1 agent worked round it with `select` by position; so the round was not blocked, but every fresh round will meet the same answer if it calls `get_mesh`. The orchestrator files the bug; round 2 waits on it per the Scope ("a product fault ... fixed before the next round").

### Hand-off (agent 3, Claude Sonnet 5.5 `claude-sonnet-5-5`): round 1's look, 2026-11-24

Opened 4 images (a stray unrelated Read of a stale temp file counted; then 3 montages of the round's frames; no build, verify or run). Saw: front and three-quarter renders show the steel as grey-blue with a thin bright rim on every edge (head, ring, arm), fairly uniform, closer to an outline than to chipped wear; the grip, socket and caps are near-white (clean, as asked, but flat, not rubber-like); no red or black parts distinguishable; the drive (small dark square shaft) shows only in the close side renders and no dirt can be seen on it from the front or three-quarter; the `uvs` frame (1024) shows the layout with a bright outline on every island, which reads like the layout's own wireframe plus the mask, hard to tell apart.

**The person's ruling on the look (2026-11-24): approved.** Note, word for word: "Generally fine but the wear reads like an edge highlight - how do we expect wear to be implemented?" That is a question to the orchestrator and the next agent, not acted on: the person asks what wear should be (curvature-masked edge brightening with noise breakup, chips, or something else) and the description or margin may need to say so. No code, check or description changed. Not checked: the other 8 session frames, the `glb-*` frames (same hashes as the renders).
