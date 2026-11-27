using System.Text;
using Patina.Graphs.Nodes;

namespace Patina.Server;

/// <summary>
/// The node types, read from the registry so the list is always what patina accepts, served by the nodes tool (T-170:
/// Claude Code cuts a tool description at 4,096 characters, and the catalogue in define_graph's never arrived whole):
/// each type's inputs (optional ones marked), outputs when there are several, and parameters with their ranges and
/// defaults, after a line of what the type does, then its worked examples. define_graph's description carries only
/// <see cref="Index"/>.
/// </summary>
public static class NodeCatalogue
{
    /// <summary>What each type does, by name, in T-110's and T-112's words. A type with no line here is listed by its ports alone.</summary>
    private static readonly Dictionary<string, string> Says = new(StringComparer.Ordinal)
    {
        ["colour"] = "a flat colour",
        ["noise"] = "greyscale value noise in 0..1: cells is whole lattice cells across the image, so it tiles; interpolation smooth or step (one value per cell); each octave doubles the cells and halves the weight; seed is added to the graph's seed",
        ["gradient"] = "greyscale ramp along axis u (across) or v (down), repeat times; linear runs 0 to 1 across each repeat, triangle is 0 at each repeat's edges and 1 in its middle",
        ["levels"] = "remaps in_low..in_high to out_low..out_high with gamma, clamped; out_low above out_high inverts; greyscale or colour in, the same out",
        ["blend"] = "combines a and b by mode at opacity; colour if either input is (grey spread to grey); in mix the mask takes a where it is 0 and b where it is 1; difference is |a - b|, so a noise against a flat colour at a level is dark in a thin line where the noise crosses it (veins, cracks: levels it to widen or invert)",
        ["height_to_normal"] = "a tangent-space normal (glTF: green up) from a greyscale height, strength the slope per image width at height 1, wrapping at the edges",
        ["normal_blend"] = "a detail normal laid over a base normal by reoriented normal mapping: the detail is turned into the base's frame, so a bump on a bevel keeps its tilt (adding the x and y would flatten it). base and detail are colour normals in -1..1 as height_to_normal gives them; strength 0..1 moves the detail toward flat, so 0 is the base; the result is unit length",
        ["tiles"] = "a layout of tiles or bricks across the image, every size relative: count [x, y] tiles; stagger shifts every second row by that many tiles (0.5 is running bond); gap and bevel are parts of a tile's width (gap 0 to 0.45; the bevel is the ramp inside each edge); jitter moves each tile's size and place by the hash, inside its slot; orientation rows or columns. Outputs: mask (1 on a tile, 0 in the gap), height (0 in the gap, rising over the bevel to 1), random (one value per tile), uv (colour: each texel's place in its tile, red and green 0..1, for a pattern laid inside each tile)",
        ["stripes"] = "greyscale stripes, 1 on a stripe: count whole stripes across the image, axis u (upright stripes), v (lying stripes) or diagonal (count along each axis), width a part of a period, softness the edge ramp's reach each side of the edge as a part of a period (the ramp is twice it); a stripe is centred on each multiple of 1/count",
        ["shape"] = "greyscale, one shape per cell of count [x, y] cells, 1 inside: kind circle, square, ring, diamond or polygon (sides 3 to 12), size the part of the cell's shorter side it spans, softness 0..1 the ramp inward from the edge, rotation in degrees inside the cell",
        ["worley"] = "Worley cells: one feature point in each of cells [x, y] cells (wrapping, so it tiles), placed by hash, jitter 0 (cell centre) to 1 (anywhere in the cell); distances are in cell widths by metric; f1 is the distance to the nearest point, edge is f2 - f1 (0 on the boundary between cells), random is one value 0..1 for each cell; seed is added to the graph's seed",
        ["fbm"] = "fractal noise in 0..1: octaves of value or gradient (Perlin-style) basis on cells [x, y], the cells doubling each octave, each octave gain times the weight of the one before, normalised; style plain, ridged (1 - |2n - 1|) or billow (|2n - 1|); seed is added to the graph's seed",
        ["warp"] = "bends in by by: a colour by gives the u and v offsets in its R and G about 0.5, a greyscale by the offset along angle degrees (0 is u, 90 is v, down); amount is the largest offset as a fraction of the image, in 0..0.25; read bilinear and wrapping",
        ["blur"] = "a box blur that wraps (so it tiles): radius is a part of the image's width, 0 to 0.1; quality box is one pass over 2r + 1 texels, smooth three passes (close to a Gaussian); greyscale or colour in, the same out; radius 0 is the input; a large radius on a large image is slow",
        ["gradient_map"] = "colours a greyscale in through positions (2 to 8 numbers from 0 to 1, low to high) and colours (as many \"#rrggbb\"): linear between stops in sRGB, the first colour below the first stop and the last above the last; a count mismatch or positions out of order is a fault",
        ["graph"] = "another .texgraph used as one node (version 2): path is relative to this graph's file (a graph never saved: the tool folder); its other params are the nested graph's exposed parameters by name, checked against their ranges (an exposed parameter of this graph may drive one, as \"wall.mortar\", taking its range); its inputs are the nested graph's input nodes by name, and its outputs the nested graph's outputs by role or name (wall.base_colour). It is expanded inline when validated, its nodes renamed <node>/<id> (wall/layout), so a fault inside names that path and the file; the outer graph's size and seed rule, and the nested graph's own are not used; nesting goes 8 files deep, and a cycle through files is a fault",
        ["image"] = "a PNG read into the graph (version 2): path is relative to this graph's file (a graph never saved: the tool folder), no extension added; kind colour (r, g, b / 255), grey (r / 255) or normal (each of r, g, b as 2c / 255 - 1, made unit length: a tangent-space normal as height_to_normal gives, for a baked normal map); alpha is not read. The image covers the whole square at any size, read texel for texel at the graph's size and bilinear, wrapping, at others; a missing file or one it cannot read (16-bit, palette, interlaced) is a fault on path",
        ["input"] = "one of the graph's image inputs (version 2), a port of a graph node that uses this graph: name is the port, kind grey or colour, default the value used when the graph is evaluated on its own (a number 0 to 1, or \"#rrggbb\"); a graph with an input lacking a default saves but cannot be evaluated on its own. Its value input is wired only by the graph node using it, so leave it unconnected",
        ["transform"] = "moves, repeats, mirrors, turns and scales in, about the image centre, applied in this order: tile [x, y] whole repeats; scale the image's size (2 doubles it so a half shows, 0.5 halves it so it shows twice); rotation degrees clockwise as seen; mirror none, u (left for right), v (top for bottom) or both; offset [u, v] fractions of the image, wrapping (positive u moves it right); filter bilinear or nearest, reading wrapping. It still tiles at a whole number of quarter turns (0, 90, 180, 270, -90 ...) with scale 1 or 1/n (0.5, 0.25: n repeats); a whole-number scale above 1 zooms in and does not tile, and any other rotation or scale generally does not tile, and check's seam rule reports it",
        ["distance"] = "each texel's distance to the edge of the mask in (inside at 0.5 or more), from its centre to the nearest texel centre on the other side, as a fraction of range (a part of the image width, 0 to 0.25), clamped to 1; side inside (outside texels 0), outside (inside texels 0) or both; exact Euclidean and wrapping, so it tiles: a bevel is the inside distance",
        ["mesh_curvature"] = "grey from the mesh, not an image: 0.5 on flat surface, toward 1 at convex edges, toward 0 at concave ones; width is metres on the mesh (above 0 to 1), so the picture is the same at every size. In patina a stand-in: 0.5 everywhere, and the real map comes from the mesh when a carve material names the graph",
        ["mesh_occlusion"] = "grey from the mesh: 1 where open, toward 0 where other geometry shadows it; rays 1 to 1024 cast per texel, reach in metres (above 0 to 10). In patina a stand-in: 1 everywhere, and the real map comes from the mesh when a carve material names the graph",
        ["mesh_mask"] = "grey from the mesh: 1 on the faces of one group or one material, 0 elsewhere; give exactly one of group or material, a name as carve has it. In patina a stand-in: 1 everywhere, and the real map comes from the mesh when a carve material names the graph",
        ["mesh_position"] = "colour from the mesh: r, g, b are the x, y, z of the surface, each 0 to 1 across the mesh's own bounding box at rest. In patina a stand-in: #808080 everywhere, and the real map comes from the mesh when a carve material names the graph",
        ["output"] = "one of the material's images, at most one per role or name: base_colour takes colour or grey, normal colour, roughness, metallic, occlusion and height grey; with a name in place of a role, an extra image <name>.png, colour or grey",
    };

