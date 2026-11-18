using System.Numerics;
using System.Text.Json;
using StbImageSharp;
using static Carve.Server.Tests.CarveProcess;

namespace Carve.Server.Tests;

/// <summary>A model of two named meshes through the real tools: aiming each tool, the refusals, undo across meshes, and the frame.</summary>
[Trait("Tier", "regression")]
public sealed class NamedMeshTests() : TempFolderTests("carve-named")
{
    internal const string Names = "'crate', 'lid'";

    /// <summary>The define_mesh, define_mesh, move calls of define_mesh's two-mesh worked example.</summary>
    internal static (string Tool, string Arguments)[] TwoMeshExample(JsonElement tools)
    {
        var text = ToolList.Description(tools, "define_mesh");
        var at = text.IndexOf("Worked example (two meshes):", StringComparison.Ordinal);
        Assert.True(at >= 0, "define_mesh's description has no two-mesh worked example");
        return text[at..].Split('\n').Skip(1)
            .TakeWhile(line => line.Length > 2 && char.IsDigit(line[0]) && line[1] == '.')
            .Select(line => line.Split(' ', 3))
            .Select(p => (p[1], p[2].Split(" (", 2)[0]))
            .ToArray();
    }

    internal static JsonElement Ok(JsonElement result)
    {
        Assert.False(IsError(result), TextOf(result));
        return JsonDocument.Parse(TextOf(result)).RootElement;
    }

    internal static string Refused(JsonElement result)
    {
        Assert.True(IsError(result), TextOf(result));
        return TextOf(result);
    }

    internal static async Task<(CarveProcess Carve, JsonElement Tools)> StartWithCrateAndLidAsync(string? folder = null)
    {
        var carve = new CarveProcess([], workingDirectory: folder);
        await carve.InitializeAsync();
        var tools = await carve.ListToolsAsync();
        var example = TwoMeshExample(tools);
        Assert.Equal(["define_mesh", "define_mesh", "move"], example.Select(e => e.Tool));
        Ok(await carve.CallToolAsync(example[0].Tool, example[0].Arguments));
        Ok(await carve.CallToolAsync(example[1].Tool, example[1].Arguments));
        return (carve, tools);
    }

    /// <summary>The same two meshes on the harness, for the checks that need no engine.</summary>
    internal static async Task<(ToolHarness Carve, JsonElement Tools)> StartWithCrateAndLidOnHarnessAsync(string? folder = null)
    {
        var carve = new ToolHarness(workingDirectory: folder);
        try
        {
            await carve.InitializeAsync();
            var tools = await carve.ListToolsAsync();
            var example = TwoMeshExample(tools);
            Assert.Equal(["define_mesh", "define_mesh", "move"], example.Select(e => e.Tool));
            Ok(await carve.CallToolAsync(example[0].Tool, example[0].Arguments));
            Ok(await carve.CallToolAsync(example[1].Tool, example[1].Arguments));
            return (carve, tools);
        }
        catch
        {
            await carve.DisposeAsync();
            throw;
        }
    }

    internal static float[] Point(JsonElement mesh, int vertex) =>
        [.. mesh.GetProperty("vertices")[vertex].GetProperty("at").EnumerateArray().Select(x => x.GetSingle())];

