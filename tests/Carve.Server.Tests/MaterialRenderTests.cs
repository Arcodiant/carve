using System.IO.Compression;
using System.Numerics;
using System.Text.Json;
using StbImageSharp;

namespace Carve.Server.Tests;

/// <summary>Small RGB PNGs for material tests, made in memory.</summary>
internal static class TestPng
{
    private static readonly uint[] Crc = Enumerable.Range(0, 256).Select(n =>
    {
        var c = (uint)n;
        for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
        return c;
    }).ToArray();

    public static byte[] Encode(int width, int height, Func<int, int, (byte R, byte G, byte B)> at)
    {
        var raw = new byte[height * (1 + width * 3)];
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var (r, g, b) = at(x, y);
            var i = y * (1 + width * 3) + 1 + x * 3;
            (raw[i], raw[i + 1], raw[i + 2]) = (r, g, b);
        }

        using var packed = new MemoryStream();
        using (var z = new ZLibStream(packed, CompressionLevel.Optimal, leaveOpen: true)) z.Write(raw);
        var header = new byte[13];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(header, (uint)width);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4), (uint)height);
        header[8] = 8;
        header[9] = 2;
        using var png = new MemoryStream();
        png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        Chunk(png, "IHDR", header);
        Chunk(png, "IDAT", packed.ToArray());
        Chunk(png, "IEND", []);
        return png.ToArray();
    }

    private static void Chunk(Stream png, string type, byte[] data)
    {
        Span<byte> four = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(four, (uint)data.Length);
        png.Write(four);
        var body = System.Text.Encoding.ASCII.GetBytes(type).Concat(data).ToArray();
        png.Write(body);
        var crc = 0xFFFFFFFFu;
        foreach (var b in body) crc = Crc[(crc ^ b) & 0xFF] ^ (crc >> 8);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(four, crc ^ 0xFFFFFFFFu);
        png.Write(four);
    }

    /// <summary>
    /// The marker (rule P5): 64 x 64, four 32-texel quadrants as stored, top row first: red top-left, so at uv (0, 0); green
    /// top-right; blue bottom-left; white bottom-right. A u-mirror swaps red and green, a v-flip red and blue.
    /// </summary>
    public static byte[] Marker { get; } = Encode(64, 64, (x, y) => (x < 32, y < 32) switch
    {
        (true, true) => (255, 0, 0),
        (false, true) => (0, 255, 0),
        (true, false) => (0, 0, 255),
        _ => (255, 255, 255),
    });

    /// <summary>Two halves split at u = 0.5 (rows the same): <paramref name="left"/> then <paramref name="right"/>.</summary>
    public static byte[] Halves((byte, byte, byte) left, (byte, byte, byte) right, bool across = false) =>
        Encode(64, 64, (x, y) => (across ? y : x) < 32 ? left : right);
}

/// <summary>
/// T-78: a face with a material through the real <c>carve</c>'s <c>render</c>, drawn as the game's PBR shading draws it. Every
/// sample is a cell centre of the +Z face worked from the cube, its UVs and the camera, by the shared pixel rules P1 to P6.
/// </summary>
[Trait("Tier", "regression")]
public sealed class MaterialRenderTests() : FileToolTests("carve-material-render")
{
    /// <summary>A level difference that counts (rule P3).</summary>
    internal const int M = 12;

    /// <summary>"Lit, not near-black": halfway between probe step 2d's metal without the sky (11) and with it (150).</summary>
    internal const int T = 80;

    private static readonly string Frames =
        Directory.CreateDirectory(Path.Combine(Paths.RepoRoot, "artifacts", "material-tests")).FullName;

    internal static string Box(string name, float x0) =>
        $$"""{"name":"{{name}}","vertices":[[{{x0}},0,0],[{{x0 + 1}},0,0],[{{x0 + 1}},1,0],[{{x0}},1,0],[{{x0}},0,1],[{{x0 + 1}},0,1],[{{x0 + 1}},1,1],[{{x0}},1,1]],"faces":[[0,3,2,1],[4,5,6,7],[0,1,5,4],[3,7,6,2],[0,4,7,3],[1,2,6,5]]}""";

