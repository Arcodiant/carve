using System.Numerics;
using Xunit;

namespace Carve.Core.Tests;

/// <summary>T-67's <see cref="Knife"/>, and the interior points it added to <see cref="FaceSplit"/>.</summary>
[Trait("Tier", "unit")]
public class KnifeTests
{
    /// <summary>The cube's tolerance: 1e-4 of its bounds diagonal, sqrt 3.</summary>
    private static readonly float Tol = (float)(1e-4 * Math.Sqrt(3));

    private static Vector3 V(float x, float y, float z) => new(x, y, z);

    private static void IsClosed(PolygonMesh m) =>
        Assert.All(m.Topology.Edges, e => Assert.True(e.IsManifold, e.Name));

    private static (PolygonMesh Mesh, KnifeResult Result) Cut(PolygonMesh mesh, bool closed, params Vector3[] points)
    {
        var r = Knife.Cut(mesh, points, closed);
        return (TestMeshes.Ok(r.Edit), r);
    }

    private static string Refusal(PolygonMesh mesh, bool closed, params Vector3[] points)
    {
        var r = Knife.Cut(mesh, points, closed);
        Assert.False(r.Edit.Succeeded);
        Assert.Empty(r.Edges);
        return string.Join("; ", r.Edit.Errors.Select(e => $"{e.Element}: {e.Message}"));
    }

    /// <summary>The front face's closed square, (0.25..0.75)^2 at z = 1.</summary>
    private static readonly Vector3[] Square = [V(0.25f, 0.25f, 1), V(0.75f, 0.25f, 1), V(0.75f, 0.75f, 1), V(0.25f, 0.75f, 1)];

    [Fact]
    public void AnOpenPathVertexToVertexSplitsTheFace()
    {
        var (m, r) = Cut(TestMeshes.Cube(), false, V(0, 0, 1), V(1, 1, 1));
        Assert.Empty(r.Edit.NewVertices);
        Assert.Equal([(4, 6)], r.Edges);
        var s = Assert.Single(r.Edit.Split);
        Assert.Equal(1, s.Face);
        Assert.Equal([1, 6], s.Pieces);
        Assert.Equal([4, 5, 6], m.Faces[1].Loop);
        Assert.Equal([4, 6, 7], m.Faces[6].Loop);
        IsClosed(m);
    }

    [Fact]
    public void AnOpenPathEdgeToEdgeAddsAVertexOnEachEdgeSharedByTheNeighbours()
    {
        var (m, r) = Cut(TestMeshes.Cube(), false, V(0.5f, 0, 1), V(0.5f, 1, 1));
        Assert.Equal([8, 9], r.Edit.NewVertices);
        Assert.Equal([(8, 9)], r.Edges);
        Assert.Equal(V(0.5f, 0, 1), m.Vertices[8]);
        Assert.Equal(V(0.5f, 1, 1), m.Vertices[9]);
        Assert.Contains(8, m.Faces[2].Loop);
        Assert.Contains(9, m.Faces[3].Loop);
        Assert.Equal([1, 6], Assert.Single(r.Edit.Split).Pieces);
        IsClosed(m);
    }

    [Fact]
    public void InteriorPointsBecomeNewVerticesInsideTheFace()
    {
        var (m, r) = Cut(TestMeshes.Cube(), false, V(0.5f, 0, 1), V(0.4f, 0.5f, 1), V(0.5f, 1, 1));
        Assert.Equal([8, 9, 10], r.Edit.NewVertices);
        Assert.Equal(V(0.4f, 0.5f, 1), m.Vertices[10]);
        Assert.Equal([(8, 10), (10, 9)], r.Edges);
        Assert.Equal([4, 8, 10, 9, 7], m.Faces[1].Loop);
        Assert.Equal([8, 5, 6, 9, 10], m.Faces[6].Loop);
        Assert.Equal(1, m.FaceArea(1) + m.FaceArea(6), 5);
        IsClosed(m);
    }

