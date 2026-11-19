---
description: >-
  A stylised face with targets Smile, Frown, BrowRaise, Blink_L, Blink_R and JawOpen on a Neck > Head rig, built through the tools and seen at rest and at weights
---
# T-149 — The face, built through the tools

**Status:** Open

**Pri:** P1

**Milestone:** M-32

**Batch:** — (standalone)

**Depends on:** — (none)

**Verify:** core-unit

**Perf:** skip: Carve declares no heavy suites

## Goal

M-32's content exists from the first wave: the agreed face is built through today's tools (no clip weights yet), rigged, and seen at rest and at each target's weight. Its frame list for the clips is worked through the ray test before T-151 draws them.

## Scope

- **New scenario** `scenarios/Carve.Scenario.Face` (CLAUDE.md's *Writing a scenario*), joined to `Carve.slnx`, with a `regression` suite `scenario-face` in `tests.json` and `scripts/run-face-scenario.ps1`. It follows `scenario-morph`'s pattern (linking `CameraPixels.cs`, `ComparisonPng.cs`, `NotPlanarFold.cs` as Morph does).
- **`FaceSpec.cs`**: the face as agreed at planning (the sketch is in [M-32](../milestones/M-32-faces-that-move.md)).
  - Four meshes: `Head` (skin, with a pulled nose wedge and a mouth slot whose inside is dark red), `Eyes` (white with black pupils, static), `Brows` (dark brown) and `Lids` (skin).
  - Cages are built with `define_mesh` and the edit tools, then subdivided once and smoothed.
  - The face looks along +Z, so its own left (`_L`) is image right from the front.
  - Bones `Neck` (root) > `Head`, every vertex weighted to `Head` except the neck ring.
- **Targets**, each through `define_target` with the deltas worked in `FaceSpec`:
  - `Smile` (Head: mouth corners up);
  - `Frown` (Head: corners down; Brows: inner ends down). One dial on two meshes.
  - `BrowRaise` (Brows);
  - `Blink_L` / `Blink_R` (Lids: one lid each closing over its eye);
  - `JawOpen` (Head: lower lip and chin down, showing the inside).
  - `check` at each target's weight 1 is silent, except for pinned not-planar warnings (`NotPlanarFold`, as T-106 ruled).
- **Frames (render):**
  - rest front, side (+X) and 3/4;
  - each target at weight 1 from the front, and JawOpen from the side too.
  - Every pixel check is worked from `get_mesh`'s positions by a ray test (`FacePlan.cs`, as `ArmPlan` does). `FaceGeometry` in `tests/Carve.Core.Tests` (linking `FacePlan.cs`) works the counts and probes first.
  - Blink_L closing image-right only is the mirror guard: a mirrored camera fails it.
- **Save** as `samples/face.carve` (version 8 here; T-150 rewrites it at version 10 with clips). It's kept equal byte for byte to what the scenario saves, and reopened with frames byte-identical.
- **The clip frame list** agreed at planning (M-32's frames 4–14 and the render_glb crops): work each frame through `FacePlan`'s ray test, at the agreed clip times with the clips' planned weights.
  - Record which features are in sight and which checks each frame carries, and which the face's symmetry can't carry.
  - Put every miss to the person in **one** question before this task finishes (M-9's lesson).
  - Write the result into this task's As built for T-151.
- **Breaks:** `CARVE_FACE_BREAK` with `mirror` and `weights` (targets ignored) must fail.

## Notes

- **`scenario-face` in `**Verify:**`:** the suite doesn't exist until T-149 registers it in `tests.json`, so lint refuses it at planning. Add `scenario-face` to this task's `**Verify:**` line as its first commit (T-149: once the suite is registered).

- **Design-heavy content that others build on** (T-150, T-151 and T-152 use the face): Opus for the geometry and the ray test.
- **Plan a second agent** for the frame review: it opens the frames itself, and the frames then go to the person.
- **Runs in wave 1 beside T-144,** using only today's tools. Don't touch `src/`. If the face needs something a tool can't do, stop and ask rather than widening a tool.
- **File ownership:** the new scenario, `FacePlan.cs`/`FaceGeometry` tests, `samples/face.carve`, `scripts/run-face-scenario.ps1`, the `scenario-face` entry in `tests.json`, and a `Carve.Scenario.Face` row in CLAUDE.md's Layout table.
- **Stop and ask:** departing from the sketch (shape, meshes, which eye is `_L`), the not-planar count, and any check you'd loosen.
- **Write down for dependents (As built):** `FaceSpec`'s API (how T-150 adds clips), each target's moved vertex rows, and the worked clip frame list.
- **Carried:**
  - each new check shown failing on a broken product, as a table;
  - evidence at the absolute path the dispatch names;
  - `finish` run inside the worktree, with its `Merge task/T-149` line given.