    /// <summary>The +Z face, [4,5,6,7], laid so uv (0,0) is its top-left as seen from +Z: u = x - x0, v = 1 - y.</summary>
    internal static string FrontUvs(string mesh) =>
        $$"""{"mesh":"{{mesh}}","faces":[{"face":1,"uv":[[0,1],[1,1],[1,0],[0,0]]}]}""";

    private static readonly Vector3 Min = Vector3.Zero, Max = Vector3.One;

    /// <summary>The +Z face's point at cell (c, r)'s centre of an 8 x 8 grid (8 texels a cell), for a box whose low x is <paramref name="x0"/>.</summary>
    private static Vector3 Cell(int c, int r, float x0 = 0) => new(x0 + (c + 0.5f) / 8, 1 - (r + 0.5f) / 8, 1);

    internal static Vector2 PixelOf(Vector3 world, CameraSpec camera, Vector3 min, Vector3 max, int size = 512, Projection projection = Projection.Perspective)
    {
        var space = DrawnSpace.Of(min, max);
        var drawn = camera.Target is { } t ? camera with { Target = space.Apply(t) } : camera;
        var frame = ViewFraming.Frame(space.Apply(max), drawn, projection);
        return ScreenProjection.ToPixel(space.Apply(world), frame.View, frame.Projection, size);
    }

    private static Vector2 Z(Vector3 world, int size = 512) => PixelOf(world, new CameraSpec(Vector3.UnitZ), Min, Max, size);

    private static (int R, int G, int B) At(ImageResult image, Vector2 pixel) => CheckerLookTests.At(image, pixel);

    /// <summary>The marker's class of a pixel: white when every channel is lit (the sky makes it bluish), else by channel order (rule P3), or "other".</summary>
    internal static string Class((int R, int G, int B) p) =>
        Math.Min(p.R, Math.Min(p.G, p.B)) >= T ? "white"
        : p.R > p.G + M && p.R > p.B + M ? "red"
        : p.G > p.R + M && p.G > p.B + M ? "green"
        : p.B > p.R + M && p.B > p.G + M ? "blue"
        : "other";

    /// <summary>
    /// Rule P5 with P6's floor: 16 samples, four in each quadrant, corner cells among them, each the marker's class there.
    /// A u-mirror, a v-flip or a mirrored camera swaps two quadrants and fails.
    /// </summary>
    /// <remarks><paramref name="bottom"/> and <paramref name="step"/> move the lower quadrants' samples up, for a frame where the face's foot is out of sight.</remarks>
    private static void MarkerUnflipped(ImageResult image, Func<Vector3, Vector2> pixel, string what, float x0 = 0, int bottom = 5, int step = 2)
    {
        foreach (var (c0, r0, expected) in new[] { (0, 0, "red"), (5, 0, "green"), (0, bottom, "blue"), (5, bottom, "white") })
        foreach (var (dc, dr) in new[] { (0, 0), (2, 0), (0, step), (2, step) })
        {
            var p = At(image, pixel(Cell(c0 + dc, r0 + dr, x0)));
            Assert.True(Class(p) == expected, $"{what}: cell ({c0 + dc}, {r0 + dr}) is {p}, {Class(p)}, not {expected}");
        }
    }

    private async Task<(ImageResult Image, string Caption)> RenderAsync(CarveProcess carve, string arguments, string keepAs, int width = 512)
    {
        var result = await carve.CallToolAsync("render", arguments);
        if (IsError(result)) Assert.Fail(TextOf(result));
        var png = Convert.FromBase64String(Png(result));
        File.WriteAllBytes(Path.Combine(Frames, keepAs + ".png"), png);
        var image = ImageResult.FromMemory(png, ColorComponents.RedGreenBlueAlpha);
        Assert.Equal(width, image.Width);
        return (image, TextOf(result, 1));
    }

    private static string Png(JsonElement result) => result.GetProperty("content")[0].GetProperty("data").GetString()!;

