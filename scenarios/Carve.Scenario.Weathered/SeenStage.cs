using System.Numerics;
using Carve.Debug;
using Carve.SampleTextures;
using Carve.Scenario.Materials;
using Carve.Scenario.Uvs;
using static Carve.Scenario.Crate.CrateBuild;
using static Carve.Scenario.Weathered.WeatheredSets;

namespace Carve.Scenario.Weathered;

/// <summary>
/// Stage 2: the weathered barrel seen at bake sizes 256, 1024 and 512 (512 last, so the stages after it start from it): front, back, top,
/// three-quarter, close on a hoop rim from two directions, close on a stave where it meets a hoop, the <c>uvs</c> look of <c>wear</c> and of
/// <c>grime</c> with a 4x crop of each, and a six-view sheet. Every check is worked from <c>get_mesh</c>'s geometry and UVs and from texels read
/// through <c>render mode=uvs</c>: the five thresholds the person ruled (2026-11-24), the clean iron and wood read as M-7's relations (lit, and
/// warm or not), and the caps M-7's end grain (<see cref="Pool.Rings"/>, which also catches the <c>mirror</c> break: a mirrored top view sees the
/// bottom cap).
/// </summary>
internal sealed class SeenStage : IWeatheredStage
{
    public static readonly int[] Sizes = [256, 1024, WeatheredRun.BakeSize];
    public static readonly Vector3 Front = new(1.6f, 1.2f, 2.2f), Back = -Front, ThreeQuarter = new(-2.2f, 0.8f, 1.6f);
    private const int Tile = 256, Gutter = 2;

    /// <summary>The closes' cameras, worked once from the mesh: on the top hoop's rim facing the front, from two sides, and on the stave below it.</summary>
    public static (Camera RimA, Camera RimB, Camera Stave) Closes(Dictionary<string, Texel[]> sets)
    {
        var toFront = Vector3.Normalize(new Vector3(Front.X, 0, Front.Z));
        Vector3 Out(Vector3 p) => Vector3.Normalize(new Vector3(p.X, 0, p.Z));
        // The rim close as the research measured it (the threshold's close): the rim texel facing the front most, seen from 0.3 above its point's
        // horizontal offset from the axis, not normalised first.
        var rim = sets[Rim].MaxBy(t => Vector3.Dot(Out(t.P), toFront)).P;
        var stave = sets[Beside].Where(t => t.Top).MaxBy(t => Vector3.Dot(Out(t.P), toFront)).P;
        var side = Vector3.Cross(Vector3.UnitY, Out(rim));
        return (new Camera(Vector3.Normalize(new Vector3(rim.X, 0, rim.Z) + new Vector3(0, 0.3f, 0)), Zoom: 4, Target: rim),
            new Camera(Vector3.Normalize(Out(rim) + side * 0.8f + new Vector3(0, -0.2f, 0)), Zoom: 4, Target: rim),
            new Camera(Vector3.Normalize(Out(stave) + new Vector3(0, 0.25f, 0)), Zoom: 4, Target: stave));
    }

