using System.Globalization;
using System.Numerics;
using System.Text.Json;
using Carve.Debug;
using Carve.Scenario.Uvs;
using static Carve.Scenario.Bakes.BakesChecks;
using static Carve.Scenario.Crate.CrateBuild;

namespace Carve.Scenario.Bakes;

/// <summary>
/// Stage 1: the wrench's masks and base colour. <c>open</c> the wrench (<c>samples/wrench.carve</c>, copied beside the scenario),
/// <c>bake</c> both maps at 1024 with padding 8 into the run folder's <c>textures/</c>, with fps-arms' group colours as the surface,
/// then <c>define_material</c> <c>wrench</c> from the base colour and <c>set_material</c> on every face. Seen in the <c>uvs</c> look
/// with the base colour and with the <c>steel</c> mask under it (and a 4x crop of a steel island's padding), and on the textured
/// wrench from six sides, close on the jaw, the grip and face 80, and in a six-view sheet; then exported and the glb's frames
/// compared with render's under D-6's rule.
/// </summary>
internal sealed class SurfaceStage : IBakesStage
{
    private const int Size = 1024;

    /// <summary>fps-arms' <c>steel</c> colour, which <see cref="BevelStage"/> predicts its strips' shade from.</summary>
    internal const string SteelColour = "#80858A";

    /// <summary>fps-arms' <c>GROUP_RGB</c> (<c>pipeline/tools.py</c>), <c>all</c> first as <c>dark</c> so a later group wins its faces.</summary>
    private static readonly (string Group, string Colour)[] Surface =
    [
        ("all", "#36393E"), ("rubber", "#282A2E"), ("steel", SteelColour), ("dark", "#36393E"), ("red", "#BE3C32"), ("black", "#1E1F22"),
    ];

    /// <summary>
    /// The person's ruling (2026-11-20): in each view the darkest <c>steel</c> sample is at least this much Rec. 709 luma above the
    /// lightest <c>rubber</c>, <c>dark</c> or <c>black</c> sample of that view. Probed on the wrench's own frames: least 45.4 (left).
    /// </summary>
    private const double SteelMargin = 30;

    /// <summary>
    /// The person's ruling (2026-11-20): a <c>red</c> sample's R leads max(G, B) by at least this; no other group's leads at all
    /// (lead at most 0). Probed: red at least 105, the rest at most −6.
    /// </summary>
    private const int RedLeadMin = 50;

