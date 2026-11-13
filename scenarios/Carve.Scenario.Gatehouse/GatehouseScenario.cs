using System.Numerics;
using System.Text.Json;
using Carve.Debug;
using Carve.Extensibility;
using Carve.Scenario.Cuts;
using Microsoft.Extensions.DependencyInjection;
using static Carve.Scenario.Shed.Checks;
using static Carve.Scenario.Cuts.CutsChecks;

namespace Carve.Scenario.Gatehouse;

/// <summary>Registers the scenario, as every scenario extension does.</summary>
public sealed class GatehouseExtension : ICarveExtension
{
    public void ConfigureHost(CarveHostBuilderContext context) => context.Services.AddSingleton<IScenario, GatehouseScenario>();

    public void OnStarted(IServiceProvider services)
    {
    }
}

/// <summary>
/// M-5's sample, a gatehouse wall that only exists because it was cut: seven stages through the real tools, each seen from
/// <c>[0.6, 0.35, 1]</c> and <c>[-0.6, 0.35, -1]</c>, three of them in the wireframe with their new edges highlighted, and the
/// last straight on in orthographic, labelled, and as a six-view sheet. Each stage reads the ids it needs from the mesh and
/// the results before it, never typed constants. A failed check is noted and the run goes on; the run fails at its end with all of them.
/// </summary>
public sealed class GatehouseScenario : IScenario
{
    /// <summary>The door's colour, the one T-67's knife stage uses.</summary>
    internal const string DoorBrown = "#6b4423";

    private static readonly double[] Normal = [0.8 / 1.7, 1.5 / 1.7, 0];
    private static readonly double[] Mirror = [-0.8 / 1.7, 1.5 / 1.7, 0];

    private static readonly double[][] Slab =
        [[-1.5, 0, -0.15], [1.5, 0, -0.15], [1.5, 4, -0.15], [-1.5, 4, -0.15], [-1.5, 0, 0.15], [1.5, 0, 0.15], [1.5, 4, 0.15], [-1.5, 4, 0.15]];

    private static readonly int[][] SlabFaces = [[0, 3, 2, 1], [4, 5, 6, 7], [0, 1, 5, 4], [3, 7, 6, 2], [0, 4, 7, 3], [1, 2, 6, 5]];

    private static readonly double[][] Door = [[-1.1, 0, 0.15], [-1.1, 1.6, 0.15], [-0.4, 1.6, 0.15], [-0.4, 0, 0.15]];

    private static double[][] Window(double z) => [[0.3, 0.8, z], [1.1, 0.8, z], [1.1, 1.5, z], [0.7, 1.75, z], [0.3, 1.5, z]];

