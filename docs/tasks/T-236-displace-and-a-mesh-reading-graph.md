---
description: >-
  displace knows the mesh-input node types and tells an agent truly why a mesh-reading graph cannot displace, or what it does with one, with its tools/list entry pinned
---
# T-236 — displace and a mesh-reading graph

**Status:** Open

**Pri:** P2

**Milestone:** — (off-milestone)

**Batch:** — (standalone)

**Depends on:** — (none)

## Goal

`displace` meets M-29's mesh-reading graphs truthfully. Given a graph with a mesh-input node it either says plainly
that such a graph cannot be used to displace, and why, or evaluates it with a stated meaning; it no longer calls a
valid graph faulty or sends the agent to patina to fix it.

## Scope

## Notes

- **From RV-90,** the review of M-29's rebase onto `main` (2026-11-26), where M-29 met T-223's `displace`, three
  findings filed here by the person's choice in place of a fix and another review round:
  - *(Minor)* given a valid mesh-reading graph (`samples/graphs/weathered-iron.texgraph`, output `wear`), `displace`
    refuses and leaves the mesh unchanged, but answers `node 'curv': unknown type 'mesh_curvature'; the types are ...;
    fix them in patina`. The graph is valid and patina opens it with no fault. The same for a mesh node in a nested
    file, and for one with a bad parameter. `DisplaceToolTests.A_mesh_reading_graph_is_refused_and_changes_nothing`,
    added by the rebase, pins that wording.
  - *(Nit)* `displace`'s tools/list entry is pinned by no test: being in `PartToolsListTests.NewTools` keeps it out of
    the digest, the gap RV-82 closed for `help` and `bake`. Its description is 3,180 characters, under the limit.
  - *(Nit)* the new test's doc comment sits after `[Fact]`, attached to nothing.
- **To settle:** whether `displace` refuses a mesh-reading graph (knowing the mesh node types through `Carve.Graphs`'
  registry, in `GraphSlotProblem`'s wording) or evaluates it on the mesh being displaced (its own curvature and
  occlusion, which change as it displaces). The second is a feature, and a design question.
- `displace` reads a graph with its own loading and not `GraphSlotBake`; M-29's rebase agent was told not to rewire it.
