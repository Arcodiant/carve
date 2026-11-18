extern alias carve;

using GameLimits = carve::Carve.Server.GameLimits;
using LimitsSource = carve::Carve.Server.LimitsSource;
using System.Numerics;
using Carve.Core;
using Carve.Core.Tests;
using Model = Carve.Core.Model;
using Skeleton = Carve.Core.Skeleton;

namespace Carve.Server.Tests;

/// <summary>T-51: a game's limits, read from its carve-limits.json, and where export finds that file.</summary>
[Trait("Tier", "unit")]
public sealed class GameLimitsTests() : TempFolderTests("carve-limits")
{
    private static Model Cube() => Model.Of("mesh", TestMeshes.Make(TestMeshes.CubeVertices, TestMeshes.CubeFaces));

    [Fact]
    public void A_file_of_every_key_reads_back_and_writes_in_key_order()
    {
        var limits = GameLimits.Parse("""{"max_height": 2, "min_height": 0.5, "max_submeshes": 3, "max_triangles": 100}""", out var error);
        Assert.Null(error);
        Assert.Equal(new GameLimits(100, 3, 0.5f, 2f), limits);
        Assert.Equal("{\n  \"max_triangles\": 100,\n  \"max_submeshes\": 3,\n  \"min_height\": 0.5,\n  \"max_height\": 2\n}\n", limits!.ToFileText());
        Assert.Equal(limits, GameLimits.Parse(limits.ToFileText(), out _));
        Assert.Equal(GameLimits.Starting, GameLimits.Parse(GameLimits.Starting.ToFileText(), out _));
    }

    [Fact]
    public void Every_key_is_optional_and_one_left_out_never_warns()
    {
        Assert.Equal(GameLimits.None, GameLimits.Parse("{}", out _));
        Assert.Equal("{}\n", GameLimits.None.ToFileText());
        Assert.Empty(GameLimits.None.Warnings(Cube(), 1_000_000, 1_000));
        var onlyTriangles = GameLimits.Parse("""{"max_triangles": 12}""", out _)!;
        Assert.Empty(onlyTriangles.Warnings(Cube(), 12, 1_000));
        Assert.Equal("13 triangles, above the game's max_triangles 12", Assert.Single(onlyTriangles.Warnings(Cube(), 13, 1_000)));
    }

    [Theory]
    [InlineData("[]", "expected a JSON object")]
    [InlineData("{", "not valid JSON")]
    [InlineData("""{"max_tris": 5}""", "max_tris: not a limit; the limits are max_triangles, max_submeshes, min_height, max_height")]
    [InlineData("""{"max_triangles": 0}""", "max_triangles: expected a whole number of at least 1, got 0")]
    [InlineData("""{"max_triangles": 2.5}""", "max_triangles: expected a whole number of at least 1, got 2.5")]
    [InlineData("""{"max_submeshes": "8"}""", "max_submeshes: expected a whole number of at least 1, got \"8\"")]
    [InlineData("""{"min_height": 0}""", "min_height: expected a number of metres above 0, got 0")]
    [InlineData("""{"max_height": -1}""", "max_height: expected a number of metres above 0, got -1")]
    [InlineData("""{"min_height": 2, "max_height": 1}""", "min_height 2 is above max_height 1")]
    public void A_bad_file_is_refused_saying_why(string text, string expected)
    {
        Assert.Null(GameLimits.Parse(text, out var error));
        Assert.Contains(expected, error);
    }

    [Fact]
    public void A_static_model_is_warned_on_triangles_and_submeshes_but_never_height()
    {
        var limits = new GameLimits(10, 1, 5f, 6f);
        var cube = Cube();
        var warnings = limits.Warnings(cube, 12, 2);
        Assert.Equal(2, warnings.Count);
        Assert.Equal("12 triangles, above the game's max_triangles 10", warnings[0]);
        Assert.StartsWith("2 submeshes and 0 colours, above the game's max_submeshes 1", warnings[1]);
    }

