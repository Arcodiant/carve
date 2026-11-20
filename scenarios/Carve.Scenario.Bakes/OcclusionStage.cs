using System.Globalization;
using System.Numerics;
using System.Text.Json;
using Carve.Debug;
using Carve.Scenario.Uvs;
using static Carve.Scenario.Bakes.BakesChecks;
using static Carve.Scenario.Crate.CrateBuild;

namespace Carve.Scenario.Bakes;

/// <summary>
/// Stage 2 (T-124): occlusion and the ORM. <c>bake</c> <c>occlusion</c> and <c>orm</c> of the wrench at 1024 with 32 rays and reach
/// 0.035 (fps-arms') and fps-arms' roughness and metallic; redefine <c>wrench</c> with stage 1's base colour, the ORM in both slots and
/// occlusion 1. Seen in the <c>uvs</c> look with the occlusion under it, on the wrench from six sides, the three-quarter view and stage 1's
/// close frames, and close on the square drive, the wrench's one crevice. Checked: the crevice darker than the grip's middle, the grip's
/// middle open, the ORM's G and B exactly the surface's on every covered texel, and render against <c>render_glb</c> with the full material.
/// <c>CARVE_BAKES_BREAK</c> <c>reach</c> bakes with reach 0.0001 and <c>metal</c> with <c>steel</c> metallic 0.
/// </summary>
internal sealed class OcclusionStage : IBakesStage
{
    private const int Size = 1024;
    private const int Rays = 32;
    private const double Reach = 0.035;

    /// <summary>fps-arms' roughness and metallic by group (<c>pipeline/tools.py</c>), <c>all</c> first as <c>dark</c>, as stage 1's colours.</summary>
    private static readonly (string Group, double Roughness, double Metallic)[] Surface =
    [
        ("all", 0.62, 0), ("rubber", 0.88, 0), ("steel", 0.36, 1), ("dark", 0.62, 0), ("red", 0.45, 0), ("black", 0.50, 0),
    ];

    /// <summary>
    /// Ruled by the person (2026-11-20, corrected): every crevice texel (socket top by the drive) reads at least this far below
    /// every open texel (the grip's middle), in 8-bit occlusion: the open texels' least minus the crevice texels' most. Break:
    /// <c>reach</c>.
    /// </summary>
    private const int CreviceMargin = 50;

    /// <summary>Ruled by the person (2026-11-20): open texels far from other geometry (the grip's middle) read at least this.</summary>
    private const int OpenLeast = 250;

    // Faces of samples/wrench.carve. The wrench is separate solids stacked: the steel square drive (faces 100 to 105) stands between
    // the dark ring under the head (face 99, facing -z) and the dark socket's top (face 118, facing +z), the one crevice it has; the
    // rubber grip's facet 5 faces +z with nothing within reach above it.
    private static readonly int[] Drive = [100, 101, 102, 103, 104, 105];
    private const int SocketTop = 118;
    private const int RingUnder = 99;
    private const int HeadTop = 65;
    private const int GripFacet = 5;

    /// <summary>How far from the drive's footprint, in metres, a texel of the faces it stands between still counts as the crevice's corner.</summary>
    private const float CornerBand = 0.0015f;

