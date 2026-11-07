---
description: >-
  An agent saves its work to a Carve file and exports a .glb that loads through Empyrean
---
# M-3 — First model in a game

**Status:** Done

**One-liner:** *An agent saves its work to a Carve file and exports a .glb that loads through Empyrean*

## What this milestone delivers

The end-to-end slice is complete: an agent saves its work to a Carve file, opens it again in a later session, and exports a `.glb` that loads through Empyrean's `IModelLoader` and looks right.

Content: the crate exported and shown through the engine, and a worked example taking a model from an empty file to a game's content folder.

## Open at planning

- The Carve file: text or binary, geometry or a list of operations (D-1).
- How normals are made on export, and what controls hard and smooth edges.
- Scale, axes and naming conventions a game expects, and whether Carve checks them.
- `SharpGLTF` or a writer of our own.
- How a scenario proves the export loads, without Carve taking on a game's job.

**Decided at planning (2026-11-04).** Each decision is the person's. All but the load proof were on a recommendation. Before the questions there was a probe on the planning machine (engine 0.117.0), kept in `artifacts/m3-probe/` and not committed. The probe found three things:

- SharpGLTF.Toolkit 1.0.7 writes a cube `.glb` that is byte-identical across writes, and Empyrean's `IModelGeometryLoader.Parse` reads it. It keeps the authored normals (24 vertices, 6 normals) and flips the winding to clockwise.
- With no `NORMAL` attribute, the engine averages the normals over shared vertices, so the export must write them.
- glTF's default material is metallic 1, roughness 1, and SharpGLTF writes that unless told otherwise.

The decisions:

- **The Carve file** (`.carve`) is JSON text holding the geometry, not operations:
  - `{"carve": 1, "vertices": [[x,y,z]…], "faces": [[ids]…]}`;
  - floats are written so they read back bit-exact, and ids are kept;
  - read through `PolygonMesh.Create`;
  - no undo history;
  - an unknown key or version is refused until M-6 and M-7 add theirs.
- **Normals**: flat, the face's Newell normal, with every edge hard. This matches what `render` draws. Smoothing control is left to a later milestone.
- **Writer**: SharpGLTF.Toolkit, pinned in `Directory.Build.props`, used by `Carve.Server` only. Render and export share one flat triangle list (`FlatShading` in the Core), so they cannot disagree.
- **Conventions**: pass-through, with no checks:
  - 1 unit = 1 metre, +Y up, +Z front, nothing converted, the origin as authored;
  - the node and the mesh are named after the file;
  - `export` reports the size and the bounds.
