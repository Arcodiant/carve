using System.Numerics;
using System.Text.Json;
using Carve.Scenario.Crate;
using StbImageSharp;
using static Carve.Server.Tests.CarveProcess;

namespace Carve.Server.Tests;

/// <summary>
/// T-57: <c>mode: "checker"</c> with each of render's argument families, through the real <c>carve</c>. Each frame's
/// caption is asserted whole and one cell is predicted from the geometry and the UVs given: the white cell at uv (0,0),
/// which a mirrored camera, a u-mirrored texture or a v-flipped upload would put elsewhere.
/// </summary>
[Trait("Tier", "regression")]
public sealed class CheckerArgumentTests() : FileToolTests("carve-checker-args")
{
    private static string Box(string name, float x0) =>
        $$"""{"name":"{{name}}","vertices":[[{{x0}},0,0],[{{x0 + 1}},0,0],[{{x0 + 1}},1,0],[{{x0}},1,0],[{{x0}},0,1],[{{x0 + 1}},0,1],[{{x0 + 1}},1,1],[{{x0}},1,1]],"faces":[[0,3,2,1],[4,5,6,7],[0,1,5,4],[3,7,6,2],[0,4,7,3],[1,2,6,5]]}""";

    /// <summary>The +Z face, [4,5,6,7], laid so uv (0,0) is its top-left as seen from +Z: u = x - x0, v = 1 - y.</summary>
    private static string FrontUvs(string mesh) =>
        $$"""{"mesh":"{{mesh}}","faces":[{"face":1,"uv":[[0,1],[1,1],[1,0],[0,0]]}]}""";

    private static readonly Vector3 Min = Vector3.Zero, Max = Vector3.One;

    /// <summary>The +Z face's point at cell (c, r)'s centre, for a box whose low x is <paramref name="x0"/>.</summary>
    private static Vector3 Cell(int c, int r, float x0 = 0) => new(x0 + (c + 0.5f) / 8, 1 - (r + 0.5f) / 8, 1);

    private static Vector2 PixelOf(Vector3 world, CameraSpec camera, Vector3 min, Vector3 max, int size = 512, Projection projection = Projection.Perspective)
    {
        var space = DrawnSpace.Of(min, max);
        // A target is given in the mesh's coordinates and framed in the drawn ones.
        var drawn = camera.Target is { } t ? camera with { Target = space.Apply(t) } : camera;
        var frame = ViewFraming.Frame(space.Apply(max), drawn, projection);
        return ScreenProjection.ToPixel(space.Apply(world), frame.View, frame.Projection, size);
    }

    private static Vector2 Z(Vector3 world, int size = 512) => PixelOf(world, new CameraSpec(Vector3.UnitZ), Min, Max, size);

    private static (int R, int G, int B) At(ImageResult image, Vector2 pixel, int dx = 0, int dy = 0) =>
        CheckerLookTests.At(image, pixel + new Vector2(dx, dy));

    private static bool White((int R, int G, int B) p) => CheckerColours.IsWhite(p.R, p.G, p.B);

    private static bool Magenta((int R, int G, int B) p) => CheckerColours.IsMagenta(p.R, p.G, p.B);

    private static void WhiteAtOriginOnly(ImageResult image, Func<Vector3, Vector2> pixel, string what, float x0 = 0)
    {
        Assert.True(White(At(image, pixel(Cell(0, 0, x0)))), $"{what}: uv (0,0) is {At(image, pixel(Cell(0, 0, x0)))}, not white");
        Assert.False(White(At(image, pixel(Cell(7, 0, x0)))), $"{what}: the u-mirror of uv (0,0) is white");
        Assert.False(White(At(image, pixel(Cell(0, 7, x0)))), $"{what}: the v-flip of uv (0,0) is white");
    }

    private async Task<(ImageResult Image, string Caption)> RenderAsync(CarveProcess carve, string arguments, string keepAs, int width = 512)
    {
        var result = await carve.CallToolAsync("render", arguments);
        return (CheckerLookTests.Image(result, "args-" + keepAs, width), TextOf(result, 1));
    }