    /// <summary>A few words for each type, for define_graph's index.</summary>
    private static readonly Dictionary<string, string> Gist = new(StringComparer.Ordinal)
    {
        ["colour"] = "a flat colour",
        ["noise"] = "value noise",
        ["gradient"] = "a ramp",
        ["levels"] = "remap a range",
        ["blend"] = "combine two by a mode",
        ["height_to_normal"] = "a normal from a height",
        ["normal_blend"] = "bump on a normal",
        ["tiles"] = "tiles or bricks",
        ["stripes"] = "stripes",
        ["shape"] = "a shape per cell",
        ["worley"] = "cells and their edges",
        ["fbm"] = "fractal noise",
        ["warp"] = "bend by another image",
        ["blur"] = "a wrapping blur",
        ["gradient_map"] = "colour a grey by stops",
        ["graph"] = ".texgraph as a node",
        ["image"] = "a PNG read in",
        ["input"] = "an image input of this graph",
        ["transform"] = "move, repeat, mirror, turn, scale",
        ["distance"] = "distance to a mask's edge",
        ["mesh_curvature"] = "curvature of the mesh (stand-in)",
        ["mesh_occlusion"] = "occlusion of the mesh (stand-in)",
        ["mesh_mask"] = "a mesh group or material (stand-in)",
        ["mesh_position"] = "position on the mesh (stand-in)",
        ["output"] = "one of the material's images",
    };

