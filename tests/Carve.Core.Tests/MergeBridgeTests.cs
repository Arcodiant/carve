using System.Numerics;
using Xunit;

namespace Carve.Core.Tests;

/// <summary>T-68's Core: <see cref="FaceMerge"/> and <see cref="Bridge"/>.</summary>
[Trait("Tier", "unit")]
public class MergeBridgeTests
{
    private const string Red = "#C8503C";
    private const string Blue = "#3C78C8";

    /// <summary>
    /// A flat grid of <paramref name="w"/> x <paramref name="h"/> unit quads in z = 0, facing +z: vertex (i, j) is
    /// <c>j * (w + 1) + i</c> and quad (i, j) is face <c>j * w + i</c>, wound [(i, j), (i+1, j), (i+1, j+1), (i, j+1)].
    /// <paramref name="stray"/> appends one vertex no face uses.
    /// </summary>
    private static PolygonMesh Grid(int w, int h, bool stray = false)
    {
        var verts = new List<Vector3>();
        for (var j = 0; j <= h; j++)
            for (var i = 0; i <= w; i++) verts.Add(new(i, j, 0));
        if (stray) verts.Add(new(9, 9, 9));
        var faces = new List<int[]>();
        for (var j = 0; j < h; j++)
            for (var i = 0; i < w; i++)
            {
                int v(int x, int y) => y * (w + 1) + x;
                faces.Add([v(i, j), v(i + 1, j), v(i + 1, j + 1), v(i, j + 1)]);
            }
        return TestMeshes.Make(verts, faces);
    }

    private static EditResult Merge(PolygonMesh mesh, params int[] faces) => FaceMerge.Merge(mesh, faces, out _);

    private static void Refused(EditResult r, string element, string phrase)
    {
        Assert.False(r.Succeeded);
        Assert.Contains(r.Errors, e => e.Element == element && e.Message.Contains(phrase, StringComparison.Ordinal));
    }

    // ---- merge_faces ----

    [Fact]
    public void TwoQuadsMergeIntoOneKeepingTheCollinearVertices()
    {
        var m = TestMeshes.Ok(Merge(Grid(2, 1), 1, 0));
        Assert.Equal(new Face([0, 1, 2, 5, 4, 3]), m.Faces[0]);
        Assert.True(m.IsFaceDeleted(1));
        Assert.Equal(1, m.LiveFaceCount);
        Assert.Equal(6, m.LiveVertexCount);
    }

    [Fact]
    public void ThreeInAnLMergeIntoTheLOutline()
    {
        // Quads (0,0), (1,0) and (0,1) of a 2 x 2 grid: the L's inner corner is vertex 4, still on the outline.
        var r = Merge(Grid(2, 2), 0, 1, 2);
        var m = TestMeshes.Ok(r);
        Assert.Equal(new Face([0, 1, 2, 5, 4, 7, 6, 3]), m.Faces[0]);
        Assert.Equal([1, 2], r.Deleted.Faces);
        Assert.Empty(r.Deleted.Vertices);
        Assert.Equal(3.0, m.FaceArea(0), 6);
    }

    [Fact]
    public void ARingOfFourRoundASquareGivesAHoledFace()
    {
        // A frame of four trapezoids round the square 4..7, which keeps its own face (4).
        var mesh = TestMeshes.Make(
            [new(-2, -2, 0), new(2, -2, 0), new(2, 2, 0), new(-2, 2, 0), new(-1, -1, 0), new(1, -1, 0), new(1, 1, 0), new(-1, 1, 0)],
            [[0, 1, 5, 4], [1, 2, 6, 5], [2, 3, 7, 6], [3, 0, 4, 7], [4, 5, 6, 7]]);
        var r = Merge(mesh, 3, 2, 1, 0);
        var m = TestMeshes.Ok(r);
        Assert.Equal(new Face([0, 1, 2, 3], [[4, 7, 6, 5]]), m.Faces[0]);
        Assert.Equal([1, 2, 3], r.Deleted.Faces);
        Assert.Empty(r.Deleted.Vertices);
        Assert.Equal(12.0, m.FaceArea(0), 6);
        // The hole's edges are shared with the square, each manifold.
        foreach (var (a, b) in new[] { (4, 5), (5, 6), (6, 7), (7, 4) }) Assert.True(m.Topology.IsManifoldEdge(a, b));
    }

