---
description: >-
  An agent authors and unwraps UVs, carries them through file and export, and checks them by eye
---
# M-6 — UVs

**Status:** Active

**One-liner:** *An agent authors and unwraps UVs, carries them through file and export, and checks them by eye*

## What this milestone delivers

An agent authors UVs by coordinates and by projection, marks seams and unwraps, carries UVs through the Carve file and `.glb` export, and checks them in a UV render mode and with a checker texture.

Content: the UV look, the checker texture and a sample unwrapped model.

## Open at planning

- Which projections and which unwrap method; written here or from a library.
- Does the UV mode draw the layout flat, the checker on the model, or both?
- How seams relate to the mesh's edges and to export vertex splitting.

**Decided at planning (2026-11-09).** Asked and answered by the person, each on the recommended option:

- **Order:** M-6 runs after M-18. It builds on T-42's per-corner normals, `mark_edges` and file version 3, and on T-43's `subdivide`. M-5's cuts plan their own UV carrying whenever M-5 runs.
- **UV axes:** glTF's. (0,0) is the texture's top-left, u runs right and v runs down, in the tools, the file, the layout image and the export, with no conversion anywhere.
- **Storage:** UVs per face corner. A face may have none (unmapped); edits keep UVs.
  - `move` keeps them.
  - `extrude`'s cap keeps its own, and the new walls are unmapped.
  - `subdivide` interpolates them linearly.
  - `undo` restores them.
- **Seams:** a marked edge set (vertex pairs), beside hard edges. `unwrap` cuts at seams and at open edges. Export splits a vertex wherever its corners' UV or normal differ. `get_mesh` reports UVs and seams.
- **Tools:**
  - `set_uvs` (by coordinates);
  - `project_uvs` (`planar`, `box`, `cylindrical`, into a rectangle);
  - seams marked with `mark_edges(..., seam)`: T-42's tool widened, rather than a separate `mark_seams` (asked again once T-42's tool came to light);
  - `unwrap`.
- **Unwrap:** LSCM written here, with a conjugate-gradient solver in double, then a shelf packer. Deterministic, with no dependency.
- **Looks:** both.
  - `render` `mode: "checker"` draws the model lit with a generated checker in every view.
  - `mode: "uvs"` draws one mesh's layout flat (sketch below).
- **Check:** warns of unmapped, flipped, zero-area and overlapping UV faces.
- **Sample:** a barrel (`samples/barrel.carve`, seamed and unwrapped) plus the existing crate box-projected, in a scenario of their own.
- **Acceptance:** a closing run by fresh agents with a planted fault (T-62).

**Checked at planning:**

- **Engine probe** (2026-11-09, files in `%TEMP%\m6-probe`): a `carve`-exported cube with `TEXCOORD_0` and an embedded 8 x 8 checker added by hand loaded through `IModelLoader` in `spikes/Carve.ModelViewer` and drew headless, crisp under a nearest sampler. A marker texel at image (0,0) landed where glTF's top-left origin puts it.
  - The engine docs give `IGraphicsResourceManager.CreateTexture` from raw pixels and `Material.LitTextured`, and Carve's render vertex is already `PositionUvNormalVertex`.
  - So no engine need is expected. T-57 confirms Carve's own path first, and records an engine need if it fails.
- **The barrel's geometry**, defined through `carve` (40 vertices, 34 faces, 72 edges, closed, no warnings, `check` ok, volume 0.4398 m³). Its definition is in T-53's Notes. With T-56's seams it unwraps to three islands: the side (32 faces) and the two caps.

**Set by the planner, open to the person's objection:**

- the checker's colours (T-57);
- the layout's colours and window rule (T-58);
- `box`'s 3 x 2 grid and `cylindrical`'s defaults (axis +Y, seam at -Z) (T-55);
- unwrap refusing an island that is not a disc, the margin of 1/64, and the stretch measure (T-56);
- the overlap tolerance of 1e-6 in UV area (T-60);
- the "unflipped" rule: corners counterclockwise on screen with v down, i.e. a negative shoelace sum (T-55, T-60).

