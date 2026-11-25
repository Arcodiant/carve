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

### As built

Built by two agents, both on Claude Opus 5.5 (`claude-opus-5-5`): the first built the fix and handed off at its call
checkpoint; the second fixed the failing check, tightened the guidance, measured what was left and finished.

**Built.** `get_mesh` takes `include` (a list of the answer's own keys; `closed`, `bounds`, `warnings`, `parent`, `world`, and
`target`/`level` when asked, are always answered; `include: []` answers only those) and `faces` (face ids or a group's name;
faces, uvs, colours, groups and face_materials narrow to those faces, dropping a group or colour left empty; vertices and
`target.positions` to the vertices those faces use; edges to those with a chosen face, each still listing every face on it;
seams to the chosen edges). Refusals: an unknown section lists every section; an unknown group lists the mesh's groups; a bad
id uses `FaceArguments.CheckIds`; an empty list and a non-list/non-string are refused. Narrowing is in `GetMeshTool.Run` (server):
it is presentation of a query answer, no edit, so nothing went into the Core. The description leads with reading in parts and
the wrench example (`GetMeshTool.WrenchGroups`, `WrenchSteel`); the cube and target examples and the "Vertex and face ids..."
sentence moved word for word to `GetMeshTool.Reference`, the new `help` entry. The graph-slot sentences stay in the description.
`PartToolsListTests.Before` retaken (now 16999BFC...); only `GetMeshTool.cs` (description and schema) and `HelpTool.cs` (help
is in `NewTools`, outside the digest) changed in `src/`. Verify line names `server-regression` too (`HelpThroughProcessTests`,
`ToolFamiliesThroughProcessTests` read the tool list and `get_mesh`).

**Settled, and why.** Always answered: `closed`, `bounds`, `warnings`, `parent`, `world` (a few hundred characters, and what
tells an agent where the part is and whether it is sound), with `target` and `level` when asked, since asking is the include.
Vertices and edges follow the chosen faces (the point of `faces` is a smaller answer); an edge keeps every face on it, so a
neighbour across the boundary is still visible. Narrowing lives in the server, as presentation of a query answer, not the Core.
The guidance line says to "ask a few of one group at a time", and the worked example asks a group with two sections.

**The default held.** `GetMeshPartsTests` holds the default answer by SHA-256 taken on c518cce before the change (wrench,
barrel-textured, gardener `{}` and `Body`, rover `Cab` and `Cab` level 0). The first `verify` failed it on barrel-textured
alone: a texture's path in the answer is absolute, so the answer names the checkout, and the suite runs in cairn's own copy.
The test now swaps the running checkout's root for the one the digests were taken in (`D:epos\carveranchesug\B-10`,
as the test host spelled it). That root was found, not assumed: the answer from a scratch build of this branch, with that root
put in, hashes to the digest taken before (CC6E5E26...), so the bytes are as before; the digests were not retaken.

**Served description and the digest.** `get_mesh`'s description as served by `tools/list` is 3,682 characters of the 3,686
allowed (3,676 before B-10). The served `tools/list` of this branch against a scratch build of `origin/milestone/M-29` differs
in three fields only: `get_mesh`'s description, `get_mesh`'s input schema, and `help`'s description (it names `get_mesh`
among its entries; help is outside the digest).

**Sizes on the wrench, characters** (whole answer 52,990): uvs 16,312; faces 13,827; edges 11,612; vertices 8,882; seams 2,139;
groups 1,085; colours 189; the always-answered keys alone 176. Group faces+vertices: all 22,533, steel 10,745, dark 8,128,
rubber 2,912, red 1,229, black 840. Every section alone and every group's faces and vertices is under 25,000 (the test says
which is largest). **Over 25,000 when narrowed:** `{"faces":"all","include":["faces","uvs"]}` 29,963 and `{"faces":"steel"}`
with every section 25,237. Neither is a single section; the description and README say to ask a few sections of one group at
a time, and README says these two can still pass the limit.

**Other tools on the wrench** (measured through a scratch build's `tools/list`/`tools/call`, not fixed): `get_parts {}` 360,
`check {}` 475, `select` of every face 417, of every vertex 743, of every edge 2,717, `get_clips {}` 12; `get_weights` and
`get_skeleton` refuse (the wrench has no skeleton). None is over 25,000.

**README.** "Asking the mesh" gains a line on `include` and `faces`, the wrench example, to ask a few sections of one group at a
time and which two asks still pass the limit, that with neither the answer is as before, and `help {"tool":"get_mesh"}`.

**Checks.** `server-unit` and `server-regression` through `cairn bug B-10 verify`. The first verify's `server-regression`
failed three extension-loading `LoaderTests` ("carve still running after 00:00:05", log
`B-10-server-regression-20261124-224717-255144.log`, a 14-minute run); B-10 does not touch extension loading, and they passed on
the rerun in the final verify, so the first is recorded as a flake.

**Not checked.** T-168's round 2 (a fresh agent reading the wrench without a refused call) is T-168's to run. Other samples'
sizes when narrowed were not measured.
