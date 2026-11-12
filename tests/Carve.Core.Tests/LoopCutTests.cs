using System.Numerics;
using Xunit;

namespace Carve.Core.Tests;

/// <summary>T-66's <see cref="LoopCut"/>: edge loops across a ring of quads, on <see cref="FaceSplit"/>.</summary>
[Trait("Tier", "unit")]
public class LoopCutTests
{
    private static void IsClosed(PolygonMesh m) =>
        Assert.All(m.Topology.Edges, e => Assert.True(e.IsManifold, e.Name));

    private static PolygonMesh Ok(LoopCutResult r) => TestMeshes.Ok(r.Edit);

    /// <summary>A strip of quads on z = 0: bottom vertices 0..n (y = 0) and top n+1..2n+1, the heights given.</summary>
    private static PolygonMesh Strip(float[] heights)
    {
        var n = heights.Length - 1;
        var v = Enumerable.Range(0, n + 1).Select(i => new Vector3(i, 0, 0))
            .Concat(Enumerable.Range(0, n + 1).Select(i => new Vector3(i, heights[i], 0))).ToArray();
        var faces = Enumerable.Range(0, n).Select(j => new[] { j, j + 1, j + n + 2, j + n + 1 }).ToArray();
        return TestMeshes.Make(v, faces);
    }

    [Fact]
    public void ARingRoundACubesSidesIsClosed()
    {
        var r = LoopCut.Cut(TestMeshes.Cube(), (0, 3), [0.5]);
        var m = Ok(r);

        Assert.Equal(12, m.LiveVertexCount);
        Assert.Equal(10, m.LiveFaceCount);
        Assert.Equal([0, 1, 4, 5], r.Edit.Split.Select(s => s.Face));
        Assert.All(r.Edit.Split, s => Assert.Equal(2, s.Pieces.Count));
        Assert.Equal(4, r.Ring.Count);
        Assert.Equal([0, 1, 4, 5], r.Ring.Order());
        Assert.Equal(4, r.Edit.NewVertices.Count);
        Assert.All(r.Edit.NewVertices, v => Assert.Equal(0.5f, m.Vertices[v].Y));
        // The top and bottom are not in the ring: they keep their four corners (plus no loop through them).
        Assert.Equal(4, m.Faces[2].Loop.Count);
        Assert.Equal(4, m.Faces[3].Loop.Count);
        IsClosed(m);
    }

    [Fact]
    public void ARingOnAnOpenStripStopsAtTheOpenEnds()
    {
        var r = LoopCut.Cut(Strip([1, 1, 1, 1]), (0, 4), [0.5]);
        var m = Ok(r);

        Assert.Equal(3, r.Ring.Count);
        Assert.Equal(12, m.LiveVertexCount);
        Assert.Equal(6, m.LiveFaceCount);
        Assert.Equal([0, 1, 2], r.Edit.Split.Select(s => s.Face));
        Assert.Equal(4, r.Edit.NewVertices.Count);
        Assert.Contains(m.Topology.Edges, e => !e.IsManifold);

        // From a middle rung the ring walks both ways, and gives the same ring.
        var middle = LoopCut.Cut(Strip([1, 1, 1, 1]), (1, 5), [0.5]);
        Assert.Equal([0, 1, 2], Ok(middle) is { } && middle.Ring.Count == 3 ? middle.Ring.Order() : []);
        Assert.Equal(4, middle.Edit.NewVertices.Count);
    }

    [Fact]
    public void ARingStopsAtATriangle()
    {
        // Q0, Q1 and then a triangle on rung 2-6.
        var v = new Vector3[] { new(0, 0, 0), new(1, 0, 0), new(2, 0, 0), new(3, 0, 0), new(0, 1, 0), new(1, 1, 0), new(2, 1, 0) };
        var m0 = TestMeshes.Make(v, new[] { new[] { 0, 1, 5, 4 }, new[] { 1, 2, 6, 5 }, new[] { 2, 3, 6 } });
        var r = LoopCut.Cut(m0, (0, 4), [0.5]);
        var m = Ok(r);

        Assert.Equal([0, 1], r.Ring);
        Assert.Equal([0, 1], r.Edit.Split.Select(s => s.Face));
        // The triangle is not split, but takes the new vertex on the rung it shares.
        Assert.Equal(4, m.Faces[2].Loop.Count);
        Assert.Equal(3, r.Edit.NewVertices.Count);
        Assert.Equal(5, m.LiveFaceCount);
    }

