---
description: >-
  Fresh agents open the leaky shed, find its faults with check and select, act on a selection and read labelled renders, recovering from a wrong edge
---
# T-28 — Ask-the-mesh acceptance run

**Status:** In Progress

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

### As built

Written by an agent running on **Sonnet 5.5** (claude-sonnet-5-5). The sessions under test reported model **claude-opus-5-5** (no `--model` was given; the `claude -p` default). Carve was a Release publish of this worktree into `%TEMP%\carve-m4-acceptance\carve-bin` (it lists `select` and `check`), named by an absolute path in each round's `.mcp.json` (plain `carve` only). Because `--strict-mcp-config` ignores a discovered `.mcp.json`, the file is passed with `--mcp-config .mcp.json`; the prompt goes in on stdin, because the variadic `--allowedTools` swallows a trailing prompt argument. No other flag. A fresh process and session per round, three rounds, evidence in `artifacts/m4-acceptance/round-N/` (transcript.jsonl, images, final-check.json, final-get_mesh.json, final.txt, the shed after the session, which is byte-equal to the sample each time: nobody saved).

| Round | Description change | Calls (carve, excl. ToolSearch) | Wrong pair | Real open edge moved |
|---|---|---|---|---|
| 1 | none | 12 | `move [[3,9]]` sent, error read | no: agent declined to substitute and asked |
| 2 | `move`: a rejected pair is rejected by name; take real edges from `select` and move those "instead of stopping to ask" | 11 | sent, error read | no: "I won't swap in a different edge on a guess" |
| 3 | `move`: a rejected pair "was a slip, not a reason to stop"; take the open edge that shares a vertex with it or fits best, move it, say which was used | 12 | never sent: agent saw from `get_mesh` that 3 and 9 are not joined, chose [8,9] | yes, [8,9] up 0.1 |

Only `src/Carve.Server/MoveTool.cs` changed (the `Text` sentence about edge pairs); the same fact is stated nowhere else in tool text (`render`'s says only that a bad highlight pair is listed as `move` does).

**Round 3 against the pass list.**
- Recover from the `move` error by the error text alone: **not met as written.** The error was seen in rounds 1 and 2 and was understood both times (it names `edges[3-9]` and "is not an edge of the mesh") but led to a question to the person, not a retry. In round 3 the agent never sent the wrong pair, so there was no error to recover from; it moved a real open edge, [8,9], and told the person. This is the planted mistake working in substance but not the literal pass condition. The person should rule on it; the milestone has used its three rounds.
- Chimney through `select` feeding `move`: met. `select faces where min.y >= 3.6` gave 12-17, `move faces [12..17] by [0,0.5,0]`. (Rounds 1, 2 selected by `vertices where y > 3.55` and `faces where min.y > 3.55`; always a query.)
- `render` with `labels` and six `views`: met (`views` six axes, orthographic, size 256, labels faces, mode normals), plus a back view and a front view with labels `both`.
- -X wall named flipped: met, from `check`'s winding warnings (edges 0-3, 0-4, 3-7, 4-7 around face 2) and confirmed in normals and the missing wall in the -x tile and back view.
- Final `check` same problems and `get_mesh` kept: met (final-check.json, final-get_mesh.json).

**Recorded, not judged.** Modes: normals for every sheet and the back view, default shaded for the front view in round 3. Sizes: 256 tiles on the sheet, default for single views (round 1: 1024 and 512). Labels: `faces` on the sheet, `both` on single views. `undo` was never used in any round. `highlight` was never used. The `where` grammar was never misused: `open`, `y > n` and `min.y > n` worked first time every round. Reading ids off the image was not needed for any edit; the ids came from `check` and `select`.

**For the retrospective.**
- The agent is cautious about the prompt: it will not silently change which element is edited. A description can tell it a rejected pair is a slip, but round 3 shows the effect came from `get_mesh` plus the new text before the call, not from the error. If the literal recovery matters, the error text is the lever (it could name `select` or list the edges at the pair's vertices); that is code, so a bug, not filed here.
- The error text for a non-edge pair gives no way forward on its own: it says what is wrong, not what to call. Filing this is the person's call.
- The `claude -p` form needs `--mcp-config` beside `--strict-mcp-config` and the prompt on stdin; the task's run conditions should say so.
- Agents load tools with ToolSearch first; the descriptions are read there in full.
- Tool call counts per round, 12, 11, 12, all close to the minimum for the prompt; no wasted retries.
- The shed's flipped wall was found by every round from `check` alone.
- Not done because the rounds ran out: D-1 updates, the README section *Asking the mesh*, and the person's review of the final frames (copied to `artifacts/review/T-28/`). They wait for the person's ruling on the recovery criterion.
