---
description: >-
  Roughen part of a sharp-edged mesh in three calls (define_group, tessellate, displace) without rounding it or bending the faces around it
---
# M-41 — Wear on hard surfaces

**Status:** Planned

**One-liner:** *Roughen part of a sharp-edged mesh in three calls (define_group, tessellate, displace) without rounding it or bending the faces around it*

## What this milestone delivers

An agent can give part of a hard-surface model real surface wear (worn flagstones, dented plates, chipped ashlar)
while every other face keeps its exact position: `define_group`, then `tessellate` on the group, then `displace` on
the group with its border pinned. Today it cannot. `subdivide` is Catmull-Clark only (it rounds hard-surface edges),
works on the whole mesh and stops at level 3. `displace` on a group moves the group's border vertices and bends the
faces around it out of plane.

- `tessellate` (T-237): a new edit that cuts the selected faces into triangles on their own plane, none longer
  than `max_edge`, keeping holes, moving no existing vertex, and adding the border points to the neighbours' loops
  so the mesh stays closed.
- `displace` takes `pin_border` (T-238).
- `moved` is short at `summary` verbosity (T-239), so a displace of thousands of vertices is not mostly a list of ids.

## Why

Requested from the Shoalward terrain project (modular sci-fi terrain for resin printing, fits toleranced to
0.2 mm). Tried on its piece `D-DAIS` (1,120 vertices, 777 faces, every edge sharp, units mm):
`displace {"source":{"noise":{"seed":7,"scale":4,"octaves":4}},"amount":0.4}` with no selection moved every vertex
(the peg holes and underside too), left 544 faces non-planar, warned the mesh was too coarse (edges average 7.979
against a scale of 4), and listed all 1,120 moved ids at `summary`.

The non-planar faces have two causes: the border vertices of a selection move, bending the unselected neighbours
(T-238 fixes this), and any face of four or more corners inside the selection goes non-planar once its corners move
by different amounts (only triangles stay flat, so T-237 makes triangles).

## Done when

On a fixture shaped like `D-DAIS` (a flat-topped slab with holed faces, pegs and sharp edges, built in Carve's own
tests; the Shoalward file is not in this repo), with its top faces in a group `wear`:

- `tessellate` at `max_edge` 0.75 leaves the mesh closed with no new warnings; every vertex outside the group is
  unmoved, and no face outside the group is split, only given border points in its loop.
- `displace` on group `wear` with `pin_border: true` then leaves every face outside the group planar and unmoved,
  and the mesh closed.

## Open at planning

- The algorithm for points inside a face: Carve's `Triangulator` only ear-clips the corners a face already has and
  there is no Delaunay code in `src`. A lattice and a constrained Delaunay triangulation is the obvious route; T-237
  settles it.
- Order: T-238 depends on T-237 (its tests and its Cuts stage build on the tessellated slab); T-239 is
  independent but touches `DisplaceTool.cs` beside T-238.

## Required tasks

- [ ] [T-237](../tasks/T-237-tessellate-split-selected-faces-into-flat-triangles-down-to-max-edge.md) — A new edit that cuts selected faces into triangles on their own plane no longer than max_edge, keeping holes and moving no existing vertex, with the border points added to the neighbours' loops so the mesh stays closed
- [ ] [T-238](../tasks/T-238-displace-pins-the-selection-s-border.md) — displace takes pin_border: a vertex also on a face outside the selected faces or group does not move, so the faces around a worn patch stay flat and unmoved
- [ ] [T-239](../tasks/T-239-moved-is-short-at-summary-verbosity.md) — displace and move report moved as {count, first} at summary, the first 10 ids, and the whole list only at full

## Retrospective
