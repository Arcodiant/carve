---
description: >-
  An agent colours faces and named face groups, and sees the colours in render and as glTF materials
---
# T-30 — Face colours and groups

**Status:** Done

**Pri:** P1

**Milestone:** M-16

**Batch:** — (standalone)

**Depends on:** T-29

**Verify:** server-regression, scenario-cube, scenario-crate

**Perf:** skip: Carve declares no heavy suites

## Goal

A character is not grey: an agent colours faces and named face groups, and sees those colours in `render` and in an exported `.glb`.

## Scope

- **Core:** per-face colour and named face groups on a model mesh, as D-6's *Colour is a material* says. Colours and groups follow a face through `extrude`: walls and caps take the source face's.
- **Tools:** `define_group(mesh, name, faces)` and `set_colour(mesh, faces | group, colour)`, with `colour` as sRGB hex, `#RRGGBB`, kept as given and converted to linear for drawing and export. `get_mesh` reports both.
- **Render:** a submesh and material per colour. An uncoloured model draws exactly as it does today.
- **Export:** a material per colour with metallic 0 and roughness 1, on T-16's static path.
- **Sample:** the crate with coloured frame and panels, in `scenario-crate`, added as new frames beside the old ones.
- **Tests:** `unit` for colour through `extrude` and undo, the sRGB to linear conversion against known values, and a malformed hex refused; `Parse` of the export for base colours by submesh; a pixel check of each colour in a frame.

## Notes

- **Carried from the M-1 to M-4 plans:** the byte-identical guard on every existing frame, by hash before and after; a positive pixel check on every new frame, and one a mirrored or flipped camera would fail; each new check shown failing, as a table.
- **Open rules: stop and ask.** Anything D-6 does not settle, any loosened check, and any change to a D-6 decision.
- Run `finish` from inside the worktree and leave the folder.

### As built

One agent, on Sonnet 5.5.

- **Core:** `FaceStyle` (`src/Carve.Core/FaceStyle.cs`) is immutable: face id to `#RRGGBB` as given, and named groups of face ids in definition order. It hangs on the mesh (`PolygonMesh.Style`, null when empty; `WithStyle` shares the geometry). `MeshEdits` carry it through `Move` and `Extrude`: ids never change, so colours and groups stay put, and each wall takes its source face's colour and joins its groups. `Model.Combined` carries colours (offset by face) so a several-mesh render keeps them; groups are not carried into it. `FaceStyle.ToLinear` is the sRGB curve; `ColourProblem` is the `#RRGGBB` check (six hex digits, either case).
- **Store:** `ModelStore.Restyle` changes a mesh's style as one undo step. A new `define_mesh` or `open` of a name starts it uncoloured.
- **Tools:** `define_group(name, faces, mesh?)` and `set_colour(colour, faces | group, mesh?)` (`FaceStyleTools.cs`), registered in `Program.cs`. `get_mesh` adds `colours:[{colour, faces}]` and `groups:[{name, faces}]`.
- **Render:** in the shaded look a mesh with coloured faces is one index buffer cut into a submesh per colour (uncoloured first, then in order of first face) with a `LitMesh` material each, set as `SubmeshMaterials`. Vertices stay in triangle order, so highlight vertex colours still line up. A mesh with no colours takes the old path unchanged. Wireframe and normals looks are unlit and ignore colour.
- **Export:** one primitive and material per colour, `carve` for uncoloured faces and `carve #RRGGBB` for each colour (hex in either case is one material), metallic 0, roughness 1, opaque, `baseColorFactor` linear. The result line names every material. An uncoloured mesh's `.glb` is byte-identical to before.
- **Sample:** `CrateColours.cs` in `scenario-crate`, run last: the crate built again, groups `panels` (faces 0 to 5) and `frame` (the rest), blue `#3C78C8` and wood `#C8503C`; frames `crate-coloured-front`, `crate-coloured-front-again`, `crate-coloured-dir-0.4-2-0.8`. Four refusals are checked.
- **Pixel check:** a lit channel is surface colour times one light factor, so each sampled pixel, in linear light, must equal the grey frame's pixel of the same view and point times colour over grey (0.72, 0.72, 0.70), within 8 percent. Points are projected from the mesh (lid panel, +Z band, +X band, front view; lid panel and rim, tilted view), so a mirrored camera lands on the wrong face.
- **Readings the spec left open:** the Carve file does not keep colours or groups (the spec lists no file change, and the file's byte layout is a settled rule), so `save` then `open` loses them. *Superseded by T-35:* the version 2 file keeps colours and groups (RV-24 A7). `export` of several meshes is still refused (*superseded by T-34*, which merges them; RV-24 A7). Group names must be non-empty and are otherwise free. A face may be in several groups. Colour after colour: the last wins.

**Checked.** `server-regression`, `scenario-cube`, `scenario-crate` pass; all 33 existing crate frames are byte-identical to before (`hashes-before.txt`, `hashes-after-crate.txt` in `M-16/artifacts/review/T-30/`). Break table (`break-table.txt`): nine breaks, each failing the new unit tests or the new pixel check. Coloured frames in the same folder.
