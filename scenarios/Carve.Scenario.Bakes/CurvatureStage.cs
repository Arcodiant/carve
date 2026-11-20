using System.Globalization;
using System.Numerics;
using Carve.Debug;
using Carve.Scenario.Uvs;
using static Carve.Scenario.Bakes.BakesChecks;
using static Carve.Scenario.Crate.CrateBuild;

namespace Carve.Scenario.Bakes;

/// <summary>
/// The curvature map (T-126): the wrench baked with <c>curvature</c> at 1024, width 0.003. Seen in the <c>uvs</c> look with the map under
/// it, whole and cropped at 4x on the jaw's islands. Checked in the PNG: a texel on each convex jaw edge reads above 128 by
/// <see cref="ConvexMargin"/>, and the middle of the handle's flat face reads exactly 128.
/// </summary>
internal sealed class CurvatureStage : IBakesStage
{
    private const int Size = 1024;
    private const double Width = 0.003;

    /// <summary>
    /// The person's ruling (2026-11-20): a texel at the middle of a convex jaw edge's UV edge, on all 120 edges, the 71 degree ones
    /// included, must read at least 128 plus this (218). Probed on the wrench: the least is 227 (a 71 degree edge, margin 99), 254 on
    /// the 90 and 109 degree edges (margin 126).
    /// </summary>
    private const int ConvexMargin = 90;

    /// <summary>PROVISIONAL, not yet answered by the person: a texel on the L-block's inner edge must read at most 128 minus this.</summary>
    private const int ConcaveMargin = 90;

    /// <summary>Bends of at least this many degrees between two jaw faces count as the jaw's edges (the wrench's are 65 and up, its rounding 30 and under).</summary>
    private const double EdgeDegrees = 60;

    public async Task RunAsync(ScenarioDriver driver)
    {
        var scene = await Scene.ReadAsync(driver, "wrench");
        var mesh = scene["wrench"];
        var groups = mesh.Raw.GetProperty("groups").EnumerateArray()
            .ToDictionary(g => g.GetProperty("name").GetString()!, g => g.GetProperty("faces").EnumerateArray().Select(f => f.GetInt32()).ToArray());
        var textures = Path.Combine(driver.RunFolder, "textures");
        var bake = await CallJsonAsync(driver, "bake", new { mesh = "wrench", folder = textures, size = Size, padding = 8, maps = new[] { "curvature" }, curvature = new { width = Width } });
        var file = bake.GetProperty("files").EnumerateArray().Single();
        var path = file.GetProperty("path").GetString()!;
        if (Path.GetFileName(path) != "wrench.curvature.png") Note($"curvature: the file is {path}, expected wrench.curvature.png");
        if (bake.GetProperty("skipped_faces").GetArrayLength() != 0 || bake.GetProperty("warnings").GetArrayLength() != 0) Note($"curvature: expected no skipped faces and no warnings, got {bake}");
        var png = Texels.Read(path);

        // The look with the map under it, whole and cropped at 4x on the jaw's islands.
        var frame = await driver.CaptureAsync("wrench-uvs-curvature", new { mode = "uvs", image = path, size = Size });
        CheckUvImage("wrench-uvs-curvature", frame, mesh, png, blend: 2);
        var (cx, cy, side) = JawBox(mesh, groups["dark"]);
        WriteCrop(frame, Path.Combine(driver.RunFolder, "frames", "wrench-uvs-curvature-jaw-4x.png"), cx - side / 2, cy - side / 2, side, 4);

        // The s-inverted break reads the map as if convex and concave were swapped: 255 less each value.
        int Read(int x, int y) => Break == "curvature" ? 255 - png.At(x, y).R : png.At(x, y).R;
        var rows = new List<string> { "kind\tface\tother\tbend_deg\tx\ty\tvalue" };

        // A texel on each convex jaw edge: the one holding the middle of the edge's UV edge on its face.
        var edges = JawEdges(mesh, groups["dark"]).ToList();
        foreach (var e in edges)
        {
            var uv = mesh.Uvs[e.Face];
            var mid = (uv[e.Corner] + uv[(e.Corner + 1) % uv.Length]) / 2;
            var (x, y) = ((int)Math.Floor(mid.X * Size), (int)Math.Floor(mid.Y * Size));
            var value = Read(x, y);
            rows.Add(string.Create(CultureInfo.InvariantCulture, $"convex\t{e.Face}\t{e.Other}\t{e.Degrees:F1}\t{x}\t{y}\t{value}"));
            if (value < 128 + ConvexMargin) Note($"curvature: jaw face {e.Face}'s convex edge to face {e.Other} ({e.Degrees:F1} degrees), texel ({x}, {y}) reads {value}, under {128 + ConvexMargin}");
        }

        if (edges.Count < 50) Note($"curvature: only {edges.Count} convex jaw edges were found to check");

        // The middle of the handle's flat face: exactly 128.
        var handle = groups["rubber"].MaxBy(f => Area(mesh.Uvs[f]));
        var c = mesh.Uvs[handle].Aggregate(Vector2.Zero, (a, b) => a + b) / mesh.Uvs[handle].Length;
        var (fx, fy) = ((int)Math.Floor(c.X * Size), (int)Math.Floor(c.Y * Size));
        var flat = Read(fx, fy);
        rows.Add($"flat\t{handle}\t-\t0\t{fx}\t{fy}\t{flat}");
        if (flat != 128) Note($"curvature: the middle of the handle's face {handle}, texel ({fx}, {fy}), reads {flat}, expected exactly 128");

        await ConcaveAsync(driver, rows);
        File.WriteAllLines(Path.Combine(driver.RunFolder, "curvature.tsv"), rows);
    }

