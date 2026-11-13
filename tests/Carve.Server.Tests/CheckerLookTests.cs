using System.Numerics;
using System.Text.Json;
using Carve.Scenario.Crate;
using StbImageSharp;

namespace Carve.Server.Tests;

/// <summary>T-57: the checker look through the real <c>carve</c>. Every cell checked is worked from the cube and the UVs given.</summary>
[Trait("Tier", "regression")]
public sealed class CheckerLookTests() : TempFolderTests("carve-checker-look")
{
    private const string Cube =
        """{"vertices":[[0,0,0],[1,0,0],[1,1,0],[0,1,0],[0,0,1],[1,0,1],[1,1,1],[0,1,1]],"faces":[[0,3,2,1],[4,5,6,7],[0,1,5,4],[3,7,6,2],[0,4,7,3],[1,2,6,5]]}""";

    /// <summary>The +Z face, [4,5,6,7], laid so uv (0,0) is its top-left as seen from +Z: u = x, v = 1 - y.</summary>
    private const string FrontUvs = """{"faces":[{"face":1,"uv":[[0,1],[1,1],[1,0],[0,0]]}]}""";

    private static readonly string Frames =
        Directory.CreateDirectory(Path.Combine(Paths.RepoRoot, "artifacts", "checker-tests")).FullName;

    internal static ImageResult Image(JsonElement result, string keepAs, int size = 512)
    {
        Assert.False(IsError(result), result.ToString());
        var png = Convert.FromBase64String(result.GetProperty("content")[0].GetProperty("data").GetString()!);
        File.WriteAllBytes(Path.Combine(Frames, keepAs + ".png"), png);
        var image = ImageResult.FromMemory(png, ColorComponents.RedGreenBlueAlpha);
        Assert.Equal(size, image.Width);
        return image;
    }

    internal static (int R, int G, int B) At(ImageResult image, Vector2 pixel)
    {
        var (x, y) = ((int)MathF.Floor(pixel.X), (int)MathF.Floor(pixel.Y));
        var at = (y * image.Width + x) * 4;
        return (image.Data[at], image.Data[at + 1], image.Data[at + 2]);
    }

    /// <summary>Where a point of the unit cube lands, looking along <paramref name="direction"/>.</summary>
    internal static Vector2 PixelOf(Vector3 world, Vector3 direction, int size = 512)
    {
        var space = DrawnSpace.Of(Vector3.Zero, Vector3.One);
        var frame = ViewFraming.Frame(space.Apply(Vector3.One), new CameraSpec(direction));
        return ScreenProjection.ToPixel(space.Apply(world), frame.View, frame.Projection, size);
    }

    /// <summary>The +Z face's point at cell (c, r)'s centre, under <see cref="FrontUvs"/>.</summary>
    private static Vector3 FrontCell(int c, int r) => new((c + 0.5f) / 8, 1 - (r + 0.5f) / 8, 1);

    [Fact]
    public async Task Probe_the_checker_draws_on_the_gpu_unflipped_with_vertex_colour_multiplied()
    {
        using var carve = new CarveProcess([], workingDirectory: Root);
        await carve.InitializeAsync();
        Assert.False(IsError(await carve.CallToolAsync("define_mesh", Cube)));
        Assert.False(IsError(await carve.CallToolAsync("set_uvs", FrontUvs)));

        var head = Image(await carve.CallToolAsync("render", """{"view":"+z","mode":"checker"}"""), "probe-z");
        var corner = Image(await carve.CallToolAsync("render", """{"mode":"checker"}"""), "probe-front");
        var log = new List<string>();
        for (var r = 0; r < 8; r++)
        {
            log.Add(string.Join("  ", Enumerable.Range(0, 8).Select(c => At(head, PixelOf(FrontCell(c, r), Vector3.UnitZ)))));
        }

        var top = At(corner, PixelOf(new Vector3(0.5f, 1, 0.5f), ViewFraming.NamedViews["front"]));
        var side = At(corner, PixelOf(new Vector3(1, 0.5f, 0.5f), ViewFraming.NamedViews["front"]));
        log.Add($"unmapped +y {top}  unmapped +x {side}");
        File.WriteAllLines(Path.Combine(Frames, "probe-cells.txt"), log);

        // Hue order along row 0 and down column 0, from the geometry: column c's hue at every cell but the white one.
        for (var c = 1; c < 8; c++)
        {
            var (pr, pg, pb) = At(head, PixelOf(FrontCell(c, 0), Vector3.UnitZ));
            Assert.Equal(c, CheckerColours.NearestColumn(pr, pg, pb));
        }

        var (dr, dg, db) = At(head, PixelOf(FrontCell(0, 7), Vector3.UnitZ));
        Assert.Equal(0, CheckerColours.NearestColumn(dr, dg, db));

        // White at uv (0,0), the top-left as seen, and not at its mirrors (a v-flipped upload or a u-mirrored texture).
        Assert.True(IsWhite(At(head, PixelOf(FrontCell(0, 0), Vector3.UnitZ))));
        Assert.False(IsWhite(At(head, PixelOf(FrontCell(7, 0), Vector3.UnitZ))));
        Assert.False(IsWhite(At(head, PixelOf(FrontCell(0, 7), Vector3.UnitZ))));
        // A light cell above its dark neighbour, near a corner and in the middle.
        Assert.True(Value(At(head, PixelOf(FrontCell(0, 6), Vector3.UnitZ))) > Value(At(head, PixelOf(FrontCell(0, 7), Vector3.UnitZ))));
        Assert.True(Value(At(head, PixelOf(FrontCell(4, 4), Vector3.UnitZ))) > Value(At(head, PixelOf(FrontCell(3, 4), Vector3.UnitZ))));
        // Unmapped faces are lit magenta: the vertex colour is multiplied into the texture.
        Assert.True(IsMagenta(top), top.ToString());
        Assert.True(IsMagenta(side), side.ToString());
        Assert.False(IsMagenta(At(head, PixelOf(FrontCell(7, 1), Vector3.UnitZ))));
    }

    private static bool IsWhite((int R, int G, int B) p) => CheckerColours.IsWhite(p.R, p.G, p.B);

    private static bool IsMagenta((int R, int G, int B) p) => CheckerColours.IsMagenta(p.R, p.G, p.B);

    private static int Value((int R, int G, int B) p) => CheckerColours.Value(p.R, p.G, p.B);
}
