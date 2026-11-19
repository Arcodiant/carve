using System.Numerics;
using System.Text.Json;
using Carve.Debug;
using Carve.Rover;
using Carve.Scenario.Crate;
using Carve.Scenario.Cuts;
using Carve.Scenario.Shed;

namespace Carve.Scenario.Parts;

/// <summary>The scenario's break switch (T-96): each value makes the scenario wrong on purpose, and it must then fail. Unset it is the scenario.</summary>
internal static class PartsBreak
{
    /// <summary><c>mirror</c> (every frame read left for right), <c>no-instance-rotation</c> (the instances defined unturned), <c>levels-ignored</c> (the expected draw ignores <c>level</c>) or <c>part-not-isolated</c> (the expected draw ignores <c>part</c>).</summary>
    public static readonly string? Value = Environment.GetEnvironmentVariable("CARVE_PARTS_BREAK") is { Length: > 0 } b ? b : null;
}

/// <summary>One part's own geometry as <c>get_mesh</c> gives it: part-local, slot by slot (a deleted slot is not live).</summary>
internal sealed record LocalMesh(Vector3[] Vertices, bool[] LiveVertex, int[][] Faces, string?[] Colours)
{
    public int LiveVertexCount => LiveVertex.Count(v => v);

    public int LiveFaceCount => Faces.Count(f => f.Length > 0);

    public static LocalMesh Read(JsonElement mesh)
    {
        var vertices = mesh.GetProperty("vertices").EnumerateArray().ToArray();
        var at = vertices.Select(v => v.ValueKind == JsonValueKind.Null ? Vector3.Zero : V(v.GetProperty("at"))).ToArray();
        var faces = mesh.GetProperty("faces").EnumerateArray()
            .Select(f => f.ValueKind == JsonValueKind.Null ? [] : f.GetProperty("vertices").EnumerateArray().Select(i => i.GetInt32()).ToArray()).ToArray();
        var colours = new string?[faces.Length];
        foreach (var group in mesh.GetProperty("colours").EnumerateArray())
            foreach (var f in group.GetProperty("faces").EnumerateArray()) colours[f.GetInt32()] = group.GetProperty("colour").GetString();
        return new LocalMesh(at, [.. vertices.Select(v => v.ValueKind != JsonValueKind.Null)], faces, colours);
    }

    public static Vector3 V(JsonElement a) => new(a[0].GetSingle(), a[1].GetSingle(), a[2].GetSingle());
}

/// <summary>What a draw should show, worked here: the geometry placed in the model by <see cref="RoverSpec"/>'s tree, as a ray-test solid.</summary>
internal sealed class Placed
{
    public required Solid Solid { get; init; }
    public required string?[] Colours { get; init; }
    public required List<(string Part, int Start, int Count)> Occurrences { get; init; }
    public required Vector3 Min { get; init; }
    public required Vector3 Max { get; init; }
    public required int VertexCount { get; init; }
    public required int FaceCount { get; init; }

    public Range FacesOf(string part)
    {
        var o = Occurrences.Single(o => o.Part == part);
        return new Range(o.Start, o.Start + o.Count);
    }

    /// <summary>A face's pixel in 8 bit sRGB from <see cref="ColourRule"/>; an uncoloured face is <see cref="ColourRule.Grey"/>.</summary>
    public (int R, int G, int B) Shade(int face, Vector3 direction)
    {
        var normal = Solid.Normal(face);
        var lit = Colours[face] is { } hex
            ? ColourRule.Lit(hex, normal, direction)
            : ColourRule.Lit(ColourRule.Grey.X, ColourRule.Grey.Y, ColourRule.Grey.Z, normal, direction);
        return (ColourRule.Encode(lit[0]), ColourRule.Encode(lit[1]), ColourRule.Encode(lit[2]));
    }

    // Tolerance 4 until B-8: ColourRule predicts low-albedo faces too dark. The person's ruling of 2026-11-18 (T-94): 4 for
    // the wheels' #3C3C3C faces only, 2 everywhere else.
    public int Tolerance(int face) => Colours[face] == RoverSpec.DarkGrey ? 4 : 2;
}

/// <summary>The rover's geometry as the product holds it (<c>get_mesh</c>, live and at each kept level), placed by the spec's tree.</summary>
internal sealed class RoverWorld
{
    private readonly Dictionary<string, LocalMesh> _live = [];
    private readonly Dictionary<(string, int), LocalMesh> _level = [];

    /// <summary>The mast's rotation as the scenario now holds it (turned once the mast turns).</summary>
    public Quaternion MastRotation { get; set; } = Quaternion.Identity;

