---
description: >-
  A node-graph texture tool as its own MCP server on a shared engine-free graph package, and layered, masked materials on Carve meshes
---
# D-7 — Texture graphs and layered surfaces

**Status:** Reserved

**Raised by:** discussion session with the project owner, 2026-11-17, on whether tools in the shape of Substance Designer (node-graph textures) and Substance Painter (painting materials onto a model) would serve game agents.

**Default firmness:** Leaning · Rule

## Intent

An agent on a game makes its own textures instead of depending on supplied PNGs, a generator, or one-off image code. It builds them as a node graph: a small, deterministic, text-sized description it can edit by changing a parameter, check by measurement, and look at through renders. On a Carve mesh it layers those graphs under masks taken from what the mesh knows (selections, groups, baked curvature and occlusion), so wear, dirt and marks land where the shape puts them, and survive edits to the mesh and its UVs.

The person who owns the game can read how each texture was made, as D-1 asks of models.

Why now:

- [M-7](../milestones/M-7-materials-and-textures.md) takes supplied PNGs and has no tool for making them. Its barrel textures came from T-77's purpose-written generator (`tools/Carve.SampleTextures`), which is a texture graph written as code.
- The fps-arms project wrote its own bakes and group masks on Carve's UVs, which led to [M-24](../milestones/M-24-baked-maps.md). Those bakes are the inputs a layered material needs.

Painting by brush is not carried over. Painter's value to an agent is its layer stack and mesh-driven masks; a stroke, where one is wanted, is a call with points on the surface, as `knife` takes them.

Milestones planned against this doc: [M-27](../milestones/M-27-a-first-texture-graph.md), [M-28](../milestones/M-28-a-node-library.md), [M-29](../milestones/M-29-graphs-on-a-mesh.md), [M-30](../milestones/M-30-layered-materials.md) and [M-31](../milestones/M-31-strokes-volumes-and-decals.md).

## Decision

### The texture tool is its own MCP server

**Certainty:** Leaning · **Strictness:** Rule

Graph editing, swatch renders and PNG export are tools on a separate MCP server, not on Carve's. Textures serve more than meshes (terrain, UI, particles, trim sheets), and Carve's tool list (about 30 tools) is its interface: adding a graph tool set to it costs every modelling session context. Both servers render through Empyrean with the same fixed sky and `Material.Pbr`, and host through the same extensibility pattern.

**Alternatives:** graph tools as part of Carve's server, which avoids a second server entry in `.mcp.json` and a second process.
**Would change if:** acceptance runs show agents routinely need both servers in one session and lose track of which holds what, or the two tool sets turn out small enough to share one list without crowding it.

### A shared texture-graph package

**Certainty:** Leaning · **Strictness:** Rule

One engine-free package holds the graph format, its evaluation, caching, validation errors that name the node, the standard image nodes, and the texture checks (tiling seams, PBR value ranges, unit normals). The texture server and Carve both use it. Carve evaluates graphs itself and never calls the texture server. Like `Carve.Core`, it never references the engine, `System.Random` or the wall clock: noise is an integer hash of the texel and a seed, as in T-77.

Nodes that read a mesh (curvature, occlusion, id masks, position) live in Carve and plug into the package as an extension, so the package knows nothing of meshes.

**Alternatives:** each side with its own graph code (rejected unless the package proves awkward, since Carve's layers must evaluate exactly what the texture server shows).
**Would change if:** Carve's needs from a graph turn out to be only "load these PNGs", with no evaluation, in which case Carve could depend on export output alone.

### No generic graph base yet

**Certainty:** Leaning · **Strictness:** Rule

The package is for texture graphs, not a general node-graph framework. No second kind of graph (shaders, animation blending, procedural geometry) is planned, and an abstraction drawn from one example would be a guess. Inside the package, evaluation is kept apart from the image types by a namespace boundary, so a later split is mechanical.

**Alternatives:** a generic graph core package with texture nodes on top, from the start.
**Would change if:** a second kind of graph is planned.

### The same repo for now

**Certainty:** Leaning · **Strictness:** Rule

The package and the texture server are projects in `Carve.slnx`, alongside `Carve.Core`, `Carve.Extensibility` and `Carve.Server`, so the graph format can change without versioning a package between repos.

**Alternatives:** a repo of its own, publishing the package to the local feed as Empyrean is published.
**Would change if:** something outside this repo needs the package, or the texture server's release pace parts from Carve's.

### A graph is its own file

**Certainty:** Leaning · **Strictness:** Rule

A graph is saved as its own text file, which a Carve material names by path and output, relative to the `.carve` file as texture paths are (T-74). The texture server edits it with no `.carve` file involved.

**Alternatives:** graphs embedded in the `.carve` file, which keeps a model in one file.
**Would change if:** keeping a model and its graphs in step across two files proves a recurring fault in acceptance runs.

### Graphs are baked to PNG on export

**Certainty:** Leaning · **Strictness:** Rule

The game never evaluates a graph. Carve's export, and the texture server's, write PNGs (embedded in the `.glb` for Carve) at a stated resolution, so the game's loader and D-1's "a model is finished when it imports into an Empyrean game" are unchanged.

**Alternatives:** graphs evaluated by Empyrean at load time.
**Would change if:** Empyrean gains a procedural texture path, or the size of baked textures becomes a problem for a game.

### Masks are calls, not brushes

**Certainty:** Leaning · **Strictness:** Rule

Every mask on a layer is given by exact arguments with a checkable result: a `select` result, a group or material, a baked mesh map, a falloff volume, a stroke given as points on the surface with a width, or a projected decal. A layer stack is stored, not flattened, and re-baked when the mesh or its UVs change, so an agent never repaints.

**Alternatives:** a raster mask painted into UV space and stored as a PNG, as Painter keeps it.
**Would change if:** agents need detail that no volume, stroke or decal can express, and a stored raster is the only way to keep it.

## Open questions

- ~~**The server's name.**~~ Answered at M-27's planning (2026-11-17, by the person): `patina`.
- ~~**The graph file format.**~~ Answered at M-27's planning: versioned JSON with the extension `.texgraph`. Nodes are
  keyed by an id, an input names `"<id>"` or `"<id>.<port>"`, and outputs are `output` nodes with a role (T-110).
- ~~**The first node set.**~~ Answered at M-27's planning: `noise`, `gradient`, `levels`, `blend`, `colour`,
  `height_to_normal` and `output` (T-110, T-112).
- **Resolution.** Partly answered at M-27's planning: a graph states a default `size`, every parameter is relative to
  the image, and patina may evaluate at any power of two up to 8192 (swatches up to 2048). What size Carve evaluates
  a graph material at is still M-29's to decide.
- **Hosting.** Answered at M-27's planning: both servers host through a shared `Carve.Hosting` library extracted from
  `Carve.Server` (T-111), and patina loads the same extensions as carve.
- **How quality is judged.** Value checks catch broken textures, not ugly ones. Whether swatch renders let an agent judge a texture well enough is for M-27's and M-28's acceptance runs to show.
- **M-24's outputs as graph inputs.** M-24 is planned to write bakes as PNGs. M-29 needs them as mesh-input nodes; whether M-24 provides both, or M-29 wraps its PNGs, is for whichever is planned first.
