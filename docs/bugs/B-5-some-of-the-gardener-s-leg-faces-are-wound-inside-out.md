---
description: >-
  Some thigh faces of GardenerSpec and samples/gardener.carve are wound so that their normals point into the leg
---
# B-5 — Some of the gardener's leg faces are wound inside out

**Status:** Open

**Severity:** Major

**Milestone:** — (off-milestone)

## Symptom

Found in M-19's review fix round (RV-52, scenario finding 1, the colour checks on the thighs). Some of the gardener's leg faces are wound inside out, so their normals point into the leg:

- thigh face 126, centroid (0.066, 0.485, 0.058), normal (0.38, 0.04, -0.92);
- thigh face 132, centroid (0.142, 0.35, 0.022), normal (-0.92, 0.05, -0.38).

Both point into the leg. `render` and the scenarios' lit-palette checks (`GardenerPixels.ClassifyLit`) both use the inverted normal, so they agree with each other and nothing fails. A game would not: it would light those faces from inside, or cull them as back faces and show a hole.

The same faces are in `GardenerSpec` (`scenarios/Carve.Scenario.Character/GardenerSpec.cs`, built through the tools by every gardener scenario) and in `samples/gardener.carve`. Not yet checked: `samples/gardener-soft.carve` and the exported gardener `.glb` files, which are built from the same spec and likely carry them too.

## Cause

Not yet found. The legs are octagonal lathes (`GardenerSpec`'s `Lathe`); the likely cause is a ring or segment whose corner order is reversed against the others.

## Fix

**Goal.** Every face of the gardener is wound outward: each face's normal points away from the inside of its part.

**Scope.**

- Find which leg (or other) faces are wound inward, by a check over the whole mesh, not by these two alone, and fix their order in `GardenerSpec`.
- Rebuild `samples/gardener.carve` and `samples/gardener-soft.carve` from the scenarios, as T-84 did, and keep them equal by content.
- A check that would have caught it: every face of the gardener faces outward (for example, its normal against the direction from its bone segment to its centroid), shown failing on the faces as they are now.
- The fix moves frames: every shaded gardener frame of the character, soft, rig and Orcward scenarios where these faces show, and the exported `.glb` files. Hash before and after, and put the moved frames to the person.

## Notes

- **The person's ruling (2026-11-17, RV-52): "File a bug, off-milestone".** M-19 does not fix it.
- Off-milestone; plan it into a later milestone with the gardener's other content.

## Verified by
