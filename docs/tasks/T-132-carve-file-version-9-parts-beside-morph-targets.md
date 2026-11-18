---
description: >-
  The Carve file holds a part tree beside morph targets as version 9, so save no longer refuses a model with both
---
# T-132 — Carve file version 9: parts beside morph targets

**Status:** In Progress

**Pri:** P1

**Milestone:** M-9

**Batch:** — (standalone)

**Depends on:** T-92

**Verify:** core-unit, server-unit

**Perf:** skip: Carve declares no heavy suites

## Goal

A model that has both a part tree and morph targets saves and opens bit-exact. The person ruled this on 2026-11-18, answering the question RV-64 left for M-9's planning: version 9, built in M-9. Until now, version 7 (T-92) has held parts but no `targets`, version 8 (T-101) has held targets but no `parts` or `levels`, and T-92 landed a stopgap that refuses `save` of a model with both.

## Scope

- **`CarveFile` version 9** is version 8 (targets, T-101) plus version 7's additions (T-92): the `parts` array after `meshes`, and `levels` on a mesh. It uses the same keys, key order and number formatting as those two writers. Version 9 adds no key of its own.
- **The lowest version that holds the model:**
  - Version 9 only when the model needs both: a part tree by T-92's rule (`CarveFile.HasPartTree`), and morph targets.
  - Every other model writes exactly what it writes today, 7 or 8 or lower.
  - Every committed `.carve` stays byte-identical.
- **Remove T-92's stopgap**, the refusal in `CarveFile.VersionProblem`, and its tests. Put a round trip in their place.
- **Reading** takes version 9 as well. Every problem is named by where it is, as in T-92 and T-101. Reading refuses whatever `Model.Create` refuses.
- **Pieces:** `save mesh=` / `open mesh=` of a part with targets carries its targets, as a version 8 piece does today, and its levels.
- **Descriptions:** wherever `save`/`open` or CLAUDE.md name the versions, add 9. In particular, CLAUDE.md's Carve file line currently says "a part tree beside morph targets is refused until RV-64 is ruled"; replace that clause. A wording change searches every text that says the same thing.
- **Tests (`unit`):**
  - a part-tree model with targets on a part round-trips by content and writes the same text twice;
  - it writes version 9;
  - removing either the parts or the targets drops it to 8 or 7;
  - every version 9 refusal, one test each;
  - all samples are byte-identical when written again.

## Notes

- **Decides a format others build on:** Opus.
- **File ownership:**
  - This task owns `CarveFile.cs`, its tests, and the version lines in `SaveTool.cs`, `OpenTool.cs` and CLAUDE.md.
  - T-93 (tools) and T-95 (export) may be running beside it. Don't touch the tool registration, the part tools or export.
- **Open rules: stop and ask.**
  - Whether a kept level of a mesh with targets snapshots its targets too. The recommendation is yes: a level is a full mesh snapshot, as ruled.
  - Any existing file whose bytes would change.
  - Anything the rulings don't settle.
- **Write down for dependents (As built):** the version 9 layout, with one short example.
- **Carried:**
  - every committed `.carve` byte-identical, by hash before and after;
  - each new check shown failing on a broken product, as a table;
  - evidence at the absolute path the dispatch names;
  - `finish` run inside the worktree, its output read whole, and its `Merge task/T-132` line given;
  - report a `verify` that waits more than a few minutes.

### As built

Opus 5.5, 2026-11-18, one agent.

