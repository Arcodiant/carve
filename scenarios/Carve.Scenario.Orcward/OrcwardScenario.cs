using System.Numerics;
using System.Text;
using Carve.Debug;
using Carve.Extensibility;
using Carve.Scenario.Crate;
using Microsoft.Extensions.DependencyInjection;
using static Carve.Scenario.Crate.CameraPixels;
using static Carve.Scenario.Character.GardenerPixels;
using static Carve.Scenario.Crate.CrateBuild;
using Carve.Scenario.Character;

namespace Carve.Scenario.Orcward;

/// <summary>Registers the scenario, as every scenario extension does.</summary>
public sealed class OrcwardExtension : ICarveExtension
{
    public void ConfigureHost(CarveHostBuilderContext context) => context.Services.AddSingleton<IScenario, OrcwardScenario>();

    public void OnStarted(IServiceProvider services)
    {
    }
}

/// <summary>
/// Orcward's done-when, in one run (M-19): the gardener with both looping clips exported in the three modes with no warnings
/// into <c>game/Content/models/</c> of the run folder (which <c>tests/Carve.OrcwardLoad.Tests</c> then loads as a game would), and
/// every <c>render_glb</c> frame of an exported file compared with <c>render</c>'s frame of the model under Orcward's rule (a pixel
/// differs when its luma differs past 4 of 255, the person's ruling of 2026-11-17; a frame fails past 0.5 % of its pixels or with a
/// connected patch of differing pixels past 50), at rest and at times of each clip, between keys
/// included. Then the same comparison on files and poses that are wrong, each of which must fail. The measured differences are
/// written to <c>differences.tsv</c> and a triptych of each pair to <c>compare/</c>.
/// </summary>
public sealed class OrcwardScenario : IScenario
{
    private const int Size = 512;
    private static readonly string[] BodyGroups = ["skin", "shirt", "trousers", "boots", "can"];
    private static readonly string[] HatGroups = ["straw", "band"];
    private readonly List<Comparison.Result> _results = [];
    private readonly Dictionary<string, string> _colour = [];

