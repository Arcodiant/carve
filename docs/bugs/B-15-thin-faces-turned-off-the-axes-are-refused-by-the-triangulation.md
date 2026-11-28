---
description: >-
  Thin faces turned off the axes are refused by the triangulation
---
# B-15 — Thin faces turned off the axes are refused by the triangulation

**Status:** Open

**Severity:** Minor

**Milestone:** — (off-milestone)

## Symptom

Found by RV-101 (round 2 of M-41's review, 2026-11-27). `tessellate` refuses a thin face that is not aligned to the axes
of its own box. Each is a single quad in z = 0 at `max_edge` 0.002:

| Strip | Angle | Message (before M-41's plain one) |
|---|---|---|
| 20 x 0.002 | 10° or 30° | "the triangles are not linked" |
| 20 x 0.002 | 45° | "a constraint could not be recovered" |
| 7 x 0.002 | 10° | "a constraint could not be recovered" |
| 10 x 0.002 | 10° | refused the same way |

The same strips axis-aligned, or 1 to 5 m long, succeed, so the failure starts at about 3,500:1 on a rotated face.

Repro: a quad 20 x 0.002 m turned 10° about z, `tessellate {"max_edge":0.002}`. Since M-41 the refusal reads
`faces[0]: is too thin for max_edge 0.002; ...`, a tool error that changes nothing; it is pinned by
`TessellateTests.A_thin_strip_turned_off_the_axes_is_refused_plainly` (`tests/Carve.Core.Tests`), which this bug's fix
turns into a success.

## Cause

Not traced. Likely the tolerance `_eps = 1e-12 * size * size` in `src/Carve.Core/ConstrainedDelaunay.cs` (its
constructor), scaled to the face's bounding box: on a long strip turned off the axes the box is large while the
triangles are tiny, so orientation tests at the scale of the strip's width fall inside `_eps`. To be made local (scaled
to the points being compared), then checked against the table above.

## Fix

## Verified by
