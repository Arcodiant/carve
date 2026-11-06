---
description: >-
  Automatic weights, group overrides and read-back, with bones and weights shown in renders so skinning faults are seen before export
---
# M-17 — Rig it well

**Status:** Planned

**One-liner:** *Automatic weights, group overrides and read-back, with bones and weights shown in renders so skinning faults are seen before export*

## What this milestone delivers

Rigging becomes practical. One call weights a mesh, a group override corrects it, and bones and weights show in renders, so a skinning fault is seen before export.

Content: the bones overlay, the weights look, and the gardener with planted faults.

Plans against [D-6](../design/D-6-characters-rigging-animation-and-character-export.md), from Orcward's request of 2026-11-06.

**Depends on:** M-18, in Orcward's order: one shared rig means few bodies to weight at first.

**Order.** T-37 then T-38, beside T-39 then T-40; T-41 last. Stronger model on T-37, Sonnet on the rest.

## Required tasks

- [ ] [T-37](../tasks/T-37-automatic-weights.md) — weigh gives every vertex up to four weights from bone proximity, deterministically
- [ ] [T-38](../tasks/T-38-weight-overrides-and-read-back.md) — An agent overrides weights on a face group, reads weights back, and is warned about unweighted vertices
- [ ] [T-39](../tasks/T-39-bones-in-renders.md) — render draws the skeleton over the model, with bone names available as labels
- [ ] [T-40](../tasks/T-40-weights-look.md) — render shows one bone's weights as a heat look on the model, so a stray or missing weight can be seen
- [ ] [T-41](../tasks/T-41-rig-scenario.md) — A scenario finds planted skinning faults through get_weights and the bones and weights looks, in its own suite

## Retrospective