- **The open rule, asked and ruled.** Whether a kept level of a mesh with targets keeps its targets: the person picked yes (the recommendation) through the review window on 2026-11-18, before any of this was built. `keep_level` already snapshots the live mesh's targets in memory (`Model.WithLevel`), so the file now keeps what the model holds.
- **The layout.** Version 9 is version 8 plus version 7's additions, with no key of its own: `parts` after `meshes` (before `skeleton`), and `levels` as the last key of a mesh, after `targets` when it has any. A level holds the keys of a version 8 mesh but `name`, so its own `targets` are its last key. Every key, order and number as the version 7 and 8 writers write them. Short example (cube bodies cut):

  ```
  {
    "carve": 9,
    "meshes": [
      {
        "name": "Body",
        "vertices": [ ... ],
        ...
        "hard_edges": [],
        "targets": [
          {"name": "Bulge", "deltas": {"0": [0, 0.25, 0], "6": [0.1, 0, -0.3]}}
        ],
        "levels": [
          { "level": 0, "vertices": [ ... ], ..., "hard_edges": [] },
          {
            "level": 1,
            "vertices": [ ... ],
            ...
            "hard_edges": [],
            "targets": [
              {"name": "Bulge", "deltas": {"0": [0, 0.25, 0], "6": [0.1, 0, -0.3]}}
            ]
          }
        ]
      },
      { "name": "Arm", ..., "targets": [ {"name": "Wave", ...} ] }
    ],
    "parts": [ ...as version 7... ],
    "skeleton": null,
    "clips": []
  }
  ```

- **When it is written:** `CarveFile.HasPartTree(model)` and a target on some live mesh or some kept level (a target on a level alone is enough). Otherwise exactly as before: 7 for a tree with no targets, 8 for targets with no tree, lower still for the rest. `CarveFile.PartsAndTargetsVersion` is 9; `Version` stays 8 and `PartTreeVersion` 7. The 9 committed `.carve` files are byte-identical (SHA-256 before and after).
- **T-92's stopgap is gone:** `CarveFile.VersionProblem` and its call in `save` are removed, and its test replaced by the round trips below.
- **Reading** takes 1 to 9. In version 9, as in 7, `parts` is required; `levels` is not a key of a version 8 mesh, `targets` not a key of a version 7 mesh or level. Level targets are read as a mesh's are, each problem at `meshes[i].levels[j].targets[k]...`. The rest is `Model.Create`'s, as in version 7.
- **Pieces.** No code change was needed: `Model.PartPiece` keeps the mesh's and levels' targets, so `save mesh=` of a part with targets and levels writes version 9, and one with targets and no levels writes version 8 as a plain mesh does. `open mesh=` of a version 9 file, or of one part of it, brings in the targets and levels and keeps the part's place.
- **Descriptions:** `save` replaces the "refused for now" sentence with the version 9 one; `open` names version 9; CLAUDE.md's Carve file line replaces the RV-64 clause. A search for "RV-64", "refused for now" and "no file version holds" found no other text saying the same.
- **Tests (`unit`):** `PartsBesideTargetsFileTests` (Core, 51 cases): the round trip by content (records, vertices, faces and every mesh's and level's target deltas, to the bit) with the same text twice; version 9 and its exact `targets` then `levels` text; a level's targets alone giving 9; dropping the targets gives the very bytes of T-92's tree (7), dropping the tree gives 8; 10 version 9 refusals, one case each; T-92's 33 version 7 refusals run again on a version 9 file; pieces both ways. `PartsBesideTargetsToolTests` (server, in-process, 2 cases): `save`/`open` of the whole model byte for byte, and `save mesh=`/`open mesh=` of a part with targets and levels. `PartTreeSample` gained `Bulge` and `TargetedTree`, and `PartTreeFileTests.Mutate` is now `internal`. The samples' byte-for-byte guards are T-92's and T-101's, unchanged and passing. Changed: version 10 is now the unread version in `CarveFileTests` and `SaveOpenToolTests`, and the read-versions message says "7, 8 and 9".
- **Evidence** in `G:\My Drive\artifacts\carve\M-9\T-132\`: `carve-hashes-before.txt` and `carve-hashes-after.txt` (identical), the two option files the person was asked on, and `break-table.tsv`: 10 breaks of the product (version 9 never written, or written for every tree; a level's targets not a trigger; 9 not read; a level holding no targets on read; parts or levels not written, or not read, in 9; parts not required; a version 9 mesh refusing `levels`), each failing 1 to 11 of the new tests.
- **Not checked:** no real run of the server over MCP, and no frames, since this is a file format change and the in-process tool tests cover it.
