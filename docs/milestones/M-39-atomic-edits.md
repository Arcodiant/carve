---
description: >-
  Every mesh edit is a sequence of a small set of atomic operations in the kernel, proved by a bevel written on them alone
---
# M-39 — Atomic edits

**Status:** Planned

**One-liner:** *Every mesh edit is a sequence of a small set of atomic operations in the kernel, proved by a bevel written on them alone*

## What this milestone delivers

The kernel names the handful of atomic edits that every mesh operation is made of, and one new operation is written on them alone, as a tool an agent can call.

- An atomic edit API in `Carve.Core`: a vertex from a blend of sources or an offset, a face from a source face, an edge split into a chain, removal, and a face's loop rewritten. Each keeps the mesh valid and carries every attribute layer (T-71).
- The existing edits (move, extrude, subdivide, the cuts, merge, bridge, delete) expressed as sequences of atoms, with every existing frame and file byte for byte as before.
- One new operation written on the atoms and nothing else, exposed as a tool. It is the test: is it shorter and safer this way?
- A written answer for [D-8](../design/D-8-scripts-the-call-journal-and-atomic-edits.md): how much of an operation is atoms and how much is geometric decision, and so whether a language over the atoms is worth building.

Content: the new tool shown on a sample, with its description and worked example.

## Estimated size

About **5 tasks**:

1. The atomic set, named and tested, each atom shown to keep a mesh valid.
2. The existing edits moved onto it, under the byte-identical guard.
3. The new operation in the Core.
4. Its tool, sample and scenario.
5. The findings written into D-8.

## Why

Raised in the discussion of 2026-11-22 (the person's idea: identify the most atomic operations and define the exposed mesh operations in terms of them, to make new ones simpler to create). T-71's `EditProvenance` is most of the set already; T-71 itself came out of an earlier discussion of a stack language over mesh primitives, which was not built.

The edits run 150 to 570 lines each today, and most of that is geometric decision and refusal text. What this milestone finds out is how much the atoms shorten.

## Open at planning

- **Which operation.** Bevel is the one T-71's paper run was about, and [M-14](M-14-bevel-and-finish.md) plans it against D-5, which is not written. An inset or a poke is mostly topology and needs no design doc. Either this milestone takes a small operation and M-14 builds bevel on the atoms afterwards, or the two are planned as one.
- Whether moving the existing edits is in scope here, or only the API and the new operation.
- The moved-vertex UV rule T-71 left open (decision 3): a bevel needs it.
- Two tiers of atom, element-at-a-time and whole-array (D-8, *GPU acceleration waits for a measurement*): name both now, or only the first?

## Required tasks

Not planned yet.

## Retrospective
