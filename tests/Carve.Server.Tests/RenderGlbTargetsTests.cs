using System.Text.Json;
using StbImageSharp;
using static Carve.Server.Tests.CarveProcess;

namespace Carve.Server.Tests;

/// <summary>
/// T-108: render_glb at morph target weights, through the real server and the engine's own loader, <c>MorphWeights</c> and
/// GPU morph-before-skin. The placeholder content is T-103's cylinder (8 sides, rings at y 0, 1 and 2, radius 0.5) with a
/// <c>Bulge</c> that scales the middle ring 1.4 times about the axis, exported and drawn at weights 0, 0.5 and 1.
/// </summary>
[Trait("Tier", "regression")]
public class RenderGlbTargetsTests
{
    private const int Size = 512;
    private const int Sides = 8;
    private const double Radius = 0.5;

    private static readonly string Root = Directory.CreateTempSubdirectory("carve-render-glb-targets-").FullName;

    /// <summary>Kept, for a person to look at: <c>artifacts/render-glb-targets-tests</c>.</summary>
    private static readonly string Frames =
        Directory.CreateDirectory(Path.Combine(Paths.RepoRoot, "artifacts", "render-glb-targets-tests")).FullName;

    private static async Task<CarveProcess> StartAsync()
    {
        var carve = new CarveProcess([], workingDirectory: Root);
        await carve.InitializeAsync();
        return carve;
    }

    /// <summary>The cylinder with its Bulge, exported to <paramref name="file"/>.</summary>
    private static async Task ExportCylinderAsync(CarveProcess carve, string file)
    {
        var vertices = new List<double[]>();
        for (var ring = 0; ring < 3; ring++)
        for (var k = 0; k < Sides; k++)
        {
            var a = 2 * Math.PI * k / Sides;
            vertices.Add([Math.Round(Radius * Math.Cos(a), 6), ring, Math.Round(-Radius * Math.Sin(a), 6)]);
        }

        var faces = new List<int[]>();
        for (var ring = 0; ring < 2; ring++)
        for (var k = 0; k < Sides; k++)
            faces.Add([Sides * ring + k, Sides * ring + ((k + 1) % Sides), Sides * (ring + 1) + ((k + 1) % Sides), Sides * (ring + 1) + k]);
        faces.Add(Enumerable.Range(0, Sides).Reverse().ToArray());
        faces.Add(Enumerable.Range(2 * Sides, Sides).ToArray());
        Ok(await carve.CallToolAsync("define_mesh", JsonSerializer.Serialize(new { vertices, faces })));
        Ok(await carve.CallToolAsync("set_shading", """{"shading":"smooth"}"""));

        var positions = new Dictionary<string, double[]>();
        for (var k = 0; k < Sides; k++)
        {
            var a = 2 * Math.PI * k / Sides;
            positions[(Sides + k).ToString()] = [Math.Round(Radius * 1.4 * Math.Cos(a), 6), 1, Math.Round(-Radius * 1.4 * Math.Sin(a), 6)];
        }

        Ok(await carve.CallToolAsync("define_target", JsonSerializer.Serialize(new { name = "Bulge", positions })));
        Ok(await carve.CallToolAsync("export", JsonSerializer.Serialize(new { path = file })));
    }

    private static void Ok(JsonElement result) => Assert.False(IsError(result), IsError(result) ? TextOf(result) : "");

    private static string Refused(JsonElement result)
    {
        Assert.True(IsError(result), TextOf(result));
        return TextOf(result);
    }

    private static byte[] Png(JsonElement result, string keepAs)
    {
        Ok(result);
        var png = Convert.FromBase64String(result.GetProperty("content")[0].GetProperty("data").GetString()!);
        File.WriteAllBytes(Path.Combine(Frames, keepAs + ".png"), png);
        return png;
    }

    private static ImageResult Decode(byte[] png) => ImageResult.FromMemory(png, ColorComponents.RedGreenBlueAlpha);

    private static bool IsBackground(ImageResult image, int x, int y)
    {
        var i = (y * image.Width + x) * 4;
        return Math.Abs(image.Data[i] - 48) <= 1 && Math.Abs(image.Data[i + 1] - 52) <= 1 && Math.Abs(image.Data[i + 2] - 56) <= 1;
    }

    /// <summary>The count of pixels that are not the background along the row at the cylinder's middle ring (y 1 of 0 to 2: the image's middle).</summary>
    private static int WidthAtMiddle(ImageResult image)
    {
        var y = Size / 2;
        return Enumerable.Range(0, Size).Count(x => !IsBackground(image, x, y));
    }

    private const string Front = "\"view\":\"+z\",\"projection\":\"orthographic\"";

