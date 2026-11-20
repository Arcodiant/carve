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

**As planned.**

- **The slice runs end to end.** An agent opens the fps-arms wrench (`samples/wrench.carve`, the committed bytes of
  fps-arms `1535e3e`), bakes it with one `bake` call into masks, base colour, occlusion, an ORM, a bevel normal map and a
  curvature map, sees each in `render mode=uvs image=<png>`, names them in one material and exports it. `render_glb` of
  the export matches `render`: front 0 px, three-quarter 1 px with the full material.
- **The members, in the planned waves:**
  - **T-120**, the Core kernel `Carve.Core.Baking`: the layout with its coverage, clamp and tie rules, the overlap
    refusal, `Pad`, the engine's tangent frame, `NearEdges` and the masks. Its public API held: T-122 to T-126 built on it
    and none changed it.
  - **T-121**, the image under the `uvs` look: nearest sampling with no mips (the checker's sampler), so each pixel is a
    texel. Every existing frame stayed byte-identical (91 crate and uvs frames, 13 materials frames).
  - **T-122**, the `bake` tool and the wrench.
  - **T-123**, suite `scenario-bakes`, built in stages so each later map is one file and one line.
  - **T-124**, ray-cast occlusion (a BVH, a hash-rotated cosine Fibonacci set, `1 − (t / reach)²`) and the ORM.
  - **T-125**, bevel normals in the engine's frame, with a render test on a box that fails with G or R inverted.
  - **T-126**, curvature.
- **The planning-time calls held:** the coverage clamp and padding tie rules (T-120); base colour's fallback order and
  the result's stats (T-122); the falloff, the rotated cosine set and the 10 s budget (T-124: 7.4 to 8.8 s in Debug);
  summing bends at corners (T-125, T-126); curvature's `θ / (π/2)` scale (T-126).
- **The tangent frame matches the probe.** On `FrontUvs` the planning formula's second vector points down the image, so
  T-120 negates it. A unit test holds the raw formula's sign and the frame's, and T-125's render test shows +G brightening
  toward the top face, with G or R inverted failing.
- **Every new check was shown failing on a break, two of them only in RV-74's answer,** with the counts in each member's As built: the steel/red swap (448),
  a faint steel and red (53), a mirrored camera (57), inverted curvature (123), reach 0.0001 (the crevice 0 below the
  open texels), metallic 0 (271418 of 497040 texels), the G-inverted normal map (6 of 6 strip checks), the thin-width
  seam break (32.4° against 9°). The open texels at least 250 and the flat texels holding N had no break until RV-74
  (claude-opus-5-5) added `dark` (the occlusion read inverted: 2 failures, the grip's middle reading 0) and `bevel-wide`
  (the map baked 20 times as wide: 380 of 448 flat texels, and 2 strips); RV-74 also gave the bevel export the control
  that must fail (88205 of 1048576 pixels, failing).
- **The person approved every frame set, each in round 1 with no notes:** T-121's four, T-123's 17, T-124's 17,
  T-125's 19 and T-126's four.
- **`cairn milestone M-24 verify` (2026-11-20, at 6887cfe, before this retrospective)** passed every check but `review`,
  which comes after the person's go:
  - 7/7 members done and merged;
  - all 24 suites pass, 22 ran and 2 carried (`patina-unit`, and `server-regression` from T-125's last run at 4e5f673).
- **Models:**

  | Member | Model | Agents |
  |---|---|---|
  | T-120 | Opus 5.5 | 1 |
  | T-121 | Sonnet 5.5 | 3 (the checkpoint before the review; the review agent, stopped by an app restart after its last commit; a third ran `finish`) |
  | T-122 | Sonnet 5.5 | 1 |
  | T-123 | Opus 5.5 | 2 (the thresholds, then the review) |
  | T-124 | Opus 5.5 | 4 (the thresholds; a research agent on `render_glb`; the checkpoint; the review and `finish`) |
  | T-125 | Opus 5.5 | 4 (the checkpoint; the thresholds; a wait for T-124; the review and `finish`) |
  | T-126 | Sonnet 5.5 | 2 (the thresholds, then the review) |

  No rework was traced to the cheaper tier. Every extra agent was a stop for the person, a checkpoint, the planned
  review agent or the wait for T-124.

**Not as planned.**

- **`render_glb` had no mipmaps.** T-124's full material failed D-6's frame rule in the three-quarter view (215 px,
  patch 54 against 50). An Opus research agent found the cause: `render_glb` loaded the `.glb` with the engine's default
  options, so it sampled textures nearest with no mips, while `render` uploads with mips (T-78's ruling 1; D-1's engine
  need 8 already said every load generates them). The baked occlusion varies texel to texel, so only it showed the
  difference. The person ruled it be fixed in T-124. With `GenerateMipmaps = true`:
  - the ORM comparison went to 0 px front and 1 px three-quarter, T-123's from 48 to 0 and 10 to 1, T-125's from 47 to 0
    and 15 to 1; the controls still fail;
  - 286 of 287 frames of `scenario-orcward`, `scenario-morph` and `server-regression` stayed byte-identical; the one
    that changed is the new `RenderGlbSamplingTests` frame, whose test fails without the fix (184846 px);
  - exported files and `render` frames did not change.
- **The wrench is not the shape the plan assumed.** It is a ratchet built from stacked solids, with no jaw opening and
  no concave edge anywhere (all 212 hard edges convex).
  - T-126's concave check moved to a small L-block the curvature stage builds, bakes and undoes. The wrench's frames
    and bakes are byte-identical with and without it.
  - T-124's "jaw's inside corner" became the crevice where the square drive stands on the socket top, and the frame
    from inside the opening became `wrench-orm-drive-close`.
  - T-125's first strip edge was buried under the ring head from every direction, and the dark jaw's shades lay within
    3 luma, so the strip check could not tell the map from G inverted. It moved to a steel grip edge (72, 74), where the
    good map reads at most 1.00 from the predicted shade and G inverted at least 5.21.
- **Two of the plan's rules were wrong as written.** The edge's mean normal renders darker than both faces (113.2
  against 116.5 and 117.5), so "the +X strip leans toward +X's shade" and "the strip lies between the two faces" were
  dropped (the person). T-120's planning probe already showed it: R+ read 114.3.
- **The orchestrator misstated one measurement to the person.** T-124's first agent reported a crevice margin of 72
  without saying which measure, and the orchestrator put it to the person as a difference of means, which is about 121.
  The agent coding it caught the mismatch, and the person re-ruled on the measure the 72 came from.
- **T-124 moved `bake`'s worked example from 1024 to 512,** its own call: at 1024 the ORM took 13 s in the harness and
  ran past its 30 s answer window under load, failing `finish`. No check changed.
- **T-121's review agent was stopped by an app restart** after it had folded the hand-off and committed the person's
  approval. The orchestrator saw the approval only as that commit and said so to the person; a third agent finished.
- **T-126's As built merged with a garbled evidence path** (backslashes lost in the write); the orchestrator repaired the
  line in this commit. T-123's and T-124's As built carried the same fault (a `\r` read as a line break) and were
  repaired in the answer to RV-74.
- **Every member left its task folder on disk.** `finish` could not remove `branches/task/T-120` to `T-126` while a shell
  sat in them; each is unregistered from git and safe to delete.

**Rulings the person gave during the milestone** (each recorded in the As built of the member named, except those given in answer to RV-74, which are recorded in RV-74's answers and here):

- **T-120:** flat-mesh coplanarity is `PolygonMesh.PlanarityTolerance` applied to the pair, as landed (asked after it
  landed, since the spec named it a stop).
- **T-123:** in each view, the darkest steel sample is at least 30 luma above the lightest rubber, dark or black one;
  red leads max(G, B) by at least 50 and no other group leads; the sample placement as built.
- **T-124:** every crevice texel at least 50 below every open texel (measured 72, corrected from a mean-to-mean reading,
  above); open texels at least 250; the drive crevice as the stand-in; the mipmap fix in T-124.
- **T-125:** top strip at least 4.7 brighter, +X strip at least 1.6 darker; the strip check on the steel grip within 2
  luma of the predicted shade; the seam check as a Core test on the wrench within 9°; `bevel-thin` dropped from the
  stage.
- **T-126:** convex jaw edges at least 218; the L-block's inner edge at most 64; `blend: 2` on the curvature `uvs`
  frames; the occlusion (`wrench-orm-uvs-occlusion`) and normal-map (`wrench-uvs-normal`) `uvs` frames have T-121's block
  rule with `blend: 1` (the person, 2026-11-20, RV-74: strictly 96 and 3 inside pixels fail; with `blend` reading each channel
  none does), every other frame keeping the strict rule.
- **`bake`, all three blocks:** an `occlusion`, `bevel` or `curvature` block whose map is not asked for bakes what was
  asked and warns, e.g. `bevel: ignored, 'normal' is not in maps`, rather than refusing. It is one helper, `UnusedBlock`.

**Never verified.**

- A bake at 4096, and any timing in a Release build.
- The wrench's bake stats against fps-arms' own bake (`pipeline/toolbake.py`).
- Occlusion on a model whose parts are moved relative to one another: occluders use each mesh's stored positions.
- PNGs with alpha below 255, 16-bit or palette PNGs under the `uvs` look, and UVs beyond the unit square there.
- How much room T-126's L-block margin of 64 leaves at other sizes or widths.
- UV overlaps on layouts other than the wrench at 1024 and the barrel at 256, which T-120's layout found none of.

**Known and left.**

- **The review, after the person's go (2026-11-20).** A Sonnet 5.5 agent rebased the milestone onto `origin/main` with no conflict, and an Opus 5.5 agent's refactor pass removed the stages' and tests' copied helpers with every frame, bake and `.glb` byte-identical. RV-74 (three Opus 5.5 reviewers: the Core kernel, the server and rendering, the whole diff) found 19: two Major (`NearEdges` called a convex edge concave across a non-convex face; `ColourRule.cs` missing from `scenario-bakes`' inputs), ten Minor and seven Nit. Two Opus 5.5 agents and a Sonnet 5.5 agent answered them: 18 fixed, one skipped (the seam test's 4 s in `unit`), with every approved bake and frame byte-identical and `Pad` 287 ms at 4096. RV-75 (one Opus 5.5 reviewer) found that 18 answers hold and four Nits; the person chose to fix the docs slip and file the other three as **T-158**, off-milestone, one of them (the `uvs` block rule coded twice) having survived both rounds.
- **T-154**, filed from the test report: `cairn test report --since 2d` counted one flaky pass of `scenario-bakes` in
  18 runs and one run that could not start. The report held no more than the counts, so T-154 finds the run's log and
  names the cause. Nothing else in the report called for filing: no other suite flaked, and the costliest,
  `server-regression`, caught a failure in the window.
- `CLAUDE.md` is an input of no suite, so `verify` warns on every member that edits it, as before.

**Next time.**

- **Look at the content model before writing checks around its shape.** Three members wrote checks for a jaw opening and
  concave edges the wrench does not have. A planning probe listing the sample's concave edges and openings would have
  moved those checks before dispatch.
- **Say which statistic a margin is measured on, in the agent's report and in the question.** Ask members to report a
  margin as `a − b` with both named.
- **Check a render rule written from intuition against the planning probe first.** The probe already showed the mean
  normal darker than both faces.
- **Cover each loader's options with a sampling check.** Nothing covered `render_glb`'s load until a noisy texture
  exposed it; `RenderGlbSamplingTests` does now.
