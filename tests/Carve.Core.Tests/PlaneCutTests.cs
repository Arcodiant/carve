using System.Numerics;
using Xunit;

namespace Carve.Core.Tests;

/// <summary>T-65's <see cref="PlaneCut"/>: a mesh sliced by a plane, both sides kept or one with the cut filled.</summary>
[Trait("Tier", "unit")]
public class PlaneCutTests
{
    private static readonly Vector3 Up = new(0, 1, 0);
    private static readonly Vector3 Half = new(0, 0.5f, 0);

    private static void IsClosed(PolygonMesh m) =>
        Assert.All(m.Topology.Edges, e => Assert.True(e.IsManifold, e.Name));

    private static PolygonMesh Ok(PlaneCutResult r) => TestMeshes.Ok(r.Edit);

    private static float Top(PolygonMesh m) => m.LiveVertices.Max(v => m.Vertices[v].Y);
    private static float Bottom(PolygonMesh m) => m.LiveVertices.Min(v => m.Vertices[v].Y);

    /// <summary>An L-shaped block: the L (0,0) (2,0) (2,1) (1,1) (1,2) (0,2) at z = 0 (vertices 0..5) and z = 1 (6..11).</summary>
    private static PolygonMesh LBlock()
    {
        (float X, float Y)[] l = [(0, 0), (2, 0), (2, 1), (1, 1), (1, 2), (0, 2)];
        var v = l.Select(p => new Vector3(p.X, p.Y, 0)).Concat(l.Select(p => new Vector3(p.X, p.Y, 1))).ToArray();
        var faces = new List<int[]> { new[] { 0, 5, 4, 3, 2, 1 }, new[] { 6, 7, 8, 9, 10, 11 } };
        for (var i = 0; i < 6; i++) faces.Add([i, (i + 1) % 6, (i + 1) % 6 + 6, i + 6]);
        return TestMeshes.Make(v, faces);
    }

    [Fact]
    public void KeepingBothSidesOnlySplitsTheCube()
    {
        var r = PlaneCut.Cut(TestMeshes.Cube(), Half, Up);
        var m = Ok(r);

        Assert.Equal([8, 9, 10, 11], r.Edit.NewVertices);
        Assert.Equal([6, 7, 8, 9], r.Edit.NewFaces);
        Assert.Equal([0, 1, 4, 5], r.Edit.Split.Select(s => s.Face));
        Assert.All(r.Edit.Split, s => Assert.Equal(2, s.Pieces.Count));
        Assert.True(r.Edit.Deleted.IsEmpty);
        Assert.Equal(12, m.LiveVertexCount);
        Assert.Equal(10, m.LiveFaceCount);
        Assert.All(r.Edit.NewVertices, v => Assert.Equal(0.5f, m.Vertices[v].Y));
        Assert.Equal([(8, 9), (8, 10), (9, 11), (10, 11)], r.CutEdges);
        Assert.Empty(r.Warnings);
        IsClosed(m);
    }

    [Fact]
    public void KeepingBelowWithFillGivesAHalfHeightClosedBox()
    {
        var r = PlaneCut.Cut(TestMeshes.Cube(), Half, Up, PlaneKeep.Below);
        var m = Ok(r);

        Assert.Equal(8, m.LiveVertexCount);
        Assert.Equal(6, m.LiveFaceCount);
        Assert.Equal(0.5f, Top(m));
        Assert.Equal(0f, Bottom(m));
        Assert.Equal([2, 3, 6, 7], r.Edit.Deleted.Vertices);
        // The upper pieces the split appended (6..9) are listed in split and in deleted (the person's ruling).
        Assert.Equal([3, 6, 7, 8, 9], r.Edit.Deleted.Faces);
        Assert.Equal([[0, 6], [1, 7], [4, 8], [5, 9]], r.Edit.Split.Select(s => s.Pieces.ToArray()));
        Assert.Equal([10], r.Edit.NewFaces);
        Assert.Equal([8, 9, 10, 11], r.Edit.NewVertices);
        var cap = m.Faces[10];
        Assert.Equal([8, 10, 11, 9], cap.Loop);
        Assert.True(Vector3.Dot(m.FaceNormal(10), Up) > 0.99f, "the cap faces up, out of the kept side");
        Assert.Equal(1.0, m.FaceArea(10), 6);
        Assert.Empty(r.Warnings);
        IsClosed(m);
    }

