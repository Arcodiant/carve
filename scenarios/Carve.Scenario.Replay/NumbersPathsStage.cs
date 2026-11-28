using Carve.Debug;
using static Carve.Scenario.Replay.Replay;

namespace Carve.Scenario.Replay;

/// <summary>
/// Stage 3: numbers and names as written (0.1, 0.30000000000000004, -0, 5e-324, 1e38, unicode) come back from the script as they
/// went in and replay to the same file; and a script in a subfolder holds the paths it read relative to itself and runs from there.
/// </summary>
internal sealed class NumbersPathsStage : IReplayStage
{
    public async Task RunAsync(ScenarioDriver d)
    {
        var inputs = Dir(d, "inputs");
        var work = Dir(d, "work");
        var sub = Dir(d, System.IO.Path.Combine("scripts", "sub"));

        // The awkward numbers and names, in a mesh and in a part.
        await Ok(d, "clear", new { });
        await Ok(d, "define_mesh", Json("""{"name":"é名前","vertices":[[0.1,0.30000000000000004,-0],[1,0,0],[1,1,5e-324],[0,1,0.30000000000000004],[0,0,1],[1,0,1],[1,1,1],[0,1,1]],"faces":[[0,3,2,1],[4,5,6,7],[0,1,5,4],[3,7,6,2],[0,4,7,3],[1,2,6,5]]}"""));
        await Ok(d, "define_part", Json("""{"part":"ünï","translation":[0.1,-0,5e-324]}"""));
        await Ok(d, "define_skeleton", Json("""{"bones":[{"name":"b","parent":null,"position":[0,0,0],"rotation":[0,0,0,1]}]}"""));
        await Ok(d, "define_clip", Json("""{"name":"long","duration":1e38,"loop":false,"channels":{"b":{"rotation":{"interpolation":"LINEAR","keys":[{"time":0,"value":[0,0,0,1]},{"time":1e38,"value":[0,0,0,1]}]}}}}"""));
        var a = await Save(d, System.IO.Path.Combine(work, "numbers-a.carve"));
        var text = await Script(d, System.IO.Path.Combine(d.RunFolder, "scripts", "numbers.carvescript"));
        foreach (var token in new[] { "0.1", "0.30000000000000004", "-0", "5e-324", "1e38", "é名前", "ünï" })
            if (!text.Contains(token, StringComparison.OrdinalIgnoreCase)) Note($"numbers: the script does not hold '{token}' as written");
        await Ok(d, "clear", new { });
        await Ok(d, "run_script", new { path = System.IO.Path.Combine(d.RunFolder, "scripts", "numbers.carvescript") });
        var b = await Save(d, System.IO.Path.Combine(work, "numbers-b.carve"));
        if (!Same(a, b)) Note($"numbers: the replayed file differs from the built one (first at byte {a.AsSpan().CommonPrefixLength(b)})");

        // Paths: a session that reads a file and a texture, saved to scripts/sub/, holds them as ../../inputs/...
        File.Copy(Sample("textures/iron-base.png"), System.IO.Path.Combine(inputs, "iron-base.png"), true);
        var cube = System.IO.Path.Combine(inputs, "cube.carve");
        await Ok(d, "clear", new { });
        await Ok(d, "open", new { path = cube, mesh = "cube" });
        await Ok(d, "define_material", new { name = "iron", base_colour_texture = System.IO.Path.Combine(inputs, "iron-base.png") });
        await Ok(d, "set_material", new { mesh = "cube", material = "iron", faces = new[] { 2 } });
        var pa = await Save(d, System.IO.Path.Combine(work, "paths-a.carve"));
        var scriptPath = System.IO.Path.Combine(sub, "paths.carvescript");
        var script = await Script(d, scriptPath);
        if (!script.Contains("\"../../inputs/cube.carve\"")) Note("paths: open's path is not written as ../../inputs/cube.carve:\n" + script);
        if (!script.Contains("\"../../inputs/iron-base.png\"")) Note("paths: the texture is not written as ../../inputs/iron-base.png:\n" + script);
        if (script.Contains(d.RunFolder.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase) || script.Contains(d.RunFolder.Replace("\\", "\\\\"), StringComparison.OrdinalIgnoreCase))
            Note("paths: the script holds an absolute path");
        await Ok(d, "clear", new { });
        await Ok(d, "run_script", new { path = scriptPath });
        var pb = await Save(d, System.IO.Path.Combine(work, "paths-b.carve"));
        if (!Same(pa, pb)) Note("paths: the file after running the subfolder script differs from the one before");
    }
}
