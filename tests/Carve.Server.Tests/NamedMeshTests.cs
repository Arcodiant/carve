using System.Numerics;
using System.Text.Json;
using StbImageSharp;
using static Carve.Server.Tests.CarveProcess;

namespace Carve.Server.Tests;

/// <summary>A model of two named meshes through the real tools: aiming each tool, the refusals, undo across meshes, and the frame.</summary>
[Trait("Tier", "regression")]
public sealed class NamedMeshTests() : TempFolderTests("carve-named")
{
    private const string Names = "'crate', 'lid'";

    /// <summary>The define_mesh, define_mesh, move calls of define_mesh's two-mesh worked example.</summary>
    private static (string Tool, string Arguments)[] TwoMeshExample(JsonElement tools)
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

    private static JsonElement Ok(JsonElement result)
    {
        Assert.False(IsError(result), TextOf(result));
        return JsonDocument.Parse(TextOf(result)).RootElement;
    }

    private static string Refused(JsonElement result)
    {
        Assert.True(IsError(result), TextOf(result));
        return TextOf(result);
    }

    private static async Task<(CarveProcess Carve, JsonElement Tools)> StartWithCrateAndLidAsync(string? folder = null)
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

    private static float[] Point(JsonElement mesh, int vertex) =>
        [.. mesh.GetProperty("vertices")[vertex].GetProperty("at").EnumerateArray().Select(x => x.GetSingle())];

    [Fact]
    public async Task The_worked_example_moves_the_lid_alone_and_leaving_mesh_out_is_refused_with_the_names()
    {
        var (carve, tools) = await StartWithCrateAndLidAsync();
        using var _ = carve;
        var move = TwoMeshExample(tools)[2].Arguments;
        var unnamed = move.Replace("\"mesh\":\"lid\",", "", StringComparison.Ordinal);

        Assert.Contains(Names, Refused(await carve.CallToolAsync("move", unnamed)));
        var moved = Ok(await carve.CallToolAsync("move", move));

        Assert.True(moved.GetProperty("closed").GetBoolean());
        Assert.Equal(0, moved.GetProperty("warnings").GetArrayLength());
        Assert.Equal(new float[] { -0.1f, 1.1f, -0.1f }, Point(Ok(await carve.CallToolAsync("get_mesh", """{"mesh":"lid"}""")), 0));
        Assert.Equal(new float[] { 0f, 0f, 0f }, Point(Ok(await carve.CallToolAsync("get_mesh", """{"mesh":"crate"}""")), 0));
    }

    [Theory]
    [InlineData("move", """{"faces":[0],"by":[0,1,0]}""")]
    [InlineData("extrude", """{"faces":[0],"distance":0.5}""")]
    [InlineData("get_mesh", "{}")]
    [InlineData("check", "{}")]
    [InlineData("select", """{"kind":"faces","where":"normal.y > 0.9"}""")]
    [InlineData("save", """{"path":"crate.carve"}""")]
    [InlineData("open", """{"path":"crate.carve"}""")]
    public async Task Every_tool_aimed_at_one_mesh_refuses_to_guess_among_several_and_names_them(string tool, string arguments)
    {
        var (carve, _) = await StartWithCrateAndLidAsync(Root);
        using var __ = carve;
        Assert.False(IsError(await carve.CallToolAsync("save", """{"path":"crate.carve","mesh":"crate"}""")));

        var text = Refused(await carve.CallToolAsync(tool, arguments));

        Assert.Contains(Names, text);
        Assert.Contains("mesh", text);
        var named = arguments.Insert(1, arguments.Length > 2 ? "\"mesh\":\"crate\"," : "\"mesh\":\"crate\"");
        Assert.False(IsError(await carve.CallToolAsync(tool, named)), $"{tool} {named}");
    }

    [Fact]
    public async Task An_unknown_name_and_an_empty_name_are_refused()
    {
        var (carve, _) = await StartWithCrateAndLidAsync();
        using var __ = carve;

        Assert.Contains("no mesh named 'hat'; the model's meshes are " + Names,
            Refused(await carve.CallToolAsync("get_mesh", """{"mesh":"hat"}""")));
        Assert.Contains("name: a mesh name must not be empty",
            Refused(await carve.CallToolAsync("define_mesh", """{"name":"","vertices":[[0,0,0],[1,0,0],[0,1,0]],"faces":[[0,1,2]]}""")));
        Assert.Contains("mesh: expected a string",
            Refused(await carve.CallToolAsync("get_mesh", """{"mesh":3}""")));
    }

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

