using System.Numerics;
using Xunit;

namespace Carve.Core.Tests;

/// <summary>T-65's <see cref="FaceSplit"/>: the shared face-splitting step of the cuts.</summary>
[Trait("Tier", "unit")]
public class FaceSplitTests
{
    // TestMeshes.Cube(): face 1 is +z [4, 5, 6, 7]; face 2 is -y [0, 1, 5, 4]; face 3 is +y [3, 7, 6, 2].
    private static CutPoint E(int a, int b, double t) => CutPoint.OnEdge(a, b, t);
    private static CutPoint V(int v) => CutPoint.AtVertex(v);

    private static FaceSplitResult Split(PolygonMesh mesh, params FaceCut[] cuts) => FaceSplit.Split(mesh, cuts);

    private static PolygonMesh Square() => TestMeshes.Make(
        [new(0, 0, 0), new(2, 0, 0), new(2, 2, 0), new(0, 2, 0)], [[0, 1, 2, 3]]);

    /// <summary>A 4 x 4 plate in z = 0, outer [0, 1, 2, 3] counter-clockwise, hole [4, 5, 6, 7] the middle 2 x 2, clockwise.</summary>
    private static PolygonMesh Plate()
    {
        Vector3[] v =
        [
            new(-2, -2, 0), new(2, -2, 0), new(2, 2, 0), new(-2, 2, 0),
            new(-1, -1, 0), new(-1, 1, 0), new(1, 1, 0), new(1, -1, 0),
        ];
        var r = PolygonMesh.Create(v, [new Face([0, 1, 2, 3], [[4, 5, 6, 7]])]);
        Assert.True(r.Succeeded, string.Join("; ", r.Errors));
        return r.Mesh!;
    }

    private static void IsClosed(PolygonMesh m) =>
        Assert.All(m.Topology.Edges, e => Assert.True(e.IsManifold, e.Name));

    [Fact]
    public void AQuadSplitsInTwoAndItsNeighboursTakeTheNewVerticesKeepingTheirIds()
    {
        var r = Split(TestMeshes.Cube(), new FaceCut(1, E(4, 5, 0.5), E(6, 7, 0.5)));
        var m = TestMeshes.Ok(r.Edit);

        Assert.Equal([8, 9], r.Edit.NewVertices);
        Assert.Equal(new Vector3(0.5f, 0, 1), m.Vertices[8]);
        Assert.Equal(new Vector3(0.5f, 1, 1), m.Vertices[9]);
        Assert.Equal([6], r.Edit.NewFaces);
        Assert.Equal([(8, 9)], r.Segments);
        var s = Assert.Single(r.Edit.Split);
        Assert.Equal(1, s.Face);
        Assert.Equal([1, 6], s.Pieces);
        Assert.Equal(new Face([4, 8, 9, 7]), m.Faces[1]);
        Assert.Equal(new Face([8, 5, 6, 9]), m.Faces[6]);
        Assert.Equal(new Face([0, 1, 5, 8, 4]), m.Faces[2]);
        Assert.Equal(new Face([3, 7, 9, 6, 2]), m.Faces[3]);
        Assert.Equal(TestMeshes.CubeFaces[0], m.Faces[0].Loop);
        IsClosed(m);
        Assert.Equal(1.0, m.FaceArea(1) + m.FaceArea(6), 6);
    }

    [Fact]
    public void ATriangleCutThroughAVertexKeepsItsIdOnTheNextCornerOffTheCut()
    {
        var tri = TestMeshes.Make([new(0, 0, 0), new(2, 0, 0), new(0, 2, 0)], [[0, 1, 2]]);
        var r = Split(tri, new FaceCut(0, V(0), E(1, 2, 0.5)));
        var m = TestMeshes.Ok(r.Edit);

        Assert.Equal([3], r.Edit.NewVertices);
        Assert.Equal(new Vector3(1, 1, 0), m.Vertices[3]);
        // Corner 0 is on the cut, so corner 1 decides: its piece keeps id 0.
        Assert.Equal(new Face([0, 1, 3]), m.Faces[0]);
        Assert.Equal(new Face([0, 3, 2]), m.Faces[1]);
        Assert.Equal([new SplitFace(0, [0, 1])], r.Edit.Split, SplitEq);
    }

