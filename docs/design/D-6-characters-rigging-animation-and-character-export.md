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

Carve's preview poses on the CPU in `Carve.Core` and draws the result as a plain mesh. `render_glb` loads the exported file through the engine's own loader and draws it with GPU skinning. The two share no skinning code, so a frame that matches proves the export and not only a path agreeing with itself.

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

T-48 starts from Orcward's rule: a pixel differs when R, G or B differs by more than 8 of 255, and a frame fails past 0.5 % of its pixels. It is loosened only on differences measured at T-48, and only by the person. A wrong bone, a twisted limb or a broken silhouette must always fail; shading noise need not.

**Alternatives:** counting only pixels on the character. A 512 x 512 frame allows about 1,300 differing pixels, which may be enough to hide a small limb. T-48 shows whether it is.
**Would change if:** a small twisted limb passes the whole-frame rule.

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

### UVs stay possible

**Certainty:** Settled · **Strictness:** Guideline

No UVs yet. Nothing here may stop M-6 adding per-corner UVs: the export's vertex is built in one place, and the file's mesh object takes new keys in a later version. The engine's skinned vertex already has `TexCoord`.

## Open questions

- **How `render` frames a moving character.** The working assumption is the rest-pose bounds, so a clip does not swim. T-32 confirmed it on the two-box chain (`chain-bent-z`, scenario-crate): `render` with a `pose` frames on the rest bounds, and a quarter turn at the joint, which swings a 1 m box about the middle of the model, keeps every corner of the posed model inside the image (the check projects all eight). A fixed frame is also why a pose can leave it: a bend about a joint at the end of the model's extent (a root turned a quarter about a foot, say) carries geometry beyond the rest bounds and is cut off. That follows from the decision, and is the price of a frame that holds still, not a reason to change it; T-47 must match it.
- **Whether Carve's clips should keep a game-side `loop` flag.** glTF has none, so `loop` is only Carve's check. The game sets `Animator.Loop` itself.
- **Engine need 4**, in D-1: a skinned model of several meshes. If the engine gains it, one mesh stays valid and nothing has to change.
