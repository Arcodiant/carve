---
description: >-
  The README's patina section describes M-28's node library, and CLAUDE.md makes keeping the README current part of every change, so a reader of the README sees what the product does now
---
# T-180 — The README for the node library, kept current from now on

**Status:** Done

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

### As built

Written by Opus 5.5 (claude-opus-5-5), from the code (`src/Patina.Server/*Tool.cs`, `NodeCatalogue.cs`,
`Program.Tools`) and T-155's As built, not from the plan.

**The README.** `## patina: texture graphs` is rewritten and split into short subsections: the tool list (now with
`nodes`, `swatch`, `export`, `check`, and `edit_graph`'s `set_parameter`, `set_parameters`, `remove_parameter`); Nodes
(all nineteen types, grouped as sources, filters and structure, pointing at `nodes {}` for detail); Outputs and export
(the roles, metallic in `roughness.png`'s blue channel, height and named outputs, each file's carve slot); Exposed
parameters and graphs as nodes (overrides on `swatch`, `export` and `check`; a nested `path` relative to the using
file; version 2); swatch's views (sheet, flat, tiled, sphere, cube) and `tiling`; `check`, its two targets
(`files` with `metallic_roughness`) and its five rules a line each; the samples, the grey bricks variant and the fault
graphs; a worked example; and what is slow or bounded (8192 minutes under 2 GB, blur about 2 minutes and distance
about 20 s at 8192 against T-159's 10 s, swatch and check at 2048). The old node list of seven types and the claim that
a faulted graph only cannot be saved were the parts M-28 had made untrue. carve's sections are unchanged.

**The worked example was run as written.** A throwaway stdio MCP client (in the temp folder, not the repo) started the
worktree's own `Patina.Server` (built, `dotnet run --no-build`) in the worktree and called `nodes {"type":"tiles"}`,
then steps 1 to 4: `open` and `get_graph` listed `jitter`, `mortar`, `tint`; `swatch` returned the sheet
(its line names "parameters mortar 0.08, tint #7a7a70", 1544 x 538 px); `check` returned `"ok":true`; `export` wrote the four files,
each byte-equal (`cmp`) to `samples/graphs/bricks/variants/grey/`. Step 5 ran against the worktree's `Carve.Server` in
the same folder: `define_mesh` (the unit cube), `project_uvs` box on faces 1 and 3, the `define_material` as written
(all four textures loaded, 256 x 256) and `set_material` succeeded. The exported `textures/` folder was then deleted.
The published `patina` MCP copy was not used: it predates `nodes` and `check`.

**CLAUDE.md.** Working rules gain "The README is kept current", in the Scope's words. Planning rules' content rule
gains a sentence: the README's account of what a user or an agent can reach is part of that content, and a planned
task that changes it names the `README.md` change in its scope.

**Outside M-28, stale-looking, not edited.** The README never names carve's `bake`, `keep_level`, `define_part` / `get_parts` or
`define_target` / `delete_target`, which carve's server offers (M-24's bake among them); "Build and test" names only carve's two suites, not patina's (`tests.json` lists them).
Carve's `check` after the step-5 calls also warns of the cube's four faces without UVs, as its README says it does.

**Suite proposal (not added).** No test reads `README.md`, so no suite can honestly list it as an input today. A
reasonable one: a `patina-unit` test that pulls the call lines of the README's patina worked example and runs them, as
the tool descriptions' worked examples are run; then `README.md` becomes `patina-unit`'s input. The cost is that every
README edit, carve's sections included, re-runs `patina-unit` (145 s here). Left to the person.
