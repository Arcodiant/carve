---
description: >-
  The barrel is seamed, unwrapped and seen in both UV looks, and the crate box-projected, saved, reopened and exported, in suite scenario-uvs
---
# T-61 — Barrel scenario

**Status:** Open

**Pri:** P1

**Milestone:** M-6

**Batch:** — (standalone)

**Depends on:** T-55, T-56, T-58, T-59, T-60

**Verify:** server-regression, scenario-cube, scenario-crate, scenario-shed, scenario-export, scenario-character

**Perf:** skip: Carve declares no heavy suites

## Goal

The whole of M-6, through the real tools on the real `carve`: a barrel seamed and unwrapped, a crate box-projected, both seen in both looks, a fault made and found, and the result saved, reopened and exported.

## Scope

- **`scenarios/Carve.Scenario.Uvs`**, with suite `scenario-uvs` (`regression`, gpu). It joins `Carve.slnx` and `tests.json` (with `samples/barrel.carve` among its inputs), and gets a row in `CLAUDE.md`'s Layout. Once the suite is in `tests.json`, add `scenario-uvs` to this file's `**Verify:**` line (lint refuses it before then).
- **The barrel**, built from a spec class in the scenario (the one source for its calls), as a mesh named `Barrel`:
  - `define_mesh` with T-53's barrel, then `mark_edges` with T-56's seams and `unwrap` with the defaults;
  - `get_mesh` gives three islands, and `check` gives no UV warning;
  - the saved barrel equals `samples/barrel.carve` by content.
- **The crate** as a second mesh `Crate`, built by the same tool calls as `scenario-crate` from one shared source, never a copy. Then `project_uvs` with `box`.
- **Frames:**
  - `checker` of the barrel: `front`, `back`, `-z` (the seam side), `+y` (the top cap), and a 2 x 3 `views` sheet with face labels;
  - `uvs` of the barrel at 256, 512 and 1024, with face labels, and with a side face highlighted (and the same face highlighted in `checker`);
  - `checker` of the crate `front` and `back`, and its `uvs` layout.
- **A fault made and found:**
  - `move` a middle ring outward: the `checker` frame shows the stretched cells, and `unwrap`'s stretch rises. Then `undo`.
  - `extrude` the top cap: the new walls are magenta in `checker`, `check` lists them, and `export` warns. Then `undo`.
- **Save, reopen and export:**
  - `save` both meshes to the run folder, `open` again, `get_mesh` equal, and the checker and layout frames after reopening byte-identical to before;
  - `export` `models/barrel.glb` with no warning.
- **Pixel checks**, worked from the geometry and the UVs `get_mesh` returns, through `CameraPixels` and the checker's cell rule:
  - the white cell and hue order where predicted, and not at their mirror;
  - magenta on the extruded walls;
  - in the layout, the island fills bright, empty space dim, yellow on each seam's UV segments, and the highlight orange.

  Every frame gets a positive check and one that a mirrored camera or a v-flipped texture would fail. Work every check against the geometry before writing it, and stop if one cannot pass: M-4 stopped eleven times on checks that a hand calculation would have caught.
- **Break table:** a texture uploaded with v flipped; corner UVs rotated by one; `unwrap` ignoring seams (refused, so the scenario fails); the layout drawn v-up; a mirrored camera.
- **Look at the frames**, say what was seen, and put them to the person.

## Notes

- Sonnet: everything it drives is built by now. It draws many frames, which is why it is a task of its own.
- No sound, motion or feel: the person's look at the frames is the whole review.
- **Before the first code**, read this Scope against what T-53 to T-60 landed (their As built sections) and stop on any mismatch.
- **Carried from earlier plans:**
  - the byte-identical guard on every existing frame and file of every suite, by hash before and after;
  - `finish` read whole, artifacts by job id (`cairn test artifacts` returns any branch's latest), report a long `verify` wait;
  - evidence, frames and the break table in the milestone worktree's `artifacts/review/T-61/`, not this worktree's.
- **Open rules: stop and ask.** Anything the milestone's *Decided at planning* does not settle, any spec check that cannot pass, and any loosened check.
- Run `finish` from inside the worktree and leave the folder.