    public async Task RunAsync(ScenarioDriver driver)
    {
        var models = Path.Combine(driver.RunFolder, "game", "Content", "models");
        var work = Path.Combine(driver.RunFolder, "work");
        foreach (var owned in new[] { work, Path.Combine(driver.RunFolder, "game"), Path.Combine(driver.RunFolder, "compare") })
        {
            if (Directory.Exists(owned))
            {
                Directory.Delete(owned, recursive: true);
            }
        }

        Directory.CreateDirectory(work);
        var (body, hat) = GardenerSpec.Build();
        await BuildAsync(driver, body, hat);
        await ExportAsync(driver, models, work);

        var (min, max) = GardenerSpec.RestBounds();
        var front = Perspective(min, max, Vector3.UnitZ, Size);
        var side = Perspective(min, max, Vector3.UnitX, Size);
        var glb = Path.Combine(models, "gardener.glb");

        // ---- the good frames: the exported file against the model ----
        var idle = GardenerSpec.Idle;
        var walk = GardenerSpec.Walk;
        var views = new List<(string Name, string View, string? Clip, float Time, GardenerSpec.ClipSpec? Spec)>
        {
            ("rest-front", "+z", null, 0, null),
            ("rest-side", "+x", null, 0, null),
        };
        foreach (var t in new[] { 0.5f, 1.0f, 1.5f })
        {
            views.Add(($"idle-{t:0.000}-front", "+z", "Idle", t, idle));
        }

        // Walk's keys are at 0, 0.25, 0.5, 0.75 and 1 s: 0.125 and 0.875 lie between them.
        foreach (var t in new[] { 0f, 0.125f, 0.25f, 0.5f, 0.875f })
        {
            views.Add(($"walk-{t:0.000}-side", "+x", "Walk", t, walk));
        }

        foreach (var (name, view, clip, time, spec) in views)
        {
            var (viaRender, viaGlb) = await PairAsync(driver, name, view, clip, time, glb);
            Locals locals = spec is null ? (_ => (null, null)) : (b => spec.Sample(b, time));
            foreach (var problem in CheckFrame(viaRender, view == "+z" ? front : side, view == "+z", body, hat, locals)
                .Concat(CheckFrame(viaGlb, view == "+z" ? front : side, view == "+z", body, hat, locals)))
            {
                driver.Fail(problem);
            }

            Record(driver, name, "good", viaRender, viaGlb);
        }

        // ---- shown failing: a file that is wrong in one way, against the model ----
        // The control: the same edit path with nothing edited must still be the file the model gives.
        var untouched = Path.Combine(work, "gardener-untouched.glb");
        await File.WriteAllBytesAsync(untouched, GlbEdit.Read(await File.ReadAllBytesAsync(glb)).Write());
        var bytes = await File.ReadAllBytesAsync(glb);
        var rest = await driver.CaptureAsync("break-model-rest-front", new { view = "+z" });
        var walkHalf = await driver.CaptureAsync("break-model-walk-0.500-side", new { view = "+x", clip = "Walk", time = 0.5 });
        var walkHalfFront = await driver.CaptureAsync("break-model-walk-0.500-front", new { view = "+z", clip = "Walk", time = 0.5 });
        var controlFrame = await driver.CaptureToolAsync("render_glb", "control-untouched-walk-0.500-side", new { path = untouched, clip = "Walk", time = 0.5, view = "+x" });
        var tampered = new (string Name, Action<GlbEdit> Edit)[]
        {
            ("swapped-bone-order", e => e.SwapJoints("UpperLeg_L", "UpperLeg_R")),
            ("wrong-inverse-binds", e => e.SwapInverseBinds("UpperLeg_L", "UpperLeg_R")),
            ("reversed-winding", e => Assert(driver, e.ReverseWinding() > 0, "reversed-winding: no triangles")),
            // Luma cannot tell the trousers from the band (RV-52): this one is caught by the good frames' colour checks.
            ("swapped-colours", e => e.SwapBaseColours("Body:trousers", "Hat:band")),
        };
        Locals atRestLocals = _ => (null, null);
        Locals walkHalfLocals = b => walk.Sample(b, 0.5f);
        Record(driver, "untouched-rewrite-walk-0.500-side", "control", walkHalf, controlFrame);
        foreach (var (name, edit) in tampered)
        {
            var e = GlbEdit.Read(bytes);
            edit(e);
            var file = Path.Combine(work, $"gardener-{name}.glb");
            await File.WriteAllBytesAsync(file, e.Write());
            var atRest = await driver.CaptureToolAsync("render_glb", $"break-{name}-rest-front", new { path = file, view = "+z" });
            Record(driver, $"{name}-rest-front", "break", rest, atRest, ColourVerdict(name, atRest, front, true, body, hat, atRestLocals));
            var walking = await driver.CaptureToolAsync("render_glb", $"break-{name}-walk-0.500-side", new { path = file, clip = "Walk", time = 0.5, view = "+x" });
            Record(driver, $"{name}-walk-0.500-side", "break", walkHalf, walking, ColourVerdict(name, walking, side, false, body, hat, walkHalfLocals));
            // Seen from the side a shift in X is along the view, so a bone put wrong sideways shows only from the front.
            var walkingFront = await driver.CaptureToolAsync("render_glb", $"break-{name}-walk-0.500-front", new { path = file, clip = "Walk", time = 0.5, view = "+z" });
            Record(driver, $"{name}-walk-0.500-front", "break", walkHalfFront, walkingFront, ColourVerdict(name, walkingFront, front, true, body, hat, walkHalfLocals));
        }

        // A dropped piece: the body alone, as a piece, in place of the whole.
        var bodyOnly = Path.Combine(work, "gardener.body-only.glb");
        await CallTextAsync(driver, "export", new { path = bodyOnly, mode = "piece", meshes = new[] { "Body" } });
        var dropped = await driver.CaptureToolAsync("render_glb", "break-dropped-piece-rest-front", new { path = bodyOnly, view = "+z" });
        Record(driver, "dropped-piece-rest-front", "break", rest, dropped);

        // One small limb twisted: Hand_L turned in the pose given to render, against the glb of the clip's own keys.
        var twists = new (char Axis, float Degrees)[] { ('Y', 45), ('Y', 90), ('X', 45), ('Z', 45), ('Z', 90) };
        foreach (var (axis, degrees) in twists)
        {
            var q = GardenerSpec.PoseRotation(axis, degrees);
            var pose = new Dictionary<string, object> { ["Hand_L"] = new { rotation = new[] { q.X, q.Y, q.Z, q.W } } };
            var name = $"twist-Hand_L-{axis}{degrees:0}";
            var twisted = await driver.CaptureAsync($"break-{name}-rest-front", new { view = "+z", pose });
            var good = await driver.CaptureToolAsync("render_glb", $"break-{name}-glb-rest-front", new { path = glb, view = "+z" });
            Record(driver, $"{name}-rest-front", "twist", twisted, good);
        }

        // The colour column: "-" where no colour check is asked of the row, "ok", or "wrong: " and what read wrong; a wrong colour fails the row.
        var tsv = new StringBuilder("name\tkind\tdiffering\ttotal\tpercent\tpatch\tfails\tforeground\tcolour\n");
        foreach (var r in _results)
        {
            var colour = _colour.GetValueOrDefault(r.Name, "-");
            tsv.Append($"{r.Name}\t{r.Kind}\t{r.Differing}\t{r.Total}\t{r.Percent:0.000}\t{r.LargestPatch}\t{(Fails(r) ? "fails" : "passes")}\t{r.Foreground}\t{colour}\n");
        }

        await File.WriteAllTextAsync(Path.Combine(driver.RunFolder, "differences.tsv"), tsv.ToString());
        foreach (var r in _results.Where(r => r.Kind == "good" && r.Fails))
        {
            driver.Fail($"{r.Name}: a good frame fails Orcward's rule, {r.Differing} px ({r.Percent:0.000} %), largest patch {r.LargestPatch} px");
        }

        // Each twisted limb is a patch and must fail (the cluster rule).
        foreach (var r in _results.Where(r => r.Kind == "twist" && !r.Fails))
        {
            driver.Fail($"{r.Name}: a twisted limb passes the rule, {r.Differing} px, largest patch {r.LargestPatch} px");
        }

        // A wrong file must fail at one frame at least; a swapped bone order or inverse bind shows only where a clip moves the bone.
        foreach (var group in _results.Where(r => r.Kind == "break").GroupBy(r => System.Text.RegularExpressions.Regex.Replace(r.Name, "-(rest|walk).*$", "")))
        {
            if (!group.Any(Fails))
            {
                driver.Fail($"{group.Key}: a wrong file passes Orcward's rule at every frame, {string.Join(", ", group.Select(r => $"{r.Name} {r.Differing} px"))}");
            }
        }
    }

