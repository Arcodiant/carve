using System.Numerics;
using CarveMaterial = Carve.Core.Material;
using Carve.Core;
using Carve.Core.Tests;

namespace Carve.Server.Tests;

/// <summary>T-78: the submeshes of a mesh with materials, in the export's primitive order, and a colour-only mesh unchanged.</summary>
[Trait("Tier", "unit")]
public sealed class MaterialSubmeshTests
{
    private static PolygonMesh Cube() => TestMeshes.Make(
        [new(0, 0, 0), new(1, 0, 0), new(1, 1, 0), new(0, 1, 0), new(0, 0, 1), new(1, 0, 1), new(1, 1, 1), new(0, 1, 1)],
        [[0, 3, 2, 1], [4, 5, 6, 7], [0, 1, 5, 4], [3, 7, 6, 2], [0, 4, 7, 3], [1, 2, 6, 5]]);

    private static readonly DrawnSpace Space = DrawnSpace.Of(Vector3.Zero, Vector3.One);

    [Fact]
    public void A_mesh_of_colours_only_gets_exactly_the_buffers_it_always_had()
    {
        var mesh = Cube().WithStyle(FaceStyle.None.WithColour([2], "#ff0000").WithColour([4], "#00FF00"))
            .WithUvs(MeshUvs.None.WithFaces([KeyValuePair.Create(1, (IReadOnlyList<Vector2>?)[new(0, 1), new(1, 1), new(1, 0), new(0, 0)])]));
        var plain = MeshBridge.ToMeshData(mesh, Space);
        var cut = MeshBridge.ToSubmeshedMeshData(mesh, Space, out var colours, out var materials);
        Assert.Equal(plain.Vertices!, cut.Vertices!);
        Assert.All(cut.Vertices!, v => Assert.Equal(Vector2.Zero, v.TexCoord));
        Assert.Equal(new string?[] { null, "#FF0000", "#00FF00" }, colours);
        Assert.All(materials, Assert.Null);
    }

    [Fact]
    public void Materials_follow_the_colours_in_order_of_first_use_with_the_exports_corner_uvs()
    {
        var mesh = Cube().WithStyle(FaceStyle.None.WithMaterial([5], "b").WithColour([0], "#123456").WithMaterial([1, 3], "a"))
            .WithUvs(MeshUvs.None.WithFaces([KeyValuePair.Create(1, (IReadOnlyList<Vector2>?)[new(0, 1), new(1, 1), new(1, 0), new(0, 0)])]));
        var cut = MeshBridge.ToSubmeshedMeshData(mesh, Space, out var colours, out var materials);
        // The export's order: uncoloured, colours, then materials by first face (face 1's 'a' before face 5's 'b').
        Assert.Equal(new string?[] { null, "#123456", null, null }, colours);
        Assert.Equal(new string?[] { null, null, "a", "b" }, materials);
        var cube = new[] { new CarveMaterial("a"), new CarveMaterial("b") };
        Assert.Equal(new[] { "carve", "carve #123456", "a", "b" }, GlbExport.MaterialNames(mesh, cube));
        // Each corner's own UV (A, C, B per triangle), (0, 0) on an unmapped face; the checker's white centre nowhere.
        var expected = LookMeshes.CornerUvs(mesh, Vector2.Zero);
        Assert.Equal(expected, cut.Vertices!.Select(v => v.TexCoord).ToArray());
        Assert.Contains(new Vector2(1, 1), expected);
        Assert.DoesNotContain(CheckerTexture.WhiteCentre, expected);
        Assert.Equal(12, cut.Submeshes!.Sum(s => s.IndexCount) / 3);
    }
}
