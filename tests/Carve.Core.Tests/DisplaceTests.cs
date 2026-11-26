using System.Numerics;
using Xunit;
using static Carve.Core.Tests.TestMeshes;

namespace Carve.Core.Tests;

/// <summary>T-223's displace: determinism, a hand-worked ramp, seams that stay shut, and the refusals.</summary>
[Trait("Tier", "unit")]
public class DisplaceTests
{
    private static readonly NoiseSource Rough = new(7, 0.5, 4);

    private static PolygonMesh Run(PolygonMesh mesh, DisplaceSource source, float amount, float midlevel = 0.5f, Vector3? axis = null,
        IReadOnlyCollection<int>? vertices = null) =>
        Ok(Displacement.Displace(mesh, vertices, source, amount, midlevel, axis, out _));

    /// <summary>A 4 x 4 ramp, texel (x, y) = (x + 4y) / 16.</summary>
    private static HeightField Ramp() => new(4, 4, [.. Enumerable.Range(0, 16).Select(i => i / 16f)]);

    /// <summary>Grid(3, 3) with vertex (i, j) at uv ((i + 0.5) / 4, (j + 0.5) / 4): each vertex sits on texel (i, j)'s centre.</summary>
    private static PolygonMesh MappedGrid()
    {
        var grid = Grid(3, 3);
        var uvs = grid.LiveFaces.Select(f => KeyValuePair.Create(f, (IReadOnlyList<Vector2>?)grid.Faces[f].Corners
            .Select(v => new Vector2((v % 4 + 0.5f) / 4, (v / 4 + 0.5f) / 4)).ToArray()));
        return grid.WithUvs(MeshUvs.None.WithFaces(uvs));
    }

    [Fact]
    public void The_same_seed_and_arguments_give_bit_identical_positions_and_another_seed_does_not()
    {
        var cube = Ok(MeshEdits.Subdivide(Cube(), 2));
        var a = Run(cube, Rough, 0.3f);
        var b = Run(cube, Rough, 0.3f);
        Assert.Equal(a.Vertices.Select(BitsOf), b.Vertices.Select(BitsOf));
        Assert.NotEqual(a.Vertices, cube.Vertices);
        Assert.NotEqual(a.Vertices, Run(cube, Rough with { Seed = 8 }, 0.3f).Vertices);
    }

    private static (int, int, int) BitsOf(Vector3 v) => (BitConverter.SingleToInt32Bits(v.X), BitConverter.SingleToInt32Bits(v.Y), BitConverter.SingleToInt32Bits(v.Z));

    [Fact]
    public void A_flat_grid_displaced_by_a_ramp_rises_to_the_hand_worked_heights()
    {
        // Normal +Z everywhere; vertex (i, j) on texel (i, j), so h = (i + 4j) / 16 and z = 2 * (h - 0.25).
        var mesh = Displacement.Displace(MappedGrid(), null, new FieldSource(Ramp(), "ramp"), 2, 0.25f, null, out var report);
        var displaced = Ok(mesh);
        for (var v = 0; v < 16; v++)
        {
            var (i, j) = (v % 4, v / 4);
            Assert.Equal(new Vector3(0.25f * i, 0.25f * j, 2 * ((i + 4 * j) / 16f - 0.25f)), displaced.Vertices[v]);
        }
        Assert.Equal(2 * (15 / 16.0 - 0.25), report!.Largest, 6);
    }

    [Fact]
    public void Bilinear_sampling_between_texel_centres_and_across_the_wrap()
    {
        var ramp = Ramp();
        Assert.Equal((0 + 1) / 2.0 / 16, ramp.Sample(new Vector2(0.25f, 0.125f)), 6);
        // u = 0 is halfway between texel 3 and texel 0 of the row, wrapping.
        Assert.Equal((3 + 0) / 2.0 / 16, ramp.Sample(new Vector2(0, 0.125f)), 6);
    }

    [Fact]
    public void An_axis_replaces_the_normal_and_a_selection_moves_only_its_vertices()
    {
        var displaced = Run(MappedGrid(), new FieldSource(Ramp(), "ramp"), 1, 0, new Vector3(0, 1, 4), [5]);
        var expected = Grid(3, 3).Vertices[5] + Vector3.Normalize(new Vector3(0, 1, 4)) * (5 / 16f);
        Assert.True(Vector3.Distance(expected, displaced.Vertices[5]) < 1e-6f, $"{expected} vs {displaced.Vertices[5]}");
        Assert.Equal(Grid(3, 3).Vertices.Where((_, v) => v != 5), displaced.Vertices.Where((_, v) => v != 5));
    }

