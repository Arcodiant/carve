using System.Text.Json;
using Carve.Debug;
using static Carve.Scenario.Replay.Replay;

namespace Carve.Scenario.Replay;

/// <summary>
/// Stage 1: every model-changing tool once, on small models of its own, then save A, save_script, clear, run_script, save B, A == B
/// byte for byte, and the front render before and after equal. The coverage rule is here: tools/list must hold nothing that is in
/// neither list, and every model-changing tool must be a statement of the exported script.
/// </summary>
internal sealed class EveryToolStage : IReplayStage
{
    /// <summary>The tools this stage calls: each must be a statement of the script it exports.</summary>
    public static readonly string[] ModelChanging =
    [
        "define_mesh", "move", "extrude", "delete", "merge_faces", "bridge", "plane_cut", "loop_cut", "knife", "subdivide", "tessellate",
        "displace", "unwrap", "project_uvs", "set_uvs", "set_colour", "define_material", "set_material", "set_shading", "mark_edges",
        "define_group", "define_part", "keep_level", "define_skeleton", "set_weights", "weigh", "define_clip", "delete_clip",
        "define_target", "delete_target", "open", "clear",
    ];

    /// <summary>The tools that change nothing in the model, or that a script refuses (undo, run_script), so are no statement.</summary>
    public static readonly string[] NotModelChanging =
    [
        "undo", "run_script", "save", "save_script", "export", "bake", "check", "select", "render", "render_glb", "help",
        "get_mesh", "get_parts", "get_skeleton", "get_weights", "get_clips",
    ];

    /// <summary>The exported script and the file A, for stage 5 to break.</summary>
    public static string ScriptText = "";
    public static byte[] FileA = [];
    public static string ScriptPath = "";
    public static string WorkFolder = "";