    public async Task RunAsync(WeatheredRun run)
    {
        var driver = run.Driver;
        var mesh = await Scene.ReadOneAsync(driver, "mesh");
        var scene = await Scene.ReadAsync(driver, "mesh");
        var faces = Enumerable.Range(0, mesh.Faces.Length).Select(f => new FaceGeo(f, mesh.Faces[f].Select(v => mesh.Vertices[v]).ToArray(), mesh.Uvs.TryGetValue(f, out var uv) ? uv : null)).ToArray();
        var caps = Barrel.Caps.ToDictionary(f => f, _ => "end-grain");
        var (min, max) = (mesh.BoundsMin, mesh.BoundsMax);

        // The neutral break: the texel checks read a flat black PNG, the neutral of a mask, in place of the graph's output.
        var neutral = run.InRun("neutral.png");
        File.WriteAllBytes(neutral, Png.Encode([.. Enumerable.Range(0, 512 * 512 * 4).Select(i => (byte)(i % 4 == 3 ? 255 : 0))], 512, 512));

        foreach (var size in Sizes)
        {
            var at = $"at {size}";
            foreach (var (name, args) in Barrel.Materials(run, size).Where(m => m.Name != "end-grain"))
            {
                var defined = await CallJsonAsync(driver, "define_material", args);
                var others = defined.TryGetProperty("warnings", out var w) ? w.EnumerateArray().Select(e => e.GetString()!).Where(e => !e.Contains("the graph reads the mesh, so it is baked onto each mesh", StringComparison.Ordinal)).ToArray() : [];
                if (others.Length != 0) run.Note($"define_material {name} {at}: warnings {string.Join("; ", others)}");
            }

            var sets = Of(mesh, size);
            run.Log.Add($"{at}: texels {string.Join(", ", Names.Select(n => $"{n} {sets[n].Length}"))}");

            // The uvs looks: the ruled texel thresholds (3) and (4), and a 4x crop of each about the top hoop's rim facing the front.
            var values = new Dictionary<string, Dictionary<string, double[]>>();
            foreach (var (graph, output) in new[] { ("weathered-iron", "wear"), ("weathered-staves", "grime") })
            {
                object image = WeatheredRun.Break == "neutral" ? neutral : new { graph = Barrel.Graph(run, graph), output, size };
                var uvs = run.Frames[$"w{size}-uvs-{output}"] = await driver.CaptureAsync($"w{size}-uvs-{output}", new { mode = "uvs", image, size = UvFrame });
                values[output] = Names.ToDictionary(n => n, n => Values(uvs, sets[n]));
                foreach (var n in Names) run.Log.Add($"{at} {output} {n}: mean {F(values[output][n].DefaultIfEmpty().Average())} max {F(values[output][n].DefaultIfEmpty().Max())}");
                var centre = sets[Rim].Where(t => t.Top).MaxBy(t => Vector3.Dot(Vector3.Normalize(new Vector3(t.P.X, 0, t.P.Z)), Vector3.Normalize(new Vector3(Front.X, 0, Front.Z))));
                Crop(uvs, centre.Px, centre.Py, Path.Combine(driver.RunFolder, "frames", $"w{size}-uvs-{output}-crop4.png"));
            }

            double Mean(string output, string set) => values[output][set].DefaultIfEmpty().Average();
            run.AtLeast($"{at}: wear, {Rim} minus {HoopMiddle}", Mean("wear", Rim), Mean("wear", HoopMiddle), 0.2);
            run.AtLeast($"{at}: grime, {Beside} minus {StaveMiddle}", Mean("grime", Beside), Mean("grime", StaveMiddle), 0.15);
            foreach (var n in new[] { StaveMiddle, Rim, HoopMiddle })
            {
                var top = values["grime"][n].DefaultIfEmpty().Max();
                run.Log.Add($"{at}: grime max of {n} {F(top)} (at most 0.02)");
                if (top > 0.02) run.Note($"{at}: grime of {n} reaches {F(top)}, over the ruled 0.02");
            }

            // The shaded frames: the ruled (1) and (2) in front, back and the first close, the clean relations in every frame.
            var (rimA, rimB, stave) = Closes(sets);
            foreach (var (view, camera, ruled) in new (string, Camera, bool)[]
            {
                ("front", new Camera(Front), true), ("back", new Camera(Back), true), ("three-quarter", new Camera(ThreeQuarter), false),
                ("close-rim", rimA, true), ("close-rim-side", rimB, false), ("close-stave", stave, false),
            })
            {
                var name = $"w{size}-{view}";
                var frame = run.Frames[name] = await driver.CaptureAsync(name, camera.Args());
                ShadedChecks(run, $"{name}", frame, scene, mesh, camera, sets, ruled);
            }

            var topFrame = run.Frames[$"w{size}-top"] = await driver.CaptureAsync($"w{size}-top", new { view = "+y", size = Shaded, mode = "shaded" });
            var topCamera = new Camera(Vector3.UnitY).Predicted(min, max);
            run.Try(() => new Pool(driver, topFrame, new Shot(faces, min, max, topCamera.Direction, Shaded), $"top {at}", caps, new HashSet<int>()).Rings(33));

            // The six-view sheet, each tile its own camera.
            (string View, Vector3 Direction)[] views = [("front", Front), ("back", Back), ("+x", Vector3.UnitX), ("-x", -Vector3.UnitX), ("+y", Vector3.UnitY), ("-y", -Vector3.UnitY)];
            var sheet = run.Frames[$"w{size}-sheet"] = await driver.CaptureAsync($"w{size}-sheet", new { views = views.Select(v => v.View).ToArray(), columns = 3, size = Tile, mode = "shaded" });
            for (var k = 0; k < views.Length; k++)
            {
                var camera = new Camera(views[k].Direction, Tile, Left: k % 3 * (Tile + Gutter), Top: k / 3 * (Tile + Gutter));
                var what = $"w{size}-sheet tile {views[k].View}";
                if (views[k].View is "+y" or "-y")
                {
                    var c = camera.Predicted(min, max);
                    var cap = views[k].View == "+y" ? 33 : 32;
                    run.Try(() => new Pool(driver, sheet, new Shot(faces, min, max, c.Direction, Tile, 1, null, c.Left, c.Top), what, caps, new HashSet<int>()).Rings(cap));
                }
                else
                {
                    ShadedChecks(run, what, sheet, scene, mesh, camera, sets, false);
                }
            }
        }
    }

