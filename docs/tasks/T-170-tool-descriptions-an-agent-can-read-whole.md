---
description: >-
  patina's tool descriptions reach an agent whole, with the node catalogue no longer crammed into define_graph's, so a fresh agent learns the node types by reading rather than by sending wrong graphs
---
# T-170 — Tool descriptions an agent can read whole

**Status:** In Progress

**Pri:** P1

**Milestone:** M-28

**Batch:** — (standalone)

**Depends on:** T-143

**Verify:** patina-unit, scenario-patina

## Goal

A fresh agent on plain `patina` reads every tool description in full and knows the node types, their ports and their
parameters from them, without probing with graphs it expects to fail.

## Scope

- **Confirm the cause first.** T-143's agents all said the descriptions arrived cut short; `define_graph`'s is 17,909
  characters. Find out where the cut happens (the client's cap on a description or on `tools/list`, or something else)
  and what the limit is, from the client's docs or a measured probe: one fresh `claude -p` session asked to quote the end
  of `define_graph`'s description. Write the finding here.
- **The fix, shaped by the cause.** The likely shape: the node catalogue (`NodeCatalogue`) moves out of
  `define_graph`'s description into a short index there plus a tool of its own (for example `nodes`, returning one
  node type's or every type's ports, parameters, ranges and an example), so every description stays under the limit
  with a margin. If the cause calls for another shape, propose it to the person before building it.
- Every worked example in a description is still run as written by a test. A test holds each description's length
  under the measured limit, with its margin stated.
- **Re-run** T-143's session 1 (the woven rug) once, under round 1's conditions (`--model claude-opus-5-5`,
  `--allowedTools "mcp__patina__*" --tools ""`), into `C:\Users\andre\artifacts\carve\M-28\T-143\round-2-descriptions\`.
  Record whether the agent read the descriptions whole and how many probing calls it made, against round 1's.
  The person approves the run conditions before it, as T-143's did.

## Notes

- **Decided (2026-11-20, by the person):** fixed in M-28 rather than filed, after T-143 landed with the problem open.
  T-143's spec said a description problem is fixed in the task; it was not.
- Runs beside T-171, which owns `EditGraphTool.cs`. This task owns `DefineGraphTool.cs`, `NodeCatalogue` and any new
  tool. Shared lines that only add (`Program.Tools`, the tool-list tests, CLAUDE.md's Layout) are kept on both sides.
- Opus: the cause is unconfirmed, and the fix shapes how every later agent learns patina.

### As built

Built by Claude Opus 5.5 (`claude-opus-5-5`); the probe session ran on `claude-opus-5-5` (its init line, Claude Code
2.1.329).

- **Cause, measured.** One fresh `claude -p` probe under round 1's flags (`--model claude-opus-5-5 --allowedTools
  "mcp__patina__*" --tools ""`), asked to quote the last 150 characters of every description without calling a tool.
  Claude Code cuts **each MCP tool description at 4,096 characters** and appends `… [truncated]`; it is a cap per
  description, not on `tools/list` (41,654 characters arrived in all). The three quoted endings match patina's text at
  exactly character 4,096: `define_graph` (17,908; cut inside fbm's entry, so 13 of 19 types and every worked example
  never arrived), `check` (5,408) and `edit_graph` (4,239). Probe prompt and answer: `G:\My Drive\artifacts\carve\M-28\T-170\round-1\`.
- **Approved by the person (Waystone, 2026-11-20)** before building: the named shape for define_graph; check trimmed in
  this task; edit_graph (T-171's file, over the limit and growing there) left to T-171 to bring under, named in the
  test rather than skipped silently; a margin of 10%, each description at most 3,686 characters, so swatch and export
  were trimmed too.
- **`nodes`** (`NodeTool.cs`, listed after define_graph in `Program.Tools`): `nodes {}` returns every type's entry,
  `nodes {"type": T}` one; an entry is what the type does, its inputs, outputs, every parameter with range and default
  (from the registry, as before), then a worked define_graph example. Every type has one: the existing ones moved from
  define_graph's description (staves for colour/noise/blend, tiles, worley, fbm/blur/gradient_map, transform, the
  metal plate for output, both graph-node examples), and eight new ones in `NodeCatalogue` (gradient, levels,
  height_to_normal, stripes, shape, warp, input, distance). An unknown type is refused, listing the types.
- **define_graph** keeps the format, the faults, the stave example and its misspelt copy, and an index of every type
  with a few words (`NodeCatalogue.Index`), pointing at `nodes`. **check** keeps the clean bricks example; its
  chrome-in-paint and PNG-files examples left the description and still run as written in `CheckToolTests`. swatch's
  and export's examples now say the metal plate is nodes' output example.
- **Lengths now** (`tools/list` of a Release publish): define_graph 3,430, nodes 1,533, export 3,672, swatch 3,666,
  check 3,680, get_graph 2,214, the rest under 1,500; edit_graph 4,239, still cut, T-171's.
- **Tests:** `NodeToolTests` (every description from `tools/list` at most 3,686 = 4,096 x 0.9, edit_graph named as
  T-171's; every type's examples run as written and held to its entry; `nodes` one, all and refused; define_graph's
  index names every type). The tool-list checks in `GraphToolTests`, `PatinaProcessTests` and scenario-patina's
  `GraphStage` gain `nodes`, so Verify adds scenario-patina.

### Hand-off

Checkpoint reached (code committed and verified, a real run still to come), by Claude Opus 5.5.

- **Done:** cause measured and recorded above; fix approved and built; tests; CLAUDE.md's Layout.
- **Left:** (1) put the re-run's conditions to the person before it: T-143 round 1's session 1 (`s1-rug`, its
  `prompt.txt` unchanged) with `run-session.cmd`'s flags, against a Release publish of this branch (one is at
  `%LOCALAPPDATA%\Temp\carve-m28-t170\patina\`), into `C:\Users\andre\artifacts\carve\M-28\T-143\round-2-descriptions\`;
  write `.mcp.json` with `json.dump`, not by hand. (2) Run it, and record whether the agent read the descriptions
  whole and its probing calls against round 1's 3 (calls 1 to 3 of 25 were deliberately wrong graphs). (3) `cairn lint`,
  `finish`.
- **Checked:** `patina-unit` passed (143, 6 skipped) before the Verify line gained scenario-patina; see the verify
  output in the report for scenario-patina.
- **Surprising:** the cut is the same 4,096 for every tool; check and edit_graph were cut as well as define_graph,
  which T-143's record did not name. T-171 should be told that edit_graph must come under 3,686.
