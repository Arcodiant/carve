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

**Decided at planning (2026-11-25).**

By the person, each put with a recommendation:

- **Rotation is an axis and an angle, in degrees.** The axis is `x`, `y`, `z` or a vector, and the angle follows the
  right hand. The pivot defaults to the selection's bounds centre (my recommendation, not asked separately). A multiple
  of 90° about an axis is exact. Declined: radians, a quaternion (that stays `define_part`'s) and Euler angles.
- **Symmetry is a per-call argument**, `symmetric: x | y | z` on `move`, `rotate` and `scale`, through the mesh's own
  origin. Partners are found by position within 1e-5 m, vertices on the plane stay on it, and an unpaired vertex is
  refused by id. `extrude` is not covered. Declined: a mode the server holds, and mirror alone.
- **A mirror is a welded copy.** It copies the given faces (default all) across an axis plane (`axis`, `at`) and welds
  the vertices on the plane, which keep their ids. New vertices are appended in source-id order, and new faces in
  source order with their loops reversed. UVs, colours, groups and seams are copied. A face lying in the plane is
  refused. A mesh with weights or morph targets is refused. Declined: a copy with no weld, and a flip in place.
- **Primitives are one tool, `primitive`, of four kinds:** box, cylinder, cone and UV sphere, with segments and rings,
  axis +Y, centred on `centre`. Caps are n-gons. Each kind comes **with seams marked and UVs unwrapped**, which the
  person chose over my recommendation of none. `name` works as in `define_mesh`, so a primitive can fill an empty part,
  and the report is `define_mesh`'s.
- **scale takes factors > 0 only.** A factor ≤ 0 is refused, naming `mirror`.
- **M-10 starts after M-29 lands.** It is planned against M-29's live branch: the `help` tool, the 3,686-character
  description budget (T-207) and `get_mesh`'s `include` (B-10). They touch the same descriptions and the same
  registration line.
- **An acceptance run with fresh agents ends the milestone** (T-216).
- **The samples and scenario as sketched below.**

By me (each written into its task as open to the person):

- The defaults and ranges (radius 0.5, segments 16 from 3 to 256, sphere rings 8), and the ring and id order of each
  primitive (`BarrelSpec`'s for the cylinder; the unit cube's for the box).
- In symmetric editing, the + side of the plane leads, and a − side vertex listed counts as its partner listed.

Still open, to ask before T-210 is dispatched: whether `rotate` and `scale` turn a morph target's deltas. I recommend
keeping them as `move` does.

The agreed sketch:

```
WHEEL (axis X, r 0.35, width 0.12, 16 sides)        BARREL (axis Y, r 0.36, h 1.0, 16 sides, 4 bands)
 1 primitive cylinder h 0.06, 16 seg  -> 32 v 18 f    1 primitive cylinder rings 4   -> 80 v 66 f
 2 rotate 90 deg about Z (pivot origin)                2 scale middle ring x,z by 1.17 (bulge)
 3 move +0.03 X  (half wheel, x 0..0.06)              3 scale ring 1 x,z 1.06, symmetric y -> ring 3 follows
 4 delete inner cap (x=0)             -> 32 v 17 f    4 unwrap; check closed, no warnings
 5 extrude outer cap inset .22 d .04  -> 48 v 33 f
 6 mirror x at 0, weld                -> 80 v 66 f      side view          top view
 7 unwrap                                                 ___               .-''-.
                                                         /   \             /      \
   front (+Z) view      side (+X) view                  |     |           |  (  )  |
     ____ ____             .-""-.                       (     )  <- bulge  \      /
    |    |    |           /  __  \                      |     |             '-..-'
   =|hub |hub |=         |  (  )  |                      \___/
    |____|____|           \  ""  /
      ^ welded plane        '-..-'
```

Scenario `scenario-shapes` has one stage per file: the primitives (T-213), rotate and scale (T-214), mirror (T-214),
and the wheel and barrel with the symmetric frames (T-215). Every subject is asymmetric or has one face coloured, so a
mirrored camera fails, and every pixel is worked by the Cuts ray test.

**Order.**

- Wave 1: T-209, T-210 and T-211, side by side. Each adds new files and one entry to `CarveServer.AddTools`; keep both
  sides of that clash.
- Wave 2: T-212 (after T-210 and T-211) and T-213 (after T-209).
- Wave 3: T-214 (after T-210, T-211 and T-213) and T-215 (after T-212 and T-213).
- Wave 4: T-216.

The sample needs every mechanic, so it cannot sit in a mechanic's wave. Until it lands, each tool's worked examples,
run as written, are the content that tool arrives with.

## Required tasks

- [ ] [T-209](../tasks/T-209-primitive-generators.md) — primitive makes a box, cylinder, cone or UV sphere with segments and rings, seams marked and UVs unwrapped, under a name as define_mesh does, and answers define_mesh's report
- [ ] [T-210](../tasks/T-210-rotate-and-scale-about-a-pivot.md) — rotate turns vertices, edges or faces about an axis by an angle in degrees and scale scales them per axis, both about a pivot (default the selection's bounds centre), keeping every id as move does
- [ ] [T-211](../tasks/T-211-mirror-across-a-plane.md) — mirror copies faces across an axis plane, welds the vertices on the plane, appends the new ids in source order with loops reversed, and carries UVs, colours, groups and seams
- [ ] [T-212](../tasks/T-212-symmetric-editing.md) — move, rotate and scale take symmetric x, y or z: each vertex's mirror partner, found by position, follows mirrored, vertices on the plane stay on it, and an unpaired vertex is refused by id
- [ ] [T-213](../tasks/T-213-the-shapes-scenario-and-the-primitives-stage.md) — scenario-shapes: a new scenario whose first stage shows the four primitives from several sides and in the wireframe and UV looks, every pixel worked from the geometry
- [ ] [T-214](../tasks/T-214-rotate-scale-and-mirror-stages.md) — scenario-shapes gains a stage for rotate and scale on an L-block about a tilted axis and one for mirror with its new faces highlighted, every pixel worked from the geometry
- [ ] [T-215](../tasks/T-215-the-wheel-and-the-barrel-from-primitives.md) — samples/wheel.carve and samples/barrel-turned.carve built from a cylinder by rotate, delete, extrude, mirror and a symmetric scale, seen in scenario-shapes and kept equal by content to what it builds
- [ ] [T-216](../tasks/T-216-shape-tools-acceptance-run.md) — Fresh agents build a wheel and a barrel from primitives with carve's descriptions alone, and recover from a planted unpaired vertex

## Retrospective
