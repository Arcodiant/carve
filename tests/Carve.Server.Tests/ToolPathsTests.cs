namespace Carve.Server.Tests;

/// <summary>The path rules export (and save and open) keep, on a temp folder.</summary>
[Trait("Tier", "unit")]
public sealed class ToolPathsTests() : TempFolderTests("carve-toolpaths")
{
    private ToolPath Resolve(string path)
    {
        var resolved = ToolPaths.Resolve("export", path, ".glb", out var error, Root);
        Assert.True(resolved is not null, error);
        Assert.Null(error);
        return resolved!;
    }

    private string Refused(string? path)
    {
        var resolved = ToolPaths.Resolve("export", path, ".glb", out var error, Root);
        Assert.Null(resolved);
        Assert.NotNull(error);
        Assert.StartsWith("export: ", error);
        Assert.DoesNotContain('\n', error);
        return error!;
    }

    [Fact]
    public void A_relative_path_resolves_against_the_working_directory()
    {
        var path = Resolve("models/crate.glb");
        Assert.Equal(Path.Combine(Root, "models", "crate.glb"), path.FullPath);
        Assert.Equal("crate.glb", path.FileName);
        Assert.Equal("crate", path.Stem);
        Assert.True(Path.IsPathFullyQualified(path.FullPath));
    }

    [Fact]
    public void An_absolute_path_is_kept()
    {
        var absolute = Path.Combine(Root, "elsewhere", "Crate.GLB");
        Assert.Equal(absolute, Resolve(absolute).FullPath);
    }

    [Fact]
    public void A_write_creates_missing_folders_and_says_when_it_replaced_a_file()
    {
        var path = Resolve("a/b/c/crate.glb");
        Assert.True(ToolPaths.Write("export", path, [1, 2, 3], out var replaced, out var error), error);
        Assert.False(replaced);
        Assert.Equal([1, 2, 3], File.ReadAllBytes(path.FullPath));

        Assert.True(ToolPaths.Write("export", path, [4, 5], out replaced, out error, overwrite: true), error);
        Assert.True(replaced);
        Assert.Equal([4, 5], File.ReadAllBytes(path.FullPath));
        Assert.Equal(["crate.glb"], Directory.GetFiles(Path.GetDirectoryName(path.FullPath)!).Select(Path.GetFileName));
    }

    [Fact]
    public void Without_overwrite_an_existing_file_is_refused_byte_unchanged_and_with_it_replaced()
    {
        var path = Resolve("crate.glb");
        Assert.True(ToolPaths.Write("export", path, [1, 2, 3], out var replaced, out var error, overwrite: false), error);
        Assert.False(replaced);

        Assert.False(ToolPaths.Write("export", path, [9, 9], out replaced, out error, overwrite: false));
        Assert.False(replaced);
        Assert.Equal($"export: {path.FullPath} already exists; pass overwrite: true to replace it. Nothing was written.", error);
        Assert.Equal([1, 2, 3], File.ReadAllBytes(path.FullPath));
        Assert.Equal(["crate.glb"], Directory.GetFiles(Root).Select(Path.GetFileName));

        Assert.True(ToolPaths.Write("export", path, [9, 9], out replaced, out error, overwrite: true), error);
        Assert.True(replaced);
        Assert.Equal([9, 9], File.ReadAllBytes(path.FullPath));
    }

    [Theory]
    [InlineData("crate.obj", "ends in \".obj\"")]
    [InlineData("crate", "has no extension")]
    [InlineData("crate.glb.txt", "ends in \".txt\"")]
    public void Any_other_extension_is_an_error_naming_the_one_wanted(string path, string says)
    {
        var error = Refused(path);
        Assert.Contains(says, error);
        Assert.Contains("must end in .glb", error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void An_empty_path_is_an_error(string? path) => Assert.Contains("path is empty", Refused(path));

    [Theory]
    [InlineData("cr|ate.glb", "'|'")]
    [InlineData("mod*els/crate.glb", "'*'")]
    [InlineData("crate\0.glb", "U+0000")]
    public void A_path_with_a_character_no_path_may_hold_is_an_error(string path, string names) =>
        Assert.Contains(names, Refused(path));

    [Fact]
    public void A_path_naming_a_folder_is_an_error() => Assert.Contains("names a folder", Refused("models/"));

    [Fact]
    public void A_failed_write_names_the_path_and_leaves_the_old_file_and_no_partial_one()
    {
        var path = Resolve("locked.glb");
        File.WriteAllBytes(path.FullPath, [9]);
        string? error;
        using (new FileStream(path.FullPath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.False(ToolPaths.Write("export", path, [1, 2, 3], out var replaced, out error));
            Assert.False(replaced);
        }
        Assert.StartsWith($"export: could not write {path.FullPath}: ", error);
        Assert.Equal([9], File.ReadAllBytes(path.FullPath));
        Assert.Equal(["locked.glb"], Directory.GetFiles(Root).Select(Path.GetFileName));
    }
}