    [Fact]
    public async Task Undo_steps_back_across_meshes_one_step_per_call()
    {
        var (carve, tools) = await StartWithCrateAndLidAsync();
        using var _ = carve;
        Ok(await carve.CallToolAsync("move", TwoMeshExample(tools)[2].Arguments));
        Ok(await carve.CallToolAsync("extrude", """{"mesh":"crate","faces":[3],"distance":0.5}"""));

        var first = Ok(await carve.CallToolAsync("undo", "{}"));
        Assert.Equal("extrude", first.GetProperty("undone").GetString());
        var meshes = first.GetProperty("meshes");
        Assert.Equal(["crate", "lid"], meshes.EnumerateArray().Select(m => m.GetProperty("name").GetString()));
        Assert.Equal(6, meshes[0].GetProperty("faces").GetInt32());
        Assert.Equal(1.2f, meshes[1].GetProperty("bounds").GetProperty("max")[1].GetSingle(), 5);

        Assert.Equal("move", Ok(await carve.CallToolAsync("undo", "{}")).GetProperty("undone").GetString());
        Assert.Equal(new float[] { -0.1f, 1f, -0.1f }, Point(Ok(await carve.CallToolAsync("get_mesh", """{"mesh":"lid"}""")), 0));

        var last = Ok(await carve.CallToolAsync("undo", "{}"));
        Assert.Equal("define_mesh", last.GetProperty("undone").GetString());
        Assert.False(last.TryGetProperty("meshes", out var __)); // one mesh left: define_mesh's own report
        Assert.Equal(8, last.GetProperty("vertices").GetInt32());
        Assert.Contains("no mesh named 'lid'", Refused(await carve.CallToolAsync("get_mesh", """{"mesh":"lid"}""")));
        Assert.True(IsError(await carve.CallToolAsync("undo", "{}")));
    }

    [Fact]
    public async Task A_defined_name_is_replaced_in_place_and_open_adds_a_mesh_by_name()
    {
        var (carve, _) = await StartWithCrateAndLidAsync(Root);
        using var __ = carve;
        Assert.False(IsError(await carve.CallToolAsync("save", """{"path":"lid.carve","mesh":"lid"}""")));

        var redefined = Ok(await carve.CallToolAsync("define_mesh", """{"name":"crate","vertices":[[0,0,0],[1,0,0],[0,1,0]],"faces":[[0,1,2]]}"""));
        Assert.Equal(3, redefined.GetProperty("vertices").GetInt32());
        Ok(await carve.CallToolAsync("open", """{"path":"lid.carve","mesh":"spare"}"""));

        var undo = Ok(await carve.CallToolAsync("undo", "{}"));
        Assert.Equal(["crate", "lid"], undo.GetProperty("meshes").EnumerateArray().Select(m => m.GetProperty("name").GetString()));
        Assert.Equal(3, undo.GetProperty("meshes")[0].GetProperty("vertices").GetInt32());
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

    /// <summary>Where a world point lands in the front view, framed on the bounds of both boxes, (0,0,0) to (3,2,1).</summary>
    private static (int X, int Y) PixelOf(Vector3 world)
    {
        var space = DrawnSpace.Of(Vector3.Zero, new Vector3(3, 2, 1));
        var frame = ViewFraming.Frame(space.Apply(new Vector3(3, 2, 1)), RenderView.Front);
        var clip = Vector4.Transform(new Vector4(space.Apply(world), 1), frame.View * frame.Projection);
        return ((int)((clip.X / clip.W + 1) / 2 * Size), (int)((1 - clip.Y / clip.W) / 2 * Size));
    }

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

    private static bool IsOrange(ImageResult image, (int X, int Y) p)
    {
        var at = (p.Y * image.Width + p.X) * 4;
        return image.Data[at] > 150 && image.Data[at] > image.Data[at + 1] + 40 && image.Data[at + 2] < 90;
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
        Assert.True(IsOrange(lit, PixelOf(new Vector3(2.5f, 1.5f, 1))), "the tall box's front face is not orange");
        Assert.False(IsOrange(lit, PixelOf(new Vector3(0.5f, 0.5f, 1))), "the cube's front face is orange");

        // The tall box's front face carries its label; the cube's, whose id 1 is the same number, carries none.
        var bare = ImageResult.FromMemory(plain, ColorComponents.RedGreenBlueAlpha);
        var marked = ImageResult.FromMemory(labelled, ColorComponents.RedGreenBlueAlpha);
        Assert.True(DiffersNear(bare, marked, PixelOf(new Vector3(2.5f, 1, 1))), "the tall box's front face has no label");
        Assert.False(DiffersNear(bare, marked, PixelOf(new Vector3(0.5f, 0.5f, 1))), "the cube's front face has a label");
    }
}