    private async Task<CarveProcess> MarkedAsync(string mesh = "mesh", float x0 = 0)
    {
        File.WriteAllBytes(Path.Combine(Root, "marker.png"), TestPng.Marker);
        var carve = new CarveProcess([], workingDirectory: Root);
        await carve.InitializeAsync();
        Ok(await carve.CallToolAsync("define_mesh", Box(mesh, x0)));
        Ok(await carve.CallToolAsync("set_uvs", FrontUvs(mesh)));
        Ok(await carve.CallToolAsync("define_material", """{"name":"marker","base_colour_texture":"marker.png"}"""));
        Ok(await carve.CallToolAsync("set_material", $$"""{"mesh":"{{mesh}}","faces":[1],"material":"marker"}"""));
        return carve;
    }

    [Fact]
    public async Task A_textured_face_with_each_camera_size_sheet_label_and_highlight_argument_draws_the_marker_unflipped()
    {
        using var carve = await MarkedAsync();

        var (front, caption) = await RenderAsync(carve, """{"view":"front"}""", "view-front");
        Assert.Equal("512x512, view front, 8 vertices, 6 faces", caption);
        MarkerUnflipped(front, p => PixelOf(p, new CameraSpec(ViewFraming.NamedViews["front"]), Min, Max), "view front");

        // From behind the +Z face is culled: what is there is the -Z face, uncoloured, not the marker.
        var (back, _) = await RenderAsync(carve, """{"view":"back"}""", "view-back");
        Assert.True(back.Width == 512);

        var zoomed3 = new CameraSpec(Vector3.UnitZ, 3, new Vector3(0.5f, 0.5f, 1));
        (var close, caption) = await RenderAsync(carve, """{"view":"+z","zoom":3,"target":[0.5,0.5,1]}""", "zoom-3");
        Assert.Equal("512x512, view +z, zoom 3, target (0.5, 0.5, 1), 8 vertices, 6 faces", caption);
        // At zoom 3 on the face's centre, where the four quadrants meet: two cells of each, one beside the meeting point.
        foreach (var (c, r, expected) in new[] { (3, 3, "red"), (2, 2, "red"), (4, 3, "green"), (5, 2, "green"), (3, 4, "blue"), (2, 5, "blue"), (4, 4, "white"), (5, 5, "white") })
        {
            var p = At(close, PixelOf(Cell(c, r), zoomed3, Min, Max));
            Assert.True(Class(p) == expected, $"zoom 3: cell ({c}, {r}) is {p}, not {expected}");
        }

        var slanted = new CameraSpec(new Vector3(0.3f, 0.2f, 1), 1.5f, new Vector3(0.5f, 0.5f, 1));
        (var direction, caption) = await RenderAsync(carve, """{"direction":[0.3,0.2,1],"zoom":1.5,"target":[0.5,0.5,1]}""", "direction");
        Assert.Equal("512x512, direction (0.3, 0.2, 1), zoom 1.5, target (0.5, 0.5, 1), 8 vertices, 6 faces", caption);
        MarkerUnflipped(direction, p => PixelOf(p, slanted, Min, Max), "direction");

        (var ortho, caption) = await RenderAsync(carve, """{"view":"+z","projection":"orthographic"}""", "projection");
        Assert.Equal("512x512, view +z, orthographic, 8 vertices, 6 faces", caption);
        MarkerUnflipped(ortho, p => PixelOf(p, new CameraSpec(Vector3.UnitZ), Min, Max, projection: Projection.Orthographic), "orthographic");

        foreach (var size in new[] { 256, 1024 })
        {
            (var sized, caption) = await RenderAsync(carve, $$"""{"view":"+z","size":{{size}}}""", $"size-{size}", size);
            Assert.Equal($"{size}x{size}, view +z, 8 vertices, 6 faces", caption);
            MarkerUnflipped(sized, p => Z(p, size), $"size {size}");
        }

        (var sheet, caption) = await RenderAsync(carve, """{"views":["front","+z"],"columns":2,"size":256}""", "views", 2 * 256 + 2);
        Assert.Equal("2x1 tiles of 256x256, views front, +z, 8 vertices, 6 faces", caption);
        MarkerUnflipped(sheet, p => Z(p, 256) + new Vector2(258, 0), "the +z tile");

        (var labelled, caption) = await RenderAsync(carve, """{"view":"+z","labels":"faces"}""", "labels");
        Assert.Equal("512x512, view +z, labels faces, 8 vertices, 6 faces", caption);
        MarkerUnflipped(labelled, p => Z(p), "labels faces");

        // A highlighted material face is its texture times orange: white turns orange, red stays red.
        (var picked, caption) = await RenderAsync(carve, """{"view":"+z","highlight":{"faces":[1]}}""", "highlight");
        Assert.Equal("512x512, view +z, highlight 1 face, 8 vertices, 6 faces", caption);
        foreach (var (c, r) in new[] { (5, 5), (7, 5), (5, 7), (7, 7), (6, 6), (5, 6), (6, 5), (7, 6) })
        {
            var (pr, pg, pb) = At(picked, Z(Cell(c, r)));
            Assert.True(pr > pg + M && pg > pb + M, $"highlighted white cell ({c}, {r}) is ({pr}, {pg}, {pb}), not orange");
        }
        Assert.Equal("red", Class(At(picked, Z(Cell(1, 1)))));
    }

