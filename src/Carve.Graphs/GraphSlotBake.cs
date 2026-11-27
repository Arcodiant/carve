using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Carve.Core;
using Carve.Core.Baking;
using Patina.Graphs;
using Patina.Graphs.Evaluation;
using Patina.Graphs.Imaging;
using Patina.Graphs.Nodes;

namespace Carve.Graphs;

/// <summary>What a graph slot's file reads (T-163): the file, every nested file all levels down (image nodes' PNGs too, T-240), a hash of all their bytes, and whether the expanded graph has a mesh node.</summary>
public sealed record GraphSlotFiles(string Path, IReadOnlyList<string> Files, string Hash, bool ReadsMesh);

/// <summary>A graph slot that cannot be baked for a reason of its own: the file gone or unreadable, or an output the slot cannot take.</summary>
public class GraphSlotException(string message) : Exception(message);

/// <summary>The graph file is not there (<see cref="Path"/>); the server words it with the .texgraph files beside it (T-166).</summary>
public sealed class GraphMissingException(string path) : GraphSlotException($"the graph file '{path}' is not there")
{
    public string Path { get; } = path;
}

/// <summary>An output the graph does not have, or one the slot cannot take (T-166); its message says which, and what would be right.</summary>
public sealed class GraphOutputException(string message) : GraphSlotException(message);

/// <summary>
/// The bake of a material's graph slot (M-29, T-163), engine-free: <see cref="Read"/> reads and hashes the graph's files,
/// <see cref="Key"/> gives the content key the server caches a bake under, and <see cref="Png"/> evaluates the output at
/// the slot's size and gives the PNG the slot's texture is, packed by <see cref="OutputPacking"/> as patina's export packs
/// it. A graph that reads the mesh is evaluated on that mesh's UV layout (<see cref="MeshBakeSource"/>) and padded
/// <see cref="MeshBakeSource.Padding"/> texels past its islands; one that does not is left as evaluated, so it tiles.
/// A slot's bake is never stored in the Carve file.
/// </summary>
public static class GraphSlotBake
{
    public const string BaseColour = "base_colour_texture";
    public const string MetallicRoughness = "metallic_roughness_texture";
    public const string Normal = "normal_texture";
    public const string Emissive = "emissive_texture";
    public const string Occlusion = "occlusion_texture";

    /// <summary>The built-in nodes and the mesh nodes bound to no mesh: what a graph is read and validated with.</summary>
    public static NodeRegistry Unbound { get; } = MeshNodes.Register(NodeRegistry.BuiltIn, null);

