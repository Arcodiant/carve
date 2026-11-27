using Patina.Graphs.Evaluation;
using Patina.Graphs.Imaging;
using Patina.Graphs.Nodes;
using Xunit;

namespace Patina.Graphs.Tests;

/// <summary>
/// T-240's <c>image</c> node: texel for texel at the graph's size in each kind, the normal decode unit length, a 2x and a
/// 0.5x image against bilinear worked by hand, the wrap at the seam, tile-size and cap invariance, every fault (found at
/// validation), a nested graph's image, and a table of broken copies each caught.
/// </summary>
[Trait("Tier", "unit")]
public sealed class ImageNodeTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("patina-t240-").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch (IOException) { }
    }

    private string WritePng(string name, int width, int height, byte[] rgba)
    {
        var path = Path.Combine(_dir, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, PngWriter.Encode(rgba, width, height));
        return path;
    }

    /// <summary>An image whose texel (x, y) is (<paramref name="r"/>, <paramref name="g"/>, <paramref name="b"/>) of it, alpha 77 (never read).</summary>
    private static byte[] Rgba(int width, int height, Func<int, int, (int R, int G, int B)> texel)
    {
        var rgba = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var (r, g, b) = texel(x, y);
                var i = (y * width + x) * 4;
                (rgba[i], rgba[i + 1], rgba[i + 2], rgba[i + 3]) = ((byte)r, (byte)g, (byte)b, 77);
            }
        return rgba;
    }

    private static byte[] Hashed(int width, int height) => TestPng.Bytes(width * height * 4, (uint)(width * 31 + height));

    private TextureGraph Graph(string nodes, int size)
    {
        var read = TexGraphFile.Read($$"""{ "version": 2, "size": {{size}}, "seed": 0, "nodes": { {{nodes}} } }""", folder: _dir);
        Assert.True(read.Loaded, read.Error?.ToString());
        Assert.Empty(read.Faults);
        return read.Graph!;
    }

    private static string Image(string path, string kind, string id = "t") =>
        $$""" "{{id}}": { "type": "image", "params": { "path": "{{path}}", "kind": "{{kind}}" } } """;

    /// <summary>Node "t"'s texels, three channels each (grey three times), at <paramref name="size"/>, in the tile size and cap given.</summary>
    private float[] Run(string path, string kind, int size, int? tileSize = null, long? cacheBytes = null)
    {
        var role = kind == "grey" ? "roughness" : kind == "normal" ? "normal" : "base_colour";
        var graph = Graph(Image(path, kind) + $$""", "o": { "type": "output", "inputs": { "in": "t" }, "params": { "role": "{{role}}" } }""", size);
        var result = new float[size * size * 3];
        new GraphEvaluator(graph).Evaluate(role, size, tile =>
        {
            for (var y = tile.Rect.Y; y < tile.Rect.Bottom; y++)
                for (var x = tile.Rect.X; x < tile.Rect.Right; x++)
                {
                    var (r, g, b) = tile.Colour(x, y);
                    (result[(y * size + x) * 3], result[(y * size + x) * 3 + 1], result[(y * size + x) * 3 + 2]) = (r, g, b);
                }
        }, tileSize, cacheBytes);
        return result;
    }

    private static (double X, double Y, double Z) Decoded(byte r, byte g, byte b)
    {
        double x = 2.0 * r / 255 - 1, y = 2.0 * g / 255 - 1, z = 2.0 * b / 255 - 1;
        var length = Math.Sqrt(x * x + y * y + z * z);
        return (x / length, y / length, z / length);
    }

    // ---- at the graph's own size, texel for texel ----------------------------------------------------------------

    [Fact]
    public void At_the_graphs_size_each_kind_is_read_texel_for_texel()
    {
        const int Size = 32;
        var rgba = Hashed(Size, Size);
        WritePng("hashed.png", Size, Size, rgba);
        var colour = Run("hashed.png", "colour", Size);
        var grey = Run("hashed.png", "grey", Size);
        var normal = Run("hashed.png", "normal", Size);
        for (var t = 0; t < Size * Size; t++)
        {
            byte r = rgba[t * 4], g = rgba[t * 4 + 1], b = rgba[t * 4 + 2];
            Assert.Equal((r / 255f, g / 255f, b / 255f), (colour[t * 3], colour[t * 3 + 1], colour[t * 3 + 2]));
            Assert.Equal(r / 255f, grey[t * 3]); // grey is R alone, though G and B differ
            var (x, y, z) = Decoded(r, g, b);
            Assert.Equal(x, normal[t * 3], 1e-6);
            Assert.Equal(y, normal[t * 3 + 1], 1e-6);
            Assert.Equal(z, normal[t * 3 + 2], 1e-6);
        }
    }

    [Fact]
    public void A_normal_is_unit_length_at_every_size()
    {
        WritePng("normals.png", 48, 40, Hashed(48, 40));
        foreach (var size in new[] { 32, 64, 128 })
        {
            var n = Run("normals.png", "normal", size);
            for (var t = 0; t < size * size; t++)
            {
                var length = Math.Sqrt(n[t * 3] * n[t * 3] + n[t * 3 + 1] * n[t * 3 + 1] + n[t * 3 + 2] * n[t * 3 + 2]);
                Assert.InRange(length, 0.99999, 1.00001);
            }
        }
    }

    [Fact]
    public void A_flat_baked_normal_reads_as_nearly_straight_out()
    {
        // carve's bake writes a flat normal (0, 0, 1) as (128, 128, 255): 2 * 128 / 255 - 1 = 1/255, renormalised.
        WritePng("flat.png", 32, 32, Rgba(32, 32, (_, _) => (128, 128, 255)));
        var n = Run("flat.png", "normal", 32);
        Assert.Equal(Decoded(128, 128, 255).Z, n[2], 6);
        Assert.Equal(1 / 255.0, n[0], 4);
    }

    // ---- other sizes, bilinear, by hand ----------------------------------------------------------------------------

    [Fact]
    public void A_half_size_image_is_read_bilinear_by_hand_wrapping_at_the_seam()
    {
        // 16 columns, column c holding 10c in R (and 3c in G, 200 - 5c in B), read at 32: texel x's centre falls at image
        // x (2x + 1) / 4 - 0.5, between columns floor of that and the next, wrapping.
        WritePng("half.png", 16, 16, Rgba(16, 16, (x, _) => (10 * x, 3 * x, 200 - 5 * x)));
        var c = Run("half.png", "colour", 32);
        float R(int x) => c[x * 3] * 255;
        Assert.Equal(37.5f, R(0), 3);   // -0.25: 0.75 of the way from column 15 (150) to column 0 (0), across the seam
        Assert.Equal(2.5f, R(1), 3);    // 0.25 of the way from column 0 to 1
        Assert.Equal(7.5f, R(2), 3);    // 0.75
        Assert.Equal(112.5f, R(31), 3); // 15.25: a quarter of the way from column 15 (150) back to column 0, across the seam
        Assert.Equal(200 - 5 * 0.25f, c[1 * 3 + 2] * 255, 3); // B at texel 1, 0.25 of the way from 200 to 195
        for (var y = 1; y < 32; y++) Assert.Equal(c[0], c[(y * 32) * 3]); // the rows are alike, so y changes nothing
    }

    [Fact]
    public void A_double_size_image_is_read_bilinear_by_hand()
    {
        // 64 rows, row r holding 3r in G, read at 32: texel y's centre falls at image y 2y + 0.5, halfway between rows 2y and 2y + 1.
        WritePng("double.png", 64, 64, Rgba(64, 64, (_, y) => (0, 3 * y, 0)));
        var c = Run("double.png", "colour", 32);
        float G(int y) => c[(y * 32) * 3 + 1] * 255;
        Assert.Equal(1.5f, G(0), 3);
        Assert.Equal(187.5f, G(31), 3);
        Assert.Equal(43.5f, G(7), 3);  // rows 14 and 15
    }

    [Fact]
    public void The_wrap_holds_down_the_image_too()
    {
        WritePng("rows.png", 16, 16, Rgba(16, 16, (_, y) => (10 * y, 0, 0)));
        var c = Run("rows.png", "grey", 32);
        Assert.Equal(37.5f, c[0] * 255, 3);
        Assert.Equal(112.5f, c[(31 * 32) * 3] * 255, 3);
    }

    [Fact]
    public void A_non_square_image_is_stretched_over_the_square()
    {
        // 32 x 64: across, texel for texel; down, halfway between rows 2y and 2y + 1.
        WritePng("tall.png", 32, 64, Rgba(32, 64, (x, y) => (x, y, 0)));
        var c = Run("tall.png", "colour", 32);
        Assert.Equal(5f, c[(3 * 32 + 5) * 3] * 255, 3);
        Assert.Equal(6.5f, c[(3 * 32 + 5) * 3 + 1] * 255, 3);
    }

    [Fact]
    public void The_image_is_the_same_at_every_tile_size_and_cap()
    {
        WritePng("odd.png", 48, 40, Hashed(48, 40));
        foreach (var kind in new[] { "colour", "grey", "normal" })
        {
            var whole = Run("odd.png", kind, 64);
            foreach (var tile in new[] { 1, 8, 16, 32 })
                Assert.Equal(whole, Run("odd.png", kind, 64, tile));
            Assert.Equal(whole, Run("odd.png", kind, 64, 8, cacheBytes: 0));
        }
    }

    // ---- files and versions ----------------------------------------------------------------------------------------

    [Fact]
    public void A_nested_graphs_image_resolves_against_its_own_folder_and_is_among_the_files()
    {
        var rgba = Hashed(32, 32);
        var png = WritePng(Path.Combine("lib", "bake.png"), 32, 32, rgba);
        var inner = Path.Combine(_dir, "lib", "inner.texgraph");
        File.WriteAllText(inner, """{ "version": 2, "size": 32, "seed": 0, "nodes": { "i": { "type": "image", "params": { "path": "bake.png" } }, "o": { "type": "output", "inputs": { "in": "i" }, "params": { "role": "base_colour" } } } }""");
        var graph = Graph(""" "g": { "type": "graph", "params": { "path": "lib/inner" } }, "o": { "type": "output", "inputs": { "in": "g.base_colour" }, "params": { "role": "base_colour" } } """, 32);

        Assert.Equal([inner, png], GraphValidator.Files(graph));
        Assert.Equal([inner, png], GraphValidator.Uses(graph).Single().Files);
        Assert.Empty(GraphValidator.Images(graph)); // only the graph's own image nodes
        var texels = new List<float>();
        new GraphEvaluator(graph).Evaluate("base_colour", 32, tile => texels.Add(tile.Colour(3, 0).R));
        Assert.Equal(rgba[3 * 4] / 255f, texels[0]);
    }

    [Fact]
    public void The_graphs_own_image_is_listed_with_its_size()
    {
        var png = WritePng("bake.png", 48, 40, Hashed(48, 40));
        var graph = Graph(Image("bake.png", "colour") + """, "o": { "type": "output", "inputs": { "in": "t" } } """, 32);
        Assert.Equal([new ImageUse("t", png, 48, 40)], GraphValidator.Images(graph));
        Assert.Equal([png], GraphValidator.Files(graph));
    }

    [Fact]
    public void An_image_node_is_format_version_2()
    {
        WritePng("bake.png", 32, 32, Hashed(32, 32));
        var graph = Graph(Image("bake.png", "grey") + """, "o": { "type": "output", "inputs": { "in": "t" }, "params": { "role": "roughness" } } """, 32);
        Assert.Contains("\"version\": 2", TexGraphFile.Write(graph));
        var v1 = TexGraphFile.Read("""{ "version": 1, "size": 32, "seed": 0, "nodes": { "t": { "type": "image", "params": { "path": "bake.png" } } } }""", folder: _dir);
        Assert.Null(v1.Graph);
        Assert.Contains("version 2", v1.Error!.ToString());
    }

    // ---- faults ----------------------------------------------------------------------------------------------------

    private IReadOnlyList<GraphFault> Faults(string imageNode)
    {
        var read = TexGraphFile.Read($$"""{ "version": 2, "size": 32, "seed": 0, "nodes": { {{imageNode}}, "o": { "type": "output", "inputs": { "in": "t" } } } }""", folder: _dir);
        Assert.NotNull(read.Graph);
        return GraphValidator.Validate(read.Graph!);
    }

    public static TheoryData<string, string, string> FaultCases() => new()
    {
        { "no path", """ "t": { "type": "image" } """, "node 't' parameter 'path': an image node needs path, the .png it reads, relative to this graph's file" },
        { "an absolute path", """ "t": { "type": "image", "params": { "path": "/bake.png" } } """, "node 't' parameter 'path' is \"/bake.png\", but must be a path to a .png file, relative to this graph's file" },
        { "a missing file", """ "t": { "type": "image", "params": { "path": "gone.png" } } """, "node 't' parameter 'path': no file at <dir>/gone.png; the .png files in <dir> (3) are: bad.png, deep.png, good.png" },
        { "a file that is not a PNG", """ "t": { "type": "image", "params": { "path": "graph.texgraph" } } """, "node 't' parameter 'path': <dir>/graph.texgraph is not a PNG patina reads: it is not a PNG (no PNG signature)" },
        { "a damaged PNG", """ "t": { "type": "image", "params": { "path": "bad.png" } } """, "node 't' parameter 'path': <dir>/bad.png is not a PNG patina reads: its IDAT chunk's CRC is wrong (the file is damaged)" },
        { "a 16-bit PNG", """ "t": { "type": "image", "params": { "path": "deep.png" } } """, "node 't' parameter 'path': <dir>/deep.png is not a PNG patina reads: it is a 16-bit PNG; it reads 8-bit greyscale, grey + alpha, RGB and RGBA PNGs, not interlaced" },
        { "a wrong kind", """ "t": { "type": "image", "params": { "path": "good.png", "kind": "height" } } """, "node 't' parameter 'kind' is \"height\", but must be one of colour, grey, normal" },
    };

    [Theory]
    [MemberData(nameof(FaultCases))]
    public void Every_fault_is_found_at_validation_on_its_parameter(string what, string node, string message)
    {
        File.WriteAllText(Path.Combine(_dir, "graph.texgraph"), "{}");
        var bad = PngWriter.Encode(Hashed(32, 32), 32, 32);
        bad[^20] ^= 1;
        File.WriteAllBytes(Path.Combine(_dir, "bad.png"), bad);
        WritePng("good.png", 32, 32, Hashed(32, 32));
        File.WriteAllBytes(Path.Combine(_dir, "deep.png"), TestPng.Make(4, 4, 2, new byte[4 * 4 * 6], _ => 0, depth: 16));

        var faults = Faults(node);
        var fault = Assert.Single(faults, f => f.Node == "t");
        Assert.Equal(message.StartsWith("node 't' parameter 'kind'", StringComparison.Ordinal) ? "kind" : "path", fault.Parameter);
        Assert.True(message.Replace("<dir>/", _dir + Path.DirectorySeparatorChar, StringComparison.Ordinal).Replace("<dir>", _dir, StringComparison.Ordinal) == fault.Message, $"{what}: {fault.Message}");
    }

    [Fact]
    public void A_nested_graphs_image_fault_names_its_path_and_file()
    {
        File.WriteAllText(Path.Combine(_dir, "inner.texgraph"), """{ "version": 2, "size": 32, "seed": 0, "nodes": { "i": { "type": "image", "params": { "path": "gone.png" } }, "o": { "type": "output", "inputs": { "in": "i" }, "params": { "role": "base_colour" } } } }""");
        var read = TexGraphFile.Read("""{ "version": 2, "size": 32, "seed": 0, "nodes": { "g": { "type": "graph", "params": { "path": "inner" } }, "o": { "type": "output", "inputs": { "in": "g.base_colour" } } } }""", folder: _dir);
        var fault = Assert.Single(GraphValidator.Validate(read.Graph!));
        Assert.Equal(("g/i", "path"), (fault.Node, fault.Parameter));
        Assert.StartsWith($"nodes['g/i'].params['path']: no file at {Path.Combine(_dir, "gone.png")}; ", fault.Message);
        Assert.EndsWith("(inner.texgraph)", fault.Message);
    }

    // ---- broken copies -------------------------------------------------------------------------------------------

    private delegate (float R, float G, float B) TexelFn(PngImage image, string kind, int x, int y, int size);

    /// <summary>A copy of the node's read, with its wrap, its renormalisation, its row order or its blue channel optionally broken.</summary>
    private static (float R, float G, float B) TexelCopy(PngImage image, string kind, int x, int y, int size,
        bool wrap = true, bool renormalise = true, bool flipRows = false, bool dropBlue = false)
    {
        int W = image.Width, H = image.Height;
        double px = (x + 0.5) * W / size - 0.5, py = (y + 0.5) * H / size - 0.5;
        int x0 = (int)Math.Floor(px), y0 = (int)Math.Floor(py);
        float fx = (float)(px - x0), fy = (float)(py - y0);
        int Fix(int v, int n) => wrap ? ((v % n) + n) % n : Math.Clamp(v, 0, n - 1);
        float At(int ix, int iy, int c)
        {
            var row = Fix(iy, H);
            if (flipRows) row = H - 1 - row;
            return image.Rgba[(row * W + Fix(ix, W)) * 4 + c];
        }
        float Lerp(int c) =>
            (At(x0, y0, c) * (1 - fx) + At(x0 + 1, y0, c) * fx) * (1 - fy) + (At(x0, y0 + 1, c) * (1 - fx) + At(x0 + 1, y0 + 1, c) * fx) * fy;
        float r = Lerp(0), g = Lerp(1), b = dropBlue ? 0 : Lerp(2);
        if (kind == "grey") return (r / 255, r / 255, r / 255);
        if (kind == "colour") return (r / 255, g / 255, b / 255);
        float nx = 2 * r / 255 - 1, ny = 2 * g / 255 - 1, nz = 2 * b / 255 - 1;
        if (!renormalise) return (nx, ny, nz);
        var length = MathF.Sqrt(nx * nx + ny * ny + nz * nz);
        return (nx / length, ny / length, nz / length);
    }

    private static void CheckTexel(TexelFn texel)
    {
        var columns = new PngImage(16, 16, Rgba(16, 16, (x, y) => (10 * x, 7 * y, 200 - 5 * x)));
        Assert.Equal(37.5f, texel(columns, "colour", 0, 0, 32).R * 255, 3);              // catches the missing wrap (clamped: 0)
        Assert.Equal(7 * 3f, texel(columns, "colour", 6, 3, 16).G * 255, 3);            // catches the rows flipped (row 12: 84)
        Assert.Equal(200 - 5 * 6f, texel(columns, "colour", 6, 3, 16).B * 255, 3);      // catches a channel dropped
        var tilted = new PngImage(16, 16, Rgba(16, 16, (_, _) => (200, 200, 200)));
        var n = texel(tilted, "normal", 0, 0, 16);
        Assert.InRange(MathF.Sqrt(n.R * n.R + n.G * n.G + n.B * n.B), 0.99999f, 1.00001f); // catches no renormalisation
    }

    [Fact]
    public void Every_break_is_caught_on_a_broken_copy()
    {
        CheckTexel(ImageNode.Texel);
        CheckTexel((i, k, x, y, s) => TexelCopy(i, k, x, y, s)); // the copy, unbroken, passes too

        var breaks = new (string Name, Action Check)[]
        {
            ("no wrap at the seam", () => CheckTexel((i, k, x, y, s) => TexelCopy(i, k, x, y, s, wrap: false))),
            ("no renormalisation", () => CheckTexel((i, k, x, y, s) => TexelCopy(i, k, x, y, s, renormalise: false))),
            ("rows flipped", () => CheckTexel((i, k, x, y, s) => TexelCopy(i, k, x, y, s, flipRows: true))),
            ("a channel dropped", () => CheckTexel((i, k, x, y, s) => TexelCopy(i, k, x, y, s, dropBlue: true))),
        };
        foreach (var (name, check) in breaks)
            Assert.True(Throws(check), $"the broken copy '{name}' was not caught");
    }

    private static bool Throws(Action check)
    {
        try { check(); return false; }
        catch (Xunit.Sdk.XunitException) { return true; }
    }
}