    [Fact]
    public async Task Export_merges_several_meshes_and_render_labels_and_highlight_need_mesh_among_several()
    {
        var (carve, _) = await StartWithCrateAndLidAsync(Root);
        using var __ = carve;

        // T-34 lifts the refusal: with no skeleton, several meshes merge into one, as T-30's static export writes one.
        var exported = await carve.CallToolAsync("export", """{"path":"crate.glb"}""");
        Assert.False(IsError(exported), TextOf(exported));
        Assert.Contains("24 triangles", TextOf(exported));
        var parsed = GlbExportTests.Parser.Parse(File.ReadAllBytes(Path.Combine(Root, "crate.glb")), Empyrean.ModelFormat.Gltf);
        Assert.Empty(parsed.Warnings);
        Assert.Null(parsed.Skin);
        Assert.Equal(24 * 3, parsed.MeshData.Indices!.Length);
        Assert.Contains(Names, Refused(await carve.CallToolAsync("render", """{"labels":"faces"}""")));
        Assert.Contains(Names, Refused(await carve.CallToolAsync("render", """{"highlight":{"faces":[0]}}""")));
        Assert.Contains("no mesh named 'hat'; the model's meshes are " + Names,
            Refused(await carve.CallToolAsync("render", """{"mesh":"hat"}""")));
        Assert.Contains("mesh: expected a string", Refused(await carve.CallToolAsync("render", """{"mesh":3}""")));
        // Ids are the named mesh's own: the lid has 6 faces, so face 6 is not one of them though the drawn whole has 12.
        Assert.Contains("faces[6]: the 1st listed face: there is no face 6: ids run 0..5", Refused(await carve.CallToolAsync("render", """{"mesh":"lid","highlight":{"faces":[6]}}""")));

        Assert.False(IsError(await carve.CallToolAsync("render", "{}")));
        Assert.False(IsError(await carve.CallToolAsync("render", """{"mesh":"lid","labels":"both","highlight":{"faces":[5]}}""")));
    }

    // ---- The two-mesh frame -------------------------------------------------------------------

    private const string Left = """{"name":"left","vertices":[[0,0,0],[1,0,0],[1,1,0],[0,1,0],[0,0,1],[1,0,1],[1,1,1],[0,1,1]],"faces":[[0,3,2,1],[4,5,6,7],[0,1,5,4],[3,7,6,2],[0,4,7,3],[1,2,6,5]]}""";

    /// <summary>A box twice as tall, standing 1 m to the right of the cube.</summary>
    private const string Right = """{"name":"right","vertices":[[2,0,0],[3,0,0],[3,2,0],[2,2,0],[2,0,1],[3,0,1],[3,2,1],[2,2,1]],"faces":[[0,3,2,1],[4,5,6,7],[0,1,5,4],[3,7,6,2],[0,4,7,3],[1,2,6,5]]}""";

    /// <summary>The same two boxes as one mesh: the cube's ids, then the tall box's offset by 8.</summary>
    private const string Both = """{"vertices":[[0,0,0],[1,0,0],[1,1,0],[0,1,0],[0,0,1],[1,0,1],[1,1,1],[0,1,1],[2,0,0],[3,0,0],[3,2,0],[2,2,0],[2,0,1],[3,0,1],[3,2,1],[2,2,1]],"faces":[[0,3,2,1],[4,5,6,7],[0,1,5,4],[3,7,6,2],[0,4,7,3],[1,2,6,5],[8,11,10,9],[12,13,14,15],[8,9,13,12],[11,15,14,10],[8,12,15,11],[9,10,14,13]]}""";

    private const int Size = 512;

    private static readonly string Frames =
        Directory.CreateDirectory(Path.Combine(Paths.RepoRoot, "artifacts", "render-tests")).FullName;

    private static byte[] Png(JsonElement result)
    {
        Assert.False(IsError(result), result.ToString());
        return Convert.FromBase64String(result.GetProperty("content")[0].GetProperty("data").GetString()!);
    }

    /// <summary>
    /// Where a world point lands in the front view, framed on the bounds of both boxes, (0,0,0) to (3,2,1). Worked out by
    /// the scenarios' own camera (<c>CameraPixels</c>, with the front direction (1.6, 1.2, 2.2) written out), not by the product's <c>ViewFraming</c>,
    /// so a mirror or flip in the product's framing moves the image and not these points.
    /// </summary>
    private static (int X, int Y) PixelOf(Vector3 world) =>
        Carve.Scenario.Crate.CameraPixels.Holding(
            Carve.Scenario.Crate.CameraPixels.Perspective(Vector3.Zero, new Vector3(3, 2, 1), new Vector3(1.6f, 1.2f, 2.2f), Size)(world));

    private static bool IsBackground(ImageResult image, (int X, int Y) p)
    {
        var at = (p.Y * image.Width + p.X) * 4;
        return image.Data[at] is >= 47 and <= 49 && image.Data[at + 1] is >= 51 and <= 53 && image.Data[at + 2] is >= 55 and <= 57;
    }