    private delegate (Quaternion? Rotation, Vector3? Translation) Locals(string bone);

    private static void Assert(ScenarioDriver driver, bool ok, string message)
    {
        if (!ok)
        {
            driver.Fail(message);
        }
    }

    /// <summary>A row fails by D-6's rule, or, where its colours are checked, by a colour read wrong.</summary>
    private bool Fails(Comparison.Result r) => r.Fails || _colour.GetValueOrDefault(r.Name, "-").StartsWith("wrong", StringComparison.Ordinal);

    /// <summary>
    /// The colour column of a break's row: the good frames' colour checks (<see cref="CheckFrame"/>) run on the wrong file's frame,
    /// for the break whose fault is a colour only; "-" for the others, whose rows keep D-6's verdict alone.
    /// </summary>
    private static string ColourVerdict(string name, ScenarioFrame frame, Func<Vector3, Vector2> project, bool fromFront, GardenerSpec.MeshSpec body, GardenerSpec.MeshSpec hat, Locals locals)
    {
        if (name != "swapped-colours")
        {
            return "-";
        }

        var problems = CheckFrame(frame, project, fromFront, body, hat, locals);
        return problems.Count == 0 ? "ok" : "wrong: " + string.Join(" | ", problems).Replace('\t', ' ').Replace('\n', ' ');
    }

    private void Record(ScenarioDriver driver, string name, string kind, ScenarioFrame viaRender, ScenarioFrame viaGlb, string? colour = null)
    {
        if (colour is not null)
        {
            _colour[name] = colour;
        }

        var r = Comparison.Measure(name, kind, viaRender, viaGlb);
        _results.Add(r);
        Comparison.WriteTriptych(Path.Combine(driver.RunFolder, "compare", $"{kind}-{name}.png"), viaRender, viaGlb);
    }