    [Fact]
    public async Task Checker_with_each_camera_size_sheet_label_and_highlight_argument_draws_the_cell_predicted()
    {
        using var carve = new CarveProcess([], workingDirectory: Root);
        await carve.InitializeAsync();
        Ok(await carve.CallToolAsync("define_mesh", Box("mesh", 0)));
        Ok(await carve.CallToolAsync("set_uvs", FrontUvs("mesh")));

        var (front, caption) = await RenderAsync(carve, """{"view":"front","mode":"checker"}""", "view");
        Assert.Equal("512x512, view front, checker, 8 vertices, 6 faces", caption);
        WhiteAtOriginOnly(front, p => PixelOf(p, new CameraSpec(ViewFraming.NamedViews["front"]), Min, Max), "view front");

        var close = new CameraSpec(new Vector3(0.3f, 0.2f, 1), 2, new Vector3(0.25f, 0.75f, 1));
        (var zoomed, caption) = await RenderAsync(carve, """{"direction":[0.3,0.2,1],"zoom":2,"target":[0.25,0.75,1],"mode":"checker"}""", "direction");
        Assert.Equal("512x512, direction (0.3, 0.2, 1), checker, zoom 2, target (0.25, 0.75, 1), 8 vertices, 6 faces", caption);
        Assert.True(White(At(zoomed, PixelOf(Cell(0, 0), close, Min, Max))));
        Assert.False(White(At(zoomed, PixelOf(Cell(1, 0), close, Min, Max))));

        (var ortho, caption) = await RenderAsync(carve, """{"view":"+z","projection":"orthographic","mode":"checker"}""", "projection");
        Assert.Equal("512x512, view +z, orthographic, checker, 8 vertices, 6 faces", caption);
        WhiteAtOriginOnly(ortho, p => PixelOf(p, new CameraSpec(Vector3.UnitZ), Min, Max, projection: Projection.Orthographic), "orthographic");

        foreach (var size in new[] { 256, 1024 })
        {
            (var sized, caption) = await RenderAsync(carve, $$"""{"view":"+z","size":{{size}},"mode":"checker"}""", $"size-{size}", size);
            Assert.Equal($"{size}x{size}, view +z, checker, 8 vertices, 6 faces", caption);
            WhiteAtOriginOnly(sized, p => Z(p, size), $"size {size}");
        }

        // The second tile, +z, is offset by a tile and the 2-pixel gutter.
        (var sheet, caption) = await RenderAsync(carve, """{"views":["front","+z"],"columns":2,"size":256,"mode":"checker"}""", "views", 2 * 256 + 2);
        Assert.Equal("2x1 tiles of 256x256, checker, views front, +z, 8 vertices, 6 faces", caption);
        WhiteAtOriginOnly(sheet, p => Z(p, 256) + new Vector2(258, 0), "the +z tile");

        (var labelled, caption) = await RenderAsync(carve, """{"view":"+z","labels":"faces","mode":"checker"}""", "labels");
        Assert.Equal("512x512, view +z, checker, labels faces, 8 vertices, 6 faces", caption);
        WhiteAtOriginOnly(labelled, p => Z(p), "labels faces");

        // A highlighted face is the checker times orange: the white cell turns orange, the rest stays its hue's.
        (var picked, caption) = await RenderAsync(carve, """{"view":"+z","highlight":{"faces":[1]},"mode":"checker"}""", "highlight");
        Assert.Equal("512x512, view +z, checker, highlight 1 face, 8 vertices, 6 faces", caption);
        var (r, g, b) = At(picked, Z(Cell(0, 0)));
        Assert.True(r > g && g > b && !White((r, g, b)), $"the highlighted white cell is ({r}, {g}, {b}), not orange");
    }

