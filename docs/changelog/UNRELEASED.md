# Unreleased

- T-8: Carve.Core derives an edge table from each mesh, and vertex and face ids stay the same across every edit
- T-10: Carve.Core moves vertices, edges and faces and extrudes or insets faces, keeping ids and refusing a bad edit by element
- T-11: An agent edits the mesh with move and extrude, reads its ids with get_mesh and steps back with undo
- T-9: render takes any direction, a zoom and a target, so an agent can look at a mesh from any side and close up
