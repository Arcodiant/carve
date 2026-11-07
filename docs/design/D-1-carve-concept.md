---
description: >-
  What Carve is, who it is for, and the decisions and open questions that shape it
---
# D-1 — Carve concept

**Status:** Accepted

**Raised by:** project setup session, 2026-10-31

**Default firmness:** Leaning · Rule

## Intent

Carve is a 3D modelling tool whose user is a Claude agent. It runs as an MCP
server, so an agent building a game on the Empyrean engine can make the game's
models itself instead of depending on a person, a downloaded asset or a generator.

An agent has no hand and no eye on a viewport, so everything a modeller does by
pointing has to work as a call with exact arguments and a checkable result. The
agent defines and edits meshes by coordinates or with tools, looks at what it made
through renders, asks precise questions of the mesh, and exports a model the game
imports as content.

The person who owns the game benefits at one remove: they see models in the game,
and can read how each was made.

### What the content is

Carve is mostly code, but it has content, planned with the mechanics that show it:

- **Sample models**: the small models each tool is demonstrated and tested on.
- **Render-mode looks**: what a shaded view, a normals view or a UV view looks like,
  so an agent can read it.
- **Tool descriptions and worked examples**: the text agents learn the tools from.
  For a product used only through MCP, this text is the interface.

## Decision

### MCP server for agents

**Certainty:** Settled · **Strictness:** Invariant

Carve's interface is MCP tools. Everything a user can do, an agent can do through a
tool call, with no step that needs a person at a screen.

**Enforced by:** review only, for now; every milestone must be reachable through
MCP (`CLAUDE.md`, *Planning rules*).

### Empyrean games are the target

**Certainty:** Settled · **Strictness:** Rule

A model is finished when it imports into an Empyrean game and looks right there.
Other targets are not designed for.

### Editing by coordinates and by tools

**Certainty:** Settled · **Strictness:** Rule

An agent can define geometry directly by coordinates, and edit it with modelling
tools: cut, extrude, and moving a vertex, edge or face.

### Looking at the model

**Certainty:** Settled · **Strictness:** Rule

An agent can capture screenshots and render the model in different modes, UVs and
normals among them.

### Queries and selections

**Certainty:** Settled · **Strictness:** Rule

An agent can run simple query commands, such as "select all unattached vertices" or
"select all faces above y=10".

*M-4:* the shape is a stateless id list. `select` takes a kind and a `where` expression and returns the matching ids, without changing the mesh or keeping a selection; the agent passes the ids to `move` or `extrude` itself, and asks again after an edit (ids never change, so an answer stays good until an edit removes or reshapes what it names). `check` is a separate tool that lists faults by element.

### C# on Empyrean

**Certainty:** Leaning · **Strictness:** Rule

C#/.NET 10, with renders drawn by Empyrean itself, so a screenshot shows the model
as the game would. The kernel, `Carve.Core`, stays engine-free. Rendering needs a
machine with a Vulkan device, even with no window.

**Alternatives:** a small software rasteriser of our own, which runs anywhere and
is byte-repeatable, but won't match the game.
**Would change if:** Empyrean cannot run windowless inside a long-lived server
process, or its renders do not compare from run to run closely enough for scenarios.

### Polygon meshes

**Certainty:** Settled · **Strictness:** Rule

The mesh is polygons: faces of any size, stored as plain indexed vertex loops, with
an **edge table** derived from them (`MeshTopology`: each undirected edge, the faces
on it and which way each crosses it; each vertex's faces; each face's edges in loop
order). Triangles are a derived view for render, validation and export, never
stored. Operations keep their meaning on a logical face or edge loop. Open,
non-manifold and mixed-winding meshes stay legal, with warnings. A vertex's and a
face's id is its position in the mesh's arrays; an edit never renumbers, appends
new elements at the end, and keeps a reshaped face's id. Along the current chain of
edits an id is never reused; undo frees the undone step's ids, and the next edit
gives them out again (qualified after RV-10).

**Alternatives:** a half-edge structure, which makes walking a manifold surface
cheap but cannot hold the non-manifold and open input Carve accepts without special
cases; triangles only, a simpler kernel that loses the logical faces.
**Would change if:** the structure's cost outweighs its use once cut and extrude
exist, seen in how agents actually use them.

