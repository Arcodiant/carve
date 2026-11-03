---
description: >-
  A fresh agent builds a crate with the edit tools from their descriptions alone, recovering from a planted mistake
---
# T-13 — Edit tools acceptance run

**Status:** In Progress

**Pri:** P1

**Milestone:** M-2

**Batch:** — (standalone)

**Depends on:** T-12

**Verify:** full

**Perf:** skip: Carve declares no heavy suites

## Goal

An agent that has never seen Carve, in an ordinary MCP client with plain `carve`, builds a crate with the edit tools and corrects a mistake from the error text alone. That proves the milestone from a plain launch.

## Scope

- **The run**: in a scratch folder under `artifacts/m2-acceptance/`, start a fresh `claude -p` session whose only MCP server is plain `carve` (the README's `.mcp.json` entry). It has no Carve docs and no project `CLAUDE.md`. The prompt:

  > *Using the carve tools, build a wooden-crate shape: start from a single cube, then use the edit tools, not a long list of coordinates, to give it a raised frame round each side with recessed panels, and a handle bar on two opposite sides. Look at it from at least four sides, including from above, and close up on a handle. Start by extruding face 3 of the cube with an inset of 0.7.*

  The last sentence is the **planted mistake**, from the M-1 retrospective: the inset is too large, and the agent must read the error and recover.
- Keep the transcript, its images and the final `get_mesh` there.
- **Pass means**:
  - the agent recovers from the planted error from its text alone;
  - it uses edit tools after one `define_mesh`;
  - it uses `direction` and `zoom` without guessing;
  - the result is closed with no warnings;
  - its renders show a crate.

  How many calls it took and whether it used `undo` are recorded, not judged.
- Wherever the agent hesitates, guesses wrong or misuses a tool, sharpen that tool's description and repeat in a fresh session. **At most three rounds**; if it still fails, stop and report.
- Update D-1's *Tool shape* question with what the run showed.
- Put notes for the milestone's retrospective in the closing report.

## Notes

- A real run fills context: this task is the run, its transcripts and text edits, split from the code on purpose. It changes tool description text and docs only. A code defect found here is a bug filed under M-2, not a fix in this task.
- A description change must keep every worked-example test (T-11) and the scenarios passing. Run `cairn task T-13 verify`.
- **Open rules: stop and ask.** A change to a tool's arguments or behaviour, and any prompt change beyond the planted mistake.
- The person's look at the frames is in T-12; this one is the agent's.
- Leave the worktree before `finish`.
