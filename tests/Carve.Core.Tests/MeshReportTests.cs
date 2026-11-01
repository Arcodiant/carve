using System.Numerics;
using Xunit;

namespace Carve.Core.Tests;

public class MeshReportTests
{
    private static MeshReport Report(Vector3[] v, int[][] f) => MeshReport.Create(TestMeshes.Make(v, f));

    private static bool Has(MeshReport r, MeshWarningKind kind) => r.Warnings.Any(w => w.Kind == kind);

    [Fact]
    public void UnitCubeIsClosedWithCleanCounts()
    {
        var r = MeshReport.Create(TestMeshes.Cube());
        Assert.Equal(8, r.VertexCount);
        Assert.Equal(6, r.FaceCount);
        Assert.Equal(12, r.EdgeCount);
        Assert.True(r.Closed);
        Assert.Equal(Vector3.Zero, r.BoundsMin);
        Assert.Equal(Vector3.One, r.BoundsMax);
        Assert.Empty(r.Warnings);
    }

    [Fact]
    public void SingleQuadHasOpenEdges()
    {
        var r = Report([new(0, 0, 0), new(1, 0, 0), new(1, 1, 0), new(0, 1, 0)], [[0, 1, 2, 3]]);
        Assert.False(r.Closed);
        Assert.Equal(4, r.EdgeCount);
        Assert.Equal(4, r.Warnings.Count(w => w.Kind == MeshWarningKind.OpenEdge));
    }

    [Fact]
    public void EdgeSharedByThreeFacesWarns()
    {
        var v = new Vector3[] { new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(0, -1, 0), new(0, 0, 1) };
        var r = Report(v, [[0, 1, 2], [1, 0, 3], [0, 1, 4]]);
        Assert.True(Has(r, MeshWarningKind.NonManifoldEdge));
        Assert.False(r.Closed);
    }

    [Fact]
    public void AdjacentFacesWoundOppositelyWarn()
    {
        var v = new Vector3[] { new(0, 0, 0), new(1, 0, 0), new(1, 1, 0), new(0, 1, 0) };
        // Shared edge 0-2: first face runs 2->0, the second also 2->0.
        var r = Report(v, [[0, 1, 2], [2, 3, 0]]);
        Assert.False(Has(r, MeshWarningKind.InconsistentWinding));
        r = Report(v, [[0, 1, 2], [0, 3, 2]]);
        Assert.Contains(r.Warnings, w => w.Kind == MeshWarningKind.InconsistentWinding && w.Element == "edges[0-2]");
    }

    [Fact]
    public void NonPlanarQuadWarnsButSlightWarpDoesNot()
    {
        var bent = Report([new(0, 0, 0), new(1, 0, 0), new(1, 1, 0.2f), new(0, 1, 0)], [[0, 1, 2, 3]]);
        Assert.Contains(bent.Warnings, w => w.Kind == MeshWarningKind.NonPlanarFace && w.Element == "faces[0]");

        var flat = Report([new(0, 0, 0), new(1, 0, 0), new(1, 1, 1e-7f), new(0, 1, 0)], [[0, 1, 2, 3]]);
        Assert.False(Has(flat, MeshWarningKind.NonPlanarFace));
    }

    [Fact]
    public void PlanarityToleranceScalesWithFaceSize()
    {
        // The same relative warp (0.2 of the side) warns at any scale.
        var big = Report([new(0, 0, 0), new(1000, 0, 0), new(1000, 1000, 200), new(0, 1000, 0)], [[0, 1, 2, 3]]);
        Assert.True(Has(big, MeshWarningKind.NonPlanarFace));
        // A 0.001 deviation is large on a unit face but negligible on a 1000-unit one.
        var small = Report([new(0, 0, 0), new(1000, 0, 0), new(1000, 1000, 0.001f), new(0, 1000, 0)], [[0, 1, 2, 3]]);
        Assert.False(Has(small, MeshWarningKind.NonPlanarFace));
    }

    [Fact]
    public void UnusedVertexWarnsAndIsLeftOutOfBounds()
    {
        var r = Report([new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(50, 50, 50)], [[0, 1, 2]]);
        Assert.Contains(r.Warnings, w => w.Kind == MeshWarningKind.UnusedVertex && w.Element == "vertices[3]");
        Assert.Equal(new Vector3(1, 1, 0), r.BoundsMax);
    }
}
