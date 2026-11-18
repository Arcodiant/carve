---
description: >-
  The Carve file holds the part tree as version 7, written only when a model needs it, and save and open carry one part's geometry and levels as a piece
---
# T-92 — Carve file version 7

**Status:** In Progress

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

### As built

Opus 5.5, 2026-11-18, one agent.

- **The layout as landed.** Version 7 is version 6 with `parts` after `meshes` (before `skeleton`), and `levels` as the last key of a mesh (after `hard_edges`, or `uvs`/`seams` when there are any). Every other key, its order and every number are written exactly as version 6's writer writes them (floats by `"R"`, ids as integers, two-space indent, `
`). `parts` holds every part's record, one per line in `Model.Parts` order (pre-order), so a read gives the same order back; each record is `name`, `parent` (a name or `null`), `translation`, `rotation` `[x, y, z, w]` and `scale`, always written, then `envelope` `{"min", "max"}`, `instance_of` and `"moves": true`, each left out when absent. A level is an object spanning lines, `"level": N` first and then the keys of a version 6 mesh but `name` (vertices, faces, colours, materials, groups, weights, shading, hard_edges, uvs, seams), the full keys as recommended. An empty or an instance has a record and no mesh. Short example (the cube bodies cut to one line each here):

  ```
  {
    "carve": 7,
    "meshes": [
      {
        "name": "Body",
        "vertices": [ ... ],
        "faces": [ ... ],
        "colours": [],
        "groups": [],
        "weights": [],
        "shading": "flat",
        "hard_edges": [],
        "levels": [
          {
            "level": 0,
            "vertices": [ ... ],
            ...
            "hard_edges": []
          }
        ]
      },
      { "name": "Arm", ... }
    ],
    "parts": [
      {"name": "Body", "parent": null, "translation": [0, 0, 0], "rotation": [0, 0, 0, 1], "scale": [1, 1, 1], "envelope": {"min": [-1, -1, -1], "max": [3, 3, 3]}},
      {"name": "Arm", "parent": "Body", "translation": [0.5, 1.25, -0.1], "rotation": [0, 0.70710677, 0, 0.70710677], "scale": [1, 2, 0.5], "moves": true},
      {"name": "Empty", "parent": "Body", "translation": [0, 2, 0], "rotation": [0, 0, 0, 1], "scale": [1, 1, 1]},
      {"name": "Copy", "parent": "Body", "translation": [0, 0, 3], "rotation": [0, 0, 0, 1], "scale": [1, 1, 1], "instance_of": "Arm"}
    ],
    "skeleton": null,
    "clips": []
  }
  ```

- **When it is written:** `CarveFile.HasPartTree(model)`, true when some part is not `IsPlain` (a parent, `HasTransform`, an envelope, `instance_of`, `moves` or a kept level). `HasTransform` compares exactly, so a part set back to the identity is plain again and the model writes version 6 or lower. `CarveFile.PartTreeVersion` is 7; `CarveFile.Version` stays 8 (the targets' version).
- **A part tree beside morph targets is refused, not written.** RV-64 is open, so `CarveFile.VersionProblem(model)` names the meshes with targets and `Write` throws it; `save` returns it as a tool error and writes nothing. This is an interim refusal for the person to replace with their ruling, not a format decision. `open` of either kind of file is unaffected, so such a model can be built in memory but not saved.
- **Reading** takes 1 to 8 (`ReadableVersions`). In version 7 `parts` is required (the writer always writes it). A mesh's levels join that part's record; a mesh with levels and no record would become a top-level part after the records. Level numbers are checked in the reader (`meshes[0].levels[1].level`: negative, or not ascending), and each level is read as a mesh is, so its problems are at `meshes[i].levels[j].<key>`. Everything else about a part (its own fields, a missing or cyclic parent, `instance_of` rules, a name twice) is `Model.Create`'s, at `parts[i].<field>`. `"moves": false` is accepted. `targets` is an unknown key in version 7 and `parts`/`levels` in versions 6 and 8.
- **Pieces.** `Model.PartPiece(name, out problem)` (what `save mesh=` writes): the part's own live mesh and kept levels as `Part.Of(name) with { Levels }`, the materials the mesh and its levels use, and the skeleton and clips when it or a level has weights. It refuses as `Model.Target` does, so an instance or an empty is named as one. A piece with levels is version 7; one without is written exactly as a plain mesh is. `Model.WithPartPieceFrom(source, name, out problem)` (what `open mesh=` does, through the additive `ModelStore.OpenPiece`): each level and then the live mesh are brought in as `WithMeshFrom` brings a mesh (weights by bone name, materials added or matched), and the part keeps its record but its `Levels` are replaced by the file's (none when the piece has none). A new name is a new top-level part. An instance or an empty is refused **in the model or in the file**: the spec's "in either" read as both ends of `open`. `CarveFile.Read(text, folder, mesh)` on a version 7 file reduces the file to that part's mesh and levels with no placement, and the materials it reads include those its levels use. The old `Model.Piece` and `OpenMesh` are unchanged.
- **Descriptions:** `save` and `open` each gained a paragraph on what a part tree keeps and what a piece carries, plus one worked example on the rover (`Wheel_FL` saved and opened back as a piece, `Wheel_FR` refused). The `mesh` argument text of both says the same in one clause. CLAUDE.md's Carve file line now names version 7.
- **Tests (`unit`):** `PartTreeFileTests` (Core, 65 cases) and `PartPieceToolTests` (server, in-process, 9 cases). They cover the rover round trip by content (each part's record and each mesh and level, to the bit) with the same text twice; the small tree `PartTreeSample.Tree()` (linked into the server tests) with the exact `parts` and `levels` text; each trigger alone moving a file to 7; plain models staying at 2; every `samples/*.carve` and the v1 fixture reading below 7 and writing back to the same SHA-256; 33 version 7 refusals, one case each, by element; and pieces on a part, an instance, an empty and a placed part. Three existing tests changed: version 7 is now readable, so `AnotherVersionIsRefused` uses 9, the read-versions message says "6, 7 and 8", and `Version_7_holds_no_targets` replaces `Version_7_is_not_read_by_this_build`.
- **Evidence** in `G:\My Drive\artifacts\carve\M-9\T-92\`: `carve-hashes-before.txt` and `carve-hashes-after.txt` (the 9 committed `.carve` files, identical), and `break-table.tsv`: 15 breaks of the product (always version 7, levels not a trigger, envelope not written, moves or levels not read, level order unchecked, rotation written w first, targets beside a tree allowed, open one part keeping its file place, a piece keeping placement or dropping levels, open dropping placement or allowing an empty, the old save and open paths), each failing at least one of the new tests, at unit tier.