    [Fact]
    public async Task A_material_follows_the_mesh_named_a_pose_and_a_clip()
    {
        using (var two = await MarkedAsync())
        {
            Ok(await two.CallToolAsync("define_mesh", Box("other", 2)));
            // Every mesh is drawn, framed on them all; 'mesh' names the one whose ids label the image.
            var (one, _) = await RenderAsync(two, """{"view":"+z","mesh":"mesh","labels":"faces"}""", "mesh");
            MarkerUnflipped(one, p => PixelOf(p, new CameraSpec(Vector3.UnitZ), Min, new Vector3(3, 1, 1)), "mesh");
        }

        using var carve = await MarkedAsync();
        Ok(await carve.CallToolAsync("define_skeleton", """{"bones":[{"name":"base","parent":null,"position":[0,0,0],"rotation":[0,0,0,1]}]}"""));
        Ok(await carve.CallToolAsync("set_weights", """{"mesh":"mesh","vertices":[0,1,2,3,4,5,6,7],"weights":{"base":1}}"""));
        Ok(await carve.CallToolAsync("define_clip", """{"name":"drop","duration":1,"loop":false,"channels":{"base":{"translation":{"interpolation":"LINEAR","keys":[{"time":0,"value":[0,0,0]},{"time":1,"value":[0,-0.5,0]}]}}}}"""));

        // Down a quarter, two cells, with the framing on the rest pose: the marker moves down the screen with the face.
        var down = new Vector3(0, -0.25f, 0);
        var (posed, caption) = await RenderAsync(carve, """{"view":"+z","pose":{"base":{"translation":[0,-0.25,0]}}}""", "pose");
        Assert.StartsWith("512x512, view +z", caption);
        MarkerUnflipped(posed, p => Z(p + down), "pose", bottom: 4, step: 1);
        Assert.NotEqual("red", Class(At(posed, Z(Cell(1, 0)))));

        (var clipped, caption) = await RenderAsync(carve, """{"view":"+z","clip":"drop","time":0.5}""", "clip");
        Assert.StartsWith("512x512, view +z", caption);
        MarkerUnflipped(clipped, p => Z(p + down), "clip", bottom: 4, step: 1);
    }

