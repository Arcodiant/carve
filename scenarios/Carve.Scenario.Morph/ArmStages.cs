using System.Numerics;
using System.Text.Json;
using Carve.Debug;
using Carve.Scenario.Orcward;
using Carve.Scenario.Soft;
using static Carve.Scenario.Crate.CrateBuild;

namespace Carve.Scenario.Morph;

/// <summary>
/// T-105's stages, after the cylinder: the arm with two dials (<see cref="ArmPlan"/>), built through the tools, seen in seven frames, saved as
/// <c>samples/arm.carve</c> and <c>samples/arm-lean.carve</c>. Every pixel is worked from <c>get_mesh</c>'s positions by <see cref="ArmPlan"/>'s ray test
/// (the numbers first in Core, <c>ArmGeometry</c>). <c>CARVE_MORPH_BREAK</c> makes the scenario wrong on purpose and it must fail:
/// <c>mirror</c> draws the side views from -Z, <c>weights</c> leaves <c>targets</c> out of every <c>render</c>.
/// </summary>
internal static class ArmStages
{
    private const int Size = 512;

    public static readonly string? Break = Environment.GetEnvironmentVariable("CARVE_MORPH_BREAK") is { Length: > 0 } b ? b : null;

    private sealed class Arm
    {
        public ArmPlan.Soup Rest = null!;
        public Dictionary<int, Vector3> Bulk = null!;
        public Dictionary<int, Vector3> Lean = null!;
        public float[] Elbow = null!;
        public ArmPlan.Cam Side = null!;
        public ArmPlan.Cam Corner = null!;
    }

    private sealed record Look(string Name, Dictionary<string, double> Weights, string View, string? Mode = null, bool Curl = false);

