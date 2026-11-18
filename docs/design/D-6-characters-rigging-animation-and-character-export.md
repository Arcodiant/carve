---
description: >-
  How a model holds named meshes, colours, a skeleton, weights and clips, and how it reaches Empyrean as a skinned .glb and a clip library
---
# D-6 — Characters: rigging, animation and character export

**Status:** Accepted

**Raised by:** Orcward's request, 2026-11-06: original animated characters for an Empyrean 0.119 game, loaded without conversion. It takes up the rigs and animation, smoothing and colour items in D-1's *Scope of a model*.

**Default firmness:** Leaning · Rule

## Intent

An agent on a game authors an original animated character in Carve: a body and costume pieces on one skeleton, coloured and soft-shaded, with looping clips. The source is a committed text file, and the `.glb` built from it loads in Empyrean with no conversion. Skinning faults are seen in Carve's own renders before export.

Milestones planned against this doc: [M-16](../milestones/M-16-a-character-that-moves.md), [M-17](../milestones/M-17-rig-it-well.md), [M-18](../milestones/M-18-soft-characters.md) and [M-19](../milestones/M-19-a-clip-library-for-a-game.md).

**Out of scope:** animation events (`*.events.json`), humanoid role maps (`*.humanoid.json`), morph targets, IK.

### What the planning probe found

A throwaway probe on 2026-11-06 wrote small `.glb` files with SharpGLTF 1.0.7 and read them with Empyrean 0.119.0's `IModelGeometryLoader`. The bones were given breadth-first on purpose: `Hips, LegL, LegR, FootL, FootR`.

| File | Result |
|---|---|
| Two glTF meshes on one skin | **Second mesh dropped.** Only the warning says so: `glTF skinned mesh 'Body' (mesh 0) loads alone; the file's other meshes were dropped: 'Coat' (mesh 1)` |
| One glTF mesh, two primitives, two materials | Both kept: 2 submeshes, no warnings |
| Clips only, no skin | `ParseAnimationSet` reorders bones depth-first: `Hips, LegL, FootL, LegR, FootR` |
| Clips only, a skin and no mesh | Bones in the given order, and SharpGLTF writes it |
| Armature node above the bones | Not a bone; 5 bones, identity `RootTransform` |
| A clip keying one bone of five | 5 tracks, so the clip fits the skeleton |
| A piece: the whole skin, one bone weighted, no animations | All 5 bones in order, no clips, no warnings |

From the docs: `PositionUvNormalSkinnedVertex` has no colour attribute, so `COLOR_0` cannot reach a skinned character, and it packs bone indices one per byte. Every glTF clip import throws on CUBICSPLINE.

Not probed, and left to the tasks that meet them: rotation interpolation between keys (T-33, T-47), drawing a `SkinnedPose` on Carve's headless host (T-47), and byte-identity across processes with skins and animations (T-34).

## Decision

### A model is an ordered list of named meshes

**Certainty:** Leaning · **Strictness:** Rule

`Carve.Core` gains an immutable `Model`: named meshes in the order they were defined, a skeleton or none, and clips. A mesh keeps its own vertex and face ids. The store holds a `Model`, and undo steps over models. A tool names its target with a `mesh` argument, which may be left out when the model has one mesh.

The list is flat. M-9's part tree is the later form of it: a flat list is a tree one level deep, so a named mesh becomes a part.

**Alternatives:** building M-9's part tree first, which delays Orcward behind transforms, instances and levels a character does not use.
**Would change if:** M-9's planning finds a flat list cannot grow into the tree without breaking `mesh` arguments or the file.

### A skinned export is one glTF mesh

**Certainty:** Settled · **Strictness:** Invariant

Every piece of a skinned model is written into **one** glTF mesh, with one primitive per piece and colour, because Empyrean keeps only the first of several skinned meshes. Each primitive has its own material, named `<mesh>:<group>` or `<mesh>:<rrggbb>`, so a piece's name survives in the file. Primitives follow the model's mesh order, then first use of each colour in face order.

**Enforced by:** T-34's test, which fails on any `ParsedModel.Warnings` entry and counts submeshes against the pieces written.
**Rejected:** a glTF mesh per piece, which drops every costume piece on load; a model file per piece, which costs the game a second load and a shared pose.

