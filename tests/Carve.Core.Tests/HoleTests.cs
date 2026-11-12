using System.Numerics;
using Xunit;

namespace Carve.Core.Tests;

/// <summary>Faces with holes (T-63): validation, triangulation, topology, health, edits and the file.</summary>
[Trait("Tier", "unit")]
public class HoleTests
{
    // A 2 x 2 plate in the XY plane facing +Z, with a 1 x 1 square hole (wound clockwise seen from +Z).
    private static readonly Vector3[] Plate =
    [
        new(-1, -1, 0), new(1, -1, 0), new(1, 1, 0), new(-1, 1, 0),
        new(-0.5f, -0.5f, 0), new(-0.5f, 0.5f, 0), new(0.5f, 0.5f, 0), new(0.5f, -0.5f, 0),
    ];

    private static readonly int[] Outer = [0, 1, 2, 3];
    private static readonly int[] Hole = [4, 5, 6, 7];

    private static PolygonMesh Make(Vector3[] v, params Face[] faces)
    {
        var r = PolygonMesh.Create(v, faces);
        Assert.True(r.Succeeded, string.Join("; ", r.Errors));
        return r.Mesh!;
    }

    private static IReadOnlyList<MeshError> Errors(Vector3[] v, params Face[] faces)
    {
        var r = PolygonMesh.Create(v, faces);
        Assert.False(r.Succeeded);
        return r.Errors;
    }

    private static Face Holed(int[] loop, params int[][] holes) => new(loop, holes);

    // ---- validation -------------------------------------------------------------------------------------------

    [Fact]
    public void ThePlateWithAHoleIsCreated()
    {
        var m = Make(Plate, Holed(Outer, Hole));
        Assert.True(m.Faces[0].HasHoles);
        Assert.Equal(3.0, m.FaceArea(0), 5);
    }

    [Fact]
    public void AHoleOfTwoVerticesIsAnError()
        => Assert.Contains(Errors(Plate, Holed(Outer, [4, 5])), e => e.Element == "faces[0].holes[0]");

    [Fact]
    public void AHoleWoundLikeTheOuterLoopIsAnError()
        => Assert.Contains(Errors(Plate, Holed(Outer, [4, 7, 6, 5])),
            e => e.Element == "faces[0].holes[0]" && e.Message.Contains("clockwise"));

    [Fact]
    public void AHoleOutsideTheOuterLoopIsAnError()
    {
        var v = Plate.ToArray();
        for (var i = 4; i < 8; i++) v[i] += new Vector3(3, 0, 0);
        Assert.Contains(Errors(v, Holed(Outer, Hole)),
            e => e.Element == "faces[0].holes[0]" && e.Message.Contains("strictly inside"));
    }

    [Fact]
    public void AHoleCrossingTheOuterLoopIsAnError()
    {
        var v = Plate.ToArray();
        for (var i = 4; i < 8; i++) v[i] += new Vector3(0.8f, 0, 0);
        Assert.Contains(Errors(v, Holed(Outer, Hole)),
            e => e.Element == "faces[0].holes[0]" && e.Message.Contains("strictly inside"));
    }

    [Fact]
    public void AHoleTouchingTheOuterLoopIsAnError()
    {
        var v = Plate.ToArray();
        for (var i = 4; i < 8; i++) v[i] += new Vector3(0.5f, 0, 0); // its right side lies on x = 1
        Assert.Contains(Errors(v, Holed(Outer, Hole)),
            e => e.Element == "faces[0].holes[0]" && e.Message.Contains("strictly inside"));
    }

    private static Vector3[] TwoHoles(float gap)
    {
        // Two 0.4-square holes side by side, gap apart in x.
        var l = -gap / 2 - 0.4f;
        var r = gap / 2;
        return
        [
            new(-1, -1, 0), new(1, -1, 0), new(1, 1, 0), new(-1, 1, 0),
            new(l, -0.2f, 0), new(l, 0.2f, 0), new(l + 0.4f, 0.2f, 0), new(l + 0.4f, -0.2f, 0),
            new(r, -0.2f, 0), new(r, 0.2f, 0), new(r + 0.4f, 0.2f, 0), new(r + 0.4f, -0.2f, 0),
        ];
    }

