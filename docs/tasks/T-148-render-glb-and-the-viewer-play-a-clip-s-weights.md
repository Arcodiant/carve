---
description: >-
  render_glb with clip and time, and the viewer playing a clip, draw the clip's weights from the .glb's weights channel
---
# T-148 — render_glb and the viewer play a clip's weights

**Status:** Open

**Pri:** P1

**Milestone:** M-32

**Batch:** — (standalone)

**Depends on:** T-146, T-147

**Verify:** server-unit, server-regression, scenario-morph, viewer-cube

**Perf:** skip: Carve declares no heavy suites

## Goal

The exported file's weights channels are seen moving: `render_glb` with `clip` and `time` draws them, and the model viewer plays a chosen clip with them live. The engine does neither yet, so Carve samples the channel itself, as an interim recorded as an engine need.

## Scope

- **Engine need 9**, as CLAUDE.md says: add a row to D-1's *Engine needs* table.
  - What is missing: "Morph-weight tracks in clips: `AnimationClip` (Geometry 0.129.0, also 0.131.0) holds only `BoneTrack[]`, so a glTF `weights` channel is not read, and a weights-only animation parses with `Duration` 0. The split load says `weights` channels are ignored."
  - Priority: Low.
  - The interim: the sampler below.
  - Cite it at each workaround site: `// Interim until engine need 9: ...`.
- **The sampler** (`src/Carve.Server/GlbWeightTracks.cs`, new): it reads the file's animations with SharpGLTF, and for a named clip and a time gives one weight per target in `ParsedMorphTargets.TargetNames` order. It implements glTF's LINEAR and STEP over T-147's layout (end keys clamped). It also gives each animation's name and duration from the glTF, including weights channels.
- **`render_glb`** (`GlbScene.Prepare`, `RenderSystem`):
  - With `clip` and `time`, the clip's sampled weights are written to `MorphWeights` beside the bone pose. The bone pose still comes from the engine.
  - **Unskinned files:** a file with no skin but with animations takes `clip`. Names, durations and the time check come from the sampler, replacing "the file has no skin, so it has no clip" for such a file only.
  - `targets` naming a target the clip keys is refused, naming the clip (the person's rule, as T-146 words it for `render`).
- **The viewer** (`spikes/Carve.ModelViewer`):
  - It links `GlbWeightTracks.cs`.
  - The window can pick a clip by number key, not just the first one, and plays it with its weights applied every frame.
  - The panel shows the clip's name and its live weights.
  - Captures are unchanged (no panel in them).
  - The person uses it for T-151's real-time pass.
- **Content (skeleton-less clips, the person, 2026-11-19):**
  - In `scenario-morph`, M-21's unrigged cylinder gains a weights-only clip `Pulse` over `Bulge` and `Pinch`: LINEAR, 1 s, looping, Bulge 0→1→0 and Pinch at STEP 0/0.5. It's defined through `define_clip`.
  - It's exported unskinned, and drawn by `render` and `render_glb` at 0.25 s and 0.6 s (one key, one between keys), from front and from above.
  - Each pair is held to D-6's frame rule, with a control that must fail.
  - Its frames are worked through the cylinder's own ray test, as T-103's are.
  - A `CARVE_MORPH_BREAK` value `glbclipweights2` (the sampler ignoring weights) must fail.
- **Tests:**
  - `unit`: the sampler against hand-written channels (LINEAR, STEP, clamp, mixed layout).
  - `server-regression`: `render_glb` of a skinned mixed clip and an unskinned weights-only clip, equal to `render` under D-6's rule, plus the refusals.
  - `viewer-cube`: still passes, and a viewer test shows a clip picked by key. The window keys stay covered by tests and the person's pass, as `WeightPanelTests` are.

## Notes

- **An interim on the engine's behalf, and a format reader:** Opus.
- **The ruling that binds:** interim and engine need, with no engine bump and no wait (the person, 2026-11-19). Carve isn't registered as an engine request client, so no request is filed.
- **File ownership:**
  - This task owns `GlbScene.cs`, `RenderGlbTool.cs`, `RenderSystem.cs`'s weight write, the new sampler, the viewer, D-1's needs table, and `scenarios/Carve.Scenario.Morph`, with its tests and suite inputs.
  - T-150 runs beside it in `scenarios/Carve.Scenario.Face`. A `tests.json` conflict only adds, so keep both sides.
- **Every existing frame stays byte-identical:** hash all frame suites before and after. The arm's frames and `differences.tsv` are unchanged.
- **Stop and ask:** any pin you'd re-take (the `scenario-morph` sample guards, frame hashes), and anything the rulings don't settle.
- **Write down for dependents (As built):** the viewer's keys and how to start it on a file (T-151's live pass uses it), and the sampler's API.
- **Plan a second agent** for the Pulse frames' review: it opens the frames itself.
- **Carried:**
  - each new check shown failing on a broken product, as a table;
  - evidence at the absolute path the dispatch names;
  - `finish` run inside the worktree, with its `Merge task/T-148` line given;
  - report a `verify` that waits more than a few minutes.
