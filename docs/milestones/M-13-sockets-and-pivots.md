---
description: >-
  Attachment points and rotation axes on parts, carried through export to an Empyrean game
---
# M-13 — Sockets and pivots

**Status:** Planned

**One-liner:** *Attachment points and rotation axes on parts, carried through export to an Empyrean game*

## What this milestone delivers

An agent marks the points and axes a game uses: a turret's rotation axis, a muzzle socket, wheel axles, a seat. Each is named and attached to a part, kept in the Carve file, exported to the `.glb`, and readable by an Empyrean game.

Content: M-9's sample vehicle with its sockets and pivots marked, and the viewer showing them after import.

Plans against [D-4](../design/D-4-sockets-and-pivots.md).

**Depends on:** M-3 (export) and M-9 (parts, whose origin is their pivot).

## Open at planning

- How a socket is exported: an empty glTF node named by convention, `extras`, or both.
- What Empyrean reads of either, and whether that raises an engine need.
- Whether a pivot carries limits (a gun's elevation range), or only an axis.
- How a render shows sockets and axes.

## Required tasks

## Retrospective
