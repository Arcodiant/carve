---
description: >-
  An agent bakes masks and a base colour from per-group surfaces into PNGs with bake, names them in define_material, and samples/wrench.carve comes from fps-arms
---
# T-122 — The bake tool and the wrench

**Status:** In Progress

**Pri:** P1

**Milestone:** M-24

**Batch:** — (standalone)

**Depends on:** T-120

**Verify:** core-unit, server-unit

## Goal

M-24's MCP surface. `bake` writes a mesh's maps as PNGs that `define_material` takes as they come back. Its first two
maps are `masks` and `base_colour`. The fps-arms wrench joins the samples as the model M-24 is shown on.

## Scope

- **`bake`** (a new tool file, registered beside the others), with the shape agreed at planning:

  ```
  bake {
    mesh: "wrench",                       // required when the model has several meshes, as render's mesh
    folder: "textures",                   // relative to the working directory, as export's paths; created if missing
    size: 1024,                           // a power of two, 64 to 4096; default 1024
    padding: 8,                           // texels, 0 to size/2; default 8
    maps: ["masks", "base_colour"],       // this task's maps; T-124 to T-126 add theirs
    surface: {                            // per group or material name, applied in the order given
      "steel":  {base_colour: "#80858A", roughness: 0.36, metallic: 1},
      "rubber": {base_colour: "#282A2E", roughness: 0.88}
    }
  }
  ```

  - **Files:** `<folder>/<mesh>.<map>.png`. Masks are `<folder>/<mesh>.mask.<group>.png` for each group and
    `<folder>/<mesh>.material.<name>.png` for each material, so a group and a material with one name never collide.
    A name a file name cannot hold is refused, naming it. A file that exists is replaced, and the result says so for
    that file.
  - **Result:** `{files: [{map, name?, path, size, replaced, coverage, min, max, mean}], skipped_faces: [...],
    warnings: []}`.
    - `path` is the form `define_material` accepts, relative to the working directory with `/` separators.
    - `coverage` is the fraction of texels faces cover before padding.
    - `min`, `max` and `mean` are over covered texels in 0..1, per channel for a colour map.
  - **`base_colour`:** each covered texel takes the colour of its face, resolved in this order:
    1. the last `surface` entry naming a group the face is in, or its material;
    2. the face's own colour (`set_colour`);
    3. its material's `base_colour`;
    4. white.

    The PNG holds the sRGB bytes of the hex, as `define_material`'s `base_colour` hex is sRGB. RGB, 8-bit.
  - **`surface`:** `base_colour` is a hex as elsewhere. `roughness` and `metallic` are 0 to 1 and are validated here,
    though only T-124's `orm` uses them; give no warning for them. An unknown group or material name is an error
    listing the names there are. A face in two named groups takes the later entry. The wrench's `all` group is the
    case to test: put it first as a default.
  - **Encoding:** single-channel maps as 8-bit grey PNGs, colour maps as RGB, `round(v * 255)` clamped. Use
    StbImageWriteSharp, already referenced by `Carve.Server`. M-27 checked that it writes identical bytes twice
    (2026-11-17). Padding comes from T-120's `Pad`.
  - **Errors**, all at once, as other tools give them:
    - an unknown mesh, listing the meshes;
    - a mesh with no UVs;
    - T-120's overlap error, passed through as it stands;
    - a bad size or padding;
    - an unknown map, listing the maps;
    - an empty `maps`.

    Unmapped faces are skipped, listed, and given a warning. `bake` changes no model state: it adds no undo entry and
    leaves the model unsaved-clean.
  - **Description (content):** what each map is, the file names, that `define_material` takes the paths as they are,
    and one worked example from bake to `set_material`.
- **`samples/wrench.carve`:** fps-arms' `carve/wrench.carve` (private repo `Arcodiant/fps-arms`, `main` at `1535e3e`;
  on this machine at `D:\repos\fps-arms\branches\main\carve\wrench.carve`), copied byte for byte. It is a version 5
  file: 120 faces, all mapped, smooth, 212 hard edges, 219 seams, and groups `rubber` (14), `steel` (58), `dark` (41),
  `red` (6), `black` (1) and `all` (120). It has no face colours. Checked at planning: `CarveFile.Read` opens it with
  no error or warning, and `MeshUvs.Check` finds no problem. A `unit` test in `tests/Carve.Core.Tests` keeps that
  true and checks that T-120's layout finds no overlap at 1024. The `CLAUDE.md` `samples/` row gains the wrench: where
  it came from, and that it is fps-arms' file unchanged.
