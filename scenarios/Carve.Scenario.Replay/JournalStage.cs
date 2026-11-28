using Carve.Debug;
using static Carve.Scenario.Replay.Replay;

namespace Carve.Scenario.Replay;

/// <summary>
/// Stage 2, the journal as the script shows it: read-only and refused calls leave no line, undo takes its line out, a whole-model
/// open and clear start the journal again and undo of either puts the old one back, and 105 moves (past the 100 undo steps kept)
/// are all in the script.
/// </summary>
internal sealed class JournalStage : IReplayStage
{
    public async Task RunAsync(ScenarioDriver d)
    {
        var scripts = Dir(d, "scripts");
        var inputs = Dir(d, "inputs");
        string Path(string name) => System.IO.Path.Combine(scripts, name + ".carvescript");
        async Task<string> Tools_(string name) => string.Join(" ", Tools(await Script(d, Path(name))));

        await Ok(d, "clear", new { });
        await DefineCube(d, "cube");
        var plain = await Tools_("j1");
        Equal("a fresh model: clear, define_mesh", "clear define_mesh", plain);

        // Read-only, not-a-step and refused calls add nothing.
        foreach (var tool in new[] { "get_mesh", "get_parts", "get_skeleton", "get_weights", "get_clips", "check" })
            await d.CallAsync(tool, new { });
        await d.CallAsync("select", new { mesh = "cube", faces = new { y_above = 0.5 } });
        await d.CallAsync("render", new { view = "front" });
        await Ok(d, "save", new { path = System.IO.Path.Combine(d.RunFolder, "work", "j.carve") });
        await Refused(d, "move", new { mesh = "cube", faces = new[] { 99 }, by = new[] { 0, 1, 0 } });
        await Refused(d, "extrude", new { mesh = "cube", faces = new[] { 99 }, distance = 1 });
        Equal("reads, save and refused calls leave no line", plain, await Tools_("j2"));

        // Undo takes its line out; undo twice goes on through the history.
        await Ok(d, "move", new { mesh = "cube", faces = new[] { 3 }, by = new[] { 0, 0.5, 0 } });
        await Ok(d, "extrude", new { mesh = "cube", faces = new[] { 1 }, distance = 0.2 });
        Equal("two edits", "clear define_mesh move extrude", await Tools_("j3"));
        await Ok(d, "undo", new { });
        Equal("undo drops the last line", "clear define_mesh move", await Tools_("j4"));
        await Ok(d, "undo", new { });
        Equal("undo again", plain, await Tools_("j5"));
        await Ok(d, "move", new { mesh = "cube", faces = new[] { 3 }, by = new[] { 0, 0.5, 0 } });

        // A whole-model open starts the journal again, and undo of it puts the old one back.
        var file = System.IO.Path.Combine(inputs, "journal.carve");
        await Save(d, file);
        await Ok(d, "open", new { path = file });
        Equal("open (whole model) starts the journal", "open", await Tools_("j6"));
        await Ok(d, "move", new { faces = new[] { 3 }, by = new[] { 0, 0.1, 0 } });
        Equal("a call after open", "open move", await Tools_("j7"));
        await Ok(d, "undo", new { });
        await Ok(d, "undo", new { });
        Equal("undo of open restores the journal before it", "clear define_mesh move", await Tools_("j8"));

        // clear starts it again; undo of clear brings the old one back.
        await Ok(d, "clear", new { });
        Equal("clear starts the journal", "clear", await Tools_("j9"));
        await Ok(d, "undo", new { });
        Equal("undo of clear restores the journal", "clear define_mesh move", await Tools_("j10"));

        // 105 moves: more than the 100 undo steps the history keeps, and every one is in the script.
        await Ok(d, "clear", new { });
        await DefineCube(d, "cube");
        for (var i = 0; i < 105; i++)
            await Ok(d, "move", new { mesh = "cube", faces = new[] { 3 }, by = new[] { 0, 0.001, 0 } });
        var many = await Script(d, Path("j11"));
        Equal("105 moves, all in the script", 105, Tools(many).Count(t => t == "move"));
        Equal("and nothing else but clear and define_mesh", 107, Tools(many).Count);
    }
}
