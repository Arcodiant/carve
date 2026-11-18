using System.Text.Json;

namespace Carve.Server.Tests;

/// <summary>
/// One end-to-end case per tool family through the real <c>carve</c> process (T-89), so the wiring in <c>Program.cs</c>
/// stays covered now the behaviour checks run on <see cref="ToolHarness"/>: the same calls go to the process and to the
/// harness, each in a working folder of its own, and every result must be the same JSON, with the written files equal.
/// A call meant to succeed must succeed on both sides, so each family's real edit path runs; a call marked as a refusal
/// must be refused on both.
/// </summary>
[Trait("Tier", "regression")]
public sealed class ToolFamiliesThroughProcessTests() : TempFolderTests("carve-families")
{
    private const string Chain =
        """{"bones":[{"name":"Hips","parent":null,"position":[0,-0.5,0],"rotation":[0,0,0,1]},{"name":"Head","parent":"Hips","position":[0,1,0],"rotation":[0,0,0,1]}]}""";

    /// <summary>One tool call; <paramref name="Refused"/> marks a deliberate refusal, otherwise the call must succeed.</summary>
    private sealed record Call(string Tool, string Arguments, bool Refused = false)
    {
        public static implicit operator Call((string Tool, string Arguments) call) => new(call.Tool, call.Arguments);
    }

    private const string TwoSquares =
        """{"vertices":[[0,0,0],[1,0,0],[1,1,0],[0,1,0],[0,0,1],[1,0,1],[1,1,1],[0,1,1]],"faces":[[0,3,2,1],[4,5,6,7]]}""";

    private static readonly (string Family, Call[] Calls)[] Families =
    [
        ("edit", [("define_mesh", ToolHarnessTests.Cube), ("move", """{"faces":[1],"by":[0,0.5,0]}"""), ("extrude", """{"faces":[1],"distance":0.25}"""), ("get_mesh", "{}"), ("undo", "{}")]),
        ("cut", [("define_mesh", ToolHarnessTests.Cube), ("loop_cut", """{"edge":[3,0],"at":[0.25,0.5]}"""), ("merge_faces", """{"faces":[7,1]}"""), new("merge_faces", """{"faces":[0,1]}""", Refused: true)]),
        ("bridge", [("define_mesh", TwoSquares), ("bridge", """{"loops":[[0,1,2,3],[6,5,4,7]]}"""), ("check", "{}"), new("bridge", """{"loops":[[0,1,2,3],[4,5,6]]}""", Refused: true)]),
        ("delete and select", [("define_mesh", ToolHarnessTests.Cube), ("select", """{"kind":"faces","where":"y == 1"}"""), ("delete", """{"faces":[5]}"""), ("check", "{}")]),
        ("shading", [("define_mesh", ToolHarnessTests.Cube), ("set_shading", """{"shading":"smooth"}"""), ("mark_edges", """{"edges":[[1,0],[3,0]]}""")]),
        ("uvs", [("define_mesh", ToolHarnessTests.Cube), ("set_uvs", """{"faces":[{"face":0,"uv":[[0,0],[0,1],[1,1],[1,0]]}]}"""), ("get_mesh", "{}")]),
        ("skeleton, weights and clips", [("define_mesh", ToolHarnessTests.Cube), ("define_skeleton", Chain), ("set_weights", """{"vertices":[0,1],"weights":{"Head":1}}"""), ("get_weights", "{}"), ("define_clip", CarveFileVersion2Tests.Wave), ("get_clips", "{}")]),
        ("save, open and export", [("define_mesh", ToolHarnessTests.Cube), ("define_skeleton", Chain), ("set_weights", """{"weights":{"Hips":1}}"""), ("save", """{"path":"m.carve"}"""), ("open", """{"path":"m.carve"}"""), ("export", """{"path":"out/m.glb"}"""), ("export", """{"path":"out/m.glb"}""")]),
    ];

    [Fact]
    public async Task Every_tool_family_answers_through_the_process_as_it_does_on_the_harness()
    {
        foreach (var (index, (family, calls)) in Families.Index())
        {
            var processRoot = Path.Combine(Root, $"process{index}"); var harnessRoot = Path.Combine(Root, $"harness{index}");
            Directory.CreateDirectory(processRoot); Directory.CreateDirectory(harnessRoot);
            using var process = new CarveProcess([], workingDirectory: processRoot);
            using var harness = new ToolHarness(workingDirectory: harnessRoot);
            await process.InitializeAsync();
            await harness.InitializeAsync();
            foreach (var (tool, arguments, refused) in calls)
            {
                var resultOfProcess = await process.CallToolAsync(tool, arguments);
                var resultOfHarness = await harness.CallToolAsync(tool, arguments);
                var fromProcess = resultOfProcess.GetRawText().Replace($"process{index}", "run");
                var fromHarness = resultOfHarness.GetRawText().Replace($"harness{index}", "run");
                Assert.True(fromProcess == fromHarness, $"{family}: {tool} {arguments}\nprocess: {fromProcess}\nharness: {fromHarness}");
            }
            // The same set of files on both sides, then the same bytes in each.
            string[] Listing(string root) => [.. Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Select(f => Path.GetRelativePath(root, f)).Order(StringComparer.Ordinal)];
            Assert.Equal(Listing(harnessRoot), Listing(processRoot));
            foreach (var file in Listing(harnessRoot))
                Assert.Equal(File.ReadAllBytes(Path.Combine(harnessRoot, file)), File.ReadAllBytes(Path.Combine(processRoot, file)));
        }
    }
}