    public static async Task RunAsync(ScenarioDriver driver)
    {
        var work = Path.Combine(driver.RunFolder, "work");
        Directory.CreateDirectory(work);
        var lean = Path.Combine(work, "arm-lean.carve");
        var armFile = Path.Combine(work, "arm.carve");

        // ---- Lean's cage, built through the tools and saved, as a cage script is re-run ----
        await CallJsonAsync(driver, "define_mesh", new { vertices = ArmPlan.Cage(ArmPlan.LeanScale), faces = ArmPlan.Faces() });
        await CallTextAsync(driver, "save", new { path = lean });

        // ---- the arm ----
        var made = await CallJsonAsync(driver, "define_mesh", new { vertices = ArmPlan.Cage(), faces = ArmPlan.Faces() });
        CheckCounts(driver, "define_mesh", made, ArmPlan.CageVertices, ArmPlan.CageFaces);
        var bulk = await CallJsonAsync(driver, "define_target", new { name = "Bulk", positions = ArmPlan.Bulk() });
        if (bulk.GetProperty("vertices").GetInt32() != 48) driver.Fail($"define_target Bulk moved {bulk.GetProperty("vertices")} vertices, expected 48");
        var leanTarget = await CallJsonAsync(driver, "define_target", new { name = "Lean", file = lean });
        if (leanTarget.GetProperty("vertices").GetInt32() != ArmPlan.CageVertices) driver.Fail($"define_target Lean moved {leanTarget.GetProperty("vertices")} vertices, expected {ArmPlan.CageVertices}");
        await CallJsonAsync(driver, "set_shading", new { shading = "smooth" });
        await CallJsonAsync(driver, "define_skeleton", new
        {
            bones = ArmPlan.Bones.Select(b => (object)new { name = b.Name, parent = b.Parent, position = b.Position, rotation = new[] { 0, 0, 0, 1 } }).ToArray(),
        });
        foreach (var (vertices, weights) in ArmPlan.RigWeights()) await CallJsonAsync(driver, "set_weights", new { vertices, weights });
        await CallJsonAsync(driver, "define_clip", new
        {
            name = "Curl", duration = 1.0, loop = false,
            channels = new Dictionary<string, object>
            {
                ["Elbow"] = new Dictionary<string, object>
                {
                    ["rotation"] = new { interpolation = "LINEAR", keys = new object[] { new { time = 0.0, value = new[] { 0, 0, 0, 1 } }, new { time = 1.0, value = ArmPlan.ElbowQuarterTurn } } },
                },
            },
        });
        var sub = await CallJsonAsync(driver, "subdivide", new { levels = 1 });
        var arm = await ReadAsync(driver);
        if (arm.Rest.V.Length != 338 || arm.Rest.F.Length != 336) driver.Fail($"subdivide: {arm.Rest.V.Length} vertices and {arm.Rest.F.Length} faces, expected 338 and 336 ({sub})");
        if (arm.Rest.F.Any(f => f.Length != 4)) driver.Fail("subdivide: every face should be a quad");

        // check: Bulk and Lean break nothing, at rest and at each frame's weights.
        await ExpectNoTargetProblemAsync(driver, new { });
        foreach (var w in new[] { W(("Bulk", 1)), W(("Bulk", 0.5), ("Lean", 0.5)), W(("Lean", 1)) }) await ExpectNoTargetProblemAsync(driver, new { targets = w });

        var frames = new Look[]
        {
            new("arm-1-rest-z", W(), "+z"),
            new("arm-2-bulk-1-z", W(("Bulk", 1)), "+z"),
            new("arm-3-bulk-half-lean-half-z", W(("Bulk", 0.5), ("Lean", 0.5)), "+z"),
            new("arm-4-lean-1-z", W(("Lean", 1)), "+z"),
            new("arm-5-bulk-1-front", W(("Bulk", 1)), "front"),
            new("arm-6-curl-1-bulk-1-z", W(("Bulk", 1)), "+z", Curl: true),
        };
        var shots = new List<ScenarioFrame>();
        foreach (var look in frames) shots.Add(await ShotAsync(driver, arm, look, ""));
        await CheckDifferenceAsync(driver, shots);

        // ---- saved, reopened, and frames 1 to 6 again, byte for byte ----
        await CallTextAsync(driver, "save", new { path = armFile });
        CompareSample(driver, lean, "arm-lean.carve");
        CompareSample(driver, armFile, "arm.carve");
        await CallJsonAsync(driver, "open", new { path = armFile, drop_targets = true });
        for (var i = 0; i < frames.Length; i++)
        {
            var again = await ShotAsync(driver, arm, frames[i], "-reopened");
            driver.AssertBytesEqual($"{frames[i].Name} after a reopen", shots[i].Pixels, again.Pixels);
        }

        // ---- the planted fault ----
        var rest = arm.Rest;
        var bad = ArmPlan.BadVertices().ToDictionary(v => v.ToString(), v => new[] { (double)rest.V[v].X, ArmPlan.BadY, rest.V[v].Z });
        await CallJsonAsync(driver, "define_target", new { name = "Bad", positions = bad });
        var checkBad = await CallJsonAsync(driver, "check", new { });
        var named = checkBad.GetProperty("problems").EnumerateArray().Where(p => p.GetProperty("check").GetString()!.StartsWith("target-")).ToArray();
        if (!named.Any(p => p.GetProperty("check").GetString() == "target-flips-face" && p.GetProperty("element").GetString() == "targets['Bad']"))
            driver.Fail($"check: expected target-flips-face naming targets['Bad'], got {checkBad.GetProperty("problems")}");
        if (named.Any(p => p.GetProperty("element").GetString() != "targets['Bad']")) driver.Fail($"check: only Bad should be named, got {checkBad.GetProperty("problems")}");
        var badShot = await ShotAsync(driver, arm, new("arm-7-bad-1-normals-z", W(("Bad", 1)), "+z", "normals"), "", bad: true);
        CheckDiffers(driver, shots[0], badShot, "the rest", "Bad at 1 in the normals look");
        await CallJsonAsync(driver, "delete_target", new { name = "Bad" });
        await ExpectNoTargetProblemAsync(driver, new { });
        await ExpectNoTargetProblemAsync(driver, new { targets = W(("Bulk", 1), ("Lean", 0.25)) });

        // The range, as T-103 settled it: 0 to 1.
        await ExpectToolErrorAsync(driver, "render", new { targets = W(("Bulk", 1.2)) }, "targets.Bulk", "outside 0 to 1");
        await ExpectToolErrorAsync(driver, "check", new { targets = W(("Lean", -0.5)) }, "targets.Lean", "outside 0 to 1");

        // ---- the arm in a game: exported with Curl, drawn by the engine at three weights against render (T-106) ----
        await ExportAndCompareAsync(driver, shots);

        // The clip goes, so that the cylinder's own skeleton can be defined after the arm.
        await CallJsonAsync(driver, "delete_clip", new { name = "Curl" });
    }

