# Unreleased

- T-1: Carve.Core holds an indexed polygon mesh: validation, stats, flat face normals and ear-clipped triangulation
- T-2: carve starts as a generic-host MCP server over stdio, runs the engine on its own thread and loads extensions from an explicit list
- T-3: An agent defines a polygon mesh by coordinates through define_mesh and gets counts, bounds and warnings back
- T-4: An agent calls render and gets a shaded Empyrean image of the current mesh from the front or the back
