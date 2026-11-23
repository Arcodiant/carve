using System.Buffers.Binary;
using System.Text.Json;
using System.Text.RegularExpressions;
using Carve.Scenario.Patina;
using StbImageSharp;

namespace Patina.Server.Tests;

/// <summary>T-115: export writes the open graph's outputs as PNGs, equal to the samples at the graph's size, refusing what it cannot do.</summary>
[Trait("Tier", "unit")]
public class ExportToolTests
{
    private static readonly string[] Roles = ["base_colour", "normal", "roughness", "occlusion"];

    private static string Planks => Path.Combine(Paths.RepoRoot, "samples", "graphs", "planks.texgraph");

    private static async Task<PatinaHarness> OpenPlanks(string folder)
    {
        var patina = await PatinaHarness.StartAsync(folder);
        await patina.Ok("open", JsonSerializer.Serialize(new { path = Planks }));
        return patina;
    }

    private static (int W, int H) PngSize(string path)
    {
        var b = File.ReadAllBytes(path);
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, b[..4]);
        return (BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(16)), BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(20)));
    }

    [Fact]
    public async Task Export_at_the_graph_s_size_writes_bytes_equal_to_the_samples_and_names_each_slot()
    {
        using var folder = new TempFolder();
        await using var patina = await OpenPlanks(folder.Path);

        var result = await patina.Ok("export", """{"folder":"out/planks"}""");

        var target = Path.Combine(folder.Path, "out", "planks");
        var lines = result.Split('\n');
        Assert.Equal($"export: wrote 4 files to {target} at size 256", lines[0]);
        foreach (var role in Roles)
        {
            var written = File.ReadAllBytes(Path.Combine(target, role + ".png"));
            Assert.Equal(File.ReadAllBytes(Path.Combine(Paths.RepoRoot, "samples", "graphs", "planks", role + ".png")), written);
            var slot = role == "roughness" ? "metallic_roughness_texture" : role + "_texture";
            Assert.Contains($"- {role}.png: 256x256, {written.Length} bytes, slot {slot}", lines);
        }
    }

    /// <summary>T-188: export's file and <c>Png.Encode</c> of the same texels are one byte string (both go through <c>PngWriter</c>).</summary>
    [Fact]
    public async Task Export_at_256_and_Png_Encode_give_the_same_bytes_for_the_same_texels()
    {
        using var folder = new TempFolder();
        await using var patina = await OpenPlanks(folder.Path);
        await patina.Ok("export", """{"folder":"out/same"}""");
        var read = Patina.Graphs.TexGraphFile.ReadFile(Planks);
        Assert.True(read.Loaded, read.Error?.ToString());
        var evaluator = new Patina.Graphs.Evaluation.GraphEvaluator(read.Graph!);

        foreach (var role in Roles)
        {
            var texels = Patina.Graphs.Evaluation.OutputImage.FileRgba8(evaluator, role, 256);
            Assert.Equal(Patina.Graphs.Imaging.Png.Encode(texels, 256, 256), File.ReadAllBytes(Path.Combine(folder.Path, "out", "same", role + ".png")));
        }
    }

    [Fact]
    public async Task Export_names_a_size_and_roles_writes_only_those_and_marks_a_replaced_file()
    {
        using var folder = new TempFolder();
        await using var patina = await OpenPlanks(folder.Path);
        const string arguments = """{"folder":"t","size":64,"outputs":["roughness","base_colour"]}""";

        var first = await patina.Ok("export", arguments);
        var second = await patina.Ok("export", arguments);

        Assert.Equal(["base_colour.png", "roughness.png"], Directory.GetFiles(Path.Combine(folder.Path, "t")).Select(Path.GetFileName).Order());
        Assert.Equal((64, 64), PngSize(Path.Combine(folder.Path, "t", "roughness.png")));
        Assert.DoesNotContain("replaced", first);
        Assert.Contains("(replaced)", second);
        Assert.Contains("slot metallic_roughness_texture", first);
    }

    [Fact]
    public async Task Export_at_a_larger_size_is_the_same_picture_sampled_at_matching_uv()
    {
        using var folder = new TempFolder();
        await using var patina = await OpenPlanks(folder.Path);
        await patina.Ok("export", """{"folder":"a","size":256,"outputs":["base_colour"]}""");
        await patina.Ok("export", """{"folder":"b","size":1024,"outputs":["base_colour"]}""");

        Assert.Equal((1024, 1024), PngSize(Path.Combine(folder.Path, "b", "base_colour.png")));
        var a = Decode(Path.Combine(folder.Path, "a", "base_colour.png"));
        var b = Decode(Path.Combine(folder.Path, "b", "base_colour.png"));
        var largest = ExportMatch.LargestDifference(a.Data, 256, b.Data, 1024);
        Assert.True(largest <= ExportMatch.Tolerance, $"1024 is not the 256 picture at matching uv (largest difference {largest:0.0})");
    }

    /// <summary>
    /// RV-68: the normal's rule (flat texels within 4, the planks' kinks within 32) passes the good 1024 export and fails a
    /// u-mirror, a flat map and shifts in u of a quarter stave, one 256 texel and half a 256 texel.
    /// </summary>
    [Fact]
    public async Task The_normal_s_rule_passes_the_good_export_and_fails_a_mirror_a_flat_map_and_each_shift()
    {
        using var folder = new TempFolder();
        await using var patina = await OpenPlanks(folder.Path);
        await patina.Ok("export", """{"folder":"a","size":256,"outputs":["normal"]}""");
        await patina.Ok("export", """{"folder":"b","size":1024,"outputs":["normal"]}""");

        var a = Decode(Path.Combine(folder.Path, "a", "normal.png"));
        var b = Decode(Path.Combine(folder.Path, "b", "normal.png"));
        var good = ExportMatch.NormalDifferences(a.Data, 256, b.Data, 1024);
        Assert.True(ExportMatch.NormalMatches(a.Data, 256, b.Data, 1024), $"normal: the good 1024 export fails the rule (flat {good.Flat:0.0}, kink {good.Kink:0.0})");
        Assert.InRange(good.FlatShare, 0.55, 0.70);
        var faults = ExportMatch.Faults("normal", b.Data, 1024);
        Assert.Equal(["u-mirror", "flat", "quarter-stave shift", "one-texel shift", "half-texel shift"], faults.Select(f => f.Name));
        foreach (var (fault, data) in faults)
        {
            var wrong = ExportMatch.NormalDifferences(a.Data, 256, data, 1024);
            Assert.False(ExportMatch.NormalMatches(a.Data, 256, data, 1024), $"normal: a {fault} passed the rule (flat {wrong.Flat:0.0}, kink {wrong.Kink:0.0})");
        }
    }

    /// <summary>RV-68: the matching-uv rule passes the good 1024 export and fails a half-tile shift, a u-mirror and a flat map of it (occlusion: the flat map; the normal has its own rule, tested above).</summary>
    [Fact]
    public async Task The_matching_uv_rule_fails_a_shifted_a_mirrored_and_a_flat_picture()
    {
        using var folder = new TempFolder();
        await using var patina = await OpenPlanks(folder.Path);
        await patina.Ok("export", """{"folder":"a","size":256}""");
        await patina.Ok("export", """{"folder":"b","size":1024}""");

        foreach (var role in new[] { "base_colour", "roughness", "occlusion" })
        {
            var a = Decode(Path.Combine(folder.Path, "a", role + ".png"));
            var b = Decode(Path.Combine(folder.Path, "b", role + ".png"));
            Assert.True(ExportMatch.Matches(a.Data, 256, b.Data, 1024), $"{role}: the good 1024 export fails the rule (largest difference {ExportMatch.LargestDifference(a.Data, 256, b.Data, 1024):0.0})");
            foreach (var (fault, data) in ExportMatch.Faults(role, b.Data, 1024))
            {
                var wrong = ExportMatch.LargestDifference(a.Data, 256, data, 1024);
                Assert.True(wrong > ExportMatch.Tolerance, $"{role}: a {fault} passed the rule (largest difference {wrong:0.0})");
            }
        }
    }

    private static ImageResult Decode(string path) => ImageResult.FromMemory(File.ReadAllBytes(path), ColorComponents.RedGreenBlueAlpha);

    [Theory]
    [InlineData("100")]
    [InlineData("16")]
    [InlineData("16384")]
    [InlineData("0")]
    [InlineData("256.5")]
    [InlineData("\"big\"")]
    public async Task A_size_that_is_not_a_power_of_two_in_range_gives_the_allowed_values_and_writes_nothing(string size)
    {
        using var folder = new TempFolder();
        await using var patina = await OpenPlanks(folder.Path);

        var error = await patina.Refused("export", "{\"folder\":\"t\",\"size\":" + size + "}");

        Assert.Equal($"export: size {size} is not allowed; size is a power of two from 32 to 8192 (32, 64, 128, 256, 512, 1024, 2048, 4096 or 8192)", error);
        Assert.False(Directory.Exists(Path.Combine(folder.Path, "t")));
    }

    [Fact]
    public async Task Export_refuses_with_no_graph_a_faulty_graph_a_missing_role_and_odd_arguments()
    {
        using var folder = new TempFolder();
        await using var patina = await PatinaHarness.StartAsync(folder.Path);
        Assert.Equal("export: there is no open graph yet; define_graph or open one first", await patina.Refused("export", """{"folder":"t"}"""));

        await patina.Ok("define_graph", """{"graph":{"version":1,"size":64,"seed":0,"nodes":{"c":{"type":"colour"},"out":{"type":"output","inputs":{"in":"c"}}}}}""");
        Assert.Equal("export: the graph has no normal output; it has base_colour", await patina.Refused("export", """{"folder":"t","outputs":["normal"]}"""));
        Assert.Equal("export: the graph has no shiny output; it has base_colour", await patina.Refused("export", """{"folder":"t","outputs":["shiny"]}"""));
        Assert.Equal("export: outputs holds \"Shiny!\", which is not a role or a name; the roles are base_colour, normal, roughness, metallic, occlusion, height, and a named output is given by its name", await patina.Refused("export", """{"folder":"t","outputs":["Shiny!"]}"""));
        Assert.StartsWith("export: outputs is empty;", await patina.Refused("export", """{"folder":"t","outputs":[]}"""));
        Assert.StartsWith("export: folder is missing;", await patina.Refused("export", "{}"));
        Assert.Equal("export: unknown argument 'path'; export takes folder, size, outputs, parameters", await patina.Refused("export", """{"folder":"t","path":"x"}"""));
        Assert.False(Directory.Exists(Path.Combine(folder.Path, "t")));

        var faulty = await patina.Ok("define_graph", """{"graph":{"version":1,"size":64,"seed":0,"nodes":{"c":{"type":"colour"},"out":{"type":"output","inputs":{"in":"cc"}}}}}""");
        Assert.Contains("1 fault", faulty);
        var refused = await patina.Refused("export", """{"folder":"t"}""");
        Assert.Contains("export: refused: the graph has 1 fault:\n- node 'out' input 'in': names 'cc'", refused);
        Assert.False(Directory.Exists(Path.Combine(folder.Path, "t")));
    }

    [Fact]
    public async Task A_folder_that_cannot_be_written_is_named_with_the_reason()
    {
        using var folder = new TempFolder();
        File.WriteAllText(Path.Combine(folder.Path, "taken"), "a file");
        await using var patina = await OpenPlanks(folder.Path);

        var error = await patina.Refused("export", """{"folder":"taken/inside","size":32}""");

        Assert.StartsWith($"export: could not write to folder {Path.Combine(folder.Path, "taken", "inside")}: ", error);
    }

    [Fact]
    public async Task A_folder_with_a_character_a_path_may_not_hold_is_named_with_the_reason()
    {
        using var folder = new TempFolder();
        await using var patina = await OpenPlanks(folder.Path);

        var error = await patina.Refused("export", JsonSerializer.Serialize(new { folder = "a\0b", size = 32 }));

        Assert.StartsWith("export: could not write to folder " + JsonSerializer.Serialize("a\0b") + ": ", error);
        Assert.Empty(Directory.GetFileSystemEntries(folder.Path));
    }

    [Fact]
    public async Task The_worked_example_gives_the_description_s_lines()
    {
        using var folder = new TempFolder();
        await using var patina = await OpenPlanks(folder.Path);

        var result = await patina.Ok("export", ExportTool.Worked);

        var head = ExportTool.WorkedHead.Replace("<folder>", Path.Combine(folder.Path, "textures", "planks"));
        Assert.Matches(new Regex("^" + Regex.Escape(head) + @"\n- base_colour\.png: 1024x1024, \d+ bytes, slot base_colour_texture\n- normal\.png: 1024x1024, \d+ bytes, slot normal_texture$"), result);
    }

    [Fact]
    public async Task Other_tools_answer_while_an_export_runs()
    {
        using var folder = new TempFolder();
        await using var patina = await OpenPlanks(folder.Path);

        // Some 6 s to run in a debug build, so it is still evaluating when the other calls come.
        var export = patina.CallToolAsync("export", """{"folder":"big","size":2048,"outputs":["base_colour"]}""", TestContext.Current.CancellationToken);
        await Task.Delay(500, TestContext.Current.CancellationToken);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        await patina.Ok("get_graph", "{}");
        Assert.False(Results.IsError(await patina.CallToolAsync("swatch", """{"view":"flat","size":32}""", TestContext.Current.CancellationToken)));
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(3), $"get_graph and swatch waited {watch.Elapsed} behind the export");
        Assert.False(Results.IsError(await export));
    }

    [Fact]
    public async Task An_export_named_in_notifications_cancelled_stops_and_writes_nothing()
    {
        using var folder = new TempFolder();
        await using var patina = await OpenPlanks(folder.Path);

        using var end = new CancellationTokenSource();
        // Some 20 s to run in a debug build. notifications/cancelled is sent by hand: the SDK's client (2.3.0) sends
        // none when its token is cancelled, but a client over stdio does, as the reviewer's probe did.
        var export = patina.CallToolWithIdAsync("big-export", "export", """{"folder":"big","size":4096,"outputs":["base_colour"]}""", end.Token);
        await Task.Delay(500, TestContext.Current.CancellationToken);
        await patina.CancelAsync("big-export");
        await AssertStopped(patina, folder.Path);
        // A cancelled request gets no answer; the client stops waiting for one here.
        end.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => export);
    }

    /// <summary>The cancelled export has given up its turn (a small export answers at once) and wrote no file.</summary>
    private static async Task AssertStopped(PatinaHarness patina, string folder)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        Assert.False(Results.IsError(await patina.CallToolAsync("export", """{"folder":"small","size":32,"outputs":["base_colour"]}""", TestContext.Current.CancellationToken)));
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(3), $"a 32 px export waited {watch.Elapsed} behind the cancelled one");
        await Task.Delay(500, TestContext.Current.CancellationToken);
        Assert.False(File.Exists(Path.Combine(folder, "big", "base_colour.png")), "the cancelled export wrote its file");
        Assert.False(File.Exists(Path.Combine(folder, "big", "base_colour.png.writing")), "the cancelled export left its temporary file");
    }
}
