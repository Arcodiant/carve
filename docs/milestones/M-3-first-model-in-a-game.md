---
description: >-
  An agent saves its work to a Carve file and exports a .glb that loads through Empyrean
---
# M-3 — First model in a game

**Status:** Planned

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