    [Fact]
    public async Task A_material_face_beside_colour_faces_leaves_them_exactly_as_they_were()
    {
        using var carve = new CarveProcess([], workingDirectory: Root);
        await carve.InitializeAsync();
        File.WriteAllBytes(Path.Combine(Root, "marker.png"), TestPng.Marker);
        Ok(await carve.CallToolAsync("define_mesh", Box("mesh", 0)));
        Ok(await carve.CallToolAsync("set_uvs", FrontUvs("mesh")));
        Ok(await carve.CallToolAsync("define_material", """{"name":"marker","base_colour_texture":"marker.png"}"""));
        // +X coloured, +Y uncoloured: from front both are seen beside the +Z face.
        Ok(await carve.CallToolAsync("set_colour", """{"faces":[5],"colour":"#C04020"}"""));
        var front = new CameraSpec(ViewFraming.NamedViews["front"]);
        var (before, _) = await RenderAsync(carve, """{"view":"front"}""", "beside-before");
        Ok(await carve.CallToolAsync("set_material", """{"faces":[1],"material":"marker"}"""));
        var (after, _) = await RenderAsync(carve, """{"view":"front"}""", "beside-after");

        // Rule P1, locality: every interior sample of the +X and +Y faces is the same bytes; the +Z face is the marker now.
        var samples = 0;
        for (var a = 1; a <= 7; a += 2)
        for (var b = 1; b <= 7; b += 2)
        {
            var (u, v) = (a / 8f, b / 8f);
            foreach (var world in new[] { new Vector3(1, u, v), new Vector3(u, 1, v) })
            {
                Assert.Equal(At(before, PixelOf(world, front, Min, Max)), At(after, PixelOf(world, front, Min, Max)));
                samples++;
            }
        }
        Assert.Equal(32, samples);
        Assert.Equal("red", Class(At(before, PixelOf(new Vector3(1, 0.5f, 0.5f), front, Min, Max))));
        MarkerUnflipped(after, p => PixelOf(p, front, Min, Max), "beside a colour");
    }

    [Fact]
    public async Task Double_sided_is_drawn_from_behind_and_single_sided_is_not()
    {
        using var carve = new CarveProcess([], workingDirectory: Root);
        await carve.InitializeAsync();
        // One quad, wound counter-clockwise seen from +Z.
        Ok(await carve.CallToolAsync("define_mesh", """{"vertices":[[0,0,0],[1,0,0],[1,1,0],[0,1,0]],"faces":[[0,1,2,3]]}"""));
        Ok(await carve.CallToolAsync("define_material", """{"name":"leaf","base_colour":"#20C040","double_sided":true}"""));
        Ok(await carve.CallToolAsync("set_material", """{"faces":[0],"material":"leaf"}"""));
        var inside = new[] { (0.1f, 0.1f), (0.9f, 0.1f), (0.1f, 0.9f), (0.9f, 0.9f), (0.5f, 0.5f), (0.3f, 0.7f), (0.7f, 0.3f), (0.5f, 0.2f) };
        var behind = new CameraSpec(-Vector3.UnitZ);
        var (two, _) = await RenderAsync(carve, """{"view":"-z"}""", "double-sided-behind");
        foreach (var (x, y) in inside)
            Assert.Equal("green", Class(At(two, PixelOf(new Vector3(x, y, 0), behind, Min, new Vector3(1, 1, 0)))));

        Ok(await carve.CallToolAsync("define_material", """{"name":"leaf","base_colour":"#20C040","double_sided":false}"""));
        var (one, _) = await RenderAsync(carve, """{"view":"-z"}""", "single-sided-behind");
        var background = At(one, new Vector2(2, 2));
        foreach (var (x, y) in inside)
            Assert.Equal(background, At(one, PixelOf(new Vector3(x, y, 0), behind, Min, new Vector3(1, 1, 0))));
        var (seen, _) = await RenderAsync(carve, """{"view":"+z"}""", "single-sided-front");
        foreach (var (x, y) in inside)
            Assert.Equal("green", Class(At(seen, PixelOf(new Vector3(x, y, 0), new CameraSpec(Vector3.UnitZ), Min, new Vector3(1, 1, 0)))));
    }