    public async Task RunAsync(ScenarioDriver d)
    {
        var inputs = Dir(d, "inputs");
        var work = WorkFolder = Dir(d, "work");
        var scripts = Dir(d, "scripts");
        File.Copy(Sample("textures/iron-base.png"), Path.Combine(inputs, "iron-base.png"), true);
        File.Copy(Sample("graphs/floor-tiles.texgraph"), Path.Combine(inputs, "floor-tiles.texgraph"), true);
        var cubeFile = Path.Combine(inputs, "cube.carve");
        var leanFile = Path.Combine(inputs, "cube-lean.carve");

        // The files the session reads: a cube, and the same cube with its top moved. Built, then thrown away by clear.
        await DefineCube(d, "cube");
        await Save(d, cubeFile);
        await Ok(d, "move", new { mesh = "cube", faces = new[] { 3 }, by = new[] { 0, 0.5, 0 } });
        await Save(d, leanFile);

        // The session. The cube comes in by open (one mesh of a file), the rest are defined.
        await Ok(d, "clear", new { });
        await Ok(d, "open", new { path = cubeFile, mesh = "cube" });
        await Ok(d, "loop_cut", new { mesh = "cube", edge = new[] { 0, 3 }, at = new[] { 0.5 } });
        await Ok(d, "merge_faces", new { mesh = "cube", faces = new[] { 1, 7 } });
        await Ok(d, "move", new { mesh = "cube", faces = new[] { 3 }, by = new[] { 0, 0.5, 0 } });

        // A plate: a quad extruded, coloured, grouped, smooth, an edge marked, UVs projected and one face's set.
        await Ok(d, "define_mesh", Json("""{"name":"plate","vertices":[[3,0,0],[5,0,0],[5,0,2],[3,0,2]],"faces":[[0,3,2,1]]}"""));
        await Ok(d, "extrude", new { mesh = "plate", faces = new[] { 0 }, distance = 0.2 });
        await Ok(d, "set_colour", new { mesh = "plate", colour = "#C8503C", faces = new[] { 0 } });
        await Ok(d, "define_group", new { name = "top", mesh = "plate", faces = new[] { 0, 1 } });
        await Ok(d, "set_shading", new { mesh = "plate", shading = "smooth" });
        await Ok(d, "mark_edges", new { mesh = "plate", edges = new[] { new[] { 0, 1 } } });
        await Ok(d, "project_uvs", new { mesh = "plate", method = "box" });
        await Ok(d, "set_uvs", new { mesh = "plate", faces = new[] { new { face = 0, uv = new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 1, 1 }, new[] { 0, 1 } } } } });
        await Ok(d, "define_material", new { name = "iron", roughness = 0.25, base_colour_texture = Path.Combine(inputs, "iron-base.png") });
        await Ok(d, "set_material", new { mesh = "plate", material = "iron", faces = new[] { 1 } });

        // One cube each for the cuts that change a mesh's ids.
        await DefineCube(d, "cut");
        await Ok(d, "plane_cut", new { mesh = "cut", point = new[] { 0, 0.5, 0 }, normal = new[] { 0, 1, 0 }, keep = "below" });
        await DefineCube(d, "carved");
        await Ok(d, "knife", new { mesh = "carved", points = new[] { new[] { 0.35, 0, 1 }, new[] { 0.35, 0.7, 1 }, new[] { 0.65, 0.7, 1 }, new[] { 0.65, 0, 1 } } });
        await DefineCube(d, "holed");
        await Ok(d, "delete", new { mesh = "holed", faces = new[] { 3 } });
        await Ok(d, "define_mesh", Json(Squares.Replace("{\"vertices\"", "{\"name\":\"bridged\",\"vertices\"")));
        await Ok(d, "bridge", new { mesh = "bridged", loops = new[] { new[] { 0, 1, 2, 3 }, new[] { 4, 5, 6, 7 } } });
        await DefineCube(d, "fine");
        await Ok(d, "subdivide", new { mesh = "fine", levels = 2 });
        await Ok(d, "displace", new { mesh = "fine", source = new { noise = new { seed = 7, scale = 0.35, octaves = 5 } }, amount = 0.2 });
        await DefineCube(d, "tiled");
        await Ok(d, "tessellate", new { mesh = "tiled", max_edge = 0.25, faces = new[] { 1 } });
        await Ok(d, "project_uvs", new { mesh = "tiled", method = "box" });
        await Ok(d, "displace", new { mesh = "tiled", source = new { graph = Path.Combine(inputs, "floor-tiles.texgraph"), output = "height" }, amount = 0.05 });
        await DefineCube(d, "flat");
        var edges = new[] { new[] { 0, 1 }, new[] { 1, 2 }, new[] { 2, 3 }, new[] { 3, 0 }, new[] { 4, 5 }, new[] { 5, 6 }, new[] { 6, 7 }, new[] { 7, 4 }, new[] { 0, 4 }, new[] { 1, 5 }, new[] { 2, 6 }, new[] { 3, 7 } };
        await Ok(d, "mark_edges", new { mesh = "flat", edges, seam = true });
        await Ok(d, "unwrap", new { mesh = "flat" });

        // Targets: by positions, from a file and one deleted again.
        await DefineCube(d, "morph");
        await Ok(d, "define_target", new { mesh = "morph", name = "Bulge", positions = new Dictionary<string, double[]> { ["2"] = [1.1, 1.3, -0.1], ["3"] = [-0.1, 1.3, -0.1], ["6"] = [1.1, 1.3, 1.1], ["7"] = [-0.1, 1.3, 1.1] } });
        await Ok(d, "define_target", new { mesh = "morph", name = "Lean", file = leanFile, file_mesh = "cube" });
        await Ok(d, "define_target", new { mesh = "morph", name = "Tmp", positions = new Dictionary<string, double[]> { ["0"] = [0, 0, -0.1] } });
        await Ok(d, "delete_target", new { mesh = "morph", name = "Tmp" });

        // A rig of three bones, weights, a clip, and a second clip deleted again.
        await Ok(d, "define_skeleton", Json("""{"bones":[{"name":"hips","parent":null,"position":[0,1,0],"rotation":[0,0,0,1]},{"name":"spine","parent":"hips","position":[0,0.3,0],"rotation":[0,0,0,1]},{"name":"head","parent":"spine","position":[0,0.4,0],"rotation":[0,0,0,1]}]}"""));
        await Ok(d, "set_weights", new { mesh = "plate", vertices = new[] { 0, 1, 2, 3 }, weights = new { spine = 1 } });
        await Ok(d, "weigh", new { mesh = "cut" });
        const string Keys = """[{"time":0,"value":[0,0,0,1]},{"time":0.5,"value":[0,0,0.70710677,0.70710677]},{"time":1,"value":[0,0,0,1]}]""";
        await Ok(d, "define_clip", Json("{\"name\":\"wave\",\"duration\":1,\"loop\":true,\"channels\":{\"spine\":{\"rotation\":{\"interpolation\":\"LINEAR\",\"keys\":" + Keys + "}}}}"));
        await Ok(d, "define_clip", Json("{\"name\":\"tmp\",\"duration\":1,\"loop\":true,\"channels\":{\"spine\":{\"rotation\":{\"interpolation\":\"LINEAR\",\"keys\":" + Keys + "}}}}"));
        await Ok(d, "delete_clip", new { name = "tmp" });

        // The part tree: an instance of the cube, and every part's geometry kept as level 0.
        await Ok(d, "define_part", new { part = "cube_twin", instance_of = "cube", translation = new[] { 0, 0, 3 }, moves = true });
        await Ok(d, "keep_level", new { level = 0 });

        // Save A and look at the model from the front, then export, clear and replay.
        var a = FileA = await Save(d, Path.Combine(work, "a.carve"));
        var before = await d.CaptureAsync("replay-before", new { view = "front" });
        var script = ScriptText = await Script(d, ScriptPath = Path.Combine(scripts, "every-tool.carvescript"));

        // Coverage: tools/list against the two lists, and each model-changing tool a statement of the script.
        var served = (await d.ListToolsAsync()).Select(t => t.Name).ToList();
        foreach (var tool in served.Where(t => !ModelChanging.Contains(t) && !NotModelChanging.Contains(t)))
            Note($"coverage: tool '{tool}' is in neither ModelChanging nor NotModelChanging (EveryToolStage.cs): add it to one, and call it here if it changes the model");
        foreach (var tool in ModelChanging.Concat(NotModelChanging).Where(t => !served.Contains(t)))
            Note($"coverage: '{tool}' is in a list of EveryToolStage.cs but tools/list has no such tool");
        var statements = Tools(script);
        foreach (var tool in ModelChanging.Where(t => !statements.Contains(t)))
            Note($"coverage: model-changing tool '{tool}' is not a statement of the exported script");
        foreach (var tool in statements.Distinct().Where(t => !ModelChanging.Contains(t)))
            Note($"coverage: statement '{tool}' is not in ModelChanging");

        await Ok(d, "clear", new { });
        await Ok(d, "run_script", new { path = ScriptPath });
        var b = await Save(d, Path.Combine(work, "b.carve"));
        Equal("replay: B has A's length", a.Length, b.Length);
        if (!Same(a, b)) Note($"replay: b.carve differs from a.carve (first at byte {a.AsSpan().CommonPrefixLength(b)})");
        var after = await d.CaptureAsync("replay-after", new { view = "front" });
        // Two empty frames are byte-equal too: some 1 % of the frame must differ by over 40 from the sky at its row's left edge.
        var drawn = 0;
        for (var y = 0; y < before.Height; y++)
            for (var x = 0; x < before.Width; x++)
                if (Math.Abs(before.Pixel(x, y).R - before.Pixel(0, y).R) + Math.Abs(before.Pixel(x, y).G - before.Pixel(0, y).G) + Math.Abs(before.Pixel(x, y).B - before.Pixel(0, y).B) > 40) drawn++;
        if (drawn < before.Width * before.Height / 100) Note($"replay: the front render shows little or nothing drawn ({drawn} pixels differ from the sky)");
        if (!Same(before.Pixels, after.Pixels)) Note("replay: the front render after replay differs from before");
    }
}
