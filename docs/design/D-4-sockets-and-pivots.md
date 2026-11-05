---
description: >-
  Game metadata on parts, attachment points and rotation axes, and how it reaches Empyrean through export
---
# D-4 — Sockets and pivots

**Status:** Reserved

**Raised by:** discussion of what Blender lacks for an agent, 2026-11-04.

**Default firmness:** Leaning · Rule

## Intent

A game needs to know where things attach and how they turn: a turret's rotation axis, a gun's muzzle, wheel axles, a seat. In Blender these are ad-hoc empties that are easily lost on export. Carve should hold them as named data on parts, carried through the Carve file and the `.glb` to the game. The modelling skill fills them in from the brief ("the turret rotates about Y").

Milestone planned against this doc: [M-13](../milestones/M-13-sockets-and-pivots.md).

## Decision

### Sockets and pivots are named data on parts

**Certainty:** Leaning · **Strictness:** Rule

A socket is a named point and orientation attached to a part. A pivot is the part's origin ([D-2](D-2-part-tree-and-hierarchical-modelling.md), *A part's origin is its pivot*) plus, for a part that moves, its axis. Both are kept in the Carve file and exported.

**Alternatives:** leave attachment points for each game to add after import.
**Would change if:** Empyrean games turn out to place attachments in code rather than read them from models.

## Open questions

- How a socket is exported: an empty glTF node named by convention, `extras`, or both.
- What Empyrean reads of either, and whether that raises an engine need.
- Whether a pivot carries limits (a gun's elevation range), or only an axis.
- How a render shows sockets and axes.
