---
description: >-
  The Carve file holds a part tree beside morph targets as version 9, so save no longer refuses a model with both
---
# T-132 — Carve file version 9: parts beside morph targets

**Status:** Open

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
