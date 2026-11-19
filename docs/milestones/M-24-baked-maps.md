---
description: >-
  Carve bakes occlusion, bevel normals and id masks into a mesh's UV layout as PNGs a material can name
---
# M-24 — Baked maps

**Status:** Active

**One-liner:** *Carve bakes occlusion, bevel normals and id masks into a mesh's UV layout as PNGs a material can name*

## What this milestone delivers

An agent asks Carve for maps computed from the mesh, and names them in a material.

- Ambient occlusion into the UV layout, with ray count and reach.
- A normal map that rounds hard edges by a given width.
- A mask per group or material, and a curvature map for wear.
- Padding past island borders, and output paths a material from [M-7](M-7-materials-and-textures.md) can use.

Content: one fps-arms tool textured from Carve bakes alone.

## Why

Raised by the fps-arms project (private repo `Arcodiant/fps-arms`, 2026-11-15), which built a rigged, textured pair of first-person arms and four tools with Carve 0.9.0 through script-written `.carve` files.

- [M-7](M-7-materials-and-textures.md) takes supplied PNGs and has no texture-making tool.
- fps-arms wrote its own bakes on Carve's UVs: ray-cast occlusion, a 1.6 mm edge bevel into the normal map, and group masks for colour, roughness and emission.
- Each needed the mesh, its hard edges and its UVs read back out of the Carve file.

## Open at planning

- Tangent convention and where tangents come from (glTF, green up).
- Resolution, padding and how occlusion, roughness and metallic are packed.
- Occlusion between separate meshes, and for a skinned mesh, in which pose?

**Decided at planning (2026-11-18).**

By the person:

- **Tangents match the engine.** glTF, green up. Each texel's frame is its triangle's UV-derivative (cotangent) frame
  about the interpolated shading normal, as Empyrean's `applyNormalMap` builds its frame. No `TANGENT` is exported.
  MikkTSpace was weighed: it is portable to other tools, but render and the game would disagree with it on curved
  faces.
- **One texture set for one material.** `bake` writes base colour (per group or material, from `surface`) and an ORM
  PNG: R occlusion, G roughness, B metallic. Masks and curvature are written as separate grey PNGs.
- **Occlusion** is cast against every mesh of the model at rest (bind) pose, and narrowed by `occluders`.
- **Bevel normals** round shading breaks: the marked hard edges of a smooth mesh, every non-coplanar edge of a flat
  one. On the edge the normal is the mean of the two faces' normals, easing by smoothstep to the shading normal at
  `width` metres, measured in 3D.
- **Curvature** is edge-based. It is grey 128 where flat, lighter toward convex edges and darker toward concave ones,
  falling off over `width` and scaled by the dihedral angle.
- **Limits:**
  - sizes are powers of two from 64 to 4096, default 1024;
  - padding defaults to 8 texels and fills only texels no island covers, nearest island first;
  - `rays` defaults to 32, and `reach` is required;
  - directions are fixed, so a bake is byte-identical from run to run;
  - overlapping UVs are refused with the faces named, and unmapped faces are skipped with a warning.
- **The tool and the look:** one `bake` tool, and `render mode=uvs image=<png>`, as sketched below. `surface` entries
  apply in order, so a later entry wins on a face that is in two groups.
- **The content is fps-arms' wrench**, copied unchanged into `samples/wrench.carve`.
- **No acceptance run** in this milestone.

Chosen by the orchestrator, each written in its task's Notes as a stop if it proves wrong:

- the coverage clamp and the padding tie rules (T-120);
- base colour's fallback order and the result's stats (T-122);
- occlusion's `1 − (t / reach)²` falloff, the hash-rotated cosine set and a 10 s budget for the wrench (T-124);
- summing bends at corners (T-125, T-126);
- curvature's `θ / (π/2)` scale (T-126).

Checked at planning:

- **Empyrean 0.129.0 reads normal maps with glTF's green up** (a throwaway regression test, deleted). On a box's +Z
  face with uv (0, 0) at its top-left, a +G map brightened the face toward the top face's shade and −G darkened it.
  +R behaved as +u, which is +x. The figures are in T-120's Notes. The engine's docs state that `applyNormalMap` uses
  a derivative frame and no vertex tangent, but do not state its sign.
- **The wrench opens as it is.** `CarveFile.Read` reads fps-arms' `wrench.carve` as version 5 with no error or
  warning, and `MeshUvs.Check` finds no problem. It has 120 faces, all mapped, is smooth, with 212 hard edges and
  groups `rubber`, `steel`, `dark`, `red`, `black` and `all`. It has no face colours, so `surface` carries
  `base_colour`.
- **The wrench's numbers:**
  - 3.05 UV per metre on every face, which is 3.1 texels per mm at 1024;
  - fps-arms' 1.6 mm bevel is 5 texels;
  - the smallest face covers 390 texels;
  - the unwrap's default island margin of 1/64 is 16 texels at 1024, so 8 texels of padding fills half of the gap.
