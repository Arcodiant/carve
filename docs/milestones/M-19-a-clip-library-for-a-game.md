---
description: >-
  Clips-only export, loop and in-place checks, and proof that a game's load of the .glb matches Carve's own preview
---
# M-19 — A clip library for a game

**Status:** Planned

**One-liner:** *Clips-only export, loop and in-place checks, and proof that a game's load of the .glb matches Carve's own preview*

## What this milestone delivers

Orcward's *Done when*. Clips are checked and read back, a clips-only file serves as a game's shared library, and `render_glb` shows a `.glb` through the engine's own loader and skinning, so a loaded frame can be compared with Carve's preview. Fresh agents then do the whole job from the tool descriptions.

Content: the gardener's clip library, and the frames comparing game and preview.

Plans against [D-6](../design/D-6-characters-rigging-animation-and-character-export.md), from Orcward's request of 2026-11-06.

**Depends on:** M-16 for T-45 to T-48; M-17 and M-18 as well for T-49.

**Order.** T-45, T-46 and T-47 side by side; T-48; T-49. Stronger model on T-47, Sonnet on the rest.

**The person decides** the frame tolerance at T-48, from measured differences.

## Required tasks

- [ ] [T-45](../tasks/T-45-clip-checks-and-read-back.md) — get_clips reads clips back, and looping clips that do not close or clips that travel are warned about
- [ ] [T-46](../tasks/T-46-clips-only-export.md) — export in clips mode writes an armature, skin and animations with no mesh, which LoadAnimationSet reads with the same bones and clips
- [ ] [T-47](../tasks/T-47-render-a-loaded-glb.md) — render_glb draws a .glb through Empyrean's own loader and GPU skinning at a clip and time, framed as Carve's preview is
- [ ] [T-48](../tasks/T-48-orcward-done-when-scenario.md) — One suite proves the request's done-when: both exports, Load, LoadAnimationSet, and a loaded frame matching Carve's preview
- [ ] [T-49](../tasks/T-49-character-acceptance-run.md) — Fresh agents build, rig, animate and export an original character from the tool descriptions alone, across two sessions

## Retrospective