    public LocalMesh Live(string part) => _live[part];

    public static async Task<RoverWorld> ReadAsync(ScenarioDriver driver)
    {
        var world = new RoverWorld();
        foreach (var part in RoverSpec.GeometryParts)
        {
            world._live[part] = LocalMesh.Read(await Checks.CallJsonAsync(driver, "get_mesh", new { mesh = part }));
            foreach (var n in new[] { 0, 1 })
            {
                var got = await Checks.CallJsonAsync(driver, "get_mesh", new { mesh = part, level = n });
                if (got.GetProperty("level").GetProperty("shown").GetInt32() != n) continue;
                world._level[(part, n)] = LocalMesh.Read(got);
            }
        }

        return world;
    }

    /// <summary>The newest kept level at or below <paramref name="level"/>, or null when the part has none.</summary>
    private LocalMesh? At(string source, int level) =>
        Enumerable.Range(0, level + 1).Reverse().Select(n => _level.GetValueOrDefault((source, n))).FirstOrDefault(m => m is not null);

    public Matrix4x4 WorldOf(string name)
    {
        var p = RoverSpec.Find(name);
        var rotation = name == "Mast" ? MastRotation : p.Rotation;
        var local = Matrix4x4.CreateFromQuaternion(rotation) * Matrix4x4.CreateTranslation(p.Translation);
        return p.Parent is { } parent ? local * WorldOf(parent) : local;
    }

    private static bool Under(RoverSpec.PartSpec p, string root) => p.Name == root || (p.Parent is { } parent && Under(RoverSpec.Find(parent), root));

    /// <summary>The draw of <paramref name="part"/>'s subtree (the whole model when null) live or at <paramref name="level"/>, parts in pre-order.</summary>
    public Placed Draw(string? part, int? level)
    {
        if (PartsBreak.Value == "part-not-isolated") part = null;
        if (PartsBreak.Value == "levels-ignored") level = null;
        var (vertices, faces, colours, occurrences) = (new List<Vector3>(), new List<(int[], int[][])>(), new List<string?>(), new List<(string, int, int)>());
        var (min, max, live, liveFaces) = (new Vector3(float.MaxValue), new Vector3(float.MinValue), 0, 0);
        foreach (var p in RoverSpec.Tree)
        {
            if (part is not null && !Under(p, part)) continue;
            var source = p.InstanceOf ?? p.Name;
            if (!RoverSpec.GeometryParts.Contains(source)) continue;
            var mesh = level is { } n ? At(source, n) : _live[source];
            if (mesh is null) continue;
            var world = WorldOf(p.Name);
            var first = vertices.Count;
            for (var i = 0; i < mesh.Vertices.Length; i++)
            {
                var at = Vector3.Transform(mesh.Vertices[i], world);
                vertices.Add(at);
                if (!mesh.LiveVertex[i]) continue;
                (min, max) = (Vector3.Min(min, at), Vector3.Max(max, at));
            }

            occurrences.Add((p.Name, faces.Count, mesh.Faces.Length));
            for (var f = 0; f < mesh.Faces.Length; f++)
            {
                faces.Add((mesh.Faces[f].Select(v => v + first).ToArray(), []));
                colours.Add(mesh.Colours[f]);
            }

            (live, liveFaces) = (live + mesh.LiveVertexCount, liveFaces + mesh.LiveFaceCount);
        }

        return new Placed
        {
            Solid = new Solid([.. vertices], [.. faces]), Colours = [.. colours], Occurrences = occurrences,
            Min = min, Max = max, VertexCount = live, FaceCount = liveFaces,
        };
    }
}

/// <summary>The pixel judging every frame shares, the T-94 rules: the Cuts ray test, <see cref="ColourRule"/>, the shed's camera and its background.</summary>
internal static class PartsJudge
{
    public static Camera CameraFor(Placed placed, Vector3 direction, int size, bool orthographic = false, Vector3? up = null) =>
        new(placed.Min, placed.Max, direction, size, orthographic, up);

    private static bool Near((int R, int G, int B) p, (int R, int G, int B) q, int within) =>
        Math.Abs(p.R - q.R) <= within && Math.Abs(p.G - q.G) <= within && Math.Abs(p.B - q.B) <= within;

    private static (int R, int G, int B) Rgb((byte R, byte G, byte B, byte A) p) => (p.R, p.G, p.B);

    /// <summary>Where a tile's own caption is drawn, left out of every judgement.</summary>
    private static bool InCaption(int x, int y) => x < 48 && y < 32;

