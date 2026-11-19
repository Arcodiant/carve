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
`Surprised`, `Angry`, `Talk` and `Blink`. These mostly move weights, not bones. The face is exported, and its `.glb`'s
`weights` channels are what a game plays. The planning probe found that Empyrean doesn't read those channels yet
(0.129.0, pinned; 0.131.0, the latest), so until engine need 9 Carve draws them itself (see below).

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

**Decided at planning (2026-11-19).** The first four were answered by the person in issue #1, from the consumer side. The rest were decided by the person in the planning session, each on the planner's recommendation unless noted.

- **`weights` is a separate argument** of `define_clip`, beside `channels`: `weights: {<target>: {interpolation, keys}}`. Bone names and target names stay separate namespaces (issue #1).
- **Targets are keyed by name across the whole model.** One name is one dial, as `define_target` already says (issue #1).
- **Layering waits.** One clip is sampled at a time, and the game layers at runtime. A combined preview isn't needed now (issue #1).
- **The file:** whatever fits Carve's versioning (issue #1). Settled as **one version 10**: version 9 plus a clip's `weights`, written only when some clip keys a weight.
- **The engine gap: interim plus an engine need.** The planning probe parsed a SharpGLTF-built skinned `.glb` with the 0.129.0 loader. A weights-only animation came back as a clip with `Duration` 0 and only bone tracks. `AnimationClip` holds `BoneTrack[]` alone, and the split load says `weights` channels are ignored (0.131.0 is the same). So:
  - Carve writes standard glTF `weights` channels.
  - `render_glb` and the viewer sample them with SharpGLTF, which writes and reads LINEAR and STEP weights channels (probed, 1.0.7). This is cited as **engine need 9** in D-1.
  - A skinned weights-only clip keeps today's held bone-0 rest channel, so the engine's `Duration` is right.
  - The load checks read the channels with SharpGLTF.
- **Skeleton-less clips are allowed** (the person's choice, against the planner's recommendation to keep needing a skeleton). A model with targets and no bones may have weights-only clips, exported unskinned with animations, and drawn by `render_glb` from the sampler.
- **Content for that:** the face keeps a small rig (`Neck` > `Head`), and M-21's unrigged cylinder in `scenario-morph` gains a weights-only clip `Pulse`.
- **`render`/`render_glb` with `clip` and `targets`:** a target the clip keys is refused, naming the clip. Targets it doesn't key still apply.
- **Part trees:** when every target lands on a single glTF mesh used by one node, that node gets the channel. Otherwise `export` refuses, naming the clip and the parts. The person asked whether exporting moving parts as submeshes would make this easier. It would make the weights trivial, but primitives share one node, so each moving part would have to become a rigidly skinned joint, replacing the split-load layout. That was not taken.
- **`export mode: clips`:** bone channels are written, and the weights are omitted with a warning naming the clip and its targets.
- **Mixed interpolation in one clip:** glTF has one interpolation per channel. A clip that mixes STEP and LINEAR tracks is written as one LINEAR channel, each STEP track held by an extra key 1e-4 s before each of its next keys.
- **Loop closure:** a looping clip's weight track warns when its last key differs from its first by more than 0.001.
- **A target a clip keys can't be lost:** `delete_target`, `drop_targets` and any replace that would remove it are refused, naming the clips.
- **One fresh-agent acceptance round** runs last (T-152). Its plant is put to the person before the run.

**The agreed sketch** (the person approved it in the planning session):

```
FRONT (camera on +Z; the face looks along +Z, so its own LEFT is image RIGHT)

          .-------------------------.
         /    ====         ====      \    Brows (dark brown): BrowRaise lifts both,
        |    __                __     |   Frown drops the inner ends
        |   (o )  Lids  Lids  (o )    |   Eyes (white + black pupil), static.
        |   Blink_R          Blink_L  |   Lids (skin): Blink_L / Blink_R close one eye each
        |              /\             |   Nose: a pulled-out wedge in Head
        |             /__\            |
        |      \______________/       |   Mouth slot in Head: Smile lifts corners,
        |       |  (dark red)  |      |   Frown drops them, JawOpen lowers lower lip
         \      '--------------'     /    and chin, showing the inside
          '-------------------------'
                 |  Neck  |               Bones: Neck (root) > Head

SIDE (+X)        ___
               /    \__  brow
              |    o  |>  nose
              |      _|   lips
               \____/     chin drops on JawOpen
                 ||

Meshes: Head (skin, mouth inside dark red), Eyes, Brows, Lids.
Targets: Smile, Frown (Head + Brows: one dial on two meshes), BrowRaise (Brows),
Blink_L, Blink_R (Lids), JawOpen (Head).

CLIPS
 Happy     2 s loop   Smile up to 0.8, BrowRaise 0.3        (weights only)
 Surprised 1 s        BrowRaise 1, JawOpen 0.7              (weights only)
 Angry     1 s        Frown 1, Head pitched 8 deg down      (mixed)
 Talk      1.2 s loop JawOpen STEP 0/0.6/0.2/0.8,
                      Head nods 3 deg LINEAR                (mixed, STEP)
 Blink     0.3 s      Blink_L leads Blink_R by 0.04 s       (weights only, asymmetric)

FRAMES (render)
 1-3   rest: front, side, 3/4
 4     Happy at 1 s          5  Surprised at 1 s
 6     Surprised, side       7  Angry at 1 s
 8-10  Talk at 0.15 (STEP held between keys), 0.3 (a key), 0.75 (nod)
 11-13 Blink at 0.08 (L shut, R half), 0.15 (both), 0.3 (open)
 14    Blink at 0.08, close on the eyes
 render_glb against render (D-6 frame rule + patch rule):
       4, 5, 7, 9, 11, plus close crops on the mouth (9) and eyes (11),
       and a control that must fail
 Cylinder: Pulse (weights-only, unskinned) at 2 times, render_glb against render
```

**Order:**

| Wave | Tasks | Why |
|---|---|---|
| 1 | T-144 (Core), T-149 (the face, on today's tools) | The face's content starts with the mechanic, and needs no new tool |
| 2 | T-145 (file), T-146 (tools and render), T-147 (export) | All three need T-144's type, and their files don't overlap |
| 3 | T-148 (render_glb, viewer, `Pulse`), T-150 (the face's clips, saved and exported) | T-148 needs T-146's render rule and T-147's channel; T-150 needs T-145 to T-147 and the face |
| 4 | T-151 (the person's live pass, then the clip frames) | Needs the viewer (T-148) and `face.glb` (T-150); goldens are approved only after the live pass |
| 5 | T-152 (acceptance) | Last, on the finished descriptions |

Nine tasks, one over the guide's eight, because the clip frames are split from the code they check (M-19's and M-9's lesson).

## Required tasks

- [ ] [T-144](../tasks/T-144-weight-tracks-in-a-clip-in-the-core.md) — A clip keys morph target weights by target name, LINEAR or STEP, beside or without bone channels, sampled and checked in the Core
- [ ] [T-145](../tasks/T-145-carve-file-version-10-weight-tracks-in-clips.md) — The Carve file holds a clip's weight tracks as version 10, written only when some clip keys a weight
- [ ] [T-146](../tasks/T-146-define-clip-keys-weights-and-render-draws-them.md) — define_clip takes a weights argument keyed by target name, get_clips returns it, a model with no skeleton may have weights-only clips, and render with clip and time draws the weights
- [ ] [T-147](../tasks/T-147-weights-channels-in-the-exported-glb.md) — export writes one glTF weights channel per clip that keys weights, on the node holding the morph mesh, skinned or not
- [ ] [T-148](../tasks/T-148-render-glb-and-the-viewer-play-a-clip-s-weights.md) — render_glb with clip and time, and the viewer playing a clip, draw the clip's weights from the .glb's weights channel
- [ ] [T-149](../tasks/T-149-the-face-built-through-the-tools.md) — A stylised face with targets Smile, Frown, BrowRaise, Blink_L, Blink_R and JawOpen on a Neck > Head rig, built through the tools and seen at rest and at weights
- [ ] [T-150](../tasks/T-150-the-face-s-clips-saved-and-exported.md) — The face gets the clips Happy, Surprised, Angry, Talk and Blink, saved as samples/face.carve and exported to a .glb whose weights channels equal the clips' keys
- [ ] [T-151](../tasks/T-151-the-face-s-clips-seen-and-played.md) — The face's clips are seen at their times by render and render_glb, played live in the viewer by the person, and compared frame by frame
- [ ] [T-152](../tasks/T-152-fresh-agents-key-a-face-s-weights.md) — A fresh agent on plain carve authors and exports a clip that keys weights from the tool descriptions alone

## Retrospective