    [Fact]
    public void TwoHolesApartAreCreated()
        => Assert.Equal(4 - 2 * 0.16, Make(TwoHoles(0.2f), Holed(Outer, [4, 5, 6, 7], [8, 9, 10, 11])).FaceArea(0), 4);

    [Fact]
    public void TwoOverlappingHolesAreAnError()
        => Assert.Contains(Errors(TwoHoles(-0.2f), Holed(Outer, [4, 5, 6, 7], [8, 9, 10, 11])),
            e => e.Element == "faces[0].holes[1]" && e.Message.Contains("faces[0].holes[0]"));

    [Fact]
    public void TwoTouchingHolesAreAnError()
        => Assert.Contains(Errors(TwoHoles(0f), Holed(Outer, [4, 5, 6, 7], [8, 9, 10, 11])),
            e => e.Element == "faces[0].holes[1]" && e.Message.Contains("faces[0].holes[0]"));

    [Fact]
    public void AVertexUsedByTheOuterLoopAndAHoleIsAnError()
        => Assert.Contains(Errors(Plate, Holed(Outer, [4, 5, 2, 7])), e => e.Element == "faces[0].holes[0][2]");

    [Fact]
    public void AVertexUsedByTwoHolesIsAnError()
        => Assert.Contains(Errors(TwoHoles(0.2f), Holed(Outer, [4, 5, 6, 7], [8, 9, 10, 6])), e => e.Element == "faces[0].holes[1][3]");

    [Fact]
    public void AHoleCornerOffThePlaneIsANonPlanarWarning()
    {
        var v = Plate.ToArray();
        v[6] += new Vector3(0, 0, 0.1f);
        var report = MeshReport.Create(Make(v, Holed(Outer, Hole)));
        Assert.Contains(report.Warnings, w => w.Kind == MeshWarningKind.NonPlanarFace && w.Element == "faces[0]");
    }

    // ---- triangulation ----------------------------------------------------------------------------------------