    [Fact]
    public async Task Render_draws_both_meshes_framed_on_them_both_exactly_as_the_same_boxes_in_one_mesh()
    {
        byte[] two, one;
        using (var carve = new CarveProcess([]))
        {
            await carve.InitializeAsync();
            Ok(await carve.CallToolAsync("define_mesh", Left));
            Ok(await carve.CallToolAsync("define_mesh", Right));
            two = Png(await carve.CallToolAsync("render", """{"view":"front"}"""));
        }
        using (var carve = new CarveProcess([]))
        {
            await carve.InitializeAsync();
            Ok(await carve.CallToolAsync("define_mesh", Both));
            one = Png(await carve.CallToolAsync("render", """{"view":"front"}"""));
        }
        File.WriteAllBytes(Path.Combine(Frames, "two-meshes-front.png"), two);

        Assert.Equal(one, two);
        var image = ImageResult.FromMemory(two, ColorComponents.RedGreenBlueAlpha);
        // Both boxes' front faces (z = 1) are drawn, and the space above the cube is empty: a mirrored camera puts the
        // tall box over there, and a flipped one puts the cube there.
        Assert.False(IsBackground(image, PixelOf(new Vector3(0.5f, 0.5f, 1))), "the cube's front face is missing");
        Assert.False(IsBackground(image, PixelOf(new Vector3(2.5f, 1.5f, 1))), "the tall box's upper front face is missing");
        Assert.True(IsBackground(image, PixelOf(new Vector3(0.5f, 1.6f, 1))), "something is drawn above the cube");
        Assert.True(IsBackground(image, PixelOf(new Vector3(1.9f, 0.5f, 1))), "something is drawn between the boxes");
    }

    /// <summary>
    /// Whether the pixel reads as the highlight's orange. Since T-82 a highlighted face is a PBR-shaded tint (the person's ruling
    /// of 2026-11-13): the orange vertex colour (1, 0.55, 0.1) multiplies the lit base colour (0.72, 0.72, 0.70). In the light
    /// model the front face (n.l 0.513) is (145, 110, 47), and (145, 145, 143) untinted; at the cube's darkest n.l (0.351) the
    /// tint is (137, 103, 44). So red at least 120, red over green by at least 20 (35 and 34 tinted, 0 untinted) and blue
    /// under 90 (47 tinted, 143 untinted); before T-82, under LitMesh, it was red over 150 and over green by 40.
    /// </summary>
    private static bool IsOrange(ImageResult image, (int X, int Y) p)
    {
        var at = (p.Y * image.Width + p.X) * 4;
        return image.Data[at] >= 120 && image.Data[at] >= image.Data[at + 1] + 20 && image.Data[at + 2] < 90;
    }

    private static string PixelAt(ImageResult image, (int X, int Y) p)
    {
        var at = (p.Y * image.Width + p.X) * 4;
        return $"({image.Data[at]}, {image.Data[at + 1]}, {image.Data[at + 2]})";
    }

    /// <summary>Whether two frames differ anywhere within 12 pixels of <paramref name="p"/>.</summary>
    private static bool DiffersNear(ImageResult a, ImageResult b, (int X, int Y) p)
    {
        for (var y = p.Y - 12; y <= p.Y + 12; y++)
        {
            for (var x = p.X - 12; x <= p.X + 12; x++)
            {
                var at = (y * a.Width + x) * 4;
                if (a.Data[at] != b.Data[at] || a.Data[at + 1] != b.Data[at + 1] || a.Data[at + 2] != b.Data[at + 2])
                {
                    return true;
                }
            }
        }
        return false;
    }

