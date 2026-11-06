---
description: >-
  A headless viewer spike loads a .glb from a content folder through IModelLoader, as a game does, and captures it from fixed views
---
# T-18 — Model viewer spike

**Status:** Done

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

### As built

Run on **Claude Opus 5.5** (`claude-opus-5-5`).

**Files.** `spikes/Carve.ModelViewer/`: `Program.cs` (the host and its defaults), `ViewerOptions.cs` (the command line), `ViewFrame.cs` (auto-framing), `ViewerLook.cs` (size, sun, light intensities), `ViewerSystem.cs` (load, pose, capture, `view.json`), `ViewerRun.cs` (exit code). `scripts/run-viewer.ps1`. `tests/Carve.ModelViewer.Tests/`: `CubeFixture.cs`, `ViewerProcess.cs`, `ViewerFrame.cs`, `ViewerCubeTests.cs`. `ProductReferenceGuard` now forbids `Carve.ModelViewer` and `spikes/`, and `server-unit` takes `spikes/**/*.csproj` as an input.

**How it runs.** `EmpyreanHostBuilder.CreateDefault(...).UseStandardHost(...)` with `VirtualSurface = true`, 512x512 and `AntiAliasing.X4` as code-level defaults, the `StandardHdr` preset, and `IScreenshotService` for the frames. `Empyrean:Time:FrameDeltaTime` is 1/60, inserted as the lowest configuration source. The scene has nothing random, so there is no seed to fix. Only `--Empyrean:...` arguments reach the host.

**For T-19.**

- **Command line:** `--content <folder> --model <content path> --out <folder> [--view <name>=<x,y,z>]...`; no `--view` means `front=1.6,1.2,2.2`. A view name is `[A-Za-z0-9_-]+`. A bad argument exits 2 with the usage on stderr. A view with no direction, or within 1 degree of vertical, is refused, since +Y is up. A failed load or capture exits 1 with a `carve-viewer: ...` line on stderr naming the model.
- **Script:** `pwsh scripts/run-viewer.ps1 <content> <model> <out> [views] [-NoBuild] [-Configuration Debug] [-TimeoutSeconds 120]`, each view as `<name>=<x,y,z>`; a word beginning `--` goes to the viewer as it is. The exit code is the viewer's, or 124 on a timeout. All the viewer's output goes to stderr.
- **`view.json`:** `model`, `triangles`, `bounds.min`, `bounds.max`, `materials[]` (`baseColor`, `metallic`, `roughness`, `pbr`), `views[]` (`name`, `direction`, `eye`), `sun` (`toSun`, `intensity`, `ambient`). It is written last, so a failed run has none. The material's name is not there: `Material` has no name to read, though the milestone's sketch says `material 'carve'`.
- **The sun:** toward the sun is normalised (0.3, 0.85, 0.55), world-fixed, intensity 2.0, white; ambient 0.2, white.
- **The cube's shades** (red channel, out of 255; green is the same and blue 1 or 2 less): top 207, +Z 191, +X 171, -X 125, background 0. So `front` is 207/191/171 and `minus-x` is 207/191/125, with +Z on the right there. The smallest gap between faces is 16/255.
- **Where the faces land on the unit cube:** `front` has the top's middle at (256, 133), +Z's at (172, 302), +X's at (367, 289); `minus-x` has -X's at (172, 302), +Z's at (367, 289).
- **Normals:** with no `NORMAL` in a file the engine welds the vertices and makes smooth normals, which the docs do not say. A face then runs from 125 to 211 and only its middle looks right, so sample a crate's faces away from their middles too. See below.

**The pixels chosen.** Each face's middle, projected from the `eye` in `view.json` by the test's own camera, because it is the furthest point from every edge and so free of anti-aliasing. Each corner sample is 4 pixels in, where the framing's margin keeps any box away. The flatness samples are a tenth in from each face corner: as near the corners as stays clear of the edges, which is where smooth normals differ most.

**Each check failing on a broken product** (`viewer-cube`, each break applied and reverted):