    [Fact]
    public async Task Checker_draws_the_mesh_named_or_the_whole_model_and_says_when_no_face_has_uvs()
    {
        using var carve = new CarveProcess([], workingDirectory: Root);
        await carve.InitializeAsync();
        Ok(await carve.CallToolAsync("define_mesh", Box("plain", 0)));

        // A mesh without UVs, alone: all magenta, and the caption says so.
        var front = new CameraSpec(ViewFraming.NamedViews["front"]);
        var (plain, caption) = await RenderAsync(carve, """{"view":"front","mode":"checker"}""", "mesh-alone");
        Assert.Equal("512x512, view front, checker, no face has UVs, 8 vertices, 6 faces", caption);
        foreach (var centre in new[] { new Vector3(0.5f, 0.5f, 1), new Vector3(1, 0.5f, 0.5f), new Vector3(0.5f, 1, 0.5f) })
        {
            Assert.True(Magenta(At(plain, PixelOf(centre, front, Min, Max))), $"{centre} is {At(plain, PixelOf(centre, front, Min, Max))}");
        }

        Ok(await carve.CallToolAsync("define_mesh", Box("mapped", 2)));
        Ok(await carve.CallToolAsync("set_uvs", FrontUvs("mapped")));

        // Every mesh is drawn, as one: the second mesh's UVs reach the GPU through Model.Combined, the first is magenta.
        // The note counts over the mesh named, else the whole.
        var (min, max) = (Vector3.Zero, new Vector3(3, 1, 1));
        var plusZ = new CameraSpec(Vector3.UnitZ);
        foreach (var (arguments, keepAs, expected) in new[]
                 {
                     ("""{"view":"+z","mode":"checker"}""", "mesh-whole", "512x512, view +z, checker, 16 vertices, 12 faces"),
                     ("""{"view":"+z","mesh":"mapped","mode":"checker"}""", "mesh-mapped", "512x512, view +z, checker, 16 vertices, 12 faces"),
                     ("""{"view":"+z","mesh":"plain","mode":"checker"}""", "mesh-plain", "512x512, view +z, checker, no face has UVs, 16 vertices, 12 faces"),
                 })
        {
            (var whole, caption) = await RenderAsync(carve, arguments, keepAs);
            Assert.Equal(expected, caption);
            WhiteAtOriginOnly(whole, p => PixelOf(p, plusZ, min, max), keepAs, 2);
            Assert.True(Magenta(At(whole, PixelOf(new Vector3(0.5f, 0.5f, 1), plusZ, min, max))), keepAs);
        }
    }

    [Fact]
    public async Task Checker_follows_a_pose_and_a_clip()
    {
        using var carve = new CarveProcess([], workingDirectory: Root);
        await carve.InitializeAsync();
        Ok(await carve.CallToolAsync("define_mesh", Box("mesh", 0)));
        Ok(await carve.CallToolAsync("set_uvs", FrontUvs("mesh")));
        Ok(await carve.CallToolAsync("define_skeleton", """{"bones":[{"name":"base","parent":null,"position":[0,0,0],"rotation":[0,0,0,1]}]}"""));
        Ok(await carve.CallToolAsync("set_weights", """{"mesh":"mesh","vertices":[0,1,2,3,4,5,6,7],"weights":{"base":1}}"""));
        Ok(await carve.CallToolAsync("define_clip", """{"name":"drop","duration":1,"loop":false,"channels":{"base":{"translation":{"interpolation":"LINEAR","keys":[{"time":0,"value":[0,0,0]},{"time":1,"value":[0,-0.5,0]}]}}}}"""));

        // Down a quarter, two cells: framing stays on the rest pose, so uv (0,0) moves down on screen to where cell
        // (0, 2) was at rest, and cell (0, 2), light red, to where cell (0, 4) was.
        var down = new Vector3(0, -0.25f, 0);
        var (posed, caption) = await RenderAsync(carve, """{"view":"+z","pose":{"base":{"translation":[0,-0.25,0]}},"mode":"checker"}""", "pose");
        Assert.StartsWith("512x512, view +z, checker", caption);
        Assert.True(White(At(posed, Z(Cell(0, 0) + down))));
        Assert.False(White(At(posed, Z(Cell(0, 0)))));
        Assert.Equal(0, Column(At(posed, Z(Cell(0, 2) + down))));

        (var clipped, caption) = await RenderAsync(carve, """{"view":"+z","clip":"drop","time":0.5,"mode":"checker"}""", "clip");
        Assert.StartsWith("512x512, view +z, checker", caption);
        Assert.True(White(At(clipped, Z(Cell(0, 0) + down))));
        Assert.False(White(At(clipped, Z(Cell(0, 0)))));
    }

