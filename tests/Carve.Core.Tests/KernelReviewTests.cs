using System.Numerics;
using Xunit;

namespace Carve.Core.Tests;

/// <summary>Regressions from the RV-1 review of the kernel: scale, self-intersection, many-sided faces, per-face guards, winding, immutability.</summary>
public class KernelReviewTests
{
    private static MeshResult Create(Vector3[] v, int[][] f) => PolygonMesh.Create(v, f);

    public static TheoryData<float> Scales => new() { 1e-12f, 1e-11f, 1e-6f, 1f, 1e10f, 1e15f, 1e20f };

    [Theory]
    [MemberData(nameof(Scales))]
    public void TriangleAtAnyScaleIsAcceptedWithAUnitNormal(float s)
    {
        var mesh = TestMeshes.Make([new(0, 0, 0), new(s, 0, 0), new(0, s, 0)], [[0, 1, 2]]);
        Assert.True((mesh.FaceNormal(0) - Vector3.UnitZ).Length() < 1e-6f);
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void QuadAtAnyScaleIsAcceptedTriangulatesAndHasAUnitNormal(float s)
    {
        var mesh = TestMeshes.Make([new(0, 0, 0), new(s, 0, 0), new(s, s, 0), new(0, s, 0)], [[0, 1, 2, 3]]);
        Assert.True((mesh.FaceNormal(0) - Vector3.UnitZ).Length() < 1e-6f);
        Assert.Equal(2, Triangulator.Triangulate(mesh).Count);
        Assert.DoesNotContain(MeshReport.Create(mesh).Warnings, w => w.Kind == MeshWarningKind.NonPlanarFace);
    }

    [Fact]
    public void ExactlyPlanarQuadFarFromTheOriginHasNoNonPlanarWarning()
    {
        var mesh = TestMeshes.Make(
            [new(65536, 65536, -131072), new(65537.375f, 65535.5f, -131072.875f), new(65536.125f, 65536.875f, -131073), new(65534.75f, 65537.375f, -131072.125f)],
            [[0, 1, 2, 3]]);
        Assert.DoesNotContain(MeshReport.Create(mesh).Warnings, w => w.Kind == MeshWarningKind.NonPlanarFace);
    }

    [Fact]
    public void DoubleWoundHexagonIsRefusedAsSelfIntersecting()
    {
        var v = new Vector3[] { new(0, 0, 0), new(4, 0, 0), new(2, 3, 0), new(0.1f, 0.1f, 0), new(3.9f, 0.1f, 0), new(2, 2.5f, 0) };
        var r = Create(v, [[0, 1, 2, 3, 4, 5]]);
        Assert.False(r.Succeeded);
        var error = Assert.Single(r.Errors);
        Assert.Equal("faces[0]", error.Element);
        Assert.Contains("self-intersecting", error.Message);
    }

    [Fact]
    public void SelfIntersectionIsFoundInThePlaneOfTheFaceNotTheXyPlane()
    {
        // The same hexagon standing in the XZ plane.
        var v = new Vector3[] { new(0, 0, 0), new(4, 0, 0), new(2, 0, 3), new(0.1f, 0, 0.1f), new(3.9f, 0, 0.1f), new(2, 0, 2.5f) };
        Assert.Contains(Create(v, [[0, 1, 2, 3, 4, 5]]).Errors, e => e.Element == "faces[0]" && e.Message.Contains("self-intersecting"));
    }

    [Fact]
    public void AnOutlineThatOnlyTouchesItselfIsNotReportedAsCrossing()
    {
        // A vertex (3,0) on the opposite edge: touching, not crossing. Whatever else is said, it is not a crossing.
        var v = new Vector3[] { new(0, 0, 0), new(6, 0, 0), new(6, 4, 0), new(4, 4, 0), new(3, 0, 0), new(2, 4, 0), new(0, 4, 0) };
        var r = Create(v, [[0, 1, 2, 3, 4, 5, 6]]);
        Assert.DoesNotContain(r.Errors, e => e.Message.Contains("crosses"));
    }

    [Theory]
    [InlineData(64)]
    [InlineData(4096)]
    public void ManySidedConvexFaceKeepsEveryVertex(int n)
    {
        var v = Enumerable.Range(0, n).Select(i => new Vector3(MathF.Cos(2 * MathF.PI * i / n), MathF.Sin(2 * MathF.PI * i / n), 0)).ToArray();
        var mesh = TestMeshes.Make(v, [Enumerable.Range(0, n).ToArray()]);
        var tris = Triangulator.Triangulate(mesh);
        Assert.Equal(n - 2, tris.Count);
        Assert.Equal(n, tris.SelectMany(t => new[] { t.A, t.B, t.C }).Distinct().Count());
    }

    [Fact]
    public void BadVertexSuppressesChecksOnlyForFacesThatTouchIt()
    {
        var v = new Vector3[] { new(0, 0, 0), new(1, 0, 0), new(2, 0, 0), new(0, 1, 0), new(0, float.PositiveInfinity, 0) };
        var r = Create(v, [[0, 1, 2], [0, 1, 3, 4]]);
        Assert.Contains(r.Errors, e => e.Element == "vertices[4]");
        Assert.Contains(r.Errors, e => e.Element == "faces[0]" && e.Message.Contains("zero area"));
        Assert.DoesNotContain(r.Errors, e => e.Element == "faces[1]");
    }

    [Fact]
    public void NoVerticesGivesAnOutOfRangeMessageThatMakesSense()
    {
        var error = Assert.Single(Create([], [[0, 1, 2]]).Errors, e => e.Element == "faces[0][0]");
        Assert.Contains("no vertices", error.Message);
        Assert.DoesNotContain("-1", error.Message);
    }

    [Fact]
    public void FacesSharingAnEdgeInTheSameDirectionReportInconsistentWindingBesideNonManifold()
    {
        var mesh = TestMeshes.Make([new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(0, 0, 1), new(0, -1, 0)], [[0, 1, 2], [0, 1, 3], [0, 1, 4]]);
        var warnings = MeshReport.Create(mesh).Warnings;
        Assert.Contains(warnings, w => w.Kind == MeshWarningKind.NonManifoldEdge && w.Element == "edges[0-1]");
        Assert.Contains(warnings, w => w.Kind == MeshWarningKind.InconsistentWinding && w.Element == "edges[0-1]");
    }

    [Fact]
    public void TwoFacesCrossingAnEdgeInOppositeDirectionsAreConsistent()
    {
        var mesh = TestMeshes.Make([new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(0, 0, 1)], [[0, 1, 2], [1, 0, 3]]);
        Assert.DoesNotContain(MeshReport.Create(mesh).Warnings, w => w.Kind == MeshWarningKind.InconsistentWinding);
    }

    [Fact]
    public void MeshDoesNotChangeWhenTheCallersArraysDo()
    {
        var vertices = TestMeshes.CubeVertices.ToArray();
        var face = new[] { 0, 3, 2, 1 };
        var faces = new[] { face };
        var mesh = TestMeshes.Make(vertices, faces);

        vertices[0] = new Vector3(9, 9, 9);
        face[0] = 7;
        faces[0] = [5, 6, 7];

        Assert.Equal(Vector3.Zero, mesh.Vertices[0]);
        Assert.Equal([0, 3, 2, 1], mesh.Faces[0]);
        Assert.Single(mesh.Faces);
    }

    [Fact]
    public void MeshExposesNoWayToWriteThrough()
    {
        var mesh = TestMeshes.Cube();
        Assert.Throws<NotSupportedException>(() => ((IList<Vector3>)mesh.Vertices)[0] = Vector3.One);
        Assert.Throws<NotSupportedException>(() => ((IList<int>)mesh.Faces[0])[0] = 7);
        Assert.Throws<NotSupportedException>(() => ((IList<IReadOnlyList<int>>)mesh.Faces)[0] = new[] { 1, 2, 3 });
    }
}