    [Fact]
    public async Task The_bulge_widens_the_middle_of_the_cylinder_as_the_weight_says_and_the_frame_does_not_move()
    {
        using var carve = await StartAsync();
        await ExportCylinderAsync(carve, "bulge.glb");

        var at = new Dictionary<double, ImageResult>();
        foreach (var weight in new[] { 0.0, 0.5, 1.0 })
        {
            var result = await carve.CallToolAsync("render_glb", $$"""{"path":"bulge.glb","targets":{"Bulge":{{weight.ToString(System.Globalization.CultureInfo.InvariantCulture)}}},{{Front}}}""");
            at[weight] = Decode(Png(result, $"bulge-{weight:0.0}-glb"));
            Assert.Contains($"targets Bulge {weight}", TextOf(result, 1));
        }

        // The middle ring's x extent is its radius times the weight's scale (1, 1.2, 1.4); the frame holds the rest bounds, so
        // the widths in pixels keep that ratio. Weights ignored would give three equal widths.
        var (w0, w5, w1) = (WidthAtMiddle(at[0.0]), WidthAtMiddle(at[0.5]), WidthAtMiddle(at[1.0]));
        Assert.InRange((double)w1 / w0, 1.4 * 0.97, 1.4 * 1.03);
        Assert.InRange((double)w5 / w0, 1.2 * 0.97, 1.2 * 1.03);

        // The caps do not move: the silhouette's top and bottom rows are the same at every weight.
        foreach (var y in new[] { 0, Size - 1 })
            Assert.Equal(Enumerable.Range(0, Size).Count(x => !IsBackground(at[0.0], x, y)), Enumerable.Range(0, Size).Count(x => !IsBackground(at[1.0], x, y)));
    }

    [Fact]
    public async Task Without_targets_and_at_weight_zero_the_frame_is_what_it_always_was()
    {
        using var carve = await StartAsync();
        await ExportCylinderAsync(carve, "same.glb");

        var plain = Png(await carve.CallToolAsync("render_glb", $$"""{"path":"same.glb",{{Front}}}"""), "same-plain-glb");
        var empty = Png(await carve.CallToolAsync("render_glb", $$"""{"path":"same.glb","targets":{},{{Front}}}"""), "same-empty-glb");
        var zero = Png(await carve.CallToolAsync("render_glb", $$"""{"path":"same.glb","targets":{"Bulge":0},{{Front}}}"""), "same-zero-glb");
        Assert.Equal(plain, empty);
        Assert.Equal(Decode(plain).Data, Decode(zero).Data);
    }

    [Fact]
    public async Task Targets_the_file_lacks_are_refused_naming_the_target()
    {
        using var carve = await StartAsync();
        await ExportCylinderAsync(carve, "refuse.glb");
        Ok(await carve.CallToolAsync("define_mesh", """{"vertices":[[0,0,0],[1,0,0],[1,1,0],[0,1,0],[0,0,1],[1,0,1],[1,1,1],[0,1,1]],"faces":[[0,3,2,1],[4,5,6,7],[0,1,5,4],[3,7,6,2],[0,4,7,3],[1,2,6,5]],"drop_targets":true}"""));
        Ok(await carve.CallToolAsync("export", """{"path":"plain.glb"}"""));

        var unknown = Refused(await carve.CallToolAsync("render_glb", """{"path":"refuse.glb","targets":{"Bulg":0.5}}"""));
        Assert.Contains("targets.Bulg", unknown);
        Assert.Contains("the targets are 'Bulge'", unknown);

        var none = Refused(await carve.CallToolAsync("render_glb", """{"path":"plain.glb","targets":{"Bulge":0.5}}"""));
        Assert.Contains("targets.Bulge", none);
        Assert.Contains("no morph targets", none);

        // The name is told before the weight, as render tells it; the weight's own refusals are RenderGlbTargetArgumentTests, on the harness.
        var both = Refused(await carve.CallToolAsync("render_glb", """{"path":"refuse.glb","targets":{"Nope":2}}"""));
        Assert.Contains("targets.Nope: the file has no target named 'Nope'", both);
        Assert.DoesNotContain("outside 0 to 1", both);
    }

    [Theory]
    [InlineData("""{"Bulge":2}""", "targets.Bulge: weight 2 is outside 0 to 1")]
    [InlineData("""{"Bulge":1e999}""", "targets.Bulge: the weight is not a finite number")]
    public async Task A_known_target_with_a_refused_weight_is_refused_and_nothing_is_drawn(string targets, string expected)
    {
        using var carve = await StartAsync();
        var file = $"weight-{Math.Abs(targets.GetHashCode())}.glb";
        await ExportCylinderAsync(carve, file);

        // RV-65: the refused weight must never be carried on to the draw as 0, so it is checked on the real engine.
        var result = await carve.CallToolAsync("render_glb", $$"""{"path":"{{file}}","targets":{{targets}}}""");
        Assert.Contains(expected, Refused(result));
        Assert.DoesNotContain("image/png", result.GetRawText());
    }

