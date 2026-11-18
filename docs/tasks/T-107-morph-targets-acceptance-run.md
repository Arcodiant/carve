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
