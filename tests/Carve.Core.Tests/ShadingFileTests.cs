using System.Numerics;
using Xunit;
using static Carve.Core.Tests.TestMeshes;

namespace Carve.Core.Tests;

/// <summary>Version 3 of the Carve file (shading and hard edges), the versions before it, and corner normals under a pose.</summary>
[Trait("Tier", "unit")]
public class ShadingFileTests
{
    [Fact]
    public void A_model_with_no_smooth_mesh_and_no_hard_edge_still_writes_version_2_with_no_new_key()
    {
        var text = CarveFile.Write(Cube());
        Assert.Contains("\"carve\": 2,", text);
        Assert.DoesNotContain("shading", text);
        Assert.DoesNotContain("hard_edges", text);
    }

    [Fact]
    public void Shading_and_hard_edges_write_as_version_3_and_read_back_the_same()
    {
        var mesh = Cube().WithShade(MeshShade.Of(ShadingMode.Smooth, [(3, 2), (0, 1)]));
        var text = CarveFile.Write(Model.Of("body", mesh).With("flat", Cube()));
        Assert.Contains("\"carve\": 3,", text);
        Assert.Contains("\"shading\": \"smooth\",", text);
        Assert.Contains("\"hard_edges\": [\n", text);
        var read = CarveFile.Read(text);
        Assert.True(read.Model is not null, string.Join("; ", read.Errors));
        Assert.Equal(3, read.Version);
        var body = read.Model!.Meshes[0].Mesh;
        Assert.Equal(ShadingMode.Smooth, body.ShadingMode);
        Assert.Equal([(0, 1), (2, 3)], body.HardEdges);
        Assert.Equal(ShadingMode.Flat, read.Model.Meshes[1].Mesh.ShadingMode);
        Assert.Equal(text, CarveFile.Write(read.Model));
    }

    [Fact]
    public void Flat_with_marked_edges_is_kept_for_later()
    {
        var text = CarveFile.Write(Cube().WithShade(MeshShade.Of(ShadingMode.Flat, [(0, 1)])));
        var mesh = CarveFile.Read(text).Mesh!;
        Assert.Equal(ShadingMode.Flat, mesh.ShadingMode);
        Assert.Equal([(0, 1)], mesh.HardEdges);
    }

    [Fact]
    public void Versions_1_and_2_still_read_and_a_version_2_file_may_not_carry_the_version_3_keys()
    {
        var v2 = CarveFile.Write(Cube());
        Assert.NotNull(CarveFile.Read(v2).Mesh);
        var sneaked = v2.Replace("\"weights\": []", "\"weights\": [], \"shading\": \"smooth\"");
        Assert.NotEqual(v2, sneaked);
        Assert.Null(CarveFile.Read(sneaked).Model);
    }

    [Theory]
    [InlineData("\"shading\": \"round\"", "shading")]
    [InlineData("\"shading\": \"flat\", \"hard_edges\": [[0, 6]]", "not an edge")]
    [InlineData("\"shading\": \"flat\", \"hard_edges\": [[0, 1], [1, 0]]", "listed earlier")]
    [InlineData("\"shading\": \"flat\", \"hard_edges\": [[0]]", "not an edge")]
    public void A_bad_shading_or_hard_edge_is_refused_naming_where(string keys, string message)
    {
        var text = CarveFile.Write(Cube().WithShade(MeshShade.Of(ShadingMode.Smooth, [(0, 1)])));
        var start = text.IndexOf("\"shading\"", StringComparison.Ordinal);
        var end = text.LastIndexOf(']', text.LastIndexOf("\"hard_edges\"", StringComparison.Ordinal) + 40);
        var bad = text[..start] + keys + text[(end + 1)..];
        var result = CarveFile.Read(bad);
        Assert.Null(result.Model);
        Assert.Contains(result.Errors, e => e.Element.StartsWith("meshes[0]") && e.Message.Contains(message));
    }

    [Fact]
    public void Posed_corner_normals_turn_with_the_bone_and_a_posed_model_draws_them()
    {
        var skeleton = Skeleton.Create([new("base", null, Vector3.Zero, Quaternion.Identity)]).Skeleton!;
        var cube = Cube().WithShade(MeshShade.Flat.WithMode(ShadingMode.Smooth))
            .WithWeights(VertexWeights.Of(Enumerable.Range(0, 8).ToDictionary(v => v, v => new[] { new Influence(0, 1.0) })));
        var quarter = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 2);
        var pose = new Pose(new Dictionary<string, (Quaternion?, Vector3?)> { ["base"] = (quarter, null) });

        var rest = Shading.Corners(cube);
        var posed = Skinning.CornerNormals(cube, skeleton, pose);
        for (var f = 0; f < rest.Length; f++)
            for (var k = 0; k < rest[f].Length; k++)
                Assert.True((Vector3.Transform(rest[f][k], quarter) - posed[f][k]).Length() < 1e-5f);

        var model = Skinning.Posed(Model.Of("body", cube).WithSkeleton(skeleton), pose);
        var drawn = Shading.CornersOf(model.Combined!);
        for (var f = 0; f < posed.Length; f++)
            for (var k = 0; k < posed[f].Length; k++) Assert.Equal(posed[f][k], drawn[f][k]);
    }

    [Fact]
    public void A_posed_flat_mesh_is_lit_by_its_skinned_corners_and_shows_its_faces()
    {
        // RV-52 finding 9, held by RV-53: the export writes a flat face's normal at each corner and a game skins it, so a posed
        // flat mesh is lit by those skinned corners, not by its posed polygons' own normals. Vertex 0 follows "base" alone and the
        // rest are blended half and half with a quarter-turned "tip", so the faces at vertex 0 bend and the two differ there.
        var skeleton = Skeleton.Create([new("base", null, Vector3.Zero, Quaternion.Identity), new("tip", "base", Vector3.Zero, Quaternion.Identity)]).Skeleton!;
        var cube = Cube().WithWeights(VertexWeights.Of(Enumerable.Range(0, 8).ToDictionary(v => v,
            v => v == 0 ? new[] { new Influence(0, 1.0) } : [new Influence(0, 0.5), new Influence(1, 0.5)])));
        Assert.Equal(ShadingMode.Flat, cube.ShadingMode);
        var quarter = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 2);
        var pose = new Pose(new Dictionary<string, (Quaternion?, Vector3?)> { ["tip"] = (quarter, null) });

        var skinned = Skinning.CornerNormals(cube, skeleton, pose);
        var combined = Skinning.Posed(Model.Of("body", cube).WithSkeleton(skeleton), pose).Combined!;
        Assert.True(Shading.HasCornerNormals(combined), "a posed flat mesh carries no skinned corners: it is lit by its posed polygons");
        var drawn = Shading.CornersOf(combined);
        for (var f = 0; f < skinned.Length; f++)
        {
            Assert.False(Shading.FaceHasCornerNormals(combined, f), $"face {f} of a posed flat mesh shows corner normals in the normals look");
            for (var k = 0; k < skinned[f].Length; k++)
                Assert.True((skinned[f][k] - drawn[f][k]).Length() < 1e-5f, $"face {f} corner {k}: lit by {drawn[f][k]}, skinned {skinned[f][k]}");
        }

        // The case is one the fix changes: some corner's skinned normal is not its posed polygon's own.
        var own = Shading.Corners(cube.WithVertices(combined.Vertices));
        Assert.Contains(Enumerable.Range(0, skinned.Length), f => Enumerable.Range(0, skinned[f].Length).Any(k => (skinned[f][k] - own[f][k]).Length() > 1e-3f));
    }
}
