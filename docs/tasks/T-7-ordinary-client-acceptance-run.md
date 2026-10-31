---
description: >-
  A fresh agent in an ordinary MCP client builds a shape from the tool descriptions alone, and how to install carve is written down
---
# T-7 — Ordinary client acceptance run

**Status:** In Progress

**Pri:** P1

**Milestone:** M-1

**Batch:** — (standalone)

**Depends on:** T-6

## Goal

An agent that has never seen Carve, in an ordinary MCP client, builds a shape from the tool descriptions alone, which proves the milestone from a plain launch.

## Scope

- Add `README.md` at the repo root: what Carve is in two lines, the `.mcp.json` entry that starts `carve`, and that it needs a Vulkan device. No extension in it.
- **The run**: in a scratch folder under `artifacts/m1-acceptance/`, a fresh `claude -p` session whose only MCP server is plain `carve`, with no Carve docs and no project `CLAUDE.md`, is asked: *Using the carve tools, make a cube, then a small house shape with a pitched roof, and check from both sides that it looks right.* Keep the transcript and images there.
- Pass means: the agent succeeds with no outside help, the image result reaches the model, and any error is corrected from its text alone.
- Any place the agent hesitates, guesses wrong or misuses a tool: sharpen that tool's description, then repeat in a fresh session. At most three rounds; if it still fails, stop and report it.
- Update D-1: answer *Transport* as stdio for now, and record what the run showed about *Tool shape*.
- Put notes for the milestone's retrospective in this task's closing report.

No new scenario: the example test and the cube scenario keep the descriptions honest, and both run in verify.

## Notes

- A real run fills context: this task is the run, its transcripts and text edits, and is split from the code on purpose. It changes tool text and docs only. A code defect found here is a bug filed under M-1, not a fix in this task.
- `claude -p` is the ordinary client on this machine. That an image reaches the model through it has not been tested; if it does not, stop and go back to the person, because it reopens the return decided at planning.
- The person's own look at the render is in T-6; this one is the agent's. Nothing here has sound or motion, so no listening or real-time pass is planned.