    [Fact]
    public void TheLowestIdSurvivesAndOnlyTheMergedFacesOrphansGo()
    {
        var mesh = Grid(2, 2, stray: true); // vertex 9 is a stray no face used
        var r = Merge(mesh, 3, 1, 2, 0);
        var m = TestMeshes.Ok(r);
        Assert.Equal([1, 2, 3], r.Deleted.Faces);
        Assert.Equal([4], r.Deleted.Vertices); // the centre, used only by the merged faces
        Assert.Equal(new Face([0, 1, 2, 5, 8, 7, 6, 3]), m.Faces[0]);
        Assert.True(m.IsVertexDeleted(4));
        Assert.False(m.IsVertexDeleted(9));
        Assert.Equal(4, m.Faces.Count); // slots kept
        // A later edit appends after the last slot.
        var x = MeshEdits.Extrude(m, [0], 1);
        Assert.Equal(Enumerable.Range(4, 8), x.NewFaces);
        Assert.Equal(Enumerable.Range(10, 8), x.NewVertices);
    }

    [Fact]
    public void AMergeKeepsTheSurvivorsStyleAndWarnsOfAnotherColour()
    {
        var mesh = Grid(3, 1).WithStyle(FaceStyle.None.WithColour([0, 2], Red).WithColour([1], Blue).WithGroup("left", [0]).WithGroup("right", [2]));
        var r = FaceMerge.Merge(mesh, [0, 1, 2], out var warnings);
        var m = TestMeshes.Ok(r);
        Assert.Equal(Red, m.Style!.ColourOf(0));
        Assert.Equal([0], m.Style.FacesOf("left"));
        Assert.Null(m.Style.FacesOf("right")); // its only face went
        var warning = Assert.Single(warnings);
        Assert.StartsWith("faces[1]: ", warning, StringComparison.Ordinal);
        Assert.Contains(Blue, warning, StringComparison.Ordinal);

        // No colour is a colour of its own.
        FaceMerge.Merge(Grid(2, 1).WithStyle(FaceStyle.None.WithColour([1], Red)), [0, 1], out var none);
        Assert.StartsWith("faces[1]: ", Assert.Single(none), StringComparison.Ordinal);
        FaceMerge.Merge(Grid(2, 1).WithStyle(FaceStyle.None.WithColour([0, 1], Red)), [0, 1], out var same);
        Assert.Empty(same);
    }

    [Fact]
    public void AHardMarkOnADissolvedEdgeGoes()
    {
        var mesh = Grid(2, 1).WithShade(MeshShade.Of(ShadingMode.Smooth, [(1, 4), (0, 1)]));
        var m = TestMeshes.Ok(Merge(mesh, 0, 1));
        Assert.Equal([(0, 1)], m.Shade!.Hard);
    }

    [Fact]
    public void ANonPlanarMergeWarnsAsDefineMeshDoes()
    {
        var m = TestMeshes.Ok(Merge(TestMeshes.Cube(), 1, 2)); // +z and -y share edge 4-5
        Assert.Equal(new Face([4, 5, 6, 7, 0, 1]).Corners.Order(), m.Faces[1].Corners.Order());
        Assert.Contains(MeshReport.Create(m).Warnings, w => w.Kind == MeshWarningKind.NonPlanarFace && w.Element == "faces[1]");
    }

    [Fact]
    public void FewerThanTwoFacesAreRefused()
    {
        Refused(Merge(Grid(2, 1), 0), "faces", FaceMerge.TooFewMessage);
        Refused(Merge(Grid(2, 1)), "faces", FaceMerge.TooFewMessage);
    }

    [Fact]
    public void AnUnknownDeletedOrRepeatedFaceIsRefused()
    {
        Refused(Merge(Grid(2, 1), 0, 7), "faces[7]", "there is no face 7");
        Refused(Merge(Grid(2, 1), 0, 0), "faces[0]", "listed 1st too");
        var cut = TestMeshes.Ok(MeshDelete.Delete(Grid(3, 1), null, [1]));
        Refused(Merge(cut, 0, 1), "faces[1]", PolygonMesh.DeletedMessage);
    }

    [Fact]
    public void FacesNotConnectedByAnEdgeAreRefused()
    {
        Refused(Merge(Grid(2, 2), 0, 3), "faces[3]", "must be connected"); // they share vertex 4 only
        Refused(Merge(Grid(3, 1), 0, 2), "faces[2]", "must be connected");
    }

    [Fact]
    public void MixedWindingAcrossASharedEdgeIsRefused()
    {
        var mesh = TestMeshes.Make(Grid(2, 1).Vertices, [[0, 1, 4, 3], [4, 5, 2, 1]]);
        Refused(Merge(mesh, 0, 1), "edges[1-4]", "mixed winding");
    }

