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

It needs the .NET SDK, the Empyrean package feed (the local feed `empyrean-local` in
`NuGet.config`, `http://localhost:5555/v3/index.json`; without it `dotnet run` cannot restore),
and a Vulkan device (a GPU with a current driver); there
is no window, every look is an offscreen render. If no device is found the
server stays up and the `render` tool says so.

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
   `.carve` file and export again.
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
