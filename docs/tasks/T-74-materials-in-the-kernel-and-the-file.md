---
description: >-
  A model holds named PBR materials, faces are assigned one, and edits, undo and the Carve file carry them with texture paths relative to the file
---
# T-74 — Materials in the kernel and the file

**Status:** Open

**Pri:** P1

**Milestone:** M-7

**Batch:** — (standalone)

**Depends on:** — (none)

**Verify:** core-unit, server-unit, server-regression, scenario-cube, scenario-crate, scenario-shed, scenario-export, scenario-character, scenario-soft

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
