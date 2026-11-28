using System.Text;
using Carve.Debug;
using static Carve.Scenario.Replay.Replay;

namespace Carve.Scenario.Replay;

/// <summary>
/// Stage 5, the comparison shown to bite: stage 1's exported text is damaged on the scenario's side (no product flag, no hook), and
/// each damaged copy must either be refused by run_script or replay to a different file than A. The table is written to
/// <c>breaks.tsv</c> in the run folder, with a Caught column; a control that is not damaged must come out equal to A.
/// </summary>
internal sealed class BreaksStage : IReplayStage
{
    public async Task RunAsync(ScenarioDriver d)
    {
        var scripts = Dir(d, "scripts");
        var statements = Statements(EveryToolStage.ScriptText);
        string Join(IEnumerable<string> s) => string.Concat(s);
        int Find(string tool, int nth = 0) => statements.Select((s, i) => (s, i)).Where(x => x.s.StartsWith(tool + "(")).Skip(nth).First().i;

        var move = Find("move");
        var loopCut = Find("loop_cut");
        var rows = new List<(string Name, string Script, bool MustBeCaught)>();

        rows.Add(("control: the script as exported", EveryToolStage.ScriptText, false));

        var dropped = statements.ToList();
        dropped.RemoveAt(move);
        rows.Add(("a statement dropped (the cube's move)", Join(dropped), true));

        var droppedMid = statements.ToList();
        droppedMid.RemoveAt(Find("extrude"));
        rows.Add(("a statement dropped (the plate's extrude)", Join(droppedMid), true));

        // Two that do not commute: loop_cut then merge_faces on the cube (merge_faces names faces loop_cut made).
        var swapped = statements.ToList();
        (swapped[loopCut], swapped[loopCut + 1]) = (swapped[loopCut + 1], swapped[loopCut]);
        rows.Add(("two statements swapped (loop_cut, merge_faces)", Join(swapped), true));

        // The cube's move and the first statement after it: move after a later edit changes nothing the replay can see, so swap with a define.
        var swapped2 = statements.ToList();
        var extrude = Find("extrude");
        (swapped2[extrude - 1], swapped2[extrude]) = (swapped2[extrude], swapped2[extrude - 1]);
        rows.Add(("two statements swapped (define_mesh plate, extrude)", Join(swapped2), true));

        var changed = statements.ToList();
        if (!changed[move].Contains("0.5")) Note("breaks: the move statement has no 0.5 to change:\n" + changed[move]);
        changed[move] = changed[move].Replace("0.5", "0.25");
        rows.Add(("a number changed (the move's 0.5 to 0.25)", Join(changed), true));

        var nudged = statements.ToList();
        var shift = Find("project_uvs");
        nudged[shift] = nudged[shift].Replace(", ", ",  ");
        rows.Add(("control: whitespace only changed in a statement", Join(nudged), false));

        var table = new StringBuilder("Break\tResult\tCaught\n");
        var n = 0;
        foreach (var (name, text, must) in rows)
        {
            var path = System.IO.Path.Combine(scripts, $"break-{++n}.carvescript");
            await File.WriteAllTextAsync(path, text);
            await Ok(d, "clear", new { });
            var run = await d.CallAsync("run_script", new { path });
            string result;
            bool caught;
            if (run.IsError is true)
            {
                caught = true;
                result = "run_script refused: " + Text(run).Split('\n')[0];
            }
            else
            {
                var saved = await Save(d, System.IO.Path.Combine(EveryToolStage.WorkFolder, $"break-{n}.carve"));
                caught = !Same(EveryToolStage.FileA, saved);
                result = caught ? "ran, file differs from A" : "ran, file equals A";
            }

            table.Append($"{name}\t{result.Replace('\t', ' ').Replace("\r", "").Replace("\n", " ")}\t{(caught ? "yes" : "no")}\n");
            if (must && !caught) Note($"breaks: '{name}' was not caught");
            if (!must && caught) Note($"breaks: '{name}' should equal A but did not: {result}");
        }

        await File.WriteAllTextAsync(System.IO.Path.Combine(d.RunFolder, "breaks.tsv"), table.ToString());
    }
}