### A Carve file, and .glb export

**Certainty:** Leaning · **Strictness:** Rule

Work persists in a native Carve file that keeps the mesh as authored. Models reach a
game as exported `.glb`, which Empyrean's `IModelLoader` loads.

*Decided at M-3 planning (2026-11-04), by the person:*

- **The Carve file** (`.carve`) is versioned JSON holding the geometry: `{"carve": 1, "vertices": [...], "faces": [...]}`. Floats read back bit-exact, ids are kept, and there is no undo history.
- **Export** is SharpGLTF, with flat Newell normals and every edge hard, and one material `carve` (base colour 0.72/0.72/0.70, metallic 0, roughness 1).
- **Conventions**: pass-through (1 unit = 1 m, +Y up, +Z front, the origin as authored) with no checks.
- **The load proof** is a viewer spike that stands in for a game.

*Extended by [D-6](D-6-characters-rigging-animation-and-character-export.md), 2026-11-06:* the file also holds several meshes, colours, a skeleton, weights and clips; export adds colour materials, smooth normals and skins. `export` refuses to overwrite without `overwrite: true`.

**Alternatives:** `.glb` only, which loses the polygons on every save; server memory
only, which loses unexported work on a restart. A list of operations as the file was rejected because a change to the kernel could change or break old files.
**Would change if:** none recorded.

### No window

**Certainty:** Leaning · **Strictness:** Rule

The shipped product has no window. Every look is an offscreen render through a tool
call.

**Alternatives:** an optional live viewer, so a person can watch an agent work.
**Would change if:** a person watching turns out to matter enough to pay for a frame
loop and input.

### The renderer lives in the server process

One generic host (`Host.CreateEmptyApplicationBuilder`) owns one container holding both the
engine and MCP, and the engine runs on a dedicated `frame` thread of its own under Carve's own
`IHostedService`, since `EmpyreanHost` refuses `IHostedService`. A Vulkan failure is held and
reported by whatever needs the engine; the process and MCP stay up.

A child process, so a device loss could not take the session down, was the rejected
alternative: holding the failure in process already keeps the session up, without a second
process and a protocol between the two.

## Open questions

