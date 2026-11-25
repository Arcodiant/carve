using System.Globalization;
using Carve.Core.Baking;
using Patina.Graphs.Evaluation;
using Patina.Graphs.Nodes;

namespace Carve.Graphs;

/// <summary>
/// The mesh-input node types (M-29): <c>mesh_curvature</c>, <c>mesh_occlusion</c>, <c>mesh_mask</c> and <c>mesh_position</c>,
/// each reading its map from the <see cref="IMeshMaps"/> it was registered with and copying the texels of each tile out of it.
/// With no source every one gives its neutral value everywhere, so a graph that reads the mesh still evaluates in patina.
/// </summary>
public static class MeshNodes
{
    /// <summary><paramref name="registry"/> with the four types, bound to <paramref name="source"/> (null: neutral everywhere).</summary>
    public static NodeRegistry Register(NodeRegistry registry, IMeshMaps? source)
    {
        ArgumentNullException.ThrowIfNull(registry);
        return registry.With(new MeshCurvatureNode(source), new MeshOcclusionNode(source), new MeshMaskNode(source), new MeshPositionNode(source));
    }

    /// <summary>The four types' names.</summary>
    public static IReadOnlyList<string> TypeNames { get; } =
        [MeshCurvatureNode.TypeName, MeshOcclusionNode.TypeName, MeshMaskNode.TypeName, MeshPositionNode.TypeName];

    /// <summary>Copies <paramref name="map"/>'s texels of the tile into <c>out</c>, or fills it with <paramref name="neutral"/> when there is no map.</summary>
    internal static void Copy(NodeEvaluation e, BakeImage? map, float neutral)
    {
        var output = e.Output();
        if (map is null)
        {
            for (var y = e.Rect.Y; y < e.Rect.Bottom; y++) output.Row(y).Fill(neutral);
            return;
        }
        if (map.Width != e.Size || map.Height != e.Size || map.Channels != output.Channels)
            throw new InvalidOperationException($"node '{e.NodeId}': the source gave a {map.Width}x{map.Height} map of {map.Channels} channel(s) for a {e.Size} image of {output.Channels}");
        var values = map.Values;
        var run = e.Rect.Width * map.Channels;
        for (var y = e.Rect.Y; y < e.Rect.Bottom; y++)
            values.Slice((y * e.Size + e.Rect.X) * map.Channels, run).CopyTo(output.Row(y));
    }

    /// <summary>A float parameter that must be above 0, which the file's range (from 0) cannot refuse.</summary>
    internal static double Positive(NodeEvaluation e, string name, string unit) =>
        NotPositive(e.Parameters, name, unit) is { } wrong
            ? throw new MeshParameterException($"nodes['{e.NodeId}'].params['{name}']: {wrong}")
            : e.Parameters.Value(name).Number;

    /// <summary>The fault of a float parameter not above 0 for <see cref="NodeType.CheckParameters"/>, so validation refuses it (RV-82) as the bake does.</summary>
    internal static IEnumerable<(string? Parameter, string Message)> CheckPositive(NodeParameters parameters, string name, string unit) =>
        NotPositive(parameters, name, unit) is { } wrong ? [(name, wrong)] : [];

    private static string? NotPositive(NodeParameters parameters, string name, string unit)
    {
        var v = parameters.Value(name).Number;
        return v > 0 ? null : $"is {v.ToString(CultureInfo.InvariantCulture)}, but must be above 0 {unit}";
    }
}
