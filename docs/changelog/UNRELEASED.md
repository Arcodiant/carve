# Unreleased

- T-161: Carve.Graphs, a new engine-free project: mesh_curvature, mesh_occlusion, mesh_mask and mesh_position node types fed from Carve.Core.Baking on a mesh's UV layout, neutral where there is no mesh
- T-160: define_material takes {graph, output, size} for any texture slot in place of a PNG path, kept in the Carve file as version 10 relative to the file, and read back by get_mesh, save and open
- T-162: patina defines, saves and swatches graphs with mesh-input nodes on neutral stand-ins and says so, and samples/graphs gains weathered-iron and weathered-staves with their PNGs
- T-163: render draws a material whose slots name graph outputs, evaluated on each mesh's UVs and bakes, re-read and re-baked when the graph files, the mesh or its UVs change, and reports what it baked and how long it took
- T-164: export embeds each graph slot's evaluated PNG in the .glb, with one <material>@<mesh> copy per mesh when a mesh-reading material is used by several, and render_glb agrees with render
- T-166: A missing graph file lists the .texgraph files in its folder, a graph fault names its node, an unknown output lists the outputs, and check warns of every graph material that cannot be baked
- T-165: render mode uvs takes image as {graph, output, size} and draws that output, evaluated on the mesh, under its layout, so mesh masks can be seen