- fps-arms' own bake (`pipeline/toolbake.py`, `ao.py`): 1024, 24 rays, reach 35 mm, 10 rounds of dilation, ORM packed.
  The scenario uses reach 0.035 and its group colours, roughness and metallic.

Not checked: whether the wrench's UVs have overlaps that `MeshUvs.Check` does not look for (T-122's test checks it
with T-120's layout), how long occlusion takes (T-124 measures it), and how the engine samples an image in the `uvs`
look (T-121 probes it).

**The `uvs` look with an image**, as agreed:

```
bake {
  mesh: "wrench", folder: "textures",          // relative to the working dir, as export
  size: 1024, padding: 8,
  maps: ["occlusion","normal","curvature","masks","base_colour","orm"],
  occlusion: {rays: 32, reach: 0.035, occluders: ["wrench"]},
  bevel: {width: 0.0016}, curvature: {width: 0.003},
  surface: {"steel": {roughness: 0.36, metallic: 1}, "rubber": {roughness: 0.88}}
}
-> textures/wrench.occlusion.png  .normal  .curvature  .base_colour  .orm
   textures/wrench.mask.<group>.png (one per group; .material.<name>.png per material)
   {files:[{map, path, size, coverage, min, max, mean}], warnings:[]}

render {mode:"uvs", mesh:"wrench", image:"textures/wrench.occlusion.png"}
+------------------------------------------------+
|................................................|  outside islands: the image
|..+-----------+.....+------+..........+----+....|  at half brightness, so the
|..|###########|.....|######|..........|####|....|  padding shows as a fringe
|..|## image ##|.....|######|..+---+...|####|....|
|..|###########|.....+------+..|###|...+----+....|  inside islands: the image
|..+=====+-----+..............+===+.............|  as stored; white edges,
|................................................|  yellow seams (=), as today
+------------------------------------------------+
 u right, v down, one mesh; the caption names the
 image and its size
```

**Order.**

| Wave | Tasks | Why |
|---|---|---|
| 1 | T-120 beside T-121 | The Core kernel and the look share no file. T-121 touches only the `uvs` look and one scenario-materials frame |
| 2 | T-122 | The tool needs T-120's API |
| 3 | T-123 | The scenario needs the tool and the look |
| 4 | T-124, T-125 and T-126 side by side | Each adds a Core file, a map and block in `bake` and a stage. Conflicts in `bake`'s file, `BakesStages.cs` and the CLAUDE.md row only add. They share T-120's `NearEdges` and frame, and none of them changes those |

Shared files, one owner each: `src/Carve.Core/Baking/` and its public API belong to T-120; `bake`'s file, its
registration and `samples/wrench.carve` to T-122; the Bakes scenario, `BakesStages.cs`, suite `scenario-bakes` and its
CLAUDE.md row to T-123.

Live elsewhere at planning: M-27's T-111 moves the server's host into `Carve.Hosting` (`Program.cs`); T-122 rebases
onto it if it lands first. T-71 (off-milestone) moves groups and hard edges onto attribute layers with their behaviour
unchanged; T-120 reads them through `FaceStyle` and `MeshShade`. Neither changes what M-24 needs. M-29 depends on this
milestone and wraps T-120's API as mesh-input nodes.

## Required tasks

- [ ] [T-120](../tasks/T-120-bake-framework-in-the-core.md) — The Core lays a mesh's UV layout onto texels with a position, shading normal and engine-matched tangent frame per texel, refuses overlapping UVs, pads past island borders, finds nearby shading-break edges, and bakes a mask per group or material
- [ ] [T-121](../tasks/T-121-an-image-under-the-uv-layout-look.md) — render mode uvs takes an image argument and draws a PNG under one mesh's layout, full inside the islands and dimmed outside, so padding and bakes can be seen
- [ ] [T-122](../tasks/T-122-the-bake-tool-and-the-wrench.md) — An agent bakes masks and a base colour from per-group surfaces into PNGs with bake, names them in define_material, and samples/wrench.carve comes from fps-arms
- [ ] [T-123](../tasks/T-123-the-bakes-scenario.md) — Suite scenario-bakes opens the wrench, bakes its masks and base colour, and sees them in the UV layout look and on the textured wrench from every side and up close
- [ ] [T-124](../tasks/T-124-occlusion-and-the-orm-bake.md) — bake writes ray-cast ambient occlusion against the whole model with a ray count and reach, and an ORM PNG with occlusion, roughness and metallic for glTF
- [ ] [T-125](../tasks/T-125-bevel-normals.md) — bake writes a tangent-space normal map, glTF green up in the engine's own frame, that rounds every shading-break edge over a given width
- [ ] [T-126](../tasks/T-126-curvature.md) — bake writes a curvature map for wear, grey where flat, lighter toward convex edges and darker toward concave ones over a given width

## Retrospective
