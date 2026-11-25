---
description: >-
  How an agent using the tools is given recipes (wear that reads as wear, grime, layered looks) beyond one worked example per node: where they live, how they are reached, and how they are kept true
---
# T-208 — Recipes and guidance for agents using carve and patina

**Status:** Open

**Pri:** P2

**Milestone:** — (off-milestone)

**Batch:** — (standalone)

**Depends on:** — (none)

## Goal

Decide, then build, how an agent using `carve` and `patina` is given recipes and guidance: how to get a look (wear
that reads as wear and not as an edge highlight, grime, a layered material), beyond the one worked example each node
and tool carries today.

## Scope

## Notes

- **Raised by the person on 2026-11-24,** during M-29's acceptance run (T-168): round 1's agent followed
  `mesh_curvature`'s worked example (a curvature band narrowed by `levels`, masking a blend to bright steel) and got a
  thin, even bright rim on every edge. The person approved the look with the note "the wear reads like an edge
  highlight - how do we expect wear to be implemented?", and then: "we should also consider how to provide
  recipes/guidance to agents using the tool".
- **What M-29 did about it:** only the taught example changed, for round 2 (the curvature mask broken up by noise).
  The question of recipes as such was left for this task.
- **To weigh (the orchestrator's notes, nothing decided):** the two `help` and `nodes` tools already serve text on
  request, so a recipe could be an entry there (`help {"topic": ...}`), each run as written by a test as worked examples
  are; sample graphs under `samples/graphs/` an agent can open and read; MCP prompts or resources, which T-207 found
  an agent with no built-in tools cannot read; and what the acceptance rounds show about whether agents call `help` at
  all (round 1's never did). A recipe's look needs a person's eye, so each would come with frames to approve.
- Belongs near M-30 (layered materials), where wear and dirt as layers are planned.