    private static double Area2D(Vector3 a, Vector3 b, Vector3 c)
        => Math.Abs((b.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (b.Y - a.Y)) / 2;

    private static bool Inside(IReadOnlyList<Vector3> poly, Vector3 p)
    {
        var inside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            if ((poly[i].Y > p.Y) != (poly[j].Y > p.Y)
                && p.X < (poly[j].X - poly[i].X) * (p.Y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X)
                inside = !inside;
        }
        return inside;
    }

    private static void AssertCovers(PolygonMesh m, double expected)
    {
        var tris = Triangulator.TriangulateFace(m, 0);
        var v = m.Vertices;
        Assert.Equal(expected, tris.Sum(t => Area2D(v[t.A], v[t.B], v[t.C])), 4);
        foreach (var t in tris)
        {
            var c = (v[t.A] + v[t.B] + v[t.C]) / 3;
            Assert.True(Inside(m.Faces[0].Loop.Select(i => v[i]).ToArray(), c), $"centroid {c} is outside the outer loop");
            foreach (var h in m.Faces[0].Holes)
                Assert.False(Inside(h.Select(i => v[i]).ToArray(), c), $"centroid {c} lies in a hole");
            // Each triangle faces the way the face does (+Z).
            Assert.True(Vector3.Cross(v[t.B] - v[t.A], v[t.C] - v[t.A]).Z > 0);
        }
    }

    [Fact]
    public void ASquareWithOneSquareHoleTriangulatesToOuterLessHole()
        => AssertCovers(Make(Plate, Holed(Outer, Hole)), 3.0);

    [Fact]
    public void ASquareWithTwoHolesTriangulatesToOuterLessHoles()
        => AssertCovers(Make(TwoHoles(0.2f), Holed(Outer, [4, 5, 6, 7], [8, 9, 10, 11])), 4 - 2 * 0.16);

    [Fact]
    public void AConcaveOuterLoopWithAHoleTriangulatesToOuterLessHole()
    {
        // An L: 2 x 2 less its top-right 1 x 1, with a 0.4-square hole in the bottom-right arm.
        Vector3[] v =
        [
            new(-1, -1, 0), new(1, -1, 0), new(1, 0, 0), new(0, 0, 0), new(0, 1, 0), new(-1, 1, 0),
            new(0.3f, -0.7f, 0), new(0.3f, -0.3f, 0), new(0.7f, -0.3f, 0), new(0.7f, -0.7f, 0),
        ];
        AssertCovers(Make(v, Holed([0, 1, 2, 3, 4, 5], [6, 7, 8, 9])), 3 - 0.16);
    }

    [Fact]
    public void AHoledFacesLabelPointLiesOnTheFaceNotInItsHole()
    {
        var m = Make(Plate, Holed(Outer, Hole));
        var p = m.FaceLabelPoint(0);
        Assert.True(Inside(Outer.Select(i => Plate[i]).ToArray(), p), $"{p} is off the plate");
        Assert.False(Inside(Hole.Select(i => Plate[i]).ToArray(), p), $"{p} is in the hole");
        var plain = Make(Plate, Outer);
        Assert.Equal(plain.FaceCentre(0), plain.FaceLabelPoint(0));
    }

    // ---- topology and health ----------------------------------------------------------------------------------

    /// <summary>The plate as a 0.2-thick box: the holed front, optionally the inner face, the back and four sides.</summary>
    private static PolygonMesh Box(bool inner)
    {
        var v = new List<Vector3>();
        foreach (var p in Plate) v.Add(p + new Vector3(0, 0, 0.1f));
        for (var i = 0; i < 4; i++) v.Add(Plate[i] - new Vector3(0, 0, 0.1f));
        var faces = new List<Face>
        {
            Holed(Outer, Hole),
            new[] { 8, 11, 10, 9 },
            new[] { 0, 8, 9, 1 }, new[] { 1, 9, 10, 2 }, new[] { 2, 10, 11, 3 }, new[] { 3, 11, 8, 0 },
        };
        if (inner) faces.Add(new[] { 4, 7, 6, 5 });
        return Make(v.ToArray(), faces.ToArray());
    }

    [Fact]
    public void ThePlateWithItsHoleFilledIsClosedAndEveryEdgeManifold()
    {
        var m = Box(inner: true);
        Assert.All(m.Topology.Edges, e => Assert.True(e.IsManifold, e.Name));
        Assert.Equal(16, m.Topology.Edges.Count);
        var health = MeshHealth.Create(m);
        Assert.Single(health.Shells);
        Assert.True(health.Shells[0].Closed);
        Assert.True(health.Shells[0].ConsistentWinding);
        Assert.Empty(health.Problems);
    }

    [Fact]
    public void AHoleEdgeNamesItsHoleAndCorner()
    {
        var m = Box(inner: true);
        var edges = m.Topology.FaceEdges(0);
        Assert.Equal(8, edges.Count);
        // Outer loop first, then the hole in corner order.
        Assert.Equal((0, 1), (edges[0].Lo, edges[0].Hi));
        Assert.Equal((4, 5), (edges[4].Lo, edges[4].Hi));
        var use = edges[5].Faces.Single(u => u.Face == 0);
        Assert.Equal((0, 1), (use.Hole, use.Corner));
        Assert.Equal("faces[0].holes[0][1]", use.CornerName);
    }

    [Fact]
    public void ThePlateWithoutTheInnerFaceHasItsFourHoleEdgesOpen()
    {
        var m = Box(inner: false);
        var open = m.Topology.Edges.Where(e => e.Faces.Count == 1).Select(e => e.Name).OrderBy(n => n).ToArray();
        Assert.Equal(new[] { "edges[4-5]", "edges[4-7]", "edges[5-6]", "edges[6-7]" }, open);
        Assert.False(MeshHealth.Create(m).Shells[0].Closed);
        Assert.False(MeshReport.Create(m).Closed);
    }

    // ---- edits ------------------------------------------------------------------------------------------------

    [Fact]
    public void ExtrudingAHoledFaceMakesATube()
    {
        var plate = Make(Plate, Holed(Outer, Hole));
        var r = MeshEdits.Extrude(plate, [0], 0.2f);
        Assert.True(r.Succeeded, string.Join("; ", r.Errors));
        var m = r.Mesh!;
        Assert.Equal(16, m.Vertices.Count);
        Assert.Equal(9, m.Faces.Count);
        var x = Assert.Single(r.Extruded);
        Assert.Equal(0, x.Cap);
        Assert.Equal(8, x.Walls.Count);
        Assert.Equal(8, x.NewVertices.Count);
        var cap = m.Faces[0];
        Assert.Single(cap.Holes);
        Assert.All(cap.Corners, c => Assert.Contains(c, x.NewVertices));
        Assert.All(cap.Corners, c => Assert.Equal(0.2f, m.Vertices[c].Z, 5));
        Assert.Equal(3.0, m.FaceArea(0), 5);
        // Four outer walls face outwards, four hole walls face into the hole.
        var centre = Vector3.Zero;
        foreach (var w in x.Walls.Take(4))
            Assert.True(Vector3.Dot(m.FaceNormal(w), m.FaceCentre(w) - centre) > 0, $"outer wall faces[{w}] faces inwards");
        foreach (var w in x.Walls.Skip(4))
            Assert.True(Vector3.Dot(m.FaceNormal(w), m.FaceCentre(w) - centre) < 0, $"hole wall faces[{w}] faces away from the hole");
        // The tube's open edges are only the original rings: outer and hole loops of the base.
        var open = m.Topology.Edges.Where(e => e.Faces.Count == 1).ToArray();
        Assert.Equal(8, open.Length);
        Assert.All(open, e => Assert.True(e.Lo < 8 && e.Hi < 8, e.Name));
    }

    [Fact]
    public void AnInsetOnAHoledFaceIsRefusedNamingTheFace()
    {
        var r = MeshEdits.Extrude(Make(Plate, Holed(Outer, Hole)), [0], 0.2f, 0.1f);
        Assert.False(r.Succeeded);
        Assert.Contains(r.Errors, e => e.Element == "faces[0]" && e.Message.Contains("hole"));
    }

    [Fact]
    public void AMoveAfterWhichAHoleLeavesItsFaceIsRefused()
    {
        var r = MeshEdits.Move(Make(Plate, Holed(Outer, Hole)), [6], null, null, by: new Vector3(1, 0, 0));
        Assert.False(r.Succeeded);
        Assert.Contains(r.Errors, e => e.Element.StartsWith("faces[0]"));
    }

    [Fact]
    public void MovingAHoleCornerInsideItsFaceSucceeds()
    {
        var r = MeshEdits.Move(Make(Plate, Holed(Outer, Hole)), [6], null, null, by: new Vector3(0.2f, 0.2f, 0));
        Assert.True(r.Succeeded, string.Join("; ", r.Errors));
        Assert.Equal(new Vector3(0.7f, 0.7f, 0), r.Mesh!.Vertices[6]);
    }

    [Fact]
    public void SubdivideRefusesAHoledFace()
    {
        var r = MeshEdits.Subdivide(Box(inner: true), 1);
        Assert.False(r.Succeeded);
        Assert.Contains(r.Errors, e => e.Element == "faces[0]" && e.Message.Contains("holes"));
    }

    // ---- the file ---------------------------------------------------------------------------------------------

    [Fact]
    public void AHoledFaceRoundTripsThroughVersion4()
    {
        var m = Box(inner: false);
        var text = CarveFile.Write(m);
        Assert.Contains("\"carve\": 4", text);
        Assert.Contains("{\"loop\": [0, 1, 2, 3], \"holes\": [[4, 5, 6, 7]]}", text.Replace("\r", ""));
        var read = CarveFile.Read(text);
        Assert.True(read.Succeeded, string.Join("; ", read.Errors));
        Assert.Equal(4, read.Version);
        Assert.Equal(m.Faces, read.Mesh!.Faces);
        Assert.Equal(m.Vertices, read.Mesh.Vertices);
        Assert.Equal(text, CarveFile.Write(read.Mesh));
    }

    [Fact]
    public void AModelWithNoHoledFaceStillWritesVersion2()
        => Assert.Contains("\"carve\": 2", CarveFile.Write(TestMeshes.Cube()));

    [Theory]
    [InlineData("vertices")]
    [InlineData("faces")]
    public void ANullTombstoneIsRefusedUntilT64(string key)
    {
        var text = CarveFile.Write(Box(inner: false)).Replace("\r", "");
        text = key == "faces"
            ? text.Replace("\"faces\": [\n", "\"faces\": [\n    null,\n")
            : text.Replace("\"vertices\": [\n", "\"vertices\": [\n    null,\n");
        var read = CarveFile.Read(text);
        Assert.False(read.Succeeded);
        Assert.Contains(read.Errors, e => e.Message.StartsWith("null is the tombstone"));
    }
}