    /// <summary>
    /// The concave check (the person's ruling, 2026-11-20): the wrench has no concave break edge, so a small L-block (the L hexagon
    /// of 0.1 m arms, extruded 0.1 m) is built through the tools, given box UVs, and baked with <c>curvature</c>. A texel on its inner edge,
    /// the one just inside each of its two faces (4 and 5) at the middle of that edge's UV edge, reads below 128 by
    /// <see cref="ConcaveMargin"/>. The block is undone afterwards so the wrench's model is as the earlier stages left it.
    /// </summary>
    private async Task ConcaveAsync(ScenarioDriver driver, List<string> rows)
    {
        int[][] l = [[0, 0], [2, 0], [2, 1], [1, 1], [1, 2], [0, 2]];
        var vertices = l.Select(p => new[] { p[0] * 0.1, p[1] * 0.1, 0.0 }).Concat(l.Select(p => new[] { p[0] * 0.1, p[1] * 0.1, 0.1 })).ToArray();
        var faces = new List<int[]> { new[] { 5, 4, 3, 2, 1, 0 }, new[] { 6, 7, 8, 9, 10, 11 } };
        for (var i = 0; i < 6; i++) faces.Add(new[] { i, (i + 1) % 6, (i + 1) % 6 + 6, i + 6 });
        await CallJsonAsync(driver, "define_mesh", new { name = "lblock", vertices, faces });
        await CallJsonAsync(driver, "project_uvs", new { mesh = "lblock", method = "box", into = new[] { 0.04, 0.04, 0.96, 0.96 } });
        var block = await Scene.ReadOneAsync(driver, "lblock");

        var textures = Path.Combine(driver.RunFolder, "textures");
        var bake = await CallJsonAsync(driver, "bake", new { mesh = "lblock", folder = textures, size = Size, padding = 8, maps = new[] { "curvature" }, curvature = new { width = Width } });
        var path = bake.GetProperty("files").EnumerateArray().Single().GetProperty("path").GetString()!;
        if (Path.GetFileName(path) != "lblock.curvature.png") Note($"curvature: the L-block's file is {path}, expected lblock.curvature.png");
        if (bake.GetProperty("skipped_faces").GetArrayLength() != 0 || bake.GetProperty("warnings").GetArrayLength() != 0) Note($"curvature: the L-block: expected no skipped faces and no warnings, got {bake}");
        var png = Texels.Read(path);

        var frame = await driver.CaptureAsync("lblock-uvs-curvature", new { mode = "uvs", mesh = "lblock", image = path, size = Size });
        CheckUvImage("lblock-uvs-curvature", frame, block, png, blend: 2);

        // The inner edge is between side faces 4 (vertices 2, 3, 9, 8: its edge 3-9 is corner 1) and 5 (3, 4, 10, 9: corner 3). Box
        // islands abut with no gap, so the texel is the one half a texel inside the face from the middle of its UV edge, never the
        // neighbouring island's.
        foreach (var (face, corner) in new[] { (4, 1), (5, 3) })
        {
            var uv = block.Uvs[face];
            var (a, b) = (uv[corner], uv[(corner + 1) % uv.Length]);
            var inward = uv.Aggregate(Vector2.Zero, (s, p) => s + p) / uv.Length - (a + b) / 2;
            var along = Vector2.Normalize(b - a);
            inward -= along * Vector2.Dot(inward, along);
            var at = (a + b) / 2 + Vector2.Normalize(inward) * (0.5f / Size);
            var (x, y) = ((int)Math.Floor(at.X * Size), (int)Math.Floor(at.Y * Size));
            var value = Break == "curvature" ? 255 - png.At(x, y).R : png.At(x, y).R;
            rows.Add(string.Create(CultureInfo.InvariantCulture, $"concave	{face}	-	90.0	{x}	{y}	{value}"));
            if (value > 128 - ConcaveMargin) Note($"curvature: the L-block's inner edge, face {face}, texel ({x}, {y}) reads {value}, over {128 - ConcaveMargin}");
        }

        var (c, edge) = (block.Uvs[4][1], block.Uvs[4][2]);
        var centre = (c + edge) / 2;
        var (px, py) = ((int)(Size / 16.0 + centre.X * Size * 7 / 8.0), (int)(Size / 16.0 + centre.Y * Size * 7 / 8.0));
        WriteCrop(frame, Path.Combine(driver.RunFolder, "frames", "lblock-uvs-curvature-inner-4x.png"), px - 64, py - 64, 128, 4);

        // Put the model back as it was: the L-block's UVs and its definition.
        await CallJsonAsync(driver, "undo", new { });
        await CallJsonAsync(driver, "undo", new { });
        if ((await driver.CallAsync("get_mesh", new { mesh = "lblock" })).IsError is not true) Note("curvature: the L-block is still in the model after its two undos");
        if ((await driver.CallAsync("get_mesh", new { mesh = "wrench" })).IsError is true) Note("curvature: the wrench is gone after the L-block's undos");
    }

