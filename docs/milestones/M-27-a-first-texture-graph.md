---
description: >-
  An agent builds a small node graph in a new texture server, sees it as swatches, exports PNGs and textures the barrel with them
---
# M-27 — A first texture graph

**Status:** Done

**Depends on:** M-20

**One-liner:** *An agent builds a small node graph in a new texture server, sees it as swatches, exports PNGs and textures the barrel with them*

## What this milestone delivers

The thin slice of [D-7](../design/D-7-texture-graphs-and-layered-surfaces.md), end to end: an agent defines a texture graph on a new MCP server, looks at it, exports its outputs as PNGs, and names them in a Carve material with M-7's tools.

- The shared, engine-free texture-graph package: the graph format, deterministic evaluation, and errors that name the node and port at fault.
- Six to eight first nodes, enough for one material: a noise, a gradient, levels, blend, colour, height to normal, and outputs for base colour, normal and roughness.
- A new MCP server with tools to define, edit, read back, save and open a graph.
- Swatch renders: flat, tiled 3 x 3 to show seams, and on a sphere through `Material.Pbr` with Carve's fixed sky.
- PNG export of each output at a given resolution.

Content: a planks graph, committed with its PNGs, that gives M-7's barrel its staves in place of T-77's generated ones.

## Estimated size

About **6 tasks**:

1. Graph format and evaluator in the shared package, with determinism tests.
2. The first nodes.
3. The server project and its graph tools (define, edit, get, save, open).
4. Swatch renders and PNG export.
5. The planks graph, the barrel textured from it, and a scenario.
6. Acceptance: fresh agents build a material from the tool descriptions alone.

## Why

Raised in a discussion with the project owner on 2026-11-17; see D-7's *Intent*. In short: M-7 can only take supplied PNGs, and T-77's barrel textures needed a purpose-written generator.

## Open at planning

- The server's name, the graph file's format and extension (D-7, *Open questions*).
- Which six to eight nodes come first.
- Whether the server keeps an undo history like Carve's.

**Decided at planning (2026-11-17).**

By the person:

- **The server is `patina`** (exe `patina`, project `src/Patina.Server`).
- **A graph is versioned JSON, `.texgraph`**, written and read back bit-exact. Nodes are keyed by an id the agent
  chooses, an input names `"<id>"` or `"<id>.<port>"`, and outputs are `output` nodes with a role.
- **Seven first node types:** `noise`, `gradient`, `levels`, `blend`, `colour`, `height_to_normal` and `output`
  (roles `base_colour`, `normal`, `roughness` and `occlusion`). The parameters are in T-112.
- **Size:** the graph states a default `size`, and every parameter is relative to the image. `export` takes any power
  of two from 32 to 8192, evaluated in bounded memory. `swatch` stays at 2048 or below.
