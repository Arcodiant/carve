using System.Text.Json;
using Patina.Graphs;

namespace Patina.Server.Tests;

/// <summary>check (T-141) through the harness: its worked examples as written, a graph or PNG files, and what it refuses.</summary>
[Trait("Tier", "unit")]
public class CheckToolTests
{
    private static string Sample(string relative) => Path.Combine(Paths.RepoRoot, "samples", "graphs", relative);

    private static string Open(string relative) => JsonSerializer.Serialize(new { path = Sample(relative) });

    [Fact]
    public async Task The_worked_examples_run_as_written_and_are_held_to_the_description()
    {
        await using var patina = await PatinaHarness.StartAsync(Paths.RepoRoot);
        var text = CheckTool.Text;

        await patina.Ok("open", Open("bricks.texgraph"));
        Assert.Equal(CheckTool.WorkedResult, await patina.Ok("check", CheckTool.Worked));
        Assert.Contains("check " + CheckTool.Worked + " returns, clean:\n" + CheckTool.WorkedResult, text, StringComparison.Ordinal);

        await patina.Ok("open", Open("faults/chrome-in-paint.texgraph"));
        Assert.Equal(CheckTool.WorkedFaultResult, await patina.Ok("check", CheckTool.WorkedFault));
        // T-170: the fault and files examples left the description (it is held under Claude Code's 4,096-character cut); they still run as written.
        Assert.DoesNotContain(CheckTool.WorkedFaultResult, text, StringComparison.Ordinal);

        Assert.Equal(CheckTool.WorkedFilesResult, await patina.Ok("check", CheckTool.WorkedFiles));
    }

    [Fact]
    public async Task A_clean_graph_is_ok_and_a_planted_fault_is_a_problem_not_an_error()
    {
        await using var patina = await PatinaHarness.StartAsync(Paths.RepoRoot);
        await patina.Ok("open", Open("faults/flat-mask.texgraph"));
        var report = JsonDocument.Parse(await patina.Ok("check", "{}")).RootElement;
        Assert.False(report.GetProperty("ok").GetBoolean());
        var problem = Assert.Single(report.GetProperty("problems").EnumerateArray());
        Assert.Equal(("one-value", "wear"), (problem.GetProperty("check").GetString(), problem.GetProperty("output").GetString()));
        Assert.True(report.GetProperty("stats").GetProperty("outputs").TryGetProperty("wear", out _));
    }

