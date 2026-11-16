---
description: >-
  A model holds named PBR materials, faces are assigned one, and edits, undo and the Carve file carry them with texture paths relative to the file
---
# T-74 — Materials in the kernel and the file

**Status:** In Progress

**Pri:** P1

**Milestone:** M-7

**Batch:** — (standalone)

**Depends on:** — (none)

**Verify:** core-unit, server-unit, server-regression, scenario-cube, scenario-crate, scenario-shed, scenario-export, scenario-character, scenario-soft, scenario-uvs, scenario-cuts, scenario-gatehouse, scenario-rig

**Perf:** skip: Carve declares no heavy suites

## Goal

The kernel holds named, model-wide PBR materials and each face's assignment, carried through every edit, undo and the Carve file, so the tools, export and render (T-75, T-76, T-78) all build on one record.

## Scope

- **`Carve.Core` gains `Material`** (immutable record), every field the engine's glTF loader reads (milestone, *Decided at planning*):
  - `Name`;
  - `BaseColour` sRGB hex `#RRGGBB` (default `#FFFFFF`, glTF's), `Metallic` (default 0), `Roughness` (default 1), `Emissive` sRGB hex (default `#000000`), `Occlusion` strength (default 1), `DoubleSided` (default false);
  - five optional textures: `BaseColourTexture`, `MetallicRoughnessTexture`, `NormalTexture`, `EmissiveTexture`, `OcclusionTexture`. A texture is its absolute path, its PNG bytes as read when the material was defined, and its width and height.
  - Validation, each a named problem like `FaceStyle.ColourProblem`: hex as `FaceStyle` checks it; `Metallic`, `Roughness` and `Occlusion` finite and in [0, 1]; a texture's bytes start with the PNG signature and its size is 1 to 4096 on each side (read from `IHDR`; the Core decodes nothing else).
  - Name rules: not empty, and no `:` or `#`, and not `carve`, so a material can never take the name of an exported colour material (`carve`, `carve #rrggbb`, `<mesh>:<group>`, `<mesh>:<rrggbb>`).
- **The model holds the materials**, an ordered list by definition order. Defining a name that exists replaces it in place. Materials belong to the model, not a mesh, and any mesh's faces may use any of them.
- **A face has a colour or a material, not both.** Extend `FaceStyle` (or what T-71 made of it, if it has landed) with a face's material name. Assigning a material clears that face's colour, and assigning a colour clears its material. An unassigned face is unchanged (Carve's surface grey).
- **Edits carry the assignment** as they carry colours today: `move` keeps it, `extrude`'s cap and walls take their source face's as a colour does, `subdivide`'s child faces take their parent's, and `undo` restores it. If T-71's registry has landed, it is one more face layer with the colour's rules.
- **Removing a material** is out of scope. A material no face uses stays defined and is saved.
- **The Carve file:** the next version after whatever has landed (M-6's T-53 and M-5's T-63 each take one; read `CarveFile` first). It is written only when the model has a material, so every existing file and sample stays at its version, byte for byte.
  - A top-level `materials` array in definition order. Each holds `name`, every field that differs from its default, and each texture as a path relative to the `.carve` file's folder, with `/` separators.
  - A mesh's faces carry their material name beside their colour, in a key left out when no face has one.
  - Reading resolves each path against the file's folder and reads the PNG. A missing or unreadable texture fails `open` with the path the file gave and the absolute path tried.
  - Reading checks every rule above and that every face's material is defined.
- **Tests (`unit`):**
  - every default, every validation problem, and each name rule;
  - replacing a material in place keeps its position;
  - colour and material clearing each other;
  - carrying through `move`, `extrude`, `subdivide` and `undo`;
  - a round trip by content, with textures beside the file and in a subfolder;
  - a missing texture on read;
  - the older versions still read;
  - every committed sample and fixture reads and writes unchanged.

## Notes

- **Opus.** It decides the record and the file format that T-75, T-76, T-78 and T-80 build on. Plan for two agents.
- **Write down for the dependants**, in this file's As built: the types and their API, how a face's material is read, the file version and keys, and how texture bytes are held (T-76 embeds them and T-78 uploads them).
- In the first wave it owns `Carve.Core` and `tests/Carve.Core.Tests`'s material and file tests. T-77 runs beside it and owns `Carve.slnx`, `tests.json` and `CLAUDE.md` then, so this task edits none of them.
- The PNG bytes are held from the moment of definition so that export and render are deterministic: a file changed on disk later does not change the model until it is defined again or reopened.
- **Before the first code**, read this Scope against what M-6 (T-53's UV layer and file version), M-5 (T-63) and T-71, if landed, actually built. Stop if they disagree.
- **Open rules: stop and ask.** These are the open rules, and there is a stop on each:
  - anything the milestone's *Decided at planning* does not settle;
  - any change to how an existing colour, group or edit behaves;
  - any change to an existing file's bytes;
  - any loosened check.
- **Carried from earlier plans:**
  - the byte-identical guard on every existing frame and committed file, by hash or by content as M-16 ruled, before and after;
  - each new check shown failing on a broken product, as a table;
  - read `finish`'s output whole and give its `Merge task/T-NN` line;
  - fetch queued artifacts by job id;
  - report a `verify` that waits more than a few minutes;
  - evidence in the milestone worktree's `artifacts/review/T-74/`.
- Run `finish` from inside the worktree and leave the folder.

### Plan

Researched 2026-11-16 against the milestone worktree (M-6 and M-5 landed; T-71 still Open, so `FaceStyle` is extended directly). Items marked **(Q)** wait on the open questions at the end.

**Landed facts this plan rests on.**

- `CarveFile.Version` is 5 (T-53), and `ReadableVersions` is 1 to 5. Version 4 is T-63/T-64's. No live milestone branch (M-19 is the other active one) touches `CarveFile.cs`, so **version 6** is free.
- The writer already picks "the lowest version that holds the model" (`CarveFile.Write`, around line 117). Version 6 goes at the top of that chain.
- Core does no file I/O today. `CarveFile.Read(string text)` is text only, and `OpenTool`/`SaveTool` do the disk work.
- Every edit carries face data through only these paths:
  - `FaceStyle.Carry(made)`: extrude walls, subdivide children, bridge quads, and knife, loop cut and plane cut through `FaceSplit` (line 599) and `EditResult.CarriedRest`;
  - `FaceStyle.Without(faces)`: tombstones from delete, merge_faces and cuts;
  - `MeshEdits` subdivide levels, which reuse `level.Style`.
  
  `move` keeps the style as it is. `undo` restores whole `Model` snapshots (`ModelStore._history`), so anything on `Model` or `FaceStyle` is undone for free.
- `Model.Combined` (render and static export) rebuilds the style from `FaceStyle.ColoursOffset`/`OfColours`. That copies colours only, not groups. `Skinning.Posed` rebuilds the model from `Model.Empty`, which drops everything but meshes. Both must carry materials.
- `Model.Piece` (`save mesh=`) and `Model.WithMeshFrom` (`open mesh=`) build models from parts.

**Types (all in `src/Carve.Core`).**

- `Material.cs`:
  - `sealed record MaterialTexture(string Path, ImmutableArray<byte> Png, int Width, int Height)`.
    - `Path` is absolute.
    - Equality is by `Path`, `Width`, `Height` and the bytes (`SequenceEqual`). Override `Equals`/`GetHashCode`; `ImmutableArray` compares by reference.
    - `static MaterialTexture? FromPng(string absolutePath, byte[] bytes, out string? problem)` checks the signature `89 50 4E 47 0D 0A 1A 0A`, that the first chunk is `IHDR` at offset 12, and that width and height (big-endian, offsets 16 and 20) are each 1 to 4096. It decodes nothing else.
    - `static MaterialTexture? Load(string absolutePath, out string? problem)` is Core's only I/O: `File.ReadAllBytes`, then `FromPng`.
  - `sealed record Material(string Name, string BaseColour = "#FFFFFF", float Metallic = 0, float Roughness = 1, string Emissive = "#000000", float Occlusion = 1, bool DoubleSided = false, MaterialTexture? BaseColourTexture = null, … MetallicRoughnessTexture, NormalTexture, EmissiveTexture, OcclusionTexture)`.
    - Hex is kept as given, as `FaceStyle` keeps colours.
    - `static IReadOnlyList<(string Field, string Problem)> Problems(Material m)` names the field in snake case (`base_colour`, `metallic`, …), so T-75 and the reader name the argument or element.
    - Named checks: `NameProblem(string?)` (empty; contains `:` or `#`; equals `carve` ordinal), `FactorProblem(string field, float)` (finite and in [0, 1]), and hex through `FaceStyle.ColourProblem`.
    - Name rule **(Q1)**.
- `FaceStyle`:
  - Add `SortedDictionary<int, string> _materials`, with `Materials` (face id to name), `MaterialOf(face)` and `IsEmpty` counting it.
  - `WithMaterial(faces, name)` sets the material and removes those faces from `_colours`.
  - `WithColour` also removes those faces from `_materials`. It is unchanged for any face without a material, so no existing behaviour changes.
  - `Carry` copies the source face's material the same way it copies its colour. `Without` drops it.
  - Replace `ColoursOffset`/`OfColours` with a pair that carries colours and materials (`LooksOffset`/`OfLooks`). Groups stay uncarried, as today.
  - The constructor stays private. `None` is unchanged.
- `Model`:
  - Add `ImmutableArray<Material> _materials` to the private constructor, and thread it through all six `new Model(` sites, `Empty` included.
  - `IReadOnlyList<Material> Materials`, `FindMaterial(name)`, and `WithMaterial(Material)`, which replaces in place or appends and returns `(Model, bool Replaced)` or exposes `Replaced` some other way for T-75.
  - `Create` gains a `materials` parameter. A new overload keeps the old signature, so existing callers compile. It checks:
    - unique names: `materials[i].name`;
    - every face's material is defined: `meshes[i].materials[k]`.
  - `Combined` carries materials through `LooksOffset`.
  - `Piece` keeps the materials **(Q2)**, and `WithMeshFrom` merges them **(Q3)**.
  - `Skinning.Posed` starts from `Model.Empty.WithMaterials(model.Materials)`. Add an internal `WithMaterials(list)`.
- `PolygonMesh` itself is untouched: the assignment lives in `FaceStyle`.
- How a dependant reads a face's material: `mesh.Style?.MaterialOf(f)` gives a name, then `model.FindMaterial(name)` gives the record. `GlbExport` gets only `Model.Combined`'s mesh today, so T-76 must pass the model or its materials as well (see T-76's note).

**Carve file version 6.** It is written only when `model.Materials.Count > 0`, at the top of the version chain. Otherwise nothing about the text changes.

- Top-level keys are `carve`, `materials`, `meshes`, `skeleton`, `clips`; `FileKeysV6` adds `materials` after `carve`, so definitions come before use.
- Each material is one object per line in definition order, with `name` first and then only the fields whose value is not the default, in record order.
  - The default test is ordinal on the hex text (`#ffffff` is written, so it reads back as given) and bitwise on floats.
  - Floats use the writer's existing `Number` (shortest round-trip).
  - Textures are written as `"base_colour_texture": "tex/wood.png"`.
  - Example: `{"name": "wood", "roughness": 0.8, "base_colour_texture": "tex/wood.png"}`.
- Mesh key `materials` (in `MeshKeysV6`) comes right after `colours` and has the same shape: `[{"material": "wood", "faces": [..]}]`. Entries are grouped by name and ordered by first face. The key is left out when the mesh has no material face, so the comma logic mirrors `uvs`/`seams`.
- Paths: `Path.GetRelativePath(folder, texture.Path)` with `\` replaced by `/`. A texture on another drive, or one with no relative form **(Q4)**.
- API:
  - `Write(Model)` keeps its signature and throws `ArgumentException` when a material has a texture.
  - `Write(Model, string folder)` is new; `SaveTool` passes the destination folder (a one-line change, done in T-75's wave, or here if T-75 agrees).
  - `Read(string text)` keeps its signature and refuses a file with a texture: `materials[i].base_colour_texture: a texture needs the file's folder`.
  - `Read(string text, string folder)` resolves each path against `folder` with `Path.GetFullPath(Path.Combine(folder, p))` and loads it with `MaterialTexture.Load`, reading each distinct absolute path once per read.
  - A missing or unreadable texture is the error `materials[i].<field>: cannot read 'tex/wood.png' (D:\…\tex\wood.png): <reason>`.
  - Every `Problems` rule is an error at `materials[i].<field>`. An unknown key is refused.
  - A face's undefined material is `meshes[m].materials[k].material: no material named 'x'; the materials are …`. A face given two materials, or a colour and a material, is an error at the face, as a twice-coloured face is today.
- Fix the stale docs while there: `CarveFileResult.Version` ("1 to 4") and `Read`'s remarks ("versions 2, 3 and 4").

**Edits.** No edit file changes. `Carry`/`Without` give move, extrude, subdivide, bridge, the cuts and delete the new behaviour. `Combined`, `Posed` and undo come from the `Model` work.

- `merge_faces`: the survivor keeps its material, as it keeps its colour. The colour warning in `FaceMerge.cs` line 225 compares `ColourOf` only **(Q5)**.

**Tests (`tests/Carve.Core.Tests`, unit).**

- `MaterialTests`:
  - each default;
  - each `Problems` entry: bad hex in both colours; NaN, -0.01 and 1.01 for each factor; a non-PNG; a PNG of width 0 and one of 4097; a first chunk that is not `IHDR`;
  - each name rule;
  - texture equality by bytes.
  - Build small PNGs in code (signature and `IHDR`, plus `IEND`; Core never decodes them).
- `ModelMaterialTests`:
  - replace keeps the position;
  - `WithMaterial` clears colour and `WithColour` clears material, faces not named are untouched;
  - carry through move, extrude (cap and walls), subdivide (one level and two), loop cut and delete;
  - `Combined` and `Posed` keep the assignment;
  - an undo-style snapshot equality.
- `MaterialFileTests`:
  - a round trip by content (`Write` text equal after `Read`, plus bytes equal) with textures beside the file and in `tex/`, saved to a different folder (the paths re-relativise);
  - a missing texture, which gives the error text with both paths;
  - an undefined face material;
  - every field at its default writes only `name`;
  - a model with an unused material is still written as version 6.
- Byte-identical guard:
  - versions 1 to 5 still read (the existing tests stay green);
  - every committed `.carve` (`samples/*.carve`, `tests/Carve.Core.Tests/Fixtures/*.carve`) reads and writes to its exact bytes, with each file's SHA-256 in the evidence before and after;
  - no frame changes, because `RenderSystem` still sees only colours (material faces draw grey until T-78).
- For "each new check shown failing": break `FromPng`'s size bound, the name rule, `WithColour`'s clearing, `Carry`'s material copy and the relative-path write, one at a time.

**Two agents.** Agent 1 does `Material`, `FaceStyle` and `Model`, with their tests. Agent 2 does `CarveFile` v6 and the file tests, after agent 1's types compile.

**Open questions (put to the person before the first code).**

- **Q1. A material named like a mesh.** A skinned export names a piece's uncoloured material after the bare mesh name (`SkinnedExport.Materials`, line 128), so a material `barrel` on a mesh `barrel` would give two glTF materials one name. The Scope's name rules miss this. Options:
  - (a) Refuse a material name equal to any mesh name at `define_material` and on read. The reverse (defining a mesh named like a material) would then need refusing too, which changes `define_mesh`.
  - (b) Leave the names alone and let T-76 rename the uncoloured skinned primitive only in a model that clashes (no existing export changes).
  - (c) Allow the duplicate if the engine's loader does not key materials by name.
  
  Recommend (b), which changes no existing behaviour.
- **Q2. `save mesh=`.** Which materials go into a one-piece file? Options: (a) only those its faces use, in model order; (b) all of them. Recommend (a), since the piece file "opens on its own".
- **Q3. `open mesh=` of a mesh whose material name is already defined differently.** Options:
  - (a) refuse, model unchanged, naming the material;
  - (b) the file's definition replaces the model's in place;
  - (c) keep the model's.
  
  An undefined name is appended in every case. Recommend (a).
- **Q4. A texture with no relative path from the save folder** (another drive). Options: (a) refuse `save` naming the texture; (b) write it absolute. Recommend (a), since the decision says relative.
- **Q5. merge_faces' colour warning.** A material face merged into a coloured one is reported as "colour none". Options: (a) extend the warning to say `material 'x'` (a changed warning text); (b) leave it. Recommend (a).

### Rulings (2026-11-16, the person, on the research agent's open questions)

- `save mesh=` writes only the materials that mesh's faces use.
- `open mesh=` whose file defines a material the model already has under the same name but differently is refused, naming the material.
- A texture with no relative path from the save folder (another drive) refuses the save, naming the texture.
- For T-76: a material named like a mesh is allowed; skinned export renames the uncoloured primitive's material only when it clashes.
- For T-75: `merge_faces`' warning names a face's material; `check`'s textured-without-UVs warning replaces the generic unmapped-face warning on that face.
- For T-76: export writes `TEXCOORD_0` whenever a used material has a texture.