    [Fact]
    public void TheIdGoesToThePieceHoldingCornerZeroOrTheNextCornerOffTheCut()
    {
        // Corner 0 off the cut: its piece keeps the id, whichever side.
        var a = TestMeshes.Ok(Split(Square(), new FaceCut(0, E(1, 2, 0.5), E(2, 3, 0.5))).Edit);
        Assert.Contains(0, a.Faces[0].Loop);
        Assert.Equal(new Face([0, 1, 4, 5, 3]), a.Faces[0]);
        Assert.Equal(new Face([4, 2, 5]), a.Faces[1]);

        var b = TestMeshes.Ok(Split(Square(), new FaceCut(0, E(0, 1, 0.5), E(0, 3, 0.5))).Edit);
        Assert.Equal(new Face([0, 4, 5]), b.Faces[0]);
        Assert.Equal(new Face([4, 1, 2, 3, 5]), b.Faces[1]);

        // Corner 0 on the cut (the diagonal 0-2): corner 1 decides.
        var c = TestMeshes.Ok(Split(Square(), new FaceCut(0, V(0), V(2))).Edit);
        Assert.Equal(new Face([0, 1, 2]), c.Faces[0]);
        Assert.Equal(new Face([0, 2, 3]), c.Faces[1]);
        // The other diagonal: corner 0 is off it, and its piece keeps the id.
        var d = TestMeshes.Ok(Split(Square(), new FaceCut(0, V(3), V(1))).Edit);
        Assert.Equal(new Face([0, 1, 3]), d.Faces[0]);
        Assert.Equal(new Face([1, 2, 3]), d.Faces[1]);
    }

    [Fact]
    public void WhenCornersZeroAndOneAreBothOnTheCutCornerTwoDecides()
    {
        var hex = TestMeshes.Make(
            [new(2, 0, 0), new(1, 1.7f, 0), new(-1, 1.7f, 0), new(-2, 0, 0), new(-1, -1.7f, 0), new(1, -1.7f, 0)],
            [[0, 1, 2, 3, 4, 5]]);
        var r = Split(hex, new FaceCut(0, V(0), V(3)), new FaceCut(0, V(1), V(3)));
        var m = TestMeshes.Ok(r.Edit);

        Assert.Equal(new Face([1, 2, 3]), m.Faces[0]);
        Assert.Equal(new Face([0, 1, 3]), m.Faces[1]);
        Assert.Equal(new Face([0, 3, 4, 5]), m.Faces[2]);
        Assert.Equal([new SplitFace(0, [0, 1, 2])], r.Edit.Split, SplitEq);
    }

    [Fact]
    public void ACutThroughAHoleCutsTheHoleIntoBothOutlines()
    {
        var r = Split(Plate(),
            new FaceCut(0, E(0, 1, 0.5), E(7, 4, 0.5)),
            new FaceCut(0, E(5, 6, 0.5), E(2, 3, 0.5)));
        var m = TestMeshes.Ok(r.Edit);

        // By edge, low id then high: (0, 1), (2, 3), (4, 7), (5, 6).
        Assert.Equal([8, 9, 10, 11], r.Edit.NewVertices);
        Assert.Equal(new Vector3(0, -2, 0), m.Vertices[8]);
        Assert.Equal(new Vector3(0, 2, 0), m.Vertices[9]);
        Assert.Equal(new Vector3(0, -1, 0), m.Vertices[10]);
        Assert.Equal(new Vector3(0, 1, 0), m.Vertices[11]);
        Assert.Equal(new Face([0, 8, 10, 4, 5, 11, 9, 3]), m.Faces[0]);
        Assert.Equal(new Face([8, 1, 2, 9, 11, 6, 7, 10]), m.Faces[1]);
        Assert.False(m.Faces[0].HasHoles);
        Assert.False(m.Faces[1].HasHoles);
        Assert.Equal(6.0, m.FaceArea(0), 6);
        Assert.Equal(6.0, m.FaceArea(1), 6);
    }

