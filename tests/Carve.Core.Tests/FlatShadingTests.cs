using Xunit;
using static Carve.Core.Tests.TestMeshes;

namespace Carve.Core.Tests;

[Trait("Tier", "unit")]
public class FlatShadingTests
{
    public static TheoryData<string> Meshes => ["cube", "lblock"];

    [Theory]
    [MemberData(nameof(Meshes))]
    public void Every_triangle_in_Triangulator_order_with_the_meshs_positions_and_its_faces_normal(string name)
    {
        var mesh = name == "cube" ? Cube() : LBlock();
        var triangles = Triangulator.Triangulate(mesh);
        var flat = FlatShading.Triangles(mesh);

        Assert.Equal(triangles.Count, flat.Count);
        for (var t = 0; t < triangles.Count; t++)
        {
            var expected = new FlatTriangle(mesh.Vertices[triangles[t].A], mesh.Vertices[triangles[t].B], mesh.Vertices[triangles[t].C],
                mesh.FaceNormal(triangles[t].Face), triangles[t].Face);
            Assert.Equal(expected, flat[t]);
        }
    }
}
