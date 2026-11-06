---
description: >-
  A two-mesh coloured character with a skeleton, weights and a clip is saved, exported as a skinned .glb and loads through Empyrean
---
# M-16 — A character that moves

**Status:** Planned

**One-liner:** *A two-mesh coloured character with a skeleton, weights and a clip is saved, exported as a skinned .glb and loads through Empyrean*

## What this milestone delivers

The thin slice of Orcward's request, end to end. An agent builds a character of two named meshes, colours it, gives it a skeleton and hand weights, writes a clip, looks at it at any moment, saves it, and exports a skinned `.glb` that Empyrean's loader reads with every piece, bone and clip.

Content: the gardener, an original small character with a hat and a watering can, used through M-19.

Plans against [D-6](../design/D-6-characters-rigging-animation-and-character-export.md), from Orcward's request of 2026-11-06.

**Depends on:** T-21 and M-3. It runs ahead of M-4 (the person, 2026-11-06).

**Order.**

| Wave | Tasks | Model |
|---|---|---|
| 1 | T-29 | stronger: every later tool uses its `mesh` argument |
| 2 | T-30, T-31 | Sonnet |
| 3 | T-32 | Sonnet |
| 4 | T-33 | stronger: the sampler must match the engine |
| 5 | T-34, T-35 | stronger: the file layouts are public |
| 6 | T-36 | Sonnet |

There is no fresh-agent acceptance run here: the first is T-49, once the tools are complete.

## Required tasks

- [ ] [T-29](../tasks/T-29-a-model-of-named-meshes.md) — The kernel and the store hold a model of named meshes, and every existing tool works on a named mesh with every frame unchanged
- [ ] [T-30](../tasks/T-30-face-colours-and-groups.md) — An agent colours faces and named face groups, and sees the colours in render and as glTF materials
- [ ] [T-31](../tasks/T-31-skeleton.md) — An agent defines a skeleton of named bones with parents and rest transforms, reads it back, and is refused over 255 bones
- [ ] [T-32](../tasks/T-32-explicit-weights-and-posing.md) — An agent sets per-vertex weights and renders the model in a given pose, skinned by Carve.Core
- [ ] [T-33](../tasks/T-33-clips-and-the-sampler.md) — An agent defines named clips of rotation and translation keys and renders any clip at any time
- [ ] [T-34](../tasks/T-34-skinned-export.md) — export writes a skinned .glb with one mesh, a primitive per piece and colour, skins[0] and the clips, byte-identical run to run, which Empyrean's loader reads
- [ ] [T-35](../tasks/T-35-carve-file-version-2.md) — The whole model saves to and reloads from the Carve file, and version 1 files still open
- [ ] [T-36](../tasks/T-36-character-scenario.md) — A scenario builds the small test character through the real tools, poses it, saves, reopens and exports it, in its own suite

## Retrospective
