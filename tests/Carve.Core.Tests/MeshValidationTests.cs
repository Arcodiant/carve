using System.Numerics;
using Xunit;

namespace Carve.Core.Tests;

[Trait("Tier", "unit")]
public class MeshValidationTests
{
    private static readonly Vector3[] Tri = [new(0, 0, 0), new(1, 0, 0), new(0, 1, 0)];

    private static IReadOnlyList<MeshError> Errors(Vector3[] v, int[][] f)
    {
        var r = PolygonMesh.Create(v, f);
        Assert.False(r.Succeeded);
        Assert.Null(r.Mesh);
        return r.Errors;
    }

    [Fact]
    public void ValidMeshIsCreated()
    {
        var r = PolygonMesh.Create(Tri, [[0, 1, 2]]);
        Assert.True(r.Succeeded);
        Assert.Empty(r.Errors);
        Assert.Equal(3, r.Mesh!.Vertices.Count);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void NonFiniteCoordinateNamesTheVertex(float bad)
    {
        var v = new[] { new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 1, 0), new Vector3(0, bad, 0) };
        Assert.Contains(Errors(v, [[0, 1, 2]]), e => e.Element == "vertices[3]");
    }

    [Fact]
    public void FaceWithTwoIndicesIsAnError()
        => Assert.Contains(Errors(Tri, [[0, 1, 2], [0, 1]]), e => e.Element == "faces[1]");

    [Fact]
    public void OutOfRangeIndexNamesFaceAndPosition()
    {
        Assert.Contains(Errors(Tri, [[0, 1, 2], [0, 1, 2], [0, 5, 2]]), e => e.Element == "faces[2][1]");
        Assert.Contains(Errors(Tri, [[0, -1, 2]]), e => e.Element == "faces[0][1]");
    }

    [Fact]
    public void RepeatedIndexNamesTheRepeat()
        => Assert.Contains(Errors(Tri, [[0, 1, 2, 1]]), e => e.Element == "faces[0][3]");

    [Fact]
    public void ZeroAreaFaceIsAnError()
    {
        var v = new[] { new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(2, 0, 0) };
        Assert.Contains(Errors(v, [[0, 1, 2]]), e => e.Element == "faces[0]" && e.Message.Contains("zero area"));
    }

    [Fact]
    public void NoFacesIsAnError()
        => Assert.Contains(Errors(Tri, []), e => e.Element == "faces");

    [Fact]
    public void BowtieFaceNamesTheEdgesThatCross()
    {
        // Not the symmetric bowtie (0,0),(1,1),(1,0),(0,1), whose Newell area is exactly 0 and stops at the zero-area check.
        var v = new[] { new Vector3(0, 0, 0), new Vector3(2, 2, 0), new Vector3(2, 0, 0), new Vector3(0, 1, 0) };
        var error = Assert.Single(Errors(v, [[0, 1, 2, 3]]));
        Assert.Equal("faces[0]", error.Element);
        Assert.Equal("face is self-intersecting: the edge from faces[0][0] crosses the edge from faces[0][2]", error.Message);
    }

    [Theory]
    [InlineData("[[0,9]]", new[] { "faces[0]", "faces[0][1]" })]
    [InlineData("[[0,0,9]]", new[] { "faces[0][1]", "faces[0][2]" })]
    [InlineData("[[0,0,1,9,9]]", new[] { "faces[0][1]", "faces[0][3]", "faces[0][4]" })]
    [InlineData("[[0,9,1,-1]]", new[] { "faces[0][1]", "faces[0][3]" })]
    public void OneFaceWithTwoFaultsListsThemAll(string faces, string[] elements)
    {
        var f = System.Text.Json.JsonSerializer.Deserialize<int[][]>(faces)!;
        var errors = Errors(Tri, f);
        Assert.Equal(elements.Order(), errors.Select(e => e.Element).Distinct().Order());
    }

    [Fact]
    public void AllErrorsAreReportedTogether()
    {
        var v = new[] { new Vector3(float.NaN, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 1, 0) };
        var errors = Errors(v, [[0, 1], [0, 1, 9]]);
        Assert.Equal(3, errors.Count);
    }
}