    [Fact]
    public void AHoleWhollyOnOneSideStaysAHoleOfThePieceAroundIt()
    {
        var r = Split(Plate(), new FaceCut(0, E(0, 1, 0.875), E(2, 3, 0.125)));
        var m = TestMeshes.Ok(r.Edit);

        Assert.Equal(new Face([0, 8, 9, 3], [[4, 5, 6, 7]]), m.Faces[0]);
        Assert.Equal(new Face([8, 1, 2, 9]), m.Faces[1]);
        Assert.Equal(10.0, m.FaceArea(0), 5);
        Assert.Equal(2.0, m.FaceArea(1), 5);

        // The same cut the other way round puts the hole in the appended piece when corner 0 is beyond it.
        var other = Split(Plate(), new FaceCut(0, E(0, 1, 0.125), E(2, 3, 0.875)));
        var o = TestMeshes.Ok(other.Edit);
        Assert.Equal(new Face([0, 8, 9, 3]), o.Faces[0]);
        Assert.Equal(new Face([8, 1, 2, 9], [[4, 5, 6, 7]]), o.Faces[1]);
    }

    [Fact]
    public void AVertexOnASharedEdgeIsMadeOnceForBothFaces()
    {
        // +z cut from 4-5, and -y cut to 5-4 (the same point, given from the other end).
        var r = Split(TestMeshes.Cube(),
            new FaceCut(1, E(4, 5, 0.5), E(6, 7, 0.5)),
            new FaceCut(2, E(0, 1, 0.5), E(5, 4, 0.5)));
        var m = TestMeshes.Ok(r.Edit);

        Assert.Equal([8, 9, 10], r.Edit.NewVertices);
        Assert.Equal(new Vector3(0.5f, 0, 0), m.Vertices[8]);
        Assert.Equal(new Vector3(0.5f, 0, 1), m.Vertices[9]);
        Assert.Equal(new Vector3(0.5f, 1, 1), m.Vertices[10]);
        Assert.Equal([(9, 10), (8, 9)], r.Segments);
        Assert.Equal([1, 2], r.Edit.Split.Select(s => s.Face));
        Assert.Equal([6, 7], r.Edit.NewFaces);
        IsClosed(m);
    }

    [Fact]
    public void APointWithinTheToleranceOfAVertexIsThatVertex()
    {
        var tol = FaceSplit.Tolerance(TestMeshes.Cube());
        Assert.Equal(PolygonMesh.PlanarityTolerance * Math.Sqrt(3), tol, 9);

        // 4-5 is 1 long: a point at 0.5 tol from 4 is vertex 4, one at 2 tol is new.
        var near = Split(TestMeshes.Cube(), new FaceCut(1, E(4, 5, tol / 2), E(6, 7, 0.5)));
        Assert.Equal([8], near.Edit.NewVertices);
        Assert.Equal([(4, 8)], near.Segments);
        var far = Split(TestMeshes.Cube(), new FaceCut(1, E(4, 5, tol * 2), E(6, 7, 0.5)));
        Assert.Equal([8, 9], far.Edit.NewVertices);

        // Two points on one edge closer than the tolerance are one vertex.
        var two = Split(TestMeshes.Cube(),
            new FaceCut(1, E(4, 5, 0.5), E(6, 7, 0.5)),
            new FaceCut(2, E(0, 1, 0.5), E(5, 4, 0.5 + tol / 4)));
        Assert.Equal(3, two.Edit.NewVertices.Count);
    }

