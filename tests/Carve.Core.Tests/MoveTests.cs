using System.Numerics;
using Xunit;
using static Carve.Core.Tests.TestMeshes;

namespace Carve.Core.Tests;

[Trait("Tier", "unit")]
public class MoveTests
{
    private static readonly Vector3 Up = new(0, 0.5f, 0);

    private static void AssertMoved(PolygonMesh before, EditResult result, int[] expected, Vector3 by)
    {
        var after = Ok(result);
        Assert.Equal(expected, result.MovedVertices);
        Assert.Empty(result.NewVertices);
        Assert.Empty(result.NewFaces);
        Assert.Equal(before.Vertices.Count, after.Vertices.Count);
        Assert.Equal(before.Faces, after.Faces);
        for (var v = 0; v < before.Vertices.Count; v++)
            Assert.Equal(expected.Contains(v) ? before.Vertices[v] + by : before.Vertices[v], after.Vertices[v]);
    }

    [Fact]
    public void MovesAVertex()
    {
        var cube = TestMeshes.Cube();
        AssertMoved(cube, MeshEdits.Move(cube, [2], null, null, Up), [2], Up);
    }

    [Fact]
    public void MovesBothEndsOfAnEdgeInEitherOrder()
    {
        var cube = TestMeshes.Cube();
        AssertMoved(cube, MeshEdits.Move(cube, null, [(6, 2)], null, Up), [2, 6], Up);
    }

    [Fact]
    public void MovesEveryCornerOfAFace()
    {
        var cube = TestMeshes.Cube();
        AssertMoved(cube, MeshEdits.Move(cube, null, null, [1], Up), [4, 5, 6, 7], Up);
    }

    [Fact]
    public void MovesTheUnionOnce()
    {
        var cube = TestMeshes.Cube();
        AssertMoved(cube, MeshEdits.Move(cube, [6, 6, 0], [(7, 6)], [3], Up), [0, 2, 3, 6, 7], Up);
    }

    [Fact]
    public void LeavesTheGivenMeshAsItWas()
    {
        var cube = TestMeshes.Cube();
        Ok(MeshEdits.Move(cube, null, null, [1], Up));
        Assert.Equal(TestMeshes.CubeVertices, cube.Vertices);
    }

    [Fact]
    public void MovingOneTopCornerUpWarnsNonPlanar()
    {
        var cube = TestMeshes.Cube();
        var report = MeshReport.Create(Ok(MeshEdits.Move(cube, [6], null, null, Up)));
        var warning = Assert.Single(report.Warnings);
        Assert.Equal(MeshWarningKind.NonPlanarFace, warning.Kind);
        Assert.Equal("faces[3]", warning.Element);
    }

    private static MeshError Refused(EditResult result)
    {
        Assert.False(result.Succeeded);
        Assert.Null(result.Mesh);
        Assert.Empty(result.MovedVertices);
        return Assert.Single(result.Errors);
    }

    [Fact]
    public void RefusesAnUnknownVertex()
    {
        var error = Refused(MeshEdits.Move(TestMeshes.Cube(), [0, 8], null, null, Up));
        Assert.Equal("vertices[8]", error.Element);
        Assert.Contains("2nd listed", error.Message);
        Assert.Contains("no vertex 8", error.Message);
    }

    [Fact]
    public void RefusesAnUnknownFace()
    {
        var error = Refused(MeshEdits.Move(TestMeshes.Cube(), null, null, [-1], Up));
        Assert.Equal("faces[-1]", error.Element);
        Assert.Contains("no face -1", error.Message);
    }

    [Fact]
    public void RefusesAPairThatIsNotAnEdge()
    {
        // 0-1 is an edge; 0-2 is only a diagonal of face 0.
        var error = Refused(MeshEdits.Move(TestMeshes.Cube(), null, [(0, 1), (2, 0)], null, Up));
        Assert.Equal("edges[0-2]", error.Element);
        Assert.Contains("2nd listed edge, [2, 0], is not an edge", error.Message);
        Assert.EndsWith("is not an edge of the mesh; select with kind edges lists the real edges", error.Message);
    }

    [Fact]
    public void RefusesNothingListed()
    {
        var error = Refused(MeshEdits.Move(TestMeshes.Cube(), [], null, [], Up));
        Assert.Equal("vertices, edges, faces", error.Element);
        Assert.Contains("nothing to move", error.Message);
    }

    [Fact]
    public void RefusesAZeroMove()
    {
        var error = Refused(MeshEdits.Move(TestMeshes.Cube(), [0], null, null, Vector3.Zero));
        Assert.Equal("by", error.Element);
        Assert.Contains("zero", error.Message);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void RefusesANonFiniteMove(float bad)
    {
        var error = Refused(MeshEdits.Move(TestMeshes.Cube(), [0], null, null, new Vector3(0, 0, bad)));
        Assert.Equal("by", error.Element);
        Assert.Contains("not finite", error.Message);
    }

    [Fact]
    public void ListsEveryProblem()
    {
        var result = MeshEdits.Move(TestMeshes.Cube(), [9], [(0, 6)], [6], Vector3.Zero);
        Assert.False(result.Succeeded);
        Assert.Equal(["by", "vertices[9]", "edges[0-6]", "faces[6]"], result.Errors.Select(e => e.Element));
    }
}
