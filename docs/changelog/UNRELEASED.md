# Unreleased

- T-161: Carve.Graphs, a new engine-free project: mesh_curvature, mesh_occlusion, mesh_mask and mesh_position node types fed from Carve.Core.Baking on a mesh's UV layout, neutral where there is no mesh
- T-160: define_material takes {graph, output, size} for any texture slot in place of a PNG path, kept in the Carve file as version 10 relative to the file, and read back by get_mesh, save and open
- T-162: patina defines, saves and swatches graphs with mesh-input nodes on neutral stand-ins and says so, and samples/graphs gains weathered-iron and weathered-staves with their PNGs
