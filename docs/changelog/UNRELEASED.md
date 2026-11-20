# Unreleased

- T-133: Patina.Graphs evaluates in square tiles, each node naming the input rectangle it needs, under a capped tile cache, with every existing output byte-identical
- T-136: tiles (bricks with gaps and jitter), stripes and shape nodes, and the bricks and fabric sample graphs with their PNGs
- T-135: worley, fbm and warp nodes, and samples/graphs/cobblestones.texgraph with its PNGs
- T-134: Texture graphs gain metallic (packed into the glTF metallic-roughness image), height and named outputs, list parameters and format version 2's machinery, with export and the swatch sheet showing them
- T-153: A node can ask for an input's rectangle after reading another input, so warp at a large amount reads the few tiles its offsets reach rather than a box widened by the whole amount
- T-137: blur and gradient_map nodes, and samples/graphs/painted-metal.texgraph with its PNGs
- T-139: A graph declares named parameters that drive its node parameters, and swatch and export take overrides, so one graph gives variants; the bricks sample exposes mortar, tint and jitter
- T-156: swatch takes a sphere tiling (whole repeats, default 1), so a material can be judged on the sphere at a texel density nearer a game's; the samples' committed sheets use 3
- T-140: A graph node uses another .texgraph by path, its exposed parameters set by name and its input nodes as ports; grime and old-wall sample graphs
