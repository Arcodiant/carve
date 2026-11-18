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
    public async Task Targets_the_file_lacks_or_weights_outside_0_to_1_are_refused_naming_the_target()
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

        // The range render and check refuse (the person's ruling, 2026-11-18): 0 to 1, naming the target.
        Assert.Contains("targets.Bulge: weight 1.5 is outside 0 to 1", Refused(await carve.CallToolAsync("render_glb", """{"path":"refuse.glb","targets":{"Bulge":1.5}}""")));
        Assert.Contains("targets.Bulge: weight -0.25 is outside 0 to 1", Refused(await carve.CallToolAsync("render_glb", """{"path":"refuse.glb","targets":{"Bulge":-0.25}}""")));
        Assert.Contains("targets.Bulge: expected a number", Refused(await carve.CallToolAsync("render_glb", """{"path":"refuse.glb","targets":{"Bulge":"half"}}""")));
        Assert.Contains("targets: expected an object", Refused(await carve.CallToolAsync("render_glb", """{"path":"refuse.glb","targets":[0.5]}""")));
    }
}
