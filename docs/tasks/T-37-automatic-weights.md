---
description: >-
  weigh gives every vertex up to four weights from bone proximity, deterministically
---
# T-37 — Automatic weights

**Status:** In Progress

**Pri:** P1

**Milestone:** M-17

**Batch:** — (standalone)

**Depends on:** T-44

**Verify:** server-regression, scenario-cube, scenario-crate, scenario-character

**Perf:** skip: Carve declares no heavy suites

## Goal

An agent rigs a whole mesh with one call, not a weight per vertex.

## Scope

- **Core:** weights by distance to each bone's segment, as D-6 says: a falloff by distance, the top four kept, normalised. A leaf bone's segment runs along its parent's direction, for half its parent's length. No randomness, and a fixed order for ties.
- **Tool:** `weigh(mesh, bones?)`, optionally limited to named bones. It leaves explicit weights alone unless given `replace: true`.
- **Sample:** the gardener weighted automatically, compared with its hand weights.
- **Tests:** `unit` for a vertex on a bone being fully that bone's, a point midway sharing it, determinism, and the cap of four; a bent-arm frame with no vertices left at rest.

## Notes

- The falloff is a design choice; record it and its constant in D-6. Stronger model.
- **Carried from the M-1 to M-4 plans:** the byte-identical guard on every existing frame, by hash before and after; a positive pixel check on every new frame, and one a mirrored or flipped camera would fail; each new check shown failing, as a table.
- **Open rules: stop and ask.** Anything D-6 does not settle, any loosened check, and any change to a D-6 decision.
- Run `finish` from inside the worktree and leave the folder.

### As built

Built on Opus 5.5 (claude-opus-5-5), in two agents (the second took over at the hand-off and added the gardener sample and the review).

- **Core** `src/Carve.Core/AutoWeights.cs`: segments at rest (one per child, the nearest counting; a leaf runs on along its parent's direction for half the parent's length; a lone childless root is a point), falloff `(d_nearest / d)^4`, a vertex at distance 0 shared equally among exactly the bones it lies on, then `VertexWeights.Normalise` (top four, ties to the lower index). Worked in double, in bone order; no randomness.
- **Tool** `weigh(mesh, bones?, replace?)` (`src/Carve.Server/WeighTool.cs`, `ModelStore.Weigh`), one undo step, result `{mesh, weighed, kept, unweighted}`.
- **Rulings recorded** (the person's, 2026-11-10, now in D-6 *Weights* with the falloff): "explicit" is any existing weight, so `weigh` fills only unweighted vertices and `replace: true` re-weighs every vertex in scope; no provenance, no file-format change. A bone with several children has a segment to each child's head, distance the nearest.
- **`bones` scope, ruled by the person 2026-11-10** (in D-6): with `bones`, a vertex is in scope when its nearest bone of the whole skeleton (ties to the lower index) is a named one, and it is weighed among the named bones alone. The first agent built it so and asked; the person confirmed it.
- **Tests:** `AutoWeightsTests` (unit): on a bone wholly that bone's (and on a leaf's extension), midway shared evenly (two leaves, and a joint half and half), the fourth-power falloff by value, several children as near as the nearest segment, determinism bit for bit, the cap of four with ties to the lower index, keep/replace, named-bone scope, a lone root. `weigh` joins the named-mesh theory in `NamedMeshTests` and the unknown-argument sweep.
- **Frame:** `chain-weighed-bent-z` in scenario-crate (`ChainPose.WeighedArmAsync`): the chain's two boxes redefined as columns with a ring every 0.5 m, weighed with one call each, bent a quarter turn. Positive checks on the arm at x -0.3, -0.7, -0.85; background where the upper column rested, where a mirrored bend or camera would put the arm, above and below.
- **Sample** (scenario-character, `WeighAsync` after `ExportAsync`, so `samples/gardener.carve` is unchanged): the hat's top extruded again, and default `weigh` weighs exactly the 8 new vertices and keeps the 48 hand-weighted ones; both undone, `gardener.glb` exports byte-identical to the hand-weighted one. Then `weigh replace: true` on Body and Hat, saved to `work/gardener-weighed.carve`, and the dominant bone per vertex compared with the hand weights in `weigh-agreement.txt`: **Hat 48 of 48 agree; Body 208 of 320 (65%)**. The disagreements are mostly at joints (Hand_R to Elbow_R or WateringCan, Spine_03 to Head, UpperLeg to Hips, Ankle to Ball or LowerLeg), but a few cross the body: torso vertices go to the elbows and shoulders (Hips to Elbow_L/R, Spine_03 to Shoulder_L/R), and one can vertex to UpperLeg_R, since the hanging arm and can lie close beside the torso and thigh. Frames `gardener-weighed-rest-front` (byte-identical to the hand-weighted rest, checked) and `gardener-weighed-pose-front` (the pose's checks: the can follows the raised arm, not where a mirrored frame puts it). In the pose the auto-weighed shirt bulges out under the raised arm and one corner of the can is stretched down toward the thigh: what pure distance weighting does, with no visibility or heat diffusion. No agreement floor is asserted: the figure is logged for the person to judge, not a check.
- **Capture review** (round 1, `C:/Users/andre/artifacts/carve/M-17/T-37/round-1`: both chain bends, both gardener poses and the agreement log): **approved** by the person, with two notes left open for a later member, not acted on here since either fix would change D-6's falloff decision: on the chain, "Should the inside of the chain be pulled in so much?" (the inner corner of the elbow collapses where the joint ring is shared half and half, linear blend skinning's pinch); on the gardener, "The side of the torso appears to be following the arm, and not the spine" (the torso's side vertices lie nearer the hanging arm's segments than the spine's, so distance alone gives them to the arm).
- **Byte-identical guard:** scenario-crate run on the commit before (run 7) and after (run 8) on this machine: 44 of 44 earlier frames byte-identical by `cmp`, one new frame.
- **Byte-identical guard, scenario-character:** run 1 (before the sample) and run 2 (after) on this machine: 17 of 17 earlier frames byte-identical by `cmp`, and `work/gardener.carve` too; two new frames.
- **Each new check shown failing** (local runs with the scenario mutated, then restored):

| Mutation | Check that failed |
|---|---|
| upper column not weighed (left at rest) | the weighed arm at x -0.3 (and, with the arm checks removed, "where the upper column rested") |
| bend turned the other way (as a mirrored camera shows) | the weighed arm at x -0.3 (and, with the arm checks removed, "x +0.4" background) |
| each column weighed with one bone only (`bones`) | `weigh upper: expected all 12 vertices weighed` (got 8; the joint ring is nearer base by the tie rule) |
| (gardener) default `weigh` after the extrude given `replace: true` | `weigh Hat after the extrude: expected the 8 new vertices weighed and the 48 hand-weighted kept` (got 56 weighed, 0 kept) |
| (gardener) only one of the two undos | `undo of the weigh and the extrude: the hat is not as it was` |
| (gardener) a hat weight changed after the undos (unseen by `get_mesh`) | `gardener.glb, exported after the weigh and the extrude were undone: first differ at byte 56468` |
| (gardener) the weighed rest captured in the pose | `the weighed gardener at rest, against the hand-weighted rest: first differ at byte 113708` |
| (gardener) the can's group set to Hips after the weigh | `gardener-weighed-pose-front: the pose: the can face, posed to <-0.74, 0.65, 0.3> ... reads as background, expected can` |