    /// <summary>T-170's worked examples, for the types define_graph's own examples did not show. Tests run them as written.</summary>
    public const string WorkedGradient =
        """{"graph":{"version":1,"size":256,"seed":0,"nodes":{"ramp":{"type":"gradient","params":{"axis":"v","repeat":2,"shape":"triangle"}},"out":{"type":"output","inputs":{"in":"ramp"},"params":{"role":"base_colour"}}}}}""";

    public const string WorkedLevels =
        """{"graph":{"version":1,"size":256,"seed":0,"nodes":{"tone":{"type":"noise","params":{"cells":[8,8]}},"rough":{"type":"levels","inputs":{"in":"tone"},"params":{"in_low":0.2,"in_high":0.8,"out_low":0.5,"out_high":0.9}},"out":{"type":"output","inputs":{"in":"rough"},"params":{"role":"roughness"}}}}}""";

    public const string WorkedNormal =
        """{"graph":{"version":1,"size":256,"seed":0,"nodes":{"layout":{"type":"tiles","params":{"count":[4,4],"gap":0.05,"bevel":0.15}},"bump":{"type":"height_to_normal","inputs":{"height":"layout.height"},"params":{"strength":2}},"out":{"type":"output","inputs":{"in":"bump"},"params":{"role":"normal"}}}}}""";

    public const string WorkedNormalBlend =
        """{"graph":{"version":1,"size":256,"seed":0,"nodes":{"layout":{"type":"tiles","params":{"count":[4,4],"gap":0.05,"bevel":0.15}},"bevel":{"type":"height_to_normal","inputs":{"height":"layout.height"},"params":{"strength":2}},"grain":{"type":"fbm","params":{"cells":[16,16]}},"bump":{"type":"height_to_normal","inputs":{"height":"grain"},"params":{"strength":0.1}},"both":{"type":"normal_blend","inputs":{"base":"bevel","detail":"bump"},"params":{"strength":0.8}},"out":{"type":"output","inputs":{"in":"both"},"params":{"role":"normal"}}}}}""";

    public const string WorkedStripes =
        """{"graph":{"version":1,"size":256,"seed":0,"nodes":{"weft":{"type":"stripes","params":{"count":16,"axis":"v","width":0.4,"softness":0.2}},"out":{"type":"output","inputs":{"in":"weft"},"params":{"role":"base_colour"}}}}}""";

    public const string WorkedShape =
        """{"graph":{"version":1,"size":256,"seed":0,"nodes":{"studs":{"type":"shape","params":{"kind":"circle","count":[6,6],"size":0.6,"softness":0.2}},"out":{"type":"output","inputs":{"in":"studs"},"params":{"role":"base_colour"}}}}}""";

    public const string WorkedWarp =
        """{"graph":{"version":1,"size":256,"seed":0,"nodes":{"grain":{"type":"stripes","params":{"count":12}},"wobble":{"type":"fbm","params":{"cells":[4,4]}},"bent":{"type":"warp","inputs":{"in":"grain","by":"wobble"},"params":{"amount":0.05}},"out":{"type":"output","inputs":{"in":"bent"},"params":{"role":"base_colour"}}}}}""";

    public const string WorkedInput =
        """{"graph":{"version":2,"size":256,"seed":0,"nodes":{"src":{"type":"input","params":{"name":"in","kind":"grey","default":0.5}},"dark":{"type":"levels","inputs":{"in":"src"},"params":{"out_high":0.6}},"out":{"type":"output","inputs":{"in":"dark"},"params":{"role":"base_colour"}}}}}""";