    private static async Task<(ScenarioFrame Render, ScenarioFrame Glb)> PairAsync(ScenarioDriver driver, string name, string view, string? clip, float time, string glb)
    {
        object args = clip is null ? new { view } : new { view, clip, time };
        object glbArgs = clip is null ? new { path = glb, view } : new { path = glb, view, clip, time };
        var a = await driver.CaptureAsync($"render-{name}", args);
        var b = await driver.CaptureToolAsync("render_glb", $"glb-{name}", glbArgs);
        return (a, b);
    }

    /// <summary>
    /// The positive pixel checks on a new frame, worked from the framing's bounds; what fails, empty when nothing does. From the
    /// front the can's face, posed to this clip time, reads as the can, and neither its mirror in X nor the same pixel flipped upside
    /// down does; from the side the near hand reads as skin, and its flipped place does not (its mirror too, where it is off the
    /// image's centre). From either, a face of the near thigh reads as the trousers and one of the hat's band as the band: luma
    /// cannot tell those two apart (RV-52, the person's ruling of 2026-11-17, "Add colour checks"). Each positive check reads
    /// against the palette lit at the face's posed normal (<see cref="ClassifyLit"/>), as the rest checks do (M-19 row 6).
    /// </summary>
    private static List<string> CheckFrame(ScenarioFrame frame, Func<Vector3, Vector2> project, bool fromFront, GardenerSpec.MeshSpec body, GardenerSpec.MeshSpec hat, Locals locals)
    {
        Func<string, (Quaternion? Rotation, Vector3? Translation)> f = b => locals(b);
        var problems = new List<string>();
        var direction = fromFront ? Vector3.UnitZ : Vector3.UnitX;
        Vector3 Expect(GardenerSpec.MeshSpec mesh, string group, Vector3 near, string bone, string what)
        {
            var face = mesh.NearestFace(group, near);
            var (at, normal) = PosedFace(mesh.Centroid(face), mesh.Normal(face), bone, f);
            var (x, y) = Holding(project(at));
            if (LitProblem(frame, x, y, group, $"{what}, posed to {at}", normal, direction) is { } problem)
            {
                problems.Add(problem);
            }

            return at;
        }

        void Not(int x, int y, string group, string what)
        {
            if (Classify(frame, x, y).Group == group)
            {
                problems.Add($"{frame.Name}: {what} at ({x}, {y}) reads as {group}, which it must not");
            }
        }

        if (fromFront)
        {
            var at = Expect(body, "can", new Vector3(-0.435f, 0.39f, 0.30f), "WateringCan", "the can face");
            var (x, y) = Holding(project(at));
            var (mx, my) = Holding(project(new Vector3(-at.X, at.Y, at.Z)));
            Not(mx, my, "can", "the can's mirror in X");
            Not(x, Size - 1 - y, "can", "the can's place flipped upside down");
            Expect(body, "trousers", new Vector3(0.09f, 0.40f, 0.3f), "UpperLeg_L", "the left thigh's front face");
            Expect(hat, "band", new Vector3(0, 1.43f, 0.3f), "Head", "the band's front face");
        }
        else
        {
            var hand = GardenerSpec.Posed(new Vector3(0.415f, 0.60f, 0), "Hand_L", f);
            var (x, y) = Holding(project(hand));
            var handFace = body.NearestFace("skin", new Vector3(0.465f, 0.60f, 0));
            var (_, handNormal) = PosedFace(body.Centroid(handFace), body.Normal(handFace), "Hand_L", f);
            if (LitProblem(frame, x, y, "skin", $"the near hand at {hand}", handNormal, direction) is { } problem)
            {
                problems.Add(problem);
            }

            Not(x, Size - 1 - y, "skin", "the hand's place flipped upside down");
            if (Math.Abs(x - Size / 2) > 20)
            {
                Not(Size - 1 - x, y, "skin", "the hand's mirror");
            }

            Expect(body, "trousers", new Vector3(0.3f, 0.40f, 0), "UpperLeg_L", "the near thigh's outer face");
            Expect(hat, "band", new Vector3(0.3f, 1.43f, 0), "Head", "the band's near face");
        }

        return problems;
    }

