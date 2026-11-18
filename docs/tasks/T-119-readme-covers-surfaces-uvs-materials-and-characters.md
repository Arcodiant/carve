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