    [Fact]
    public void AResultThatIsNotOneFaceIsRefused()
    {
        // Quads (1,1), (1,0), (2,0), (3,0), (3,1), (3,2) and (2,2) of a 4 x 4 grid: (1,1) and (2,2) touch at vertex
        // (2,2), 12, only, with neither (2,1) nor (1,2) listed.
        Refused(Merge(Grid(4, 4), 5, 1, 2, 3, 7, 11, 10), "faces", "vertices[12] twice");
        Refused(Merge(TestMeshes.Cube(), TestMeshes.AllCubeFaces), "faces", "close a shell");
    }

    // ---- bridge ----

    /// <summary>Two unit squares 1 apart facing each other: face 0 at z = 0 facing +z, face 1 at z = 1 facing -z.</summary>
    private static PolygonMesh Squares(bool sameWay = false) => TestMeshes.Make(
        [new(0, 0, 0), new(1, 0, 0), new(1, 1, 0), new(0, 1, 0), new(0, 0, 1), new(1, 0, 1), new(1, 1, 1), new(0, 1, 1)],
        [[0, 1, 2, 3], sameWay ? [4, 5, 6, 7] : [4, 7, 6, 5]]);

    /// <summary>
    /// The holed slab (T-63's plate): 2 x 2, z from -0.1 to 0.1, a 1 x 1 square hole through front (face 0) and back
    /// (face 1), the four outer sides closed and the hole's inner faces left out. Front hole 4..7, back hole 12..15.
    /// </summary>
    private static PolygonMesh Slab()
    {
        var verts = new List<Vector3>();
        foreach (var z in new[] { 0.1f, -0.1f })
            foreach (var s in new[] { 1f, 0.5f })
                verts.AddRange([new(-s, -s, z), new(s, -s, z), new(s, s, z), new(-s, s, z)]);
        var faces = new List<Face>
        {
            new([0, 1, 2, 3], [[4, 7, 6, 5]]),
            new([8, 11, 10, 9], [[12, 13, 14, 15]]),
            new([1, 0, 8, 9]), new([2, 1, 9, 10]), new([3, 2, 10, 11]), new([0, 3, 11, 8]),
        };
        return PolygonMesh.Create(verts, faces).Mesh!;
    }

    private static void ClosedAndManifold(PolygonMesh m)
    {
        Assert.True(MeshReport.Create(m).Closed);
        Assert.All(m.Topology.Edges, e => Assert.True(e.IsManifold, e.Name));
    }

    [Fact]
    public void TwoSquaresBridgeIntoAClosedBoxWoundToAgree()
    {
        var mesh = Squares();
        var r = Bridge.Join(mesh, [0, 1, 2, 3], [4, 5, 6, 7]);
        var m = TestMeshes.Ok(r);
        Assert.Equal([2, 3, 4, 5], r.NewFaces);
        Assert.Empty(r.NewVertices);
        Assert.Equal(8, m.Vertices.Count);
        // Face 0 runs 0 -> 1, so quad 0 runs 1 -> 0; face 1 runs 5 -> 4, so the quad runs 4 -> 5.
        Assert.Equal(new Face([1, 0, 4, 5]), m.Faces[2]);
        Assert.Equal(new Face([2, 1, 5, 6]), m.Faces[3]);
        Assert.Equal(new Face([0, 3, 7, 4]), m.Faces[5]);
        ClosedAndManifold(m);
    }

    [Fact]
    public void ThePairingIsTheSameWhicheverWayTheSecondLoopIsListed()
    {
        var mesh = Squares();
        var expected = TestMeshes.Ok(Bridge.Join(mesh, [0, 1, 2, 3], [4, 5, 6, 7])).Faces;
        foreach (int[] second in new[] { new[] { 5, 6, 7, 4 }, [6, 7, 4, 5], [7, 6, 5, 4], [5, 4, 7, 6], [4, 7, 6, 5] })
            Assert.Equal(expected, TestMeshes.Ok(Bridge.Join(mesh, [0, 1, 2, 3], second)).Faces);
        Assert.Equal(new Bridge.Pairing(1, -1), Bridge.Pair(mesh.Vertices, [0, 1, 2, 3], [5, 4, 7, 6]));
    }

    [Fact]
    public void APairingTieGoesToTheFirstInTheStatedOrder()
    {
        // Second loop straight over the centre of the first: every rotation, either way, is the same length.
        var verts = new Vector3[] { new(-1, -1, 0), new(1, -1, 0), new(1, 1, 0), new(-1, 1, 0), new(0, 0, 1), new(0, 0, 1), new(0, 0, 1), new(0, 0, 1) };
        Assert.Equal(new Bridge.Pairing(0, 1), Bridge.Pair(verts, [0, 1, 2, 3], [4, 5, 6, 7]));
    }