    /// <summary>A rest point and its face's rest normal, moved with <paramref name="bone"/>: the normal turns as the bone turns it.</summary>
    private static (Vector3 At, Vector3 Normal) PosedFace(Vector3 rest, Vector3 normal, string bone, Func<string, (Quaternion? Rotation, Vector3? Translation)> locals)
    {
        var at = GardenerSpec.Posed(rest, bone, locals);
        return (at, Vector3.Normalize(GardenerSpec.Posed(rest + (0.01f * normal), bone, locals) - at));
    }

    // ---- build and export, through the tools ----

    private static async Task BuildAsync(ScenarioDriver driver, GardenerSpec.MeshSpec body, GardenerSpec.MeshSpec hat)
    {
        foreach (var m in new[] { body, hat })
        {
            await CallJsonAsync(driver, "define_mesh", new { name = m.Name, vertices = m.Vertices, faces = m.Faces });
        }

        await CallJsonAsync(driver, "define_skeleton", new { bones = GardenerSpec.SkeletonArgument() });
        foreach (var (m, groups) in new[] { (body, BodyGroups), (hat, HatGroups) })
        {
            foreach (var group in groups)
            {
                await CallJsonAsync(driver, "define_group", new { mesh = m.Name, name = group, faces = m.FacesOf(group) });
                var colour = GardenerSpec.Palette.Single(p => p.Group == group).Colour;
                await CallJsonAsync(driver, "set_colour", new { mesh = m.Name, colour, group });
            }

            foreach (var (vertices, weights) in m.Weights)
            {
                await CallJsonAsync(driver, "set_weights", new { mesh = m.Name, vertices, weights });
            }

            var last = await CallJsonAsync(driver, "set_weights", new { mesh = m.Name, vertices = new[] { 0 }, weights = m.Weights.First(w => w.Vertices.Contains(0)).Weights });
            if (last.GetProperty("unweighted").GetInt32() != 0)
            {
                driver.Fail($"{m.Name}: vertices left unweighted: {last}");
            }
        }

        foreach (var clip in new[] { GardenerSpec.Idle, GardenerSpec.Walk })
        {
            var defined = await CallJsonAsync(driver, "define_clip", new { name = clip.Name, duration = clip.Duration, loop = clip.Loop, channels = clip.Argument() });
            if (!defined.GetProperty("loop").GetBoolean() || defined.GetProperty("warnings").GetArrayLength() != 0)
            {
                driver.Fail($"define_clip {clip.Name}: expected a looping clip with no warnings, got {defined}");
            }
        }
    }

    /// <summary>The three exports, each with no warnings, written into the content folder; their results kept for the load checks.</summary>
    private static async Task ExportAsync(ScenarioDriver driver, string models, string work)
    {
        var exports = new (string File, object Args, string[] Mentions)[]
        {
            ("gardener.glb", new { path = Path.Combine(models, "gardener.glb") }, ["22 bone(s)", "2 clip(s)", "'Idle'", "'Walk'"]),
            ("gardener.clips.glb", new { path = Path.Combine(models, "gardener.clips.glb"), mode = "clips" }, ["as clips, with no mesh: 22 bone(s)", "2 clip(s)"]),
            ("gardener.hat.glb", new { path = Path.Combine(models, "gardener.hat.glb"), mode = "piece", meshes = new[] { "Hat" } }, ["as a piece", "22 bone(s)"]),
        };
        foreach (var (file, args, mentions) in exports)
        {
            var result = await CallTextAsync(driver, "export", args);
            foreach (var mention in mentions.Append(file))
            {
                if (!result.Contains(mention))
                {
                    driver.Fail($"export {file}: expected '{mention}' in {result}");
                }
            }

            if (result.Contains("warning(s)") || result.Contains("replaced"))
            {
                driver.Fail($"export {file}: expected no warnings and a new file, got {result}");
            }

            await File.WriteAllTextAsync(Path.Combine(work, $"export-{file}.txt"), result);
        }
    }
}