    /// <summary>
    /// One shaded frame's checks: the ruled (1) rim minus hoop-top middle at least 8 luma and (2) stave middle minus stave beside a hoop at least 7,
    /// when <paramref name="ruled"/>; and in every frame the clean iron (the hoop tops' middles) lit and not ordered like wood, and the clean wood (the
    /// staves' middles) lit and warm, each on the set's mean colour and with M-7's margins (<see cref="Pool.T"/>, <see cref="Pool.Warm"/>).
    /// </summary>
    public static Dictionary<string, (Texel Texel, (byte R, byte G, byte B, byte A) Pixel)[]> ShadedChecks(WeatheredRun run, string what, ScenarioFrame frame, Scene scene, SceneMesh mesh, Camera camera, Dictionary<string, Texel[]> sets, bool ruled)
    {
        var seen = Names.ToDictionary(n => n, n => Seen(frame, scene, mesh, camera, sets[n]));
        double Mean(string n) => seen[n].Select(s => Luma(s.Pixel)).DefaultIfEmpty(double.NaN).Average();
        run.Log.Add($"{what}: seen {string.Join(", ", Names.Select(n => $"{n} {seen[n].Length} luma {F(Mean(n))}"))}");
        if (ruled)
        {
            run.AtLeast($"{what}: shaded, {Rim} minus {HoopMiddle}", Mean(Rim), Mean(HoopMiddle), 8);
            run.AtLeast($"{what}: shaded, {StaveMiddle} minus {Beside}", Mean(StaveMiddle), Mean(Beside), 7);
        }

        foreach (var (n, wood) in new[] { (HoopMiddle, false), (StaveMiddle, true) })
        {
            if (seen[n].Length < 8) { if (ruled) run.Note($"{what}: only {seen[n].Length} points of {n} seen, at least 8 needed"); continue; }
            var p = ((int)seen[n].Average(s => s.Pixel.R), (int)seen[n].Average(s => s.Pixel.G), (int)seen[n].Average(s => s.Pixel.B));
            if (Pool.Max(p) < Pool.T) run.Note($"{what}: {n} reads {p} on the mean, near-black (below {Pool.T})");
            if (Pool.Warm(p) != wood) run.Note($"{what}: {n} reads {p} on the mean, {(wood ? "not" : "")} ordered R > G > B by {Pool.M} as {(wood ? "wood" : "rust or wood")} is");
        }

        return seen;
    }

    /// <summary>A quarter-wide window of the frame about (x, y), scaled 4x by repeating each pixel, written as a PNG for the review.</summary>
    private static void Crop(ScenarioFrame frame, int x, int y, string path)
    {
        var w = frame.Width / 4;
        var (left, top) = (Math.Clamp(x - w / 2, 0, frame.Width - w), Math.Clamp(y - w / 2, 0, frame.Height - w));
        var rgba = new byte[w * 4 * w * 4 * 4];
        for (var j = 0; j < w * 4; j++)
        for (var i = 0; i < w * 4; i++)
        {
            var p = frame.Pixel(left + i / 4, top + j / 4);
            var o = (j * w * 4 + i) * 4;
            (rgba[o], rgba[o + 1], rgba[o + 2], rgba[o + 3]) = (p.R, p.G, p.B, p.A);
        }

        File.WriteAllBytes(path, Png.Encode(rgba, w * 4, w * 4));
    }
}
