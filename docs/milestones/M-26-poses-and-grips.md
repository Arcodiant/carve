---
description: >-
  Pose a skeleton by moving bone ends to targets, keep named poses, and check a posed hand against a held part for gaps and penetration
---
# M-26 — Poses and grips

**Status:** Planned

**One-liner:** *Pose a skeleton by moving bone ends to targets, keep named poses, and check a posed hand against a held part for gaps and penetration*

## What this milestone delivers

An agent poses a hand on a tool inside Carve and learns whether the grip holds up before a clip is built on it.

- Move a bone end to a point with a pole direction; the chain above it follows.
- Named poses in the file, usable as clip keys.
- A part held on a bone while posing ([M-13](M-13-sockets-and-pivots.md)).
- A contact report: gap or penetration between chosen bones' skin and the held part.
- Joint angles reported against limits, and a render from the side of a named joint.

Content: the fps-arms wrench and scanner grips rebuilt with these tools, with their wrist angles in the report.

## Why

Raised by the fps-arms project (private repo `Arcodiant/fps-arms`, 2026-11-15), which built a rigged, textured pair of first-person arms and four tools with Carve 0.9.0 through script-written `.carve` files.

- fps-arms built every grip in script: tool-to-hand transforms, finger curls and a thumb solver, written into clips.
- The wrench grip bent the wrist 119 degrees and the scanner grip 75. The person caught it, not the tools.
- Fingers lying across a scanner's screen and a thumb sinking into a dial were found by eye in renders.

## Open at planning

- Where joint limits come from: the file, a preset for a humanoid hand, or the caller?
- Finger chains: one solve per finger, or hand shapes by curl and spread?
- How this sits with the clip tools from M-16 to M-19.

## Required tasks

Not planned yet.

## Retrospective
