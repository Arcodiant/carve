using System.Numerics;
using Xunit;

namespace Carve.Core.Tests;

/// <summary>Planar, box and cylindrical projection (T-55).</summary>
[Trait("Tier", "unit")]
public class UvProjectionTests
{
    private static readonly UvProjection.Rect Unit = UvProjection.Rect.Unit;

    /// <summary>The shoelace sum over (u, v): negative when the corners run counterclockwise on screen with v down (unflipped).</summary>
    private static double Shoelace(Vector2[] uv)
    {
        double sum = 0;
        for (var i = 0; i < uv.Length; i++)
        {
            var (a, b) = (uv[i], uv[(i + 1) % uv.Length]);
            sum += (double)a.X * b.Y - (double)b.X * a.Y;
        }
        return sum;
    }

    [Fact]
    public void Planar_of_the_cubes_plus_z_face_from_plus_z_gives_its_corners_exactly()
    {
        var uv = UvProjection.Planar(TestMeshes.Cube(), [1], Vector3.UnitZ, Unit)[1];
        Assert.Equal([new(0, 1), new(1, 1), new(1, 0), new(0, 0)], uv);
        Assert.True(Shoelace(uv) < 0);
    }

    [Fact]
    public void Planar_fits_a_given_rectangle_by_one_scale_and_centres()
    {
        var uv = UvProjection.Planar(TestMeshes.Cube(), [1], Vector3.UnitZ, new(2, 3, 4, 7))[1];
        Assert.Equal([new(2, 6), new(4, 6), new(4, 4), new(2, 4)], uv);
    }

    [Fact]
    public void Planar_takes_render_s_up_rule_and_any_direction_length()
    {
        // From above (+y) up is -Z and +X is right: the top face's u runs with x and v with z.
        var top = UvProjection.Planar(TestMeshes.Cube(), [3], new Vector3(0, 7, 0), Unit)[3];
        Assert.True(Shoelace(top) < 0);
        // face 3 = [3, 7, 6, 2] = (0,1,0), (0,1,1), (1,1,1), (1,1,0); -Z is up so z=1 is lower (larger v).
        Assert.Equal([new(0, 0), new(0, 1), new(1, 1), new(1, 0)], top);
    }

    [Fact]
    public void Planar_gives_one_uv_per_corner_for_every_face_asked_and_none_for_others()
    {
        var uvs = UvProjection.Planar(TestMeshes.Cube(), [1, 3], new Vector3(1, 1, 1), Unit);
        Assert.Equal([1, 3], uvs.Keys.ToArray());
        Assert.All(uvs.Values, v => Assert.Equal(4, v.Length));
    }

    [Fact]
    public void Box_of_the_cube_gives_six_squares_in_the_grid_at_one_scale_all_unflipped()
    {
        var box = UvProjection.Box(TestMeshes.Cube(), [0, 1, 2, 3, 4, 5], Unit);
        // Faces 0..5 are -z, +z, -y, +y, -x, +x; cells are +x, -x, +y / -y, +z, -z.
        var cellOf = new Dictionary<int, int> { [5] = 0, [4] = 1, [3] = 2, [2] = 3, [1] = 4, [0] = 5 };
        foreach (var (face, cell) in cellOf)
        {
            var uv = box.Uvs[face];
            Assert.True(Shoelace(uv) < 0, $"face {face} is flipped");
            Assert.All(uv, p => Assert.InRange(p.X, cell % 3 / 3f - 1e-6f, (cell % 3 + 1) / 3f + 1e-6f));
            Assert.All(uv, p => Assert.InRange(p.Y, cell / 3 / 2f - 1e-6f, (cell / 3 + 1) / 2f + 1e-6f));
            Assert.Equal(1 / 3f, uv.Max(p => p.X) - uv.Min(p => p.X), 5);
            Assert.Equal(1 / 3f, uv.Max(p => p.Y) - uv.Min(p => p.Y), 5);
            Assert.Equal((cell % 3 + 0.5f) / 3, (uv.Max(p => p.X) + uv.Min(p => p.X)) / 2, 5);
            Assert.Equal((cell / 3 + 0.5f) / 2, (uv.Max(p => p.Y) + uv.Min(p => p.Y)) / 2, 5);
        }
        Assert.Equal([5], box.Axes["+x"]);
        Assert.Equal([0], box.Axes["-z"]);
    }