    [Fact]
    public void TheSameCallGivesTheSameIdsEveryTime()
    {
        FaceCut[] cuts =
        [
            new(2, E(0, 1, 0.3), E(5, 4, 0.7)),
            new(1, E(4, 5, 0.3), E(6, 7, 0.7)),
            new(3, E(7, 6, 0.3), E(3, 2, 0.3)),
        ];
        var one = TestMeshes.Ok(FaceSplit.Split(TestMeshes.Cube(), cuts).Edit);
        var two = TestMeshes.Ok(FaceSplit.Split(TestMeshes.Cube(), cuts).Edit);
        Assert.Equal(one.Vertices, two.Vertices);
        Assert.Equal(one.Faces, two.Faces);
        Assert.Equal(12, one.Vertices.Count);
        IsClosed(one);
    }

    [Fact]
    public void PiecesTakeColourAndGroupsAndHardEdgesSplitHard()
    {
        var cube = TestMeshes.Cube()
            .WithStyle(FaceStyle.None.WithColour([1], "#ff0000").WithGroup("lid", [1, 3]))
            .WithShade(MeshShade.Of(ShadingMode.Smooth, [(4, 5), (0, 1)]));
        var m = TestMeshes.Ok(Split(cube, new FaceCut(1, E(4, 5, 0.25), E(6, 7, 0.5))).Edit);

        Assert.Equal("#ff0000", m.Style!.ColourOf(6));
        Assert.Equal("#ff0000", m.Style.ColourOf(1));
        Assert.Equal([1, 3, 6], m.Style.FacesOf("lid"));
        Assert.Equal(ShadingMode.Smooth, m.ShadingMode);
        Assert.Equal([(0, 1), (4, 8), (5, 8)], m.HardEdges);
        Assert.False(m.Shade!.IsHard(8, 9));
    }

    [Fact]
    public void ANewVertexBlendsItsEdgesWeightsByItsFractionCappedAtFour()
    {
        var weights = VertexWeights.Of(
        [
            KeyValuePair.Create(4, new Influence[] { new(0, 0.5), new(1, 0.3), new(2, 0.2) }),
            KeyValuePair.Create(5, new Influence[] { new(3, 0.6), new(4, 0.4) }),
            KeyValuePair.Create(6, new Influence[] { new(0, 1) }),
        ]);
        var cube = TestMeshes.Cube().WithWeights(weights);
        var m = TestMeshes.Ok(Split(cube, new FaceCut(1, E(4, 5, 0.25), E(6, 7, 0.5))).Edit);

        // 8 at 0.25 from 4: 0.375, 0.225, 0.15, 0.15, 0.1 by bone; the smallest goes and the four left sum to 1.
        var at8 = m.Weights!.Of(8);
        Assert.Equal([0, 1, 2, 3], at8.Select(i => i.Bone));
        Assert.Equal(0.375 / 0.9, at8[0].Weight, 12);
        Assert.Equal(0.15 / 0.9, at8[3].Weight, 12);
        Assert.Equal(1.0, at8.Sum(i => i.Weight), 12);
        // 9 on 6-7 at 0.5: only 6 is weighted.
        Assert.Equal([new Influence(0, 1)], m.Weights.Of(9));
        Assert.Equal(weights.Of(4), m.Weights.Of(4));
        Assert.False(m.Weights.Has(0));
    }

    [Fact]
    public void ASegmentAlongAnEdgeOrToItselfSplitsNothing()
    {
        var r = Split(Square(), new FaceCut(0, V(0), V(1)), new FaceCut(0, V(2), E(2, 3, 0.5)), new FaceCut(0, V(3), V(3)));
        var m = TestMeshes.Ok(r.Edit);
        Assert.Empty(r.Edit.Split);
        Assert.Equal(new Face([0, 1, 2, 4, 3]), m.Faces[0]);
        Assert.Equal([(0, 1), (2, 4), (3, 3)], r.Segments);
    }

