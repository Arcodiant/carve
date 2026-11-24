---
description: >-
  Every carve tool description fits Claude Code's 4,096-character cut, held by a test, with the detail that was cut still reachable by an agent
---
# T-207 — carve's tool descriptions under Claude Code's cut

**Status:** Open

**Pri:** P1

**Milestone:** M-29

**Batch:** — (standalone)

**Depends on:** T-165, T-166

**Verify:** server-unit

## Goal

Every description `carve` serves in `tools/list` reaches an agent whole. Claude Code cuts an MCP tool description at
4,096 characters, and several of carve's are past it: `render` (about 18,600), `export` (about 14,000), `check` (about
8,300), `save` (6,286) and `open` (5,617), all from before M-29. What is cut today is still reachable by an agent
through the tools, and a test holds every description under the cut, as `NodeToolTests` does for patina (T-170).

## Scope

## Notes

- **Added to M-29 by the person on 2026-11-24,** to land before T-168, whose fresh agents work from the descriptions
  alone. Asked whether to cut the descriptions down inside M-29, file it off-milestone or leave them, the person chose a
  new member before T-168.
- **Found by:** T-160 (`save`, `open`), T-163 (`render`), T-164 (`export`) and T-166 (`check`), each of which added
  the sentences its spec asked for to a description already over the cut.
- **Precedent:** T-170 did this for patina (an index in `define_graph`'s description, the detail served by `nodes`).
- **Waits on T-165 and T-166,** which are still changing descriptions.