    [Fact]
    public async Task Every_bad_entry_is_told_in_one_call_and_a_weight_is_told_before_a_path_error()
    {
        using var carve = await StartAsync();
        await ExportCylinderAsync(carve, "several.glb");

        var several = Refused(await carve.CallToolAsync("render_glb", """{"path":"several.glb","targets":{"Bulge":"half","Other":2,"Bulg":1e999}}"""));
        Assert.Contains("targets.Bulge: expected a number", several);
        Assert.Contains("targets.Other: the file has no target named 'Other'", several);
        Assert.Contains("targets.Bulg: the file has no target named 'Bulg'", several);
        Assert.DoesNotContain("outside 0 to 1", several);

        // Before a path error: a weight is told, as before a7c00c6; the name of a target waits for the file.
        var missing = Refused(await carve.CallToolAsync("render_glb", """{"path":"missing.glb","targets":{"Bulge":2}}"""));
        Assert.Contains("targets.Bulge: weight 2 is outside 0 to 1", missing);
        Assert.DoesNotContain("no file at", missing);
        var named = Refused(await carve.CallToolAsync("render_glb", """{"path":"missing.glb","targets":{"Nope":0.5}}"""));
        Assert.Contains("no file at", named);
    }

    /// <summary>
    /// D-6's frame rule (Orcward's): a pixel differs when its luma differs past 4 of 255; a frame fails past 0.5 % of its
    /// pixels differing or with an 8-connected patch of them past 50.
    /// </summary>
    private static (int Differing, int Patch) Measure(ImageResult a, ImageResult b)
    {
        Assert.Equal((a.Width, a.Height), (b.Width, b.Height));
        var w = a.Width;
        var differs = new bool[w * a.Height];
        var count = 0;
        for (var p = 0; p < differs.Length; p++)
        {
            var i = p * 4;
            var d = 0.2126 * (a.Data[i] - b.Data[i]) + 0.7152 * (a.Data[i + 1] - b.Data[i + 1]) + 0.0722 * (a.Data[i + 2] - b.Data[i + 2]);
            if (Math.Abs(d) > 4) { differs[p] = true; count++; }
        }

        var seen = new bool[differs.Length];
        var stack = new Stack<int>();
        var largest = 0;
        for (var start = 0; start < differs.Length; start++)
        {
            if (!differs[start] || seen[start]) continue;
            var size = 0;
            seen[start] = true;
            stack.Push(start);
            while (stack.Count > 0)
            {
                var p = stack.Pop();
                size++;
                for (var dy = -1; dy <= 1; dy++)
                for (var dx = -1; dx <= 1; dx++)
                {
                    int nx = p % w + dx, ny = p / w + dy;
                    if (nx < 0 || ny < 0 || nx >= w || ny >= a.Height) continue;
                    var n = ny * w + nx;
                    if (differs[n] && !seen[n]) { seen[n] = true; stack.Push(n); }
                }
            }

            largest = Math.Max(largest, size);
        }

        return (count, largest);
    }

    [Fact]
    public async Task The_exported_file_at_a_weight_matches_render_of_the_model_at_the_same_weight_under_the_frame_rule()
    {
        using var carve = await StartAsync();
        await ExportCylinderAsync(carve, "agree.glb");

        var results = new List<string>();
        foreach (var weight in new[] { 0.0, 0.5, 1.0 })
        {
            var w = weight.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var model = Decode(Png(await carve.CallToolAsync("render", $$"""{"targets":{"Bulge":{{w}}},{{Front}}}"""), $"agree-{w}-render"));
            var file = Decode(Png(await carve.CallToolAsync("render_glb", $$"""{"path":"agree.glb","targets":{"Bulge":{{w}}},{{Front}}}"""), $"agree-{w}-glb"));
            var (differing, patch) = Measure(model, file);
            results.Add($"weight {w}: {differing} of {model.Width * model.Height} pixels differ, largest patch {patch}");
            Assert.True(differing * 200L <= model.Width * model.Height && patch <= 50, results[^1]);
        }

        File.WriteAllLines(Path.Combine(Frames, "agree.txt"), results);

        // The rule can fail: the file at weight 1 against the model at weight 0 is a different shape.
        var rest = Decode(Png(await carve.CallToolAsync("render", $$"""{"targets":{"Bulge":0},{{Front}}}"""), "agree-control-render"));
        var full = Decode(Png(await carve.CallToolAsync("render_glb", $$"""{"path":"agree.glb","targets":{"Bulge":1},{{Front}}}"""), "agree-control-glb"));
        var (cd, cp) = Measure(rest, full);
        Assert.True(cd * 200L > rest.Width * rest.Height || cp > 50, $"control: {cd} pixels, patch {cp}");
    }
}