    public async Task RunAsync(ScenarioDriver driver)
    {
        var work = Path.Combine(driver.RunFolder, "work");
        foreach (var owned in new[] { work, Path.Combine(driver.RunFolder, "game") })
        {
            if (Directory.Exists(owned))
            {
                Directory.Delete(owned, recursive: true);
            }
        }

        var frames = new GatehouseFrames(driver);

        // 1. The slab.
        var slab = await CallJsonAsync(driver, "define_mesh", new { vertices = Slab, faces = SlabFaces });
        Summary("define_mesh the slab", slab, 8, 6, true);
        await frames.StageAsync(1, "slab", null);

        // 2. loop_cut on a vertical edge at 0.5 and 0.55, then the front band extruded out by 0.08: the cornice.
        var solid = await Solid.ReadAsync(driver);
        var slabSolid = solid;
        var (bottom, top) = (Corner(solid, -1.5f, 0, 0.15f), Corner(solid, -1.5f, 4, 0.15f));
        var ring = await CallJsonAsync(driver, "loop_cut", new { edge = new[] { bottom, top }, at = new[] { 0.5, 0.55 } });
        Summary("loop_cut at 0.5 and 0.55", ring, 16, 14, true);
        solid = await Solid.ReadAsync(driver);
        var band = Single(solid, "the front band", f => On(solid, f, z: 0.15f) && Spans(solid, f, 2f, 2.2f));
        var cornice = await CallJsonAsync(driver, "extrude", new { faces = new[] { band }, distance = 0.08 });
        Summary("extrude the band by 0.08", cornice, 20, 18, true);
        await frames.StageAsync(2, "cornice", slabSolid, wire: true);

        // 3. merge_faces on the back's three pieces and on each end's three.
        foreach (var (what, x, z) in new[] { ("the back", (float?)null, (float?)-0.15f), ("the left end", -1.5f, null), ("the right end", 1.5f, null) })
        {
            solid = await Solid.ReadAsync(driver);
            // The cornice's end wall lies in the end's plane too, but stands out of the slab (z up to 0.23): not a piece of the end.
            var pieces = Enumerable.Range(0, solid.Faces.Length).Where(f => On(solid, f, x, z: z) && solid.Faces[f].Loop.All(v => solid.Vertices[v].Z <= 0.15f + 1e-4f)).ToArray();
            Equal($"merge_faces {what}: pieces", 3, pieces.Length);
            var merged = await CallJsonAsync(driver, "merge_faces", new { faces = pieces });
            Equal($"merge_faces {what}: the faces dissolved", 2, merged.GetProperty("deleted").GetProperty("faces").GetArrayLength());
        }

        solid = await Solid.ReadAsync(driver);
        var before3 = solid;
        await frames.StageAsync(3, "merged", null);

        // 4. plane_cut twice, keeping below with fill: the gable. The slab's top face goes whole, and shows in deleted.
        solid = await Solid.ReadAsync(driver);
        var topFace = Single(solid, "the top", f => Enumerable.Range(0, 1).All(_ => solid.Faces[f].Loop.All(v => MathF.Abs(solid.Vertices[v].Y - 4) < 1e-4f)));
        var cuts = new List<JsonElement>
        {
            await CallJsonAsync(driver, "plane_cut", new { point = new[] { 0, 3.4, 0 }, normal = Normal, keep = "below", fill = true }),
            await CallJsonAsync(driver, "plane_cut", new { point = new[] { 0, 3.4, 0 }, normal = Mirror, keep = "below", fill = true }),
        };
        Summary("plane_cut, the right slope", cuts[0], 22, 13, true);
        Summary("plane_cut, the left slope", cuts[1], 22, 13, true);
        Equal("plane_cut: the top face shows in deleted", true,
            cuts.Any(c => c.GetProperty("deleted").GetProperty("faces").EnumerateArray().Any(f => f.GetInt32() == topFace)));
        await frames.StageAsync(4, "gable", before3, wire: true);

        // 5. knife, open, the door on the front; its face is the one created; coloured brown.
        var beforeDoor = await Solid.ReadAsync(driver);
        var door = await CallJsonAsync(driver, "knife", new { points = Door });
        Summary("knife the door", door, 26, 14, true);
        var doorFace = door.GetProperty("created").GetProperty("faces").EnumerateArray().Single().GetInt32();
        await CallJsonAsync(driver, "set_colour", new { faces = new[] { doorFace }, colour = DoorBrown });
        await frames.StageAsync(5, "door", beforeDoor, doorFace: doorFace);

        // 6. knife, closed, the window on the front and then the back: each leaves a hole and an inner face.
        var beforeWindows = await Solid.ReadAsync(driver);
        var frontWindow = await CallJsonAsync(driver, "knife", new { points = Window(0.15), closed = true });
        Summary("knife the window, front", frontWindow, 31, 15, true);
        var backWindow = await CallJsonAsync(driver, "knife", new { points = Window(-0.15), closed = true });
        Summary("knife the window, back", backWindow, 36, 16, true);
        var (frontFace, backFace) = (Created(frontWindow), Created(backWindow));
        await frames.StageAsync(6, "windows", beforeWindows, wire: true, doorFace: doorFace);

        // 7. delete both window faces, then bridge the front and back hole loops, and check.
        var deleted = await CallJsonAsync(driver, "delete", new { faces = new[] { frontFace, backFace } });
        Equal("delete the window faces", 2, deleted.GetProperty("deleted").GetProperty("faces").GetArrayLength());
        solid = await Solid.ReadAsync(driver);
        var loops = new[] { 0.15f, -0.15f }.Select(z =>
        {
            var holed = Single(solid, $"the face with the window hole at z = {z}", f => solid.Faces[f].Holes.Length == 1 && On(solid, f, z: z));
            return solid.Faces[holed].Holes[0];
        }).ToArray();
        var bridge = await CallJsonAsync(driver, "bridge", new { loops });
        Summary("bridge the window loops", bridge, 36, 19, true);
        Equal("bridge: five reveal faces", 5, bridge.GetProperty("created").GetProperty("faces").GetArrayLength());
        var check = await CallJsonAsync(driver, "check", new { });
        Equal("check: ok", true, check.GetProperty("ok").GetBoolean());
        Equal("check: closed", true, check.GetProperty("stats").GetProperty("closed").GetBoolean());
        Equal("check: no problems", 0, check.GetProperty("problems").GetArrayLength());
        Equal("check: no warnings", 0, check.GetProperty("warnings").GetArrayLength());
        await frames.StageAsync(7, "bridged", beforeWindows, doorFace: doorFace, last: true);

        await SaveOpenExportAsync(driver, work);

        if (Failures.Count > 0)
        {
            driver.Fail($"{Failures.Count} check(s) failed:\n" + string.Join("\n", Failures.Select(f => "  " + f)));
        }
    }

