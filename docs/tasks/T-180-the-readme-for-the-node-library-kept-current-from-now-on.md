---
description: >-
  The README's patina section describes M-28's node library, and CLAUDE.md makes keeping the README current part of every change, so a reader of the README sees what the product does now
---
# T-180 — The README for the node library, kept current from now on

**Status:** Open

**Pri:** P1

**Milestone:** M-28

**Batch:** — (standalone)

**Depends on:** — (none)

**Verify:** patina-unit

## Goal

A person reading `README.md` learns what patina does after M-28, and from now on every change that alters what a user
or an agent can reach updates the README in the same task, as a rule the project's `CLAUDE.md` states and every
member's `### As built` accounts for.

## Scope

- **The README's `## patina: texture graphs` section** brought up to M-28, from the code and the members' As built,
  not from the plan:
  - the nodes (worley, fbm, warp, tiles, stripes, shape, blur, gradient_map, transform, distance, input, graph), and the
    `nodes` tool that describes them;
  - the outputs: metallic packed in `roughness.png`'s blue channel, height, named outputs;
  - exposed parameters and overrides on `swatch`, `export` and `check`; graphs used as nodes, with paths relative to
    the using file;
  - `check` and its rules, in a line each, with the `files` roles (`metallic_roughness` for the packed image);
  - `swatch`'s views (sheet, flat, tiled, sphere, cube) and `tiling`;
  - the samples in `samples/graphs/` (cobblestones, bricks and its grey variant, fabric, painted metal, floor tiles,
    grime, old wall) and a short worked example an agent or person can follow;
  - what is slow or bounded: large sizes at 8192, the 2 GB memory bound, and blur and distance missing 10 s (T-159).
- Correct anything else in the README that M-28 made untrue. Read the rest of it for that, and leave carve's sections
  alone unless M-28 changed them; note in As built anything outside M-28 that looks stale (M-24's bake, for example),
  without editing it.
- **The rule, in CLAUDE.md's `## Working rules`:** a task or bug that changes what a user or agent can reach (a tool, an
  argument, a node, an output, a sample, a server, a run condition) updates `README.md` in the same task, and its
  `### As built` says what changed in the README, or that nothing needed to. Add the same to the planning rules' wording
  where it fits, so a plan's tasks carry it.
- If a suite can reasonably take `README.md` as an input so that `verify` stops listing it as uncovered, propose it in
  As built rather than adding it; it would make every README edit re-run that suite.

## Notes

- **Asked for (2026-11-22, by the person):** "make sure we're updating the Readme, and documenting those updates as
  part of the regular dev process".
- Runs beside T-179, which also edits CLAUDE.md's rows. Keep both sides on a rebase.
- The README is what a person reads; tool descriptions are what an agent reads. Keep the README short and point at the
  `nodes` tool and the descriptions for detail rather than copying them.
- Opus 5.5: Sonnet is under its weekly usage limit until 2026-11-23.
