# Unreleased

- T-120: The Core lays a mesh's UV layout onto texels with a position, shading normal and engine-matched tangent frame per texel, refuses overlapping UVs, pads past island borders, finds nearby shading-break edges, and bakes a mask per group or material
- T-122: An agent bakes masks and a base colour from per-group surfaces into PNGs with bake, names them in define_material, and samples/wrench.carve comes from fps-arms
- T-121: render mode uvs takes an image argument and draws a PNG under one mesh's layout, full inside the islands and dimmed outside, so padding and bakes can be seen
- T-123: Suite scenario-bakes opens the wrench, bakes its masks and base colour, and sees them in the UV layout look and on the textured wrench from every side and up close
- T-126: bake writes a curvature map for wear, grey where flat, lighter toward convex edges and darker toward concave ones over a given width