- **Tool shape.** *Decided at T-11:* one tool per operation (`define_mesh`, `move`, `extrude`, `get_mesh`, `undo`), not a few general ones. Elements are addressed by ids that survive edits: a vertex or face id is its position in `define_mesh`'s lists, an edge is a vertex pair, and an edit appends new ids at the end and renumbers nothing; the server holds no selections. `get_mesh` is how an agent learns the ids (no ids are drawn on renders). An edit returns the report plus the ids it created, and for `extrude` its cap, walls and ring; `get_mesh` returns the whole mesh, untrimmed. *M-1 acceptance run (T-7):* with two tools only, a fresh agent built a cube and a house from the descriptions alone with no error to correct; it could not look at a face neither view showed, which render-from-any-direction (T-9) answers. *M-2 acceptance run (T-13):* with one tool per operation, three fresh `claude -p` agents (plain `carve`, no docs) each built a closed, warning-free crate in 13 to 20 carve calls from the descriptions alone, with `define_mesh` once, then only `extrude` and `move`, and used `direction`, `zoom` and `target` unaided; none needed `undo` and two never called `get_mesh` (they tracked ids from the `extruded` and `created` fields of each report), so the ids-in-every-report shape was enough. The shape held with one description edit (`move` now warns that a face crossing itself is rejected after the whole call). The planted inset error never fired: all three read the `extrude` description, saw that 0.7 cannot fit a unit face and built a 4-unit cube instead, so the run showed the descriptions prevent that mistake but not that its error text recovers it (that text is exercised by the scenarios, and by round 2's unplanted `move` error, which the agent read and fixed at once). No handle with a gap behind it was made: nothing bridges two faces, so a later tool's gap. *M-3 acceptance run (T-20):* two fresh `claude -p` sessions per round (plain `carve`), one to build and save a crate and one, in a new process, to `open` it, lengthen the handle bars, `save` and `export` it as `models/crate.glb`; two rounds, no description changed. Both rounds ended with a session-2 mesh after `open` identical to session 1's final `get_mesh` (ids and positions), a `.glb` at the right place that the viewer loaded with exit 0, and a closed, warning-free result; `save` and `open` were used unaided, with the path relative or absolute, and no agent needed `undo`. The planted wrong name `crates.carve` was not hit in round 1, where the agent listed the folder with its own file tools before `open`; with those tools removed (round 2) the `open` error text, which lists `crate.carve`, was enough to recover in one call. So the tool shape of one tool per operation, with `save`, `open` and `export` as separate tools, held for the whole save-and-ship path. *M-4 acceptance run (T-28):* the tool shape held with `select`, `check`, `render` labels, `highlight` and `views` added, and one description edit. Fresh `claude -p` agents (plain `carve`, no docs) opened the leaky shed, ran `check` and found the faults, found open edges with `select edges where open`, moved the detached chimney with ids from a `select` (always a query: the `where` grammar was never misused), asked for a six-view normals sheet with face labels and named the -X wall as flipped, in 11 to 12 carve calls, never using `undo` or `highlight`. The planted wrong pair [3, 9] was rejected by `move` and its error was understood; the agents stopped and asked rather than guess which edge was meant (accepted as recovery). Telling `move` to take real edges from `select` did not change that, and a stronger text (round 3) only made the agent avoid the error; the lever is the error text (B-2).
- **Undo and history.** *Decided at T-11:* an agent gets plain undo for the last 100 steps, in memory, with no redo; `define_mesh` and `open` are steps too, and a failed call is not: within a session undo takes an `open` back, while the first mesh a process has (from either) has nothing before it and cannot be undone. Every change, an undo included, raises the model's version by one. *Still open:* whether a model's history is recorded so the person can read how it was made, or replayed to rebuild it, and whether any history survives a restart.
- **The Carve file.** *Decided at M-3 planning:* JSON text holding the geometry; see *A Carve file, and .glb export*.
- **Scope of a model.** Which of these does Carve author, and when: UVs and
  unwrapping, materials and textures, smoothing and normals, several parts or a node
  hierarchy, LODs, colliders, rigs and animation? *Rigs, animation, colour and smoothing are decided in D-6.*
- **Render modes.** Which come first, and do any need something Empyrean does not
  have? The first look at its docs did not find a normals or UV debug view; a
  material or shader of Carve's own may do it. *Decided at M-4:* wireframe, normals and highlight came first. None needed anything of the engine: it had line lists on the headless path, per-vertex colours on the unlit `SolidColor` material and an immediate text surface for the labels (T-25, T-26), so there is no engine need. UVs are still open.
- **Transport.** Stdio for now (decided at T-7; the README's `.mcp.json` entry starts it). HTTP, and
  how the server is installed for a game's Claude session beyond that entry, stay open until a
  use needs them.
- **Empyrean-specific export.** *Decided at M-3 planning:* pass-through scale and axes, the node and mesh named after the file, no checks. Whether a game's conventions ever warrant checks stays open.
- **Smoothing.** *Decided in D-6:* M-3 exports every edge hard; M-18 adds smooth meshes with hard edges marked by a tool.
- **Boolean and heavier operations.** Does "cut" mean a plane cut, a knife path, or
  boolean subtraction?

### Engine needs

| # | What is missing | Priority | Interim |
|---|---|---|---|
| 1 | A named error when Vulkan cannot start: with no driver, `IGameLoop.Initialise` throws `ArgumentNullException` for `ptr` from inside `Vulkan.Instance.Create` | Low | `EngineThread` words the fault itself, naming Vulkan |
| 2 | An in-memory PNG encode: `IRenderTargetReadback.WritePngAsync` writes only to a path, and `DecodedImage` has no encoder, while `render` must write nothing to disk | Low | `RenderTool` calls `StbImageWriteSharp` directly on `ToDecodedImage`'s bytes |
| 3 | A material name: the engine's `Material` carries none, so a loaded `.glb`'s material `carve` cannot be read back by name | Low | The viewer's `view.json` lists each material's colour, metallic and roughness, and no name |
| 4 | A skinned model of several meshes: a skinned file loads only its first skinned mesh and drops the rest with a warning | Low | D-6: every piece is written as a primitive of one glTF mesh |