    public async Task RunAsync(ScenarioDriver driver)
    {
        var here = Path.GetDirectoryName(typeof(SurfaceStage).Assembly.Location)!;
        await CallJsonAsync(driver, "open", new { path = Path.Combine(here, "wrench.carve") });
        var scene = await Scene.ReadAsync(driver, "wrench");
        var mesh = scene["wrench"];
        var groups = GroupsOf(mesh.Raw);

        // The face's group and colour as the surface's order gives them: the last entry naming a group the face is in.
        var groupOf = new Dictionary<int, string>();
        var colourOf = new Dictionary<int, (int R, int G, int B)>();
        foreach (var (group, colour) in Surface)
        {
            foreach (var f in groups[group]) (groupOf[f], colourOf[f]) = (group, Hex(colour));
        }

        if (colourOf.Count != mesh.Faces.Length) Note($"surface: {colourOf.Count} of {mesh.Faces.Length} faces have a colour");

        // The bake. The swap break bakes steel and red swapped; the faint break bakes steel a dimmer grey and red a duller red, so
        // the steel margin and the red lead each fall under the ruled numbers while red still leads and steel is still grey. Every
        // check still expects the colours above.
        string Baked(string group, string colour) => (Break, group) switch
        {
            ("swap", "steel") => "#BE3C32",
            ("swap", "red") => SteelColour,
            ("faint", "steel") => "#5C6064",
            ("faint", "red") => "#8C6460",
            _ => colour,
        };
        var surface = Surface.ToDictionary(e => e.Group, e => (object)new { base_colour = Baked(e.Group, e.Colour) });
        var textures = Path.Combine(driver.RunFolder, "textures");
        var bake = await CallJsonAsync(driver, "bake", new { mesh = "wrench", folder = textures, size = Size, padding = 8, maps = new[] { "masks", "base_colour" }, surface });
        var files = bake.GetProperty("files").EnumerateArray().ToArray();
        WriteBakes(driver, files);
        if (bake.GetProperty("skipped_faces").GetArrayLength() != 0 || bake.GetProperty("warnings").GetArrayLength() != 0) Note($"bake: expected no skipped faces and no warnings, got {bake}");
        var wantFiles = groups.Keys.Select(g => $"wrench.mask.{g}.png").Append("wrench.base_colour.png").Order().ToArray();
        var gotFiles = files.Select(f => Path.GetFileName(f.GetProperty("path").GetString()!)).Order().ToArray();
        if (!wantFiles.SequenceEqual(gotFiles)) Note($"bake: the files are {string.Join(", ", gotFiles)}, expected {string.Join(", ", wantFiles)}");
        string PathOf(string map, string name) => files.First(f => f.GetProperty("map").GetString() == map && (map == "base_colour" || f.GetProperty("name").GetString() == name)).GetProperty("path").GetString()!;
        var baseColour = PathOf("base_colour", "");
        var steelMask = PathOf("masks", "steel");

        await CallJsonAsync(driver, "define_material", new { name = "wrench", base_colour_texture = baseColour });
        await CallJsonAsync(driver, "set_material", new { material = "wrench", group = "all" });

        // The uvs look with each bake under it, by T-121's block rule, the islands also held to the colour worked from the groups.
        var uvsBase = await driver.CaptureAsync("wrench-uvs-base-colour", new { mode = "uvs", image = baseColour, size = Size });
        CheckUvImage("wrench-uvs-base-colour", uvsBase, mesh, Texels.Read(baseColour), f => colourOf[f]);
        var steel = groups["steel"].ToHashSet();
        var uvsSteel = await driver.CaptureAsync("wrench-uvs-steel", new { mode = "uvs", image = steelMask, size = Size });
        CheckUvImage("wrench-uvs-steel", uvsSteel, mesh, Texels.Read(steelMask), f => steel.Contains(f) ? (255, 255, 255) : (0, 0, 0));
        var (cx, cy) = SteelEdge(mesh, steel);
        WriteCrop(uvsSteel, Path.Combine(driver.RunFolder, "frames", "wrench-uvs-steel-padding-4x.png"), cx - 48, cy - 48, 96, 4);

        // The textured wrench from every side, then close.
        var jaw = Mean(mesh, groups["dark"]);
        var grip = Mean(mesh, groups["rubber"]);
        var views = new View[]
        {
            new("wrench-front", Vector3.UnitZ, Size), new("wrench-back", -Vector3.UnitZ, Size), new("wrench-left", -Vector3.UnitX, Size, Mirror: false),
            new("wrench-right", Vector3.UnitX, Size, Mirror: false), new("wrench-top", Vector3.UnitY, Size), new("wrench-three-quarter", ThreeQuarter, Size),
            new("wrench-jaw-close", ThreeQuarter, Size, 4, jaw), new("wrench-grip-close", ThreeQuarter, Size, 4, grip),
            new("wrench-face-80-close", Vector3.Normalize(mesh.Normal(80)), Size, 8, mesh.Centre(80), Mirror: false),
        };
        var frames = new Dictionary<string, ScenarioFrame>();
        var samples = new List<Sample>();
        var mirrors = new List<string> { "view\tface\tx\ty\tr\tg\tb\tred_lead" };
        foreach (var view in views)
        {
            var frame = frames[view.Name] = await driver.CaptureAsync(view.Name, view.Args);
            var seen = SampleFaces(frame, view, scene, mesh, groupOf);
            samples.AddRange(seen);
            if (seen.Count < 10) Note($"{view.Name}: only {seen.Count} points of the faces could be sampled");
            foreach (var s in seen) CheckClass(s);
            CheckSteelMargin(view.Name, seen);

            // M-2: the red box is off the wrench's middle, so its mirror in x must not show red.
            foreach (var s in seen.Where(s => view.Mirror && s.Group == "red" && s.Point == "centre"))
            {
                if (Mirrored(frame, view, scene, mesh.Centre(s.Face)) is not { } m) continue;
                mirrors.Add(string.Join('\t', view.Name, s.Face, m.X, m.Y, m.Shown.R, m.Shown.G, m.Shown.B, RedLead(m.Shown)));
                if (RedLead(m.Shown) > 0) Note($"{view.Name}: face {s.Face}'s mirror in x, pixel ({m.X}, {m.Y}), shows red {m.Shown}: the camera is mirrored");
            }
        }

        foreach (var name in new[] { "wrench-front", "wrench-three-quarter", "wrench-jaw-close" })
        {
            if (!samples.Any(s => s.View == name && s.Group == "red")) Note($"{name}: the red box is not sampled, so a mirrored camera would not be caught");
        }

        foreach (var group in new[] { "rubber", "steel", "dark", "red", "black" })
        {
            if (!samples.Any(s => s.Group == group)) Note($"no view samples a face of '{group}'");
        }

        WriteSamples(driver, samples, mirrors);

        await driver.CaptureAsync("wrench-sheet", new
        {
            views = new object[] { "+z", "-z", "-x", "+x", "+y", new { direction = new[] { ThreeQuarter.X, ThreeQuarter.Y, ThreeQuarter.Z } } },
            columns = 3, size = 256, mode = "shaded",
        });

        await CompareExportAsync(driver, frames, views);
    }

