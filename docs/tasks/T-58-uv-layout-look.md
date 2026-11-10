---
description: >-
  render mode uvs draws a mesh's UV layout flat over the dimmed checker, with seams, labels and highlight
---
# T-58 — UV layout look

**Status:** Open

**Pri:** P1

**Milestone:** M-6

**Batch:** — (standalone)

**Depends on:** T-57

**Verify:** server-unit, server-regression, scenario-cube, scenario-crate, scenario-shed, scenario-export, scenario-character

**Perf:** skip: Carve declares no heavy suites

## Goal

An agent sees a mesh's UV layout flat, as the texture will be laid out: islands, seams, overlaps and empty space, with face ids it can act on.

## Scope

- **`render` with `mode: "uvs"`** draws one mesh's layout, as agreed at planning (the sketch is in M-6's file). It is an Empyrean render with an orthographic camera on a flat scene.
  - **Window:** the unit square, grown to take in the UV bounds of the mapped faces, kept square and centred, with a margin of 1/16 of the window on each side. The frame is square, at `size` (256, 512 or 1024; default 512).
  - **Ground:** the checker tiled across the whole window (repeat), dimmed to 40%.
  - **Unit square:** border in white, its corners labelled `(0,0)`, `(1,0)`, `(0,1)`, `(1,1)`. u runs right and v down, so `(0,0)` is the top-left.
  - **Faces:** each mapped face is filled with the checker at full brightness at its own UVs, so the frame shows exactly what each face samples.
  - **Edges:** every edge of a mapped face is drawn white, and marked seams are drawn yellow `#FFD800` over them.
  - **`labels`:** `faces` puts each face's id at the centroid of its UV polygon, in the existing label style. Other kinds are refused in this mode.
  - **`highlight`:** faces are filled in the highlight orange.
  - **`mesh`** is required when the model has more than one mesh.
  - **Refused in this mode**, each a tool error naming the argument: `view`, `direction`, `zoom`, `target`, `projection`, `views`, `columns`, `pose`, `clip` and `time`.
  - A mesh with no UVs is a tool error that names `set_uvs`, `project_uvs` and `unwrap`.
  - The caption is `uvs <mesh>`.
- **The description is content:** how to read the layout (bright is used, dim is empty, overlapping fills mean a shared texture area, yellow is a seam), with a worked example.
- **Frames in `scenario-crate`**, on T-57's crate UVs with one seam marked:
  - the layout at 256, 512 and 1024;
  - with face labels, and with one face highlighted;
  - one with a face's UVs set outside 0..1, so the window grows.
- **Pixel checks**, worked from the UVs given:
  - the white origin cell at the unit square's top-left, and not at its mirror (this fails a v-up layout);
  - a face's interior bright and an unused cell dim;
  - yellow on the seam's UV segment;
  - the highlighted face orange.

  Work every check before writing it, and stop if one cannot pass.
- **Tests:** `regression`: every refusal, the no-UV error, two meshes with and without `mesh`, and the sizes.

## Notes

- Opus: a new kind of frame, unlike any view of the model.
- Follows T-57 in `RenderTool.cs`, `RenderSystem.cs` and the crate look files. Runs alongside nothing else in those files.
- **Write down for the dependants:** the window rule as built, the colours, and the frame hashes. T-61 checks the barrel's layout against them.
- No sound, motion or feel: the person's look at the frames is the whole review.
- **Carried from earlier plans:** as T-57's Notes (the hash guard on every suite, a positive and a mirror-failing check per frame, a break per check as a table, `finish` read whole, artifacts by job id). Evidence goes in the milestone worktree's `artifacts/review/T-58/`.
- **Open rules: stop and ask.** Anything the milestone's sketch and *Decided at planning* do not settle, any loosened check, and any change to the agreed colours or layout.
- Run `finish` from inside the worktree and leave the folder.
