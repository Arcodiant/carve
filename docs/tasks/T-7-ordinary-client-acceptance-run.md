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

### As built

Ran on Sonnet 5.5 (`claude-sonnet-5-5`); the acceptance session itself ran on the machine's default model, `claude-opus-5-5`, via `claude -p` 2.1.301. Passed in round 1 of 3; no tool description was changed.

**Files.** `README.md` (two lines on what Carve is, the `.mcp.json` entry using `dotnet run --project <path>/src/Carve.Server --no-launch-profile`, needs .NET SDK and a Vulkan device, no extension); D-1 *Transport* answered (stdio for now) and *Tool shape* given what the run showed. The run kit is in `artifacts/m1-acceptance/` (ignored): `run.sh N`, `mcp.json`, `prompt.txt`, `summarize.py`, and per round `rN/transcript.jsonl` plus the images pulled from it.

**The run.** `run.sh` starts `claude -p` in `artifacts/m1-acceptance/rN` with `--strict-mcp-config` and an mcp file holding only `carve` (the README's entry form, so the README is exercised), `--setting-sources ""`, built-in tools disallowed, `CLAUDE_CODE_DISABLE_CLAUDE_MDS=1` and `CLAUDE_CODE_DISABLE_AUTO_MEMORY=1`. The scratch folder sits inside this worktree, so the flag is what keeps its CLAUDE.md out. Isolation was checked from the transcript: `init` lists one MCP server `carve` (connected), no skills or slash commands, tools only `define_mesh` and `render` among the carve ones. The `init` event does not show whether instruction text loaded, so a control run (`r0`, a prompt asking the agent to quote any CLAUDE.md, memory or project text and anything on D-1) answered NONE; its only Carve knowledge was the tool names, the tool descriptions and its cwd path. Round 1 made 7 calls: `define_mesh` cube (closed), `render` front and back, `define_mesh` house (closed, no warnings), `render` front and back. No tool error occurred, so "an error corrected from its text alone" was not exercised by the agent; T-6's scenario covers errors. The image reached the model: 4 images in the transcript, and its description of the house front (five-sided gable, one slope, one wall) matches img3 (opened). Its report said the second roof slope was seen from neither view and relied on `closed: true`.

**Prelim and control.** `r1-prelim` was a first run with auto-memory not yet disabled (also passed, same shape); kept for the record, `r1` is the counted round.

**For the retrospective.**
- `claude -p` carries `image/png` tool results to the model; that decision holds.
- The two descriptions were enough: the worked example in `define_mesh` was copied as the pattern; `closed` and `warnings` were trusted as the winding check.
- Two views cannot show every face of a house: the `front` camera sees +Z, +X and top, `back` sees -Z, -X and the underside. An agent asked to "check from both sides" cannot see the second roof slope. A third view (or a choice of direction) is the obvious next tool-shape pressure; not filed, it is a feature rather than a defect.
- Spawning via `dotnet run` worked as a client entry (no stdout noise broke the protocol); startup cost not measured, 24 s for the whole 7-call session.
- Hooks on this machine (a mod plugin `context-bar`) load in `claude -p` regardless of `--setting-sources ""`; harmless here.

**Not checked:** other clients than `claude -p`; a run on another machine or GPU; the `.mcp.json` entry with a relative path; an error recovered by the agent.
