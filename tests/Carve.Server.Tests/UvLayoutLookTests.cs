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
    /// <summary>The ground's byte for a texel byte: sRGB-encode(decode(c) x 34/255), the vertex colour's dimming.</summary>
    internal static int Dimmed(int c)
    {
        static double Decode(double x) => x <= 0.04045 ? x / 12.92 : Math.Pow((x + 0.055) / 1.055, 2.4);
        static double Encode(double x) => x <= 0.0031308 ? x * 12.92 : 1.055 * Math.Pow(x, 1 / 2.4) - 0.055;
        return (int)Math.Round(Encode(Decode(c / 255.0) * 34 / 255) * 255);
    }

    private static bool Near((int R, int G, int B) got, (int R, int G, int B) want, int by = 2) =>
        Math.Abs(got.R - want.R) <= by && Math.Abs(got.G - want.G) <= by && Math.Abs(got.B - want.B) <= by;

    private static (int R, int G, int B) Bright(int x, int y) => Rgb(Texel(UvAt(x), UvAt(y)));

    private static (int R, int G, int B) Dim(int x, int y)
    {
        var (r, g, b) = Rgb(Texel(UvAt(x), UvAt(y)));
        return (Dimmed(r), Dimmed(g), Dimmed(b));
    }

    /// <summary>A seam's own pixel: #FFD800 within 10 a channel (the checker's yellow, #D8C830, is far off in red and blue).</summary>
    private static bool Yellowish((int R, int G, int B) p) => Near(p, (0xFF, 0xD8, 0x00), 10);

    /// <summary>The highlight's solid orange as shown: linear (1, 0.55, 0.1) to a byte, then sRGB-encoded.</summary>
    private static readonly (int R, int G, int B) OrangeShown = Shown(LookMeshes.Orange);

    private static (int R, int G, int B) Shown(System.Numerics.Vector4 linear)
    {
        static int One(float c)
        {
            var b = Math.Round(Math.Clamp(c, 0, 1) * 255) / 255;
            return (int)Math.Round((b <= 0.0031308 ? b * 12.92 : 1.055 * Math.Pow(b, 1 / 2.4) - 0.055) * 255);
        }

        return (One(linear.X), One(linear.Y), One(linear.Z));
    }

    public static TheoryData<string, string> RefusedArguments => new()
    {
        { "view", "\"top\"" },
        { "direction", "[0,0,1]" },
        { "zoom", "2" },
        { "target", "[0.5,0.5,0]" },
        { "projection", "\"orthographic\"" },
        { "views", "[\"front\",\"top\"]" },
        { "columns", "2" },
        { "pose", "{\"Root\":[0,0,0]}" },
        { "clip", "\"Walk\"" },
        { "time", "0.5" },
        { "bones", "true" },
        { "bone_labels", "true" },
        { "weights", "\"Root\"" },
    };

    [Theory]
    [MemberData(nameof(RefusedArguments))]
    public async Task Each_argument_the_layout_cannot_take_is_refused_by_name(string name, string value)
    {
        using var carve = new CarveProcess([], workingDirectory: Root);
        await carve.InitializeAsync();
        Ok(await carve.CallToolAsync("define_mesh", Quad));
        Ok(await carve.CallToolAsync("set_uvs", QuadUvs));
        var text = Refused(await carve.CallToolAsync("render", $$"""{"mode":"uvs","{{name}}":{{value}}}"""));
        Assert.Contains($"{name}: not with mode 'uvs'; ", text);
        Assert.Contains("1 problem", text);
    }

    [Fact]
    public async Task Refusals_are_collected_zoom_1_is_absent_and_labels_and_highlight_take_faces_only()
    {
        using var carve = new CarveProcess([], workingDirectory: Root);
        await carve.InitializeAsync();
        Ok(await carve.CallToolAsync("define_mesh", Quad));
        Ok(await carve.CallToolAsync("set_uvs", QuadUvs));
        var text = Refused(await carve.CallToolAsync("render", """{"mode":"uvs","view":"top","projection":"perspective","clip":"Walk"}"""));
        Assert.Contains("3 problem", text);
        foreach (var name in new[] { "view", "projection", "clip" }) Assert.Contains($"{name}: not with mode 'uvs'", text);

        Image(await carve.CallToolAsync("render", """{"mode":"uvs","zoom":1}"""), "zoom-1");
        foreach (var labels in new[] { "vertices", "both" })
        {
            Assert.Contains($"labels: '{labels}' is not drawn in mode 'uvs'; use 'faces' or 'none'", Refused(await carve.CallToolAsync("render", $$"""{"mode":"uvs","labels":"{{labels}}"}""")));
        }

        Assert.Contains("highlight: only faces in mode 'uvs'", Refused(await carve.CallToolAsync("render", """{"mode":"uvs","highlight":{"vertices":[0]}}""")));
        Assert.Contains("highlight: only faces in mode 'uvs'", Refused(await carve.CallToolAsync("render", """{"mode":"uvs","highlight":{"edges":[[0,1]]}}""")));

        var result = await carve.CallToolAsync("render", """{"mode":"uvs","labels":"faces","highlight":{"faces":[0]}}""");
        var image = Image(result, "labels-highlight");
        Assert.Equal("512x512, uvs mesh, labels faces, highlight 1 face, 4 vertices, 1 face", TextOf(result, 1));
        // Solid orange inside the face, away from its id at the centre; the margin still dim.
        var orange = Px(image, 120, 120);
        Assert.True(Near(orange, OrangeShown), $"(120, 120) is {orange}, orange {OrangeShown}");
        Assert.Equal(orange, Px(image, 400, 400));
        Assert.True(Near(Px(image, 256, 10), Dim(256, 10)), $"(256, 10) is {Px(image, 256, 10)}, dim {Dim(256, 10)}");
    }

    [Fact]
    public async Task A_mesh_with_no_uvs_names_the_three_tools_and_a_part_mapped_mesh_counts_the_rest()
    {
        using var carve = new CarveProcess([], workingDirectory: Root);
        await carve.InitializeAsync();
        Ok(await carve.CallToolAsync("define_mesh", Box("crate", 0)));
        Assert.Equal("mesh 'crate' has no UVs; give it some with set_uvs, project_uvs or unwrap", Refused(await carve.CallToolAsync("render", """{"mode":"uvs"}""")));

        Ok(await carve.CallToolAsync("set_uvs", QuadUvs));
        var result = await carve.CallToolAsync("render", """{"mode":"uvs"}""");
        var image = Image(result, "part-mapped");
        Assert.Equal("512x512, uvs crate, 5 faces unmapped, 8 vertices, 6 faces", TextOf(result, 1));
        Assert.True(Near(Px(image, 60, 60), Bright(60, 60)), $"(60, 60) is {Px(image, 60, 60)}");
    }

    [Fact]
    public async Task Among_two_meshes_mesh_is_required_and_names_the_one_drawn()
    {
        using var carve = new CarveProcess([], workingDirectory: Root);
        await carve.InitializeAsync();
        Ok(await carve.CallToolAsync("define_mesh", Box("left", 0)));
        Ok(await carve.CallToolAsync("define_mesh", Box("right", 3)));
        Ok(await carve.CallToolAsync("set_uvs", """{"mesh":"right","faces":[{"face":1,"uv":[[0,1],[1,1],[1,0],[0,0]]}]}"""));

        var text = Refused(await carve.CallToolAsync("render", """{"mode":"uvs"}"""));
        Assert.Contains("mode 'uvs' draws one mesh's layout", text);
        Assert.Contains("left", text);
        Assert.Contains("right", text);
        Assert.Equal("mesh 'left' has no UVs; give it some with set_uvs, project_uvs or unwrap", Refused(await carve.CallToolAsync("render", """{"mode":"uvs","mesh":"left"}""")));

        // The named mesh's face 1 (the model's face 7), highlighted by its own id; its face 6 does not exist.
        var result = await carve.CallToolAsync("render", """{"mode":"uvs","mesh":"right","highlight":{"faces":[1]}}""");
        Image(result, "two-meshes");
        Assert.Equal("512x512, uvs right, highlight 1 face, 5 faces unmapped, 8 vertices, 6 faces", TextOf(result, 1));
        Assert.Contains("face 6", Refused(await carve.CallToolAsync("render", """{"mode":"uvs","mesh":"right","highlight":{"faces":[6]}}""")));
    }

    [Fact]
    public async Task The_sizes_are_256_512_and_1024_and_a_shaded_frame_is_the_same_after_a_layout()
    {
        using var carve = new CarveProcess([], workingDirectory: Root);
        await carve.InitializeAsync();
        Ok(await carve.CallToolAsync("define_mesh", Quad));
        Ok(await carve.CallToolAsync("set_uvs", QuadUvs));
        var before = await carve.CallToolAsync("render", """{"view":"front"}""");
        Assert.False(IsError(before));

        foreach (var size in new[] { 256, 512, 1024 })
        {
            var result = await carve.CallToolAsync("render", $$"""{"mode":"uvs","size":{{size}}}""");
            var image = Image(result, $"size-{size}", size);
            Assert.Equal($"{size}x{size}, uvs mesh, 4 vertices, 1 face", TextOf(result, 1));
            // The origin cell white at the square's top-left at every size, and not at its v-flip.
            var (cell, lo) = (size * 7 / 64, size / 16);
            Assert.True(Whiteish(Px(image, lo + cell / 2, lo + cell / 2)), $"{size}: {Px(image, lo + cell / 2, lo + cell / 2)}");
            Assert.False(Whiteish(Px(image, lo + cell / 2, size - lo - cell / 2)), $"{size}: {Px(image, lo + cell / 2, size - lo - cell / 2)}");
        }

        var after = await carve.CallToolAsync("render", """{"view":"front"}""");
        Assert.False(IsError(after));
        Assert.Equal(before.GetProperty("content")[0].GetProperty("data").GetString(), after.GetProperty("content")[0].GetProperty("data").GetString());
    }

    /// <summary>
    /// Rule 2: T-56's barrel (<c>samples/barrel.carve</c>), every pixel worked from its UVs read here with <see cref="Carve.Core.CarveFile"/>:
    /// the empty bottom-right dim, the side faces' interiors bright, and every seam yellow on its UV segment.
    /// </summary>
    [Fact]
    public async Task The_barrel_shows_its_side_strip_bright_the_bottom_right_dim_and_its_seams_yellow()
    {
        var path = Path.Combine(Paths.RepoRoot, "samples", "barrel.carve");
        var read = Carve.Core.CarveFile.Read(File.ReadAllText(path));
        var mesh = read.Model!.Meshes[0].Mesh;
        var uvs = mesh.Uvs!;

        using var carve = new CarveProcess([], workingDirectory: Root);
        await carve.InitializeAsync();
        Ok(await carve.CallToolAsync("open", JsonSerializer.Serialize(new { path })));
        var result = await carve.CallToolAsync("render", """{"mode":"uvs"}""");
        var image = Image(result, "barrel");
        Assert.StartsWith("512x512, uvs mesh, ", TextOf(result, 1));

        static double Pixel(double uv) => ((uv - 0.5) / (8.0 / 7) + 0.5) * 512;

        // The bottom right, below the caps' shelf (v > 0.7053) and right of the top cap (u > 0.5568): no face there.
        foreach (var (x, y) in new[] { ((int)Pixel(0.8), (int)Pixel(0.85)), ((int)Pixel(0.7), (int)Pixel(0.95)) })
        {
            Assert.True(Near(Px(image, x, y), Dim(x, y)), $"({x}, {y}) is {Px(image, x, y)}, dim {Dim(x, y)}");
            Assert.False(Near(Px(image, x, y), Bright(x, y)), $"({x}, {y}) is bright");
        }

        // Each side face whose UV centroid lies 6 px or more inside every edge: bright, its texel's byte.
        var checkedFaces = 0;
        for (var face = 0; face < 32; face++)
        {
            var shape = mesh.Faces[face];
            var loop = Enumerable.Range(0, shape.Loop.Count).Select(i => uvs.Of(face)![shape.CornerAt(-1, i)]).ToArray();
            var (cx, cy) = ((int)Pixel(loop.Average(p => p.X)), (int)Pixel(loop.Average(p => p.Y)));
            var (u, v) = (UvAt(cx), UvAt(cy));
            if (!Inside(loop, u, v) || Clearance(loop, u, v) * 512 / (8.0 / 7) < 6) continue;
            Assert.True(Near(Px(image, cx, cy), Bright(cx, cy)), $"face {face} at ({cx}, {cy}) is {Px(image, cx, cy)}, texel {Bright(cx, cy)}");
            checkedFaces++;
        }

        Assert.True(checkedFaces >= 16, $"only {checkedFaces} side faces checked");

        // Every seam segment drawn #FFD800 within 2 px of its UV midpoint (a line's pixels are partly covered, so one in the 5 x 5); the seam column at u ~0.03 and ~0.96, not yellow 6 px off it.
        var seams = 0;
        var column = new List<double>();
        foreach (var face in Enumerable.Range(0, mesh.Faces.Count))
        {
            var shape = mesh.Faces[face];
            for (var i = 0; i < shape.Loop.Count; i++)
            {
                var j = (i + 1) % shape.Loop.Count;
                if (!uvs.IsSeam(shape.Loop[i], shape.Loop[j])) continue;
                var (a, b) = (uvs.Of(face)![shape.CornerAt(-1, i)], uvs.Of(face)![shape.CornerAt(-1, j)]);
                var (mx, my) = (Pixel((a.X + b.X) / 2.0), Pixel((a.Y + b.Y) / 2.0));
                // The line's pixels are partly covered (MSAA): one in the 5 x 5 about the midpoint must be at least 40% #FFD800 over
                // the ground or face beside the line, read 4 px either side across it.
                var (nx, ny) = (-(Pixel(b.Y) - Pixel(a.Y)), Pixel(b.X) - Pixel(a.X));
                var length = Math.Sqrt(nx * nx + ny * ny);
                (nx, ny) = (nx / length * 4, ny / length * 4);
                (int R, int G, int B)[] beside = [Px(image, (int)(mx + nx), (int)(my + ny)), Px(image, (int)(mx - nx), (int)(my - ny))];
                var best = Enumerable.Range(-2, 5).SelectMany(dx => Enumerable.Range(-2, 5).Select(dy => Px(image, (int)mx + dx, (int)my + dy)))
                    .SelectMany(p => beside.Select(bg => SeamShare(p, bg))).Max();
                Assert.True(best >= 0.4, $"seam {shape.Loop[i]}-{shape.Loop[j]} of face {face} at ({mx:0.0}, {my:0.0}) is at most {best:0.00} yellow");
                if (face < 32 && Math.Abs(a.X - b.X) < 0.01) column.Add((a.X + b.X) / 2.0);
                seams++;
            }
        }

        Assert.True(seams >= 8 + 8 + 8, $"{seams} seam segments");
        Assert.Contains(column, u => Math.Abs(u - 0.03) < 0.02);
        Assert.Contains(column, u => Math.Abs(u - 0.96) < 0.03);
        var (sx, sy) = ((int)Pixel(column.Min()) + 6, (int)Pixel(0.2));
        Assert.True(SeamShare(Px(image, sx, sy), Bright(sx, sy)) < 0.25, $"({sx}, {sy}), 6 px off the seam, is {Px(image, sx, sy)}");
        Assert.False(Yellowish(Px(image, sx, sy)));
    }

    /// <summary>
    /// How much of a pixel is #FFD800 blended over <paramref name="bg"/>, fitted in linear light (the target blends linearly and
    /// is encoded after); 0 where the fit is poor (more than 0.04 off in a channel).
    /// </summary>
    private static double SeamShare((int R, int G, int B) p, (int R, int G, int B) bg)
    {
        static double L(int c) { var x = c / 255.0; return x <= 0.04045 ? x / 12.92 : Math.Pow((x + 0.055) / 1.055, 2.4); }
        double[] got = [L(p.R), L(p.G), L(p.B)], under = [L(bg.R), L(bg.G), L(bg.B)], seam = [1, L(0xD8), 0];
        double num = 0, den = 0;
        for (var c = 0; c < 3; c++)
        {
            num += (got[c] - under[c]) * (seam[c] - under[c]);
            den += (seam[c] - under[c]) * (seam[c] - under[c]);
        }

        if (den < 1e-6) return 0;
        var f = Math.Clamp(num / den, 0, 1);
        var fits = Enumerable.Range(0, 3).All(c => Math.Abs(under[c] + f * (seam[c] - under[c]) - got[c]) <= 0.04);
        return fits ? f : 0;
    }

    private static bool Inside(IReadOnlyList<System.Numerics.Vector2> loop, double u, double v)
    {
        var inside = false;
        for (int i = 0, j = loop.Count - 1; i < loop.Count; j = i++)
        {
            var (a, b) = (loop[i], loop[j]);
            if ((a.Y > v) != (b.Y > v) && u < (b.X - a.X) * (v - a.Y) / (b.Y - a.Y) + a.X) inside = !inside;
        }

        return inside;
    }

    /// <summary>The UV distance from (u, v) to the nearest side of the loop.</summary>
    private static double Clearance(IReadOnlyList<System.Numerics.Vector2> loop, double u, double v)
    {
        var best = double.MaxValue;
        for (var i = 0; i < loop.Count; i++)
        {
            var (a, b) = (loop[i], loop[(i + 1) % loop.Count]);
            double dx = b.X - a.X, dy = b.Y - a.Y;
            var t = Math.Clamp(((u - a.X) * dx + (v - a.Y) * dy) / (dx * dx + dy * dy), 0, 1);
            best = Math.Min(best, Math.Sqrt(Math.Pow(u - a.X - t * dx, 2) + Math.Pow(v - a.Y - t * dy, 2)));
        }

        return best;
    }
}