    [Fact]
    public void AClosedPathInsideAFaceCutsOutAnInnerFaceAndLeavesAHoleWoundOpposite()
    {
        var (m, r) = Cut(TestMeshes.Cube(), true, Square);
        Assert.Equal([8, 9, 10, 11], r.Edit.NewVertices);
        Assert.Equal([6], r.Edit.NewFaces);
        Assert.Equal([(8, 9), (9, 10), (10, 11), (11, 8)], r.Edges);
        var s = Assert.Single(r.Edit.Split);
        Assert.Equal(1, s.Face);
        Assert.Equal([1, 6], s.Pieces);
        Assert.Equal([4, 5, 6, 7], m.Faces[1].Loop);
        Assert.Equal([8, 11, 10, 9], Assert.Single(m.Faces[1].Holes));
        Assert.Equal([8, 9, 10, 11], m.Faces[6].Loop);
        Assert.Empty(m.Faces[6].Holes);
        Assert.Equal(0.75, m.FaceArea(1), 5);
        Assert.Equal(0.25, m.FaceArea(6), 5);
        Assert.Equal(m.FaceNormal(1), m.FaceNormal(6));
        IsClosed(m);
    }

    [Fact]
    public void AClosedPathTouchingAnEdgeSplitsTheFaceAsUsual()
    {
        // Two corners on the bottom edge (the segment between them runs along it), the third inside.
        var (m, r) = Cut(TestMeshes.Cube(), true, V(0.25f, 0, 1), V(0.75f, 0, 1), V(0.5f, 0.5f, 1));
        Assert.Equal([8, 9, 10], r.Edit.NewVertices);
        Assert.Equal([1, 6], Assert.Single(r.Edit.Split).Pieces);
        Assert.Empty(m.Faces[1].Holes);
        Assert.Empty(m.Faces[6].Holes);
        Assert.Equal([8, 9, 10], m.Faces[6].Loop);
        Assert.Equal(0.125, m.FaceArea(6), 5);
        IsClosed(m);
    }

    [Fact]
    public void AClosedPathGrazingACornerBetweenItsPointsTouchesTheBoundaryThere()
    {
        // RV-32, the person's T-67 ruling: the corner a segment is split at counts as a touch. Every listed point is inside
        // the notched face; the first segment passes through reflex vertex 4. Once it reached the mesh's own refusal.
        var notch = TestMeshes.Make(
            [new(0, 0, 0), new(4, 0, 0), new(4, 4, 0), new(3, 4, 0), new(2.5f, 2.5f, 0), new(2, 4, 0), new(0, 4, 0)],
            [[0, 1, 2, 3, 4, 5, 6]]);
        Assert.Equal(
            "points[0], points[1]: the closed path touches the face's boundary at one point only, at [2.5, 2.5, 0]; touch it at two points to split the face, or keep it clear of the edge to cut a hole",
            Refusal(notch, true, V(1, 2.5f, 0), V(3.5f, 2.5f, 0), V(2.5f, 1, 0)));
    }

    [Fact]
    public void AClosedPathTouchingTheBoundaryAtOnePointOnlyIsRefused()
    {
        // The person's ruling (2026-11-12): the face around it would pass through that point twice.
        var cube = TestMeshes.Cube();
        Assert.Equal(
            "points[2]: the closed path touches the face's boundary at one point only, at [0.5, 0, 1]; touch it at two points to split the face, or keep it clear of the edge to cut a hole",
            Refusal(cube, true, V(0.75f, 0.5f, 1), V(0.25f, 0.5f, 1), V(0.5f, 0, 1)));
        Assert.Equal(
            "points[0]: the closed path touches the face's boundary at one point only, at [0, 0, 1]; touch it at two points to split the face, or keep it clear of the edge to cut a hole",
            Refusal(cube, true, V(0, 0, 1), V(0.5f, 0.25f, 1), V(0.25f, 0.5f, 1)));
    }

    [Fact]
    public void AClosedPathInsideAFaceLeavesTheHoleInTheFaceWhicheverCornerItStartsAt()
    {
        // The far side of the inner face is a hole in the face around it, never in the inner face itself, whichever
        // corner the path starts at (and so whichever vertex the traced hole starts at).
        for (var start = 0; start < Square.Length; start++)
        {
            var path = Square.Skip(start).Concat(Square.Take(start)).ToArray();
            var (m, r) = Cut(TestMeshes.Cube(), true, path);
            Assert.Equal([1, 6], Assert.Single(r.Edit.Split).Pieces);
            var hole = Assert.Single(m.Faces[1].Holes);
            Assert.Empty(m.Faces[6].Holes);
            Assert.Equal([8, 9, 10, 11], hole.Order());
            Assert.Equal([8, 9, 10, 11], m.Faces[6].Loop.Order());
            Assert.Equal(0.75, m.FaceArea(1), 5);
            Assert.Equal(0.25, m.FaceArea(6), 5);
            IsClosed(m);
        }
    }