    /// <summary>
    /// The arm exported into <c>game/Content/models/arm.glb</c> (skinned, with <c>Curl</c>), then <c>render_glb</c> at Bulk 0, 0.5 and 1 from +z
    /// against <c>render</c> at the same weights, under D-6's frame rule (Orcward's <see cref="Comparison"/>). Frames 1 and 2 are the twins of Bulk 0
    /// and 1; Bulk 0.5 has none among T-105's frames, so a render is taken. A control (the file at Bulk 1 against the model at rest) must fail the rule.
    /// <c>CARVE_MORPH_BREAK=glbweights</c> leaves <c>targets</c> out of <c>render_glb</c>.
    /// </summary>
    private static async Task ExportAndCompareAsync(ScenarioDriver driver, List<ScenarioFrame> shots)
    {
        var models = Path.Combine(driver.RunFolder, "game", "Content", "models");
        Directory.CreateDirectory(models);
        var glb = Path.Combine(models, "arm.glb");
        var result = await CallTextAsync(driver, "export", new { path = glb });
        if (result.Contains("replaced") || !result.Contains("exported arm.glb") || !result.Contains("1 clip(s) 'Curl'")) driver.Fail($"export arm.glb: expected a new skinned file with Curl, got {result}");
        // The smoothed quads are slightly off their planes (subdivide). By T-72's rule and the person's ruling of 2026-11-18 only not-planar
        // warnings are accepted: the mesh names its first 10 and folds the rest, and the two add up to what select's non_planar finds.
        // Any other warning fails. (The rule itself is T-127's.)
        var selected = await CallTextAsync(driver, "select", new { kind = "faces", where = "non_planar", mesh = "mesh" });
        await File.WriteAllTextAsync(Path.Combine(driver.RunFolder, "work", "select-non-planar-mesh.txt"), selected);
        var nonPlanar = System.Text.Json.JsonDocument.Parse(selected).RootElement.GetProperty("faces").EnumerateArray().Select(f => f.GetInt32()).ToList();
        var warned = result.IndexOf("warning(s): ", StringComparison.Ordinal);
        var problem = warned < 0 ? "no warnings" : NotPlanarFold.Check(result[(warned + "warning(s): ".Length)..], new Dictionary<string, IReadOnlyList<int>> { ["mesh"] = nonPlanar });
        if (problem is not null) driver.Fail($"export arm.glb: {problem}");
        await File.WriteAllTextAsync(Path.Combine(driver.RunFolder, "work", "export-arm.glb.txt"), result);

        var rows = new List<Comparison.Result>();
        void Record(string name, string kind, ScenarioFrame viaRender, ScenarioFrame viaGlb)
        {
            var r = Comparison.Measure(name, kind, viaRender, viaGlb);
            rows.Add(r);
            Comparison.WriteTriptych(Path.Combine(driver.RunFolder, "compare", $"{kind}-{name}.png"), viaRender, viaGlb);
        }

        var halfRender = await driver.CaptureAsync("arm-9-twin-bulk-half-z", new { view = "+z", projection = "orthographic", targets = W(("Bulk", 0.5)) });
        var twins = new (string Name, double Weight, ScenarioFrame Twin)[] { ("arm-8-glb-bulk-0-z", 0, shots[0]), ("arm-9-glb-bulk-half-z", 0.5, halfRender), ("arm-10-glb-bulk-1-z", 1, shots[1]) };
        var glbFrames = new List<ScenarioFrame>();
        foreach (var (name, weight, twin) in twins)
        {
            var args = new Dictionary<string, object> { ["path"] = glb, ["view"] = "+z", ["projection"] = "orthographic" };
            if (Break != "glbweights") args["targets"] = W(("Bulk", weight));
            var frame = await driver.CaptureToolAsync("render_glb", name, args);
            glbFrames.Add(frame);
            Record(name, "good", twin, frame);
        }

        var control = await driver.CaptureToolAsync("render_glb", "control-glb-bulk-1-z", new Dictionary<string, object> { ["path"] = glb, ["view"] = "+z", ["projection"] = "orthographic", ["targets"] = W(("Bulk", 1)) });
        Record("bulk-1-glb-against-the-rest", "control", shots[0], control);
        CheckDifferenceOf(driver, glbFrames);

        var tsv = new System.Text.StringBuilder();
        tsv.AppendJoin('\t', "name", "kind", "differing", "total", "largest_patch", "fails").Append('\n');
        foreach (var r in rows) tsv.AppendJoin('\t', r.Name, r.Kind, r.Differing, r.Total, r.LargestPatch, r.Fails).Append('\n');
        await File.WriteAllTextAsync(Path.Combine(driver.RunFolder, "differences.tsv"), tsv.ToString());
        foreach (var r in rows.Where(r => r.Kind == "good" && r.Fails)) driver.Fail($"{r.Name}: render_glb differs from render in {r.Differing} of {r.Total} pixels, largest patch {r.LargestPatch} (D-6's rule: 0.5 % or a patch of {Comparison.PatchLimit})");
        if (!rows.Single(r => r.Kind == "control").Fails) driver.Fail("the control (the file at Bulk 1 against the rest) passes the frame rule, so the rule cannot tell the weights apart");
    }

