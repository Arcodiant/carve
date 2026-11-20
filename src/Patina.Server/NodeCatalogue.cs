using System.Text;
using Patina.Graphs.Nodes;

namespace Patina.Server;

/// <summary>
/// The node types define_graph's description lists, read from the registry so the list is always what patina accepts:
/// each type's inputs (optional ones marked), outputs when there are several, and parameters with their ranges and
/// defaults, after a line of what the type does.
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
        ["blend"] = "combines a and b by mode at opacity; colour if either input is (grey spread to grey); in mix the mask takes a where it is 0 and b where it is 1",
        ["height_to_normal"] = "a tangent-space normal (glTF: green up) from a greyscale height, strength the slope per image width at height 1, wrapping at the edges",
        ["tiles"] = "a layout of tiles or bricks across the image, every size relative: count [x, y] tiles; stagger shifts every second row by that many tiles (0.5 is running bond); gap and bevel are parts of a tile's width (gap 0 to 0.45; the bevel is the ramp inside each edge); jitter moves each tile's size and place by the hash, inside its slot; orientation rows or columns. Outputs: mask (1 on a tile, 0 in the gap), height (0 in the gap, rising over the bevel to 1), random (one value per tile), uv (colour: each texel's place in its tile, red and green 0..1, for a pattern laid inside each tile)",
        ["stripes"] = "greyscale stripes, 1 on a stripe: count whole stripes across the image, axis u (upright stripes), v (lying stripes) or diagonal (count along each axis), width a part of a period, softness the edge ramp as a part of a period; a stripe is centred on each multiple of 1/count",
        ["shape"] = "greyscale, one shape per cell of count [x, y] cells, 1 inside: kind circle, square, ring, diamond or polygon (sides 3 to 12), size the part of the cell's shorter side it spans, softness 0..1 the ramp inward from the edge, rotation in degrees inside the cell",
        ["worley"] = "Worley cells: one feature point in each of cells [x, y] cells (wrapping, so it tiles), placed by hash, jitter 0 (cell centre) to 1 (anywhere in the cell); distances are in cell widths by metric; f1 is the distance to the nearest point, edge is f2 - f1 (0 on the boundary between cells), random is one value 0..1 for each cell; seed is added to the graph's seed",
        ["fbm"] = "fractal noise in 0..1: octaves of value or gradient (Perlin-style) basis on cells [x, y], the cells doubling each octave, each octave gain times the weight of the one before, normalised; style plain, ridged (1 - |2n - 1|) or billow (|2n - 1|); seed is added to the graph's seed",
        ["warp"] = "bends in by by: a colour by gives the u and v offsets in its R and G about 0.5, a greyscale by the offset along angle degrees (0 is u, 90 is v, down); amount is the largest offset as a fraction of the image, in 0..0.25; read bilinear and wrapping",
        ["blur"] = "a box blur that wraps (so it tiles): radius is a part of the image's width, 0 to 0.1; quality box is one pass over 2r + 1 texels, smooth three passes (close to a Gaussian); greyscale or colour in, the same out; radius 0 is the input; a large radius on a large image is slow",
        ["gradient_map"] = "colours a greyscale in through positions (2 to 8 numbers from 0 to 1, low to high) and colours (as many \"#rrggbb\"): linear between stops in sRGB, the first colour below the first stop and the last above the last; a count mismatch or positions out of order is a fault",
        ["graph"] = "another .texgraph used as one node (version 2): path is relative to this graph's file (a graph never saved: the tool folder); its other params are the nested graph's exposed parameters by name, checked against their ranges (an exposed parameter of this graph may drive one, as \"wall.mortar\", taking its range); its inputs are the nested graph's input nodes by name, and its outputs the nested graph's outputs by role or name (wall.base_colour). It is expanded inline when validated, its nodes renamed <node>/<id> (wall/layout), so a fault inside names that path and the file; the outer graph's size and seed rule, and the nested graph's own are not used; nesting goes 8 files deep, and a cycle through files is a fault",
        ["input"] = "one of the graph's image inputs (version 2), a port of a graph node that uses this graph: name is the port, kind grey or colour, default the value used when the graph is evaluated on its own (a number 0 to 1, or \"#rrggbb\"); a graph with an input lacking a default saves but cannot be evaluated on its own. Its value input is wired only by the graph node using it, so leave it unconnected",
        ["transform"] = "moves, repeats, mirrors, turns and scales in, about the image centre, applied in this order: tile [x, y] whole repeats; scale the image's size (2 doubles it so a half shows, 0.5 halves it so it shows twice); rotation degrees clockwise as seen; mirror none, u (left for right), v (top for bottom) or both; offset [u, v] fractions of the image, wrapping (positive u moves it right); filter bilinear or nearest, reading wrapping. It still tiles at a whole number of quarter turns (0, 90, 180, 270, -90 ...) with scale 1 or 1/n (0.5, 0.25); any other rotation or scale generally does not tile, and check's seam rule reports it",
        ["distance"] = "each texel's distance to the edge of the mask in (inside at 0.5 or more), from its centre to the nearest texel centre on the other side, as a fraction of range (a part of the image width, 0 to 0.25), clamped to 1; side inside (outside texels 0), outside (inside texels 0) or both; exact Euclidean and wrapping, so it tiles: a bevel is the inside distance",
        ["output"] = "one of the material's images, at most one per role or name: base_colour takes colour or grey, normal colour, roughness, metallic, occlusion and height grey; with a name in place of a role, an extra image <name>.png, colour or grey",
    };

    public static string Text(NodeRegistry registry)
    {
        var s = new StringBuilder();
        foreach (var name in registry.Names)
        {
            var type = registry.Find(name)!;
            s.Append("- ").Append(name);
            if (Says.TryGetValue(name, out var says)) s.Append(": ").Append(says);
            s.Append(". Inputs: ");
            s.Append(type.Inputs.Count == 0 ? "none" : string.Join(", ", type.Inputs.Select(i => i.Name + (i.Required ? "" : " (optional)"))));
            if (type.Outputs.Count > 1) s.Append(". Outputs: ").Append(string.Join(", ", type.Outputs));
            s.Append(". Params: ");
            s.Append(type.Parameters.Count == 0 ? "none" : string.Join("; ", type.Parameters.Select(p => $"{p.Name}: {p.Describe()}, " + (p.Optional ? "optional, no default" : $"default {p.Default}"))));
            s.Append(".\n");
        }
        return s.ToString();
    }
}