    [Fact]
    public void KeepingAboveWithFillCapsTheUnderside()
    {
        var r = PlaneCut.Cut(TestMeshes.Cube(), Half, Up, PlaneKeep.Above);
        var m = Ok(r);

        Assert.Equal(8, m.LiveVertexCount);
        Assert.Equal(6, m.LiveFaceCount);
        Assert.Equal(0.5f, Bottom(m));
        Assert.Equal(1f, Top(m));
        Assert.Equal([0, 1, 4, 5], r.Edit.Deleted.Vertices);
        Assert.Equal([0, 1, 2, 4, 5], r.Edit.Deleted.Faces);
        Assert.Equal([6, 7, 8, 9, 10], r.Edit.NewFaces);
        Assert.True(Vector3.Dot(m.FaceNormal(10), Up) < -0.99f, "the cap faces down, out of the kept side");
        IsClosed(m);
    }

    [Fact]
    public void WithoutFillTheCutIsLeftOpen()
    {
        var r = PlaneCut.Cut(TestMeshes.Cube(), Half, Up, PlaneKeep.Below, fill: false);
        var m = Ok(r);

        Assert.Equal(5, m.LiveFaceCount);
        Assert.Empty(r.Edit.NewFaces);
        Assert.Equal([(8, 9), (8, 10), (9, 11), (10, 11)], r.CutEdges);
        Assert.Equal(4, m.Topology.Edges.Count(e => !e.IsManifold));
        Assert.Empty(r.Warnings);
    }

    [Fact]
    public void AConcaveLBlockIsCutIntoTwoTipsEachCapped()
    {
        // x + y = 2.5 crosses the L's two arms and leaves the notch between them.
        var normal = Vector3.Normalize(new Vector3(1, 1, 0));
        var point = new Vector3(1.25f, 1.25f, 0);

        var both = PlaneCut.Cut(LBlock(), point, normal);
        var mb = Ok(both);
        var front = Assert.Single(both.Edit.Split, s => s.Face == 0);
        Assert.Equal(3, front.Pieces.Count);
        var back = Assert.Single(both.Edit.Split, s => s.Face == 1);
        Assert.Equal(3, back.Pieces.Count);
        IsClosed(mb);

        var below = PlaneCut.Cut(LBlock(), point, normal, PlaneKeep.Below);
        var m = Ok(below);
        IsClosed(m);
        var caps = below.Edit.NewFaces.Where(f => m.Faces[f].Loop.Count == 4 && Vector3.Dot(m.FaceNormal(f), normal) > 0.99f).ToArray();
        Assert.Equal(2, caps.Length);
        Assert.All(m.LiveVertices, v => Assert.True(m.Vertices[v].X + m.Vertices[v].Y <= 2.5f + 1e-4f));
        Assert.Empty(below.Warnings);

        var above = PlaneCut.Cut(LBlock(), point, normal, PlaneKeep.Above, fill: false);
        var ma = Ok(above);
        Assert.Equal(8, ma.Topology.Edges.Count(e => !e.IsManifold));
        Assert.Equal(8, above.CutEdges.Count);
        Assert.All(ma.LiveVertices, v => Assert.True(ma.Vertices[v].X + ma.Vertices[v].Y >= 2.5f - 1e-4f));
    }