    [Fact]
    public void Height_warns_on_one_bound_alone_and_only_on_the_Synty_bones()
    {
        var names = SkinnedExport.SyntyRequiredBones;
        var rig = Skeleton.Create([.. names.Select((n, i) => new BoneSpec(n, i == 0 ? null : names[0], new Vector3(0, 0.1f, 0), Quaternion.Identity))]).Skeleton!;
        var humanoid = Cube().WithSkeleton(rig); // 1 m tall
        Assert.Contains("below the game's min_height 1.5 m", Assert.Single(new GameLimits(null, null, 1.5f, null).Warnings(humanoid, 1, 1)));
        Assert.Contains("above the game's max_height 0.5 m", Assert.Single(new GameLimits(null, null, null, 0.5f).Warnings(humanoid, 1, 1)));
        Assert.Empty(new GameLimits(null, null, 1f, 1f).Warnings(humanoid, 1, 1));
        Assert.Empty(new GameLimits(null, null, 1.5f, null).Warnings(Cube(), 1, 1));
    }

    // ---- LimitsSource: where export finds the file.

    /// <summary>The upward search runs past the temp root; a carve-limits.json above it would be read, or filled.</summary>
    private void NoLimitsFileAboveRoot()
    {
        for (var folder = new DirectoryInfo(Root).Parent; folder is not null; folder = folder.Parent)
            Assert.False(File.Exists(Path.Combine(folder.FullName, GameLimits.FileName)), $"a {GameLimits.FileName} in {folder.FullName} would be found by these tests");
    }

    private string Glb(params string[] folders) => Path.Combine([Root, .. folders, "thing.glb"]);

    private void Put(string folder, string text)
    {
        Directory.CreateDirectory(Path.Combine(Root, folder));
        File.WriteAllText(Path.Combine(Root, folder, GameLimits.FileName), text);
    }

    [Fact]
    public void The_first_file_from_the_glbs_folder_upward_is_used()
    {
        Put("game", """{"max_triangles": 1}""");
        Put(Path.Combine("game", "Content"), """{"max_triangles": 2}""");
        var source = LimitsSource.Find("export", null, false, Glb("game", "Content", "models", "props"), out var error);
        Assert.Null(error);
        Assert.Equal(2, source!.Limits.MaxTriangles);
        Assert.Equal(Path.Combine(Root, "game", "Content", GameLimits.FileName), source.Path);
        Assert.False(source.NeedsWrite);
        Assert.Equal($"; limits from {source.Path}", source.Describe(written: false));
    }

    [Fact]
    public void No_file_means_no_limits_and_the_result_says_so()
    {
        NoLimitsFileAboveRoot();
        var glb = Glb("game", "Content");
        var source = LimitsSource.Find("export", null, false, glb, out _)!;
        Assert.Null(source.Path);
        Assert.Equal(GameLimits.None, source.Limits);
        Assert.Contains($"no game limits: no carve-limits.json in {Path.GetDirectoryName(glb)} or a folder above it", source.Describe(false));
    }

    [Fact]
    public void The_limits_argument_names_a_folder_or_a_json_file()
    {
        Put("shared", """{"max_submeshes": 4}""");
        File.WriteAllText(Path.Combine(Root, "shared", "props.json"), """{"max_submeshes": 2}""");
        Assert.Equal(4, LimitsSource.Find("export", Path.Combine(Root, "shared"), false, Glb("game"), out _)!.Limits.MaxSubmeshes);
        Assert.Equal(2, LimitsSource.Find("export", Path.Combine(Root, "shared", "props.json"), false, Glb("game"), out _)!.Limits.MaxSubmeshes);

        Assert.Null(LimitsSource.Find("export", Path.Combine(Root, "nowhere"), false, Glb("game"), out var error));
        Assert.Contains($"no limits file at {Path.Combine(Root, "nowhere", GameLimits.FileName)}; write_limits: true creates it", error);
        Assert.EndsWith("Nothing was written.", error);
    }