### Colour is a material

**Certainty:** Settled · **Strictness:** Rule

Colour is per face, set directly or through a named face group, and exported as `baseColorFactor` on a material with metallic 0 and roughness 1, as T-16's material is. Colours are given and saved as sRGB hex, `#RRGGBB`, the form Orcward's data uses, and converted to linear for render and `baseColorFactor`. Materials are opaque. A per-material metallic and roughness override, for armour, comes later in M-7; matte stays the default. An uncoloured face has Carve's surface colour `(0.72, 0.72, 0.70)`, so existing frames do not change.

**Rejected:** `COLOR_0`, which the engine's skinned vertex cannot carry.

### Bones as given

**Certainty:** Settled · **Strictness:** Invariant

Names and order are exactly the caller's, and parents come before children. Names are unique and non-empty. A rest position and rotation are **local to the parent**, as a glTF node's are; a root bone's are relative to the model. No scale. Over 255 bones is refused.

**Enforced by:** T-31's refusals, and T-34 and T-46's tests comparing the loaded bone list with the given one.

### The rest pose is the bind pose

**Certainty:** Leaning · **Strictness:** Rule

Meshes are authored in the rest pose, in model space. `inverseBindMatrices` are the inverse of each bone's rest world transform, and mesh nodes have identity transforms, since Empyrean ignores a skinned mesh's node transform.

**Alternatives:** a separate bind pose per mesh, which suits imported rigs Carve does not have.
**Would change if:** an agent needs to change a skeleton's rest pose after weighting and keep the mesh in place.

### Weights

**Certainty:** Leaning · **Strictness:** Rule

A vertex has at most four influences, normalised to sum to 1. An explicit weight beats an automatic one. Supplying more than four keeps the largest four, with ties going to the lower bone index, and the result says so. Weights live per mesh by vertex id. Automatic weights use distance to each bone's segment, from its head to its child's head, and are deterministic.

**Automatic weights (T-37).** `weigh` works from each bone's segments at rest: one from its head to each child's head, the nearest counting, so a bone with several children (the hips) reaches each of them; a leaf's runs on along its parent's direction (the parent's head to the leaf's) for half its parent's length; a lone root with no children is its head alone. The falloff is an inverse power, `w = (d_nearest / d)^4`: the nearest bone's raw weight is 1 and a bone twice as far pulls 1/16 as hard. It is scale-free, so a model's units do not matter, and it has one constant, the power 4, chosen so that a vertex beside one bone is almost wholly that bone's while one between two shares them. A vertex at distance 0 from bones (on a segment) is shared equally among exactly those: wholly a bone's in the middle of it, half and half at a joint. The largest four are kept, ties to the lower bone index, and normalised, as `set_weights` does; distances are worked in double in bone order, with no randomness. "Explicit" means any existing weight (ruled 2026-11-10): `weigh` fills only vertices with no weight, and `replace: true` weighs every vertex in scope again. Weights record no provenance, and the file format does not change. `bones` limits the scope (ruled 2026-11-10): a vertex is in it when its nearest bone of the whole skeleton (ties to the lower index) is one of those named, and it is weighed among the named bones alone.

A vertex with no weight is a warning while editing, and **`export` refuses** it, listing the vertices (Orcward, 2026-11-06, changing the request). `allow_unweighted: true` is the override for work in progress: those vertices are bound to the first root bone, because glTF needs weights that sum to 1, and the result says so.

**Alternatives:** warning at export and binding to the root, which was the request's first form and lets a fault reach the game.
**Would change if:** the refusal gets in the way of a normal authoring flow.

### Clips

**Certainty:** Leaning · **Strictness:** Rule

A clip has a name, a duration, a `loop` flag and, per bone, rotation and translation keys in that bone's local space, as absolute values. Each channel is LINEAR or STEP; any other value is refused. Bones with no keys hold rest.

glTF has no clip duration, only the last key. Export therefore keys every animated channel at 0 and at the duration, holding the nearest key where the agent gave none, so the loaded clip's `Duration` is the one asked for.

A looping clip whose channels do not end as they start is a warning, as is a clip whose root bones end somewhere else on the ground plane, since clips are in place.

