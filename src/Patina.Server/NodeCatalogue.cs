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
        ["output"] = "one of the material's images, at most one per role: base_colour takes colour or grey, normal colour, roughness and occlusion grey",
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
            s.Append(type.Parameters.Count == 0 ? "none" : string.Join("; ", type.Parameters.Select(p => $"{p.Name}: {p.Describe()}, default {p.Default}")));
            s.Append(".\n");
        }
        return s.ToString();
    }
}