    [Fact]
    public async Task Render_labels_and_highlights_the_named_mesh_alone_by_its_own_ids()
    {
        byte[] plain, highlighted, labelled, whole;
        using (var carve = new CarveProcess([]))
        {
            await carve.InitializeAsync();
            Ok(await carve.CallToolAsync("define_mesh", Left));
            Ok(await carve.CallToolAsync("define_mesh", Right));
            plain = Png(await carve.CallToolAsync("render", """{"view":"front"}"""));
            highlighted = Png(await carve.CallToolAsync("render", """{"view":"front","mesh":"right","highlight":{"faces":[1]}}"""));
            labelled = Png(await carve.CallToolAsync("render", """{"view":"front","mesh":"right","labels":"faces"}"""));
        }
        using (var carve = new CarveProcess([]))
        {
            await carve.InitializeAsync();
            Ok(await carve.CallToolAsync("define_mesh", Both));
            whole = Png(await carve.CallToolAsync("render", """{"view":"front","highlight":{"faces":[7]}}"""));
        }
        File.WriteAllBytes(Path.Combine(Frames, "two-meshes-highlight.png"), highlighted);
        File.WriteAllBytes(Path.Combine(Frames, "two-meshes-labels.png"), labelled);

        // The tall box's face 1 is face 7 of the same boxes as one mesh.
        Assert.Equal(whole, highlighted);
        var lit = ImageResult.FromMemory(highlighted, ColorComponents.RedGreenBlueAlpha);
        Assert.True(IsOrange(lit, PixelOf(new Vector3(2.5f, 1.5f, 1))), $"the tall box's front face is not orange: {PixelAt(lit, PixelOf(new Vector3(2.5f, 1.5f, 1)))}");
        Assert.False(IsOrange(lit, PixelOf(new Vector3(0.5f, 0.5f, 1))), $"the cube's front face is orange: {PixelAt(lit, PixelOf(new Vector3(0.5f, 0.5f, 1)))}");

        // The tall box's front face carries its label; the cube's, whose id 1 is the same number, carries none.
        var bare = ImageResult.FromMemory(plain, ColorComponents.RedGreenBlueAlpha);
        var marked = ImageResult.FromMemory(labelled, ColorComponents.RedGreenBlueAlpha);
        Assert.True(DiffersNear(bare, marked, PixelOf(new Vector3(2.5f, 1, 1))), "the tall box's front face has no label");
        Assert.False(DiffersNear(bare, marked, PixelOf(new Vector3(0.5f, 0.5f, 1))), "the cube's front face has a label");
    }

    /// <summary>Renders while the crate is defined again and again, so the frame is drawn on a later model than the call checked.</summary>
    private static async Task<JsonElement> RenderDuringCrateEditsAsync(CarveProcess carve, string crate, string arguments)
    {
        const int MostCalls = 2000;
        var render = carve.CallToolAsync("render", arguments);
        for (var calls = 1; !render.IsCompleted; calls++)
        {
            Assert.True(calls <= MostCalls, $"no answer in {MostCalls} calls of define_mesh");
            Ok(await carve.CallToolAsync("define_mesh", crate));
        }
        return await render;
    }

    /// <summary>
    /// RV-24 D1 through render's own path: ids shifted past the crate for the lid, on a frame drawn after the crate
    /// changed, are refused. RV-25 R1: a render that names the lid but asks for no ids shifts none, so it is never refused.
    /// </summary>
    [Fact]
    public async Task A_render_with_the_lids_ids_drawn_on_a_later_model_is_refused_and_one_with_no_ids_is_not()
    {
        const int Tries = 10;
        const string Stale = "The model changed while the frame was drawn, so the ids given for mesh 'lid' may name other elements; call render again once the edits have settled.";
        var (carve, tools) = await StartWithCrateAndLidAsync();
        using var _ = carve;
        var crate = TwoMeshExample(tools)[0].Arguments;

        for (var k = 0; k < Tries; k++)
        {
            var plain = await RenderDuringCrateEditsAsync(carve, crate, """{"view":"front","mesh":"lid"}""");
            Assert.False(IsError(plain), IsError(plain) ? TextOf(plain) : null);
        }

        var refused = 0;
        for (var k = 0; k < Tries && refused == 0; k++)
        {
            var labelled = await RenderDuringCrateEditsAsync(carve, crate, """{"view":"front","mesh":"lid","labels":"faces"}""");
            if (IsError(labelled))
            {
                Assert.Equal(Stale, TextOf(labelled));
                refused++;
            }
        }
        Assert.True(refused > 0, $"no labelled render of the lid was refused in {Tries} drawn during edits to the crate");
    }
}
