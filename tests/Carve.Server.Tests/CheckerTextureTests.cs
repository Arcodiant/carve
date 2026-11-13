using System.Numerics;
using System.Security.Cryptography;
using Carve.Core.Tests;
using Carve.Scenario.Crate;
using Empyrean;

namespace Carve.Server.Tests;

/// <summary>T-57: the checker texture and the checker look's CPU side.</summary>
[Trait("Tier", "unit")]
public sealed class CheckerTextureTests
{
    private static uint PixelAt(int x, int y)
    {
        var p = CheckerTexture.Pixels.Span.Slice((y * CheckerTexture.Size + x) * 4, 4);
        Assert.Equal(255, p[3]);
        return (uint)(p[0] << 16 | p[1] << 8 | p[2]);
    }

    [Fact]
    public void The_checker_is_256_square_with_white_at_the_origin_and_hues_by_column()
    {
        Assert.Equal(256 * 256 * 4, CheckerTexture.Pixels.Length);
        Assert.Equal(0xFFFFFFu, PixelAt(16, 16));
        Assert.Equal(0xB050C0u, PixelAt(7 * 32 + 16, 32 + 16));
        Assert.Equal(0x612C6Au, PixelAt(7 * 32 + 16, 16));
        Assert.Equal(0x7B2323u, PixelAt(16, 7 * 32 + 16));
        uint[] dark = [0x7B2323, 0x7B4C1A, 0x776E1A, 0x2C612C, 0x23616A, 0x233E7B, 0x3A2C72, 0x612C6A];
        for (var c = 0; c < 8; c++)
        {
            Assert.Equal(dark[c], CheckerTexture.Cell(c, 1 - c % 2));
            Assert.Equal(CheckerTexture.Hues[c], CheckerTexture.Cell(c, c % 2 == 0 && c > 0 ? 0 : c % 2 == 0 ? 2 : 1));
        }

        Assert.Equal(CheckerTexture.Hues, CheckerColours.Hues);
        Assert.Equal(new Vector2(1 / 16f, 1 / 16f), CheckerTexture.WhiteCentre);
    }

    [Fact]
    public void The_checker_s_bytes_are_pinned() =>
        Assert.Equal(Pinned, Convert.ToHexString(SHA256.HashData(CheckerTexture.Pixels.Span)));

    private const string Pinned = "27EBA66D119AAC9255DBD826EB165AE918976FF5123D226367C0CADC741B53EC";

    [Fact]
    public void The_checker_look_lays_corner_uvs_a_c_b_white_mapped_magenta_unmapped_orange_highlighted()
    {
        var quad = TestMeshes.Make([new(0, 0, 0), new(1, 0, 0), new(1, 1, 0), new(0, 1, 0), new(0, 0, 1)], [[0, 1, 2, 3], [0, 4, 1]]);
        Vector2[] corners = [new(0, 1), new(1, 1), new(1, 0), new(0, 0)];
        var mesh = quad.WithUvs(Carve.Core.MeshUvs.None.WithFace(0, corners));
        var space = DrawnSpace.Of(Vector3.Zero, Vector3.One);

        var scene = LookMeshes.Compose(mesh, space, Look.Checker, Highlight.None);

        var triangles = Carve.Core.Triangulator.Triangulate(mesh);
        Assert.Equal(triangles.Count * 3, scene.Uvs!.Length);
        Assert.Null(scene.FlatColour);
        Assert.Null(scene.EdgeLines);
        for (var t = 0; t < triangles.Count; t++)
        {
            var (a, b, c, face) = triangles[t];
            Vector2[] want = face == 0 ? [corners[a], corners[c], corners[b]] : [CheckerTexture.WhiteCentre, CheckerTexture.WhiteCentre, CheckerTexture.WhiteCentre];
            Assert.Equal(want, scene.Uvs[(t * 3)..(t * 3 + 3)]);
            Assert.Equal(face == 0 ? Rgba8.White : Rgba8.FromVector(LookMeshes.Unmapped), scene.VertexColours![t * 3]);
        }

        var picked = LookMeshes.Compose(mesh, space, Look.Checker, Highlight.Of([], [], [0]));
        Assert.Equal(Rgba8.FromVector(new Vector3(LookMeshes.Orange.X, LookMeshes.Orange.Y, LookMeshes.Orange.Z)), picked.VertexColours![0]);

        // Every other look is unchanged by UVs: no UV buffer.
        foreach (var look in new[] { Look.Shaded, Look.Wireframe, Look.Normals })
        {
            Assert.Null(LookMeshes.Compose(mesh, space, look, Highlight.None).Uvs);
            Assert.Equal(LookMeshes.Compose(quad, space, look, Highlight.None).VertexColours, LookMeshes.Compose(mesh, space, look, Highlight.None).VertexColours);
        }

        Assert.Equal(MeshBridge.ToMeshData(quad, space).Vertices!.ToArray(), MeshBridge.ToMeshData(mesh, space).Vertices!.ToArray());
    }
}
