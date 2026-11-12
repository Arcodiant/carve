using System.Numerics;
using Xunit;
using static Carve.Core.Tests.TestMeshes;

namespace Carve.Core.Tests;

[Trait("Tier", "unit")]
public class ExtrudeTests
{
    private const float Tolerance = 1e-5f;

    private static void AssertNear(Vector3 expected, Vector3 actual)
        => Assert.True(Vector3.Distance(expected, actual) < Tolerance, $"expected {expected}, got {actual}");

    private static void AssertClosedAndClean(PolygonMesh mesh)
    {
        var report = MeshReport.Create(mesh);
        Assert.True(report.Closed);
        Assert.Empty(report.Warnings);
    }

    /// <summary>Signed horizontal reach of a wall's normal away from the vertical axis through the top face's centre.</summary>
    private static float Outwardness(PolygonMesh mesh, int wall)
    {
        var centre = mesh.FaceCentre(wall);
        return Vector3.Dot(mesh.FaceNormal(wall), centre - new Vector3(0.5f, centre.Y, 0.5f));
    }

    [Fact]
    public void ExtrudingTheTopByOne()
    {
        var cube = TestMeshes.Cube();
        var result = MeshEdits.Extrude(cube, [3], 1);
        var mesh = Ok(result);

        Assert.Equal(12, mesh.Vertices.Count);
        Assert.Equal(10, mesh.Faces.Count);
        AssertClosedAndClean(mesh);

        var record = Assert.Single(result.Extruded);
        Assert.Equal(3, record.Face);
        Assert.Equal(3, record.Cap);
        Assert.Equal([6, 7, 8, 9], record.Walls);
        Assert.Equal([8, 9, 10, 11], record.NewVertices);
        Assert.Equal([8, 9, 10, 11], result.NewVertices);
        Assert.Equal([6, 7, 8, 9], result.NewFaces);
        Assert.Empty(result.MovedVertices);

        // The cap keeps id 3, is the new ring, and stands one above where the top was.
        Assert.Equal([8, 9, 10, 11], mesh.Faces[3].Loop);
        for (var k = 0; k < 4; k++)
            AssertNear(cube.Vertices[cube.Faces[3].Loop[k]] + Vector3.UnitY, mesh.Vertices[8 + k]);
        AssertNear(Vector3.UnitY, mesh.FaceNormal(3));

        // Wall k is [old k, old k+1, new k+1, new k], and every wall faces out.
        Assert.Equal([3, 7, 9, 8], mesh.Faces[6].Loop);
        foreach (var wall in record.Walls)
        {
            Assert.True(Outwardness(mesh, wall) > 0.49f, $"faces[{wall}] faces inward: normal {mesh.FaceNormal(wall)}");
            Assert.True(Math.Abs(mesh.FaceNormal(wall).Y) < Tolerance);
        }
    }

    [Fact]
    public void RecessingWithAnInset()
    {
        var result = MeshEdits.Extrude(TestMeshes.Cube(), [3], -0.2f, 0.2f);
        var mesh = Ok(result);
        AssertClosedAndClean(mesh);
        AssertNear(Vector3.UnitY, mesh.FaceNormal(3));
        foreach (var v in mesh.Faces[3].Loop)
        {
            Assert.Equal(0.8f, mesh.Vertices[v].Y, Tolerance);
            Assert.InRange(mesh.Vertices[v].X, 0.2f - Tolerance, 0.8f + Tolerance);
            Assert.InRange(mesh.Vertices[v].Z, 0.2f - Tolerance, 0.8f + Tolerance);
        }
        // A pocket's walls run 45 degrees from the old edge in and down to the cap, so they face up and into the pocket.
        foreach (var wall in result.Extruded[0].Walls)
        {
            var normal = mesh.FaceNormal(wall);
            Assert.True(Outwardness(mesh, wall) < -0.2f, $"faces[{wall}] faces away from the pocket: normal {normal}");
            Assert.Equal(MathF.Sqrt(0.5f), normal.Y, Tolerance);
        }
    }

    [Fact]
    public void InsetOnlyOnAllSixFacesThenRecessed()
    {
        var inset = Ok(MeshEdits.Extrude(TestMeshes.Cube(), AllCubeFaces, 0, 0.12f));
        Assert.Equal(32, inset.Vertices.Count);
        Assert.Equal(30, inset.Faces.Count);
        AssertClosedAndClean(inset);
        for (var f = 0; f < 6; f++)
            AssertNear(TestMeshes.Cube().FaceNormal(f), inset.FaceNormal(f));

        // The planning probe's first half of the crate: inset, then recess every cap.
        var recessed = Ok(MeshEdits.Extrude(inset, AllCubeFaces, -0.08f));
        Assert.Equal(56, recessed.Vertices.Count);
        Assert.Equal(54, recessed.Faces.Count);
        AssertClosedAndClean(recessed);
    }

    [Fact]
    public void NewIdsRunFaceByFaceInTheOrderListed()
    {
        var result = MeshEdits.Extrude(TestMeshes.Cube(), [5, 1], 0.5f);
        Ok(result);
        Assert.Equal(5, result.Extruded[0].Face);
        Assert.Equal([8, 9, 10, 11], result.Extruded[0].NewVertices);
        Assert.Equal([6, 7, 8, 9], result.Extruded[0].Walls);
        Assert.Equal(1, result.Extruded[1].Face);
        Assert.Equal([12, 13, 14, 15], result.Extruded[1].NewVertices);
        Assert.Equal([10, 11, 12, 13], result.Extruded[1].Walls);
    }