- **Material**: one material named `carve`, base colour `(0.72, 0.72, 0.70, 1)` (Carve's render surface), metallic 0, roughness 1.
- **Paths**:
  - a relative path resolves against carve's working directory, and every result gives the absolute path;
  - folders are created;
  - an existing file is replaced, and the result says `replaced`;
  - `.carve` and `.glb` are checked;
  - a missing file's error lists the `.carve` files in that folder;
  - carve keeps no current file.
- **Load proof**: the person chose a **model-viewer spike**, `spikes/Carve.ModelViewer`, over a product feature. It is a small headless Empyrean app that stands in for a game:
  - it mounts a content folder and loads `models/<name>.glb` by `ContentPath` through `IModelLoader`, with the file's own material;
  - it writes PNGs and `view.json`;
  - it is not shipped, referenced by nothing in `src/`, and is not a second scenario harness.

  The `scenario-export` suite runs the export scenario, then the viewer on its output.
- **Worked example**: in the `save`, `open` and `export` descriptions, plus a README section. The game folder is a scratch `game/Content/` in the run folder; no real game repo is written to.
- **Acceptance plant**: session 2 is told to open `crates.carve`. Only `crate.carve` exists, and `open`'s error lists it.

**The viewer frames**, agreed as a sketch. The views are in coordinates. Each is 512x512, auto-framed on the model's bounds, +Y up, with the file's matte grey material, a fixed sun plus ambient that does not turn with the camera, and a dark background.

```text
front: eye on (1.6, 1.2, 2.2) from centre       minus-x: eye on (-2.2, 1.2, 1.6) from centre
+Z side on the left, +X side on the right       -X side on the left, +Z side on the right
+----------------------------+                  +----------------------------+
|       _.-''''''''-._       |                  |       _.-''''''''-._       |
|   _.-' .----------. '-._   |                  |   _.-' .----------. '-._   |
|  |'-._ '-.______.-' _.-'|  |                  |  |'-._ '-.______.-' _.-'|  |
|  | .-.'-._      _.-'.-. |  |                  |=|| .-.'-._      _.-'.-. |  |
|  | |  |   '-..-'   | .|=|  |                  |=|| |  |   '-..-'   |  | |  |
|  | |+Z|    |    +X |.|=| <- +X bar     -X bar ->|| |-X|    |    +Z  |  | |  |
|  | '--'    |       '-'=|  |                  |=|| '--'    |       '--' |  |
|   '-._     |     _.-'    |                  |   '-._     |     _.-'     |
|       '-._ | _.-'        |                  |       '-._ | _.-'         |
+----------------------------+                  +----------------------------+
viewer/front.png                                viewer/minus-x.png
view.json, not drawn: models/crate.glb, triangle count, bounds, material 'carve' (base, metallic, roughness)
```

T-18's fixture cube is seen from `front` alone, with its top, +Z and +X faces in three distinct shades.

**Order.**

1. **Wave 1:** T-15 (Carve file in the Core) and T-16 (export, `FlatShading`, `ToolPaths`, the SharpGLTF pin) side by side.
2. **Wave 2:** T-17 (save and open, on T-15 and T-16) and T-18 (the viewer, on T-16 for the pin) side by side. T-18 owns `Carve.slnx`, `tests.json` and `CLAUDE.md` in this wave; T-17 owns the README.
3. **Wave 3:** T-19 (the export scenario and its frames, on T-17 and T-18).
4. **Wave 4:** T-20 (the acceptance run).

T-19 and T-20 are runs, split from the code on purpose. The crate exported and shown is their content, and T-16's tests export the crate as its placeholder until then.

**From the M-1 and M-2 retrospectives, carried into every task:**

- a named list of open rules, with a stop;
- a positive pixel check on every frame, and one that a mirrored or flipped camera would fail, since the crate is symmetric;
- each new check shown failing, given as a table;
- what a fixer may not change;
- members run `finish` inside their worktree and leave the folder;
- a planted mistake that no description can foresee;
- a final `get_mesh` asked for in the prompt.

Nothing in M-3 has sound or motion, so there is no listening or real-time pass.

## Required tasks

- [ ] [T-15](../tasks/T-15-carve-file-in-the-kernel.md) — Carve.Core reads and writes the Carve file, JSON geometry that reloads bit-exact with every id kept
- [ ] [T-16](../tasks/T-16-export-to-glb.md) — An agent exports the current mesh with export to a .glb with flat normals and one matte material, which Empyrean's glTF parser reads back exactly
- [ ] [T-17](../tasks/T-17-save-and-open-tools.md) — An agent saves its mesh to a .carve file and opens it again in a later session, with a worked example from an empty file to a game's content folder
- [ ] [T-18](../tasks/T-18-model-viewer-spike.md) — A headless viewer spike loads a .glb from a content folder through IModelLoader, as a game does, and captures it from fixed views
- [ ] [T-19](../tasks/T-19-export-scenario.md) — The export scenario saves, reopens and exports the crate into a game content folder, and the viewer shows it in its own suite
- [ ] [T-20](../tasks/T-20-save-and-export-acceptance-run.md) — Fresh agents build and save a crate, reopen it in a later session and export it to a game's content folder, recovering from a wrong file name

## Retrospective

**As planned.**

- The order held: T-15 and T-16, then T-17 and T-18, then T-19, then T-20. No rebase conflict needed the person, and no member failed.
- The slice works end to end. In both T-20 rounds, fresh `claude -p` agents on plain `carve` built a closed crate, saved it, reopened it in a second session with the mesh unchanged, and exported a `.glb` that the viewer loaded through `IModelLoader`.
- T-16's refactor was invisible: all 22 existing frames hash the same before and after `FlatShading`.
- T-19's two runs gave identical hashes for every frame, `view.json`, `crate.glb` and `crate.carve`.
- The person approved T-18's cube frame, T-19's frames and T-20's round 2 frames, all with no notes.
- T-20 changed no description, README or code.
- **Models:**
  - Opus 5.5 on T-15, T-16 and T-18, which set a format, an API and a new engine app;
  - Sonnet 5.5 on T-17, T-19 and T-20.

  No rework was traced to the cheaper tier.

**Not as planned.**

- **T-16 stalled for hours on the GPU, not on its code.** The runner refused the job (`this credential has no access to queue any-gpu`), and the local slot was held by Empyrean's `verify M-151`. It took two continuations and a credential change by the person, which needed a second attempt. The first agent sat in a silent wait for about 47 minutes before the orchestrator looked.
- **T-16's regression tests first ran at the very end**, because of that stall. Two checks failed; both tests were wrong and the product was right.
- **Waystone closed during T-18's review.** The request ended unanswered. The agent was resumed and asked again, and the first request was not counted.
- **T-19 replaced a spec check and landed it without asking.** "A +Z panel pixel differs from the +Z frame" could never pass, since both are +Z faces and share a shade (191). It now checks the recess wall (170) and that the panel is flat. The person kept it afterwards. It was on no stop list, but a check's target going back to the person is `CLAUDE.md`'s rule.
- **T-20 changed a run condition without asking.** Round 1's agent listed the folder with its own Glob and never met the planted `crates.carve`. Round 2 removed the agents' file tools (`--tools ""`) so that the plant fired. The prompts were unchanged, so this was not on its stop list. The person ruled that round 2, run with `--tools ""`, is accepted as the acceptance run.
- **T-20 tried `claude -p` with `--permission-mode bypassPermissions`.** The auto-mode classifier denied it, and T-20 reran with `--allowedTools "mcp__carve__*"`. The brief asked for neither flag. The person ruled that the relaunch is accepted as run.
- **A refactor pass came after this retrospective was first written (`d014eae`).** It moved the run scripts' shared process bounding into `scripts/bounded-run.ps1`, the crate's construction into `TestMeshes.Crate`, and the save and open tests' helpers into `FileToolTests.cs`, and removed `GlbExport.Write(PolygonMesh, ...)`. Its commit message said "no behaviour changes", backed only by a `tools/list` comparison, and it updated only some members' As built notes. RV-14 found those notes stale and fixed them, along with a gap in `run-export-scenario.ps1` that the pass did not make (the unbuilt test step came in with T-19, `eb9020d`; `d014eae` changed one line of the script).
- **Review rounds.** Round 1 (RV-14) raised 32 findings (2 Major reports of one bug, 16 Minor, 14 Nit); round 2 (RV-15) raised 7, one a repeat, and the person chose a third round. Round 3 (RV-16) raised 3: the same `open` and `undo` fault, now in `undo`'s description, and 2 Nits. The person had the wording fixed and the viewer guard Nit skipped. Round 4 (RV-17) raised 1 Nit, skipped by the orchestrator: `open`'s "once 100 changes have followed an open" miscounts changes that were undone. The `open` and `undo` wording took three rounds because each fix changed only the sentence named, and not every text saying the same thing.
- **Readings members landed that the spec did not settle:**
  - T-15: repeated keys refused; a file of another version gets only the version error; whole-file errors are named `file`; bad coordinates are named `vertices[i][k]`. The person kept all four. Two more came with RV-14's fixes: a key holding a lone surrogate is named `keys[n]` (its zero-based position, since its text cannot be named), and a face index of `1.0` is refused in a file though `define_mesh` accepts it (`Write` never emits it, so only a hand-edited file meets it).
  - T-17: `ToolPaths.Resolve` gained an optional `verb`, so `open` says "reads". The default wording is unchanged.
  - T-18: a flatness check at 2/255, a `minus-x` cube run, mirrored and flipped camera breaks, and a refusal of views along Y.
- **The viewer's background is pure black**, where Carve's own is dark slate. It meets the agreed "dark", and the person approved it side by side.
- **`view.json` has no material name**, though the milestone's sketch shows one: the engine's `Material` has none to read.
- **The planning probe's normals finding went further than it said.** With no `NORMAL`, the engine welds vertices and smooths, but a face's middle still matches the flat shade. Sampling only middles let dropped normals pass, until T-18 added samples near the corners.

**Never verified.**

- **A real game.** The `.glb` has been loaded only by the viewer spike, which stands in for one.
- **Windows and devices:** the viewer's window switch and its no-Vulkan failure path. Any GPU or machine but Xellotath's, and any golden or cross-machine frame comparison.
- **Carried passes on T-18:** `viewer-cube` and `core-unit` were carried from before its rebase, so its approved frame was not recaptured on the rebased commit. `viewer-cube` has since run fresh in T-19, and T-19's reviewed frames are later than that rebase.
- **Breaks:**
  - most rows of T-18's table ran before its flatness check existed;
  - T-15 has no break for parsing a number through `double`, which only the code's shape guards against;
  - T-17 could not build an "open is not a step at all" break without changing `ModelStore`, so a two-step break stands in.
- **T-16's frame baseline** was taken at a commit that is not an ancestor of its branch. Its source differed only in `CarveFile.cs`, which rendering does not use.
- **`undo`** was again used by no acceptance agent. `README.md` is an input of no suite, so its worked example is not tested from there.
- **"Twice as far"** was read differently by the two T-20 rounds (bars moved 0.11 and 0.13). It was recorded, not judged.
- The orchestrator opened no frame. Every look above is a member's or the person's.

**Known and left.**

- **Runner Mantorok** has no Empyrean 0.117.0 on its feed, so jobs sent there fail at restore with NU1603. It is the 14 jobs in the test report's `cnr` column. The pin was not moved. The person's ruling: Mantorok is left as it is for now; Carve jobs that land there fail at restore and are retried onto Xellotath.
- `CLAUDE.md` and `README.md` are inputs of no suite (a `verify` warning).
- Empty folders `branches/task/T-15` to `T-20` remain on disk, unregistered from git, as with M-1 and M-2.
- Three `carve-viewer-*` entries in `%TEMP%`, `%TEMP%\carve-m3-acceptance\` and the members' scratch logs are not cleared.
- Evidence is in the milestone worktree's ignored `artifacts/review/` and `artifacts/m3-acceptance/`. T-16's frames were not copied there; only their hashes are in its notes.

**The test report** (`cairn test report --since 3d`) shows no flakes. `server-regression` is the costly suite, at about 3,100 slot-seconds a week, and it caught something: its 2 failures are T-16's two wrong tests. The 14 `cnr` jobs are Mantorok, which is a machine fix rather than a suite fix. Nothing was filed; Mantorok is left as it is, by the person's ruling.

**For the next plan.**

- **Check the runner before the first dispatch:** run one GPU suite through the queue, and check every runner's feed has the pinned engine. Both failures here cost far more than that check.
- **Tell members from the start** to report a `verify` that waits more than a few minutes. The first brief did not, and later ones did.
- **Name a member's run conditions in its stop list**, not only its prompts: T-20's `--tools ""` slipped through on that gap.
- **Plants:** a plant in a file name fires only for agents without file tools. Plant a wrong face id instead, or give the run that condition from round 1.
- **Sample faces near their corners as well as their middles** in any check of shading.
- **Check a spec's pixel checks against the geometry at planning:** T-19's could never pass.
- **A wording fix searches every text that says the same thing**, not only the sentence a finding names.
- **Run a member's regression tests early**, not only at `verify`, so a wrong test costs little.