    /// <summary>A sample's class against its group's: <c>red</c> leads by <see cref="RedLeadMin"/> or more, and nothing else leads.</summary>
    private static void CheckClass(Sample s)
    {
        var at = $"{s.View}: face {s.Face} ({s.Group}) at its {s.Point}, pixel ({s.X}, {s.Y}), shows {s.Shown}";
        if (s.Group == "red" && s.RedLead < RedLeadMin) Note($"{at}, red lead {s.RedLead}, under {RedLeadMin}");
        if (s.Group != "red" && s.RedLead > 0) Note($"{at}, red lead {s.RedLead}, red-dominant");
    }

    /// <summary>
    /// In one view, the darkest <c>steel</c> sample against the lightest <c>rubber</c>, <c>dark</c> or <c>black</c> one: at least
    /// <see cref="SteelMargin"/> of luma apart. A view with no sample of either side has nothing to compare (every group's presence is
    /// checked across the views).
    /// </summary>
    private static void CheckSteelMargin(string view, List<Sample> seen)
    {
        var steel = seen.Where(s => s.Group == "steel").ToList();
        var darks = seen.Where(s => s.Group is "rubber" or "dark" or "black").ToList();
        if (steel.Count == 0 || darks.Count == 0) return;
        var (least, most) = (steel.MinBy(s => s.Luma)!, darks.MaxBy(s => s.Luma)!);
        if (least.Luma - most.Luma < SteelMargin)
        {
            Note(string.Create(CultureInfo.InvariantCulture, $"{view}: steel margin {least.Luma - most.Luma:F1} under {SteelMargin} (steel face {least.Face} at its {least.Point}, luma {least.Luma:F1}; {most.Group} face {most.Face} at its {most.Point}, luma {most.Luma:F1})"));
        }
    }

    /// <summary>
    /// The pixel, in a 1024 <c>uvs</c> frame, at the middle of the longest UV edge of a steel face that no other face shares: an
    /// island's border, where the padding starts.
    /// </summary>
    private static (int X, int Y) SteelEdge(SceneMesh mesh, HashSet<int> steel)
    {
        var count = new Dictionary<string, int>();
        IEnumerable<(Vector2 A, Vector2 B, string K)> Edges(int f) => mesh.Uvs[f].Select((c, i) =>
        {
            var d = mesh.Uvs[f][(i + 1) % mesh.Uvs[f].Length];
            var (k1, k2) = (UvKey(c), UvKey(d));
            return (c, d, string.CompareOrdinal(k1, k2) < 0 ? k1 + "|" + k2 : k2 + "|" + k1);
        });
        foreach (var f in mesh.Uvs.Keys)
        {
            foreach (var e in Edges(f)) count[e.K] = count.GetValueOrDefault(e.K) + 1;
        }

        var best = steel.Where(mesh.Uvs.ContainsKey).SelectMany(Edges).Where(e => count[e.K] == 1).MaxBy(e => Vector2.Distance(e.A, e.B));
        return UvPixel((best.A + best.B) / 2, Size);
    }