    /// <summary>Reads the graph at <paramref name="path"/> and every file it nests, hashing their bytes. Throws <see cref="GraphSlotException"/> when one cannot be read.</summary>
    public static GraphSlotFiles Read(string path)
    {
        var graph = Load(path, Unbound);
        var nested = GraphValidator.Files(graph, Unbound).Order(StringComparer.Ordinal).ToList();
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var readsMesh = HasMeshNode(graph);
        var folder = Path.GetDirectoryName(Path.GetFullPath(path))!;
        foreach (var file in nested.Prepend(path))
        {
            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(file);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                throw new GraphSlotException($"the graph file '{file}' cannot be read: {e.Message}");
            }
            // Relative to the top file's folder (RV-82): the nesting is in the key, the folder's place is not.
            hash.AppendData(Encoding.UTF8.GetBytes(Path.GetRelativePath(folder, Path.GetFullPath(file)) + "\n" + bytes.Length + "\n"));
            hash.AppendData(bytes);
            if (file != path && IsGraphFile(file) && TexGraphFile.ReadFile(file, Unbound).Graph is { } inner && HasMeshNode(inner)) readsMesh = true;
        }
        return new(path, nested, Convert.ToHexString(hash.GetHashAndReset()), readsMesh);
    }

    /// <summary>
    /// Whether the files still hold the bytes <paramref name="files"/> hashed. A bake reads the files again, so one is cached
    /// under its key only when this holds after it (RV-82): else an edit during the bake would be stored under the old bytes' key.
    /// </summary>
    public static bool Unchanged(GraphSlotFiles files)
    {
        try
        {
            return Read(files.Path).Hash == files.Hash;
        }
        catch (Exception)
        {
            return false; // unreadable now: not the bytes the key was taken from
        }
    }

    /// <summary>
    /// The content key of a bake: the files' hash, the output, the size and how <paramref name="slot"/> packs the output; for a
    /// graph that reads the mesh, also <paramref name="mesh"/>'s vertices, faces, UVs, seams, groups, face materials, shading and
    /// hard edges, and the vertices and faces of every other mesh of <paramref name="model"/> (the occluders). One that does not
    /// serves every mesh. Slots that pack the output alike share a key (RV-82).
    /// </summary>
    public static string Key(GraphSlotFiles files, string output, int size, string slot, Model model, string? mesh)
    {
        using var stream = new MemoryStream();
        using var w = new BinaryWriter(stream, Encoding.UTF8);
        w.Write(files.Hash);
        w.Write(output);
        w.Write(size);
        // Of one output, every slot but metallic-roughness packs as base colour does, or refuses it (PackingOf).
        w.Write(slot == MetallicRoughness ? MetallicRoughness : BaseColour);
        if (files.ReadsMesh)
        {
            ArgumentNullException.ThrowIfNull(mesh);
            var at = model.Meshes.ToList().FindIndex(m => m.Name == mesh);
            if (at < 0) throw new ArgumentException($"'{mesh}' is not a mesh of the model", nameof(mesh));
            w.Write(mesh);
            WriteSurface(w, model.Meshes[at].Mesh);
            for (var i = 0; i < model.Meshes.Count; i++)
            {
                if (i == at) continue;
                w.Write(model.Meshes[i].Name);
                WriteShape(w, model.Meshes[i].Mesh);
            }
        }
        w.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    /// <summary>
    /// The PNG of <paramref name="graph"/>'s output for <paramref name="slot"/> at its size. With <paramref name="mesh"/> (pass it
    /// exactly when <see cref="GraphSlotFiles.ReadsMesh"/>), the mesh nodes read that mesh of <paramref name="model"/> and the image
    /// is padded past its islands. Throws <see cref="GraphSlotException"/> (the file, or an output the slot cannot take),
    /// <see cref="GraphFaultException"/>, <see cref="MeshMapsException"/>, or <see cref="OperationCanceledException"/> between tiles.
    /// </summary>
    public static byte[] Png(MaterialGraph graph, string slot, Model model, string? mesh, CancellationToken cancel = default, Func<IMeshMaps, IMeshMaps>? wrap = null) =>
        Patina.Graphs.Imaging.Png.Encode(Rgba(graph, slot, model, mesh, cancel, wrap), graph.Size, graph.Size);

    /// <summary>As <see cref="Png"/>, the RGBA8 bytes before encoding. <paramref name="wrap"/> may stand a source of its own over the mesh's (the server times the maps through it).</summary>
    public static byte[] Rgba(MaterialGraph graph, string slot, Model model, string? mesh, CancellationToken cancel = default, Func<IMeshMaps, IMeshMaps>? wrap = null)
    {
        var source = mesh is null ? null : new MeshBakeSource(model, mesh);
        var registry = MeshNodes.Register(NodeRegistry.BuiltIn, source is null ? null : wrap?.Invoke(source) ?? source);
        var evaluator = new GraphEvaluator(Load(graph.Path, registry), registry);
        var role = PackingOf(slot, evaluator, graph.Output);
        var size = graph.Size;
        var rgba = new byte[(long)size * size * 4];
        evaluator.Evaluate(graph.Output, size, tile =>
        {
            cancel.ThrowIfCancellationRequested();
            OutputPacking.PackTile(role, tile, rgba, size);
        });
        var metallic = OutputRoles.Name(OutputRole.Metallic);
        if (role == OutputRole.Roughness && evaluator.Has(metallic))
            evaluator.Evaluate(metallic, size, tile =>
            {
                cancel.ThrowIfCancellationRequested();
                OutputPacking.PackMetallicTile(tile, rgba, size);
            });
        return source is null ? rgba : Padded(rgba, source.Layout(size));
    }

    /// <summary>
    /// How <paramref name="slot"/> packs <paramref name="output"/> (the slot table): base colour and emissive any output, a grey
    /// one as grey RGB; normal the <c>normal</c> role or a colour output; metallic-roughness <c>roughness</c> alone (G, with
    /// <c>metallic</c> in B); occlusion a grey output (in R). Throws <see cref="GraphSlotException"/> for one it cannot take.
    /// </summary>
    public static OutputRole PackingOf(string slot, GraphEvaluator evaluator, string output)
    {
        if (!evaluator.Has(output))
            throw new GraphOutputException($"the graph has no output '{output}'; its outputs are {string.Join(", ", evaluator.Outputs)}");
        var grey = evaluator.KindOf(output) == PortKind.Grey;
        var roughness = OutputRoles.Name(OutputRole.Roughness);
        return slot switch
        {
            BaseColour or Emissive => grey ? OutputRole.Occlusion : OutputRoles.PackedAs(output),
            Normal when !grey => OutputRoles.PackedAs(output),
            Normal => throw new GraphOutputException($"'{output}' is a grey output; {Normal} takes the 'normal' output or a colour one"),
            MetallicRoughness when output == roughness => OutputRole.Roughness,
            MetallicRoughness => throw new GraphOutputException($"{MetallicRoughness} takes the 'roughness' output (with 'metallic' packed in B), not '{output}'"),
            Occlusion when grey => OutputRole.Occlusion,
            Occlusion => throw new GraphOutputException($"'{output}' is a colour output; {Occlusion} takes a grey one"),
            _ => throw new ArgumentException($"'{slot}' is not a texture slot", nameof(slot)),
        };
    }

    /// <summary>
    /// Everything that makes <paramref name="graph"/> unbakeable into <paramref name="slot"/> before a mesh is in it (T-166): a file gone
    /// or unreadable, every fault of the graph, an output it lacks or the slot cannot take. Evaluates nothing. Throws as <see cref="Png"/> does.
    /// </summary>
    public static void Validate(MaterialGraph graph, string slot) =>
        PackingOf(slot, new GraphEvaluator(Load(graph.Path, Unbound), Unbound), graph.Output);

    /// <summary>The <c>mesh_mask</c> nodes of the graph and its nested files that name a group or material: (node id, source, name).</summary>
    public static IReadOnlyList<(string Node, MaskSource Source, string Name)> MaskNames(GraphSlotFiles files)
    {
        var names = new List<(string, MaskSource, string)>();
        foreach (var file in files.Files.Where(IsGraphFile).Prepend(files.Path))
            foreach (var (id, node) in TexGraphFile.ReadFile(file, Unbound).Graph?.Nodes ?? ImmutableSortedDictionary<string, GraphNode>.Empty)
            {
                if (node.Type != MeshMaskNode.TypeName) continue;
                if (node.Params.TryGetValue("group", out var g) && !string.IsNullOrEmpty(g.Text)) names.Add((id, MaskSource.Group, g.Text));
                else if (node.Params.TryGetValue("material", out var m) && !string.IsNullOrEmpty(m.Text)) names.Add((id, MaskSource.Material, m.Text));
            }
        return names;
    }

    private static TextureGraph Load(string path, NodeRegistry registry)
    {
        if (!File.Exists(path)) throw new GraphMissingException(path);
        var read = TexGraphFile.ReadFile(path, registry);
        return read.Graph ?? throw new GraphSlotException($"the graph file '{path}' cannot be read: {read.Error}");
    }

    /// <summary>A .texgraph among the files, not an image node's PNG (T-240).</summary>
    private static bool IsGraphFile(string file) => file.EndsWith(TexGraphFile.Extension, StringComparison.OrdinalIgnoreCase);

    private static bool HasMeshNode(TextureGraph graph) => graph.Nodes.Values.Any(n => MeshNodes.TypeNames.Contains(n.Type));

    private static byte[] Padded(byte[] rgba, BakeLayout layout)
    {
        var image = layout.NewImage(4);
        var values = image.Values;
        for (var i = 0; i < rgba.Length; i++) values[i] = rgba[i];
        var padded = Bake.Pad(image, layout, MeshBakeSource.Padding).Values;
        var result = new byte[rgba.Length];
        for (var i = 0; i < result.Length; i++) result[i] = (byte)padded[i];
        return result;
    }

    private static void WriteShape(BinaryWriter w, PolygonMesh mesh)
    {
        w.Write(mesh.Vertices.Count);
        for (var v = 0; v < mesh.Vertices.Count; v++)
        {
            w.Write(mesh.IsVertexDeleted(v));
            w.Write(mesh.Vertices[v].X); w.Write(mesh.Vertices[v].Y); w.Write(mesh.Vertices[v].Z);
        }
        w.Write(mesh.Faces.Count);
        foreach (var face in mesh.Faces)
        {
            w.Write(face.IsDeleted);
            w.Write(face.Corners.Count);
            foreach (var c in face.Corners) w.Write(c);
        }
    }

    private static void WriteSurface(BinaryWriter w, PolygonMesh mesh)
    {
        WriteShape(w, mesh);
        var uvs = mesh.Uvs ?? MeshUvs.None;
        w.Write(uvs.MappedFaces.Count);
        foreach (var f in uvs.MappedFaces)
        {
            w.Write(f);
            foreach (var uv in uvs.Of(f)!) { w.Write(uv.X); w.Write(uv.Y); }
        }
        WriteEdges(w, mesh.Seams);
        var style = mesh.Style ?? FaceStyle.None;
        w.Write(style.Groups.Count);
        foreach (var (name, faces) in style.Groups)
        {
            w.Write(name);
            w.Write(faces.Count);
            foreach (var f in faces) w.Write(f);
        }
        w.Write(style.Materials.Count);
        foreach (var (face, name) in style.Materials.OrderBy(p => p.Key)) { w.Write(face); w.Write(name); }
        w.Write((int)mesh.ShadingMode);
        WriteEdges(w, mesh.HardEdges);
    }

    private static void WriteEdges(BinaryWriter w, IReadOnlyList<(int Lo, int Hi)> edges)
    {
        w.Write(edges.Count);
        foreach (var (lo, hi) in edges) { w.Write(lo); w.Write(hi); }
    }
}
