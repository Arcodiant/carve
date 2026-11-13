using System.Numerics;
using Xunit;

namespace Carve.Core.Tests;

/// <summary>T-64's consumer sweep: every reader of a mesh skips tombstones, never throws on one, draws or counts one.</summary>
[Trait("Tier", "unit")]
public class TombstoneSweepTests
{
    // TestMeshes.Cube(): face 1 is +z [4, 5, 6, 7]; vertex 6 is (1, 1, 1), on faces 1 (+z), 3 (+y) and 5 (+x).
    private static PolygonMesh Topless() => TestMeshes.Ok(MeshDelete.Delete(TestMeshes.Cube(), null, [1]));

    private static PolygonMesh Cornerless() => TestMeshes.Ok(MeshDelete.Delete(TestMeshes.Cube(), [6], null));

    private static Selection Select(PolygonMesh mesh, ElementKind kind, string where) =>
        MeshQuery.Select(mesh, kind, MeshQuery.Parse(where).Expression!);

    [Fact]
    public void TriangulationGivesATombstoneNoTriangles()
    {
        var m = Cornerless();
        var triangles = Triangulator.Triangulate(m);
        Assert.Equal(6, triangles.Count);
        Assert.All(triangles, t => Assert.Contains(t.Face, new[] { 0, 2, 4 }));
        Assert.All(triangles, t => Assert.DoesNotContain(6, new[] { t.A, t.B, t.C }));
        Assert.Empty(Triangulator.TriangulateFace(m, 1));
        Assert.Equal(6, FlatShading.Triangles(m).Count);
        Assert.Equal(6, Shading.Triangles(m).Count);
    }

    [Fact]
    public void SmoothShadingSkipsTombstones()
    {
        var m = Cornerless().WithShade(MeshShade.Of(ShadingMode.Smooth, []));
        var corners = Shading.Corners(m);
        Assert.Empty(corners[1]);
        Assert.Equal(4, corners[0].Length);
        Assert.Equal(6, Shading.Triangles(m).Count);
    }

    [Fact]
    public void FaceGeometryOnATombstoneIsRefusedPlainly()
    {
        var m = Topless();
        foreach (var call in new Action[] { () => m.FaceNormal(1), () => m.FaceCentre(1), () => m.FaceArea(1), () => m.FaceLabelPoint(1) })
            Assert.Equal("faces[1]: deleted", Assert.Throws<InvalidOperationException>(call).Message);
    }

    [Fact]
    public void TopologyHasNoEdgeOfATombstone()
    {
        var m = Cornerless();
        Assert.DoesNotContain(m.Topology.Edges, e => e.Lo == 6 || e.Hi == 6);
        Assert.All(m.Topology.Edges, e => Assert.DoesNotContain(e.Faces, u => u.Face is 1 or 3 or 5));
        Assert.Empty(m.Topology.VertexFaces(6));
    }

    [Fact]
    public void ReportCountsLiveElementsAndWarnsOfNoTombstone()
    {
        var r = MeshReport.Create(Cornerless());
        Assert.Equal(7, r.VertexCount);
        Assert.Equal(3, r.FaceCount);
        Assert.False(r.Closed);
        Assert.DoesNotContain(r.Warnings, w => w.Kind == MeshWarningKind.UnusedVertex);
        // The bounds are the live vertices', not the tombstone's zeroed slot: on a cube moved off the origin to
        // (2, 2, 2)..(3, 3, 3), a zeroed slot counted would pull the minimum to the origin.
        var cube = TestMeshes.Cube();
        var moved = TestMeshes.Make(cube.Vertices.Select(v => v + new Vector3(2)).ToArray(), cube.Faces.Select(f => f.Loop.ToArray()).ToArray());
        var off = MeshReport.Create(TestMeshes.Ok(MeshDelete.Delete(moved, [6], null)));
        Assert.Equal(new Vector3(2), off.BoundsMin);
        Assert.Equal(new Vector3(3, 3, 3), off.BoundsMax);
    }

    [Fact]
    public void CheckSkipsTombstones()
    {
        var health = MeshHealth.Create(Topless());
        Assert.Single(health.Shells);
        Assert.Equal([0, 2, 3, 4, 5], health.Shells[0].Faces);
        Assert.False(health.Shells[0].Closed);
        Assert.Equal(5, health.Stats.Faces);
        Assert.Equal(8, health.Stats.Vertices);
        Assert.Equal(5.0, health.Stats.Area, 6);
        Assert.DoesNotContain(health.Problems, p => p.Element is "faces[1]" or "vertices[6]");
        // A deleted vertex's zeroed slot is not coincident with vertex 0 at the origin.
        Assert.DoesNotContain(MeshHealth.Create(Cornerless()).Problems, p => p.Check == "coincident-vertices");
    }

    [Fact]
    public void SelectNeverReturnsATombstoneAndCountsLiveRows()
    {
        var m = Cornerless();
        var unattached = Select(m, ElementKind.Vertices, "unattached");
        Assert.Empty(unattached.Ids);
        Assert.Equal(7, unattached.Of);
        var faces = Select(m, ElementKind.Faces, "area > 0");
        Assert.Equal([0, 2, 4], faces.Ids);
        Assert.Equal(3, faces.Of);
        Assert.Equal([0, 2, 4], Select(m, ElementKind.Faces, "y < 0.5 or y >= 0.5").Ids);
        Assert.Equal(4, Select(Topless(), ElementKind.Edges, "open").Edges.Count);
    }

    [Fact]
    public void SubdivideKeepsTombstonesAndAddsNoLiveStray()
    {
        var r = MeshEdits.Subdivide(Topless(), 1);
        var m = TestMeshes.Ok(r);
        Assert.True(m.IsFaceDeleted(1));
        Assert.Equal(20, m.LiveFaceCount);
        // The face point made for the tombstone (vertex 8 + 1) is a tombstone too.
        Assert.True(m.IsVertexDeleted(9));
        Assert.DoesNotContain(9, r.NewVertices);
        Assert.DoesNotContain(MeshReport.Create(m).Warnings, w => w.Kind == MeshWarningKind.UnusedVertex);
    }

    [Fact]
    public void TheCombinedModelKeepsTombstonesAtTheirOffsets()
    {
        var model = Model.Of("a", TestMeshes.Cube()).With("b", Cornerless());
        var combined = model.Combined!;
        Assert.True(combined.IsVertexDeleted(8 + 6));
        Assert.True(combined.IsFaceDeleted(6 + 1));
        Assert.Equal(15, combined.LiveVertexCount);
        Assert.Equal(9, combined.LiveFaceCount);
        Assert.Equal((8, 6), model.OffsetOf("b"));
    }
}