    [Fact]
    public async Task Checker_refuses_weights_names_itself_in_the_mode_error_and_leaves_shaded_unchanged_by_uvs()
    {
        using var carve = new CarveProcess([], workingDirectory: Root);
        await carve.InitializeAsync();
        Ok(await carve.CallToolAsync("define_mesh", Box("mesh", 0)));
        var shadedBefore = await carve.CallToolAsync("render", """{"view":"front"}""");
        Ok(await carve.CallToolAsync("set_uvs", FrontUvs("mesh")));
        var shadedAfter = await carve.CallToolAsync("render", """{"view":"front"}""");
        Assert.Equal(Png(shadedBefore), Png(shadedAfter));

        Assert.Equal("mode: 'xray' is not a mode; use 'shaded', 'wireframe', 'normals', 'checker' or 'uvs'.", Refused(await carve.CallToolAsync("render", """{"mode":"xray"}""")));
        Ok(await carve.CallToolAsync("define_skeleton", """{"bones":[{"name":"base","parent":null,"position":[0,0,0],"rotation":[0,0,0,1]}]}"""));
        Assert.Contains("checker", Refused(await carve.CallToolAsync("render", """{"weights":"base","mode":"checker"}""")));
    }

    /// <summary>
    /// RV-43: render's checker example, read from its description, on define_mesh's cube: the list it calls upright makes
    /// check report nothing on face 1, and the one it calls mirrored makes check report face 1 flipped.
    /// </summary>
    [Fact]
    public async Task The_checker_examples_upright_list_is_unflipped_and_its_mirrored_one_flipped()
    {
        var (carve, tools) = await StartProcessAsync();
        using var _ = carve;
        var text = HelpToolTests.WithReference(Description(tools, "render"), "render");
        var start = text.IndexOf("Example: on define_mesh's unit cube, face 1 is [4,5,6,7]", StringComparison.Ordinal);
        Assert.True(start >= 0, "render's checker example is missing");
        var sentence = text[start..text.IndexOf("The caption names the mode", start, StringComparison.Ordinal)];
        var lists = System.Text.RegularExpressions.Regex.Matches(sentence, @"\[\[[-0-9.,\[\] ]*\]\]").Select(m => m.Value).ToArray();
        Assert.Equal(2, lists.Length);

        string[] Face1Warnings(JsonElement body) => [.. body.GetProperty("warnings").EnumerateArray().Select(w => w.GetString()!)
            .Where(w => w.StartsWith("faces[1]", StringComparison.Ordinal))];
        Ok(await carve.CallToolAsync("set_uvs", $$"""{"faces":[{"face":1,"uv":{{lists[0]}}}]}"""));
        Assert.Empty(Face1Warnings(JsonDocument.Parse(Ok(await carve.CallToolAsync("check", "{}"))).RootElement));
        Ok(await carve.CallToolAsync("set_uvs", $$"""{"faces":[{"face":1,"uv":{{lists[1]}}}]}"""));
        Assert.Equal(["faces[1]: UVs flipped, so the texture is mirrored on the face"],
            Face1Warnings(JsonDocument.Parse(Ok(await carve.CallToolAsync("check", "{}"))).RootElement));
    }

    private static int Column((int R, int G, int B) p) => CheckerColours.NearestColumn(p.R, p.G, p.B);

    private static string Png(JsonElement result) => result.GetProperty("content")[0].GetProperty("data").GetString()!;
}
