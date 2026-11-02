using System.Numerics;
using Xunit;

namespace Carve.Core.Tests;

[Trait("Tier", "unit")]
public class TriangulatorTests
{
    private static float Area(PolygonMesh mesh, MeshTriangle t)
    {
        var v = mesh.Vertices;
        return Vector3.Cross(v[t.B] - v[t.A], v[t.C] - v[t.A]).Length() / 2;
    }

    private static Vector3 Normal(PolygonMesh mesh, MeshTriangle t)
    {
        var v = mesh.Vertices;
        return Vector3.Normalize(Vector3.Cross(v[t.B] - v[t.A], v[t.C] - v[t.A]));
    }

    [Fact]
    public void QuadGivesTwoTrianglesKeepingWindingAndFace()
    {
        var mesh = TestMeshes.Make([new(0, 0, 0), new(1, 0, 0), new(1, 1, 0), new(0, 1, 0)], [[0, 1, 2, 3]]);
        var tris = Triangulator.Triangulate(mesh);
        Assert.Equal(2, tris.Count);
        Assert.All(tris, t => Assert.Equal(0, t.Face));
        Assert.All(tris, t => Assert.True((Normal(mesh, t) - Vector3.UnitZ).Length() < 1e-6f));
        Assert.Equal(1f, tris.Sum(t => Area(mesh, t)), 1e-5f);
    }

    [Fact]
    public void ConcaveLShapeGivesFourAgreeingTrianglesOfFullArea()
    {
        var mesh = TestMeshes.Make(TestMeshes.LVertices, [[0, 1, 2, 3, 4, 5]]);
        var tris = Triangulator.Triangulate(mesh);
        Assert.Equal(4, tris.Count);
        Assert.Equal(3f, tris.Sum(t => Area(mesh, t)), 1e-5f);
        Assert.All(tris, t => Assert.True((Normal(mesh, t) - Vector3.UnitZ).Length() < 1e-6f));
    }

    [Fact]
    public void ConcaveFaceInATiltedPlaneStillTriangulates()
    {
        var rot = Matrix4x4.CreateRotationX(0.7f) * Matrix4x4.CreateRotationY(1.1f);
        var verts = TestMeshes.LVertices.Select(p => Vector3.Transform(p, rot)).ToArray();
        var mesh = TestMeshes.Make(verts, [[0, 1, 2, 3, 4, 5]]);
        var tris = Triangulator.Triangulate(mesh);
        Assert.Equal(4, tris.Count);
        Assert.Equal(3f, tris.Sum(t => Area(mesh, t)), 1e-4f);
        Assert.All(tris, t => Assert.True((Normal(mesh, t) - mesh.FaceNormal(0)).Length() < 1e-4f));
    }

    [Fact]
    public void CubeGivesTwelveTrianglesRecordingTheirFaces()
    {
        var mesh = TestMeshes.Cube();
        var tris = Triangulator.Triangulate(mesh);
        Assert.Equal(12, tris.Count);
        for (var f = 0; f < 6; f++)
        {
            var own = tris.Where(t => t.Face == f).ToArray();
            Assert.Equal(2, own.Length);
            Assert.All(own, t => Assert.True((Normal(mesh, t) - mesh.FaceNormal(f)).Length() < 1e-5f));
        }
    }

    [Fact]
    public void CollinearVertexOnAnEdgeIsHandled()
    {
        var mesh = TestMeshes.Make([new(0, 0, 0), new(1, 0, 0), new(2, 0, 0), new(2, 2, 0), new(0, 2, 0)], [[0, 1, 2, 3, 4]]);
        var tris = Triangulator.Triangulate(mesh);
        Assert.Equal(4f, tris.Sum(t => Area(mesh, t)), 1e-5f);
    }
}