    [Fact]
    public async Task Size_and_parameters_are_used_for_a_graph_and_ignored_with_a_note_for_files()
    {
        await using var patina = await PatinaHarness.StartAsync(Paths.RepoRoot);
        await patina.Ok("open", Open("bricks.texgraph"));
        var small = JsonDocument.Parse(await patina.Ok("check", """{"size":32}""")).RootElement;
        Assert.Equal(32, small.GetProperty("stats").GetProperty("size").GetInt32());

        var files = """{"size":64,"parameters":{"mortar":0.1},"files":[{"path":"samples/graphs/bricks/base_colour.png","role":"base_colour"}]}""";
        var report = JsonDocument.Parse(await patina.Ok("check", files)).RootElement;
        Assert.Equal(256, report.GetProperty("stats").GetProperty("size").GetInt32());
        Assert.Contains(report.GetProperty("notes").EnumerateArray(), n => n.GetString()!.StartsWith("size and parameters were not used", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Files_need_no_graph_and_the_errors_say_what_to_do()
    {
        await using var patina = await PatinaHarness.StartAsync(Paths.RepoRoot);
        Assert.Contains("no open graph", await patina.Refused("check", "{}"));
        Assert.Contains("does not exist", await patina.Refused("check", """{"files":[{"path":"nope.png","role":"normal"}]}"""));
        Assert.Contains("twice", await patina.Refused("check", """{"files":[{"path":"samples/graphs/bricks/normal.png","role":"normal"},{"path":"samples/graphs/bricks/normal.png","role":"normal"}]}"""));
        await patina.Ok("open", Open("bricks.texgraph"));
        Assert.Contains("is not allowed", await patina.Refused("check", """{"size":4096}"""));
        Assert.Contains("unknown", (await patina.Refused("check", """{"sise":64}""")), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A folder of its own under the temp folder for one test's PNGs; the caller deletes it.</summary>
    private static string TempFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "patina-check-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }

    private static string WritePng(string folder, string name, int w, int h, Func<int, int, (byte R, byte G, byte B)> texel)
    {
        var rgba = new byte[w * h * 4];
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var (r, g, b) = texel(x, y);
                var i = (y * w + x) * 4;
                rgba[i] = r; rgba[i + 1] = g; rgba[i + 2] = b; rgba[i + 3] = 255;
            }
        var path = Path.Combine(folder, name);
        File.WriteAllBytes(path, Patina.Graphs.Imaging.Png.Encode(rgba, w, h));
        return path;
    }

    private static string Files(params (string Path, string Role)[] files) =>
        JsonSerializer.Serialize(new { files = files.Select(f => new { path = f.Path, role = f.Role }) });

    [Fact]
    public async Task Only_a_metallic_roughness_file_has_its_blue_channel_read_as_metallic()
    {
        // RV-76, the person's ruling: role roughness is roughness only; metallic_roughness is the packed glTF image.
        await using var patina = await PatinaHarness.StartAsync(Paths.RepoRoot);
        var folder = TempFolder();
        try
        {
            var grey = WritePng(folder, "grey.png", 32, 32, (x, y) => { var v = (byte)(100 + (x + y) % 2); return (v, v, v); });
            var plain = JsonDocument.Parse(await patina.Ok("check", Files((grey, "roughness")))).RootElement;
            Assert.True(plain.GetProperty("ok").GetBoolean(), plain.GetRawText());
            Assert.False(plain.GetProperty("stats").GetProperty("outputs").TryGetProperty("metallic", out _));
            Assert.DoesNotContain(plain.GetProperty("notes").EnumerateArray(), n => n.GetString()!.Contains("metal", StringComparison.Ordinal));

            var packed = JsonDocument.Parse(await patina.Ok("check", Files((grey, "metallic_roughness")))).RootElement;
            Assert.Contains(packed.GetProperty("problems").EnumerateArray(), p => p.GetProperty("check").GetString() == "metallic-not-binary");
            Assert.True(packed.GetProperty("stats").GetProperty("outputs").TryGetProperty("roughness", out _));
            Assert.True(packed.GetProperty("stats").GetProperty("outputs").TryGetProperty("metallic", out _));
            Assert.Contains(packed.GetProperty("notes").EnumerateArray(), n => n.GetString() == "metallic read from metallic_roughness's blue channel");

            Assert.Contains("two roughness files", await patina.Refused("check", Files((grey, "roughness"), (grey, "metallic_roughness"))));
        }
        finally { Directory.Delete(folder, true); }
    }

    [Fact]
    public async Task Files_of_different_sizes_name_each_ones_size_and_files_of_one_size_give_stats_size()
    {
        // RV-76: stats.size was the first file's width, wrong for every other file.
        await using var patina = await PatinaHarness.StartAsync(Paths.RepoRoot);
        var folder = TempFolder();
        try
        {
            var small = WritePng(folder, "small.png", 32, 32, (x, y) => ((byte)(60 + (x + y) % 2), 80, 90));
            var wide = WritePng(folder, "wide.png", 64, 32, (x, y) => { var v = (byte)(100 + (x + y) % 2); return (v, v, v); });
            var mixed = JsonDocument.Parse(await patina.Ok("check", Files((small, "base_colour"), (wide, "occlusion")))).RootElement.GetProperty("stats");
            Assert.False(mixed.TryGetProperty("size", out _), mixed.GetRawText());
            var outputs = mixed.GetProperty("outputs");
            Assert.Equal((32, 32), (outputs.GetProperty("base_colour").GetProperty("width").GetInt32(), outputs.GetProperty("base_colour").GetProperty("height").GetInt32()));
            Assert.Equal((64, 32), (outputs.GetProperty("occlusion").GetProperty("width").GetInt32(), outputs.GetProperty("occlusion").GetProperty("height").GetInt32()));

            var nonSquare = JsonDocument.Parse(await patina.Ok("check", Files((wide, "occlusion")))).RootElement.GetProperty("stats");
            Assert.False(nonSquare.TryGetProperty("size", out _));

            var one = JsonDocument.Parse(await patina.Ok("check", Files((small, "base_colour")))).RootElement.GetProperty("stats");
            Assert.Equal(32, one.GetProperty("size").GetInt32());
            Assert.False(one.GetProperty("outputs").GetProperty("base_colour").TryGetProperty("width", out _));
        }
        finally { Directory.Delete(folder, true); }
    }

    [Fact]
    public async Task A_file_that_is_not_a_png_or_is_a_broken_one_is_an_error()
    {
        // RV-76: the "not a PNG" error had no test. Text, a BMP's header (StbImageSharp would read a BMP) and a PNG cut short.
        await using var patina = await PatinaHarness.StartAsync(Paths.RepoRoot);
        var folder = TempFolder();
        try
        {
            var text = Path.Combine(folder, "text.png");
            File.WriteAllText(text, "not an image");
            Assert.Contains("is not a PNG", await patina.Refused("check", Files((text, "normal"))));

            var good = File.ReadAllBytes(WritePng(folder, "good.png", 32, 32, (x, _) => ((byte)x, 0, 0)));
            var bmp = Path.Combine(folder, "image.bmp");
            File.WriteAllBytes(bmp, [(byte)'B', (byte)'M', .. good.AsSpan(2)]);
            Assert.Contains("is not a PNG", await patina.Refused("check", Files((bmp, "normal"))));

            var cut = Path.Combine(folder, "cut.png");
            File.WriteAllBytes(cut, good.AsSpan(0, 40).ToArray());
            Assert.Contains("could not read", await patina.Refused("check", Files((cut, "normal"))));
        }
        finally { Directory.Delete(folder, true); }
    }

    [Fact]
    public async Task A_graph_with_a_fault_is_an_error_naming_it_and_changes_nothing()
    {
        using var folder = new TempFolder();
        File.WriteAllText(Path.Combine(folder.Path, "planted.texgraph"),
            "{\"version\":1,\"size\":64,\"seed\":5,\"nodes\":{\"c\":{\"type\":\"colour\"},\"out\":{\"type\":\"output\",\"inputs\":{\"in\":\"cc\"}}}}");
        await using var patina = await PatinaHarness.StartAsync(folder.Path);
        await patina.Ok("open", """{"path":"planted"}""");
        var before = await patina.GraphText();
        Assert.Contains("fix them with edit_graph", await patina.Refused("check", "{}"));
        Assert.Equal(before, await patina.GraphText());
    }
}