    [Fact]
    public void EachRefusalNamesTheCut()
    {
        var cube = TestMeshes.Cube();
        Assert.Equal("cuts: nothing to split: give at least one segment", Assert.Single(FaceSplit.Split(cube, []).Edit.Errors).ToString());
        Assert.Equal("cuts[0]: faces[9]: there is no face 9: ids run 0..5",
            Assert.Single(Split(cube, new FaceCut(9, V(0), V(1))).Edit.Errors).ToString());
        var holed = TestMeshes.Ok(MeshDelete.Delete(cube, null, [0]));
        Assert.Equal("cuts[0]: faces[0]: deleted", Assert.Single(Split(holed, new FaceCut(0, V(0), V(1))).Edit.Errors).ToString());
        Assert.Equal("cuts[0]: to: vertex 0 is not a corner of faces[1]",
            Assert.Single(Split(cube, new FaceCut(1, V(4), V(0))).Edit.Errors).ToString());
        Assert.Equal("cuts[0]: from: 4-6 is not an edge of faces[1]",
            Assert.Single(Split(cube, new FaceCut(1, E(4, 6, 0.5), V(5))).Edit.Errors).ToString());
        Assert.Equal("cuts[0]: from: the fraction 1.5 along edge 4-5 is not in 0..1",
            Assert.Single(Split(cube, new FaceCut(1, E(4, 5, 1.5), V(7))).Edit.Errors).ToString());
        Assert.Equal("cuts[0]: from: the fraction NaN along edge 4-5 is not in 0..1",
            Assert.Single(Split(cube, new FaceCut(1, E(4, 5, double.NaN), V(7))).Edit.Errors).ToString());
    }

    [Fact]
    public void ACutThatLeavesTheFaceInOnePieceOrLeavesItIsRefused()
    {
        // One bridge from the outer loop to the hole: the same piece on both sides.
        var bridge = Split(Plate(), new FaceCut(0, E(0, 1, 0.5), E(7, 4, 0.5)));
        Assert.Equal($"cuts[0]: faces[0]: {FaceSplit.OnePieceMessage}", Assert.Single(bridge.Edit.Errors).ToString());

        // The same bridge beside a cut that does divide the face: the bridge still has one piece on both sides.
        var both = Split(Plate(), new FaceCut(0, E(0, 1, 0.5), E(7, 4, 0.5)), new FaceCut(0, E(0, 1, 0.875), E(2, 3, 0.125)));
        Assert.Equal($"cuts[0]: faces[0]: {FaceSplit.OnePieceMessage}", Assert.Single(both.Edit.Errors).ToString());

        // An L whose notch the segment runs across: outside the face.
        var l = TestMeshes.Make(
            [new(0, 0, 0), new(2, 0, 0), new(2, 1, 0), new(1, 1, 0), new(1, 2, 0), new(0, 2, 0)], [[0, 1, 2, 3, 4, 5]]);
        Assert.Equal("cuts[0]: the segment 2-4 runs outside faces[0]",
            Assert.Single(Split(l, new FaceCut(0, V(2), V(4))).Edit.Errors).ToString());
        Assert.Equal("cuts[0]: the segment 6-5 crosses an edge of faces[0]",
            Assert.Single(Split(l, new FaceCut(0, E(2, 3, 0.5), V(5))).Edit.Errors).ToString());

        // Two segments crossing inside the face.
        var x = Split(Square(), new FaceCut(0, V(0), V(2)), new FaceCut(0, V(1), V(3)));
        Assert.Contains(x.Edit.Errors, e => e.ToString() == "cuts[0]: the segment 0-2 crosses another segment inside faces[0]");
        Assert.False(x.Edit.Succeeded);
    }

    [Fact]
    public void AFailedSplitLeavesTheMeshAndReportsNoSplit()
    {
        var cube = TestMeshes.Cube();
        var r = Split(cube, new FaceCut(1, V(4), V(0)));
        Assert.Null(r.Edit.Mesh);
        Assert.Empty(r.Edit.Split);
        Assert.Empty(r.Segments);
        Assert.Equal(TestMeshes.CubeFaces.Select(f => new Face(f)), cube.Faces);
        Assert.Empty(MeshEdits.Move(cube, [0], null, null, new Vector3(0, 0, 0.1f)).Split);
    }

    private static readonly IEqualityComparer<SplitFace> SplitEq = EqualityComparer<SplitFace>.Create(
        (a, b) => a!.Face == b!.Face && a.Pieces.SequenceEqual(b.Pieces), s => s.Face);
}
