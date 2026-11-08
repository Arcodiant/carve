---
description: >-
  An agent colours faces and named face groups, and sees the colours in render and as glTF materials
---
# T-30 — Face colours and groups

**Status:** In Progress

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