    [Fact]
    public void A_limits_argument_no_file_could_be_at_is_refused_even_with_write_limits()
    {
        File.WriteAllText(Path.Combine(Root, "notes.txt"), "x");
        Directory.CreateDirectory(Path.Combine(Root, "odd", GameLimits.FileName));
        foreach (var (argument, expected) in new[]
        {
            ("   ", "export: limits is empty"),
            (Path.Combine(Root, "lim?ts.json"), "holds"),
            (Path.Combine(Root, "notes.txt"), "is a file but not a .json one"),
            (Path.Combine(Root, "odd"), "is a folder, not a limits file"),
            (Path.Combine(Root, "notes.txt") + Path.DirectorySeparatorChar, "is a file, so no limits file can be at"),
            (Path.Combine(Root, "notes.txt", "game.json"), "is a file, so no limits file can be at"),
            (Glb("game"), "the file being exported"),
            (Path.Combine(Glb("game"), "x.json"), "the file being exported"),
        })
        {
            Assert.Null(LimitsSource.Find("export", argument, true, Glb("game"), out var error));
            Assert.Contains(expected, error);
            Assert.StartsWith("export: limits", error);
            Assert.EndsWith("Nothing was written.", error);
        }
    }

    [Fact]
    public void A_comment_is_refused_since_write_limits_would_lose_it()
    {
        Assert.Null(GameLimits.Parse("""{ /* the orc budget */ "max_triangles": 5000 }""", out var error));
        Assert.StartsWith("not valid JSON", error);
    }

    [Fact]
    public void A_bad_file_found_is_a_tool_error_naming_it()
    {
        Put("game", """{"max_triangles": -3}""");
        Assert.Null(LimitsSource.Find("export", null, false, Glb("game"), out var error));
        Assert.StartsWith($"export: limits file {Path.Combine(Root, "game", GameLimits.FileName)}: max_triangles: expected a whole number", error);
    }

    [Fact]
    public void Write_limits_creates_the_file_beside_the_glb_with_the_starting_numbers()
    {
        NoLimitsFileAboveRoot();
        var source = LimitsSource.Find("export", null, true, Glb("game", "Content"), out _)!;
        Assert.Equal(GameLimits.Starting, source.Limits);
        Assert.True(source.NeedsWrite);
        Assert.Equal(Path.Combine(Root, "game", "Content", GameLimits.FileName), source.Path);
        Assert.True(source.Write("export", out _));
        Assert.Equal(GameLimits.Starting.ToFileText(), File.ReadAllText(source.Path!));
        Assert.EndsWith("(created with the starting numbers)", source.Describe(written: true));
    }

    [Fact]
    public void Write_limits_fills_only_the_missing_keys_and_keeps_those_set()
    {
        Put("game", """{"max_triangles": 900, "min_height": 0.3}""");
        var source = LimitsSource.Find("export", null, true, Glb("game", "Content", "models"), out _)!;
        Assert.Equal(new GameLimits(900, 8, 0.3f, 2.4f), source.Limits);
        Assert.Equal(["max_submeshes", "max_height"], source.Filled);
        Assert.True(source.Write("export", out _));
        Assert.Equal(new GameLimits(900, 8, 0.3f, 2.4f), GameLimits.Parse(File.ReadAllText(source.Path!), out _));
        Assert.EndsWith("(filled max_submeshes, max_height with the starting numbers)", source.Describe(written: true));

        // A complete file is left alone.
        var again = LimitsSource.Find("export", null, true, Glb("game"), out _)!;
        Assert.Empty(again.Filled);
        Assert.False(again.NeedsWrite);
    }

    [Fact]
    public void Write_limits_refuses_a_fill_that_would_cross_the_heights()
    {
        Put("game", """{"min_height": 3}""");
        Assert.Null(LimitsSource.Find("export", null, true, Glb("game"), out var error));
        Assert.Contains("with the starting numbers filled in, min_height 3 is above max_height 2.4", error);
        Assert.Equal("""{"min_height": 3}""", File.ReadAllText(Path.Combine(Root, "game", GameLimits.FileName)));
    }
}