**Closing tolerance** (the person, 2026-11-11: "1° / 1 mm"): a rotation channel closes when its last key differs from its first by at most 1° (q and -q are equal); a looping translation channel closes when its last key is within 1 mm of its first, compared on X, Y and Z (the person, 2026-11-12: "all axes"); a root bone counts as in place when within 1 mm on X and Z only (travel is on the ground plane). Both are constants in `ClipChecks`. The warnings name the clip, bone, channel and difference; `define_clip`, `get_clips`, `save` and `export` carry them, and `get_clips` reads the clips back as given.

**Alternatives:** refusing a clip that does not close, which the request does not ask for and which blocks work in progress; keys relative to rest, which reads well for small motions but differs from what glTF stores.
**Would change if:** agents leave these warnings unfixed and a popping clip reaches a game.

### Two samplers, on purpose

**Certainty:** Leaning · **Strictness:** Rule

Carve's preview poses on the CPU in `Carve.Core` and draws the result as a plain mesh, its corner normals skinned with the same weights; a flat mesh's too, since the export writes a flat face's normal at each corner and a game skins those, so a posed flat face is lit by its skinned corners and not by its posed polygon's own normal (RV-52, the person's ruling of 2026-11-17, "Fix render in M-19"). `render_glb` loads the exported file through the engine's own loader and draws it with GPU skinning; with no clip, a skinned file is skinned at its bind pose (every bone at its node's rest transform, through the file's inverse binds), as a game draws it with no clip playing (RV-52, the person's ruling of 2026-11-17, "Skin at bind pose"). The two share no skinning code, so a frame that matches proves the export and not only a path agreeing with itself.

They do share the material, by the person's ruling (2026-11-12, T-82: "render goes PBR"): the preview draws each colour with the engine's PBR material and the very parameters the loader makes from the exported file (metallic 0, roughness 1), in the same scene light, so the two agree in shading and differ only where the posing does. A highlighted face in the shaded look follows the surface's PBR shading (ruled 2026-11-13). At T-82 a `render` and a `render_glb` of the gardener at rest and in Walk at 0.5 s differed in no pixel past 8/255 (under `Material.LitMesh`: 15.0 % and 12.1 %).

**Alternatives:** drawing previews through the engine's skinning, which is one path and proves less.
**Would change if:** matching the engine's interpolation exactly costs more than it proves.

### Export modes

**Certainty:** Settled · **Strictness:** Rule

`export` gets a `mode`: `model` (the default: meshes, and with a skeleton the skin and clips), `clips` (armature, skin and animations, no mesh) or `piece` (the meshes named in `meshes`, with the skin and no animations). The clips file **keeps `skins[0]`** even with no mesh, because without one the loader reorders bones depth-first, and a clip's tracks then line up with the wrong bones. Bones sit under one unanimated identity node, `Armature`, which is not a bone. `clips` mode needs no mesh: a skeleton and clips alone, a clip library, export, and `save` writes such a model as a version 2 Carve file with `"meshes": []`, which `open` reads back; a file with no meshes and no skeleton is refused, and `model` mode still needs a mesh (the person, 2026-11-09).

The same model always writes the same bytes. `export` refuses an existing file unless given `overwrite: true`; `save` replaces, since the `.carve` file is the working copy (the person, 2026-11-06).

**Rejected:** a clips file with no skin, for the bone order above.

### Pieces share the body's palette

**Certainty:** Settled · **Strictness:** Invariant

Orcward swaps costumes at runtime by loading pieces as separate files and sharing one `SkinnedPose.Palette`, with the body as the driver. That palette is positional and its length must match, and nothing throws if it does not. So a piece file always carries **every** bone of the skeleton, in the same order, with the same inverse binds, even bones no vertex of the piece follows. A piece has no clips. The `model` export stays the merged whole, which is enough for the first character.

**Enforced by:** T-50's test comparing bone lists and inverse binds across the model and piece files.
**Rejected:** trimming a piece's skeleton to the bones it uses, which misaligns the palette silently.

### Carve does not assume a humanoid

**Certainty:** Settled · **Strictness:** Rule

Bone names, order and shape are the caller's. Nothing in Carve requires a humanoid or a preset: Orcward's dragon has its own rig and clips. Orcward names humanoid bones by the engine's Synty preset and writes any `*.humanoid.json` itself. Carve writes no sidecars.

### Warnings for a game's limits

**Certainty:** Leaning · **Strictness:** Guideline

The limits are the game's, not Carve's (T-51, ruled 2026-11-13). They live in `carve-limits.json` in the game's content folder: `export` looks in the `.glb`'s folder and then each folder above it and uses the first it finds, or `limits` (a folder, or a `.json` file) names it. The file is a JSON object of optional keys `max_triangles` (the whole model), `max_submeshes` (submeshes, or distinct colours), and `min_height` and `max_height` in metres. With no file there are no limit warnings, and the result says no game limits were found; a key left out never warns. `export` warns, and never refuses, past a limit and never at it. Static exports warn on triangles and submeshes too; height is the rest bounds of all meshes, and applies only to a skeleton holding every bone the Synty preset requires, so a dragon on its own rig is not warned. A file that is not those keys with sane values is a tool error that writes nothing.

`write_limits: true` fills the keys a file is missing with Orcward's starting numbers (5,000 triangles, 8 submeshes or colours, 1.4 to 2.4 m), creating the file beside the `.glb` when none was named or found; it never changes a key already set, and is written only after the `.glb` is. Those numbers are a starting point to edit, never a default. `subdivide` no longer warns on triangles, since it cannot know the game.

**Alternatives:** Orcward's numbers as constants on skinned exports only, as T-34 shipped them, which are not general to every game; `export` arguments alone, which an agent must repeat on every call; limits stored in the Carve file, which belong to the game rather than the model.
**Would change if:** one game needs different limits for different kinds of model (characters and props), which would make the file hold named profiles.

### A loaded frame against the preview

**Certainty:** Leaning · **Strictness:** Rule

T-48 starts from Orcward's rule: a pixel differs when R, G or B differs by more than 8 of 255, and a frame fails past 0.5 % of its pixels. It is loosened only by the person, on measured differences.

**Ruling, 2026-11-13 (the person), after T-48 found a twisted hand passing the whole-frame count:** the rule keeps 8/255 and 0.5 % for shading noise, and also fails any frame with a connected patch of differing pixels larger than 50 px (8-connected, so a limb's diagonal outline stays one patch). A twisted limb is a patch; noise is scattered. 50 was kept because it separates with a wide margin: the largest patch in any good frame is 2 px, in any twist 130 px or more.

| Frames (512 x 512 = 262,144 px) | Differing px | Largest patch |
|---|---|---|
| ten good frames (rest front and side, Idle 0.5/1.0/1.5 s, Walk 0, 0.125, 0.25, 0.5, 0.875 s) | 0 each, Walk 0.25 s: 2 | 0 each, Walk 0.25 s: 2 |
| Hand_L twisted Y 45 / Y 90 / X 45 / Z 45 / Z 90 (rest, front) | 159 / 244 / 339 / 284 / 599 | 130 / 135 / 249 / 275 / 597 |
| swapped bone order (Walk 0.5 side / front; rest 0) | 4,027 / 3,147 | 4,027 / 2,456 |
| swapped inverse binds (Walk 0.5 side / front; rest 0) | 697 / 9,809 | 268 / 5,484 |
| reversed winding (rest / Walk 0.5 side / front) | 39,415 / 31,622 / 38,184 | 39,401 / 31,604 / 38,184 |
| dropped piece (rest) | 26,979 | 26,589 |

**Ruling, 2026-11-13 (the person), on wrong files:** the suite must fail on every wrong file, but which frame catches it may vary. At T-48 a rest frame was the bind pose with no palette, so a swapped bone order or swapped inverse binds were invisible at rest (0 px) and caught only in a clip frame (since 2026-11-17 `render_glb` skins a rest frame at the bind pose, and both fail at rest too: see below); seen from the side a shift in X lies along the view, so the swapped inverse binds are caught from the front (the cluster rule also catches them from the side). Each wrong file therefore fails at one frame at least.

**Ruling, 2026-11-17 (the person, M-19's third rebase, "Accept all"):** a pixel differs when its luma (Rec. 709 of the sRGB values, 0.2126 R + 0.7152 G + 0.0722 B) differs past 4 of 255, in place of "R, G or B past 8"; the 0.5 % and the connected-patch-past-50 limits are kept. Under M-7's sky a good frame (Walk 0.25 s, side) had a 23 px patch under the old measure, the same as the hand twisted Y 45, and 2 px by luma. Walk 0.25 s is a key, so by T-48's own note that was a bug and not noise; RV-52 found it (`render` lit a posed flat face by its polygon's normal, the game by its skinned corners) and it was fixed in M-19 (below), so every good frame now differs by 0 px. It applies wherever the rule is used: `ComparisonPng`, the Orcward scenario and its checks, and T-49's acceptance step.

| Frames, luma past 4 (measured on M-19's third-rebase sweep) | Largest patch |
|---|---|
| ten good frames | 2 px or less |
| Hand_L twisted (five) | 101 to 597 px |
| wrong files (each at its catching frame) | 268 to 26,231 px |

**Rulings, 2026-11-17 (the person, on M-19's review RV-52).** (1) "Add colour checks": luma cannot tell the gardener's trousers (`#3C54A0`) from its hat band (`#C0392B`), lit within 1.3 to 2.6 of each other, so Orcward's good frames also check a thigh face as trousers and a hat-band face as band, front and side, against the palette lit at each face's posed normal, and a file with the two swapped (`swapped-colours`) must fail on colour; `differences.tsv` gains a `colour` column. The frame rule itself is unchanged. (2) "Skin at bind pose": `render_glb` with no clip skins a skinned file at its bind pose, so a rest frame now checks the skin (above). (3) "Fix render in M-19": `render` lights a posed flat mesh by its skinned corner normals, as the game does (*Two samplers*).

| Orcward, after the RV-52 fixes (differing px / % / largest patch) | rest front | Walk 0.5 s side | Walk 0.5 s front |
|---|---|---|---|
| good frames and the control | 0 | 0 | not drawn |
| swapped bone order | 2,669 / 1.018 / 982 (fails) | 3,823 / 1.458 / 3,823 | 2,662 / 1.015 / 1,127 |
| swapped inverse binds | 10,361 / 3.952 / 5,185 (fails) | 543 / 0.207 / 268 | 9,581 / 3.655 / 5,313 |
| reversed winding | 27,809 / 10.608 / 18,995 | 22,446 / 8.562 / 18,592 | 29,242 / 11.155 / 19,029 |
| swapped colours (fails on colour at each) | 45 px | 77 px | 45 px |
| dropped piece | 26,642 / 10.163 / 26,231 | | |
| Hand_L twisted Y 45 / Y 90 / X 45 / Z 45 / Z 90 (rest front) | 127 / 0.048 / 100; 311 / 0.119 / 281; 502 / 0.191 / 502; 332 / 0.127 / 328; 574 / 0.219 / 570 | | |

**Alternatives:** counting only pixels on the character (a twist is 0.38 % to 1.42 % of the character's pixels, still under 0.5 % for the smallest); a smaller region or a lower limit. The cluster rule was chosen over both.
**Would change if:** a good frame shows a patch near 50 px, or a twist falls below it.

### Smooth shading is per mesh, with hard edges marked

**Certainty:** Leaning · **Strictness:** Rule

A mesh is `flat` (the default, as M-3 draws it) or `smooth`, with a set of hard edges. Normals are per face corner, averaged across soft edges. One Core function feeds both render and export, so they cannot disagree. This takes smoothing control out of M-14 and is the "edges marked with a tool" answer to its open question.

**Alternatives:** a smoothing angle, which is simpler to call but cannot crease one edge among similar ones.
**Would change if:** agents mark many edges by hand that an angle would have found; an angle can be added as a way of marking.

### Subdivide is a destructive edit

**Certainty:** Leaning · **Strictness:** Guideline

`subdivide` is Catmull-Clark as one edit, like `extrude`, keeping D-1's id rules: old ids stay, a face keeps its id as its first child, new elements are appended. New vertices take colours, groups and hard edges from their source and blend weights with the same stencil, then cap at four. This meets D-5's *Would change if*: a real game needs soft shapes first.

D-5's modifier question stays open for bevel. A later move to a modifier would change this decision.

**Alternatives:** a subdivision modifier kept in the file, which keeps the base mesh low-poly.
**Would change if:** agents want to keep editing the coarse cage after subdividing.

### Morph targets

**Certainty:** Leaning · **Strictness:** Rule

Decided by the person at M-21's planning (2026-11-16), on the planner's recommendation; T-99 built the Core type.

- **A target is sparse position deltas.** A mesh holds named targets in definition order, each a map from vertex id to a delta from the base position. A vertex not listed has a zero delta, and a delta of exactly (0,0,0) is never stored. Absolute positions are never stored. The Carve file stores the sparse deltas by vertex id, in a new version; the `.glb` writes dense accessors.
- **Names.** A target name is non-empty, holds no `:` (as a mesh name), is unique in its mesh, and is case-sensitive. The same name on two meshes of a model is one dial: one glTF target at export and one weight in `render`, the model's targets being the union of names in first-definition order.
- **Evaluation** is `base + Σ wᵢ·deltaᵢ`, targets added in definition order so the sum is the same bits every time. The morph comes **before** skinning, as glTF and Empyrean do it.
- **Normal deltas are never stored.** The export computes them from `Shading`, at weight 1 minus at rest.
- **In the `.glb`** (T-104), every export mode that writes a mesh (model, skinned, piece) writes one glTF target per
  name of the union on every primitive: dense `POSITION` and `NORMAL` deltas, no `TANGENT`, `mesh.weights` all 0,
  and the names in `mesh.extras.targetNames`, which is where Empyrean's `ParsedMorphTargets.TargetNames` reads them
  (probed against 0.129.0's loader). A primitive whose Carve mesh lacks a name gets zero deltas for it. Two corners
  share a written vertex only when their deltas agree too, so coplanar flat faces that a target bends apart, or
  coincident vertices it opens, are written apart. Every mesh is a primitive of the one glTF mesh (engine need 4),
  so Empyrean's refusal of targets on two or more glTF meshes is never met. A clips file has no mesh and so no
  targets; a model without targets writes the bytes it wrote before.
- **Every edit keeps targets.** `move` carries a target with its base; `extrude`'s new vertices copy their source's delta; `delete` drops a deleted vertex's delta; `merge_faces` and `bridge` keep targets; `plane_cut`, `loop_cut` and `knife` blend deltas by the stencils that blend weights. `subdivide` is exact: Catmull-Clark, crease rules included, is linear in positions, so each new vertex's delta takes its position's stencil, and the subdivided target equals the subdivided base plus the subdivided delta. `define_mesh` or `open` replacing a mesh that has targets is refused, naming them, unless `drop_targets: true`.
- **`check`** reports each target at weight 1: `target-flips-face` (a face's normal against its rest normal below 0), `target-collapses-face` (a face become zero-area) and `target-opens-mesh` (vertices coincident at rest come apart).
- **Out of scope:** corrective targets and morph weights in clips.

**Alternatives:** absolute positions per target, which round-trip a sculpted shape exactly but are dense and do not ride along with edits; dense deltas, which waste the file on the vertices a target does not move.
**Would change if:** a game needs corrective targets or weights keyed in clips, or the float rounding of a delta (at most one unit in the last place, T-99) shows in a real model.

### UVs stay possible

**Certainty:** Settled · **Strictness:** Guideline

No UVs yet. Nothing here may stop M-6 adding per-corner UVs: the export's vertex is built in one place, and the file's mesh object takes new keys in a later version. The engine's skinned vertex already has `TexCoord`.

## Open questions

- **How `render` frames a moving character.** The working assumption is the rest-pose bounds, so a clip does not swim. T-32 confirmed it on the two-box chain (`chain-bent-z`, scenario-crate): `render` with a `pose` frames on the rest bounds, and a quarter turn at the joint, which swings a 1 m box about the middle of the model, keeps every corner of the posed model inside the image (the check projects all eight). A fixed frame is also why a pose can leave it: a bend about a joint at the end of the model's extent (a root turned a quarter about a foot, say) carries geometry beyond the rest bounds and is cut off. That follows from the decision, and is the price of a frame that holds still, not a reason to change it; T-47 must match it.
- **Whether Carve's clips should keep a game-side `loop` flag.** glTF has none, so `loop` is only Carve's check. The game sets `Animator.Loop` itself.
- **Engine need 4**, in D-1: a skinned model of several meshes. If the engine gains it, one mesh stays valid and nothing has to change.
