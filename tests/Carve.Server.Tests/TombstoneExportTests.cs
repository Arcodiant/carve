using System.Numerics;
using Carve.Core;
using Carve.Core.Tests;
using Empyrean;
using SharpGLTF.Schema2;
using Model = Carve.Core.Model;
using PolygonMesh = Carve.Core.PolygonMesh;
using Skeleton = Carve.Core.Skeleton;

namespace Carve.Server.Tests;

/// <summary>T-64: export compacts. The .glb holds live elements only, indices remapped, weights following their vertex.</summary>
[Trait("Tier", "unit")]
public class TombstoneExportTests
{
    // Vertex 6 of TestMeshes.Cube() is (1, 1, 1), on faces 1, 3 and 5.
    private static readonly Vector3 Gone = Vector3.One;

    private static double WeightOf(int v) => (v + 1) / 10.0;

    private static Model RiggedCornerless()
    {
        var rig = Skeleton.Create([
            new BoneSpec("Root", null, Vector3.Zero, Quaternion.Identity),
            new BoneSpec("Tip", "Root", new Vector3(0, 1, 0), Quaternion.Identity),
        ]).Skeleton!;
        var cube = TestMeshes.Cube();
        var weights = VertexWeights.None;
        for (var v = 0; v < cube.Vertices.Count; v++)
            weights = weights.With([v], [new Influence(0, WeightOf(v)), new Influence(1, 1 - WeightOf(v))]);
        var deleted = TestMeshes.Ok(MeshDelete.Delete(cube.WithWeights(weights), [6], null));
        return Model.Of("cube", deleted).WithSkeleton(rig);
    }

    [Fact]
    public void A_skinned_export_holds_the_live_vertices_only_each_with_its_own_weights()
    {
        var model = RiggedCornerless();
        Assert.Empty(SkinnedExport.Unweighted(model));
        var glb = SkinnedExport.Write(model, "cube");
        Assert.Equal(0, glb.BoundToRoot);

        var cube = TestMeshes.Cube();
        var root = ModelRoot.ParseGLB(glb.Bytes);
        var seen = new HashSet<Vector3>();
        var indexCount = 0;
        foreach (var primitive in root.LogicalMeshes[0].Primitives)
        {
            var positions = primitive.GetVertexAccessor("POSITION").AsVector3Array();
            var weights = primitive.GetVertexAccessor("WEIGHTS_0").AsVector4Array();
            var indices = primitive.GetIndices();
            indexCount += indices.Count;
            // Compacted: every vertex written is used by a triangle, and no index points past them.
            Assert.All(indices, i => Assert.InRange((int)i, 0, positions.Count - 1));
            Assert.Equal(positions.Count, indices.Distinct().Count());
            for (var i = 0; i < positions.Count; i++)
            {
                var p = positions[i];
                Assert.NotEqual(Gone, p);
                seen.Add(p);
                // The weights follow their vertex: the one at cube vertex v carries v's two weights.
                var v = Enumerable.Range(0, cube.Vertices.Count).Single(k => cube.Vertices[k] == p);
                var w = weights[i];
                var got = new[] { w.X, w.Y, w.Z, w.W }.Where(x => x > 0).Order().ToArray();
                var want = new[] { WeightOf(v), 1 - WeightOf(v) }.Order().ToArray();
                Assert.Equal(want.Length, got.Length);
                for (var k = 0; k < want.Length; k++) Assert.Equal(want[k], got[k], 5);
            }
        }
        Assert.Equal(3 * 2 * 3, indexCount);
        Assert.Equal(7, seen.Count);
    }

    [Fact]
    public void A_plain_export_of_a_topless_cube_reads_back_through_the_engine_with_five_faces()
    {
        var mesh = TestMeshes.Ok(MeshDelete.Delete(TestMeshes.Cube(), null, [1]));
        var parsed = GlbExportTests.Parser.Parse(GlbExport.Write(mesh, Shading.Triangles(mesh), "topless"), ModelFormat.Gltf);
        Assert.Empty(parsed.Warnings ?? []);
        Assert.Equal(5 * 2 * 3, parsed.MeshData.Indices!.Length);
        Assert.All(parsed.MeshData.Indices!, i => Assert.InRange((int)i, 0, parsed.MeshData.Vertices.Length - 1));
    }
}
