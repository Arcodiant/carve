---
description: >-
  Clips key morph target weights as well as bones, exported as glTF weights channels, so a face rig's clips are authored in Carve alone
---
# M-32 — Faces that move

**Status:** Planned

**One-liner:** *Clips key morph target weights as well as bones, exported as glTF weights channels, so a face rig's clips are authored in Carve alone*

## What this milestone delivers

A clip moves a mesh's target weights over time the way it moves bones, and the `.glb` carries that motion as a glTF
`weights` channel, so a game plays it without a separate script patching the file.

- **`define_clip`** takes a `weights` part keyed by target name (a part in `channels` or a separate argument beside them,
  to settle at planning). Each key is `{time, value}` with a weight in [0, 1], as `render`'s `targets` already takes them.
  The part takes `interpolation` `LINEAR` or `STEP`, as the bone parts do. A clip that keys only weights, with no bone
  channels, is valid.
- **Export:** one glTF `weights` channel per animation that keys any weights, on the node holding the mesh. Its output
  covers every target, since glTF requires that. A target the clip doesn't key holds the mesh's default weight (0) at
  every key.
- **Read-back and drawing:** `get_clips` returns the weights, `save` and `open` carry them, and `render` and `render_glb`
  with `clip` and `time` draw them, as they already draw a skinned clip.

Content: a face whose targets are `Smile`, `Frown`, `BrowRaise`, `Blink_L`, `Blink_R` and `JawOpen`, with the clips `Happy`,
`Surprised`, `Angry`, `Talk` and `Blink`. These mostly move weights, not bones. The face is exported and played by
Empyrean's importer, which already reads glTF `weights` channels into a clip's morph-weight track.

## Why

Raised in [GitHub issue #1](https://github.com/Arcodiant/carve/issues/1) (2026-11-19). [M-21](M-21-morph-targets.md)
authors targets and exports them, and the clips from [M-16](M-16-a-character-that-moves.md) to
[M-19](M-19-a-clip-library-for-a-game.md) key only bone `rotation` and `translation`. So the only way to animate weights
today is to export and then patch the `.glb`'s animations with a script, which splits a clip's source across two tools.

## Out of scope

- Corrective (driven) targets. That is a separate question.

## Open at planning

- Is `weights` a part inside `channels` (and so a reserved name no bone can take), or a separate argument?
- Which Carve file version holds weight tracks? It follows the lowest-version rule (2026-11-11), and it must be
  numbered after [M-21](M-21-morph-targets.md)'s version 8 and [M-9](M-9-parts.md)'s versions 7 and 9 (T-132: parts
  beside targets). A clip's weights are keyed by target name, so a model with parts and targets may need them too.
- Which mesh's targets a clip keys when a model has more than one mesh with targets: a name per mesh, or a name that must
  be unique across the model?
- How the sampler blends a weights-only clip with a bone clip. For example, does `Blink` layer over `Talk`, or does
  that wait?

## Required tasks

Not planned yet.

## Retrospective