    /// <summary>A square tube along y: outer 2 x 2 (0..3 at y = 0, 4..7 at y = 1), inner 1 x 1 (8..11, 12..15), holed ends.</summary>
    private static PolygonMesh Tube()
    {
        (float X, float Z)[] outer = [(0, 0), (2, 0), (2, 2), (0, 2)];
        (float X, float Z)[] inner = [(0.5f, 0.5f), (1.5f, 0.5f), (1.5f, 1.5f), (0.5f, 1.5f)];
        var v = new[] { outer, inner }.SelectMany(ring => new[] { 0f, 1f }.SelectMany(y => ring.Select(p => new Vector3(p.X, y, p.Z)))).ToArray();
        var faces = new List<Face> { new([0, 1, 2, 3], [[8, 11, 10, 9]]), new([4, 7, 6, 5], [[12, 13, 14, 15]]) };
        for (var i = 0; i < 4; i++)
        {
            var j = (i + 1) % 4;
            faces.Add(new Face([i, i + 4, j + 4, j]));
            faces.Add(new Face([8 + i, 8 + j, 12 + j, 12 + i]));
        }
        var r = PolygonMesh.Create(v, faces);
        Assert.True(r.Succeeded, string.Join("; ", r.Errors));
        IsClosed(r.Mesh!);
        return r.Mesh!;
    }

    [Fact]
    public void TwoNestedLoopsGiveACapWithAHole()
    {
        var r = PlaneCut.Cut(Tube(), Half, Up, PlaneKeep.Below);
        var m = Ok(r);

        var cap = m.Faces[Assert.Single(r.Edit.NewFaces, f => f >= 10 && m.Faces[f].Holes.Count > 0)];
        Assert.Single(cap.Holes);
        Assert.Equal(4, cap.Loop.Count);
        Assert.Equal(4, cap.Holes[0].Count);
        var id = m.Faces.ToList().IndexOf(cap);
        Assert.Equal(3.0, m.FaceArea(id), 5);
        Assert.True(Vector3.Dot(m.FaceNormal(id), Up) > 0.99f);
        Assert.Empty(r.Warnings);
        Assert.Equal(1, r.Edit.NewFaces.Count(f => f >= 18));
        IsClosed(m);
        Assert.Equal(0.5f, Top(m));
    }

    [Fact]
    public void AnOpenMeshLeavesTheCutOpenWithAWarningNamingItsEnds()
    {
        var open = TestMeshes.Ok(MeshDelete.Delete(TestMeshes.Cube(), null, [1]));

        var r = PlaneCut.Cut(open, Half, Up, PlaneKeep.Below);
        var m = Ok(r);

        var w = Assert.Single(r.Warnings);
        Assert.Equal(PlaneCut.OpenMessage([11, 9, 8, 10]), w);
        Assert.Equal("fill: the cut from vertex 11 to vertex 10 is not a closed loop (the mesh is open there), so it is left open", w);
        Assert.Empty(r.Edit.NewFaces);
        Assert.Equal(4, m.LiveFaceCount);
    }

    [Fact]
    public void AFaceInThePlaneIsNotSplitAndStaysUntouchedWhicheverSideIsKept()
    {
        // Ruled 2026-11-13: a face in the plane is the border. It keeps its id and colour, and no cap is laid over it.
        var top = new Vector3(0, 1, 0);
        var red = TestMeshes.Cube().WithStyle(FaceStyle.None.WithColour([3], "#ff0000"));

        var both = PlaneCut.Cut(red, top, Up);
        Assert.Empty(both.Edit.Split);
        Assert.Equal(6, Ok(both).LiveFaceCount);

        var below = PlaneCut.Cut(red, top, Up, PlaneKeep.Below);
        var m = Ok(below);
        Assert.Empty(below.Edit.Deleted.Faces);
        Assert.Empty(below.Edit.Deleted.Vertices);
        Assert.Empty(below.Edit.NewFaces);
        Assert.Equal([3, 7, 6, 2], m.Faces[3].Loop);
        Assert.Equal("#ff0000", m.Style!.ColourOf(3));
        IsClosed(m);

        foreach (var fill in new[] { true, false })
        {
            var above = PlaneCut.Cut(red, top, Up, PlaneKeep.Above, fill: fill);
            var ma = Ok(above);
            Assert.Equal([3], ma.LiveFaces);
            Assert.Empty(above.Edit.NewFaces);
            Assert.Equal("#ff0000", ma.Style!.ColourOf(3));
        }

        var floor = PlaneCut.Cut(TestMeshes.Cube(), Vector3.Zero, Up, PlaneKeep.Above);
        var mf = Ok(floor);
        Assert.Empty(floor.Edit.Deleted.Faces);
        Assert.Empty(floor.Edit.NewFaces);
        Assert.Equal([0, 1, 5, 4], mf.Faces[2].Loop);
    }

