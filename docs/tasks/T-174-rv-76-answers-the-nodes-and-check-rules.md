---
description: >-
  Every finding of RV-76's `## Findings: the nodes, check rules and samples`, and the check-tool findings of the server's, is answered
---
# T-174 — RV-76 answers: the nodes and check rules

**Status:** In Progress

**Pri:** P1

**Milestone:** M-28

**Batch:** — (standalone)

**Depends on:** — (none)

**Verify:** patina-unit, scenario-patina-library-2

## Goal

Every finding of RV-76's `## Findings: the nodes, check rules and samples`, and the check-tool findings of the server's, is answered, so that M-28's review round can pass.

## Scope

- All six findings under `## Findings: the nodes, check rules and samples (claude-opus-5-5)`, and three under `## Findings: the patina server`: check on files reading any roughness PNG's blue channel as metallic; check reporting one `stats.size` for files of different sizes; check's untested "not a PNG" error.
- Each fix with a test that fails without it, where a test can hold it.
- Fill each finding's `**Outcome:**` line in RV-76, in this task's worktree.

## Notes

- **Decided (2026-11-21, by the person):**
  - a graph's metallic is exempt from `one-value`, as a PNG's already is (an all-metal or all-non-metal material is legitimate; `metallic-not-binary` still judges its values);
  - in `check`'s `files`, a roughness PNG's blue channel is read as metallic only when the file is given as the packed glTF image, role `metallic_roughness`; role `roughness` means roughness only. Patina's own `roughness.png` exports are named for the packed role in check's description. This replaces T-141's ruling that blue is always metallic;
  - metallic in PNGs is not exempt from `does-not-tile` (the exemption was only ever from `one-value`).
- Owns `src/Patina.Graphs/Checks/`, `CheckTool.cs`, the node files under `src/Patina.Graphs/Nodes/` other than `NodeType.cs`, `BlurNode` and `DistanceNode`'s buffers (T-173), and their tests. check's description must stay at 3,686 characters or under (T-170's test).
- **Answering RV-76:** for each finding listed, fix it, or answer it `skipped: <reason>` or `no change needed: <reason>`, by filling its `**Outcome:**` line in `docs/reviews/RV-76-review-of-m-28.md`, in this task's worktree. Leave the other findings' lines as they are. A fix that would change behaviour beyond the finding, loosen a check, or change a committed output or an approved frame goes to the person first.
- Opus 5.5: Sonnet is under its weekly usage limit until 2026-11-23.

### As built

Run on claude-opus-5-5. Every finding is fixed; no committed sample's bytes changed (the sample tests pass unchanged).

- **does-not-tile on a PNG's metallic:** fixed; the rule judges every output, the metallic read from a packed image's B too.
- **An all-metal graph and one-value:** fixed by the ruling; metallic is exempt from `one-value` from a graph or a PNG, `metallic-not-binary` still judging it.
- **The 99th percentile untested:** fixed in tests; a 256-wide ramp with two outlier steps (dropped) and a third (kept), and three broken copies.
- **warp's Math.Cos and Math.Sin:** fixed; `TransformNode.SinCos`, held by `CallsNoPlatformTranscendental`, a scan of the assembly's maths references.
- **worley's second nearest:** fixed; rings past the 3 x 3 cells are searched while one could hold a point nearer than f2 (to ring 4), held by a search of every cell. Cobblestones and painted-metal are unchanged at their jitters 0.85 and 0.9.
- **stripes' softness:** fixed in the words (node and `nodes` catalogue: the reach each side of the edge, the ramp 2 x softness), so fabric keeps its approved look.
- **check reads any roughness PNG's B as metallic:** fixed by the ruling; role `roughness` is roughness only, role `metallic_roughness` the packed image (G, B), and both at once an error. `CheckSamplesTests` gives `iron-mr.png` the new role.
- **One stats.size for files of different sizes:** fixed; `stats.size` only when every output has one square size, otherwise each output's `width` and `height`.
- **check's "not a PNG" untested:** fixed; the PNG signature is checked first (StbImageSharp reads other formats), tested with text, a BMP header and a cut PNG.
- check's description is 3,686 characters or under (two clauses the schema repeats were cut to fit).