    /// <summary>Where the frame is read for expected pixel (x, y): the same, or left for right under the <c>mirror</c> break.</summary>
    private static (int X, int Y) Read(int size, int x, int y) => PartsBreak.Value == "mirror" ? (size - 1 - x, y) : (x, y);

    /// <summary>
    /// Every fourth pixel each way, inside the tile at (<paramref name="left"/>, <paramref name="top"/>) of <paramref name="size"/>, that the ray test is
    /// sure of: the background where it meets nothing, else its face's shade within the face's tolerance. Returns what was sure and what was wrong.
    /// </summary>
    public static (int Sure, List<string> Wrong) Judge(ScenarioFrame frame, Placed placed, Camera camera, int size, int left = 0, int top = 0)
    {
        var (sure, wrong) = (0, new List<string>());
        for (var y = 4; y < size - 4; y += 4)
        for (var x = 4; x < size - 4; x += 4)
        {
            if (InCaption(x, y)) continue;
            var seen = placed.Solid.Seen(camera, size, x, y);
            if (seen == -2) continue;
            sure++;
            var (px, py) = Read(size, x, y);
            var p = Rgb(frame.Pixel(left + px, top + py));
            var bad = seen == -1 ? !Checks.IsBackground(((byte)p.R, (byte)p.G, (byte)p.B, (byte)255)) : !Near(p, placed.Shade(seen, camera.Direction), placed.Tolerance(seen));
            if (bad) wrong.Add($"({x}, {y}) is {p}, expected {(seen == -1 ? "the background" : $"face {seen} colour {placed.Colours[seen]} {placed.Shade(seen, camera.Direction)}")}");
        }

        return (sure, wrong);
    }

    public static void AllRight(ScenarioFrame frame, Placed placed, Camera camera, string what, int least, int size, int left = 0, int top = 0)
    {
        var (sure, wrong) = Judge(frame, placed, camera, size, left, top);
        if (sure < least) Checks.Note($"{what}: the ray test is sure of {sure} grid pixels, fewer than {least}");
        if (wrong.Count > 0) Checks.Note($"{what}: {wrong.Count} of {sure} grid pixels wrong: {string.Join("; ", wrong.Take(6))}");
    }

    /// <summary>
    /// The frame must tell a mirrored or an upside-down camera from the right one. Of the grid pixels the ray test is sure of that the frame
    /// shows right, count those that have, at the pixel turned left for right (then top for bottom), a sure other shade (more than 6 away, or
    /// a face against the background). At least one of the two turns must tell <paramref name="least"/> pixels. The other must too, unless
    /// the drawn rover is itself that symmetric (the rover is symmetric in z, so its +x and +y views are the same turned over one way):
    /// then it tells almost none, at most <see cref="Symmetric"/>.
    /// </summary>
    public static void TellsMirrorAndUp(ScenarioFrame frame, Placed placed, Camera camera, string what, int least, int size, int left = 0, int top = 0)
    {
        var told = new[] { true, false }.Select(turned => Told(frame, placed, camera, size, left, top, turned)).ToArray();
        var note = $"{what}: mirrored {told[0]}, upside down {told[1]} grid pixels would change";
        if (told.Max() < least) Checks.Note($"{note}, fewer than {least} either way: the frame could be either way");
        else if (told.Any(t => t >= Symmetric && t < least)) Checks.Note($"{note}: one turn tells fewer than {least} yet more than the {Symmetric} of a symmetry");
        Console.Error.WriteLine($"parts: {note}");
    }

    /// <summary>The most grid pixels a turn may change and still be a symmetry of the drawn rover (shade noise on edges).</summary>
    public const int Symmetric = 100;

    private static int Told(ScenarioFrame frame, Placed placed, Camera camera, int size, int left, int top, bool turned)
    {
        var told = 0;
        for (var y = 4; y < size - 4; y += 4)
        for (var x = 4; x < size - 4; x += 4)
        {
            if (InCaption(x, y)) continue;
            var seen = placed.Solid.Seen(camera, size, x, y);
            if (seen < 0) continue;
            var (px, py) = Read(size, x, y);
            var shade = placed.Shade(seen, camera.Direction);
            if (!Near(Rgb(frame.Pixel(left + px, top + py)), shade, placed.Tolerance(seen))) continue;
            var (mx, my) = turned ? (size - 1 - x, y) : (x, size - 1 - y);
            if (InCaption(mx, my)) continue;
            var there = placed.Solid.Seen(camera, size, mx, my);
            if (there == -2) continue;
            var other = there == -1 ? (48, 52, 56) : placed.Shade(there, camera.Direction);
            if (!Near(other, shade, 6)) told++;
        }

        return told;
    }
}
