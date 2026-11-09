using System.Globalization;
using System.Numerics;
using System.Text.Json;
using Carve.Core;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using static Carve.Server.MeshToolSupport;

namespace Carve.Server;

public sealed class ExportTool(ModelStore store)
{
    public const string Extension = ".glb";

    public const string Text =
        "Writes the current mesh to a .glb file (binary glTF 2.0) that a game on the Empyrean engine loads as it is. Takes path, the file to write. "
        + "What is written: one mesh on one node, both named after the file name without its extension (crate.glb gives crate), the node's transform the identity; "
        + "the triangles render draws, with flat normals: each face keeps its own normal, so every edge is hard; "
        + "one matte grey material named 'carve' (base colour 0.72, 0.72, 0.70, metallic 0, roughness 1, single-sided), and for faces coloured with set_colour one more opaque matte material per colour, named 'carve #RRGGBB', its baseColorFactor the linear form of the colour (metallic 0, roughness 1), a primitive for each; "
        + "positions and normals only, with no UVs, colours or textures. "
        + "Axes and units are the mesh's own: +Y up, +Z front, 1 unit = 1 m, the origin where you authored it. Nothing is scaled, turned, recentred or converted, "
        + "so build the model at the size and place the game wants it. "
        + "Path rules: a relative path resolves against carve's working directory, and the result gives the absolute path; missing folders are created; "
        + "an existing file is replaced and the result says (replaced); the path must end in .glb, in any case. "
        + "Errors, each a tool error that writes nothing: no mesh yet (call define_mesh or open first); an empty path; a path holding a character a file path may not; "
        + "a path naming a folder; any extension but .glb; a write that fails (access denied, a locked file), naming the path and the system's message, "
        + "which leaves any file already there as it was and no partial file. "
        + "A mesh with warnings still exports, and the result repeats them. export changes nothing: not the mesh, its ids, or what undo steps back to. "
        + "Returns one line: exported <file> to <absolute path> (replaced, when it was): <n> triangles, 1 material 'carve' (or each material, when there are several), size <x> x <y> x <z> m, "
        + "bounds (<min x, y, z>) to (<max x, y, z>), then any warnings. "
        + "The .glb is for the game: it keeps no faces, only triangles, so the .carve file is the one to keep working in. "
        + "The whole sequence from an empty file to a game, with save and open, is the worked example in save's description. "
        + "Worked example, writing into a game's content folder (put the game's folder for <game>); the game then loads it as "
        + "models.LoadModel(new ContentPath(\"models/crate.glb\")):\n"
        + ExampleMarker + SaveTool.WorkedExport;

    public static McpServerTool Create() => MeshToolSupport.Create<ExportTool>("export", Text, PathSchema("The .glb file to write, absolute or relative to carve's working directory; replaced if it exists."));

    public CallToolResult Run(RequestContext<CallToolRequestParams> context, JsonElement? path = null)
    {
        if (PathArguments("export", context, path, out var text) is { } refused) return refused;

        var model = store.Current.Model;
        if (model.Meshes.Count > 1)
        {
            return Error($"export: the model has {model.Meshes.Count} meshes ({model.ListNames()}), and export writes a model of one mesh for now; save each mesh to its own .carve file, or define the model as one mesh, to export it");
        }

        var mesh = model.Combined;
        if (mesh is null) return Error("export: " + NoMesh);

        var target = ToolPaths.Resolve("export", text, Extension, out var pathError);
        if (target is null) return Error(pathError!);

        var triangles = FlatShading.Triangles(mesh);
        var bytes = GlbExport.Write(mesh, triangles, target.Stem);
        if (!ToolPaths.Write("export", target, bytes, out var replaced, out var writeError)) return Error(writeError!);

        return Line(Summary(mesh, target, replaced, triangles.Count));
    }

    /// <summary>The result line; numbers are culture-invariant.</summary>
    private static string Materials(PolygonMesh mesh)
    {
        var names = GlbExport.MaterialNames(mesh);
        return names.Length == 1
            ? $"1 material '{names[0]}'"
            : $"{names.Length.ToString(CultureInfo.InvariantCulture)} materials {string.Join(", ", names.Select(n => $"'{n}'"))}";
    }

    private static string Summary(PolygonMesh mesh, ToolPath target, bool replaced, int triangles)
    {
        var report = MeshReport.Create(mesh);
        var (min, max) = (report.BoundsMin, report.BoundsMax);
        static string N(float x) => x.ToString(CultureInfo.InvariantCulture);
        static string Size(float lo, float hi) => N((float)((double)hi - lo));
        static string Point(Vector3 v) => $"({N(v.X)}, {N(v.Y)}, {N(v.Z)})";

        var line = $"exported {target.Written(replaced)}: "
            + $"{triangles.ToString(CultureInfo.InvariantCulture)} triangles, {Materials(mesh)}, "
            + $"size {Size(min.X, max.X)} x {Size(min.Y, max.Y)} x {Size(min.Z, max.Z)} m, bounds {Point(min)} to {Point(max)}";
        if (report.Warnings.Count > 0)
            line += $"; {report.Warnings.Count.ToString(CultureInfo.InvariantCulture)} warning(s): "
                + string.Join("; ", report.Warnings.Select(w => w.ToString().ReplaceLineEndings(" ")));
        return line;
    }
}
