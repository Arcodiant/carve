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