    [Fact]
    public void ACapBesideAFaceInThePlaneFillsOnlyTheCutAndNotThatFace()
    {
        // The L-block cut at its step's height: side 4 (the step's top, [2, 3, 9, 8]) lies in the plane. Either side kept,
        // it stays, and the cap is the tower's section alone, on corners 3 and 9 and the two made on side 7.
        foreach (var keep in new[] { PlaneKeep.Above, PlaneKeep.Below })
        {
            var r = PlaneCut.Cut(TestMeshes.LBlock(), new Vector3(0, 1, 0), Up, keep);
            var m = Ok(r);
            Assert.Equal([2, 3, 9, 8], m.Faces[4].Loop);
            Assert.DoesNotContain(4, r.Edit.Deleted.Faces);
            var cap = Assert.Single(r.Edit.NewFaces, f => m.Faces[f].Corners.All(c => MathF.Abs(m.Vertices[c].Y - 1) < 1e-6f));
            Assert.Equal(4, m.Faces[cap].Corners.Count);
            Assert.Contains(3, m.Faces[cap].Corners);
            Assert.Contains(9, m.Faces[cap].Corners);
            Assert.DoesNotContain(2, m.Faces[cap].Corners);
            Assert.DoesNotContain(8, m.Faces[cap].Corners);
        }
    }

    [Fact]
    public void TheFillOfASmoothMeshHasAHardBorder()
    {
        var smooth = TestMeshes.Cube().WithShade(MeshShade.Of(ShadingMode.Smooth, [(0, 1)]));

        var m = Ok(PlaneCut.Cut(smooth, Half, Up, PlaneKeep.Below));

        Assert.Equal(ShadingMode.Smooth, m.Shade!.Mode);
        Assert.Equal([(0, 1), (8, 9), (8, 10), (9, 11), (10, 11)], m.Shade.Hard);

        var flat = Ok(PlaneCut.Cut(TestMeshes.Cube(), Half, Up, PlaneKeep.Below));
        Assert.True(flat.Shade is null || flat.Shade.Hard.Count == 0);
    }

    [Fact]
    public void ALimitedCutSplitsOnlyItsFaces()
    {
        var r = PlaneCut.Cut(TestMeshes.Cube(), Half, Up, faces: [4]);
        var m = Ok(r);

        Assert.Equal([4], r.Edit.Split.Select(s => s.Face));
        Assert.Equal([8, 9], r.Edit.NewVertices);
        Assert.Equal(7, m.LiveFaceCount);
        IsClosed(m);
    }

    [Theory]
    [InlineData(0f, 0f, 0f)]
    [InlineData(float.NaN, 1f, 0f)]
    [InlineData(0f, float.PositiveInfinity, 0f)]
    public void AZeroOrNonFiniteNormalIsRefused(float x, float y, float z)
    {
        var r = PlaneCut.Cut(TestMeshes.Cube(), Half, new Vector3(x, y, z));
        Assert.False(r.Edit.Succeeded);
        Assert.Equal(["normal: " + PlaneCut.NormalMessage], r.Edit.Errors.Select(e => e.ToString()));
    }

