using System.Numerics;
using Xunit;

namespace Carve.Core.Tests;

[Trait("Tier", "unit")]
public class FaceNormalTests
{
    [Fact]
    public void CubeFacesPointOutwardAlongSixAxes()
    {
        var mesh = TestMeshes.Cube();
        Vector3[] expected = [-Vector3.UnitZ, Vector3.UnitZ, -Vector3.UnitY, Vector3.UnitY, -Vector3.UnitX, Vector3.UnitX];
        for (var f = 0; f < 6; f++)
            Assert.True((mesh.FaceNormal(f) - expected[f]).Length() < 1e-6f, $"faces[{f}]");
    }

    [Fact]
    public void ConcaveFaceNormalFollowsWinding()
    {
        var ccw = TestMeshes.Make(TestMeshes.LVertices, [[0, 1, 2, 3, 4, 5]]);
        var cw = TestMeshes.Make(TestMeshes.LVertices, [[5, 4, 3, 2, 1, 0]]);
        Assert.True((ccw.FaceNormal(0) - Vector3.UnitZ).Length() < 1e-6f);
        Assert.True((cw.FaceNormal(0) + Vector3.UnitZ).Length() < 1e-6f);
    }

    [Fact]
    public void FaceCentreIsTheMeanOfItsVertices()
    {
        var cube = TestMeshes.Cube();
        Assert.Equal(new Vector3(0.5f, 0.5f, 1), cube.FaceCentre(1));
        Assert.Equal(new Vector3(0, 0.5f, 0.5f), cube.FaceCentre(4));

        // The mean of the corners, not the centroid of the area: the L's centroid is (5/6, 5/6).
        var l = TestMeshes.Make(TestMeshes.LVertices, [[0, 1, 2, 3, 4, 5]]);
        Assert.Equal(new Vector3(1, 1, 0), l.FaceCentre(0));

        // Far from the origin, the mean is still exact for a face whose corners are.
        var far = TestMeshes.Make(TestMeshes.CubeVertices.Select(v => v + new Vector3(1e6f, 0, 0)), TestMeshes.CubeFaces);
        Assert.Equal(new Vector3(1e6f + 0.5f, 0.5f, 1), far.FaceCentre(1));
    }
}
