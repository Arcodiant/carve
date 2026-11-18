# Carve

Carve is a 3D modelling tool for Claude agents, served over MCP. An agent
defines a mesh by coordinates and looks at it through offscreen renders.

## Use

Add this to the `.mcp.json` of the project whose agent should model (change the
path to where this repo is checked out):

```json
{
  "mcpServers": {
    "carve": {
      "command": "dotnet",
      "args": ["run", "--project", "C:/path/to/carve/src/Carve.Server", "--no-launch-profile"]
    }
  }
}
```

It needs the [.NET 10 SDK](https://dotnet.microsoft.com/download) and a Vulkan
device (a GPU with a current driver). Every package, including the
[Empyrean](https://www.nuget.org/packages/Arcodiant.Empyrean) engine, comes from
nuget.org, so the first `dotnet run` restores everything. There is no window;
every look is an offscreen render. If no device is found the server stays up
and the `render` tool says so.

## Build and test

```
dotnet build Carve.slnx
dotnet test tests/Carve.Core.Tests --filter "Tier!=regression"
dotnet test tests/Carve.Server.Tests --filter "Tier!=regression"
```

Those two suites need no GPU and are what CI runs. The regression tier and the
scenarios under `scenarios/` render for real and need a Vulkan device;
`tests.json` lists every suite and how to run it; `scripts/verify.ps1` builds,
runs every test project and the cube scenario.

## From an empty file to a game

The same `.mcp.json` entry above is all the setup. The calls, in order, with
the paths relative to the folder carve was started in (its working directory):

1. `define_mesh` the cube (the example in its description), then edit it with
   `move` and `extrude`.
2. `save` with `{"path":"crate.carve"}`. This writes `crate.carve`, the file to
   keep working in: the polygons and their ids exactly, not the undo history.
   Missing folders are created, and a file already there is replaced (the result
   says so).
3. In a later session, `open` with `{"path":"crate.carve"}`. It replaces the
   current mesh in one step (`undo` takes it back to the mesh before it; the first mesh a process has cannot be undone, so undo straight after an `open` that gave the process its first mesh is an error, while a process that had a mesh before it can undo it; only the last 100 changes are kept), and the ids from before
   the save still name the same vertices and faces. Carve remembers no current
   file, so the path is given every time. A path that is not there is an error
   that names the full path and lists the `.carve` files in that folder.
4. `export` with `{"path":"C:/path/to/game/Content/models/crate.glb"}`. The
   `.glb` is only for the game; it keeps triangles, not faces, so edit the
   `.carve` file and export again, adding `"overwrite":true`: export never
   replaces a file without it.
5. The game loads it with one line:

   ```csharp
   models.LoadModel(new ContentPath("models/crate.glb"))
   ```

The tool descriptions carry the same sequence, so an agent that has only the
tool list can follow it.

## Asking the mesh

An agent can question a mesh instead of reading every id out of `get_mesh`.

- `check` (no arguments) lists what is wrong: inside-out shells, coincident or unused vertices, duplicate faces, open edges (holes), flipped or non-manifold edges, bent faces, each with the element ids.
- `select` takes a `kind` (`vertices`, `edges` or `faces`) and a `where`, and returns the ids. `where` is comparisons on a property (`y > 3.55`, `min.y >= 3.6`, `length < 0.2`) or a flag (`open`, `unattached`, `non_planar`), joined by `and`, `or`, `not` and brackets; `all` and `none` also work. For example `edges` where `open`, or `faces` where `normal.y > 0.9`.
- The server keeps no selection: pass the ids of a `select` result to `move` or `extrude` yourself, and ask again after an edit.
- `render` with `labels` (`vertices`, `faces`, `both`) draws the ids on the image, and `highlight` marks named elements. With `views` (a list of the axis views `+x`, `-x`, `+y`, `-y`, `+z`, `-z`, or `front`, `back` and the rest) it returns one contact sheet, e.g. `{"views":["+x","-x","+y","-y","+z","-z"],"labels":"faces","mode":"normals"}`. `mode` also takes `wireframe`.

`samples/leaky-shed.carve` is a broken shed to try them on.

## Cutting

Each of these edits the mesh in place and appends new ids at the end; each example is on a fresh `define_mesh` unit cube unless it says otherwise. A cut is refused, with the point and its distance, when a knife point is off the surface.

- `plane_cut` slices through a plane; with `keep` it keeps one side and caps it (the default, `both`, only splits): `{"point":[0,0.5,0],"normal":[0,1,0],"keep":"below"}`. Two cuts with tilted normals make a gable.
- `loop_cut` runs a ring of new edges round a band of faces, from one edge, at fractions along it: `{"edge":[0,3],"at":[0.5]}`.
- `knife` draws a path on a face, splitting it; a closed path cuts out a piece with its own id: `{"points":[[0.25,0.25,1],[0.75,0.25,1],[0.75,0.75,1],[0.25,0.75,1]],"closed":true}`.
- `merge_faces` joins connected faces into one, a non-planar result being a warning; after the `loop_cut` above, this puts the front's two halves back together, faces 1 and 7, into face 1: `{"faces":[1,7]}`.
- `delete` removes faces or vertices; their ids stay as `null` (tombstones) in `get_mesh`: `{"faces":[3]}`.
- `bridge` joins two open loops of vertices with a band of faces, which closes an opening left by `delete`; on `bridge`'s own example, two unit squares facing each other, this makes a box: `{"loops":[[0,1,2,3],[4,5,6,7]]}`.

A window through a wall is `knife` on the front and the back, `delete` of both pieces, then `bridge` of the two loops.

## Several meshes, colours and smooth surfaces

A model holds named meshes. `define_mesh` with a `name` adds a mesh, or replaces the one of that name. Once there is more than one, the other tools take `mesh` to say which to work on, and `render` draws them all together. `define_mesh`'s own description builds a crate and a lid this way.

- `define_group` names a set of faces so that later calls can treat them as one: `{"name":"sides","faces":[0,1,4,5]}` (on the unit cube, its -Z, +Z, -X and +X faces). A face may be in several groups, and an extrude's walls join their face's groups.
- `set_colour` colours faces, or a group, with sRGB hex: `{"colour":"#C8503C","faces":[0,1]}`. Export writes one matte material per colour.
- `set_shading` makes a mesh `smooth` (or `flat`, the default): `{"shading":"smooth"}`. Under smooth, `mark_edges` keeps chosen edges creased: `{"edges":[[0,1]],"hard":true}`.
- `subdivide` rounds a mesh off by Catmull-Clark, 1 to 3 levels: `{"levels":1}`. Every id stays, new ones are appended, and hard edges stay sharp.

`render` and `export` draw the same normals, so a smooth mesh looks the same in the game.

## UVs

A face's UVs are one `[u, v]` per corner. They run as in glTF: (0, 0) is the texture's top-left, and v runs down. Every edit, `undo` and the `.carve` file carry them.

- `set_uvs` gives faces UVs by coordinates. On the unit cube this maps face 0 (corners `[0,3,2,1]`) to the whole texture: `{"faces":[{"face":0,"uv":[[0,0],[0,1],[1,1],[1,0]]}]}`.
- `project_uvs` projects faces: `planar` (one side of a thing), `box` (crates, rooms) or `cylindrical` (barrels, posts): `{"method":"box"}`, or `{"method":"planar","view":"+y","faces":[3],"into":[0,0,0.5,1]}` for the top into the left half.
- `unwrap` cuts the faces at their seams, flattens each island and packs them: `{}`. Mark the seams first with `mark_edges` and `seam`: `{"edges":[[0,3]],"seam":true}`. Each island must lie flat, so a closed box needs enough seams to unfold, and a cylinder needs one down its side and one round each rim. `unwrap`'s description works through a barrel.
- `render` with `mode` `checker` draws a checker at each face's UVs, with unmapped faces magenta. `mode` `uvs` draws the layout flat, with seams in yellow.
- `check` warns of unmapped, flipped, zero-area and overlapping UV faces.

`samples/barrel.carve` is the seamed, unwrapped barrel.

## Materials and textures

A material is a named PBR look shared by the whole model; any mesh's faces may use it. A face has either a colour or a material, and setting one clears the other.

1. Give the faces UVs (above): `project_uvs` `{"method":"box","faces":[1,3]}`, the unit cube's front and top.
2. `define_material` with `base_colour`, `metallic`, `roughness`, `emissive`, `occlusion`, `double_sided` and up to five PNG textures (`base_colour_texture`, `metallic_roughness_texture`, `normal_texture`, `emissive_texture`, `occlusion_texture`): `{"name":"crate","roughness":0.8,"base_colour_texture":"textures/crate.png"}`. Anything left out takes its default, so a bare name is matte white.
3. `set_material` puts it on faces or a group: `{"material":"crate","faces":[1,3]}`.

Texture paths are relative to carve's working folder, or absolute; the `.carve` file keeps them relative to itself. A texture is drawn only on faces with UVs, and `check` and `set_material` warn of faces without them. `render` draws a material face with the engine's PBR shading under a fixed sky, so metal reads. `export` writes each material as a glTF metallic-roughness material with its PNGs embedded.

`samples/barrel-textured.carve` is the barrel with `staves`, `iron`, `end-grain` and `rune-stave` materials from `samples/textures/`.

## Characters

A character is a model with a skeleton, bone weights and clips. `export` then writes a skinned `.glb`. The examples are one small figure: a mesh `body` and a box `hat` of 8 vertices on its head, with this skeleton.

- `define_skeleton` sets every bone, in order, each `{name, parent, position, rotation}` local to its parent, with parents before children:

  ```json
  {"bones":[{"name":"hips","parent":null,"position":[0,1,0],"rotation":[0,0,0,1]},{"name":"spine","parent":"hips","position":[0,0.3,0],"rotation":[0,0,0,1]},{"name":"head","parent":"spine","position":[0,0.4,0],"rotation":[0,0,0,1]}]}
  ```

  `get_skeleton` reads the bones back, with where each one rests in model space.
- `weigh` weights a mesh by distance to the bones: `{"mesh":"body","replace":true}`. `set_weights` overrides vertices, or a face group, by hand; this binds the whole hat to the head: `{"mesh":"hat","vertices":[0,1,2,3,4,5,6,7],"weights":{"head":1}}`. `get_weights` reads them back and lists any unweighted vertices.
- `define_clip` makes a named clip from rotation and translation keys per bone. Values are absolute and local to the parent, and each channel part has its own interpolation, `LINEAR` (the default) or `STEP`. A nod of the head, 20 degrees about X and back:

  ```json
  {"name":"nod","duration":1,"loop":true,"channels":{"head":{"rotation":{"interpolation":"LINEAR","keys":[{"time":0,"value":[0,0,0,1]},{"time":0.5,"value":[0.17364818,0,0,0.98480775]},{"time":1,"value":[0,0,0,1]}]}}}}
  ```

  `get_clips` reads clips back and warns of a looping clip that does not close. `delete_clip` removes one.
- `render` draws the model in a `pose`, or at a `clip` and `time`: `{"clip":"nod","time":0.5}`. `bones: true` draws the skeleton over it (`bone_labels: true` names the bones), and `weights` with a bone's name shows that bone's weights as heat, with unweighted vertices magenta.

`export` takes a `mode`:

- `model` (the default) writes the skinned mesh, its materials, the skeleton and one animation per clip. It refuses unweighted vertices unless given `allow_unweighted: true`.
- `clips` writes the skeleton and clips with no mesh. It is one animation file that every character sharing the skeleton can use, and the game loads it with `LoadAnimationSet`.
- `piece` with `meshes` writes chosen meshes alone, with the whole skeleton in the same order, as a costume part the game swaps at runtime on the body's pose: `{"path":"models/figure.hat.glb","mode":"piece","meshes":["hat"]}`.

`render_glb` draws an exported `.glb` through the engine's own loader and GPU skinning, at rest or at a `clip` and `time`: `{"path":"models/figure.glb","clip":"nod","time":0.25}`. A `render_glb` frame and a `render` frame of the same model, view, size, clip and time line up, so the two can be compared.

`export` also warns when a model passes a game's limits on triangles, submeshes or height, read from the first `carve-limits.json` found in the `.glb`'s folder or a folder above it (or the one `limits` names). `write_limits: true` creates the file with starting numbers to edit. `samples/gardener.carve` is a rigged, animated character, and `samples/gardener-soft.carve` is the same character subdivided and smoothed.

## patina: texture graphs

`patina` is a second MCP server, beside carve, for texture graphs (D-7): an agent
defines a graph of nodes (`noise`, `gradient`, `levels`, `blend`,
`height_to_normal`, `colour`, `output`), edits it, reads it back, and saves it
as a `.texgraph` file. Add it to the same `.mcp.json`, next to carve's entry:

```json
{
  "mcpServers": {
    "carve": {
      "command": "dotnet",
      "args": ["run", "--project", "C:/path/to/carve/src/Carve.Server", "--no-launch-profile"]
    },
    "patina": {
      "command": "dotnet",
      "args": ["run", "--project", "C:/path/to/carve/src/Patina.Server", "--no-launch-profile"]
    }
  }
}
```

Its tools: `define_graph` (the whole graph, as a `.texgraph` body), `edit_graph`
(a list of operations, all or nothing: `set_node`, `remove_node`, `connect`,
`disconnect`, `set_params`, `set`), `get_graph`, `save`, `open`, and
`push_snapshot` / `pop_snapshot` to try a change and go back. Every fault names
its node and its port or parameter. A graph with faults can be opened and fixed
with `edit_graph`, but not saved. `samples/graphs/planks.texgraph` is a whole
material to open.

`export` writes the open graph's outputs as `<folder>/<role>.png` at a size you
name, a power of two from 32 to 8192 (default the graph's own), ready for a
carve material's texture slots: `base_colour.png` for `base_colour_texture`,
`normal.png` for `normal_texture`, `roughness.png` (a glTF metallic-roughness
image) for `metallic_roughness_texture`, `occlusion.png` for `occlusion_texture`.
At 8192 it works in bounded memory but takes minutes for four outputs.

`swatch` shows the open graph: by default a sheet with a column per output (flat
over tiled 3 x 3, to show seams) beside a sphere wearing all of them through the
engine's PBR material under Carve's fixed sky; or `view` `flat`, `tiled` or
`sphere` alone, at a `size` from 32 to 2048, the sphere turned by `angle`.
patina needs a Vulkan device to draw the sphere and the sheet; flat and tiled
need none.

## License

Carve is released under the [MIT license](LICENSE).
