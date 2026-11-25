# Unreleased

- T-161: Carve.Graphs, a new engine-free project: mesh_curvature, mesh_occlusion, mesh_mask and mesh_position node types fed from Carve.Core.Baking on a mesh's UV layout, neutral where there is no mesh
- T-160: define_material takes {graph, output, size} for any texture slot in place of a PNG path, kept in the Carve file as version 10 relative to the file, and read back by get_mesh, save and open
- T-162: patina defines, saves and swatches graphs with mesh-input nodes on neutral stand-ins and says so, and samples/graphs gains weathered-iron and weathered-staves with their PNGs
- T-163: render draws a material whose slots name graph outputs, evaluated on each mesh's UVs and bakes, re-read and re-baked when the graph files, the mesh or its UVs change, and reports what it baked and how long it took
- T-164: export embeds each graph slot's evaluated PNG in the .glb, with one <material>@<mesh> copy per mesh when a mesh-reading material is used by several, and render_glb agrees with render
- T-166: A missing graph file lists the .texgraph files in its folder, a graph fault names its node, an unknown output lists the outputs, and check warns of every graph material that cannot be baked
- T-165: render mode uvs takes image as {graph, output, size} and draws that output, evaluated on the mesh, under its layout, so mesh masks can be seen
- T-207: Every carve tool description fits Claude Code's 4,096-character cut, held by a test, with the detail that was cut still reachable by an agent
- T-167: samples/barrel-weathered.carve: the barrel with raised hoops, edge wear on the hoop rims from curvature and grime beside the hoops from occlusion, in suite scenario-weathered with re-bakes after mesh, UV and graph edits
- B-10: get_mesh takes include and faces to answer part of a mesh, so a model like the wrench can be read by an agent whose client limits a tool result
- T-168: Fresh agents weather the wrench with carve and patina from the tool descriptions alone, and recover from a planted wrong graph path
- B-11: The server-regression tests that bake both weathered materials at 1024 through the real process wait longer than 30 s for that call, so they pass on a loaded machine
