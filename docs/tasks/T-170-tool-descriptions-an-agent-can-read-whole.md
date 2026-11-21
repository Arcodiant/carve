---
description: >-
  patina's tool descriptions reach an agent whole, with the node catalogue no longer crammed into define_graph's, so a fresh agent learns the node types by reading rather than by sending wrong graphs
---
# T-170 — Tool descriptions an agent can read whole

**Status:** Open

**Pri:** P1

**Milestone:** M-28

**Batch:** — (standalone)

**Depends on:** T-143

**Verify:** patina-unit

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
