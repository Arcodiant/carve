using Carve.Debug;
using static Carve.Scenario.Replay.Replay;

namespace Carve.Scenario.Replay;

/// <summary>
/// Stage 4, a run is all or nothing: an error at line N leaves model, undo and journal as they were and names N; a good run is one
/// undo step; run_script, save and render inside a script are refused, naming their line.
/// </summary>
internal sealed class AtomicStage : IReplayStage
{
    private const string Head =
        "define_mesh(vertices: [[0, 0, 0], [1, 0, 0], [1, 1, 0], [0, 1, 0], [0, 0, 1], [1, 0, 1], [1, 1, 1], [0, 1, 1]], faces: [[0, 3, 2, 1], [4, 5, 6, 7], [0, 1, 5, 4], [3, 7, 6, 2], [0, 4, 7, 3], [1, 2, 6, 5]], name: \"cube\")\n";

    public async Task RunAsync(ScenarioDriver d)
    {
        var scripts = Dir(d, "scripts");
        var work = Dir(d, "work");
        string P(string n) => System.IO.Path.Combine(scripts, n + ".carvescript");

        await Ok(d, "clear", new { });
        await DefineCube(d, "cube");
        await Ok(d, "move", new { mesh = "cube", faces = new[] { 3 }, by = new[] { 0, 0.5, 0 } });
        var model = await Ok(d, "get_mesh", new { mesh = "cube" });
        var file = await Save(d, System.IO.Path.Combine(work, "atomic-0.carve"));
        var journal = await Script(d, P("atomic-0"));

        // An error at line 3: nothing kept, and the next call builds on the old model.
        await File.WriteAllTextAsync(P("bad"), "move(mesh: \"cube\", faces: [1], by: [0, 0.1, 0])\nextrude(mesh: \"cube\", faces: [2], distance: 0.1)\nmove(mesh: \"cube\", faces: [99], by: [0, 1, 0])\n");
        var error = await Refused(d, "run_script", new { path = P("bad") }, "line 3");
        Equal("a failed run: the model", model, await Ok(d, "get_mesh", new { mesh = "cube" }));
        if (!Same(file, await Save(d, System.IO.Path.Combine(work, "atomic-1.carve")))) Note("a failed run changed the saved file; " + error);
        Equal("a failed run: the journal", journal, await Script(d, P("atomic-1")));
        await Ok(d, "undo", new { });
        Equal("a failed run: undo goes back one step before it, not into it", "clear define_mesh", string.Join(" ", Tools(await Script(d, P("atomic-2")))));
        await Ok(d, "move", new { mesh = "cube", faces = new[] { 3 }, by = new[] { 0, 0.5, 0 } });
        Equal("a call after the failed run builds on the old model", model, await Ok(d, "get_mesh", new { mesh = "cube" }));

        // A good run is one undo step.
        await File.WriteAllTextAsync(P("good"), "move(mesh: \"cube\", faces: [1], by: [0, 0.1, 0])\nextrude(mesh: \"cube\", faces: [2], distance: 0.1)\n");
        await Ok(d, "run_script", new { path = P("good") });
        Equal("a good run adds its statements", "clear define_mesh move move extrude", string.Join(" ", Tools(await Script(d, P("atomic-3")))));
        if (Same(file, await Save(d, System.IO.Path.Combine(work, "atomic-4.carve")))) Note("a good run changed nothing");
        await Ok(d, "undo", new { });
        Equal("one undo takes the whole run back", model, await Ok(d, "get_mesh", new { mesh = "cube" }));
        if (!Same(file, await Save(d, System.IO.Path.Combine(work, "atomic-5.carve")))) Note("one undo did not give back the saved file");

        // What a script may not hold, each by its line; the model unchanged.
        var cases = new (string Statement, string Tool)[]
        {
            ("run_script(path: \"good.carvescript\")", "run_script"),
            ("save(path: \"x.carve\")", "save"),
            ("render(view: \"front\")", "render"),
        };
        foreach (var (statement, tool) in cases)
        {
            await File.WriteAllTextAsync(P("refused"), "move(mesh: \"cube\", faces: [1], by: [0, 0.1, 0])\n" + statement + "\n");
            await Refused(d, "run_script", new { path = P("refused") }, "line 2", tool);
            Equal($"{tool} in a script: the model", model, await Ok(d, "get_mesh", new { mesh = "cube" }));
        }

        // Not in this stage's own script: a statement that is not a call at all.
        await File.WriteAllTextAsync(P("garbled"), Head + "move(mesh: \"cube\" faces: [1])\n");
        await Refused(d, "run_script", new { path = P("garbled") }, "line 2");
    }
}