    [Fact]
    public void Box_leaves_an_empty_group_s_cell_empty_and_shares_the_scale_across_groups()
    {
        // A 2 x 1 x 1 slab: the +z face is 2 wide, the +x face 1 wide; one scale means the +x face is half the +z face's width.
        var slab = TestMeshes.Make(TestMeshes.CubeVertices.Select(v => new Vector3(v.X * 2, v.Y, v.Z)), TestMeshes.CubeFaces);
        var box = UvProjection.Box(slab, [1, 5], Unit);
        Assert.Empty(box.Axes["-x"]);
        var wide = box.Uvs[1].Max(p => p.X) - box.Uvs[1].Min(p => p.X);
        var narrow = box.Uvs[5].Max(p => p.X) - box.Uvs[5].Min(p => p.X);
        Assert.Equal(wide / 2, narrow, 5);
        Assert.Equal(1 / 3f, wide, 5);
    }

    [Fact]
    public void Box_sends_a_tie_to_x_then_y_then_z()
    {
        var diagonal = TestMeshes.Make([new(0, 0, 0), new(1, -1, 0), new(1, -1, 1), new(0, 0, 1)], [[0, 1, 2, 3]]);
        var axes = UvProjection.Box(diagonal, [0], Unit).Axes;
        Assert.Single(axes, a => a.Value.Count > 0);
        Assert.Contains(axes, a => a.Value.Count > 0 && a.Key[1] == 'x');
    }

    [Fact]
    public void Cylindrical_of_the_barrel_side_puts_the_seam_at_the_minus_z_column_and_no_face_crosses_it()
    {
        var barrel = TestMeshes.Barrel();
        var uvs = UvProjection.Cylindrical(barrel, Enumerable.Range(0, 32).ToArray(), Vector3.UnitY, null, -Vector3.UnitZ, Unit);
        Assert.Equal(32, uvs.Count);
        foreach (var (face, uv) in uvs)
        {
            var (ring, k) = (face / 8, face % 8);
            Assert.True(Shoelace(uv) < 0, $"face {face} is flipped");
            // Corners 0, 1 on the lower ring, 2, 3 on the upper: u rises along the ring, v falls up the rings.
            Assert.True(uv[1].X > uv[0].X && uv[2].X > uv[3].X, $"face {face}: u does not rise");
            Assert.True(uv[0].Y > uv[3].Y && uv[1].Y > uv[2].Y, $"face {face}: v does not rise downward");
            Assert.All(uv, p => Assert.InRange(p.X, -1e-5f, 1 + 1e-5f));
            // The seam column is vertex 8i+2: u = 0 where a face starts on it, 1 where one ends on it.
            if (k == 2) Assert.Equal(0, uv[0].X, 4);
            if (k == 1) Assert.Equal(1, uv[1].X, 4);
        }
    }

    [Fact]
    public void Cylindrical_keeps_a_face_that_crosses_the_seam_together()
    {
        var uvs = UvProjection.Cylindrical(TestMeshes.Barrel(), Enumerable.Range(0, 32).ToArray(), Vector3.UnitY, null, new Vector3(1, 0, -0.3f), Unit);
        Assert.All(uvs.Values, uv => Assert.True(uv.Max(p => p.X) - uv.Min(p => p.X) < 0.25f));
        Assert.Contains(uvs.Values, uv => uv.Max(p => p.X) > 1);
    }

    [Fact]
    public void Cylindrical_caps_have_no_uv_area_and_the_default_into_and_a_given_one_both_fit()
    {
        var barrel = TestMeshes.Barrel();
        var caps = UvProjection.Cylindrical(barrel, [32, 33], Vector3.UnitY, null, -Vector3.UnitZ, Unit);
        Assert.All(caps.Values, uv => Assert.Equal(0, Shoelace(uv), 6));

        var into = new UvProjection.Rect(2, 1, 6, 3);
        var uvs = UvProjection.Cylindrical(barrel, Enumerable.Range(0, 32).ToArray(), Vector3.UnitY, null, -Vector3.UnitZ, into);
        var all = uvs.Values.SelectMany(v => v).ToArray();
        Assert.True(all.All(p => p.X >= 2 - 1e-5 && p.X <= 6 + 1e-5 && p.Y >= 1 - 1e-5 && p.Y <= 3 + 1e-5));
        Assert.Equal(6, all.Max(p => p.X), 4);
        Assert.Equal(2, all.Min(p => p.X), 4);
    }

    [Fact]
    public void A_seam_along_the_axis_is_refused()
    {
        Assert.Throws<ArgumentException>(() => UvProjection.Cylindrical(TestMeshes.Barrel(), [0], Vector3.UnitY, null, Vector3.UnitY, Unit));
    }
}
