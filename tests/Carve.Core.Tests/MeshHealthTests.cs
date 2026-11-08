using System.Globalization;
using System.Numerics;
using Xunit;

namespace Carve.Core.Tests;

[Trait("Tier", "unit")]
public class MeshHealthTests
{
    private static readonly int[][] Reversed = TestMeshes.CubeFaces.Select(f => f.Reverse().ToArray()).ToArray();

    /// <summary>Two cubes, the second moved by <paramref name="offset"/>, its faces after the first's.</summary>
    private static PolygonMesh TwoCubes(Vector3 offset, int[][]? secondFaces = null) => TestMeshes.Make(
        TestMeshes.CubeVertices.Concat(TestMeshes.CubeVertices.Select(v => v + offset)),
        TestMeshes.CubeFaces.Concat((secondFaces ?? TestMeshes.CubeFaces).Select(f => f.Select(i => i + 8).ToArray())));

    private static string RepoFile(string path)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Carve.slnx"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, path);
    }

    [Fact]
    public void The_cube_is_ok_with_area_6_volume_1_and_one_shell()
    {
        var health = MeshHealth.Create(TestMeshes.Cube());

        Assert.True(health.Ok);
        Assert.Empty(health.Problems);
        Assert.Empty(health.Warnings);
        Assert.Equal(["size 1 x 1 x 1 m", "base at y = 0", "origin inside the bounds"], health.Notes);
        Assert.Equal(new HealthStats(8, 6, 12, 12, 1, true, Vector3.Zero, Vector3.One, Vector3.One, 6, 1), health.Stats);
        var shell = Assert.Single(health.Shells);
        Assert.Equal(TestMeshes.AllCubeFaces, shell.Faces);
        Assert.True(shell.Closed);
        Assert.True(shell.ConsistentWinding);
        Assert.Equal(1, shell.SignedVolume);
    }

    [Fact]
    public void The_cube_reversed_is_inside_out()
    {
        var health = MeshHealth.Create(TestMeshes.Make(TestMeshes.CubeVertices, Reversed));

        var problem = Assert.Single(health.Problems);
        Assert.Equal("inside-out", problem.Check);
        Assert.Equal("shells[0]", problem.Element);
        Assert.Equal("closed shell of 6 faces (faces 0 to 5) is inside-out: its faces are wound clockwise seen from outside, so a game culls them and sees nothing", problem.Message);
        Assert.False(health.Ok);
        Assert.Equal(-1, health.Stats.Volume);
    }

    [Fact]
    public void Volume_does_not_depend_on_where_the_shell_is()
    {
        var far = TestMeshes.Make(TestMeshes.CubeVertices.Select(v => v - new Vector3(100, 200, 300)), TestMeshes.CubeFaces);

        Assert.Equal(1, MeshHealth.Create(far).Stats.Volume);
    }

    [Fact]
    public void Volume_is_taken_from_the_shells_first_vertex_so_an_off_integer_cube_far_out_keeps_its_digits()
    {
        // RV-20: whole-number coordinates are exact either way, so this cube is 0.1 across and off the grid. Measured
        // from the world origin its volume is 0.0010004878567997366; from its first vertex, 0.0010004878640756942.
        var far = TestMeshes.Make(TestMeshes.CubeVertices.Select(v => v * 0.1f + new Vector3(1000.3f, 2000.7f, -3000.1f)), TestMeshes.CubeFaces);

        Assert.Equal(0.0010004878640756942, MeshHealth.Create(far).Stats.Volume);
    }

    /// <summary>
    /// RV-20's two-sided flat sheet: four points on the plane z = 0.37x - 0.61y + 0.3, scaled by 1.37 and moved, a quad on
    /// top and two triangles underneath, so its two sides are triangulated differently and its volume is rounding noise.
    /// </summary>
    private static PolygonMesh FlatSheet(Vector3 offset)
    {
        static Vector3 On(float x, float y) => new Vector3(x, y, 0.37f * x - 0.61f * y + 0.3f) * 1.37f;
        return TestMeshes.Make(
            new[] { On(0, 0), On(1, 0), On(1, 1), On(0, 1) }.Select(v => v + offset),
            [[0, 1, 2, 3], [0, 3, 2], [0, 2, 1]]);
    }

    [Theory]
    [InlineData(13.1f, -7.3f, 3.3f)]
    [InlineData(0f, 0f, 0f)]
    [InlineData(-5.7f, 2.9f, 11.3f)]
    [InlineData(101.3f, 47.9f, -63.1f)]
    [InlineData(-1000.3f, 2000.7f, 3000.1f)]
    public void A_flat_two_sided_sheet_is_not_inside_out_whatever_the_sign_of_its_rounding(float x, float y, float z)
    {
        var health = MeshHealth.Create(FlatSheet(new Vector3(x, y, z)));

        var shell = Assert.Single(health.Shells);
        Assert.True(shell.Closed && shell.ConsistentWinding);
        Assert.DoesNotContain(health.Problems, p => p.Check == "inside-out");
    }

    [Fact]
    public void The_flat_sheet_at_the_reviewers_offset_has_a_negative_volume_so_the_threshold_is_what_passes_it()
    {
        Assert.True(MeshHealth.Create(FlatSheet(new Vector3(13.1f, -7.3f, 3.3f))).Shells[0].SignedVolume < 0);
    }

    [Fact]
    public void A_small_inside_out_cube_far_out_is_still_inside_out()
    {
        // 1 mm across and 1 km out: the threshold goes by the shell's own size, not its distance.
        var tiny = TestMeshes.Make(TestMeshes.CubeVertices.Select(v => v * 0.001f + new Vector3(1000, 0, 0)), Reversed);

        Assert.Equal("inside-out", Assert.Single(MeshHealth.Create(tiny).Problems).Check);
    }

    [Fact]
    public void Two_cubes_apart_are_two_shells_and_a_note()
    {
        var health = MeshHealth.Create(TwoCubes(new Vector3(3, 0, 0)));

        Assert.True(health.Ok);
        Assert.Equal(2, health.Stats.Shells);
        Assert.Equal(Enumerable.Range(0, 6), health.Shells[0].Faces);
        Assert.Equal(Enumerable.Range(6, 6), health.Shells[1].Faces);
        Assert.Equal(["size 4 x 1 x 1 m", "base at y = 0", "origin inside the bounds", "2 separate shells; a model is usually one"], health.Notes);
        Assert.Equal(2, health.Stats.Volume);
    }

    [Fact]
    public void Cubes_sharing_only_a_vertex_are_two_shells()
    {
        // The second cube's corner 0 is the first cube's vertex 6, at (1, 1, 1); they share no edge.
        int[] map = [6, 8, 9, 10, 11, 12, 13, 14];
        var health = MeshHealth.Create(TestMeshes.Make(
            TestMeshes.CubeVertices.Concat(TestMeshes.CubeVertices.Skip(1).Select(v => v + Vector3.One)),
            TestMeshes.CubeFaces.Concat(TestMeshes.CubeFaces.Select(f => f.Select(i => map[i]).ToArray()))));

        Assert.Equal(2, health.Stats.Shells);
    }

    [Fact]
    public void An_inside_out_shell_is_named_by_its_position_and_its_volume_subtracts()
    {
        var health = MeshHealth.Create(TwoCubes(new Vector3(3, 0, 0), Reversed));

        var problem = Assert.Single(health.Problems);
        Assert.Equal("shells[1]", problem.Element);
        Assert.Contains("(faces 6 to 11)", problem.Message);
        Assert.Equal(0, health.Stats.Volume);
    }

    [Fact]
    public void A_shell_of_scattered_faces_is_listed_first_five_and_a_count()
    {
        // The two cubes' faces interleaved, so the reversed shell is faces 1, 3, 5, 7, 9, 11.
        var faces = TestMeshes.CubeFaces.Zip(Reversed.Select(f => f.Select(i => i + 8).ToArray()), (a, b) => new[] { a, b }).SelectMany(p => p);
        var health = MeshHealth.Create(TestMeshes.Make(TestMeshes.CubeVertices.Concat(TestMeshes.CubeVertices.Select(v => v + new Vector3(3, 0, 0))), faces));

        Assert.Contains("closed shell of 6 faces (faces 1, 3, 5, 7, 9 and 1 more) is inside-out", Assert.Single(health.Problems).Message);
    }

    [Fact]
    public void The_crate_is_ok()
    {
        var health = MeshHealth.Create(TestMeshes.Crate());

        Assert.True(health.Ok);
        Assert.Equal(1, health.Stats.Shells);
        Assert.Equal(0.58723203034854, health.Stats.Volume);
    }

    [Fact]
    public void Coincident_vertices_are_found_one_problem_per_later_duplicate()
    {
        // The cube with vertex 0 three times, the copies on no face.
        var health = MeshHealth.Create(TestMeshes.Make(TestMeshes.CubeVertices.Concat([Vector3.Zero, Vector3.Zero]), TestMeshes.CubeFaces));

        Assert.Equal(
            [
                new HealthProblem("coincident-vertices", "vertices[8]", "vertex is at the same position as vertices[0]"),
                new HealthProblem("coincident-vertices", "vertices[9]", "vertex is at the same position as vertices[0]"),
            ],
            health.Problems);
    }

    [Fact]
    public void Coincidence_has_no_tolerance_one_float_step_apart_is_not_coincident()
    {
        var next = new Vector3(BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(1f) + 1), 1, 1);
        Assert.NotEqual(1f, next.X);

        var health = MeshHealth.Create(TestMeshes.Make(TestMeshes.CubeVertices.Append(next), TestMeshes.CubeFaces));

        Assert.Empty(health.Problems);
    }

    [Fact]
    public void Minus_zero_and_zero_are_the_same_position()
    {
        // The person's ruling of 2026-11-07: positions compare as floats, as select's == does, so -0 is 0.
        var health = MeshHealth.Create(TestMeshes.Make(TestMeshes.CubeVertices.Append(new Vector3(-0f, 0f, -0f)), TestMeshes.CubeFaces));

        Assert.Equal([new HealthProblem("coincident-vertices", "vertices[8]", "vertex is at the same position as vertices[0]")], health.Problems);
    }

    [Theory]
    [InlineData(new[] { 4, 5, 6, 7 })] // the same loop
    [InlineData(new[] { 6, 7, 4, 5 })] // rotated
    [InlineData(new[] { 7, 6, 5, 4 })] // reversed
    [InlineData(new[] { 5, 4, 7, 6 })] // reversed and rotated
    public void A_face_with_the_same_vertices_is_a_duplicate(int[] copy)
    {
        var health = MeshHealth.Create(TestMeshes.Make(TestMeshes.CubeVertices, TestMeshes.CubeFaces.Append(copy)));

        Assert.Equal(new HealthProblem("duplicate-face", "faces[6]", "face has the same vertices as faces[1]"), Assert.Single(health.Problems));
    }

    [Fact]
    public void An_open_box_has_no_volume()
    {
        var health = MeshHealth.Create(TestMeshes.Make(TestMeshes.CubeVertices, TestMeshes.CubeFaces.Skip(1)));

        Assert.Null(health.Stats.Volume);
        Assert.False(health.Stats.Closed);
        Assert.Empty(health.Problems);
        Assert.False(health.Ok);
        Assert.Equal(5, health.Stats.Area);
    }

    [Fact]
    public void A_closed_shell_of_mixed_winding_is_not_inside_out_and_has_no_volume()
    {
        var faces = TestMeshes.CubeFaces.ToArray();
        faces[1] = Reversed[1];

        var health = MeshHealth.Create(TestMeshes.Make(TestMeshes.CubeVertices, faces));

        var shell = Assert.Single(health.Shells);
        Assert.True(shell.Closed);
        Assert.False(shell.ConsistentWinding);
        Assert.Null(shell.SignedVolume);
        Assert.Empty(health.Problems);
    }

    [Fact]
    public void A_low_base_and_a_far_origin_are_noted_and_are_not_problems()
    {
        var health = MeshHealth.Create(TestMeshes.Make(TestMeshes.CubeVertices.Select(v => v + new Vector3(2, -0.2f, 0)), TestMeshes.CubeFaces));

        Assert.True(health.Ok);
        Assert.Equal("base at y = -0.2; a prop usually stands on y = 0", health.Notes[1]);
        Assert.Equal("origin outside the bounds", health.Notes[2]);
    }

    [Fact]
    public void Notes_do_not_follow_the_culture()
    {
        var before = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        try
        {
            var health = MeshHealth.Create(TestMeshes.Make(TestMeshes.CubeVertices.Select(v => v * 2.5f), TestMeshes.CubeFaces));
            Assert.Equal("size 2.5 x 2.5 x 2.5 m", health.Notes[0]);
        }
        finally { CultureInfo.CurrentCulture = before; }
    }

    [Fact]
    public void The_shed_gives_every_problem_warning_note_and_stat_exactly()
    {
        var health = MeshHealth.Create(TestMeshes.LeakyShed());

        Assert.False(health.Ok);
        Assert.Equal(TestMeshes.LeakyShedProblems, health.Problems.Select(p => (p.Check, p.Element, p.Message)));
        Assert.Equal(TestMeshes.LeakyShedWarnings, health.Warnings.Select(w => w.ToString()));
        Assert.Equal(TestMeshes.LeakyShedNotes, health.Notes);
        var top = new Vector3(4, 3.9f, 3);
        Assert.Equal(new HealthStats(25, 18, 38, 36, 2, false, Vector3.Zero, top, top, 60.09371484553011, null), health.Stats);

        Assert.Equal(Enumerable.Range(0, 12), health.Shells[0].Faces);
        Assert.False(health.Shells[0].Closed);
        Assert.False(health.Shells[0].ConsistentWinding);
        Assert.Equal(Enumerable.Range(12, 6), health.Shells[1].Faces);
        Assert.True(health.Shells[1].Closed);
        Assert.True(health.Shells[1].ConsistentWinding);
        Assert.True(health.Shells[1].SignedVolume < 0);
    }

    [Fact]
    public void Vertices_3_and_9_are_not_an_edge_of_the_shed()
    {
        Assert.False(TestMeshes.LeakyShed().Topology.TryGetEdge(3, 9, out _));
    }

    [Fact]
    public void The_shed_file_is_the_code_s_mesh()
    {
        var mesh = TestMeshes.LeakyShed();
        var text = File.ReadAllText(RepoFile("samples/leaky-shed.carve"));

        Assert.Equal(CarveFile.Write(mesh), text);
        var read = CarveFile.Read(text);
        Assert.True(read.Succeeded);
        Assert.Equal(mesh.Vertices, read.Mesh!.Vertices);
        Assert.Equal(mesh.Faces.Select(f => f.ToArray()), read.Mesh.Faces.Select(f => f.ToArray()));
    }
}
