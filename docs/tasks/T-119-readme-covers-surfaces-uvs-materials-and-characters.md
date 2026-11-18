---
description: >-
  README covers surfaces, UVs, materials and characters
---
# T-119 — README covers surfaces, UVs, materials and characters

**Status:** In Progress

**Pri:** P2

**Milestone:** — (off-milestone)

**Batch:** — (standalone)

**Depends on:** — (none)

## Goal

The README stops at mesh editing, asking the mesh and cutting. A reader cannot
tell that Carve also shades, unwraps, textures, rigs and animates. Bring it up
to 0.11.0.

## Scope

- A section each, in the style of *Asking the mesh* and *Cutting* (one or two
  lines per tool, a minimal JSON example taken from the tool's own
  description): surfaces (colours, groups, smooth shading, hard edges,
  subdivide), UVs (set_uvs, mark_edges, project_uvs, unwrap, checker and uvs
  looks), materials (defining, assigning, texture paths, export), characters
  (skeleton, weights, weigh, clips, get_clips, export modes, render_glb).
- Every JSON example matches the parameter names the server accepts.
- No code changes.

## Notes

The sections' JSON examples come from each tool's own description, except
where that description is wrong or does not fit one running example:

- `define_group` and `set_material` name the wrong unit-cube faces in their
  own examples (B-6); the README uses the correct ids, `[0,1,4,5]` for the
  sides and `[1,3]` for the front and top.
- The Characters section uses one small figure (meshes `body` and `hat`, the
  skeleton from `define_skeleton`'s example) so its examples run in order.

### As built

Four README sections after *Cutting*: several meshes, colours and smooth
surfaces; UVs; materials and textures; characters (skeleton, weights, clips,
the three export modes, `render_glb`, game limits). The walkthrough's export
step now says a second export needs `overwrite: true`.
