---
description: >-
  Review of T-119
---
# RV-57 — Review of T-119

**Status:** Answered

**Reviews:** T-119

**Round:** 1

**Previous:** —

**Diff:** a81059a715d9769cdf8bea2f83217da688e434e4 against 7a531be

**Reviewer:** claude-opus-5-5

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings

### The define_group example calls top and bottom "sides"

README.md:98 (Correctness). `{"name":"sides","faces":[0,1,2,3]}` is copied from `define_group`'s
own example (DefineGroupTool.Text, "naming the four sides"). On define_mesh's unit cube
(DefineMeshTool.CubeExample), face 0 is z = 0 (back), 1 is z = 1 (front, +Z), 2 is y = 0 (bottom)
and 3 is y = 1 (top). Faces 0 to 3 are back, front, bottom and top, so they are not the four sides.
The four vertical sides are `[0,1,4,5]`. Either change the README's ids or rename the group. The
tool description has the same error, and a docs-only task cannot fix it, so it needs a follow-up bug.

**Outcome:** fixed

The README's example is now `[0,1,4,5]`, named as the -Z, +Z, -X and +X faces; the tool description's error is B-6.

### The materials walkthrough departs from set_material's example, which is wrong itself

README.md:121-123 (Correctness / Scope bullet 1). The README uses faces `[1,3]` and calls them "the
unit cube's front and top". That is right: face 1 is +Z, which export's description and README.md:88
both call the front, and face 3 is +Y. But the Scope asks for examples "taken from the tool's own
description", and SetMaterialTool.Text (MaterialTools.cs:201-205) uses `[2,3]` for "its top and
front". Face 2 is the bottom, so the tool's example is wrong. The same text also lists the faces
left without UVs as "0, 1, 4 and 5". The README should keep its version. The departure, and the bug
in set_material's description, should be recorded in the task (see the tracker finding below), and a
bug should be filed against the description.

**Outcome:** fixed

Kept the README's correct `[1,3]`, recorded the departure in T-119's Notes, and filed B-6 for set_material's description.

### The character examples use bone names that the skeleton above them does not have

README.md:136, 140, 144, 148, 154, 156 (Correctness). The define_skeleton example defines `hips`,
`spine` and `head`. The examples after it use bones and names that do not exist there:
- set_weights uses `"Head"` (capital H) on a group `"hat"` that the section never defines.
- define_clip keys bone `"joint"`, which comes from define_clip's own two-bone chain (base, joint).
- render_glb uses clip `"Walk"`, and the piece export uses mesh `"Hat"`.

If a reader runs these calls in order, the server refuses them: define_clip errors on "a bone the
skeleton lacks" and set_weights on "an unknown bone name". The Cutting section says which model its
examples run on (README.md:83), but this section does not. Either say that each example comes from
its own tool's description on that tool's model, or rename the examples so they share one skeleton.

**Outcome:** fixed

The section now states one figure (meshes `body` and `hat`, the hips/spine/head skeleton) and every example uses it: weigh on `body`, set_weights binding the hat's 8 vertices to `head`, a `nod` clip on `head`, the piece and render_glb on `figure`.

### The define_clip example leaves out where `interpolation` goes

README.md:141-145 (Correctness, minor). The text says "interpolation is `LINEAR` or `STEP`". The
example is define_clip's own (ClipTools.cs:20) with `"interpolation":"LINEAR"` removed. That still
works, because LINEAR is the default, but a reader can no longer see that interpolation is set per
channel part (`rotation: {interpolation, keys}`) rather than per clip. Keep the field as the tool's
example has it, or say where it goes.

**Outcome:** fixed

The clip example carries `"interpolation":"LINEAR"`, and the text says each channel part has its own interpolation.

### Game limits: where export finds the file is wrong, and how to create one is missing

README.md:158 (Correctness / missing). The README says the limits are "read from
`carve-limits.json` in the game's content folder". ExportTool.Text says export looks in the .glb's
own folder and then in each folder above it, or in the file that `limits` names. With no file there
are no limit warnings, and `write_limits: true` creates the file with starting numbers. A reader
told that export warns on limits has no way, from the README, to get a limits file in the first
place. Mention `limits` and `write_limits`.

**Outcome:** fixed

The README says export takes the first `carve-limits.json` in the .glb's folder or above it, or the one `limits` names, and that `write_limits: true` creates it.

### Re-exporting needs `overwrite: true`, and the README never says so

README.md:58-60, 150-156 (missing; this text predates T-119). Export refuses to replace a file that
already exists unless `overwrite: true` is given (ExportTool.Text, and the `overwrite` property in
its schema). The README's workflow is "edit the `.carve` file and export again" (line 60), and the
new export-mode examples (line 154) follow the same pattern, so the second export of any model
fails. This is outside T-119's Scope, so it may be skipped here and taken up as a follow-up.

**Outcome:** fixed

The walkthrough's export step says a second export needs `"overwrite":true`; one clause, directly needed by the re-export the README describes.

### The barrel sample's materials are named loosely

README.md:127 (Correctness, nit). The README says "wood, iron, end-grain and rune materials".
samples/barrel-textured.carve names them `staves`, `iron`, `end-grain` and `rune-stave`. Since two
of the four are given by their real names, give all four that way.

**Outcome:** fixed

Named `staves`, `iron`, `end-grain` and `rune-stave`.

### The task file has no As built or Notes

docs/tasks/T-119-readme-covers-surfaces-uvs-materials-and-characters.md (Tracker hygiene). `## Notes`
is empty and there is no `### As built`. It should record the deliberate departure from
set_material's example and the two tool-description errors it found (define_group, set_material), so
that the follow-up bugs are not lost. Status In Progress is right for a branch not yet finished, and
`cairn lint` is clean.

**Outcome:** fixed

T-119 has Notes recording both departures and B-6, and an As built.

## Summary

This is a docs-only change and the diff touches README.md alone, with no scope creep. Every Scope
bullet is met in the README: surfaces, UVs, materials and characters each have a section covering
the listed tools. Because nothing here is code, "tests cover the Scope" is satisfied by that.

I checked every parameter name and value in the new JSON examples against the tool schemas and
`UnknownArguments` lists. All are accepted. The behaviour claims also match the tool descriptions:
- shading, and export using the same normals as render;
- the checker and uvs looks, magenta for faces without UVs and yellow for seams;
- check's UV warnings;
- material defaults and texture paths, and what export writes in its model, clips and piece modes;
- the bones and weights overlays;
- render_glb lining up with render.

The four samples named, and samples/textures/, all exist.

The face ids in set_uvs, project_uvs, mark_edges and the materials walkthrough are right for the
unit cube. The define_group example is not: its "sides" are the cube's back, front, bottom and top.
The character examples do not run in order against the skeleton they follow. Two tool descriptions,
define_group and set_material, have face-id errors that need bugs of their own.
