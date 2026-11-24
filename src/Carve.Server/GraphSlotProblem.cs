using System.Globalization;
using Carve.Core;
using Carve.Core.Baking;
using Carve.Graphs;
using Patina.Graphs;

namespace Carve.Server;

/// <summary>
/// The one error a graph slot that cannot be baked gives (T-163; T-166 owns its wording): the material, the slot and the
/// cause's own message. The causes: the file gone or unreadable and an output the slot cannot take
/// (<see cref="GraphSlotException"/>), a fault in the graph (<see cref="GraphFaultException"/>), no UVs, overlapping UVs,
/// a bad size or a missing group or material (<see cref="MeshMapsException"/>), and a mesh that is not there (<see cref="ArgumentException"/>).
/// </summary>
internal static class GraphSlotProblem
{
    /// <summary>The wording of every graph slot problem: <c>materials['iron'].base_colour_texture: &lt;cause&gt;</c>, the cause as <see cref="Cause"/> words it.</summary>
    public static string Of(string material, string slot, Exception cause) => $"materials['{material}'].{slot}: {Cause(cause)}";

    /// <summary>A cause's message: a missing file lists the .texgraph files beside it, a mesh's layout error names the mesh, the rest are the cause's own.</summary>
    public static string Cause(Exception cause) => cause switch
    {
        GraphMissingException m => Missing(m.Path, ".texgraph"),
        MeshLayoutException l => $"mesh '{l.Mesh}': {l.Message}",
        _ => cause.Message,
    };

    public const int ListLimit = 20;

    /// <summary>The missing-file error: the resolved path and the files with <paramref name="extension"/> that are in its folder (T-81).</summary>
    public static string Missing(string fullPath, string extension)
    {
        var folder = Path.GetDirectoryName(fullPath)!;
        var head = $"no file at {fullPath}; ";
        if (!Directory.Exists(folder)) return head + $"the folder {folder} does not exist";

        List<string> names;
        try
        {
            names = Directory.EnumerateFiles(folder).Select(f => Path.GetFileName(f)!)
                .Where(n => n.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                .OrderBy(n => n, StringComparer.Ordinal).ToList();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return head + $"the folder {folder} could not be listed: {e.Message}";
        }

        var inv = CultureInfo.InvariantCulture;
        if (names.Count == 0) return head + $"there are no {extension} files in {folder}";
        var shown = string.Join(", ", names.Take(ListLimit));
        var rest = names.Count > ListLimit ? $", and {(names.Count - ListLimit).ToString(inv)} more" : "";
        return head + $"the {extension} files in {folder} ({names.Count.ToString(inv)}) are: {shown}{rest}";
    }

    /// <summary>The error render and export give when a bake fails: <see cref="Of"/>, and that the model is as it was.</summary>
    public static GraphSlotProblemException Failed(string material, string slot, Exception cause) =>
        new(Of(material, slot, cause) + "; the model is unchanged: define the material again with a graph that bakes, or restore the file", cause);

    /// <summary>True for a cause <see cref="Of"/> reports; anything else is a fault of carve's own and is let through.</summary>
    public static bool IsCause(Exception e) => e is GraphSlotException or GraphFaultException or MeshMapsException or ArgumentException or IOException;

    /// <summary>Every graph slot of <paramref name="m"/> that cannot be baked whatever mesh wears it (the file, its faults, the output, the slot table), in slot order.</summary>
    public static IEnumerable<string> Slots(Material m)
    {
        foreach (var (field, slot) in m.Textures)
            if (slot is MaterialGraph graph)
            {
                string? problem = null;
                try { GraphSlotBake.Validate(graph, field); }
                catch (Exception e) when (IsCause(e)) { problem = Of(m.Name, field, e); }
                if (problem is not null) yield return problem;
            }
    }

    /// <summary>
    /// What stops <paramref name="m"/>'s graph slots reading <paramref name="mesh"/> as it is now (no casting, no evaluating): no UVs,
    /// overlapping UVs, a <c>mesh_mask</c> naming a group or material it lacks, and the faces that use the material but are unmapped.
    /// Empty for a material with no slot that reads the mesh, and for a slot whose file cannot be read (<see cref="Slots"/> says so).
    /// </summary>
    public static IEnumerable<string> OnMesh(Model model, Material m, string mesh)
    {
        if (model.Find(mesh) is null) yield break; // a part's level is not a model mesh
        var source = new MeshBakeSource(model, mesh);
        var reported = new HashSet<string>();
        foreach (var (field, slot) in m.Textures)
        {
            if (slot is not MaterialGraph graph) continue;
            GraphSlotFiles files;
            try
            {
                files = GraphSlotBake.Read(graph.Path);
                GraphSlotBake.Validate(graph, field);
            }
            catch (Exception e) when (IsCause(e)) { continue; }
            if (!files.ReadsMesh) continue;
            BakeLayout? layout = null;
            MeshMapsException? refused = null;
            try { layout = source.Layout(graph.Size); }
            catch (MeshMapsException e) { refused = e; }
            if (refused is not null && reported.Add(refused.Message)) yield return Of(m.Name, field, refused);
            foreach (var (node, kind, name) in GraphSlotBake.MaskNames(files))
                if (!(kind == MaskSource.Group ? source.Groups : source.Materials).Contains(name))
                {
                    var e = new MeshMaskException(mesh, kind, name, source.Groups, source.Materials);
                    if (reported.Add(node + e.Message)) yield return Of(m.Name, field, e).Replace(": mesh '", $": nodes['{node}']: mesh '");
                }
            if (layout is not null && reported.Add("unmapped"))
            {
                var style = model.Find(mesh)!.Style ?? FaceStyle.None;
                var bare = layout.Unmapped.Where(f => style.MaterialOf(f) == m.Name).ToArray();
                if (bare.Length > 0)
                    yield return $"materials['{m.Name}']: faces[{string.Join(", ", bare.Take(10))}{(bare.Length > 10 ? $", and {bare.Length - 10} more" : "")}] of '{mesh}' are unmapped, so '{m.Name}' bakes nothing onto them; give them UVs with project_uvs, unwrap or set_uvs";
            }
        }
    }
}

/// <summary>A graph slot that cannot be baked, its message <see cref="GraphSlotProblem.Of"/>'s.</summary>
internal sealed class GraphSlotProblemException(string message, Exception cause) : InvalidOperationException(message, cause);
