---
description: >-
  Fresh agents cut a wall into a gable with a see-through window from the descriptions alone, recovering from a knife point off the surface
---
# T-70 — Cut acceptance run

**Status:** In Progress

**Pri:** P1

**Milestone:** M-5

**Batch:** — (standalone)

**Depends on:** T-69

**Verify:** full

**Perf:** skip: Carve declares no heavy suites

## Goal

A fresh agent with plain `carve` and no Carve docs cuts a wall into a gable with a door and a window you can see through, from the tool descriptions alone. It recovers from a knife point off the surface by reading the error text. What it hesitates on sharpens the descriptions.

## Scope

**The run.** The run conditions are T-28's, accepted by the person on 2026-11-07:

- a scratch folder `%TEMP%\carve-m5-acceptance\round-N\`, outside every Carve `CLAUDE.md`, holding an `.mcp.json` of plain `carve` only (the README's entry), with `carve` built in Release from this worktree;
- one fresh `claude -p` session with `--strict-mcp-config`, `--mcp-config .mcp.json` and `--allowedTools "mcp__carve__*"`, and, ruled 2026-11-12, `--output-format stream-json --verbose` for the transcript; no other flag;
- the exact prompt on stdin, no Carve docs, started in that folder, on a fresh carve process.

The prompt:

> *With the carve tools, build a stone slab 3 m wide, 4 m tall and 0.3 m thick, standing on y = 0 and centred on x = 0 and z = 0. Then cut a gable top into it, so that its apex is at y = 3.4 above the centre and its slopes reach the slab's ends at y = 2.6. Put a window right through the wall, an opening you can see through, with corners at (0.3, 0.8), (1.1, 0.8), (1.1, 1.5), (0.7, 1.75) and (0.3, 1.5) in x and y; on the front face, z = 0.17. Cut a door outline into the front face from the floor, x from -1.1 to -0.4 and 1.6 m high, and colour the door brown. Look at it from the front and the back, and through the window. Finish with check and get_mesh, and save it as wall.carve.*

*Rulings after round 1 (the person, 2026-11-12):* (1) the two stream-json flags above are part of the run conditions; (2) the prompt's first two sentences are reworded as shown, so the gable is cut from a slab and "4 m tall" no longer clashes with the apex (everything after is unchanged, the plant included); (3) round 2 runs fresh with the plant as is, and if the agent works out z = 0.15 before any call and says why, that is recovery by understanding, as in M-4's acceptance.

**The planted mistake.** The window's front z is given as 0.17, but the front face is at z = 0.15. A `knife` there is refused: the error names the point, its distance (0.02) and the nearest surface point (z = 0.15). The agent can correct it from the text alone, with no guess at the person's intent: the nearest point is plainly the face meant. No description can foresee a wrong coordinate in the prompt.

**Pass means:**

- the agent meets the off-surface error and corrects the window onto z = 0.15 using the error text, or declines and says why (recorded either way; correcting is the pass);
- the result is built with cuts: `plane_cut` or `knife` for the gable, `knife` for the door and window, and `delete` with `bridge`, or another tool sequence, for the opening (record which);
- the final `check` is closed with no warnings, and the final `get_mesh` is kept;
- a render through the window shows background where the opening is;
- the door's faces are brown in `get_mesh`;
- `wall.carve` is saved.

How many calls it took, which tools it chose, and whether it used `undo`, `merge_faces` or `loop_cut` are recorded, not judged.

**Rounds.** Wherever the agent hesitates, guesses wrong or misuses a tool, sharpen that tool's description and run again fresh. Watch above all the knife's surface rule, which piece keeps an id, tombstones in `get_mesh`, the bridge's loops and `keep`/`fill`. **At most three rounds.** If it still fails, stop and report.

**Keep** in the round folder, and copy to the milestone worktree's `artifacts/m5-acceptance/round-N/`: the transcript and its images, the final `check` and `get_mesh`, and `wall.carve`.

**Afterwards:**

- Update D-1:
  - close the *Boolean and heavier operations* question with what M-5 decided (plane, loop and knife cuts, merge, delete and bridge; no boolean);
  - the *Tool shape* question with what the run showed;
  - the *Polygon meshes* decision with faces with holes and tombstones, if T-63 and T-64 have not.
- README: a short section *Cutting*: one call each for `plane_cut`, `loop_cut`, `knife`, `merge_faces`, `delete` and `bridge`. It is the README's only edit in M-5.
- Put notes for the milestone's retrospective in the closing report.
- Show the person the final round's frames (`mcp__waystone__review`, kind `approve`).

## Notes

- **Decided at planning:**
  - a fresh-agent acceptance run closes the milestone, as in M-1 to M-4;
  - the plant is a coordinate the agent can correct from the error alone (M-4's retrospective: plant something fixable without guessing intent);
  - the prompt asks for a final `get_mesh` (M-2's retrospective).
- A real run fills context. This task is the run, its transcript and text edits, split from the code on purpose. It changes description text, the README and docs only. A code defect found here is a bug filed under M-5, not a fix here.
- A description change keeps every worked-example test and every scenario passing. Run `cairn task T-70 verify`.
- **Open rules: stop and ask.**
  - A change to a tool's arguments or behaviour.
  - Any change to the prompt or the run conditions.
  - A second plant.
  - A description that tells the agent to guess or to avoid the plant (M-4's round 3).
- Sonnet is enough.
- Nothing has sound or motion, so there is no listening or real-time pass.
- Run `finish` from inside the worktree and leave the folder.

### As built

Finished by an agent on **Sonnet 5.5** (claude-sonnet-5-5); round 1 was run by an earlier Sonnet 5.5 agent. Each round's session model is the `claude -p` default, **claude-fable-5-2**. No description, tool or code changed: no round showed hesitation, a wrong guess or a misused tool. Evidence is in `%TEMP%\carve-m5-acceptanceound-N\` and `artifacts/m5-acceptance/round-N/` of the milestone worktree (transcript, images, final `check` and `get_mesh`, `wall.carve`).

**Rulings (the person, 2026-11-12), written into Scope:** the stream-json flags are accepted; the prompt is reworded (slab, then gable cut into it); round 2 runs fresh with the plant as is, and an agent that works out z = 0.15 and says why counts as recovery by understanding.

**Round 1** (original prompt, session claude-fable-5-2). 11 carve calls: `define_mesh` (gable prism built directly), `knife` x3, `delete`, `bridge`, `set_colour` on the door, `render`, `check`, `get_mesh`, `save`. No `undo`, `merge_faces`, `loop_cut` or `plane_cut`. Final `check` ok, one closed shell, no warnings; door face brown; opening is background in the render (zoomed tile, pixel (48,53,56) as in the tile corner); `wall.carve` saved. The plant was not met (z = 0.15 used, said so), and the prompt's "4 m tall" clashed with the apex, which led to ruling 2.

**Round 2** (reworded prompt, plant as is, session claude-fable-5-2). 14 carve calls after one ToolSearch: `define_mesh` (8-vertex slab), `plane_cut` x2 (normals (0.8, 1.5, 0) and (-0.8, 1.5, 0), keep below), `knife` x2 (closed window outline on z = 0.15 and z = -0.15), `delete` [16, 17], `bridge` [[16..20],[21..25]], `knife` (open door path), `select` (the door face by `where`), `set_colour` #6B4423, `render` (six tiles: front, back, +z, -z and two zoomed oblique views through the window), `check`, `get_mesh`, `save`. No `undo`, `merge_faces`, `loop_cut`.
- Plant: the agent never sent z = 0.17; it cut at 0.15 and said at the end that a 0.3 m slab centred on z = 0 has its front at z = 0.15. Recorded as recovery by understanding (ruling 3). The off-surface error was not seen, only exercised by scenarios.
- Gable by `plane_cut`; the result is 3.4 m tall and the agent said so (slab cut down from 4 m), with no clash.
- Final `check`: ok, no problems, no warnings, one closed shell, 24 vertices, 13 faces, size 3 x 3.4 x 0.3. `get_mesh` kept (tombstones as null); door face 23 normal +z is #6B4423, brown. `wall.carve` saved.
- Render: in the zoomed oblique tiles (5 and 6) the tile centre, inside the window, is (48,53,56), the same as the corner of tile 3 where nothing but background shows; the wall is light grey (about 219,219,216 to 225,225,222). So the opening shows background, and the front and back views show the same hole with its reveal faces and the brown door on the front.

**Round 3** not run: nothing in round 2 hesitated, guessed wrong or misused a tool, so there was no description to sharpen.

**Afterwards:** D-1 updated (*Boolean and heavier operations* closed: plane, loop and knife cuts, merge, delete, bridge, no boolean; *Tool shape* with this run; *Polygon meshes* already carries T-64's tombstones). README *Cutting* section added.

**Notes for the milestone's retrospective.**
- The plant was pre-solved in both rounds by an agent that did the arithmetic first (T-28 round 3 and M-4 had the same shape); a coordinate derivable from the prompt's own numbers does not trigger the error. A plant would have to be a figure the agent cannot check against the prompt.
- A prompt with two figures that conflict (4 m tall, apex 3.4) costs a round; read the prompt for internal consistency before the first run.
- Agents reached for `define_mesh` to build the gable in round 1 and for `plane_cut` in round 2; both are fine, and both used `knife`, `delete` and `bridge` for the window with no description hesitation.
- The window recipe (closed `knife` on both faces, `delete` of the two pieces, one `bridge`) was found unaided from the descriptions both times.
- Engine needs: none new.

**Review:** the person approved round 2's frame sheet (waystone review, 2026-11-12).
