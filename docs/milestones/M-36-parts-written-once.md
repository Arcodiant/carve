---
description: >-
  Script functions return parts that are placed many times as instances, with wall, prism and sweep generators and an export that keeps each part as its own node
---
# M-36 — Parts written once

**Status:** Planned

**Depends on:** M-35, M-10

**One-liner:** *Script functions return parts that are placed many times as instances, with wall, prism and sweep generators and an export that keeps each part as its own node*

## What this milestone delivers

An agent writes a beam bay, a hinge or a window frame once, as a function that returns a part, and places it many times. A whole room comes from one script, in Carve alone, and reaches a game with each object still its own.

- Functions that return parts: pure, with no I/O, under the step and face budget, built at the origin and placed by the caller. `window(width, height, mullions=1)` sizes itself to its opening.
- Calls with equal arguments are instances of one part ([M-9](M-9-parts.md)), and a rerun rebuilds only the parts whose function or arguments changed.
- Generators for flat-sided building, each a tool and so a script statement: a wall with openings, a prism (a flat outline with holes, pushed out), and a sweep of an outline along a path. They wind outward, or inward where asked, and never mirror a UV.
- A scene export: one glTF node for each part, under the part's name, with one shared mesh for instances. Today every static part merges into the root, and only a part marked `moves` keeps a node.

Content: the stone room (walls with a window and a door, a beamed ceiling, a tiled floor), from one script with its part functions, rendered and exported with the door, beams and glass as named nodes.

Plans against [D-8](../design/D-8-scripts-the-call-journal-and-atomic-edits.md).

## Estimated size

About **8 tasks**, at the top of the range; the generators could be split off as a milestone before this one:

1. Part values and registered functions, with the cache by arguments.
2. Placement as M-9 parts and instances, and rerun by part.
3. The wall with openings.
4. The prism and the sweep.
5. The scene export mode.
6. The stone room, its script and its scenario.
7. The exported room read back through the engine's loader, node by node.
8. Acceptance: fresh agents build a room from a brief, then change one function and see every instance change.

## Why

The stone-room project's feature request (2026-11-22, items 1 and 7) and its session's summary of reusable functions. Its script had to get winding and UVs right by hand; its export merged the door, beams and glass into one mesh.

## Open at planning

- Mirrored placement (D-8, Open): refused, or its own mesh.
- Shared UVs against anchored UVs (D-8, Open): a wall whose courses must line up with its neighbour's cannot share UVs with it. Settle with [M-37](M-37-real-sizes.md).
- Which parts the scene export keeps apart: every part, or by a flag, as `moves` works today.
- Does the engine's loader keep a static node's name and transform? If not, that is an engine need.
- Interior shells: a room is seen from inside, so its generators wind inward and `check` must accept that ([M-23](M-23-quiet-on-dense-meshes.md)'s intent flags).
- Whether [M-10](M-10-shape-tools.md)'s primitives need anything more to be used from a function.

## Required tasks

Not planned yet.

## Retrospective
