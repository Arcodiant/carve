---
description: >-
  Fresh agents give a mesh dials from the tool descriptions alone, across two sessions, and export it for a game
---
# T-107 — Morph targets acceptance run

**Status:** In Progress

**Pri:** P2

**Milestone:** M-21

**Batch:** — (standalone)

**Depends on:** T-106

## Goal

Agents who have never seen Carve's docs give a model dials and ship it, from the tool descriptions alone. This
proves the descriptions teach the job.

## Scope

- Two rounds at most, each two `claude -p` sessions on plain `carve`, `--allowedTools "mcp__carve__*"` and
  `--tools ""` from round 1 (M-3's lessons). Name these run conditions in the stop list below.
  - **Session 1:** build an original limb or a simple creature part as a quad cage, then make a second cage file
    of the same topology with other sizes. Give the first mesh two dials, one from positions and one from the
    file. Save.
  - **Session 2:** open, subdivide, check, see it at weights, export.
- **Plant:** the brief's second file has one face wound the other way, a topology mismatch the error names. The
  agent can fix it without guessing the person's intent (M-4 and M-5 lessons). Recovery is judged by the error
  being understood and the file fixed.
- Evidence: transcripts, the `.carve` and `.glb`, and frames, in `~\artifacts\carve\M-21\T-107\`.
  The frames go to the person.
- A description changed after a round is listed with its reason.

## Notes

- **Goes back to the person, stop and ask:** any change of run condition (flags, tools, prompt) from what is
  written here; accepting a round in which the plant did not fire.
- Sonnet.

### Hand-off

Written by Claude Sonnet 5.5. QUESTION, one round run (1 of 2 at most), evidence in `G:\My Drive\artifacts\carve\M-21\T-107\round-1\` (transcripts, summaries, prompts, `.mcp.json`, `limb.carve`, `limb-b.carve`, `limb.glb`, 8 frames).

**Run.** Scratch folder `%TEMP%\carve-m21-acceptance\round-1\` (outside every Carve CLAUDE.md), Release publish of this worktree, `.mcp.json` of plain `carve`, `claude -p --model claude-opus-5-5 --mcp-config .mcp.json --strict-mcp-config --allowedTools "mcp__carve__*" --tools ""`, prompt on stdin (M-3/M-4 conditions). Inner sessions ran on Opus 5.5 (init event), as T-49 did; `--model` was given explicitly. Notes say only "Sonnet."; I read that as the implementer (as the milestone's "Sonnet on the rest") and kept T-49's Opus for the sessions.

**Brief.** The layout is fixed in the prompt (tube of 5 rings x 6, index 6r+k, faces listed) so a second file can match; the second file is written by the agent, and the brief says "our exporter lists face 7 as [13,14,8,7]" (the plant: reversed [7,8,14,13]). Sizes, shape and bend are the agent's.

**Round 1.** Session 1: 13 calls. `define_mesh` of the (planted) second file warned "two faces cross this edge in the same direction"; `define_target` from the file failed: `faces[7][0]: vertex 7 here, 13 in the other mesh`. The agent understood it (face 7 wound the other way), did not fix `limb-b.carve`, and defined Slim from that file's vertex positions with `positions`. Saved `limb.carve` with Girth (18 vertices) and Slim (30). Session 2: 13 calls: open, subdivide, set_shading, check at weights, 3 renders, export, 2 `render_glb`; `limb.glb` has `targetNames ["Girth","Slim"]`; `render_glb` frames are byte-identical to `render` frames. The export result does not mention the targets (the agent said so and checked with `render_glb`). The person approved the 8 frames.

**Question (rule the spec leaves open).** The Scope judges recovery by "the error understood and the file fixed". Here the error was understood and the file not fixed: the agent routed round it with the same numbers through `positions`. Options: (a) accept round 1 as recovery (M-4's ruling: understood and refused to guess counts); (b) run round 2 unchanged; (c) round 2 with the prompt asking for the file to be fixed (a change of run condition). Recommendation (a): the error text led to the right diagnosis, the dial is identical in effect, and no description is at fault. Also for the person: `export`'s result names no targets; a one-line addition to the result would be a code change (T-104's area), not made.

**Left.** The ruling; `### As built`; `verify`, `cairn lint`, `finish`. No description changed; no engine need.
