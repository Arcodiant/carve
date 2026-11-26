using System.Globalization;
using System.Numerics;
using Carve.Core;
using Carve.Core.Baking;
using Carve.Core.Tests;
using Patina.Graphs;
using Patina.Graphs.Evaluation;
using Patina.Graphs.Imaging;
using Patina.Graphs.Nodes;
using Xunit;

namespace Carve.Graphs.Tests;

/// <summary>The mesh-input nodes (T-161) against <see cref="Bake"/>'s own images, M-24's numbers, the raised-hoop barrel and every refusal.</summary>
[Trait("Tier", "unit")]
public class MeshNodesTests
{
    // ---- helpers ----

    internal static string Root()
    {
        var dir = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(dir, "Carve.slnx"))) dir = Path.GetDirectoryName(dir) ?? throw new DirectoryNotFoundException("Carve.slnx not found");
        return dir;
    }

    internal static Model Wrench() => CarveFile.Read(File.ReadAllText(Path.Combine(Root(), "samples", "wrench.carve"))).Model!;

    /// <summary>The barrel with both hoop bands (faces 0 to 7 and 24 to 31) extruded 0.015 and unwrapped, and the faces the extrude made.</summary>
    internal static (PolygonMesh Mesh, IReadOnlyList<int> Walls) RaisedHoops()
    {
        var extruded = MeshEdits.Extrude(TestMeshes.SeamedBarrel(), [.. Enumerable.Range(0, 8), .. Enumerable.Range(24, 8)], 0.015f);
        var mesh = TestMeshes.Ok(extruded);
        return (Unwrap.Run(mesh).Result!.Mesh, [.. Enumerable.Range(34, mesh.Faces.Count - 34)]);
    }

    internal static PolygonMesh PlainBarrel() => Unwrap.Run(TestMeshes.SeamedBarrel()).Result!.Mesh;

    private static string N(double v) => v.ToString(CultureInfo.InvariantCulture);

    internal static string Node(string type, string parameters = "") =>
        $$"""{ "type": "{{type}}"{{(parameters.Length == 0 ? "" : $", \"params\": {{ {parameters} }}")}} }""";

    /// <summary>The node as a graph's one output (grey as occlusion, colour as base colour), evaluated; one byte a texel per channel.</summary>
    internal static byte[] Run(IMeshMaps? source, string node, int size, int? tile = null, long? cap = null)
    {
        var registry = MeshNodes.Register(NodeRegistry.BuiltIn, source);
        var colour = node.Contains(MeshPositionNode.TypeName);
        var role = colour ? "base_colour" : "occlusion";
        var read = TexGraphFile.Read($$"""{ "version": 1, "size": {{size}}, "seed": 1, "nodes": { "m": {{node}}, "o": { "type": "output", "inputs": { "in": "m" }, "params": { "role": "{{role}}" } } } }""", registry);
        Assert.True(read.Graph is not null, string.Join("; ", read.Faults.Select(f => f.ToString())));
        var rgba = OutputImage.Rgba8(new GraphEvaluator(read.Graph!, registry), colour ? OutputRole.BaseColour : OutputRole.Occlusion, size, tile, cap);
        var channels = colour ? 3 : 1;
        var bytes = new byte[size * size * channels];
        for (var i = 0; i < size * size; i++)
            for (var c = 0; c < channels; c++) bytes[i * channels + c] = rgba[i * 4 + c];
        return bytes;
    }

    /// <summary>The bake as the source must give it, worked here on its own: uncovered texels neutral, then padded 8, packed 255 · v.</summary>
    internal static byte[] Expected(BakeImage image, BakeLayout layout, float neutral)
    {
        for (var y = 0; y < layout.Size; y++)
            for (var x = 0; x < layout.Size; x++)
                if (!layout.IsCovered(x, y)) image.Texel(x, y).Fill(neutral);
        var padded = Bake.Pad(image, layout, 8).Values;
        var bytes = new byte[padded.Length];
        for (var i = 0; i < bytes.Length; i++) bytes[i] = OutputPacking.Byte(255f * padded[i]);
        return bytes;
    }

    /// <summary>Null when equal, else the first texel that differs.</summary>
    internal static string? FirstDifference(byte[] expected, byte[] actual, int channels, int size)
    {
        if (expected.Length != actual.Length) return $"lengths {expected.Length} and {actual.Length}";
        for (var i = 0; i < expected.Length; i++)
            if (expected[i] != actual[i]) return $"texel ({i / channels % size}, {i / channels / size}) channel {i % channels}: expected {expected[i]}, got {actual[i]}";
        return null;
    }

    internal static Vector3 Centre(PolygonMesh mesh, int face) =>
        mesh.Faces[face].Loop.Aggregate(Vector3.Zero, (s, v) => s + mesh.Vertices[v]) / mesh.Faces[face].Loop.Count;

    // ---- equal to the bake ----

    /// <summary>Position worked here from the layout's texels: each axis over the bounding box of the live faces' corners.</summary>
    internal static BakeImage PositionImage(PolygonMesh mesh, BakeLayout layout)
    {
        var points = mesh.LiveFaces.SelectMany(f => mesh.Faces[f].Corners).Select(v => mesh.Vertices[v]).ToList();
        var lo = points.Aggregate(Vector3.Min);
        var span = points.Aggregate(Vector3.Max) - lo;
        var image = layout.NewImage(3);
        foreach (var t in layout.Texels())
        {
            var p = (t.Position - lo) / span;
            image[t.X, t.Y, 0] = p.X; image[t.X, t.Y, 1] = p.Y; image[t.X, t.Y, 2] = p.Z;
        }
        return image;
    }

    [Fact]
    public void A_mask_is_255_on_its_groups_faces_and_0_on_the_rest()
    {
        var model = Wrench();
        var mesh = model.Meshes[0].Mesh;
        var layout = BakeLayout.Create(mesh, 256).Layout!;
        Assert.Null(MaskCheck(Run(new MeshBakeSource(model, model.Meshes[0].Name), Node(MeshMaskNode.TypeName, "\"group\": \"steel\""), 256), mesh, layout, "steel"));
    }

    internal static string? MaskCheck(byte[] bytes, PolygonMesh mesh, BakeLayout layout, string group)
    {
        var faces = mesh.Style!.Groups.Single(g => g.Name == group).Faces.ToHashSet();
        foreach (var t in layout.Texels())
            if (bytes[t.Y * layout.Size + t.X] != (faces.Contains(t.Face) ? 255 : 0)) return $"texel ({t.X}, {t.Y}) of face {t.Face} reads {bytes[t.Y * layout.Size + t.X]}";
        return null;
    }

    // ---- M-24's numbers through the node ----

    internal static (int[] Crevice, int[] Open) CreviceAndOpen(PolygonMesh mesh, BakeLayout layout, byte[] bytes)
    {
        int[] drive = [100, 101, 102, 103, 104, 105];
        var footprint = drive.SelectMany(f => mesh.Faces[f].Loop).Select(v => mesh.Vertices[v]).ToArray();
        var (lo, hi) = (footprint.Aggregate(Vector3.Min), footprint.Aggregate(Vector3.Max));
        float Outside(Vector3 p) => MathF.Max(MathF.Max(lo.X - p.X, p.X - hi.X), MathF.Max(lo.Y - p.Y, p.Y - hi.Y));
        var centre = Centre(mesh, 5);
        var crevice = layout.Texels().Where(t => t.Face == 118 && Outside(t.Position) is > 0 and <= 0.0015f).Select(t => (int)bytes[t.Y * layout.Size + t.X]).ToArray();
        var open = layout.Texels().Where(t => t.Face == 5 && Vector3.Distance(t.Position, centre) <= 0.002f).Select(t => (int)bytes[t.Y * layout.Size + t.X]).ToArray();
        Assert.NotEmpty(crevice);
        Assert.NotEmpty(open);
        return (crevice, open);
    }

    // ---- the raised-hoop barrel (planning's probe) ----

    internal static string? AllOpen(byte[] bytes) => bytes.All(b => b == 255) ? null : $"{bytes.Count(b => b != 255)} texels under 255, least {bytes.Min()}";

    // ---- neutral ----

    [Fact]
    public void With_no_source_every_node_is_neutral()
    {
        Assert.All(Run(null, Node(MeshCurvatureNode.TypeName), 64), b => Assert.Equal(128, b));
        Assert.All(Run(null, Node(MeshOcclusionNode.TypeName), 64), b => Assert.Equal(255, b));
        Assert.All(Run(null, Node(MeshMaskNode.TypeName, "\"material\": \"any name at all\""), 64), b => Assert.Equal(255, b));
        Assert.All(Run(null, Node(MeshPositionNode.TypeName), 64), b => Assert.Equal(128, b));
    }

    // ---- refusals ----

    [Fact]
    public void A_mesh_with_no_uvs_is_refused_with_the_cores_message()
    {
        var e = Assert.Throws<MeshLayoutException>(() => Run(new MeshBakeSource(Model.Of("barrel", TestMeshes.Barrel()), "barrel"), Node(MeshCurvatureNode.TypeName), 64));
        Assert.Equal(BakeLayout.NoUvsMessage, e.Message);
    }

    [Fact]
    public void Overlapping_uvs_are_refused_naming_the_faces()
    {
        var plain = PlainBarrel();
        var mesh = plain.WithUvs(plain.Uvs!.WithFace(1, plain.Uvs.Of(0)));
        var e = Assert.Throws<MeshLayoutException>(() => Run(new MeshBakeSource(Model.Of("barrel", mesh), "barrel"), Node(MeshPositionNode.TypeName), 64));
        Assert.Equal(BakeLayout.Create(mesh, 64).Error, e.Message);
        Assert.Contains("faces[0] and faces[1]", e.Message);
    }

    [Fact]
    public void A_size_no_layout_takes_is_refused()
    {
        var e = Assert.Throws<MeshLayoutException>(() => Run(new MeshBakeSource(Model.Of("barrel", PlainBarrel()), "barrel"), Node(MeshPositionNode.TypeName), 32));
        Assert.Equal(BakeLayout.SizeProblem(32), e.Message);
        Assert.Equal(32, e.Size);
    }

    [Fact]
    public void Faces_with_no_uvs_are_a_warning()
    {
        var plain = PlainBarrel();
        var source = new MeshBakeSource(Model.Of("barrel", plain.WithUvs(plain.Uvs!.WithFace(3, null))), "barrel");
        Assert.Equal([3], source.Unmapped);
        Assert.Equal(["barrel: 1 face(s) have no UVs and were skipped: 3"], source.Warnings);
        Assert.Empty(new MeshBakeSource(Model.Of("barrel", plain), "barrel").Warnings);
    }

    [Fact]
    public void A_mask_naming_what_the_mesh_lacks_lists_its_groups_and_materials()
    {
        var model = Wrench();
        var source = new MeshBakeSource(model, "wrench");
        var e = Assert.Throws<MeshMaskException>(() => Run(source, Node(MeshMaskNode.TypeName, "\"group\": \"chrome\""), 64));
        Assert.Equal(source.Groups, e.Groups);
        Assert.Equal(source.Materials, e.Materials);
        Assert.StartsWith("mesh 'wrench' has no group 'chrome'; its groups are ", e.Message, StringComparison.Ordinal);
        Assert.Contains("steel", source.Groups);
        Assert.All(source.Groups.Concat(source.Materials), n => Assert.Contains($"'{n}'", e.Message));
        Assert.Throws<MeshMaskException>(() => Run(source, Node(MeshMaskNode.TypeName, "\"material\": \"steel-x\""), 64));
    }

    [Theory]
    [InlineData("mesh_mask", "", "node 'm': give exactly one of params 'group' and 'material'")]
    [InlineData("mesh_mask", "\"group\": \"steel\", \"material\": \"steel\"", "node 'm': give exactly one of params 'group' and 'material', not both")]
    [InlineData("mesh_curvature", "\"width\": 0", "node 'm' parameter 'width': is 0, but must be above 0 metres")]
    [InlineData("mesh_occlusion", "\"reach\": 0", "node 'm' parameter 'reach': is 0, but must be above 0 metres")]
    public void Parameters_no_range_can_refuse_are_graph_faults_with_or_without_a_source(string type, string parameters, string message)
    {
        // RV-82: a fault when the graph is read and validated, not first at the bake.
        foreach (var source in new IMeshMaps?[] { null, new MeshBakeSource(Wrench(), "wrench") })
        {
            var registry = MeshNodes.Register(NodeRegistry.BuiltIn, source);
            var read = TexGraphFile.Read($$"""{ "version": 1, "size": 64, "seed": 1, "nodes": { "m": {{Node(type, parameters)}}, "o": { "type": "output", "inputs": { "in": "m" }, "params": { "role": "occlusion" } } } }""", registry);
            Assert.Equal([message], read.Faults.Select(f => f.Message));
            Assert.Equal([message], Assert.Throws<GraphFaultException>(() => new GraphEvaluator(read.Graph!, registry)).Faults.Select(f => f.Message));
        }
    }

    [Fact]
    public void Ranges_the_file_holds_are_graph_faults()
    {
        var registry = MeshNodes.Register(NodeRegistry.BuiltIn, null);
        foreach (var (type, parameters) in new[] { ("mesh_curvature", "\"width\": 1.5"), ("mesh_occlusion", "\"rays\": 0"), ("mesh_occlusion", "\"rays\": 1025"), ("mesh_occlusion", "\"reach\": 11"), ("mesh_mask", "\"group\": \"\"") })
        {
            var read = TexGraphFile.Read($$"""{ "version": 1, "size": 64, "seed": 1, "nodes": { "m": {{Node(type, parameters)}}, "o": { "type": "output", "inputs": { "in": "m" }, "params": { "role": "occlusion" } } } }""", registry);
            if (read.Graph is not null) Assert.Throws<GraphFaultException>(() => new GraphEvaluator(read.Graph, registry));
        }
    }

    [Fact]
    public void An_unknown_mesh_lists_the_meshes()
    {
        var e = Assert.Throws<ArgumentException>(() => new MeshBakeSource(Wrench(), "spanner"));
        Assert.StartsWith("'spanner' is not a mesh; the meshes are 'wrench'", e.Message);
    }

    [Fact]
    public void Registering_adds_the_four_types_to_the_registry_given()
    {
        var registry = MeshNodes.Register(NodeRegistry.BuiltIn, null);
        Assert.All(MeshNodes.TypeNames, n => Assert.NotNull(registry.Find(n)));
        Assert.All(NodeRegistry.BuiltIn.Names, n => Assert.NotNull(registry.Find(n)));
        Assert.Equal(NodeRegistry.BuiltIn.Names.Count + 4, registry.Names.Count);
    }
}
