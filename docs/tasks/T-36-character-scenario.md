---
description: >-
  A scenario builds the small test character through the real tools, poses it, saves, reopens and exports it, in its own suite
---
# T-36 — Character scenario

**Status:** In Progress

**Pri:** P1

**Milestone:** M-16

**Batch:** — (standalone)

**Depends on:** T-34, T-35, T-46, T-52

**Verify:** full

**Perf:** skip: Carve declares no heavy suites

## Goal

The thin slice is proved end to end through the real tools, on a character someone could recognise as one.

## Scope

- **The test character** (`samples/gardener.carve`, built as code in the scenario too, and kept equal by a test): an original small round gardener. Two meshes, `Body` and `Hat`, four or more colours, bones named by the engine's Synty preset as Orcward's are (about 22), deliberately asymmetric (a watering can in one hand) so a mirrored frame fails. Clips `Idle` and `Walk`, both looping and in place.
- **`scenarios/Carve.Scenario.Character`**, suite `scenario-character`: build it through the tools; capture rest, a pose, and each clip at three times; `save`, reopen, compare field for field; `export`; export again without `overwrite` (a tool error) and with it (identical bytes); export in `clips` mode; export with a vertex unweighted (a tool error, then `allow_unweighted`).
- **Load check:** a `regression` test reads the exported file through `IModelGeometryLoader.Load` from a content folder and asserts bone count, bone order and clip names, that `HumanoidRoleMap.Synty` resolves on it with no sidecar, and that the T-46 clips file loads beside it.
- **Look at the frames** and say what was seen.

## Notes

- The gardener is the sample for M-17, M-18 and M-19 too: T-41, T-44 and T-48 build on it.
- Sonnet is enough once T-34 and T-35 have landed.
- **Carried from the M-1 to M-4 plans:** the byte-identical guard on every existing frame, by hash before and after; a positive pixel check on every new frame, and one a mirrored or flipped camera would fail; each new check shown failing, as a table.
- **Open rules: stop and ask.** Anything D-6 does not settle, any loosened check, and any change to a D-6 decision.
- Run `finish` from inside the worktree and leave the folder.

### As built

One agent, on Sonnet 5.5.

- **The gardener** (`scenarios/Carve.Scenario.Character/GardenerSpec.cs`, the one source for the scenario's tool calls): original, 1.5 m, round (octagonal lathes, no trigonometry, so the numbers are machine-independent), straw hat with a red band, a watering can in the RIGHT hand (character right = -X, so it is on the image's left head-on from +Z). Meshes `Body` and `Hat`; seven colours, each its own face group (`skin`, `shirt`, `trousers`, `boots`, `can`, `straw`, `band`, so the export's materials are `Body:shirt`, `Hat:band`, ...); 22 bones: Hips, Spine_01..03, Head, Clavicle/Shoulder/Elbow/Hand L and R, UpperLeg/LowerLeg/Ankle/Ball L and R, and a prop bone `WateringCan` under `Hand_R`; every bone rests unrotated, so keys and pose values are the whole local rotation. Clips `Idle` (2 s) and `Walk` (1 s), looping, in place (the root's x and z are never keyed; first key of each channel is the last). `samples/gardener.carve` is the file the scenario saved; `CharacterLoadTests.The_sample_is_the_gardener_the_tools_built_by_content` reads both and compares `CarveFile.Write(Read(...))`, so floats are compared bit-exact and formatting is not (ruling). The Hat crown has a hidden bottom cap, since an open crown made 8 "edge belongs to only one face" export warnings.
- **Limit warnings (Orcward's numbers): none tripped.** 680 triangles (limit 5000), 7 primitives (limit 8, so it is close), height 1.5 m (1.4 to 2.4, and the Synty bones are present so the height rule applies). `export-result.txt` has no `warning(s)`; the load check asserts that. T-51 making them per game will not change this unless a game's numbers are tighter than 8 colours.
- **Scenario** (suite `scenario-character`, `scripts/run-character-scenario.ps1`, 16+ frames): built only through `define_mesh`, `define_skeleton`, `define_group`, `set_colour`, `set_weights`, `define_clip`; frames rest (front +Z, side +X, three-quarter), the pose (can arm out and up, head turned, left leg forward), Idle at 0, 1, 1.5 s (front), Walk at 0, 0.125, 0.25, 0.5 s (side) and 1 s. Then `save`, a decoy `define_mesh`, `open` (22 bones, 2 clips), `get_mesh` of both meshes and `get_skeleton` equal to before, rest/side/Walk frames byte-identical after reopen, saved again to the same bytes; `export` (model: 2 pieces, 7 materials, 22 bones, 2 clips), refused again without `overwrite`, identical with it; `mode: clips` likewise; an `extrude` of the Hat's top face makes unweighted vertices: refused, listed, nothing written, then `allow_unweighted` binds them to Hips; `undo` and the model exports to the same bytes again.
- **Pixel checks, each worked from the geometry** (`CameraPixels` projection under the rest-bounds framing, forward kinematics of the rest skeleton in the spec from the clip keys alone, not the product's sampler): the centre of a front face of each colour reads as that colour (direction in linear light equals the colour's, match >= 0.98, nearest of the palette and the background); the can is on the image's left half and the image-mirrored pixel and the world-mirrored point (+X) are background / not can; the posed can face lands where the pose rotates it and the can's old place is empty; Idle 0 s is byte-identical to the rest frame; Idle at 1 and 1.5 s place the can; Walk places the near hand (skin) and its mirror is not skin (not asked at 0.25 s, where the hand is at the image centre); the gap between the feet is background at 0 and 0.5 s; Walk at 0.125 s (between keys) has the hand half way and not at either key's place; Walk at 0 and 0.5 s are byte-identical to `render` with the `pose` of those keys; Walk at 1 s equals 0 s.
- **Load check** (`tests/Carve.CharacterLoad.Tests`, regression, reads `CARVE_CHARACTER_RUN`, skipped when unset and a skip fails the script): content folder mounted, `IModelGeometryLoader.Load` of `models/gardener.glb`: 22 bones in the saved file's order, clips `Idle`, `Walk`, no parse warnings; `HumanoidRoleMap.Synty.Resolve` and `ResolvePreset` both resolve every required role with no sidecar (so engine need 6's interim names, `Head`, `Shoulder_*`, `Elbow_*`, hold: nothing to report); `LoadAnimationSet` of `models/gardener.clips.glb` gives the same bones and clips and `Load` of it says no meshes.
- **Readings the spec left open:** the clips file is named `gardener.clips.glb`; the unweighted and replaced-file exports go to `work/`, not the content folder; the character's rest pose is arms down and slightly out (not a T-pose); the load check and the sample comparison live in one new test project rather than in `Carve.Server.Tests`; the new frame `gardener-walk-0.125-side` was added because the first break table showed a sampler that holds the earlier key passed (every other time was a key time).
- **Evidence** in `M-16/artifacts/review/T-36/`: `hashes-before.txt` (84 existing frames and export files, from T-35's after-rebase list and T-52's), hash lists after, `break-table.txt`, `sheet.png` and the frames. Break table: camera mirrored in X fails at the first colour check (the boots, on the wrong side of the image, before it reaches the can; the can's own mirror checks are therefore never run by it, which is a limit of the table); sampler without slerp fails Walk at 0.125 s (it passed before that frame was added); `export` overwriting without asking fails the scenario at the second export.
