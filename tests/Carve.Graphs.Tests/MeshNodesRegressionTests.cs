using System.Numerics;
using Carve.Core;
using Carve.Core.Baking;
using Carve.Core.Tests;
using Xunit;
using static Carve.Graphs.Tests.MeshNodesTests;

namespace Carve.Graphs.Tests;

/// <summary>
/// The mesh-input nodes' tests that bake occlusion or a 1024 layout and so take well over a second (RV-82): suite
/// <c>graphs-regression</c>. Each asserts what it did in <see cref="MeshNodesTests"/>, at the sizes T-161 names.
/// </summary>
[Trait("Tier", "regression")]
public class MeshNodesRegressionTests
{
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

    // ---- determinism ----

    [Fact]
    public void Two_sources_give_the_same_bytes()
    {
        var model = Wrench();
        foreach (var node in new[] { Node(MeshCurvatureNode.TypeName, "\"width\": 0.003"), Node(MeshOcclusionNode.TypeName, "\"rays\": 8, \"reach\": 0.035"), Node(MeshPositionNode.TypeName) })
            Assert.Equal(Run(new MeshBakeSource(model, "wrench"), node, 256), Run(new MeshBakeSource(model, "wrench"), node, 256));
    }
}