**The agreed sketch** (the person chose it from these drawings):

```
mode "checker" (any view)      mode "uvs" (512 x 512)
                               (0,0) u ->            (1,0)
     ____________             +-----------------------+
    /R W/O B V /|             |.dimmed checker........|
   /__/__/__/_/ |             |  +------+   +------+  |
  |R |O |Y |G | |             |  |  0   |   |  1   |  |
  |__|__|__|__| |             |  |      |   |      |  |
  |O |Y |G |B | /             |  +------+===+------+  |
  |__|__|__|__|/              |   white edges         |
  lit, hue per u column       |   === seam (yellow)   |
  white cell = uv (0,0)       |   0,1 = face ids      |
  unmapped face: magenta      v                       |
                            (0,1)                 (1,1)
                              +-----------------------+
                            u right, v down (glTF);
                            frame = unit square, grown
                            to fit UVs outside it
```

The checker is 256 x 256, in 8 x 8 cells. Each column has a hue from red (u = 0) to violet (u = 1), light and dark alternate, and the cell at (0,0) is white. In the layout, mapped faces show the checker at full brightness over the dimmed ground.

**Order.**

1. T-53.
2. T-54.
3. T-55, T-56, T-57, T-59 and T-60 side by side.
4. T-58 (after T-57, in the same render files).
5. T-61.
6. T-62.

Opus on T-53, T-56, T-57 and T-58, each planned for two agents. Sonnet on the rest. Ten tasks, two over the usual eight. The unwrap and the two looks are each a full agent's work, and the scenario and acceptance run follow the pattern of M-2 to M-4.

**Before dispatch:**

- check one GPU suite through the queue, and that every runner's feed has the pinned engine;
- read every member's Scope against what M-18 (and anything else since 2026-11-09) has landed.

The looks add no sound, motion or feel, so the person's look at the frames of T-57, T-58 and T-61 is the whole review.

## Required tasks

- [ ] [T-53](../tasks/T-53-uvs-and-seams-in-the-kernel-and-the-file.md) — A mesh holds UVs per face corner and marked seams, carried through every edit, undo and the Carve file
- [ ] [T-54](../tasks/T-54-set-uvs-seams-on-mark-edges-and-get-mesh.md) — An agent sets UVs by coordinates with set_uvs and marks seams with mark_edges, and get_mesh reads both back
- [ ] [T-55](../tasks/T-55-projections.md) — project_uvs lays chosen faces' UVs by planar, box or cylindrical projection into a rectangle
- [ ] [T-56](../tasks/T-56-unwrap.md) — unwrap cuts at seams and open edges, flattens each island by LSCM and packs them, shown on the barrel sample
- [ ] [T-57](../tasks/T-57-checker-look.md) — render mode checker draws the model lit with a generated checker texture, and unmapped faces magenta
- [ ] [T-58](../tasks/T-58-uv-layout-look.md) — render mode uvs draws a mesh's UV layout flat over the dimmed checker, with seams, labels and highlight
- [ ] [T-59](../tasks/T-59-uvs-in-export.md) — export writes each corner's UV as TEXCOORD_0, splitting vertices where UVs differ, and warns of unmapped faces
- [ ] [T-60](../tasks/T-60-uv-faults-in-check.md) — check warns of unmapped, flipped, zero-area and overlapping UV faces
- [ ] [T-61](../tasks/T-61-barrel-scenario.md) — The barrel is seamed, unwrapped and seen in both UV looks, and the crate box-projected, saved, reopened and exported, in suite scenario-uvs
- [ ] [T-62](../tasks/T-62-uvs-acceptance-run.md) — Fresh agents unwrap a model from the tool descriptions alone and find and fix a planted unmapped extrusion

## Retrospective

**As planned.**