    [Fact]
    public void ARingStopsAtAHoledFace()
    {
        var v = new Vector3[]
        {
            new(0, 0, 0), new(1, 0, 0), new(2, 0, 0), new(3, 0, 0), new(0, 1, 0), new(1, 1, 0), new(2, 1, 0), new(3, 1, 0),
            new(2.3f, 0.3f, 0), new(2.7f, 0.3f, 0), new(2.7f, 0.7f, 0), new(2.3f, 0.7f, 0),
        };
        var faces = new[] { new Face([0, 1, 5, 4]), new Face([1, 2, 6, 5]), new Face([2, 3, 7, 6], [[8, 11, 10, 9]]) };
        var made = PolygonMesh.Create(v, faces);
        Assert.True(made.Succeeded, string.Join("; ", made.Errors));
        var r = LoopCut.Cut(made.Mesh!, (0, 4), [0.5]);
        var m = Ok(r);

        Assert.Equal([0, 1], r.Ring);
        Assert.Equal([0, 1], r.Edit.Split.Select(s => s.Face));
        // The holed face is left whole, with the new vertex on its edge 2-6 in its outer loop.
        Assert.Equal(5, m.Faces[2].Loop.Count);
        Assert.Single(m.Faces[2].Holes);
    }

    [Fact]
    public void TwoFractionsGiveThreePiecesAFace()
    {
        var r = LoopCut.Cut(TestMeshes.Cube(), (0, 3), [0.25, 0.75]);
        var m = Ok(r);

        Assert.Equal(16, m.LiveVertexCount);
        Assert.Equal(14, m.LiveFaceCount);
        Assert.All(r.Edit.Split, s => Assert.Equal(3, s.Pieces.Count));
        Assert.Equal([0.25f, 0.25f, 0.25f, 0.25f, 0.75f, 0.75f, 0.75f, 0.75f],
            r.Edit.NewVertices.Select(v => m.Vertices[v].Y).Order());
        IsClosed(m);
    }

    [Fact]
    public void TheSideIsKeptAcrossEveryQuadWhicheverWayItsLoopRuns()
    {
        // On the cube, 0-3 is walked from 0 in face 0 and from 3 in face 4: its two quads run opposite ways.
        var fromBottom = Ok(LoopCut.Cut(TestMeshes.Cube(), (0, 3), [0.25]));
        Assert.All(Enumerable.Range(8, 4), v => Assert.Equal(0.25f, fromBottom.Vertices[v].Y));
        var fromTop = Ok(LoopCut.Cut(TestMeshes.Cube(), (3, 0), [0.25]));
        Assert.All(Enumerable.Range(8, 4), v => Assert.Equal(0.75f, fromTop.Vertices[v].Y));

        // A trapezoid strip whose second quad is wound the other way, so its rung is much taller.
        var strip = TestMeshes.Make(
            [new Vector3(0, 0, 0), new(1, 0, 0), new(2, 0, 0), new(0, 1, 0), new(1, 1, 0), new(2, 2, 0)],
            new[] { new[] { 0, 1, 4, 3 }, new[] { 4, 5, 2, 1 } });
        var r = LoopCut.Cut(strip, (0, 3), [0.25]);
        var m = Ok(r);
        Assert.Equal([0, 1], r.Ring);
        Assert.Equal([0.25f, 0.25f, 0.5f], r.Edit.NewVertices.Select(v => m.Vertices[v].Y));
    }

    [Fact]
    public void IdsAreTheSameEveryTimeAndFollowFaceSplit()
    {
        var a = LoopCut.Cut(TestMeshes.Cube(), (0, 3), [0.5]);
        var b = LoopCut.Cut(TestMeshes.Cube(), (0, 3), [0.5]);
        Assert.Equal(a.Edit.NewVertices, b.Edit.NewVertices);
        Assert.Equal(a.Edit.NewFaces, b.Edit.NewFaces);
        Assert.Equal(Ok(a).Faces.Select(f => f.ToString()), Ok(b).Faces.Select(f => f.ToString()));

        // New vertices by edge, low id then high: 0-3, 1-2, 4-7, 5-6.
        Assert.Equal([8, 9, 10, 11], a.Edit.NewVertices);
        var m = Ok(a);
        Assert.Equal(new Vector3(0, 0.5f, 0), m.Vertices[8]);
        Assert.Equal(new Vector3(1, 0.5f, 0), m.Vertices[9]);
        Assert.Equal(new Vector3(0, 0.5f, 1), m.Vertices[10]);
        Assert.Equal(new Vector3(1, 0.5f, 1), m.Vertices[11]);
        Assert.Equal([6, 7, 8, 9], a.Edit.NewFaces);
        // The starting edge's two faces and the rest keep their ids; the upper piece is appended.
        Assert.Equal([(0, new[] { 0, 6 }), (1, new[] { 1, 7 }), (4, new[] { 4, 8 }), (5, new[] { 5, 9 })],
            a.Edit.Split.Select(s => (s.Face, s.Pieces.ToArray())));
    }