    public async Task RunAsync(ScenarioDriver driver)
    {
        var scene = await Scene.ReadAsync(driver, "wrench");
        var mesh = scene["wrench"];
        var groups = GroupsOf(mesh.Raw);
        var factorOf = new Dictionary<int, (double R, double M)>();
        foreach (var (group, r, m) in Surface)
        {
            foreach (var f in groups[group]) factorOf[f] = (r, m);
        }

        var surface = Surface.ToDictionary(e => e.Group, e => (object)new { roughness = e.Roughness, metallic = Break == "metal" && e.Group == "steel" ? 0 : e.Metallic });
        var textures = Path.Combine(driver.RunFolder, "textures");
        var reach = Break == "reach" ? 0.0001 : Reach;
        var bake = await CallJsonAsync(driver, "bake", new { mesh = "wrench", folder = textures, size = Size, padding = 8, maps = new[] { "occlusion", "orm" }, surface, occlusion = new { rays = Rays, reach } });
        var files = bake.GetProperty("files").EnumerateArray().ToArray();
        string PathOf(string map) => files.Single(f => f.GetProperty("map").GetString() == map).GetProperty("path").GetString()!;
        var occlusionPath = PathOf("occlusion");
        var ormPath = PathOf("orm");
        var baseColour = Path.Combine(textures, "wrench.base_colour.png");

        await CallJsonAsync(driver, "define_material", new { name = "wrench", base_colour_texture = baseColour, metallic_roughness_texture = ormPath, occlusion_texture = ormPath, occlusion = 1 });
        await CallJsonAsync(driver, "set_material", new { material = "wrench", group = "all" });

        var occlusion = Texels.Read(occlusionPath);
        var orm = Texels.Read(ormPath);
        CheckOrm(mesh, orm, factorOf);
        var regions = Probe(mesh, occlusion);
        CheckOcclusion(regions);
        WriteProbe(driver, files, regions);

        await driver.CaptureAsync("wrench-orm-uvs-occlusion", new { mode = "uvs", image = occlusionPath, size = Size });
        var jaw = Mean(mesh, groups["dark"]);
        var grip = Mean(mesh, groups["rubber"]);
        var drive = Mean(mesh, Drive);
        var views = new View[]
        {
            new("wrench-orm-front", Vector3.UnitZ, Size), new("wrench-orm-back", -Vector3.UnitZ, Size), new("wrench-orm-left", -Vector3.UnitX, Size),
            new("wrench-orm-right", Vector3.UnitX, Size), new("wrench-orm-top", Vector3.UnitY, Size), new("wrench-orm-three-quarter", ThreeQuarter, Size),
            new("wrench-orm-jaw-close", ThreeQuarter, Size, 4, jaw), new("wrench-orm-grip-close", ThreeQuarter, Size, 4, grip),
            new("wrench-orm-face-80-close", Vector3.Normalize(mesh.Normal(80)), Size, 8, mesh.Centre(80)),
            new("wrench-orm-drive-close", new Vector3(1, 0.35f, 0), Size, 8, drive),
        };
        var frames = new Dictionary<string, ScenarioFrame>();
        foreach (var view in views) frames[view.Name] = await driver.CaptureAsync(view.Name, view.Args);

        await CompareExportAsync(driver, frames, views, "orm-");
    }

    /// <summary>
    /// The ORM's G and B against the surface: every texel whose centre lies inside a face's UV polygon reads that face's roughness and
    /// metallic, as 8-bit bytes, exactly.
    /// </summary>
    private static void CheckOrm(SceneMesh mesh, Texels orm, Dictionary<int, (double R, double M)> factorOf)
    {
        static int Byte(double v) => (int)Math.Round(v * 255, MidpointRounding.AwayFromZero);
        long checkedTexels = 0, wrong = 0;
        string? first = null;
        foreach (var (face, x, y, _) in mesh.Uvs.Keys.SelectMany(f => TexelsOf(mesh, f, orm.Width)))
        {
            checkedTexels++;
            var (r, m) = factorOf[face];
            var (_, g, b) = orm.At(x, y);
            if (g == Byte(r) && b == Byte(m)) continue;
            wrong++;
            first ??= $"face {face} at texel ({x}, {y}) reads G {g} B {b}, expected {Byte(r)} {Byte(m)}";
        }
        if (checkedTexels < 100_000) Note($"orm: only {checkedTexels} texels checked");
        if (wrong > 0) Note($"orm: {wrong} of {checkedTexels} covered texels differ from the surface's roughness and metallic; the first: {first}");
    }

    /// <summary>One region of the occlusion probe: its 8-bit values.</summary>
    private sealed record Region(string Name, int[] Values)
    {
        public int Min => Values.Length == 0 ? -1 : Values.Min();

        public int Max => Values.Length == 0 ? -1 : Values.Max();

        public double Mean => Values.Length == 0 ? -1 : Values.Average();
    }

    /// <summary>The probe's regions of the occlusion PNG, each the texels of some faces whose world point meets a condition.</summary>
    private static List<Region> Probe(SceneMesh mesh, Texels occlusion)
    {
        var footprint = Drive.SelectMany(f => mesh.Faces[f]).Select(v => mesh.Vertices[v]).ToArray();
        var lo = footprint.Aggregate(Vector3.Min);
        var hi = footprint.Aggregate(Vector3.Max);
        float Outside(Vector3 p) => MathF.Max(MathF.Max(lo.X - p.X, p.X - hi.X), MathF.Max(lo.Y - p.Y, p.Y - hi.Y));
        var ringRadius = mesh.Faces[66].Concat(mesh.Faces[77]).Select(v => new Vector2(mesh.Vertices[v].X, mesh.Vertices[v].Y - 0.214f).Length()).Max();
        var centre = mesh.Centre(GripFacet);

        Region Of(string name, IEnumerable<int> faces, Func<Vector3, bool> keep) => new(name,
            faces.SelectMany(f => TexelsOf(mesh, f, occlusion.Width)).Where(t => keep(t.World)).Select(t => occlusion.At(t.X, t.Y).R).ToArray());

        return
        [
            Of("socket-top-by-drive", [SocketTop], p => Outside(p) is > 0 and <= CornerBand),
            Of("ring-under-by-drive", [RingUnder], p => Outside(p) is > 0 and <= CornerBand),
            Of("head-top-by-ring", [HeadTop], p => new Vector2(p.X, p.Y - 0.214f).Length() - ringRadius is > 0 and <= CornerBand),
            Of("drive-sides-middle", Drive[..4], p => MathF.Abs(p.Z - (lo.Z + hi.Z) / 2) <= 0.002f),
            Of("grip-facet-5-middle", [GripFacet], p => Vector3.Distance(p, centre) <= 0.002f),
            Of("grip-all", Enumerable.Range(0, 12), _ => true),
        ];
    }

