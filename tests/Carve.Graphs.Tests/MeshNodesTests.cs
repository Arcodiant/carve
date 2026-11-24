using System.Diagnostics;
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

    public static TheoryData<int> Sizes => [256, 1024];

    [Theory]
    [MemberData(nameof(Sizes))]
    public void Each_node_equals_its_bake_at_every_tile_size_and_a_zero_cap(int size)
    {
        var model = Wrench();
        var mesh = model.Meshes[0].Mesh;
        var source = new MeshBakeSource(model, model.Meshes[0].Name);
        var layout = BakeLayout.Create(mesh, size).Layout!;
        const int rays = 32;
        var cases = new (string Node, int Channels, Func<byte[]> Expected)[]
        {
            (Node(MeshCurvatureNode.TypeName, "\"width\": 0.003"), 1, () => Expected(Bake.Curvature(layout, 0.003), layout, 0.5f)),
            (Node(MeshOcclusionNode.TypeName, $"\"rays\": {rays}, \"reach\": 0.035"), 1,
                () => Expected(Bake.Occlusion(layout, new RayCaster([.. model.Meshes.Select(m => m.Mesh)]), 0, rays, 0.035f).Image, layout, 1f)),
            (Node(MeshMaskNode.TypeName, "\"group\": \"steel\""), 1, () => Expected(Bake.Masks(layout, mesh).Single(m => m.Source == MaskSource.Group && m.Name == "steel").Image, layout, 1f)),
            (Node(MeshPositionNode.TypeName), 3, () => Expected(PositionImage(mesh, layout), layout, 128f / 255f)),
        };
        foreach (var (node, channels, expectedOf) in cases)
        {
            var expected = expectedOf();
            foreach (var (tile, cap) in new (int?, long?)[] { (16, null), (64, null), (256, null), (64, 0) })
            {
                var d = FirstDifference(expected, Run(source, node, size, tile, cap), channels, size);
                Assert.True(d is null, $"{node} at {size}, tile {tile}, cap {cap}: {d}");
            }
        }
    }

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

    [Fact]
    public void Curvature_keeps_M24s_convex_jaw_edges_and_flat_handle()
    {
        const int size = 1024;
        var model = Wrench();
        var mesh = model.Meshes[0].Mesh;
        var bytes = Run(new MeshBakeSource(model, model.Meshes[0].Name), Node(MeshCurvatureNode.TypeName, "\"width\": 0.003"), size);
        int At(Vector2 uv) => bytes[(int)Math.Floor(uv.Y * size) * size + (int)Math.Floor(uv.X * size)];

        // As CurvatureStage: each edge of two jaw ("dark") faces bending 60 degrees or more and turning away, at the middle of its UV edge.
        var jaw = mesh.Style!.Groups.Single(g => g.Name == "dark").Faces.ToHashSet();
        var owners = jaw.SelectMany(f => Edges(mesh, f).Select(e => (e.Key, f))).GroupBy(p => p.Key).ToDictionary(g => g.Key, g => g.Select(p => p.f).ToList());
        var checkedEdges = 0;
        foreach (var f in jaw.Where(mesh.Uvs!.IsMapped))
            foreach (var (key, corner) in Edges(mesh, f))
            {
                if (owners[key].Count != 2) continue;
                var g = owners[key].Single(o => o != f);
                var (n, m) = (Vector3.Normalize(mesh.FaceNormal(f)), Vector3.Normalize(mesh.FaceNormal(g)));
                var mid = (mesh.Vertices[key.Item1] + mesh.Vertices[key.Item2]) / 2;
                if (Math.Acos(Math.Clamp(Vector3.Dot(n, m), -1f, 1f)) * 180 / Math.PI < 60 || Vector3.Dot(Centre(mesh, g) - mid, n) >= 0) continue;
                var uv = mesh.Uvs.Of(f)!;
                var value = At((uv[corner] + uv[(corner + 1) % uv.Count]) / 2);
                Assert.True(value >= 218, $"jaw face {f}'s convex edge to face {g} reads {value}, under 218");
                checkedEdges++;
            }
        Assert.True(checkedEdges >= 50, $"only {checkedEdges} convex jaw edges found");

        var handle = mesh.Style.Groups.Single(g => g.Name == "rubber").Faces.Where(mesh.Uvs.IsMapped).MaxBy(f => Area(mesh.Uvs.Of(f)!));
        var c = mesh.Uvs.Of(handle)!.Aggregate(Vector2.Zero, (a, b) => a + b) / mesh.Uvs.Of(handle)!.Count;
        Assert.Equal(128, At(c));
    }

    private static IEnumerable<((int, int) Key, int Corner)> Edges(PolygonMesh mesh, int face)
    {
        var loop = mesh.Faces[face].Loop;
        for (var i = 0; i < loop.Count; i++)
            yield return ((Math.Min(loop[i], loop[(i + 1) % loop.Count]), Math.Max(loop[i], loop[(i + 1) % loop.Count])), i);
    }

    private static float Area(IReadOnlyList<Vector2> p)
    {
        float s = 0;
        for (var i = 0; i < p.Count; i++) s += p[i].X * p[(i + 1) % p.Count].Y - p[(i + 1) % p.Count].X * p[i].Y;
        return MathF.Abs(s) / 2;
    }

    /// <summary>As OcclusionStage: the socket top within 1.5 mm of the drive's footprint against grip facet 5's middle (2 mm of its centre).</summary>
    [Fact]
    public void Occlusion_keeps_M24s_drive_crevice_50_below_the_open_grip()
    {
        const int size = 512;
        var model = Wrench();
        var mesh = model.Meshes[0].Mesh;
        var bytes = Run(new MeshBakeSource(model, model.Meshes[0].Name), Node(MeshOcclusionNode.TypeName, "\"rays\": 32, \"reach\": 0.035"), size);
        var (crevice, open) = CreviceAndOpen(mesh, BakeLayout.Create(mesh, size).Layout!, bytes);
        Assert.True(open.Min() - crevice.Max() >= 50, $"the crevice's most ({crevice.Max()}) is only {open.Min() - crevice.Max()} below the grip's middle's least ({open.Min()}), under 50");
    }

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

    [Fact]
    public void The_raised_hoops_rims_and_the_staves_beside_them_read_as_planning_measured()
    {
        const int size = 512;
        var (mesh, _) = RaisedHoops();
        var source = new MeshBakeSource(Model.Of("barrel", mesh), "barrel");
        var layout = BakeLayout.Create(mesh, size).Layout!;
        float[] Of(byte[] b, Func<int, bool> faces) => [.. layout.Texels().Where(t => faces(t.Face)).Select(t => b[t.Y * size + t.X] / 255f).Order()];

        var curvature = Of(Run(source, Node(MeshCurvatureNode.TypeName, "\"width\": 0.02"), size), f => f is < 8 or (>= 24 and < 32));
        var p95 = curvature[(int)(0.95 * (curvature.Length - 1))];
        Assert.True(p95 >= 0.9f, $"the hoop tops' curvature 95th percentile is {p95}, under 0.9");

        var staves = Of(Run(source, Node(MeshOcclusionNode.TypeName, "\"rays\": 32, \"reach\": 0.15"), size), f => f is >= 8 and < 24);
        Assert.True(staves.Min() <= 0.55f, $"the staves' occlusion minimum is {staves.Min()}, over 0.55");
        Assert.True(staves.Average() >= 0.95f, $"the staves' occlusion mean is {staves.Average()}, under 0.95");
    }

    [Fact]
    public void The_plain_barrels_occlusion_is_exactly_1() =>
        Assert.Null(AllOpen(Run(new MeshBakeSource(Model.Of("barrel", PlainBarrel()), "barrel"), Node(MeshOcclusionNode.TypeName, "\"rays\": 32, \"reach\": 0.15"), 512)));

    internal static string? AllOpen(byte[] bytes) => bytes.All(b => b == 255) ? null : $"{bytes.Count(b => b != 255)} texels under 255, least {bytes.Min()}";

    // ---- neutral, determinism, timing ----

    [Fact]
    public void With_no_source_every_node_is_neutral()
    {
        Assert.All(Run(null, Node(MeshCurvatureNode.TypeName), 64), b => Assert.Equal(128, b));
        Assert.All(Run(null, Node(MeshOcclusionNode.TypeName), 64), b => Assert.Equal(255, b));
        Assert.All(Run(null, Node(MeshMaskNode.TypeName, "\"material\": \"any name at all\""), 64), b => Assert.Equal(255, b));
        Assert.All(Run(null, Node(MeshPositionNode.TypeName), 64), b => Assert.Equal(128, b));
    }

    [Fact]
    public void Two_sources_give_the_same_bytes()
    {
        var model = Wrench();
        foreach (var node in new[] { Node(MeshCurvatureNode.TypeName, "\"width\": 0.003"), Node(MeshOcclusionNode.TypeName, "\"rays\": 8, \"reach\": 0.035"), Node(MeshPositionNode.TypeName) })
            Assert.Equal(Run(new MeshBakeSource(model, "wrench"), node, 256), Run(new MeshBakeSource(model, "wrench"), node, 256));
    }

    /// <summary>Not a check: each map's time on the wrench through the source at the nodes' defaults, written to the temp folder for the As built.</summary>
    [Fact]
    public void Times_each_map_on_the_wrench()
    {
        var model = Wrench();
        var lines = new List<string>();
        foreach (var size in new[] { 512, 1024 })
        {
            var source = new MeshBakeSource(model, "wrench");
            var clock = Stopwatch.StartNew();
            _ = source.Position(size); // the layout's cost lands here
            lines.Add($"{size} layout+position {clock.ElapsedMilliseconds} ms");
            foreach (var (name, make) in new (string, Func<BakeImage>)[]
            {
                ("curvature 0.01", () => source.Curvature(size, 0.01)), ("occlusion 32 0.1", () => source.Occlusion(size, 32, 0.1f)),
                ("mask steel", () => source.Mask(size, MaskSource.Group, "steel")),
            })
            {
                clock.Restart();
                _ = make();
                lines.Add($"{size} {name} {clock.ElapsedMilliseconds} ms");
            }
        }
        File.WriteAllLines(Path.Combine(Path.GetTempPath(), "carve-graphs-times.txt"), lines);
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
    [InlineData("mesh_mask", "", "nodes['m']: give exactly one of params 'group' and 'material'")]
    [InlineData("mesh_mask", "\"group\": \"steel\", \"material\": \"steel\"", "nodes['m']: give exactly one of params 'group' and 'material', not both")]
    [InlineData("mesh_curvature", "\"width\": 0", "nodes['m'].params['width']: is 0, but must be above 0 metres")]
    [InlineData("mesh_occlusion", "\"reach\": 0", "nodes['m'].params['reach']: is 0, but must be above 0 metres")]
    public void Parameters_the_file_cannot_refuse_are_refused_with_or_without_a_source(string type, string parameters, string message)
    {
        Assert.Equal(message, Assert.Throws<MeshParameterException>(() => Run(null, Node(type, parameters), 64)).Message);
        Assert.Equal(message, Assert.Throws<MeshParameterException>(() => Run(new MeshBakeSource(Wrench(), "wrench"), Node(type, parameters), 64)).Message);
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
