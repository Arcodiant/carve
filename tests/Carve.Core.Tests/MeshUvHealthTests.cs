using System.Numerics;
using Xunit;

namespace Carve.Core.Tests;

/// <summary>check's UV warnings on small meshes worked by hand (T-60).</summary>
[Trait("Tier", "unit")]
public class MeshUvHealthTests
{
    // Two unit squares side by side in z = 0, face 0 on x 0..1 and face 1 on x 1..2, both wound counter-clockwise from +z.
    private static readonly Vector3[] Vertices = [new(0, 0, 0), new(1, 0, 0), new(2, 0, 0), new(0, 1, 0), new(1, 1, 0), new(2, 1, 0)];
    private static readonly int[][] Faces = [[0, 1, 4, 3], [1, 2, 5, 4]];

    /// <summary>The rectangle's corners going down, right and up: counter-clockwise on screen with v down, which is unflipped.</summary>
    private static Vector2[] Unflipped(float u0, float v0, float u1, float v1) => [new(u0, v0), new(u0, v1), new(u1, v1), new(u1, v0)];

    private static Vector2[] Flipped(float u0, float v0, float u1, float v1) => [new(u0, v0), new(u1, v0), new(u1, v1), new(u0, v1)];

    private static PolygonMesh Mapped(params (int Face, Vector2[]? Uv)[] faces)
        => TestMeshes.Make(Vertices, Faces).WithUvs(MeshUvs.None.WithFaces(faces.Select(f => KeyValuePair.Create(f.Face, (IReadOnlyList<Vector2>?)f.Uv))));

    private static string[] Warnings(PolygonMesh mesh) => [.. MeshHealth.Create(mesh).Warnings.Select(w => w.ToString())];

    private static string[] UvWarnings(PolygonMesh mesh) => [.. Warnings(mesh).Where(w => w.Contains("UVs") || w.Contains("no UVs"))];

    [Fact]
    public void A_mesh_with_no_UVs_gets_what_it_always_did()
    {
        var mesh = TestMeshes.Make(Vertices, Faces);
        var health = MeshHealth.Create(mesh);
        Assert.Equal(MeshReport.Create(mesh).Warnings.Select(w => w.ToString()), health.Warnings.Select(w => w.ToString()));
        Assert.Null(health.Stats.Uvs);

        // Seams alone are not UVs.
        var seamed = mesh.WithUvs(MeshUvs.None.WithSeams([(1, 4)], true));
        Assert.Null(MeshHealth.Create(seamed).Stats.Uvs);
        Assert.Equal(Warnings(mesh), Warnings(seamed));
    }

    [Fact]
    public void A_clean_mapping_has_no_UV_warnings_and_a_shared_edge_is_not_an_overlap()
    {
        var mesh = Mapped((0, Unflipped(0, 0, 0.5f, 1)), (1, Unflipped(0.5f, 0, 1, 1)));
        var health = MeshHealth.Create(mesh);
        Assert.Empty(UvWarnings(mesh));
        Assert.Equal(new UvStats(2, 0), health.Stats.Uvs);
    }

    [Fact]
    public void An_unmapped_face_is_warned_of_when_the_mesh_has_UVs()
    {
        var mesh = Mapped((0, Unflipped(0, 0, 1, 1)));
        Assert.Equal(["faces[1]: no UVs, so the face samples the texture's corner"], UvWarnings(mesh));
        Assert.Equal(new UvStats(1, 1), MeshHealth.Create(mesh).Stats.Uvs);
    }

    [Fact]
    public void A_flipped_face_is_warned_of()
    {
        var mesh = Mapped((0, Flipped(0, 0, 0.5f, 1)), (1, Unflipped(0.5f, 0, 1, 1)));
        Assert.Equal(["faces[0]: UVs flipped, so the texture is mirrored on the face"], UvWarnings(mesh));
    }