    private static void CheckDifferenceOf(ScenarioDriver driver, List<ScenarioFrame> frames)
    {
        for (var i = 0; i < frames.Count; i++)
            for (var j = i + 1; j < frames.Count; j++)
                if (frames[i].Pixels.AsSpan().SequenceEqual(frames[j].Pixels)) driver.Fail($"{frames[j].Name} draws the same as {frames[i].Name}, pixel for pixel");
    }

    private static Dictionary<string, double> W(params (string, double)[] w) => w.ToDictionary(p => p.Item1, p => p.Item2);

    /// <summary>The arm as <c>get_mesh</c> gives it now (live faces only), with its targets' positions and the Elbow's weights.</summary>
    private static async Task<Arm> ReadAsync(ScenarioDriver driver)
    {
        var mesh = JsonDocument.Parse(await CallTextAsync(driver, "get_mesh", new { })).RootElement;
        var vertices = mesh.GetProperty("vertices").EnumerateArray()
            .Select(v => v.ValueKind == JsonValueKind.Null ? Vector3.Zero : new Vector3(v.GetProperty("at")[0].GetSingle(), v.GetProperty("at")[1].GetSingle(), v.GetProperty("at")[2].GetSingle())).ToArray();
        var faces = mesh.GetProperty("faces").EnumerateArray().Where(f => f.ValueKind != JsonValueKind.Null)
            .Select(f => f.GetProperty("vertices").EnumerateArray().Select(i => i.GetInt32()).ToArray()).ToArray();
        var arm = new Arm { Rest = new ArmPlan.Soup(vertices, faces) };
        async Task<Dictionary<int, Vector3>> TargetAsync(string name)
        {
            var t = JsonDocument.Parse(await CallTextAsync(driver, "get_mesh", new { target = name })).RootElement.GetProperty("target").GetProperty("positions");
            return t.EnumerateObject().ToDictionary(p => int.Parse(p.Name), p => new Vector3(p.Value[0].GetSingle(), p.Value[1].GetSingle(), p.Value[2].GetSingle()));
        }
        arm.Bulk = await TargetAsync("Bulk");
        arm.Lean = await TargetAsync("Lean");
        var weights = JsonDocument.Parse(await CallTextAsync(driver, "get_weights", new { })).RootElement.GetProperty("vertices");
        arm.Elbow = new float[vertices.Length];
        foreach (var v in weights.EnumerateArray())
            if (v.GetProperty("weights").TryGetProperty("Elbow", out var e)) arm.Elbow[v.GetProperty("vertex").GetInt32()] = e.GetSingle();
        var (min, max) = arm.Rest.Bounds();
        arm.Side = ArmPlan.Orthographic(min, max, Size);
        arm.Corner = ArmPlan.Perspective(min, max, ArmPlan.FrontDirection, Size);
        return arm;
    }

    /// <summary>One frame, then every pixel check worked from the positions <c>get_mesh</c> gave: the weights applied to the rest, the Curl's turn if any.</summary>
    private static async Task<ScenarioFrame> ShotAsync(ScenarioDriver driver, Arm arm, Look look, string suffix, bool bad = false)
    {
        var args = new Dictionary<string, object> { ["view"] = look.View == "+z" && Break == "mirror" ? "-z" : look.View };
        if (look.View == "+z") args["projection"] = "orthographic";
        if (look.Mode is not null) args["mode"] = look.Mode;
        if (look.Weights.Count > 0 && Break != "weights") args["targets"] = look.Weights;
        if (look.Curl) { args["clip"] = "Curl"; args["time"] = 1.0; }
        var frame = await driver.CaptureAsync(look.Name + suffix, args);

        var targets = look.Weights.Select(w => (w.Key switch { "Bulk" => arm.Bulk, "Lean" => arm.Lean, _ => BadTarget(arm), }, (float)w.Value));
        var positions = ArmPlan.Morph(arm.Rest.V, targets);
        if (look.Curl) positions = ArmPlan.Curl(positions, arm.Elbow, 90);
        var soup = arm.Rest.With(positions);
        var normals = look.Mode == "normals";

        if (look.View == "+z")
        {
            IReadOnlyList<ArmPlan.Probe> probes = look.Curl ? ArmPlan.CurlProbes(arm.Side, true)
                : bad ? [.. ArmPlan.Sure(soup, arm.Side, ArmPlan.SilhouetteProbes(soup, arm.Side, ArmPlan.ProbeMargin)), .. ArmPlan.BadProbes(arm.Side, true)]
                : ArmPlan.SilhouetteProbes(soup, arm.Side, ArmPlan.ProbeMargin);
            Need(driver, frame, probes, normals, 0);
            Need(driver, frame, ArmPlan.Sweep(soup, arm.Side, 8), normals, 250);
        }
        else
        {
            Need(driver, frame, ArmPlan.Sweep(soup, arm.Corner, 8), normals, 250);
        }

        return frame;
    }