    [Fact]
    public void APlaneTouchingNoFaceIsRefused()
    {
        var r = PlaneCut.Cut(TestMeshes.Cube(), new Vector3(0, 2, 0), Up, PlaneKeep.Below);
        Assert.Equal(["point, normal: " + PlaneCut.TouchesNoFaceMessage], r.Edit.Errors.Select(e => e.ToString()));

        var limited = PlaneCut.Cut(TestMeshes.Cube(), new Vector3(0, 0, 0.5f), new Vector3(0, 0, 1), faces: [3]);
        Assert.True(limited.Edit.Succeeded);
        var beside = PlaneCut.Cut(TestMeshes.Cube(), new Vector3(0, 0, 0.5f), new Vector3(0, 0, 1), faces: [0]);
        Assert.Equal(["point, normal: " + PlaneCut.TouchesNoFaceMessage], beside.Edit.Errors.Select(e => e.ToString()));
    }

    [Fact]
    public void ACutThatWouldLeaveNothingIsRefused()
    {
        // Through corner 6 only: every face is below.
        var r = PlaneCut.Cut(TestMeshes.Cube(), new Vector3(1, 1, 1), new Vector3(1, 1, 1), PlaneKeep.Above);
        Assert.Equal(["keep: this would leave nothing: every face is below the plane"], r.Edit.Errors.Select(e => e.ToString()));
    }

    [Fact]
    public void ALimitedCutKeepingOneSideIsRefused()
    {
        var r = PlaneCut.Cut(TestMeshes.Cube(), Half, Up, PlaneKeep.Below, faces: [4]);
        Assert.Equal(["faces: " + PlaneCut.LimitedKeepMessage], r.Edit.Errors.Select(e => e.ToString()));

        var unknown = PlaneCut.Cut(TestMeshes.Cube(), Half, Up, faces: [9]);
        Assert.False(unknown.Edit.Succeeded);
        Assert.StartsWith("faces[9]", unknown.Edit.Errors[0].ToString());
    }

    [Fact]
    public void ANonFinitePointIsRefused()
    {
        var r = PlaneCut.Cut(TestMeshes.Cube(), new Vector3(float.NaN, 0, 0), Up);
        Assert.Equal(["point: must be a finite point"], r.Edit.Errors.Select(e => e.ToString()));
    }

    /// <summary>
    /// Two unit cubes meeting along one edge, 5-6 at x = z = 1: the first is <see cref="TestMeshes.Cube"/>, the second
    /// the same moved by (1, 0, 1), its corners 5, 8, 9, 6, 10, 11, 12, 13. Edge 5-6 has four faces.
    /// </summary>
    private static PolygonMesh TwoCubesOnAnEdge()
    {
        int[] b = [5, 8, 9, 6, 10, 11, 12, 13];
        var shift = new Vector3(1, 0, 1);
        var v = TestMeshes.CubeVertices.Concat(new[] { 1, 2, 4, 5, 6, 7 }.Select(i => TestMeshes.CubeVertices[i] + shift));
        var faces = TestMeshes.CubeFaces.Concat(TestMeshes.CubeFaces.Select(f => f.Select(c => b[c]).ToArray()));
        return TestMeshes.Make(v, faces);
    }

    [Fact]
    public void ANonManifoldCutBorderIsChainedByLowestVertexId()
    {
        // The person's ruling (2026-11-12): a non-manifold cut border is chained by lowest vertex id. The border meets
        // itself at 17, the new vertex on edge 5-6, with two ways on: 15 (the first cube's) and 19 (the second's).
        // The lowest closes the first cube's loop, so each cube gets its own cap; the highest would wind both into one.
        var r = PlaneCut.Cut(TwoCubesOnAnEdge(), Half, Up, PlaneKeep.Below);
        var m = Ok(r);

        Assert.Empty(r.Warnings);
        Assert.Equal([14, 15, 16, 17, 18, 19, 20], r.Edit.NewVertices);
        Assert.Equal([20, 21], r.Edit.NewFaces);
        Assert.Equal([14, 16, 17, 15], m.Faces[20].Loop);
        Assert.Equal([17, 19, 20, 18], m.Faces[21].Loop);
        foreach (var f in r.Edit.NewFaces)
        {
            Assert.True(Vector3.Dot(m.FaceNormal(f), Up) > 0.99f, "each cap faces up, out of the kept side");
            Assert.Equal(1.0, m.FaceArea(f), 6);
        }
    }
}