    [Fact]
    public void InsetOnTheConcaveLBlockCapKeepsEveryEdgeParallel()
    {
        var block = TestMeshes.LBlock();
        var result = MeshEdits.Extrude(block, [1], 0, 0.2f);
        var mesh = Ok(result);
        AssertClosedAndClean(mesh);

        var oldLoop = block.Faces[1].Loop;
        var newLoop = mesh.Faces[1].Loop;
        for (var k = 0; k < 6; k++)
        {
            var j = (k + 1) % 6;
            var before = block.Vertices[oldLoop[j]] - block.Vertices[oldLoop[k]];
            var after = mesh.Vertices[newLoop[j]] - mesh.Vertices[newLoop[k]];
            Assert.True(Vector3.Cross(before, after).Length() < Tolerance, $"edge {k} turned: {before} -> {after}");
            Assert.True(Vector3.Dot(before, after) > 0, $"edge {k} reversed");
        }
        // The reflex corner (1, 1) moves out of the notch, the convex corner (0, 0) into the face, each by the mitre.
        AssertNear(new Vector3(0.8f, 0.8f, 1), mesh.Vertices[newLoop[3]]);
        AssertNear(new Vector3(0.2f, 0.2f, 1), mesh.Vertices[newLoop[0]]);
    }

    private static MeshError Refused(EditResult result)
    {
        Assert.False(result.Succeeded);
        Assert.Null(result.Mesh);
        Assert.Empty(result.NewVertices);
        Assert.Empty(result.NewFaces);
        Assert.Empty(result.Extruded);
        return Assert.Single(result.Errors);
    }

    [Fact]
    public void RefusesAnInsetTooLargeForTheFace()
    {
        var cube = TestMeshes.Cube();
        var error = Refused(MeshEdits.Extrude(cube, [1], 0, 0.6f));
        Assert.Equal("faces[1]", error.Element);
        Assert.Contains("inset 0.6 is too large for this face", error.Message);
        Assert.Equal(TestMeshes.CubeVertices, cube.Vertices);
        Assert.Equal(TestMeshes.CubeFaces, cube.Faces.Select(f => f.Loop));
    }

    [Fact]
    public void RefusesAnInsetThatCollapsesTheFaceToAPoint()
    {
        var error = Refused(MeshEdits.Extrude(TestMeshes.Cube(), [1], 0, 0.5f));
        Assert.Equal("faces[1]", error.Element);
        Assert.Contains("too large", error.Message);
    }

    [Fact]
    public void RefusesAnInsetThatCrossesItselfOnAConcaveFace()
    {
        // A 4 x 3 plate with a notch cut down from the top to 1 above the bottom. Inset 0.6, every offset edge still runs
        // the way it did, but the notch's bottom (offset to y = 0.4) drops below the bottom edge's offset (y = 0.6).
        Vector3[] outline = [new(0, 0, 0), new(4, 0, 0), new(4, 3, 0), new(2.2f, 3, 0), new(2.2f, 1, 0), new(1.8f, 1, 0), new(1.8f, 3, 0), new(0, 3, 0)];
        var mesh = TestMeshes.Make(outline, [[0, 1, 2, 3, 4, 5, 6, 7]]);
        var error = Refused(MeshEdits.Extrude(mesh, [0], 0, 0.6f));
        Assert.Equal("faces[0]", error.Element);
        Assert.Contains("too large for this face: the inset outline crosses itself", error.Message);
    }

    [Fact]
    public void RefusesAnUnknownFace()
    {
        var error = Refused(MeshEdits.Extrude(TestMeshes.Cube(), [1, 6], 1));
        Assert.Equal("faces[6]", error.Element);
        Assert.Contains("2nd listed face: there is no face 6", error.Message);
    }

    [Fact]
    public void RefusesAFaceListedTwice()
    {
        var error = Refused(MeshEdits.Extrude(TestMeshes.Cube(), [1, 2, 1], 1));
        Assert.Equal("faces[1]", error.Element);
        Assert.Contains("3rd listed face is listed 1st too", error.Message);
    }

    [Fact]
    public void RefusesNoFaces()
    {
        var error = Refused(MeshEdits.Extrude(TestMeshes.Cube(), [], 1));
        Assert.Equal("faces", error.Element);
    }

    [Fact]
    public void RefusesDistanceAndInsetBothZero()
    {
        var error = Refused(MeshEdits.Extrude(TestMeshes.Cube(), [1], 0, 0));
        Assert.Equal("distance, inset", error.Element);
        Assert.Contains("both zero", error.Message);
    }

    [Fact]
    public void RefusesANegativeInset()
    {
        var error = Refused(MeshEdits.Extrude(TestMeshes.Cube(), [1], 1, -0.1f));
        Assert.Equal("inset", error.Element);
        Assert.Contains("negative", error.Message);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void RefusesANonFiniteInset(float bad)
    {
        var error = Refused(MeshEdits.Extrude(TestMeshes.Cube(), [1], 1, bad));
        Assert.Equal("inset", error.Element);
        Assert.Contains("not finite", error.Message);
    }

    [Fact]
    public void RefusesANonFiniteDistance()
    {
        var error = Refused(MeshEdits.Extrude(TestMeshes.Cube(), [1], float.NegativeInfinity));
        Assert.Equal("distance", error.Element);
    }

    [Fact]
    public void ListsEveryProblem()
    {
        var result = MeshEdits.Extrude(TestMeshes.Cube(), [7, 2, 2, 3], 1, 0.7f);
        Assert.False(result.Succeeded);
        Assert.Equal(["faces[7]", "faces[2]", "faces[2]", "faces[3]"], result.Errors.Select(e => e.Element));
    }
}