    private static float Area(Vector2[] p)
    {
        var a = 0f;
        for (var i = 0; i < p.Length; i++) a += p[i].X * p[(i + 1) % p.Length].Y - p[(i + 1) % p.Length].X * p[i].Y;
        return MathF.Abs(a) / 2;
    }

    private sealed record JawEdge(int Face, int Corner, int Other, double Degrees);

    /// <summary>
    /// Each edge shared by two jaw faces that bends by <see cref="EdgeDegrees"/> or more and turns away from the face (the other face's
    /// centre lies behind this face's plane), once for each of its two faces, worked from the geometry alone.
    /// </summary>
    private static IEnumerable<JawEdge> JawEdges(SceneMesh mesh, int[] jaw)
    {
        var inJaw = jaw.ToHashSet();
        var owners = new Dictionary<(int, int), List<int>>();
        foreach (var f in jaw)
        {
            var loop = mesh.Faces[f];
            for (var i = 0; i < loop.Length; i++)
            {
                var key = (Math.Min(loop[i], loop[(i + 1) % loop.Length]), Math.Max(loop[i], loop[(i + 1) % loop.Length]));
                if (!owners.TryGetValue(key, out var l)) owners[key] = l = [];
                l.Add(f);
            }
        }

        foreach (var f in jaw)
        {
            var loop = mesh.Faces[f];
            for (var i = 0; i < loop.Length; i++)
            {
                var key = (Math.Min(loop[i], loop[(i + 1) % loop.Length]), Math.Max(loop[i], loop[(i + 1) % loop.Length]));
                if (owners[key].Count != 2 || !mesh.Uvs.ContainsKey(f)) continue;
                var g = owners[key].Single(o => o != f);
                var (n, m) = (Vector3.Normalize(mesh.Normal(f)), Vector3.Normalize(mesh.Normal(g)));
                var degrees = Math.Acos(Math.Clamp(Vector3.Dot(n, m), -1f, 1f)) * 180 / Math.PI;
                var mid = (mesh.Vertices[key.Item1] + mesh.Vertices[key.Item2]) / 2;
                if (degrees >= EdgeDegrees && inJaw.Contains(g) && Vector3.Dot(mesh.Centre(g) - mid, n) < 0) yield return new JawEdge(f, i, g, degrees);
            }
        }
    }

    /// <summary>
    /// The source square of the 4x crop in the 1024 <c>uvs</c> frame: centred on the jaw's largest face and the faces joined to it by a
    /// shared UV corner (the jaw's ring and its caps), 32 pixels past them, from 64 to 256 pixels.
    /// </summary>
    private static (int X, int Y, int Side) JawBox(SceneMesh mesh, int[] jaw)
    {
        static string Key(Vector2 p) => string.Create(CultureInfo.InvariantCulture, $"{p.X:F5},{p.Y:F5}");
        var start = jaw.Where(mesh.Uvs.ContainsKey).MaxBy(f => Area(mesh.Uvs[f]));
        var cluster = new HashSet<int> { start };
        var corners = mesh.Uvs[start].Select(Key).ToHashSet();
        for (var grown = true; grown;)
        {
            grown = false;
            foreach (var f in jaw.Where(mesh.Uvs.ContainsKey).Where(f => !cluster.Contains(f)).ToList())
            {
                if (!mesh.Uvs[f].Any(p => corners.Contains(Key(p)))) continue;
                cluster.Add(f);
                foreach (var p in mesh.Uvs[f]) corners.Add(Key(p));
                grown = true;
            }
        }

        double origin = Size / 16.0, span = Size * 7 / 8.0;
        var points = cluster.SelectMany(f => mesh.Uvs[f]).Select(p => new Vector2((float)(origin + p.X * span), (float)(origin + p.Y * span))).ToArray();
        var (min, max) = (points.Aggregate(Vector2.Min), points.Aggregate(Vector2.Max));
        var extent = (int)Math.Ceiling(Math.Max(max.X - min.X, max.Y - min.Y)) + 32;
        return ((int)((min.X + max.X) / 2), (int)((min.Y + max.Y) / 2), Math.Clamp(extent, 64, 256));
    }
}