    /// <summary>The crevice against the grip's middle, and the grip's middle open, by the person's numbers.</summary>
    private static void CheckOcclusion(List<Region> regions)
    {
        var corner = regions.Single(r => r.Name == "socket-top-by-drive");
        var open = regions.Single(r => r.Name == "grip-facet-5-middle");
        if (corner.Values.Length < 20 || open.Values.Length < 20) Note($"occlusion: too few texels probed ({corner.Name} {corner.Values.Length}, {open.Name} {open.Values.Length})");
        var below = open.Min - corner.Max;
        if (below < CreviceMargin)
            Note($"occlusion: the crevice's most ({corner.Max}) is only {below} below the grip's middle's least ({open.Min}), under {CreviceMargin}");
        if (open.Min < OpenLeast) Note($"occlusion: the grip's middle reads {open.Min}, under {OpenLeast}");
    }

    /// <summary><c>occlusion-probe.tsv</c>: each region's count, least, mean and most, and each file's time; the numbers a threshold is chosen from.</summary>
    private static void WriteProbe(ScenarioDriver driver, JsonElement[] files, List<Region> regions)
    {
        var lines = new List<string> { "region\tn\tmin\tmean\tmax" };
        lines.AddRange(regions.Select(r => string.Create(CultureInfo.InvariantCulture, $"{r.Name}\t{r.Values.Length}\t{r.Min}\t{r.Mean:F1}\t{r.Max}")));
        var corner = regions.Single(r => r.Name == "socket-top-by-drive");
        var open = regions.Single(r => r.Name == "grip-facet-5-middle");
        lines.Add($"margin(grip-middle-min - crevice-max)\t-\t-\t{open.Min - corner.Max}\t-");
        lines.Add("");
        lines.Add("map\tpath\tms\tmin\tmax\tmean");
        lines.AddRange(files.Select(f => string.Join('\t', f.GetProperty("map").GetString(), f.GetProperty("path").GetString(), f.GetProperty("ms").GetRawText(),
            f.GetProperty("min").GetRawText(), f.GetProperty("max").GetRawText(), f.GetProperty("mean").GetRawText())));
        File.WriteAllLines(Path.Combine(driver.RunFolder, "occlusion-probe.tsv"), lines);
    }

    /// <summary>
    /// The texels of an image of side <paramref name="size"/> whose centres lie inside face <paramref name="face"/>'s UV polygon (a fan
    /// from its first corner; the wrench's faces are convex), each with its world point.
    /// </summary>
    private static IEnumerable<(int Face, int X, int Y, Vector3 World)> TexelsOf(SceneMesh mesh, int face, int size)
    {
        var uv = mesh.Uvs[face];
        var loop = mesh.Faces[face];
        var min = uv.Aggregate(Vector2.Min) * size;
        var max = uv.Aggregate(Vector2.Max) * size;
        for (var y = Math.Max(0, (int)min.Y - 1); y <= Math.Min(size - 1, (int)max.Y + 1); y++)
        for (var x = Math.Max(0, (int)min.X - 1); x <= Math.Min(size - 1, (int)max.X + 1); x++)
        {
            var p = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
            for (var k = 1; k + 1 < uv.Length; k++)
            {
                if (Weights(p, uv[0], uv[k], uv[k + 1]) is not { } w) continue;
                yield return (face, x, y, mesh.Vertices[loop[0]] * w.X + mesh.Vertices[loop[k]] * w.Y + mesh.Vertices[loop[k + 1]] * w.Z);
                break;
            }
        }
    }

    /// <summary>The barycentric weights of <paramref name="p"/> in triangle a, b, c when it lies strictly inside, else null.</summary>
    private static Vector3? Weights(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        static float Cross(Vector2 u, Vector2 v) => u.X * v.Y - u.Y * v.X;
        var area = Cross(b - a, c - a);
        if (area == 0) return null;
        var wa = Cross(b - p, c - p) / area;
        var wb = Cross(c - p, a - p) / area;
        var wc = 1 - wa - wb;
        return wa > 1e-6f && wb > 1e-6f && wc > 1e-6f ? new Vector3(wa, wb, wc) : null;
    }
}