    /// <summary>
    /// The result saved, something else opened over it, then reopened and compared to the build in ids and positions, tombstones
    /// included (get_mesh keeps both), and exported into <c>game/Content/models</c> for the load checks to read as a game would.
    /// </summary>
    private static async Task SaveOpenExportAsync(ScenarioDriver driver, string work)
    {
        var path = Path.Combine(work, "gatehouse.carve");
        var built = await CallTextAsync(driver, "get_mesh", new { });
        var saved = await CallTextAsync(driver, "save", new { path });
        Equal("save: a new gatehouse.carve", true, saved.Contains("gatehouse.carve") && !saved.Contains("replaced"));
        var bytes = await File.ReadAllBytesAsync(path);

        await CallJsonAsync(driver, "define_mesh", new { vertices = new[] { new[] { 0f, 0, 0 }, [1f, 0, 0], [0f, 1, 0] }, faces = new[] { new[] { 0, 1, 2 } } });
        await CallJsonAsync(driver, "open", new { path });
        var reopened = await CallTextAsync(driver, "get_mesh", new { });
        Equal("open: get_mesh equals the build's (ids, positions, tombstones)", true, reopened == built);
        var check = await CallJsonAsync(driver, "check", new { });
        Equal("open: the reopened model is closed", true, check.GetProperty("stats").GetProperty("closed").GetBoolean());

        var again = await CallTextAsync(driver, "save", new { path });
        Equal("save (again): replaced", true, again.Contains("(replaced)"));
        driver.AssertBytesEqual("gatehouse.carve, saved again after open", bytes, await File.ReadAllBytesAsync(path));

        var glb = Path.Combine(driver.RunFolder, "game", "Content", "models", "gatehouse.glb");
        var exported = await CallTextAsync(driver, "export", new { path = glb });
        Equal("export: names the file", true, exported.Contains("gatehouse.glb"));
        Equal("export: no warnings", true, !exported.Contains("warning(s)"));
        await File.WriteAllTextAsync(Path.Combine(driver.RunFolder, "export-result.txt"), exported);
    }

    private static int Created(JsonElement result) => result.GetProperty("created").GetProperty("faces").EnumerateArray().Single().GetInt32();

    private static int Corner(Solid s, float x, float y, float z) =>
        Enumerable.Range(0, s.Vertices.Length).Single(v => Vector3.Distance(s.Vertices[v], new Vector3(x, y, z)) < 1e-4f);

    /// <summary>A live face wholly on x, on z (given) .</summary>
    private static bool On(Solid s, int face, float? x = null, float? z = null) =>
        !s.IsDeleted(face) && s.Faces[face].Loop.All(v => (x is null || MathF.Abs(s.Vertices[v].X - x.Value) < 1e-4f) && (z is null || MathF.Abs(s.Vertices[v].Z - z.Value) < 1e-4f));

    private static bool Spans(Solid s, int face, float low, float high) =>
        s.Faces[face].Loop.All(v => s.Vertices[v].Y >= low - 1e-4f && s.Vertices[v].Y <= high + 1e-4f);

    private static int Single(Solid s, string what, Func<int, bool> match)
    {
        var found = Enumerable.Range(0, s.Faces.Length).Where(f => !s.IsDeleted(f) && match(f)).ToArray();
        Equal($"{what}: one face", 1, found.Length);
        return found.Length == 1 ? found[0] : -1;
    }
}
