---
description: >-
  The Carve file holds the part tree as version 7, written only when a model needs it, and save and open carry one part's geometry and levels as a piece
---
# T-92 — Carve file version 7

**Status:** Open

**Pri:** P1

**Milestone:** M-9

**Batch:** — (standalone)

**Depends on:** T-91

**Verify:** core-unit, server-unit

**Perf:** skip: Carve declares no heavy suites

## Goal

A part tree survives `save` and `open` bit-exact, and every file written before M-9 is still written byte for byte. That follows the person's ruling at M-9's planning (2026-11-17): version 7, written only when the model needs it.

## Scope

- **`CarveFile` version 7** is version 6 plus, after `meshes`, a `parts` array: one object per part in `Model.Parts` order, holding `name`, `parent` (or null), `translation`, `rotation` `[x, y, z, w]`, `scale`, `envelope` (`{"min": [...], "max": [...]}`, left out when absent), `instance_of` (left out when absent) and `moves` (left out when false). A mesh object in `meshes` gains `levels`, left out when there are none: an array of `{"level": N, ...}` objects, each holding the mesh keys a version 6 mesh has (vertices, faces, colours, groups, shading, hard edges, UVs, seams, materials). An empty part or an instance has no entry in `meshes`.
- **The lowest version that holds the model** (the 2026-11-11 rule): version 7 only when some part has a parent, a non-identity transform, an envelope, `instance_of`, `moves` or a kept level. Otherwise the file is exactly what version 6 or lower, or version 8 for a model with targets (T-101), writes today. Numbers are written as the existing writer writes them, and read back bit-exact.
- **Reading** takes versions 1 to 6 and 8 as well as 7 (8 is M-21's targets, T-101, already on `main`). Every version 7 problem is named by where it is (`parts[2].rotation`, `meshes[0].levels[1].faces[3]`), and reading refuses whatever `Model.Create` refuses (T-91). An unknown key is refused, as today.
- **Pieces, as ruled:** `save mesh=X` writes X's own geometry and kept levels as a top-level part at the origin, without its parent, transform, envelope, children or instances. `open mesh=X` adds it as a new top-level part, or replaces the geometry and levels of an existing part named X and keeps that part's placement. Naming an instance or an empty in either is a tool error that names what it is. A skinned piece keeps today's rules (skeleton and clips come along).
- **Descriptions:** `save` and `open` say what a part tree keeps and what a piece carries, with one worked example each. A wording change searches every text that says the same thing (the M-3 lesson).
- **Tests (`unit`):** the rover from `RoverSpec` (T-91) round-trips by content and writes the same text twice; every pre-M-9 sample and fixture still reads to the same model, and `samples/*.carve` written again are byte-identical; a model of untransformed top-level meshes writes version 6 or lower; every version 7 refusal, one test each; `save mesh=` and `open mesh=` on a part, on an instance (refused), on an empty (refused), and replacing a placed part's geometry while its placement stays. Tool tests run in-process at `unit` tier through M-20's harness.

## Notes

- **Decides a format others build on** (M-12 reads the levels, M-13 adds sockets): Opus.
- **File ownership in its wave:** `CarveFile.cs`, `SaveTool.cs`, `OpenTool.cs`. `ModelStore.cs` is shared with T-93 for adds only: keep both sides in a rebase. Changes to `Model` are additive, and T-91 owns its shape.
- **Open rules: stop and ask.** Key order or number formatting that differs from version 6's writer; whether a level holds keys a level can never differ in (for example `shading` when only geometry changed). The recommendation is to keep the full mesh keys, as the snapshot ruling says. Also any existing file whose bytes would change, and anything the rulings do not settle.
- **Open question for the person at M-9's planning (RV-64, not decided here):** which version holds a model with both a part tree and morph targets. Version 7 has no `targets` and version 8 (T-101) has no `parts` or `levels`; T-101's As built names the two ways it saw (a version above both, or 8 growing the part keys).
- **Write down for dependents (As built):** the version 7 layout as landed, with one short example.
- **Carried:** every committed `.carve` byte-identical, by hash before and after; each new check shown failing on a broken product, as a table; evidence at the absolute path the dispatch names; `finish` run inside the worktree, its output read whole and its `Merge task/T-92` line given; report a `verify` that waits more than a few minutes.
