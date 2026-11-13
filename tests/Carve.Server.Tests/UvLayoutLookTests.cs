using System.Text.Json;
using StbImageSharp;

namespace Carve.Server.Tests;

/// <summary>
/// T-58: <c>render</c> <c>mode: "uvs"</c> through the real <c>carve</c>. Every pixel checked is worked from the UVs given and
/// the window rule written out here (the unit square alone: centre 0.5, side 8/7), never read from the server.
/// </summary>
[Trait("Tier", "regression")]
public sealed class UvLayoutLookTests() : TempFolderTests("carve-uv-layout")
{
    /// <summary>One quad in the z = 0 plane, seen from +Z with vertex 3 at its top-left.</summary>
    private const string Quad = """{"vertices":[[0,0,0],[1,0,0],[1,1,0],[0,1,0]],"faces":[[0,1,2,3]]}""";

    /// <summary>The quad over the whole unit square, uv (0,0) at its top-left vertex.</summary>
    private const string QuadUvs = """{"faces":[{"face":0,"uv":[[0,1],[1,1],[1,0],[0,0]]}]}""";

    private static string Box(string name, int x0) =>
        $$"""{"name":"{{name}}","vertices":[[{{x0}},0,0],[{{x0 + 1}},0,0],[{{x0 + 1}},1,0],[{{x0}},1,0],[{{x0}},0,1],[{{x0 + 1}},0,1],[{{x0 + 1}},1,1],[{{x0}},1,1]],"faces":[[0,3,2,1],[4,5,6,7],[0,1,5,4],[3,7,6,2],[0,4,7,3],[1,2,6,5]]}""";

    private static readonly string Frames =
        Directory.CreateDirectory(Path.Combine(Paths.RepoRoot, "artifacts", "uv-layout-tests")).FullName;

    /// <summary>The UV a pixel centre samples, the unit square alone in the window: u = 0.5 - 4/7 + (x + 0.5) (8/7) / size.</summary>
    internal static double UvAt(int pixel, int size = 512) => 0.5 - 4.0 / 7 + (pixel + 0.5) * (8.0 / 7) / size;

    /// <summary>The checker's texel at a UV, repeated, as #RRGGBB.</summary>
    internal static uint Texel(double u, double v)
    {
        static int CellOf(double t) => (int)Math.Floor((t - Math.Floor(t)) * 8);
        return CheckerTexture.Cell(CellOf(u), CellOf(v));
    }

    private static (int R, int G, int B) Rgb(uint c) => ((int)(c >> 16 & 0xFF), (int)(c >> 8 & 0xFF), (int)(c & 0xFF));

    private static (int R, int G, int B) Px(ImageResult image, int x, int y)
    {
        var at = (y * image.Width + x) * 4;
        return (image.Data[at], image.Data[at + 1], image.Data[at + 2]);
    }

    private static ImageResult Image(JsonElement result, string keepAs, int size = 512)
    {
        Assert.False(IsError(result), IsError(result) ? TextOf(result) : "");
        var png = Convert.FromBase64String(result.GetProperty("content")[0].GetProperty("data").GetString()!);
        File.WriteAllBytes(Path.Combine(Frames, keepAs + ".png"), png);
        var image = ImageResult.FromMemory(png, ColorComponents.RedGreenBlueAlpha);
        Assert.Equal(size, image.Width);
        Assert.Equal(size, image.Height);
        return image;
    }

    private static string Refused(JsonElement result)
    {
        Assert.True(IsError(result), TextOf(result));
        return TextOf(result);
    }

    private static void Ok(JsonElement result) => Assert.False(IsError(result), TextOf(result));

    private static bool Whiteish((int R, int G, int B) p) => Math.Min(p.R, Math.Min(p.G, p.B)) >= 240;

    [Fact]
    public async Task Probe_the_layout_draws_unlit_unflipped_with_the_ground_dimmed()
    {
        using var carve = new CarveProcess([], workingDirectory: Root);
        await carve.InitializeAsync();
        Ok(await carve.CallToolAsync("define_mesh", Quad));
        Ok(await carve.CallToolAsync("set_uvs", QuadUvs));
        var result = await carve.CallToolAsync("render", """{"mode":"uvs"}""");
        var image = Image(result, "probe");
        Assert.Equal("512x512, uvs mesh, 4 vertices, 1 face", TextOf(result, 1));

        // Every cell centre of the square, and of the margin's ring of cells, against the texel it samples.
        var log = new List<string> { "x y u v texel got" };
        foreach (var x in new[] { 10, 60, 116, 172, 228, 284, 340, 396, 452, 501 })
        {
            foreach (var y in new[] { 10, 60, 116, 452, 501 })
            {
                var (u, v) = (UvAt(x), UvAt(y));
                log.Add(FormattableString.Invariant($"{x} {y} {u:0.0000} {v:0.0000} #{Texel(u, v):X6} {Px(image, x, y)}"));
            }
        }

        File.WriteAllLines(Path.Combine(Frames, "probe-pixels.txt"), log);

        // uv (0,0)'s white cell at the square's top-left, and not at its v-flip or its u-mirror.
        Assert.True(Whiteish(Px(image, 60, 60)), $"(60, 60) is {Px(image, 60, 60)}");
        Assert.False(Whiteish(Px(image, 60, 452)), $"(60, 452) is {Px(image, 60, 452)}");
        Assert.False(Whiteish(Px(image, 452, 60)), $"(452, 60) is {Px(image, 452, 60)}");
        // The margin is the ground: the dimmed checker, darker than the same texel drawn bright.
        var (gr, gg, gb) = Px(image, 10, 10);
        var (tr, tg, tb) = Rgb(Texel(UvAt(10), UvAt(10)));
        Assert.True(gr < tr && gg < tg && gb < tb, $"the margin at (10, 10) is {(gr, gg, gb)} against its texel {(tr, tg, tb)}");
    }
}
