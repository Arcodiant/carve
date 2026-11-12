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
- one fresh `claude -p` session with `--strict-mcp-config`, `--mcp-config .mcp.json` and `--allowedTools "mcp__carve__*"`, and no other flag;
- the exact prompt on stdin, no Carve docs, started in that folder, on a fresh carve process.

The prompt:

> *With the carve tools, build a stone wall 3 m wide, 4 m tall and 0.3 m thick, standing on y = 0 and centred on x = 0 and z = 0. Give it a gable top whose apex is at y = 3.4 above the centre, with the slopes reaching the wall's ends at y = 2.6. Put a window right through the wall, an opening you can see through, with corners at (0.3, 0.8), (1.1, 0.8), (1.1, 1.5), (0.7, 1.75) and (0.3, 1.5) in x and y; on the front face, z = 0.17. Cut a door outline into the front face from the floor, x from -1.1 to -0.4 and 1.6 m high, and colour the door brown. Look at it from the front and the back, and through the window. Finish with check and get_mesh, and save it as wall.carve.*

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