- **No undo history: `push_snapshot` and `pop_snapshot`** instead (the person's idea). A session-only stack, cleared
  by `open` and `define_graph`. Edits are all or nothing.
- **A shared host library**, `Carve.Hosting`, extracted from `Carve.Server` (T-111). Both servers host through it,
  and patina loads the same extensions, so `Carve.Debug` and the scenario harness serve it unchanged.
- **M-27 waits for M-20.** The extraction touches `Program.cs` and `EngineStatus.cs`, which M-20's T-87 has changed
  on its branch.
- **The barrel sits beside M-7's:** `samples/barrel-planks.carve` in a new Planks scenario. M-7's barrel, generator,
  checks and frames are unchanged.
- **The swatch sheet** below.
- **The acceptance plant:** a starter graph in which a `blend`'s `mask` names `grain_2` where the node is `grain2`,
  given to an agent with no file tools.

By the orchestrator, recorded in the tasks' Notes, each a stop if it proves wrong:

- the package is named `Patina.Graphs`;
- roughness is exported packed for glTF's metallic-roughness slot (R 255, G roughness, B metallic 0);
- `export` writes `<role>.png` into the folder given;
- a graph with reference faults opens and is reported, so it can be fixed through the tools, and `save` refuses it.

Checked at planning:

- The engine has a built-in UV sphere (`PrimitiveMesh.Sphere`, radius 0.5, Y-up), and its normal mapping builds the
  tangent frame from screen derivatives, so the sphere swatch needs no mesh code (Empyrean 0.119.0 docs).
- `StbImageWriteSharp` 1.16.7 writes identical PNG bytes twice over at 2048 and at 8192. At 8192 it took about 10 s
  and peaked at about 750 MB for one image, giving 41 MB on disk (a throwaway probe in the temp folder).
- T-77's staves are 32 texels wide at 256, with boundaries at u = k/8 (`TextureSet.StaveWidth`). The planks graph's
  `gradient(u, repeat 8)` falls on them, and T-112 makes its gaps at least 3 texels each side, since M-7's one-texel
  gaps could not be sampled.

Not checked: whether M-7's lit prediction (`MaterialsChecks`) holds with a roughness map. T-116's research pass
checks it before any frame.

**The swatch sheet**, as agreed:

```
 planks.texgraph   256 px   seed 7
+------------+------------+------------+------------+-------------------------+
| base_colour| normal     | roughness  | occlusion  |                         |
|            |            |            |            |        .-'''''-.        |
|   flat     |   flat     |   flat     |   flat     |      .'         '.      |
|            |            |            |            |     /   sphere    \     |
+------------+------------+------------+------------+    |  Pbr, fixed   |    |
|+--+--+--+  |+--+--+--+  |+--+--+--+  |+--+--+--+  |     \   sky      /     |
||  |  |  |  ||  |  |  |  ||  |  |  |  ||  |  |  |  |      '.         .'      |
|+--+--+--+  | tiled 3x3  | tiled 3x3  | tiled 3x3  |        '-.....-'        |
+------------+------------+------------+------------+-------------------------+
  cells 256 x 256; sphere cell 512 x 512; a 24 px title strip on top
  columns are the graph's outputs, in role order; a missing role has no column
```

`swatch view=flat|tiled|sphere` gives one view alone: flat at `size`; tiled at 3 x `size`, at most 1536 px; the
sphere at 512 px, turned by `angle` about y.

**Order.**

| Wave | Tasks | Why |
|---|---|---|
| 1 | T-110 beside T-111 | The package and the host library share no file but `Carve.slnx` and CLAUDE.md (conflicts there only add) |
| 2 | T-112 beside T-113 | The nodes and planks need T-110; the server needs T-110 and T-111 |
| 3 | T-114, T-115 and T-116 side by side | Swatch and export need the server and the planks; the barrel needs only the planks' PNGs |
| 4 | T-117 | Acceptance, once everything is reachable |

Shared files with one owner: `run-scenario.ps1`, `src/Patina.Server/Program.cs` and the README's patina section
belong to T-113 (T-114 and T-115 add one line or section each); `samples/graphs/` belongs to T-112; T-110 owns the
node registry (T-112 adds a line per node). `tests.json`, `Carve.slnx` and CLAUDE.md's Layout take additions from
every task, so keep both sides.

Frames: T-114 and T-116 draw new frames. Each plans its frame review as its own agent, and the person approves each
set before it lands. There is no sound or motion, so there is no real-time pass.

## Required tasks

- [ ] [T-110](../tasks/T-110-texture-graph-format-and-evaluator.md) — Patina.Graphs: the engine-free .texgraph format read back bit-exact, deterministic banded evaluation, and faults that name the node and port
- [ ] [T-111](../tasks/T-111-shared-engine-host-library.md) — Carve.Hosting: the engine thread, extension loading, host set-up, fixed sky and texture upload rules move out of Carve.Server, every frame unchanged
- [ ] [T-112](../tasks/T-112-the-first-texture-nodes-and-the-planks-graph.md) — noise, gradient, levels, blend and height_to_normal, and samples/graphs/planks.texgraph with its PNGs kept equal to the graph
- [ ] [T-113](../tasks/T-113-the-patina-server-and-its-graph-tools.md) — patina, a second MCP server: define_graph, edit_graph, get_graph, save, open, push_snapshot and pop_snapshot
- [ ] [T-114](../tasks/T-114-swatch-renders.md) — patina's swatch: the sheet, flat, tiled 3 x 3 and a sphere through Material.Pbr under Carve's fixed sky
- [ ] [T-115](../tasks/T-115-png-export-at-a-given-size.md) — patina's export writes each output as a PNG at any power of two to 8192, in bounded memory
- [ ] [T-116](../tasks/T-116-the-barrel-with-planks-staves.md) — samples/barrel-planks.carve: M-7's barrel with its staves from the planks graph, in a scenario of its own
- [ ] [T-117](../tasks/T-117-texture-graph-acceptance-run.md) — Fresh agents build a wood material in patina from the tool descriptions alone, recover from a broken starter graph and put it on the barrel
- [ ] [T-128](../tasks/T-128-swatch-sphere-lit-as-carve-renders.md) — patina's sphere swatch lit by carve's ambient and directional lights as well as the fixed sky (added 2026-11-18 by the person's ruling after T-117)

## Retrospective

**As planned.**

- **The slice runs end to end.** An agent defines a graph on `patina`, sees it with `swatch`, exports PNGs with
  `export`, and textures the barrel in carve with them. T-117's fresh agent did all of it from the tool
  descriptions alone, in both rounds. It found the planted `grain_2` from `open`'s fault and fixed it with
  `edit_graph`, reading the fault's node list rather than guessing.
- **The members, in the planned waves:**
  - **T-110**, `Patina.Graphs`: the `.texgraph` format read back to the same bytes, evaluation in bands of 2^18
    texels, and faults that name the node and the port.
  - **T-111**, `Carve.Hosting`: the host set-up, engine thread, sky and texture upload rules moved out of
    `Carve.Server`. Every carve frame and `tools/list` stayed byte-identical.
  - **T-112**, the five nodes and the planks graph with its PNGs, which the person approved.
  - **T-113**, the patina server and its seven graph tools, every worked example tested.
  - **T-114**, swatch renders: the sheet, flat, tiled and the sphere.
  - **T-115**, PNG export up to 8192 in under 2 GB.
  - **T-116**, `samples/barrel-planks.carve` in a scenario of its own, with M-7's lit prediction holding under a
    roughness map. Its frames were approved on round 1.
  - **T-117**, the acceptance run.
- **The planning-time calls held:**
  - the package name `Patina.Graphs`;
  - roughness packed for glTF (R 255, G roughness, B 0);
  - `<role>.png`;
  - a graph with reference faults opens and `save` refuses it.
- **Frames:** each set was opened by a review agent before the person saw it:
  - T-112's four PNGs, approved;
  - T-114's eight frames and its thresholds, approved, with a note that roughness barely showed;
  - T-116's five frames, approved;
  - T-128's sphere frames and its thresholds, approved.
- **`cairn milestone M-27 verify`** (2026-11-18): every check passes but `review`, which comes after the person's
  go:
  - 9/9 members done and merged;
  - 21 suites passed, 13 ran and 8 carried, including `server-regression` and every scenario.

**Not as planned.**

- **A ninth member, T-128.** Patina's sphere was lit by the fixed sky alone, and the sky's probe has no sun disc, so
  roughness never showed. The person noted it on T-114's frames and rejected both of T-117's rounds on the look.
  - An Opus research agent found no defect: the map reaches `Material.Pbr`'s metallic-roughness slot correctly in
    both servers. The cause was the swatch having no lights where carve's render has ambient 0.25 and directional
    0.9, together with the planks' narrow roughness (0.57 to 0.78).
  - The person ruled the fix be made in M-27. T-128 moved carve's lights into `Carve.Hosting` and lit the sphere
    with them. Every carve frame stayed byte-identical. The approved thresholds moved: dark 52 to 66, in band 58..110
    to 80..128. A new check requires the roughness-0 sphere to peak at least 40 above the roughness-1 sphere; it
    measured 255 against 128.
  - The planks graph stays as it is (the person).
- **T-114 put its break switch in the product** (`PATINA_SWATCH_BREAK`). The person ruled it out, per CLAUDE.md's
  no-debug-in-product rule. A third T-114 agent moved the four breaks to the scenario, and all are still caught. Two
  are now close copies rather than exact ones:
  - `u-flip` mirrors the prediction instead of the render;
  - `tiled-shift` shifts the sheet's cells by 43 px for a half-tile of 42.67.
- **T-114 decompiled `Empyrean.Geometry`** to learn the sphere's UVs, against CLAUDE.md's docs-not-source rule. The
  person ruled that no engine need is recorded and that `SwatchSphere.cs` stands.
- **Export at 8192 is slow.** All four outputs took 154.7 s (Debug), against planning's "tens of seconds", and most
  of it is evaluation. `export`'s description says minutes. Filed as T-130.
- **T-117's run conditions needed `--allowedTools "mcp__carve__*" "mcp__patina__*"`.** The person approved it.
  The default model with no `--model` flag is Opus 5.5 on this machine, not the Sonnet T-117's Notes expected.
- **T-117's first roughness figure was wrong.** It reported 0.45 mean and 6 max for roughness 0 against 1. The
  research measured 2.35 mean and 104 max on the same test, and 22.6 mean on one stave. It is corrected in T-117.
- **Hand-offs:** T-111, T-113, T-114, T-116 and T-128 each handed off at least once.
  - T-113's first agent had to wait on T-112's nodes. Its examples' fault text was guessed from T-110's As built
    and proved right.
  - T-111's first agent left a stray local git ref, `refs/t111/before`. It is still in the repo, for the person to
    delete.
- **`cairn test artifacts <suite>` returns the newest job of that suite from any branch.** T-111 was handed T-71's
  frames by it. Every later brief said to fetch by job id. This is cairn's behaviour, not Carve's.
- **Judgement calls left standing, for the review to see:**
  - T-110: the "no output" fault has no node; a bad `size` or `seed` is a load error; colours are held sRGB.
  - T-112: the gap is `levels(in 0.15..0.25, out 0.16..1)`; `levels` output goes up to 2; `blend` is unclamped.
  - T-113: `edit_graph` judges a fault new by its place, not its message; `define_graph` clears the path; `open`
    adds `.texgraph`.
  - T-115 loosened T-113's tool-list checks to "the first seven, then `export`". T-114's rebase made them exact again
    (seven, `export`, `swatch`).
- **Worktree folders** `branches/task/T-110` to `T-117` and `T-128` could not be removed by `finish` (each was an
  agent's current folder). They are unregistered from git and can be deleted by hand. T-111's holds the frame-guard
  evidence.

**Never verified.**

- No suite has run on a machine without a GPU. Whether `patina-regression` really needs none was tested only by
  hiding the Vulkan driver (T-113).
- The roughness-0 highlight clips at 255, so its size and shape are not checked (T-128).
- T-116's viewer step and the reopened frames were not looked at by eye; the reopened frames are byte-identical by
  hash.
- T-115's three named breaks were not mutated to watch them fail. T-116's and T-114's breaks were.
- Carve's frames in T-111 and T-128 were held by hash only and never opened. The before and after sets in T-111 came
  from different runners (Xellotath and Mantorok); T-128's were both from Mantorok.
- The exported `.glb` of T-117's barrel was not read back.
- **Model reports:** T-117's finishing agent was dispatched on Sonnet and reported Opus 5.5. Which is right is not
  known.

**Known and left.**

- **T-129**: the 8192 export test moves out of `patina-regression` into a heavy-tier suite.
- **T-130**: faster evaluation at 8192.
- `server-regression`'s two timing-dependent tests (T-111's As built) make every frame guard leave them out.
- The README, CLAUDE.md and `tools/Patina.SampleGraphs` are inputs of no suite (verify warns).

**Models.**

| Member | Model | Agents |
|---|---|---|
| T-110 | Opus 5.5 | 1 |
| T-111 | Opus 5.5 | 2 (hand-off) |
| T-112 | Sonnet 5.5 | 1 |
| T-113 | Opus 5.5 | 2 (hand-off while T-112 landed) |
| T-114 | Opus 5.5 | 3 (build, frame review, break move and finish) |
| T-115 | Sonnet 5.5 | 1 |
| T-116 | Sonnet 5.5 | 2 (build, frame review) |
| T-117 | Sonnet 5.5, then reported Opus 5.5 | 2 (both rounds, then the record and finish) |
| Roughness research | Opus 5.5 | 1 |
| T-128 | Opus 5.5 | 2 (build, frame review) |

No rework was traced to the Sonnet tier. The extra work came from rulings: the break switch, the sphere's light.

**Test report** (`cairn test report --since 1d`):

- `server-regression` is still most of the slot time, as after M-20.
- `patina-regression` comes fifth, at about 7,900 slot-seconds a week, almost all of it the 8192 test (T-129).
- `scenario-orcward` shows 3 fails, from outside M-27's work, as in M-20's report.
- There were no flakes.

**For the next plan.**

- **Plan a swatch, or any look, against how carve lights the same thing.** "Through `Material.Pbr` with Carve's fixed
  sky" was read literally, and carve's own lights were missed. A planning probe of roughness 0 against 1 would have
  shown it before T-114.
- **Put the scenario-or-product rule in every brief that makes breaks.** M-7 kept its switch in the scenario; T-114
  did not know to.
- **Measure an 8192 claim on the real graph at planning,** not only the PNG encoder.
- **Acceptance run conditions:** carry `--allowedTools` for both servers, and expect the default model to be Opus.
- **A member that depends on a sibling's API** (T-113 on T-112's nodes) can start early but should expect to hand off
  until the sibling lands.

**After the person's go (2026-11-18 to 2026-11-19).**

- **Rebase onto `main` (Sonnet 5.5):** clean, 45 commits, no fixes.
- **Refactor pass (Sonnet 5.5):** three cuts:
  - the dead `ViewFraming.VerticalCosine`;
  - the dead `PbrMaterials.Sampler` alias;
  - one lighting constant written twice, now shared.

  Left, as design changes: the near-duplicate tool plumbing and test harnesses of the two servers.
- **Review round 1, RV-68:** five Opus 5.5 reviewers, split by area (`Carve.Hosting` and `Carve.Server`;
  `Patina.Graphs`; `Patina.Server`; the scenarios and checks; the whole diff). 26 findings: 3 Major, 15 Minor, 8 Nit.
  The Majors:
  - reading or validating a graph threw on an input from a node of unknown type;
  - `export` held the graph lock for minutes and ignored cancellation;
  - the 1024 export check passed a shifted, a mirrored and a flat picture.
- **`main` moved mid-review.** M-21 landed. A second rebase (Opus 5.5) had one Layout-row conflict. M-21's render
  changes were in carve's own files, so nothing had to move into `Carve.Hosting`. Frames of five suites, `scenario-morph`
  among them, were byte-identical with `main`'s.
- **The person's rulings during the answers:**
  - **Scenario Patina drops its reference to `Patina.Graphs`.** It takes reference texels through patina's tools and the
    approved planks PNGs, keeping CLAUDE.md's rule. The person asked first why the rule exists; the answer is in the
    rule's own text (scenarios are the API's first client and reach the product only through it), and in practice every
    carve scenario works its predictions out independently.
  - **The validator kind-checks every input whose source resolves,** even when another input of that node is at fault
    (from RV-68's `edit_graph` finding; extended to a node on a cycle in round 2).
  - **The 1024 export rule:** each 256 texel, per channel, within 4 levels of the mean of the four 1024 texels at its
    centre. For the normal, after an Opus research agent found its 30-level difference is sampling at the planks' kinks
    and not a defect: 4 where the local step is 4 or less, 32 elsewhere.
- **Answers:** five Opus 5.5 agents answered RV-68, the first three handing off at the call limit. Export cancellation
  works on `notifications/cancelled`, which the SDK's client (2.3.0) never sends when its token is cancelled; the test
  sends it by hand.
- **Review round 2, RV-69:** two Opus 5.5 reviewers checked the 26 answers against the code and reviewed the second
  rebase. 23 held. 4 findings (3 Minor, 1 Nit): the cycle case, pre-rebase commit hashes in the As built, roughness
  unchecked at 1024 in the scenario, and a stale comment. One Opus 5.5 agent fixed all four.
- **Review round 3, RV-70:** one Opus 5.5 reviewer. All four answers held. One Nit in the docs (a one-stave shift
  claimed caught that no check makes), corrected by the orchestrator.
- **`cairn milestone M-27 verify` (2026-11-19, at 4c68375): VERIFY PASS.** The review is current at RV-70. All 22
  suites pass, 3 ran and 19 carried.
- **Left:**
  - scratch probes in `D:\repos\carve\artifacts\RV-68b\`, written at the container root by a fix agent;
  - `refs/t111/before`;
  - the unregistered member worktree folders.
- **Next time:**
  - **Check `main` just before reserving the review.** M-21 landed while round 1 ran, which made it stale and cost a
    second rebase.
  - **Round 1's split by area worked.** Most findings were in the areas the briefs pointed at.
  - **Checking answers against the code paid off again.** Round 2 found three of 26 answers incomplete.