- **Tests** (`unit`, on `ToolHarness` and in Core):
  - a two-group box at 64: each mask's texels exact, padding to the ring;
  - base colour by surface, by face colour, by material, and white;
  - the later entry winning;
  - two bakes byte-identical;
  - each error;
  - a baked PNG accepted by `define_material` and read back through `get_mesh`;
  - the wrench's masks at 256: their coverage sums to the whole layout's, and each group's coverage is in proportion
    to its UV area.

## Notes

- Decided at planning (2026-11-18), by the person:
  - one `bake` tool, with this shape and these file names;
  - one texture set packed for one material (base colour, and in T-124 an ORM);
  - `surface` entries apply in order, the later winning;
  - the wrench copied from fps-arms as it is.
- Chosen by the orchestrator, each a stop if it proves wrong: base colour's order (surface, face colour, material,
  white); `coverage`, `min`, `max` and `mean` as the stats; the `.material.` infix.
- **Stop and ask** before landing: a change to the agreed shape or names, a bake that changes the model, or a wrench
  that needs editing to bake.
- Sonnet should do: the shape is settled and the Core does the work. The description is content, so write it for an
  agent with no other context.
- Owns `bake`'s file and its registration, and `samples/wrench.carve`. T-124 to T-126 each add a map, its argument
  block and a paragraph of the description; those conflicts only add.
- M-27's T-111 moves the server's host into `Carve.Hosting` and touches `Program.cs`. If it lands first, rebase and
  register the tool where the tools are registered then.
- **Verify line:** `server-unit` covers `ToolHarness`. Name `server-regression` only if this task touches rendering or
  the stdio transport (CLAUDE.md).

### As built

Built by Claude Sonnet 5.5 (2026-11-19).

- `src/Carve.Server/BakeTool.cs`: `bake`, registered last in `CarveServer.AddTools` (`Program.cs` is untouched). Arguments, file names and result are as agreed. Core is unchanged: no file was added under `src/Carve.Core/Baking/`. A mask's `map` is `masks` and its `name` the group or material; its file is `<mesh>.mask.<group>.png` or `<mesh>.material.<name>.png`. `base_colour` is `<mesh>.base_colour.png`. `maps` left out is both; `folder` left out is `textures`.
- `samples/wrench.carve`: the bytes of `git show 1535e3e:carve/wrench.carve` in fps-arms. The working file there is the same text with CRLF line endings (a checkout with autocrlf; it differs only by `\r`), so the committed LF bytes are used. Nothing was written in fps-arms. The `CLAUDE.md` `samples/` row names it.
- Tests: `tests/Carve.Core.Tests/WrenchSampleTests.cs` (3: opens clean with no warning, UVs check clean, groups and counts; lays out at 1024 with no overlap; masks at 256 sum to the layout's coverage and each group is within one texel of its UV edge length of its UV area) and `tests/Carve.Server.Tests/BakeToolTests.cs` (25 cases: the two-group box at 64 with every texel and the padding ring, the colour order, the later entry, separate mask files for a group and a material of one name, two bakes byte-identical and no new model version, `define_material` taking the PNG and `get_mesh` reading it, skipped faces, each error, and the description's example run on the wrench). `PartToolsListTests.NewTools` gained `bake`, which the old-tools digest skips.

Readings landed:

- **`coverage` is the whole layout's**, the same number in every file of one bake: the fraction of texels the faces cover, before padding. For a mask, `min`, `max` and `mean` are over all covered texels, so `mean` is the share of covered texels in the group or material. T-120's layout counts a clamp ring (texels whose centres lie within sqrt(0.5) of a face) as covered, so a 16 x 32 island covers 18 x 34.
- **A surface entry with no `base_colour` gives no colour**: the face falls to an earlier entry, its own colour, its material, then white. `roughness` and `metallic` are validated only.
- **A name that is both a group and a material** takes the entry for both sets. An unknown name lists the mesh's groups, the mesh's materials and the model's defined materials.
- **Names**: a mesh, group or material holding a character a file name cannot hold is refused, naming it, but a group or material only when `masks` is asked for. Two files that differ only by case are refused.
- `bake` always overwrites; `replaced` says so for each file. Uncovered texels beyond the padding are black (0 or RGB 0,0,0).
- `set_material` takes `faces` or `group`, not neither, so the description's example uses `group: 'all'`.

Not checked: timing of a 4096 bake, a real stdio run or a frame (no render code touched, so `server-regression` is not named).