    [Fact]
    public void APathCrossesTwoFacesByAPointOnTheirSharedEdge()
    {
        var (m, r) = Cut(TestMeshes.Cube(), false, V(0, 0.5f, 1), V(1, 0.5f, 1), V(1, 0.5f, 0));
        Assert.Equal([1, 5], r.Edit.Split.Select(s => s.Face));
        Assert.Equal(3, r.Edit.NewVertices.Count);
        Assert.Equal(r.Edges[0].To, r.Edges[1].From);
        IsClosed(m);
    }

    [Fact]
    public void APointWithinTheToleranceOfAVertexIsThatVertexAndJustBeyondItIsOnTheEdge()
    {
        var (_, inside) = Cut(TestMeshes.Cube(), false, V(0.9f * Tol, 0, 1), V(1, 1, 1));
        Assert.Empty(inside.Edit.NewVertices);
        Assert.Equal([(4, 6)], inside.Edges);

        var (m, beyond) = Cut(TestMeshes.Cube(), false, V(1.1f * Tol, 0, 1), V(1, 1, 1));
        Assert.Equal([8], beyond.Edit.NewVertices);
        Assert.Contains(8, m.Faces[2].Loop);
    }

    [Fact]
    public void APointWithinTheToleranceOfAnEdgeIsOnItAndJustBeyondItIsInsideTheFace()
    {
        var (m, inside) = Cut(TestMeshes.Cube(), false, V(0.5f, 0.9f * Tol, 1), V(0.5f, 1, 1));
        Assert.Contains(8, m.Faces[2].Loop); // on edge 4-5, so the -y face takes it
        Assert.Equal(0, m.Vertices[8].Y);
        Assert.Equal([1, 6], Assert.Single(inside.Edit.Split).Pieces);

        Assert.StartsWith("points[0]: [0.5, 0.00019", Refusal(TestMeshes.Cube(), false, V(0.5f, 1.1f * Tol, 1), V(0.5f, 1, 1)));
        Assert.EndsWith("is inside faces[1], not on an edge or a vertex, and an open path must end on one: a cut that stops inside a face is not a face",
            Refusal(TestMeshes.Cube(), false, V(0.5f, 1.1f * Tol, 1), V(0.5f, 1, 1)));
    }

    [Fact]
    public void APointWithinTheToleranceOfTheFacesPlaneIsInsideItProjectedOntoThePlaneAndBeyondItIsOff()
    {
        var (m, _) = Cut(TestMeshes.Cube(), true, V(0.25f, 0.25f, 1 + 0.9f * Tol), Square[1], Square[2], Square[3]);
        Assert.Equal(1f, m.Vertices[8].Z);

        var text = Refusal(TestMeshes.Cube(), true, V(0.25f, 0.25f, 1 + 1.1f * Tol), Square[1], Square[2], Square[3]);
        Assert.StartsWith("points[0]: [0.25, 0.25, 1.00019] is not on the surface: it is 0.00019", text);
        Assert.EndsWith("the nearest surface point is [0.25, 0.25, 1], on faces[1]", text);
    }

    [Fact]
    public void AnOffSurfacePointIsRefusedWithItsDistanceAndTheNearestSurfacePoint()
    {
        Assert.Equal(
            "points[1]: [0.5, 0.5, 1.25] is not on the surface: it is 0.25 from it, more than the tolerance 0.000173205; the nearest surface point is [0.5, 0.5, 1], on faces[1]",
            Refusal(TestMeshes.Cube(), false, V(0.5f, 0, 1), V(0.5f, 0.5f, 1.25f), V(0.5f, 1, 1)));
        Assert.Equal(
            "points[0]: [1.5, 0.5, 1.5] is not on the surface: it is 0.707107 from it, more than the tolerance 0.000173205; the nearest surface point is [1, 0.5, 1], on faces[1]",
            Refusal(TestMeshes.Cube(), false, V(1.5f, 0.5f, 1.5f), V(0.5f, 1, 1)));
    }