    public const string WorkedDistance =
        """{"graph":{"version":1,"size":256,"seed":0,"nodes":{"layout":{"type":"tiles","params":{"count":[4,4],"gap":0.1}},"bevel":{"type":"distance","inputs":{"in":"layout.mask"},"params":{"range":0.05,"side":"inside"}},"out":{"type":"output","inputs":{"in":"bevel"},"params":{"name":"bevel"}}}}}""";

    public const string WorkedMesh =
        """{"graph":{"version":1,"size":256,"seed":0,"nodes":{"iron":{"type":"colour","params":{"colour":"#303236"}},"steel":{"type":"colour","params":{"colour":"#c4c6c8"}},"edges":{"type":"mesh_curvature","params":{"width":0.02}},"band":{"type":"levels","inputs":{"in":"edges"},"params":{"in_low":0.6,"in_high":0.9}},"grain":{"type":"fbm","params":{"cells":[12,12]}},"chips":{"type":"levels","inputs":{"in":"grain"},"params":{"in_low":0.45,"in_high":0.6}},"wear":{"type":"blend","inputs":{"a":"band","b":"chips"},"params":{"mode":"multiply"}},"worn":{"type":"blend","inputs":{"a":"iron","b":"steel","mask":"wear"},"params":{"mode":"mix"}},"out":{"type":"output","inputs":{"in":"worn"},"params":{"role":"base_colour"}}}}}""";

    public const string WorkedMeshOcclusion =
        """{"graph":{"version":1,"size":256,"seed":0,"nodes":{"occ":{"type":"mesh_occlusion","params":{"rays":32,"reach":0.15}},"grime":{"type":"levels","inputs":{"in":"occ"},"params":{"in_low":0.45,"out_low":1,"out_high":0}},"out":{"type":"output","inputs":{"in":"grime"},"params":{"name":"grime"}}}}}""";

    public const string WorkedMeshMask =
        """{"graph":{"version":1,"size":256,"seed":0,"nodes":{"steel":{"type":"mesh_mask","params":{"group":"steel"}},"out":{"type":"output","inputs":{"in":"steel"},"params":{"name":"steel"}}}}}""";

    public const string WorkedMeshPosition =
        """{"graph":{"version":1,"size":256,"seed":0,"nodes":{"where":{"type":"mesh_position"},"out":{"type":"output","inputs":{"in":"where"},"params":{"role":"base_colour"}}}}}""";

    public const string WorkedImage =
        """{"graph":{"version":2,"size":256,"seed":0,"nodes":{"baked":{"type":"image","params":{"path":"bricks/normal.png","kind":"normal"}},"out":{"type":"output","inputs":{"in":"baked"},"params":{"role":"normal"}}}}}""";

    /// <summary>What a fault-free define_graph of <paramref name="nodes"/> nodes at size 256 returns.</summary>
    public static string Evaluates(int nodes) => $"define_graph: the open graph is now {nodes} nodes, size 256, seed 0; no faults: it evaluates";