- The order held: T-53, then T-54, then T-55, T-56, T-57, T-59 and T-60 side by side, then T-58, T-61 and T-62. Every rebase clash between siblings was additive (`Program.cs`'s tool list), and each member resolved its own.
- File ownership was set at dispatch and held:
  - T-57 owned `Model.Combined` and the render files.
  - T-59 owned export and built on T-57's `Combined` commit.
  - T-60 owned `check` and the shared `UvArea` helper.
- The research agents for T-56, T-57 and T-58 ran ahead of their dependencies (T-56 and T-57 beside T-54, T-58 beside T-57), so their open rules reached the person before implementation started.
- Everything is reachable through the product's own MCP tools:
  - UVs per face corner, hole corners included;
  - seams;
  - Carve file version 5, written only when a mesh has UVs or seams;
  - `set_uvs`, `mark_edges(seam)`, `project_uvs`, `unwrap` (LSCM by CG in double, shelf packer);
  - `render` modes `checker` and `uvs`;
  - `TEXCOORD_0` in export;
  - the four UV warnings in `check`.
- No engine need. T-57's probe confirmed that `LitTextured` multiplies vertex colour. T-58's probe confirmed that an unlit textured `Material` draws.
- The guard held: every existing frame and committed file stayed byte-identical by hash through every member that renders (T-57, T-58, T-61), and a model with no UVs exports byte-identical.
- The person approved every new frame set: T-57's 6, T-58's 8 and T-61's 16, all with no notes needing a change. T-58's reviewing agent found and fixed clipped corner labels itself before sending.
- The acceptance run (T-62) passed in round 1, with no description edit. A fresh `claude -p` (Opus 5.5) found the 8 unmapped walls with `check`, named the cause, seamed and unwrapped again, and fixed the stretch `unwrap` reported by seaming the middle rings. It finished with a clean `check`, 7 islands, stretch 1.0 and 0 flipped, and used both looks.
- The milestone's `verify` passes every check but `review`: 10/10 members merged and 14 suites passing, including the new `scenario-uvs`.
- **Models and agents per member:**

  | Member | Model | Agents |
  |---|---|---|
  | T-53 | Opus 5.5 | 2 (research, implementation) |
  | T-54 | Sonnet 5.5 | 2 |
  | T-55 | Sonnet 5.5 | 1 |
  | T-56 | Opus 5.5 | 2 (research, implementation) |
  | T-57 | Opus 5.5 | 4 (research, three implementation) |
  | T-58 | Opus 5.5 | 4 (research, three implementation) |
  | T-59 | Sonnet 5.5 | 2 |
  | T-60 | Sonnet 5.5 | 1 |
  | T-61 | Sonnet 5.5 | 1 |
  | T-62 | Sonnet 5.5 | 1, plus a Sonnet check in the engine viewer |

  Sonnet held up on every member given to it: no Sonnet member needed rework from a review. Every hand-off came from Opus members with frames (T-57, T-58) or from waiting on a sibling (T-59).

**Not as planned.**

- **M-6 ran beside M-19.** At first the orchestrator stopped on M-5's "ask first" lesson. The person said milestones may run in parallel unless they depend on or interfere with each other. M-19's remaining work (T-49, an acceptance run) touched none of M-6's files.
- **The plan predated M-5's landing.** T-53's Scope assumed file version 4 was free and that M-5's cuts would plan their own UV carrying. T-53's research agent found that neither held, and the person ruled version 5, UVs on hole corners, and a carrying rule for each of the six cuts.
- **The person ruled about 60 times**, most in batches of open rules: T-53 4 asked and 3 taken as recommended; T-54 1; T-56 10; T-57 11, then the thresholds, then 2 frame rules; T-58 14, then the tolerance and the seam blend fit; T-59 1; T-61 2; T-62 the run conditions and the engine-viewer check. Every one took the recommended option.
- **Three readings landed without asking, and each was ruled on after the fact:**
  - T-60 warns of a zero-area face only as zero-area, not also as flipped;
  - T-61 skips the seam-colour check on segments within 7 px of another island's line (11 of 40 at 256, 1 at 512, none at 1024);
  - T-61 chose its own thresholds for the checker's orange highlight tint.

  The person kept all three.
- **T-54's first run failed** server-regression: a test cut `get_mesh`'s description at a phrase T-54's addition moved. The fix moved the cut, and both assertions are unchanged.
- **T-54 handed off with a `verify` running in the background.** The orchestrator stopped the agent and waited on the queued jobs itself, as M-5 advised.
- **The orchestrator found two gaps in T-62's report:**
  - The `.glb` was checked in F3D, not in the project's viewer. On the person's ruling, a Sonnet agent ran `run-viewer.ps1` on it: exit 0, 92 triangles, drawn untextured (no material texture is exported).
  - The round's evidence was said to be merged to the milestone, but `artifacts/` is ignored, so only `%TEMP%` held it. The orchestrator copied it to `artifacts/m6-acceptance/round-1/`.

**Never verified.**

- **A real game.** The barrel's `.glb` has been loaded by the engine's viewer and by F3D, not in Orcward or another game, and never with a texture bound in a material (materials are M-7).
- **Weak or partial checks:**
  - T-61 does not check the seam colour on 12 of 120 segments (logged `not checked`).
  - T-57's and T-59's broken-product tables ran before their last rebase or last test. T-58's ran before its label fix, which touches none of the table's checks.
  - T-58's barrel frame hash comes from a local run, not a queued job.
  - `Combined` dropping UVs is caught only by T-57's two-mesh regression test, not by the one-mesh crate scenario.
  - T-62 re-derived no overlap or flip itself; it took `unwrap`'s and `check`'s word.
- **Scale:** `unwrap` was not timed on a large mesh. Choosing its pins is quadratic in an island's boundary vertices.
- **A real character with UVs** was not exported; T-59 used fixtures.
- **The orchestrator opened no frame.** Every look above is a member's or the person's.

**Known and left.**

- `CLAUDE.md` is an input of no suite (a `verify` warning, as in M-5).
- Worktree folders that `finish` could not remove remain on disk, unregistered from git: `branches/task/T-53`, `T-54`, `T-55`, `T-58`, `T-59`, `T-60`, `T-61` and `T-62`, beside the older ones. T-58's agent tried to delete its folder, was blocked by the permission check, and the orchestrator did not override the check.
- Scratch is not cleared: `%TEMP%\carve-m6-acceptance\` (with a Release publish of `carve`), `%TEMP%\carve-m6-tools\`, and the members' `%TEMP%\t57\` and similar.
- Evidence is in this worktree's ignored `artifacts/review/T-53` to `T-62` and `artifacts/m6-acceptance/round-1`, and the review rounds are under `G:\My Drive\artifacts\carve\M-6\`.
- `C:\tools\carve` is re-published by hand after the merge to `main`.

**Test report** (`cairn test report --since 2d`, which includes M-19's runs in the same span): `server-regression` is about 77% of the slot time, with 8 failures (6 caught by it alone) and 1 flake. 687 passes' logs ran elsewhere and can't be read here, so the flake can't be attributed. Nothing was filed. `scenario-uvs` is cheap, about 3 s a run.

**For the next plan.**

- **Re-read a plan's assumptions against what has landed since it was made**, at dispatch rather than in the first member. M-6's plan assumed M-5 had not run, which cost a research pass and seven rulings. Planning had not caught it.
- **Let research agents run ahead of their dependencies.** Planning only needs the dependency's spec and As built. Doing it beside the implementation saved a round trip each for T-56, T-57 and T-58.
- **Name shared check rules at planning.** Four arrived mid-milestone: the checker's white and magenta thresholds, the 3 px ring across coplanar faces, the seam blend fit, and the close-seam skip. All came from anti-aliasing and small frames, so the next look-heavy milestone should plan for them.
- **Say in an acceptance brief where evidence must live.** `artifacts/` is ignored, so "merged into the milestone" cannot carry it, and the member must write it at the milestone's absolute path.
- **Opus look members took four agents each.** Plan the frame review as its own agent from the start rather than counting on one implementer to reach it.
- **Clean up `finish`'s leftover folders** at the end of each milestone. That needs the person's leave where a delete is permission-blocked.
