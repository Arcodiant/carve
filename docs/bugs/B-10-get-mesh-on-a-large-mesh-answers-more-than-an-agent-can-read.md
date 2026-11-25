---
description: >-
  get_mesh takes include and faces to answer part of a mesh, so a model like the wrench can be read by an agent whose client limits a tool result
---
# B-10 — get_mesh on a large mesh answers more than an agent can read

**Status:** In Progress

**Severity:** Major

**Milestone:** M-29

**Verify:** server-unit, server-regression

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

### Hand-off

Written at the call checkpoint by the first agent (Claude Opus 5.5, `claude-opus-5-5`). Code and tests are committed; `verify` is not yet clean.

**Done.** `get_mesh` takes `include` (a list of the answer's own keys; `closed`, `bounds`, `warnings`, `parent`, `world`, and
`target`/`level` when asked, are always answered; `include: []` answers only those) and `faces` (face ids or a group's name;
faces, uvs, colours, groups and face_materials narrow to those faces, dropping a group or colour left empty; vertices and
`target.positions` to the vertices those faces use; edges to those with a chosen face, each still listing every face on it;
seams to the chosen edges). Refusals: an unknown section lists every section; an unknown group lists the mesh's groups; a bad
id uses `FaceArguments.CheckIds`; an empty list and a non-list/non-string are refused. Narrowing is in `GetMeshTool.Run` (server):
it is presentation of a query answer, no edit, so nothing went into the Core. The description leads with reading in parts and
the wrench example (`GetMeshTool.WrenchGroups`, `WrenchSteel`); the cube and target examples and the "Vertex and face ids..."
sentence moved word for word to `GetMeshTool.Reference`, the new `help` entry. The graph-slot sentences stay in the description.
`PartToolsListTests.Before` retaken once (5E37EB7E...); only `GetMeshTool.cs` (description and schema) and `HelpTool.cs` (help
is in `NewTools`, outside the digest) changed in `src/`. README's "Asking the mesh" gains a line. Verify line now names
`server-regression` too (`HelpThroughProcessTests`, `ToolFamiliesThroughProcessTests` read the tool list and `get_mesh`).

**Checked** (with `dotnet test --filter` on the touched classes, for measuring only): `GetMeshPartsTests` (new: default answer
held by SHA-256 taken on c518cce before the change for wrench, barrel-textured, gardener `{}` (a refusal) and `Body`, rover
`Cab` and `Cab` level 0; the example runs as written; every section alone and every group's faces+vertices under 25,000),
`HelpToolTests`, `EditToolDefinitionTests`, `TargetToolTests`, `PartToolsListTests` all passed.

**Sizes on the wrench, characters** (whole answer 52,990): uvs 16,312; faces 13,827; edges 11,612; vertices 8,882; seams 2,139;
groups 1,085; colours 189; the always-answered keys alone 176. Group faces+vertices: all 22,533, steel 10,745, dark 8,128,
rubber 2,912, red 1,229, black 840. Over 25,000 when narrowed: `{"faces":"all","include":["faces","uvs"]}` 29,963 and
`{"faces":"steel"}` with every section 25,237 (neither is recommended by the description). The served description's length
is under 3,686 (HelpToolTests passes; the exact figure is written by the example test's output, not yet read).

**Left.**
1. The first `cairn bug B-10 verify` failed on: clean-tree (it ran before the commit); server-unit (failure not yet read; the
   log is under `%TEMP%\cairn-test\`, the listing hung, look by name `B-10-server-unit-*`); server-regression with three
   `LoaderTests` (extension loading, `carve still running after 00:00:05`) - not touched by B-10, likely load on a 14-minute
   run; re-run before judging.
2. Read the description's served length and state it; measure the other tools' answers on the wrench (`get_weights`,
   `check`, `select`) and name any past 25,000 in `### As built` (not fixed).
3. `### As built`, `cairn lint`, `cairn bug B-10 finish`.