    [Fact]
    public void EveryOtherRefusalNamesItsPoints()
    {
        var cube = TestMeshes.Cube();
        Assert.Equal("points: an open path needs at least 2 points, got 1", Refusal(cube, false, V(0, 0, 1)));
        Assert.Equal("points: a closed path needs at least 3 points, got 2", Refusal(cube, true, V(0, 0, 1), V(1, 1, 1)));
        Assert.Equal("points[1]: must be a finite point", Refusal(cube, false, V(0, 0, 1), V(float.NaN, 0, 1)));
        Assert.Equal("points[0], points[1]: points[0] and points[1] share no face: a segment runs across one face",
            Refusal(cube, false, V(0, 0, 1), V(1, 1, 0)));
        Assert.Equal(
            "points[1]: [0.5, 0.5, 1] is inside faces[1], not on an edge or a vertex, and an open path must end on one: a cut that stops inside a face is not a face",
            Refusal(cube, false, V(0.5f, 0, 1), V(0.5f, 0.5f, 1)));
    }

    [Fact]
    public void ASegmentLeavingItsFaceIsRefusedNamingBothPoints()
    {
        // The L (0,0) (2,0) (2,1) (1,1) (1,2) (0,2) at z = 0 and 1: corners 4 (1,2) and 2 (2,1) share only the L face,
        // and the segment between them runs outside the notch.
        (float X, float Y)[] l = [(0, 0), (2, 0), (2, 1), (1, 1), (1, 2), (0, 2)];
        var v = l.Select(p => V(p.X, p.Y, 0)).Concat(l.Select(p => V(p.X, p.Y, 1))).ToArray();
        var faces = new List<int[]> { new[] { 0, 5, 4, 3, 2, 1 }, new[] { 6, 7, 8, 9, 10, 11 } };
        for (var i = 0; i < 6; i++) faces.Add([i, (i + 1) % 6, (i + 1) % 6 + 6, i + 6]);
        Assert.Equal("points[0], points[1]: the segment from points[0] to points[1] leaves faces[0]",
            Refusal(TestMeshes.Make(v, faces), false, V(1, 2, 0), V(2, 1, 0)));
    }

    [Fact]
    public void ASegmentThroughAnExistingHoleIsRefusedNamingIt()
    {
        var (holed, _) = Cut(TestMeshes.Cube(), true, Square);
        Assert.Equal("points[0], points[1]: the segment from points[0] to points[1] passes through hole 0 of faces[1]",
            Refusal(holed, false, V(0.5f, 0, 1), V(0.5f, 1, 1)));
    }

    [Fact]
    public void APointInsideTwoOverlappingFacesIsRefusedNamingBoth()
    {
        var m = TestMeshes.Make(
            [V(0, 0, 0), V(2, 0, 0), V(2, 2, 0), V(0, 2, 0), V(1, 1, 0), V(3, 1, 0), V(3, 3, 0), V(1, 3, 0)],
            [[0, 1, 2, 3], [4, 5, 6, 7]]);
        Assert.Equal("points[1]: [1.5, 1.5, 0] lies inside faces[0] and faces[1], which overlap there",
            Refusal(m, false, V(1, 0, 0), V(1.5f, 1.5f, 0), V(1, 2, 0)));
    }

    [Fact]
    public void TheSameCutAlwaysGivesTheSameIds()
    {
        var (a, ra) = Cut(TestMeshes.Cube(), true, Square);
        var (b, rb) = Cut(TestMeshes.Cube(), true, Square);
        Assert.Equal(a.Vertices, b.Vertices);
        Assert.Equal(a.Faces.Select(f => string.Join(",", f.Corners)), b.Faces.Select(f => string.Join(",", f.Corners)));
        Assert.Equal(ra.Edges, rb.Edges);
    }

