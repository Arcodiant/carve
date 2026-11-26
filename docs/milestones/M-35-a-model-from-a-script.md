---
description: >-
  A script with variables, loops and call-time parameters builds a model, and a rerun replaces only what it generated
---
# M-35 — A model from a script

**Status:** Planned

**Depends on:** M-34

**One-liner:** *A script with variables, loops and call-time parameters builds a model, and a rerun replaces only what it generated*

## What this milestone delivers

An agent writes a model as a short program and changes it by changing the program: a variable, a loop, a parameter given at the call.

- Variables, arithmetic, loops and small functions over [M-34](M-34-say-it-again.md)'s statements, so "four beams at these x positions" is one line.
- Parameters with defaults, set in the `run` call (`window_width=1.4`), so a rerun needs no file edit.
- A step and face budget, and a refusal that says which was passed and at which line.
- The Carve file records which meshes a script made, with the script's path and parameters. A rerun replaces those and leaves the rest of the model alone.
- A quiet result: counts and bounds for each mesh, warnings folded.

Content: a sample that is plainly parametric (a staircase by step count and rise, or the barrel by stave count), its script committed, rendered at three settings of one parameter.

Plans against [D-8](../design/D-8-scripts-the-call-journal-and-atomic-edits.md).

## Estimated size

About **6 tasks**:

1. The language: the choice made, then its parser or its embedding, with line-accurate errors.
2. Parameters and the budget.
3. Generated meshes recorded in the Carve file (a new file version), and rerun.
4. Hand edits to a generated mesh: the rule D-8 leaves open, built.
5. The parametric sample and its scenario.
6. Acceptance: fresh agents write a script from a brief, then meet a changed brief with a parameter.

## Why

The stone-room project's feature request (2026-11-22, item 1, rated High): its room was built from a 300-line Python script because loops and variables were what the job needed, and the edit tools have neither.

## Open at planning

- **The language** (D-8, Open, the person's call): an embedded existing language, or our own compiled to a stack machine. Write M-34's exported scripts in each candidate first.
- A hand edit to a generated mesh: overwrite with a warning and a detach flag, refuse, or replay (D-8, Open).
- Does a rerun happen only on `run`, or does `open` offer it when the script is newer than the file?
- This milestone adds a Carve file version. So do M-29, M-37 and M-38: none of them runs beside another.

## Required tasks

Not planned yet.

## Retrospective
