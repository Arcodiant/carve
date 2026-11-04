---
description: >-
  A headless viewer spike loads a .glb from a content folder through IModelLoader, as a game does, and captures it from fixed views
---
# T-18 — Model viewer spike

**Status:** Open

**Pri:** P1

**Milestone:** M-3

**Batch:** — (standalone)

**Depends on:** T-16

**Verify:** full

**Perf:** skip: Carve declares no heavy suites

## Goal

A small Empyrean app stands in for a game. It mounts a content folder, loads a `.glb` from it by content path through `IModelLoader` with the file's own material, and writes frames, so an export is proved to load and is seen as a game shows it. It is a spike: not shipped, and referenced by nothing in `src/`.

## Scope

**`spikes/Carve.ModelViewer/Carve.ModelViewer.csproj`** (new folder `spikes/`): an exe referencing the Empyrean packages it needs at `$(EmpyreanVersion)`, and no Carve project.

- **Command line**: `--content <folder> --model <content path> --out <folder> [--view <name>=<x,y,z>]...`, for example `--content game/Content --model models/crate.glb --view front=1.6,1.2,2.2 --view minus-x=-2.2,1.2,1.6`. With no `--view`, the default is `front=1.6,1.2,2.2`.
- **Loads as a game does**:
  - `content.Mount(ContentSources.Directory(<content>))`;
  - `models.LoadModel(new ContentPath(<model>))`;
  - one `MeshInstance` with `model.Mesh`, `model.Material` and `SubmeshMaterials`, in `Mode3D` at identity.

  No Carve code, and no change to the loaded geometry or material.
- **Headless by default**, through the engine's own configuration (see the Graphics `AGENTS.md`, *On a headless host*: `IOffscreenRenderer`, or `VirtualSurface` with `IScreenshotService`). Set it as a default below the command line, so `--Empyrean:...` can switch a window back on for a person to watch, as `CLAUDE.md`'s *Headless runs* says. No `--headless` flag of the app's own.
- **Look** (agreed at planning):
  - 512x512 and a dark background;
  - one fixed sun plus ambient light, which does **not** turn with the camera;
  - each view auto-framed on the model's bounds, with the camera on the given direction from the bounds' centre, +Y up.

  Choose the sun so that in both agreed crate views (`front` (1.6, 1.2, 2.2) and `minus-x` (-2.2, 1.2, 1.6)) the three visible face directions differ by at least 10/255. Record the sun and the light intensities in the code with a comment.
- **Output** in `--out`:
  - `<view>.png` per view;
  - `view.json`: the model path, triangle count, bounds, each submesh material's base colour, metallic, roughness and `Pbr` flag as loaded, the views, and the sun;
  - a non-zero exit code with a message on stderr if the load or a capture fails (a missing file, a bad `.glb`, no Vulkan device).
- **Repeatable**: a fixed clock and seed where the engine uses them, and nothing written outside `--out`. Two runs give byte-identical PNGs.
- **`scripts/run-viewer.ps1 <content folder> <model> <out folder> [views]`** builds (unless `-NoBuild`), runs the viewer and returns its exit code. T-19's suite calls it.

**Fixture and tests**: `tests/Carve.ModelViewer.Tests` (new), `regression` tier (it needs a GPU):

- Writes a fixture into a temp content folder at `models/cube.glb`: the unit cube, flat normals, the `carve` material, written with SharpGLTF at T-16's pin, as the planning probe did. It does not call Carve's exporter: the viewer is proved on its own.
- Runs the viewer through `run-viewer.ps1` with the `front` view and checks:
  - exit 0;
  - `view.json`: 12 triangles, base colour `(0.72, 0.72, 0.70, 1)`, metallic 0, roughness 1;
  - **positive pixel checks** on `front.png`: corners are background; the top, +Z and +X faces each differ from the background and from each other by at least 10/255. Say why each pixel was chosen.
- A missing model and a corrupt `.glb` each give a non-zero exit and a message naming the file.
- A second run gives byte-identical PNGs.
- **Show each check failing**: the material left at metallic 1, normals dropped from the fixture, the wrong content path, the sun turned to light all three faces equally. Give the table.

**Wiring**:

- `Carve.slnx` lists both new projects.
- `tests.json` gains a `viewer-cube` suite (`regression`, `gpu`, `vulkan`).
- `ProductReferencesNoExtensionTests`, or its guard, also fails if the product references `Carve.ModelViewer`.
- `CLAUDE.md`'s *Layout* gains rows for `spikes/Carve.ModelViewer`, `tests/Carve.ModelViewer.Tests` and `scripts/run-viewer.ps1`, with one line saying the viewer stands in for a game. It is not the scenario harness, and nothing in the product uses it.

**Show the person** `front.png` of the cube (`mcp__waystone__review` where available, kind `approve`) before this task lands, beside the M-1 cube `front` frame for comparison. Their approval is the gate.

## Notes

- **Decided by the person at planning:**
  - a headless game-like viewer, a spike that is not published, to prove an export loads through `IModelLoader`;
  - the frames' look: 512x512, auto-framed, the file's own material, a fixed sun and ambient, dark background;
  - the two crate views (T-19 captures them).
- **This is a stand-in for a game, not a second scenario harness.** It does no MCP and has no debug extension. The rule against one-off launchers applies to looking at carve, which still goes through the harness. Do not grow the viewer into a carve client.
- **Open rules: stop and ask.**
  - Any view, size or background other than agreed.
  - A light that turns with the camera.
  - Any change to the loaded model or material in the viewer (it must show what a game sees).
  - Anything that would make the product reference the viewer.
  - Any tolerance looser than 10/255.
- If the engine cannot do something the viewer needs (for example no way to capture a headless frame with a PBR material), record an engine need in D-1's table as `CLAUDE.md` says, and stop and report. Do not file a request.
- **Write down for T-19:** the command line, the script, `view.json`'s fields, the sun, and the cube's shades.
- This task owns `Carve.slnx`, `tests.json` and `CLAUDE.md` in its wave (T-17 runs beside it and touches none of them).
- Its frames fill context. Leave room for them; look at them together at the end.
- Nothing has sound or motion, so there is no listening or real-time pass.
- Run `finish` from inside the worktree and leave the folder.