    [Fact]
    public void AnInteriorVertexBlendsItsTrianglesCornerWeightsBarycentricallyCappedAtFour()
    {
        // A tetrahedron; its base (z = 0, faces[0]) is the one triangle 0 2 1.
        var tet = TestMeshes.Make([V(0, 0, 0), V(1, 0, 0), V(0, 1, 0), V(0, 0, 1)],
            [[0, 2, 1], [0, 1, 3], [0, 3, 2], [1, 2, 3]]);
        var weights = VertexWeights.Of(
        [
            KeyValuePair.Create(0, new Influence[] { new(0, 1) }),
            KeyValuePair.Create(1, new Influence[] { new(1, 1) }),
            KeyValuePair.Create(2, new Influence[] { new(2, 0.5), new(3, 0.3), new(4, 0.2) }),
        ]);
        var (m, r) = Cut(tet.WithWeights(weights), true, V(0.2f, 0.2f, 0), V(0.2f, 0.5f, 0), V(0.5f, 0.2f, 0));
        Assert.Equal([4, 5, 6], r.Edit.NewVertices);

        // Vertex 6 at (0.5, 0.2): 0.3 of vertex 0, 0.5 of vertex 1, 0.2 of vertex 2, so bones 0.3 0.5 0.1 0.06 0.04,
        // the least (bone 4) dropped by the cap and the rest renormalised by 0.96.
        var at6 = m.Weights!.Of(6).ToDictionary(i => i.Bone, i => i.Weight);
        Assert.Equal([0, 1, 2, 3], at6.Keys.Order());
        Assert.Equal(0.3 / 0.96, at6[0], 5);
        Assert.Equal(0.5 / 0.96, at6[1], 5);
        Assert.Equal(0.1 / 0.96, at6[2], 5);
        Assert.Equal(0.06 / 0.96, at6[3], 5);

        // Vertex 4 at (0.2, 0.2): 0.6, 0.2, 0.2 blends five bones, capped at four and renormalised.
        var at4 = m.Weights.Of(4).ToArray();
        Assert.Equal(4, at4.Length);
        Assert.Equal(1, at4.Sum(i => i.Weight), 5);
        Assert.DoesNotContain(at4, i => i.Bone == 4);
        Assert.Equal(weights.Of(1), m.Weights.Of(1));
        Assert.False(m.Weights.Has(3));
    }

    [Fact]
    public void FaceSplitRefusesAnInteriorPointOutsideItsFace()
    {
        var r = FaceSplit.Split(TestMeshes.Cube(),
            [new FaceCut(1, CutPoint.AtVertex(4), CutPoint.Interior(V(0.5f, 0.5f, 0.5f)))]);
        Assert.Equal("cuts[0]: to: [0.5, 0.5, 0.5] is not inside faces[1], clear of its edges",
            string.Join("; ", r.Edit.Errors.Select(e => $"{e.Element}: {e.Message}")));
    }

    // RV-31 (ruled 2026-11-13): a segment that meets the face's boundary between its ends is cut as plane_cut cuts.
    private static PolygonMesh Flat(params (float X, float Y)[] corners) =>
        TestMeshes.Make(corners.Select(c => V(c.X, c.Y, 0)), [Enumerable.Range(0, corners.Length).ToArray()]);

    private static double Area(PolygonMesh m, Face f)
    {
        double Ring(IReadOnlyList<int> loop) => Enumerable.Range(0, loop.Count).Sum(i =>
        {
            var (a, b) = (m.Vertices[loop[i]], m.Vertices[loop[(i + 1) % loop.Count]]);
            return (double)a.X * b.Y - (double)b.X * a.Y;
        }) / 2;
        return Ring(f.Loop) - f.Holes.Sum(h => -Ring(h));
    }

    public static TheoryData<string, float[], float, float, float, int> BoundaryMeetings => new()
    {
        // The U from (0,1) to (3,1): through its two inner corners, along the edge between them.
        { "U", [0, 0, 3, 0, 3, 3, 2, 3, 2, 1, 1, 1, 1, 3, 0, 3], 1, 0, 3, 3 },
        // Grazing reflex vertex 4.
        { "notch", [0, 0, 4, 0, 4, 4, 3, 4, 2.5f, 2.5f, 2, 4, 0, 4], 2.5f, 0, 4, 3 },
        // The W's top: through both its valleys.
        { "W", [0, 0, 4, 0, 4, 4, 3, 2, 2, 4, 1, 2, 0, 4], 2, 0, 4, 4 },
        // An off-centre notch: along edge 4-5 between two parts across.
        { "off-centre", [0, 0, 3, 0, 3, 3, 1, 3, 1, 1, 0.5f, 1, 0.5f, 3, 0, 3], 1, 0, 3, 3 },
    };