    /// <summary><c>bakes.tsv</c>: one row per baked file with its stats from the result.</summary>
    private static void WriteBakes(ScenarioDriver driver, JsonElement[] files)
    {
        var lines = new List<string> { "map\tname\tpath\tsize\treplaced\tcoverage\tmin\tmax\tmean" };
        foreach (var f in files)
        {
            string Field(string k) => f.TryGetProperty(k, out var p) ? p.ValueKind == JsonValueKind.String ? p.GetString()! : p.GetRawText() : "-";
            lines.Add(string.Join('\t', Field("map"), Field("name"), Field("path"), Field("size"), Field("replaced"), Field("coverage"), Field("min"), Field("max"), Field("mean")));
        }

        File.WriteAllLines(Path.Combine(driver.RunFolder, "bakes.tsv"), lines);
    }

    /// <summary>
    /// <c>samples.tsv</c>, every sampled point, and <c>probe.tsv</c>, per view the least steel luma against the most of each dark
    /// group and the red lead of the red faces against the rest: the numbers a threshold is chosen from.
    /// </summary>
    private static void WriteSamples(ScenarioDriver driver, List<Sample> samples, List<string> mirrors)
    {
        var lines = new List<string> { "view\tface\tgroup\tpoint\tx\ty\tr\tg\tb\tluma\tred_lead" };
        lines.AddRange(samples.Select(s => string.Create(CultureInfo.InvariantCulture,
            $"{s.View}\t{s.Face}\t{s.Group}\t{s.Point}\t{s.X}\t{s.Y}\t{s.Shown.R}\t{s.Shown.G}\t{s.Shown.B}\t{s.Luma:F1}\t{s.RedLead}")));
        File.WriteAllLines(Path.Combine(driver.RunFolder, "samples.tsv"), lines);
        File.WriteAllLines(Path.Combine(driver.RunFolder, "mirrors.tsv"), mirrors);

        string Stat(IEnumerable<Sample> of, Func<IEnumerable<double>, double> pick, Func<Sample, double> by) =>
            of.Any() ? pick(of.Select(by)).ToString("F1", CultureInfo.InvariantCulture) : "-";
        var probe = new List<string> { "view\tsteel_n\tsteel_min\tsteel_max\trubber_min\trubber_max\tdark_min\tdark_max\tblack_max\tmargin\tred_n\tred_lead_min\tother_lead_max" };
        foreach (var view in samples.Select(s => s.View).Distinct().Append("all"))
        {
            var of = samples.Where(s => view == "all" || s.View == view).ToList();
            var (st, ru, dk, bl, rd) = (of.Where(s => s.Group == "steel"), of.Where(s => s.Group == "rubber"), of.Where(s => s.Group == "dark"), of.Where(s => s.Group == "black"), of.Where(s => s.Group == "red"));
            var darks = of.Where(s => s.Group is "rubber" or "dark" or "black").ToList();
            var margin = st.Any() && darks.Count > 0 ? (st.Min(s => s.Luma) - darks.Max(s => s.Luma)).ToString("F1", CultureInfo.InvariantCulture) : "-";
            probe.Add(string.Join('\t', view, st.Count(), Stat(st, Enumerable.Min, s => s.Luma), Stat(st, Enumerable.Max, s => s.Luma),
                Stat(ru, Enumerable.Min, s => s.Luma), Stat(ru, Enumerable.Max, s => s.Luma), Stat(dk, Enumerable.Min, s => s.Luma), Stat(dk, Enumerable.Max, s => s.Luma),
                Stat(bl, Enumerable.Max, s => s.Luma), margin, rd.Count(), Stat(rd, Enumerable.Min, s => s.RedLead), Stat(of.Where(s => s.Group != "red"), Enumerable.Max, s => s.RedLead)));
        }

        File.WriteAllLines(Path.Combine(driver.RunFolder, "probe.tsv"), probe);
    }
}
