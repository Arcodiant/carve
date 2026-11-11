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

**Verify:** server-regression, scenario-cube, scenario-crate

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

Built on Opus 5.5 (claude-opus-5-5), in one agent.

- **Core** `src/Carve.Core/AutoWeights.cs`: segments at rest (one per child, the nearest counting; a leaf runs on along its parent's direction for half the parent's length; a lone childless root is a point), falloff `(d_nearest / d)^4`, a vertex at distance 0 shared equally among exactly the bones it lies on, then `VertexWeights.Normalise` (top four, ties to the lower index). Worked in double, in bone order; no randomness.
- **Tool** `weigh(mesh, bones?, replace?)` (`src/Carve.Server/WeighTool.cs`, `ModelStore.Weigh`), one undo step, result `{mesh, weighed, kept, unweighted}`.
- **Rulings recorded** (the person's, 2026-11-10, now in D-6 *Weights* with the falloff): "explicit" is any existing weight, so `weigh` fills only unweighted vertices and `replace: true` re-weighs every vertex in scope; no provenance, no file-format change. A bone with several children has a segment to each child's head, distance the nearest.
- **My reading of the ruling's "scope"** (in D-6, flagged for the person): with `bones`, a vertex is in scope when its nearest bone of the whole skeleton (ties to the lower index) is a named one, and it is weighed among the named bones alone.
- **Tests:** `AutoWeightsTests` (unit): on a bone wholly that bone's (and on a leaf's extension), midway shared evenly (two leaves, and a joint half and half), the fourth-power falloff by value, several children as near as the nearest segment, determinism bit for bit, the cap of four with ties to the lower index, keep/replace, named-bone scope, a lone root. `weigh` joins the named-mesh theory in `NamedMeshTests` and the unknown-argument sweep.
- **Frame:** `chain-weighed-bent-z` in scenario-crate (`ChainPose.WeighedArmAsync`): the chain's two boxes redefined as columns with a ring every 0.5 m, weighed with one call each, bent a quarter turn. Positive checks on the arm at x -0.3, -0.7, -0.85; background where the upper column rested, where a mirrored bend or camera would put the arm, above and below.
- **Byte-identical guard:** scenario-crate run on the commit before (run 7) and after (run 8) on this machine: 44 of 44 earlier frames byte-identical by `cmp`, one new frame.
- **Each new check shown failing** (local runs with the scenario mutated, then restored):

| Mutation | Check that failed |
|---|---|
| upper column not weighed (left at rest) | the weighed arm at x -0.3 (and, with the arm checks removed, "where the upper column rested") |
| bend turned the other way (as a mirrored camera shows) | the weighed arm at x -0.3 (and, with the arm checks removed, "x +0.4" background) |
| each column weighed with one bone only (`bones`) | `weigh upper: expected all 12 vertices weighed` (got 8; the joint ring is nearer base by the tie rule) |

### Hand-off

Checkpoint: code committed and `cairn task T-37 verify` passed (server-unit, scenario-cube, scenario-crate, core-unit, server-regression all ran), with the capture review and the gardener sample still to come.

- **Done:** Core, tool, unit tests, the bent-arm frame and its failing table, D-6.
- **Left:** (1) the **Sample**: the gardener weighed automatically, compared with its hand weights. It belongs in `scenarios/Carve.Scenario.Character` after `ExportAsync` (so `samples/gardener.carve` stays as it is): reopen `work/gardener.carve`, `weigh` both meshes with `replace: true`, save to `work/gardener-weighed.carve`, compare the dominant bone per vertex with the hand weights (log the agreement in the run folder), and capture rest and pose frames beside the hand-weighted ones. The extrude in `ExportAsync` (an unweighted hat vertex) is a natural place to show default `weigh` filling only the new vertices. scenario-character is not in this member's **Verify** line; adding it is needed for that work to be covered. (2) The capture review of round 1 (`C:\Users\andre\artifacts\carve\M-17\T-37\round-1`: `chain-weighed-bent-z.png` beside the hand-weighted `chain-bent-z.png`), not yet asked. (3) `finish`.
- **Checked:** the new frame opened once: a bent arm with a blended elbow, on the image's left, nothing at the rest place.
- **Question for the person:** is the reading of "the named bones' scope" above (nearest bone over the whole skeleton is a named one; weighed among the named alone) what was meant?
