---
description: >-
  Fresh agents open the leaky shed, find its faults with check and select, act on a selection and read labelled renders, recovering from a wrong edge
---
# T-28 — Ask-the-mesh acceptance run

**Status:** Open

**Pri:** P1

**Milestone:** M-4

**Batch:** — (standalone)

**Depends on:** T-27

**Verify:** full

**Perf:** skip: Carve declares no heavy suites

## Goal

A fresh agent with plain `carve` and no Carve docs finds what is wrong with a mesh, selects by a question rather than by typed ids, reads ids off the image, and recovers from a wrong edge by the error text alone. What it hesitates on sharpens the descriptions.

## Scope

**The run**, in a scratch folder `%TEMP%\carve-m4-acceptance\round-N\`, outside every Carve `CLAUDE.md`, holding an `.mcp.json` of plain `carve` only (the README's entry) and a copy of `samples/leaky-shed.carve`. One fresh `claude -p` session with `--strict-mcp-config`, no Carve docs, started in that folder, on a fresh carve process. The prompt:

> *With the carve tools, open leaky-shed.carve from this folder. It is a small shed that was modelled badly. Run its health check and list every fault you find with the element ids. Then find every open edge with a selection; one open edge runs between vertices 3 and 9, so start by moving that edge up by 0.1. Next move the detached chimney up by 0.5, selecting it by a query rather than typing its ids. Look at the shed with the ids drawn on it from at least two views, including a contact sheet of the six axis views, and tell me which wall is flipped. Finish with check and get_mesh.*

**The planted mistake**: vertices 3 and 9 are not an edge of the shed (T-23 asserts it). No description can warn of a wrong pair; `move`'s error names it, and the agent has `select edges where open` to find the real ones.

**Pass means:**

- the agent recovers from the `move` error using the error text alone, and moves a real open edge;
- the chimney is moved through a `select` whose result feeds the `move` (the transcript shows both calls with the same ids);
- `render` is called with `labels` and with `views` of the six axis views;
- the agent names the -X wall as the flipped one (from `check`'s winding warnings, the normals look or a missing wall in a render; record which);
- the final `check` lists the same problems as before, moved positions aside, and the final `get_mesh` is kept.

How many calls it took, which modes and sizes it chose, and whether it used `undo` are recorded, not judged.

**Rounds.** Wherever the agent hesitates, guesses wrong or misuses a tool (above all the `where` grammar, the `highlight` shape, `views`, and reading ids off the image), sharpen that tool's description and run again fresh. **At most three rounds.** If it still fails, stop and report.

**Keep** in the round folder, and copy to the milestone worktree's `artifacts/m4-acceptance/round-N/`: the transcript and its images, the final `check` and `get_mesh`, the shed file after the session.

**Afterwards:**

- Update D-1: the *Tool shape* question with what the run showed about `select`, `check`, `highlight` and `views`; the *Render modes* question (the engine had line lists, per-vertex colours and an immediate text surface, so no engine need arose, or name the need T-25 or T-26 recorded); the *Queries and selections* decision's text with the stateless-id-list shape.
- README: a short section *Asking the mesh* (the `select` grammar in three lines, `check`, a labelled render and a sheet), the README's only edit in M-4.
- Put notes for the milestone's retrospective in the closing report.
- Show the person the final round's labelled frames and the sheet (`mcp__waystone__review`, kind `approve`).

## Notes

- **Decided by the person at planning (2026-11-05):** a fresh-agent acceptance run closes the milestone, as in M-1 to M-3.
- **Decided at planning on the planner's recommendation:** one session, the prompt above, the wrong-edge plant, the pass list.
- A real run fills context. This task is the run, its transcript and text edits, split from the code on purpose. It changes description text, the README and docs only. A code defect found here is a bug filed under M-4, not a fix here.
- A description change must keep every worked-example test and every scenario passing. Run `cairn task T-28 verify`.
- **Open rules: stop and ask.** A change to a tool's arguments or behaviour. Any change to the prompt beyond the planted mistake. A second plant.
- The prompt asks for a final `get_mesh` (M-2's retrospective), so there is a mesh to keep.
- Sonnet is enough.
- Nothing has sound or motion, so there is no listening or real-time pass.
- Run `finish` from inside the worktree and leave the folder.