    [Fact]
    public void A_face_of_zero_area_is_warned_of_once_and_not_as_flipped()
    {
        // All four corners on one line, and four on one point.
        var line = Mapped((0, [new(0, 0), new(0.25f, 0.25f), new(0.5f, 0.5f), new(1, 1)]), (1, Unflipped(2, 0, 3, 1)));
        Assert.Equal(["faces[0]: UVs of zero area, so the face is one smeared texel"], UvWarnings(line));
        var point = Mapped((0, [new(0.5f, 0.5f), new(0.5f, 0.5f), new(0.5f, 0.5f), new(0.5f, 0.5f)]), (1, Unflipped(2, 0, 3, 1)));
        Assert.Equal(["faces[0]: UVs of zero area, so the face is one smeared texel"], UvWarnings(point));
    }

    [Fact]
    public void A_sliver_above_the_area_tolerance_is_not_zero_area()
    {
        // Area 1e-6 x 1 against a diagonal squared of about 1: far above 1e-9.
        var mesh = Mapped((0, Unflipped(0, 0, 1e-6f, 1)), (1, Unflipped(2, 0, 3, 1)));
        Assert.Empty(UvWarnings(mesh));
    }

    [Fact]
    public void Overlap_is_warned_of_just_over_1e_6_and_not_just_under()
    {
        var over = Mapped((0, Unflipped(0, 0, 1, 1)), (1, Unflipped(1 - 1.2e-6f, 0, 2, 1)));
        Assert.Equal(["faces[0]: UVs overlap faces[1], so the two faces show the same texels; mirrored halves sharing a texture area are deliberate and still warned of"], UvWarnings(over));

        var under = Mapped((0, Unflipped(0, 0, 1, 1)), (1, Unflipped(1 - 0.8e-6f, 0, 2, 1)));
        Assert.Empty(UvWarnings(under));
    }

    [Fact]
    public void Mirrored_halves_on_the_same_area_overlap_and_the_pair_is_listed_once()
    {
        var mesh = Mapped((0, Unflipped(0, 0, 1, 1)), (1, Flipped(0, 0, 1, 1)));
        Assert.Equal(
            [
                "faces[1]: UVs flipped, so the texture is mirrored on the face",
                "faces[0]: UVs overlap faces[1], so the two faces show the same texels; mirrored halves sharing a texture area are deliberate and still warned of",
            ],
            UvWarnings(mesh));
    }

    [Fact]
    public void A_hole_is_not_covered_so_a_face_in_the_hole_does_not_overlap()
    {
        Vector3[] vertices = [new(0, 0, 0), new(4, 0, 0), new(4, 4, 0), new(0, 4, 0), new(1, 1, 0), new(1, 3, 0), new(3, 3, 0), new(3, 1, 0)];
        var holed = new Face([0, 3, 2, 1], [[4, 7, 6, 5]]);
        var mesh = PolygonMesh.Create(vertices, [holed, new Face([4, 5, 6, 7])]);
        Assert.True(mesh.Succeeded, string.Join("; ", mesh.Errors));
        Vector2 P(Vector3 v) => new(v.X, v.Y);
        var uvs = MeshUvs.None
            .WithFace(0, holed.Corners.Select(i => P(vertices[i])).ToArray())
            .WithFace(1, new[] { 4, 5, 6, 7 }.Select(i => P(vertices[i])).ToArray());
        var mapped = mesh.Mesh!.WithUvs(uvs);
        Assert.Empty(UvWarnings(mapped));
        Assert.Equal(-12.0, UvArea.Signed(holed, uvs.Of(0)!));

        // Move the filling face's UVs out of the hole, into the ring, and it overlaps.
        var onRing = mapped.WithUvs(uvs.WithFace(1, [new(0, 0), new(0, 1), new(1, 1), new(1, 0)]));
        Assert.Equal(["faces[0]: UVs overlap faces[1], so the two faces show the same texels; mirrored halves sharing a texture area are deliberate and still warned of"], UvWarnings(onRing));
    }
}