    [Theory]
    [MemberData(nameof(BoundaryMeetings))]
    public void ASegmentMeetingTheBoundaryBetweenItsEndsIsCutAsPlaneCutCutsIt(string name, float[] xy, float y, float x0, float x1, int pieces)
    {
        var mesh = Flat(Enumerable.Range(0, xy.Length / 2).Select(i => (xy[2 * i], xy[2 * i + 1])).ToArray());
        var (m, r) = Cut(mesh, false, V(x0, y, 0), V(x1, y, 0));
        var plane = TestMeshes.Ok(PlaneCut.Cut(mesh, V(0, y, 0), V(0, 1, 0)).Edit);

        var s = Assert.Single(r.Edit.Split);
        Assert.Equal(pieces, s.Pieces.Count);
        Assert.Equal(pieces, m.LiveFaceCount);
        var areas = m.LiveFaces.Select(f => Math.Round(Area(m, m.Faces[f]), 4)).Order().ToArray();
        var expected = plane.LiveFaces.Select(f => Math.Round(Area(plane, plane.Faces[f]), 4)).Order().ToArray();
        Assert.True(expected.SequenceEqual(areas), $"{name}: {string.Join(", ", areas)} against plane_cut's {string.Join(", ", expected)}");
        Assert.All(m.LiveFaces, f => Assert.True(Area(m, m.Faces[f]) > 0, $"{name}: faces[{f}]"));
    }

    [Fact]
    public void ASegmentThatLeavesTheFaceThroughAVertexIsRefusedNamingWhere()
    {
        var u = Flat((0, 0), (3, 0), (3, 3), (2, 3), (2, 1), (1, 1), (1, 3), (0, 3));
        Assert.Equal(
            "points[0], points[1]: the segment from points[0] to points[1] leaves faces[0] at [1, 3, 0]",
            Refusal(u, false, V(0, 3, 0), V(3, 3, 0)));
    }

    /// <summary>Seeded random rotations (RV-33: an exact sign on rotated input is rounding noise).</summary>
    private static IEnumerable<Quaternion> Rotations(int seed, int count)
    {
        var rng = new Random(seed);
        for (var i = 0; i < count; i++)
            yield return Quaternion.Normalize(new Quaternion((float)(rng.NextDouble() * 2 - 1), (float)(rng.NextDouble() * 2 - 1),
                (float)(rng.NextDouble() * 2 - 1), (float)(rng.NextDouble() * 2 - 1)));
    }

    [Theory]
    [MemberData(nameof(BoundaryMeetings))]
    public void ASegmentMeetingTheBoundaryBetweenItsEndsIsCutTheSameWayRotated(string name, float[] xy, float y, float x0, float x1, int pieces)
    {
        var failed = new List<string>();
        foreach (var q in Rotations(3302, 100))
        {
            var mesh = TestMeshes.Make(Enumerable.Range(0, xy.Length / 2).Select(i => Vector3.Transform(V(xy[2 * i], xy[2 * i + 1], 0), q)),
                [Enumerable.Range(0, xy.Length / 2).ToArray()]);
            var r = Knife.Cut(mesh, [Vector3.Transform(V(x0, y, 0), q), Vector3.Transform(V(x1, y, 0), q)]);
            if (!r.Edit.Succeeded) failed.Add($"{q}: {string.Join("; ", r.Edit.Errors)}");
            else if (r.Edit.Split.Single().Pieces.Count != pieces) failed.Add($"{q}: {r.Edit.Split.Single().Pieces.Count} pieces");
        }
        Assert.True(failed.Count == 0, $"{name}: {failed.Count} of 100 differ, first: {failed.FirstOrDefault()}");
    }

    [Fact]
    public void AClosedPathGrazingACornerIsRefusedTheSameWayRotated()
    {
        var failed = new List<string>();
        foreach (var q in Rotations(3303, 100))
        {
            var notch = TestMeshes.Make(
                new[] { V(0, 0, 0), V(4, 0, 0), V(4, 4, 0), V(3, 4, 0), V(2.5f, 2.5f, 0), V(2, 4, 0), V(0, 4, 0) }.Select(p => Vector3.Transform(p, q)),
                [[0, 1, 2, 3, 4, 5, 6]]);
            var r = Knife.Cut(notch, new[] { V(1, 2.5f, 0), V(3.5f, 2.5f, 0), V(2.5f, 1, 0) }.Select(p => Vector3.Transform(p, q)).ToList(), true);
            var message = string.Join("; ", r.Edit.Errors.Select(e => $"{e.Element}: {e.Message}"));
            if (!message.StartsWith("points[0], points[1]: the closed path touches the face's boundary at one point only", StringComparison.Ordinal))
                failed.Add($"{q}: {(r.Edit.Succeeded ? "succeeded" : message)}");
        }
        Assert.True(failed.Count == 0, $"{failed.Count} of 100 differ, first: {failed.FirstOrDefault()}");
    }
}
