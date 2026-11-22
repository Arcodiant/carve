---
description: >-
  The README's carve sections and its Build and test list describe what carve's server offers now, so the README is current on both servers when M-28 lands
---
# T-181 — Carve's README sections caught up

**Status:** Done

**Pri:** P1

**Milestone:** M-28

**Batch:** — (standalone)

**Depends on:** T-180

**Verify:** server-unit

## Goal

A person reading `README.md` sees every tool carve's server offers today, and every suite the repo runs, as the new
CLAUDE.md rule (T-180) asks of every change.

## Scope

- **Carve's tools the README never names**, found by T-180: `bake` (M-24), `keep_level`, `define_part` and `get_parts`,
  `define_target` and `delete_target`. Check `Program.Tools` (or carve's tool registration) for any others missing, and
  describe each in the section where it belongs, briefly, pointing at the tool's description for detail. A new section
  where none fits (for example baked maps).
- **`## Build and test`**: the suites in `tests.json` by tier, patina's and the scenario suites included (the three
  patina scenario suites from T-172 among them), or a sentence pointing at `cairn test list` if a full list would go
  stale.
- Anything else in carve's sections that the code makes untrue. Patina's section is T-180's and stays as it is.
- Any worked call the README gains runs as written through carve's tools once; say how in As built.

## Notes

- **Decided (2026-11-22, by the person):** T-180 found carve's README sections stale from before M-28. Asked whether to
  file it or fix it now, the person chose to fix it in M-28.
- Write from the code and the tool descriptions, not from old plans.
- Opus 5.5: Sonnet is under its weekly usage limit until 2026-11-23.

### As built

Written by Opus 5.5 (claude-opus-5-5), from `CarveServer.AddTools` (carve's tool registration), the tools' own
descriptions in `src/Carve.Server/`, `tests.json` and `cairn test list`, not from old plans.

**The README.** `AddTools` registers the six tools T-180 found unnamed and no others the README misses (`render` and
`render_glb` are registered apart and were already there). Each now has a place:

- **`## Parts and levels`** (new, after "Several meshes"): `define_part`, `get_parts`, envelopes and their warnings,
  `keep_level` with `get_mesh` and `render` at a `level`, `render` with `part`, and how `export` keeps nodes only for
  moving parts; on `samples/rover.carve`, which the README never named.
- **`## Baked maps`** (new, after "Materials and textures"): `bake`, its six maps, `surface`, the blocks each map
  needs, size and folder, and bake's own three-step worked example on `samples/wrench.carve`.
- **`## Morph targets`** (new, after "Characters"): `define_target` (positions or `file`), `get_mesh` `target`,
  `delete_target`, `render` and `render_glb` `targets`, and `export`; `samples/arm.carve` and `arm-lean.carve`.
- **`## Build and test`**: every suite in `tests.json` by tier (unit; regression without a GPU; regression on a
  Vulkan device, carve's scenarios and patina's three), with `cairn test list` named for device and time.
- `samples/gatehouse.carve` named under Cutting and `samples/barrel-planks.carve` under Materials, the two other
  samples it never named. Nothing else in carve's sections was found untrue. Patina's section is unchanged.

**Every worked call was run as written.** The T-180 stdio client, copied to `t181_mcp.py` in the temp folder (not
the repo), started the worktree's own `Carve.Server` (built, `--no-build`) in a temp working folder and made, in one
process: `define_mesh` (the unit cube), `define_target` Bulge (4 vertices moved), `render` `{"targets":{"Bulge":1}}`,
`get_mesh` `{"target":"Bulge"}`, `delete_target`; `open` the rover, `get_parts`, `define_part` (the Mast's turn),
`keep_level` `{"level":1}` (every part with geometry now at levels 0 and 1), `render` `{"level":0}` and
`{"part":"Mast"}`; `open` the wrench, the `bake` (masks, base colour and ORM written), `define_material` and
`set_material` `group` `all`. None returned an error; the renders returned images (not opened). The baked textures
were deleted afterward. One claim was corrected by the run: `define_target` `Lean` from `arm-lean.carve` on today's
`arm.carve` is refused (338 vertices against 88; the arm was subdivided after `Lean` was made), so the README says
`arm-lean.carve` is the cage `Lean` was made from, not a file that matches the arm now. The rover's tree was read
from `samples/rover.carve` for the section's description of it.