    [Fact]
    public void A_closed_box_mapped_cube_stays_closed_and_a_seam_vertex_takes_the_mean_of_its_samples()
    {
        var cube = Ok(MeshEdits.Subdivide(Cube(), 1));
        var box = UvProjection.Box(cube, [.. cube.LiveFaces], UvProjection.Rect.Unit);
        cube = cube.WithUvs(MeshUvs.None.WithFaces(box.Uvs.Select(p => KeyValuePair.Create(p.Key, (IReadOnlyList<Vector2>?)p.Value))));
        var field = new HeightField(8, 8, [.. Enumerable.Range(0, 64).Select(i => (i * 37 % 64) / 64f)]);

        var displaced = Run(cube, new FieldSource(field, "f"), 0.2f);

        Assert.True(MeshReport.Create(displaced).Closed);
        Assert.Equal(cube.Vertices.Count, displaced.Vertices.Count);
        Assert.Equal(cube.Faces, displaced.Faces);
        Assert.Equal(cube.Uvs!.MappedFaces, displaced.Uvs!.MappedFaces);
        // Vertex 0, a cube corner, has a corner on three faces in three islands: its height is the mean of the three samples.
        var samples = cube.LiveFaces.Where(f => cube.Faces[f].Corners.Contains(0))
            .Select(f => field.Sample(cube.Uvs.Of(f)![cube.Faces[f].CornerOf(0)])).ToArray();
        Assert.Equal(3, samples.Length);
        Assert.True(samples.Distinct().Count() > 1);
        var normal = Vector3.Normalize(cube.LiveFaces.Where(f => cube.Faces[f].Corners.Contains(0))
            .Aggregate(Vector3.Zero, (n, f) => n + cube.FaceNormal(f) * (float)cube.FaceArea(f)));
        var expected = cube.Vertices[0] + normal * (float)(0.2f * (samples.Average() - 0.5f));
        Assert.True(Vector3.Distance(expected, displaced.Vertices[0]) < 1e-6f, $"{expected} vs {displaced.Vertices[0]}");
    }

    [Fact]
    public void Noise_is_in_range_and_varies()
    {
        var values = Enumerable.Range(0, 500).Select(i => Noise.Height(new Vector3(i * 0.137f, i * 0.071f, i * 0.029f), Rough)).ToArray();
        Assert.All(values, h => Assert.InRange(h, 0, 1));
        Assert.True(values.Max() - values.Min() > 0.3, $"{values.Min()}..{values.Max()}");
    }

    [Fact]
    public void Refusals_name_the_argument()
    {
        string Error(PolygonMesh mesh, DisplaceSource source, float amount = 1, float mid = 0.5f, Vector3? axis = null, IReadOnlyCollection<int>? vertices = null)
        {
            var result = Displacement.Displace(mesh, vertices, source, amount, mid, axis, out _);
            Assert.False(result.Succeeded);
            return string.Join("; ", result.Errors);
        }
        var field = new FieldSource(Ramp(), "ramp");
        Assert.Contains("project_uvs", Error(Cube(), field));
        Assert.Contains("amount: is zero", Error(Cube(), Rough, amount: 0));
        Assert.Contains("midlevel", Error(Cube(), Rough, mid: 1.5f));
        Assert.Contains("axis: is zero", Error(Cube(), Rough, axis: Vector3.Zero));
        Assert.Contains("selection: matches no vertex", Error(Cube(), Rough, vertices: []));
        Assert.Contains("vertices[", Error(Cube(), Rough, vertices: [99]));
        Assert.Contains("octaves", Error(Cube(), Rough with { Octaves = 9 }));
        Assert.Contains("scale", Error(Cube(), Rough with { Scale = 0 }));
    }

    [Fact]
    public void A_coarse_mesh_is_warned_of_and_a_fine_one_is_not()
    {
        Displacement.Displace(Cube(), null, Rough, 0.3f, 0.5f, null, out var coarse);
        Assert.Contains(coarse!.Warnings, w => w.Contains("too coarse for the noise"));
        Displacement.Displace(Ok(MeshEdits.Subdivide(Cube(), 3)), null, Rough, 0.3f, 0.5f, null, out var fine);
        Assert.Empty(fine!.Warnings);
        // The ramp rises once across 4 texels; a grid with 4 vertices across shows it.
        Displacement.Displace(MappedGrid(), null, new FieldSource(Ramp(), "ramp"), 1, 0.5f, null, out var grid);
        Assert.Empty(grid!.Warnings);
        var busy = new HeightField(16, 16, [.. Enumerable.Range(0, 256).Select(i => (float)((i % 16 + i / 16) % 2))]);
        Displacement.Displace(MappedGrid(), null, new FieldSource(busy, "checks"), 1, 0.5f, null, out var checks);
        Assert.Contains(checks!.Warnings, w => w.Contains("too coarse for checks"));
    }

    [Fact]
    public void An_unattached_vertex_without_an_axis_is_left_out_and_named_and_with_one_it_moves()
    {
        var mesh = Make([.. CubeVertices, new Vector3(3, 3, 3)], CubeFaces);
        var result = Displacement.Displace(mesh, [8, 0], Rough, 1, 0, null, out var report);
        Assert.DoesNotContain(8, result.MovedVertices);
        Assert.Equal(new Vector3(3, 3, 3), Ok(result).Vertices[8]);
        Assert.Contains(report!.Warnings, w => w.StartsWith("vertices 8 are on no face"));

        var alone = Displacement.Displace(mesh, [8], Rough, 1, 0, null, out _);
        Assert.Contains("matches no vertex with a normal", string.Join("; ", alone.Errors));

        var lifted = Ok(Displacement.Displace(mesh, [8], Rough, 1, 0, Vector3.UnitY, out _));
        Assert.NotEqual(new Vector3(3, 3, 3), lifted.Vertices[8]);
    }
}