    [Fact]
    public void AWeightedEdgeBlendsAtTheFraction()
    {
        var weights = VertexWeights.Of(
        [
            KeyValuePair.Create(0, new Influence[] { new(0, 1) }),
            KeyValuePair.Create(3, new Influence[] { new(1, 1) }),
        ]);
        var cube = TestMeshes.Cube().WithWeights(weights);
        var m = Ok(LoopCut.Cut(cube, (0, 3), [0.25]));

        // Vertex 8 is on 0-3 at 0.25 from 0.
        var at8 = m.Weights!.Of(8);
        Assert.Equal(0.75, at8.Single(i => i.Bone == 0).Weight, 12);
        Assert.Equal(0.25, at8.Single(i => i.Bone == 1).Weight, 12);
        Assert.Equal(weights.Of(0), m.Weights.Of(0));
    }

    [Fact]
    public void AnEdgeThatIsNotOneIsRefusedNamingThePair()
    {
        var r = LoopCut.Cut(TestMeshes.Cube(), (0, 2), [0.5]);
        Assert.False(r.Edit.Succeeded);
        var e = Assert.Single(r.Edit.Errors);
        Assert.Equal("edge[0-2]", e.Element);
        Assert.Contains("[0, 2] is not an edge of the mesh", e.Message);
        Assert.Contains("select with kind edges lists the real edges", e.Message);
        Assert.Empty(r.Edit.Split);
    }

    [Fact]
    public void AnEdgeWithNoQuadOnItIsRefused()
    {
        var tetra = TestMeshes.Make(
            [new Vector3(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(0, 0, 1)],
            new[] { new[] { 0, 2, 1 }, new[] { 0, 1, 3 }, new[] { 1, 2, 3 }, new[] { 0, 3, 2 } });
        var r = LoopCut.Cut(tetra, (0, 1), [0.5]);
        Assert.Contains("has no quad on it", Assert.Single(r.Edit.Errors).Message);
    }

    [Fact]
    public void AnEdgeOnThreeFacesIsRefusedUntilTheOpenRuleIsRuled()
    {
        var v = new Vector3[] { new(0, 0, 0), new(1, 0, 0), new(1, 1, 0), new(0, 1, 0), new(1, 0, 1), new(0, 0, 1), new(1, 0, -1), new(0, 0, -1) };
        var m = TestMeshes.Make(v, new[] { new[] { 0, 1, 2, 3 }, new[] { 1, 0, 5, 4 }, new[] { 0, 1, 6, 7 } });
        var r = LoopCut.Cut(m, (0, 1), [0.5]);
        Assert.Contains("non-manifold", Assert.Single(r.Edit.Errors).Message);
    }

    [Fact]
    public void ARingMeetingAnEdgeOnThreeFacesIsRefusedToo()
    {
        var v = new Vector3[]
        {
            new(0, 0, 0), new(1, 0, 0), new(2, 0, 0), new(3, 0, 0), new(0, 1, 0), new(1, 1, 0), new(2, 1, 0), new(3, 1, 0),
            new(2, 1, 1), new(2, 0, 1),
        };
        var m = TestMeshes.Make(v, new[] { new[] { 0, 1, 5, 4 }, new[] { 1, 2, 6, 5 }, new[] { 2, 3, 7, 6 }, new[] { 6, 2, 9, 8 } });
        var r = LoopCut.Cut(m, (0, 4), [0.5]);
        Assert.False(r.Edit.Succeeded);
        Assert.Contains("the ring meets edge 2-6, on 3 faces", Assert.Single(r.Edit.Errors).Message);
    }

    public static TheoryData<double[], string, string> BadFractions => new()
    {
        { [], "at", "list at least one fraction" },
        { [0.0], "at[0]", "not strictly between 0 and 1" },
        { [1.0], "at[0]", "not strictly between 0 and 1" },
        { [double.NaN], "at[0]", "not strictly between 0 and 1" },
        { [0.5, 0.5], "at[1]", "strictly ascending" },
        { [0.6, 0.4], "at[1]", "strictly ascending" },
        { [0.00001], "at[0]", "too near an end of the shortest ring edge" },
        { [0.99999], "at[0]", "too near an end of the shortest ring edge" },
    };

    [Theory]
    [MemberData(nameof(BadFractions))]
    public void ABadAtIsRefused(double[] at, string element, string message)
    {
        var cube = TestMeshes.Cube();
        var r = LoopCut.Cut(cube, (0, 3), at);
        Assert.False(r.Edit.Succeeded);
        Assert.Contains(r.Edit.Errors, e => e.Element == element && e.Message.Contains(message, StringComparison.Ordinal));
        Assert.Empty(r.Ring);
    }
}