    [Fact]
    public void TheSlabsTwoHolesBridgeIntoAClosedTube()
    {
        var r = Bridge.Join(Slab(), [4, 5, 6, 7], [12, 13, 14, 15]);
        var m = TestMeshes.Ok(r);
        Assert.Equal([6, 7, 8, 9], r.NewFaces);
        Assert.Equal(new Face([4, 5, 13, 12]), m.Faces[6]);
        // The tube's walls face into the hole: the first, at y = -0.5, faces +y.
        Assert.Equal(Vector3.UnitY, m.FaceNormal(6));
        ClosedAndManifold(m);
    }

    [Fact]
    public void ThePlatesHolesListedAnyWayGiveTheSameClosedTube()
    {
        var expected = TestMeshes.Ok(Bridge.Join(Slab(), [4, 5, 6, 7], [12, 13, 14, 15])).Faces;
        var m = TestMeshes.Ok(Bridge.Join(Slab(), [4, 5, 6, 7], [14, 13, 12, 15]));
        Assert.Equal(expected, m.Faces);
        ClosedAndManifold(m);
    }

    [Fact]
    public void NewFacesTakeTheStyleOfTheFirstLoopsFaceAndNoWeights()
    {
        var mesh = Squares().WithStyle(FaceStyle.None.WithColour([0], Red).WithColour([1], Blue).WithGroup("base", [0]));
        var m = TestMeshes.Ok(Bridge.Join(mesh, [0, 1, 2, 3], [4, 5, 6, 7]));
        foreach (var f in new[] { 2, 3, 4, 5 }) Assert.Equal(Red, m.Style!.ColourOf(f));
        Assert.Equal([0, 2, 3, 4, 5], m.Style!.FacesOf("base"));
        var other = TestMeshes.Ok(Bridge.Join(mesh, [4, 5, 6, 7], [0, 1, 2, 3]));
        foreach (var f in new[] { 2, 3, 4, 5 }) Assert.Equal(Blue, other.Style!.ColourOf(f));
    }

    [Fact]
    public void ALoopThatIsNotACycleOfOpenEdgesIsRefused()
    {
        Refused(Bridge.Join(Squares(), [0, 1, 3, 2], [4, 5, 6, 7]), "loops[0][1]", "not joined by an edge");
        Refused(Bridge.Join(Squares(), [0, 1], [4, 5]), "loops[0]", "at least 3");
        Refused(Bridge.Join(Squares(), [0, 1, 2, 0], [4, 5, 6, 7]), "loops[0][3]", "repeats loops[0][0]");
        Refused(Bridge.Join(Squares(), [0, 1, 2, 9], [4, 5, 6, 7]), "loops[0][3]", "vertex 9");
        Refused(Bridge.Join(TestMeshes.Cube(), [4, 5, 6, 7], [0, 1, 2, 3]), "loops[1][0]", "not open");
    }

    [Fact]
    public void LoopsOfUnequalCountAreRefused()
    {
        var mesh = TestMeshes.Make(
            [new(0, 0, 0), new(1, 0, 0), new(1, 1, 0), new(0, 1, 0), new(0, 0, 1), new(0, 1, 1), new(1, 0, 1)],
            [[0, 1, 2, 3], [4, 5, 6]]);
        Refused(Bridge.Join(mesh, [0, 1, 2, 3], [4, 5, 6]), "loops", "4 and 3");
    }

    [Fact]
    public void AVertexInBothLoopsIsRefused()
    {
        Refused(Bridge.Join(Squares(), [0, 1, 2, 3], [0, 1, 2, 3]), "vertices[2]", "in both loops");
    }

    [Fact]
    public void LoopsWhoseFacesWindAgainstEveryQuadAreRefused()
    {
        Refused(Bridge.Join(Squares(sameWay: true), [0, 1, 2, 3], [4, 5, 6, 7]), "edges[4-5]", "no quad between them agrees with both");

        // A first loop round two faces wound opposite ways.
        var verts = Grid(2, 1).Vertices.Concat([new(0, 0, 1), new(1, 0, 1), new(2, 0, 1), new(2, 1, 1), new(1, 1, 1), new(0, 1, 1)]).ToArray();
        var mesh = TestMeshes.Make(verts, [[0, 1, 4, 3], [4, 5, 2, 1], [6, 11, 10, 9, 8, 7]]);
        Refused(Bridge.Join(mesh, [0, 1, 2, 5, 4, 3], [6, 7, 8, 9, 10, 11]), "loops[0][1]", "wind both ways");
    }
}