    [Fact]
    public async Task Metal_is_lit_under_the_sky_and_an_emissive_face_glows()
    {
        using var carve = await MarkedAsync();
        var cells = Enumerable.Range(0, 8).SelectMany(c => new[] { (c, 0), (c, 7), (c, 3) }).ToArray();
        var (matte, _) = await RenderAsync(carve, """{"view":"+z"}""", "metal-before");

        Ok(await carve.CallToolAsync("define_material", """{"name":"steel","base_colour":"#CCCCCC","metallic":1,"roughness":0.35}"""));
        Ok(await carve.CallToolAsync("set_material", """{"faces":[1],"material":"steel"}"""));
        var (steel, _) = await RenderAsync(carve, """{"view":"+z"}""", "metal");
        foreach (var (c, r) in cells)
        {
            var (pr, pg, pb) = At(steel, Z(Cell(c, r)));
            Assert.True(Math.Max(pr, Math.Max(pg, pb)) >= T, $"metal cell ({c}, {r}) is ({pr}, {pg}, {pb}), near-black");
        }

        Ok(await carve.CallToolAsync("define_material", """{"name":"glow","base_colour":"#202020","emissive":"#FF8020"}"""));
        Ok(await carve.CallToolAsync("set_material", """{"faces":[1],"material":"glow"}"""));
        Ok(await carve.CallToolAsync("define_material", """{"name":"dull","base_colour":"#202020"}"""));
        Ok(await carve.CallToolAsync("set_material", """{"faces":[5],"material":"dull"}"""));
        var (glow, _) = await RenderAsync(carve, """{"view":"+z"}""", "emissive");
        var (glowFront, _) = await RenderAsync(carve, """{"view":"front"}""", "emissive-front");
        foreach (var (c, r) in cells)
        {
            var (pr, pg, pb) = At(glow, Z(Cell(c, r)));
            Assert.True(pr >= 230 && pr > pg + M && pg > pb + M, $"emissive cell ({c}, {r}) is ({pr}, {pg}, {pb}), not glowing");
            Assert.True(pr >= At(matte, Z(Cell(c, r))).R + M || pg + M <= pr, $"emissive cell ({c}, {r}) is no brighter");
        }
        // Rule P4, absent: the dull +X face, the same base without emissive, glows nowhere.
        var front = new CameraSpec(ViewFraming.NamedViews["front"]);
        for (var a = 1; a <= 7; a += 2)
        for (var b = 1; b <= 7; b += 2)
        {
            var (pr, pg, pb) = At(glowFront, PixelOf(new Vector3(1, a / 8f, b / 8f), front, Min, Max));
            Assert.False(pr >= 230 && pr > pg + M && pg > pb + M, $"the dull face glows at ({pr}, {pg}, {pb})");
        }
    }

    [Fact]
    public async Task Every_other_look_ignores_materials_as_it_ignores_colours()
    {
        using var carve = new CarveProcess([], workingDirectory: Root);
        await carve.InitializeAsync();
        File.WriteAllBytes(Path.Combine(Root, "marker.png"), TestPng.Marker);
        Ok(await carve.CallToolAsync("define_mesh", Box("mesh", 0)));
        Ok(await carve.CallToolAsync("set_uvs", FrontUvs("mesh")));
        Ok(await carve.CallToolAsync("define_skeleton", """{"bones":[{"name":"base","parent":null,"position":[0,0,0],"rotation":[0,0,0,1]}]}"""));
        Ok(await carve.CallToolAsync("define_material", """{"name":"marker","base_colour_texture":"marker.png","emissive":"#FF8020"}"""));
        var looks = new[] { """{"mode":"wireframe"}""", """{"mode":"normals"}""", """{"mode":"checker"}""", """{"mode":"uvs"}""", """{"weights":"base"}""" };
        var before = new List<string>();
        foreach (var look in looks) before.Add(Png(await carve.CallToolAsync("render", look)));
        Ok(await carve.CallToolAsync("set_material", """{"faces":[0,1,2,3,4,5],"material":"marker"}"""));
        for (var i = 0; i < looks.Length; i++)
        {
            var after = await carve.CallToolAsync("render", looks[i]);
            File.WriteAllBytes(Path.Combine(Frames, $"look-{i}.png"), Convert.FromBase64String(Png(after)));
            Assert.True(before[i] == Png(after), $"{looks[i]} changed when the faces took a material");
        }
    }
}