    private static Dictionary<int, Vector3> BadTarget(Arm arm) =>
        ArmPlan.BadVertices().ToDictionary(v => v, v => new Vector3(arm.Rest.V[v].X, (float)ArmPlan.BadY, arm.Rest.V[v].Z));

    /// <summary>Each probe's pixel is the arm or the background as the ray test says (in the normals look background is not told by colour, so only the arm is).</summary>
    private static void Need(ScenarioDriver driver, ScenarioFrame frame, IReadOnlyList<ArmPlan.Probe> probes, bool drawnOnly, int atLeast)
    {
        if (probes.Count < atLeast) driver.Fail($"{frame.Name}: only {probes.Count} pixels are sure by the ray test, expected {atLeast}");
        foreach (var p in probes)
        {
            if (p.X < 0 || p.Y < 0 || p.X >= frame.Width || p.Y >= frame.Height) driver.Fail($"{frame.Name}: {p.What} lies outside the frame at pixel ({p.X}, {p.Y})");
            var background = IsBackground(frame.Pixel(p.X, p.Y));
            if (p.Drawn && background) driver.Fail($"{frame.Name}: {p.What}: pixel ({p.X}, {p.Y}) should show the arm, and is background");
            if (!p.Drawn && !background && !drawnOnly) driver.Fail($"{frame.Name}: {p.What}: pixel ({p.X}, {p.Y}) should be background, and is {frame.Pixel(p.X, p.Y)}");
        }
    }

    /// <summary>Each of the six frames shows something the others do not: the weights and the pose change the picture.</summary>
    private static Task CheckDifferenceAsync(ScenarioDriver driver, List<ScenarioFrame> shots)
    {
        for (var i = 0; i < shots.Count; i++)
            for (var j = i + 1; j < shots.Count; j++)
                if (shots[i].Pixels.AsSpan().SequenceEqual(shots[j].Pixels)) driver.Fail($"{shots[j].Name} draws the same as {shots[i].Name}, pixel for pixel");
        return Task.CompletedTask;
    }

    private static void CheckDiffers(ScenarioDriver driver, ScenarioFrame a, ScenarioFrame b, string whatA, string whatB)
    {
        if (a.Pixels.AsSpan().SequenceEqual(b.Pixels)) driver.Fail($"{b.Name}: {whatB} draws the same as {whatA}, pixel for pixel");
    }

    private static async Task ExpectNoTargetProblemAsync(ScenarioDriver driver, object arguments)
    {
        var check = await CallJsonAsync(driver, "check", arguments);
        var problems = check.GetProperty("problems").EnumerateArray().Where(p => p.GetProperty("check").GetString()!.StartsWith("target-")).ToArray();
        if (problems.Length > 0) driver.Fail($"check {JsonSerializer.Serialize(arguments)}: no target problem expected, got {string.Join("; ", problems)}");
    }

    /// <summary>A sample is the file the scenario saved, byte for byte (the writer is deterministic), kept beside this assembly by the project file.</summary>
    private static void CompareSample(ScenarioDriver driver, string saved, string name)
    {
        var sample = Path.Combine(Path.GetDirectoryName(typeof(ArmStages).Assembly.Location) ?? AppContext.BaseDirectory, name);
        if (!File.Exists(sample)) driver.Fail($"samples/{name} is missing: copy {saved} there");
        if (!File.ReadAllBytes(sample).AsSpan().SequenceEqual(File.ReadAllBytes(saved))) driver.Fail($"samples/{name} is not what the scenario builds: {saved} differs");
    }
}
