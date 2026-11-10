---
description: >-
  The test character is subdivided and smoothed through the tools and shown soft and with hard creases, in its own suite
---
# T-44 — Soft character scenario

**Status:** Done

**Pri:** P1

**Milestone:** M-18

**Batch:** — (standalone)

**Depends on:** T-43

**Verify:** full

**Perf:** skip: Carve declares no heavy suites

## Goal

The gardener looks the way the game wants: soft and painterly, with a few deliberate creases.

## Scope

- **`scenarios/Carve.Scenario.Soft`**, suite `scenario-soft`: the gardener subdivided and smoothed through the tools, with the hat's brim creased.
- Frames in rest and mid-clip, flat beside smooth, with checks that shading is continuous across a soft edge and breaks across a hard one.
- Exported, loaded and compared by triangle count and normals.
- `samples/gardener.carve` is updated if the soft version replaces it. Say which was kept.

## Notes

- **Carried from the M-1 to M-4 plans:** the byte-identical guard on every existing frame, by hash before and after; a positive pixel check on every new frame, and one a mirrored or flipped camera would fail; each new check shown failing, as a table.
- **Open rules: stop and ask.** Anything D-6 does not settle, any loosened check, and any change to a D-6 decision.
- Run `finish` from inside the worktree and leave the folder.

### As built

Run on Sonnet 5.5 (one agent).

**The person's rulings (2026-11-10):**
1. `samples/gardener.carve` stays the flat gardener, built by `scenario-character` and compared by content. A new `samples/gardener-soft.carve` (version 3) holds the soft one, kept equal by content to what `scenario-soft` builds through the tools, by `tests/Carve.SoftLoad.Tests` (read, written the one way, compared as T-36 did).
2. Subdivided at level 1: **2,576 triangles** in the model against the 5,000 budget (flat: 680).

**Built:** `scenarios/Carve.Scenario.Soft` (suite `scenario-soft`, `scripts/run-soft-scenario.ps1`, `tests/Carve.SoftLoad.Tests`, in `Carve.slnx` and `tests.json`; `GardenerSpec.cs` is linked from the Character scenario, not copied). Built flat through the tools and shown (rest front, Walk 0.125 s side, hat close, exported flat), the brim's two rims (16 edges) marked hard with `mark_edges`, `subdivide` level 1 on Body and Hat, `set_shading smooth`, shown again; the brim uncreased with the very edges `set_shading` listed, shown, creased again (the frame is byte-identical to the creased one); saved, a decoy `define_mesh`, `open`, `get_mesh` equal, three reopened frames byte-identical, saved again to the same bytes; exported soft.

**Checks** (each worked from the geometry): every colour reads as itself on both rest frames, the can is on the image's left with none where an image mirror or a world mirror would put it (also in the hat close); torso, flat: a step of 30 across each side facet edge and a plateau of 10+ pixels; soft: 5 to 9 across the same span and runs of 5 pixels or fewer (a ramp, which also fails on a subdivided mesh left flat); Walk 0.125 s: the near hand is skin where the keys put it and not at its image mirror, flat and soft (weights carried); hat: the creased brim holds its shade to the rim and breaks at once at the rim (first step of 10 or more is 28 flat, and 20+ soft), the uncreased one rolls off in steps under 20 (12 at most). Load check: the soft `.glb` loads with 22 bones in order, clips `Idle`, `Walk`, no parse warnings; its triangle count equals the saved model's and the scenario's; the loaded (position, normal) corners equal `Shading.Triangles` of the saved model; every crown position has one normal and every brim rim position two (hard); the flat export has no warnings.

**New checks shown failing** (local runs with a mutation, reverted):

| Mutation | First check that failed |
|---|---|
| every projection mirrored in x | soft-flat-rest-front: the boots face reads as background |
| every projection flipped in y | soft-flat-rest-front: the trousers face reads as shirt |
| `set_shading` flat on the subdivided meshes | soft-smooth-rest-front: the torso has a run of 18 equal pixels, a smooth one is a ramp |
| `set_shading` flat, before the run-length check was added | the uncreased hat frame (first step 40); the torso step check alone passed, so the run-length check was added |

Mirror and flip fail at the first check of the first frame, so the later frames' own mirror checks are not each shown failing. The gardener's torso and hat are symmetric, so the torso, brim and hat-close shading checks do not tell a mirror; the colour checks (can on the left, boots, trousers) and the hand's image mirror do.

**Hash guard:** baseline `origin/milestone/M-18` (5510213) run locally in a temporary detached worktree (removed) against the verify job's frames fetched by job id (J-20261110-220605-71b14c cube, -220607-921c62 crate, -220610-2a408d shed, -220614-c6df64 character, 186e09e): cube 32, crate 44, shed 10, character 17 frames, all byte-identical, none missing. Export scenario: passed in verify; its files were not hashed (nothing it uses changed). Lists in `artifacts/review/T-44/`.

**The not-planar flood got in the way:** the soft export carries 504 warnings, each a face that is not planar, so the usual "export has no warnings" check cannot hold. The soft scenario and load check assert that every warning is a not-planar face (none is a limit warning) and that the flat export has none. **The person's ruling (2026-11-10, after RV-27), as built:** until T-72 lands, both also assert that the warnings' count equals `select faces where non_planar` on the same model; the scenario selects on Body and Hat after the export and keeps the results (`select-non-planar-<mesh>.txt`) in the run folder for `SoftLoadTests`. A fold in the export result would be a task of its own.

**Not checked:** the other frames (reopened, recreased) were not opened; the person reviewed the seven frames in `round-1` and approved them. Nothing for D-1's engine needs.
