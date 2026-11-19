# Unreleased

- T-110: Patina.Graphs: the engine-free .texgraph format read back bit-exact, deterministic banded evaluation, and faults that name the node and port
- T-111: Carve.Hosting: the engine thread, extension loading, host set-up, fixed sky and texture upload rules move out of Carve.Server, every frame unchanged
- T-112: noise, gradient, levels, blend and height_to_normal, and samples/graphs/planks.texgraph with its PNGs kept equal to the graph
- T-113: patina, a second MCP server: define_graph, edit_graph, get_graph, save, open, push_snapshot and pop_snapshot
- T-115: patina's export writes each output as a PNG at any power of two to 8192, in bounded memory
- T-116: samples/barrel-planks.carve: M-7's barrel with its staves from the planks graph, in a scenario of its own
- T-114: patina's swatch: the sheet, flat, tiled 3 x 3 and a sphere through Material.Pbr under Carve's fixed sky
- T-117: Fresh agents build a wood material in patina from the tool descriptions alone, recover from a broken starter graph and put it on the barrel
- T-128: patina's sphere swatch lit by carve's ambient and directional lights as well as the fixed sky, so a swatch shows a material as carve's render does
