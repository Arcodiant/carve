---
description: >-
  An agent rotates, scales and mirrors about a pivot, edits symmetrically, and starts from primitive generators
---
# M-10 — Shape tools

**Status:** Planned

**One-liner:** *An agent rotates, scales and mirrors about a pivot, edits symmetrically, and starts from primitive generators*

## What this milestone delivers

An agent shapes geometry with more than translation: it rotates and scales a set of vertices, edges or faces about a pivot, mirrors across a plane, and edits symmetrically so that one side follows the other. It starts shapes from primitive generators (box, cylinder, cone, sphere, with a segment count) instead of typing every coordinate. Ids are kept the way `move` keeps them, and a generated primitive returns the same report as `define_mesh`.

Content: a sample built mostly from primitives and these edits (a wheel, a barrel), and tool descriptions with a worked example each.

No design doc: nothing raised in the discussion of 2026-11-04 needed one.

**Depends on:** M-2 (the edit tools). If M-9 has landed, a primitive becomes a part.

## Open at planning

- Rotation given as an axis and an angle, Euler angles or a quaternion; degrees or radians.
- Whether symmetry is a mode the server holds, or a mirror the agent calls after each edit.
- Whether a mirror welds the vertices on the plane, and how it orders the new ids.
- Which primitives, and whether a cylinder's caps are n-gons or fans.

## Required tasks

## Retrospective
