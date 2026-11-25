---
description: >-
  get_mesh takes include and faces to answer part of a mesh, so a model like the wrench can be read by an agent whose client limits a tool result
---
# B-10 — get_mesh on a large mesh answers more than an agent can read

**Status:** In Progress

**Severity:** Major

**Milestone:** M-29

**Verify:** server-unit

## Symptom

Found by T-168's round 1 (2026-11-24). A fresh agent with no file tool called `get_mesh {}` on the wrench
(`samples/wrench.carve`, 120 faces). The answer was 53,645 characters on one line. Claude Code refused it as over its
limit on a tool result, saved it to a file and told the agent to read the file; the agent tried `Bash`, which the run
does not allow, and was refused. It got on by selecting faces by position instead. Every fresh agent that calls
`get_mesh` on a model of this size meets the same wall.

Sizes by section, compact JSON characters: uvs 16,129; faces 13,642; edges 11,427; vertices 8,694; seams 1,954;
groups 899; the rest under 500 each.

## Cause

`get_mesh` takes `mesh`, `target` and `level` only, and none makes the answer smaller. Its description says "The
output is never trimmed". No description can steer an agent to a smaller answer.

## Fix

**Decided by the person (2026-11-24),** asked with the sizes above: `get_mesh` gains two arguments that narrow the
answer.

- `include`: a list of sections to answer (`vertices`, `faces`, `edges`, `uvs`, `seams`, `groups`, `materials` and the
  rest, by the names the answer already uses). A section not named is left out. What is never left out, and what an
  unknown name is refused with (the list of sections), is settled in the fix and said in the description.
- `faces`: face ids or a group's name. The face-shaped sections (faces, their uvs, and what else is per face) answer
  for those faces only; how vertices and edges follow (those of the chosen faces, or all) is settled in the fix and
  said in the description.
- **With neither argument the answer is byte for byte as today,** so every scenario, test and frame is unchanged.
- The description leads with how to read a large mesh in parts, and gives a worked example on the wrench that is run
  as written. `get_mesh`'s description sits at 3,676 of the 3,686 the length test allows (T-207), so text moves into a
  `help` entry for it.
- `README.md` says it.

Declined: a summary by default past a size (it changes the default for every caller of a large mesh), and filing it
off-milestone with round 2 run on the product as it is.

Not in scope: a size limit of carve's own, paging, or the other tools' answers. If another tool's answer on the wrench
is also past the client's limit, say which in `### As built`; do not fix it here.

## Verified by

- Unit tests of `include` and `faces`, their refusals, and the unchanged default (the tools/list digest retaken for
  `get_mesh`'s schema and description alone).
- On the wrench: each section asked for alone, and the faces of each group, answers under 25,000 characters; the test
  says what the largest is.
- T-168's round 2, where a fresh agent reads the wrench without a refused call.