    /// <summary>Each type's worked examples: what it shows, the define_graph arguments, and what define_graph returns. Every type has one.</summary>
    public static IReadOnlyList<(string Says, string Graph, string Returns)> Examples(string type) => type switch
    {
        "colour" or "noise" or "blend" => [("a tone per stave (eight staves across u, one random grey each) on a wood colour", DefineGraphTool.Worked, DefineGraphTool.WorkedResult)],
        "gradient" => [("two triangle ramps down v", WorkedGradient, Evaluates(2))],
        "levels" => [("noise narrowed and lifted into a roughness of 0.5 to 0.9", WorkedLevels, Evaluates(3))],
        "height_to_normal" => [("a normal from a tile layout's bevelled height", WorkedNormal, Evaluates(3))],
        "normal_blend" => [("an fbm bump laid over the bevels of a tile layout, both made by height_to_normal", WorkedNormalBlend, Evaluates(6))],
        "tiles" => [("a running-bond brick layout, 4 bricks across and 8 rows, every second row shifted half a brick, its height as the base colour (a type with several outputs is named \"id.port\")", DefineGraphTool.WorkedTiles, DefineGraphTool.WorkedTilesResult)],
        "stripes" => [("16 soft lying stripes", WorkedStripes, Evaluates(2))],
        "shape" => [("a 6 x 6 grid of soft round studs", WorkedShape, Evaluates(2))],
        "worley" => [("a tone per stone (worley's random output, one grey for each of 6 x 6 cells) on a stone colour", DefineGraphTool.WorkedStones, DefineGraphTool.WorkedResult)],
        "fbm" or "blur" or "gradient_map" => [("a mossy colour from a soft cloud (fbm noise, blurred, then coloured by a gradient_map: the greyscale 0 takes the first colour and 1 the last)", DefineGraphTool.WorkedMap, DefineGraphTool.WorkedMapResult)],
        "warp" => [("upright stripes bent by fractal noise, like wood grain", WorkedWarp, Evaluates(4))],
        "graph" =>
        [
            ("bricks.texgraph (beside the graph, or in the tool folder for a graph never saved) as the node wall, mortar 0.06 set on it by name, its base_colour and roughness outputs taken as wall.base_colour and wall.roughness", DefineGraphTool.WorkedNested, DefineGraphTool.WorkedNestedResult),
            ("the same with mortar an exposed parameter of this graph driving \"wall.mortar\", taking bricks' range for it, 0 to 0.45 (swatch and export then set it by name)", DefineGraphTool.WorkedPassThrough, DefineGraphTool.WorkedNestedResult),
        ],
        "image" => [("a normal map read from bricks/normal.png (beside the graph, or in the tool folder for a graph never saved) as the graph's normal", WorkedImage, Evaluates(2))],
        "input" => [("a filter darkening its input in (a graph node using this file connects it; on its own it evaluates at the default)", WorkedInput, Evaluates(3))],
        "transform" => [("stripes turned 30 degrees. It evaluates, but a turn that is not a quarter turn does not tile, so the image shows a seam where it repeats and check's seam rule reports it; rotation 90 would tile", DefineGraphTool.WorkedTurn, DefineGraphTool.WorkedTurnResult)],
        "distance" => [("a bevel inside each tile, written as the named output bevel", WorkedDistance, Evaluates(3))],
        "output" => [("the metal plate: grey, metallic from a gradient across u, roughness its inverse, and a named output edge (a triangle gradient down v); export's and swatch's examples use it", DefineGraphTool.WorkedMetal, DefineGraphTool.WorkedMetalResult)],
        "mesh_curvature" => [("a mesh_curvature node, narrowed by levels and multiplied by a noise (fbm through levels), as the mask of a blend that wears a base colour toward bright steel: wear that comes and goes along an edge reads as wear, an even band as an outline (in patina the stand-in gives 0.5, so the mask is 0 and the image is the base colour)", WorkedMesh, Evaluates(9))],
        "mesh_occlusion" => [("a grime mask: one minus the occlusion (32 rays, reach 0.15 m), as a named output; in patina the stand-in is 1, so it is 0 everywhere", WorkedMeshOcclusion, Evaluates(3))],
        "mesh_mask" => [("the faces of the group steel as a named output; in patina the stand-in is 1 everywhere", WorkedMeshMask, Evaluates(2))],
        "mesh_position" => [("the position on the mesh as the base colour; in patina the stand-in is #808080", WorkedMeshPosition, Evaluates(2))],
        _ => [],
    };

    /// <summary>define_graph's index: every type's name and a few words, in the registry's order.</summary>
    public static string Index(NodeRegistry registry) =>
        string.Join("; ", registry.Names.Select(n => Gist.TryGetValue(n, out var g) ? $"{n} ({g})" : n));

    /// <summary>One type's entry: what it does, its ports and parameters, then its worked examples.</summary>
    public static string Entry(NodeRegistry registry, string name)
    {
        var type = registry.Find(name)!;
        var s = new StringBuilder();
        s.Append("- ").Append(name);
        if (Says.TryGetValue(name, out var says)) s.Append(": ").Append(says);
        s.Append(". Inputs: ");
        s.Append(type.Inputs.Count == 0 ? "none" : string.Join(", ", type.Inputs.Select(i => i.Name + (i.Required ? "" : " (optional)"))));
        if (type.Outputs.Count > 1) s.Append(". Outputs: ").Append(string.Join(", ", type.Outputs));
        s.Append(". Params: ");
        s.Append(type.Parameters.Count == 0 ? "none" : string.Join("; ", type.Parameters.Select(p => $"{p.Name}: {p.Describe()}, " + (p.Optional ? "optional, no default" : $"default {p.Default}"))));
        s.Append(".\n");
        foreach (var (example, graph, returns) in Examples(name))
            s.Append("Worked example, ").Append(example).Append(":\ndefine_graph ").Append(graph).Append("\nreturns: ").Append(returns).Append('\n');
        return s.ToString();
    }

    /// <summary>Every type's entry, in the registry's order.</summary>
    public static string Text(NodeRegistry registry) => string.Concat(registry.Names.Select(n => Entry(registry, n)));
}
