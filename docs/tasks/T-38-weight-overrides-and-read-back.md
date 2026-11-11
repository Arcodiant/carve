---
description: >-
  An agent overrides weights on a face group, reads weights back, and is warned about unweighted vertices
---
# T-38 — Weight overrides and read-back

**Status:** In Progress

**Pri:** P1

**Milestone:** M-17

**Batch:** — (standalone)

**Depends on:** T-37

**Verify:** server-regression, scenario-cube, scenario-crate

**Perf:** skip: Carve declares no heavy suites

## Goal

An agent reads what a rig has, by vertex or by bone, with gaps reported rather than found in the game.

## Scope

- **`get_weights(mesh, vertices? | bone?)`:** per-vertex influences by bone name, or every vertex a bone moves, plus a summary and warnings.
- **Warnings:** unweighted vertices, listed by id with a count; a bone no vertex follows, as a note.
- **Tests:** `unit` for a `set_weights` group override after `weigh`, read-back equal to what was set, and warnings; `regression` for the worked examples as written.

## Notes

- **Carried from the M-1 to M-4 plans:** the byte-identical guard on every existing frame, by hash before and after; a positive pixel check on every new frame, and one a mirrored or flipped camera would fail; each new check shown failing, as a table.
- **Open rules: stop and ask.** Anything D-6 does not settle, any loosened check, and any change to a D-6 decision.
- Run `finish` from inside the worktree and leave the folder.

### As built

Built on Sonnet 5.5 (claude-sonnet-5-5), one agent.

- **Tool** `get_weights(mesh?, vertices? | bone?)` (`src/Carve.Server/GetWeightsTool.cs`, registered in `Program.cs`), read-only (no undo step). By vertex: `{mesh, vertices:[{vertex, weights:{bone: weight}}], summary, warnings}`, an unweighted vertex reading `{}`. By `bone`: `{mesh, bone, followers:[{vertex, weight}], summary, warnings}`. With neither argument it reads every vertex (D-6 does not say; chosen so the read-back of a whole mesh is one call); both is a tool error, as are an unknown bone, a bad vertex id, an empty list, no mesh and no skeleton.
- **Summary and warnings** always cover the whole mesh: `summary {vertices, weighted, unweighted, unweightedVertices, boneVertices:{bone: count}}`; warnings: "N vertices have no weight: ids..." (with the pose and `export` consequences) and "note: no vertex follows 'a', 'b'".
- **`set_weights` group override** already existed (T-30); no change to it.
- **Tests:** `GetWeightsToolTests` (regression, real process): the worked example in the description run as written (`{"vertices":[0,7]}` on an unweighted cube, empty weights, both warnings); a `set_weights` group override after `weigh` read back equal to what was set (0.75/0.25, by vertex and by bone) with no unweighted warning; partial weights listing the unweighted ids and the idle-bone note; bad requests; no mesh/skeleton. `get_weights` joins the named-mesh theory and the unknown-argument sweep in `NamedMeshTests`. The worked example is in the tool description, written by me (the spec's examples were not found elsewhere).
- **Not touched:** `render`'s description list of tools that still work in a pose (T-39 owns `render`'s text; it omits `get_weights`).
- **Frames:** none added, so no byte-identical guard or failing-check table applies.
- **Engine needs:** none.