| Break | Checks that failed |
|---|---|
| Material left at metallic 1 | `View_json_says_what_was_loaded`, `The_top_plus_z_and_plus_x_faces_differ...` (+Z and +X 100 and 95, 5/255 apart), `The_sun_keeps_the_faces_of_the_minus_x_view_apart` (-X is black, as the background) |
| Normals dropped from the fixture | `Each_face_is_one_flat_shade` (top (125, 125, 123) a tenth in, 207 at its middle), and no other |
| Wrong content path (fixture written to `models/box.glb`) | `The_run_exits_zero...`, `View_json_says...`, `Corners_are...`, `The_top_plus_z...`, `The_faces_are_the_right_way_round`, `The_sun_keeps...`, `A_second_run...` |
| Sun turned to light all three equally (1, 1, 1) | `The_top_plus_z...` (195 on all three), `The_faces_are_the_right_way_round`, `The_sun_keeps...` |
| Camera mirrored (X scale -1) | `The_top_plus_z...`, `The_faces_are_the_right_way_round`, `The_sun_keeps...` |
| Camera flipped (Y scale -1) | `The_top_plus_z...`, `The_faces_are_the_right_way_round`, `The_sun_keeps...` |
| Framed too tight (margin -1.5) | `Corners_are...`, `The_top_plus_z...`, `The_faces_are_the_right_way_round`, `The_sun_keeps...` |
| Sun varied by the wall clock | `A_second_run_gives_byte_identical_frames` |
| Stray file in the working folder | `The_run_exits_zero_and_writes_only_to_its_out_folder` |
| Load error leaves out the file | `A_missing_model_fails_naming_the_file`, `A_corrupt_glb_fails_naming_the_file` |
| Exit code always 0 | `A_missing_model...`, `A_corrupt_glb...` |
| A vertical view accepted | `A_view_that_breaks_the_agreed_look_is_refused` |
| Viewer references `Carve.Core` | `The_viewer_references_no_Carve_project` |
| Product references the viewer (`Carve.Server.csproj`) | `The_product_project_references_no_extension`, `The_product_reaches_no_extension_through_its_project_references` |

`Each_face_is_one_flat_shade` is beyond the spec's list, and so is the `minus-x` run. The first table run showed dropped normals failing nothing: the frame changed in 107,665 pixels, but the sampled face middles did not. The flatness check closes that, and its 2/255 is tighter than the 10/255 rule, since a clean cube has a spread of 0.

The rows above ran before the flatness check existed, except "Normals dropped", "Load error" and "Stray file", so those others are not shown against it. Before it landed, the "Wrong content path" and "Framed too tight" breaks would make it fail or not by what they do to `front.png`; that was not run.

**Frames and review.** `front.png`, `minus-x.png` and the M-1 cube `front` were opened and looked at; they came from the queued `viewer-cube` run, not a run by hand. The framing is the same as M-1's, the three faces read apart, and `minus-x` has -X on the left and +Z on the right, as the milestone's sketch does. The person approved `front.png` (hash `96c9265cf60f8aad...`) beside the M-1 frame through `mcp__waystone__review`, with no notes. They were told of the two differences from M-1: the background is pure black (0, 0, 0), because `StandardHdr` clears to it, where M-1's is a dark slate; and the light is fixed in the world, on purpose. Both frames are under `D:\Repos\carve\branches\milestone\M-3\artifacts\review\T-18\round-1\`.

After the rebase onto T-17, `viewer-cube` carried its pass, so the approved frame was not captured again on the rebased commit.

**Not checked.**

- The window switch, `--Empyrean:Graphics:VirtualSurface=false`, has not been run, so as not to open a window on the person's machine.
- The "no Vulkan device" failure has not been produced; `Program.cs` catches it by design only.
- `minus-x` is checked on the cube, not on a crate; that is T-19's.
- No file from Carve's own exporter has been through the viewer; that is T-19's too.

**Surprising.**

- Bash on this machine scans heredoc bodies for quotes, so an odd number of apostrophes breaks the command. Use the Write tool for prose.
- `cairn test artifacts` writes to `./test-artifacts` unless given `--out`.
- `verify` sent `core-unit` and `server-unit` to Mantorok, which failed at restore with NU1603. `cairn test run` on each, then `verify` again, got them onto Xellotath, though a `server-unit` job failed on Mantorok once more on the way.
