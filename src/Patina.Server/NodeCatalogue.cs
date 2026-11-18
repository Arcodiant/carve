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
