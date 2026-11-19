extern alias carve;
using System.Numerics;
using System.Text.Json;
using Carve.Core;
using Carve.Rover;
using Carve.Scenario.Crate;
using Carve.Scenario.Cuts;
using Carve.Scenario.Shed;
using StbImageSharp;

namespace Carve.Server.Tests;

/// <summary>
/// T-94: the rover (<see cref="RoverSpec"/>, built through the tools by <see cref="RoverTools"/>) drawn by the real
/// <c>carve</c>'s <c>render</c> as a part tree: whole at live and at level 0, the <c>Axle_F</c> subtree from [1, 0.3, 0],
/// and <c>Wheel_FL</c> with and without its face labels. Every pixel is judged by the shared rules named at M-9's
/// planning: the Cuts ray test (<see cref="Solid"/>, with <see cref="Solid.SeenNarrow"/> for the thin faces) on
/// <see cref="Model.CombinedOf"/>, which is world-placed, under the shed's camera (<see cref="Camera"/>, render's framing
/// rule) on the drawn bounds, and the Crate's <see cref="ColourRule"/> for a face's shade, at its tolerance of 2. The
/// expected model is the same rover built in this process (<see cref="ToolHarness"/>), so the product's draw is judged
/// against the core's placement, not against itself.
/// </summary>
[Trait("Tier", "regression")]
public sealed class RoverRenderTests() : FileToolTests("carve-rover-render")
{
    private const int Size = 512;

    /// <summary>ColourRule's tolerance (the person's ruling of 2026-11-14: "tolerance stays 2").</summary>
    private const int Within = 2;

    // Tolerance 4 until B-8: ColourRule predicts low-albedo faces too dark. The person's ruling of 2026-11-18: 4 for the
    // wheels' #3C3C3C faces only (measured 3 to 4 brighter than ColourRule.Lit on two of their normals), 2 everywhere else.
    private const int DarkWheelWithin = 4;

    /// <summary>The grid of judged pixels: every fourth pixel each way.</summary>
    private const int Step = 4;

    private static readonly string Frames =
        Directory.CreateDirectory(Path.Combine(Paths.RepoRoot, "artifacts", "rover-render-tests")).FullName;

    private static readonly Vector3 Front = ViewFraming.NamedViews["front"];
    /// <summary>The least count of sure inner-cap pixels for each wheel in the Axle_F frame (3 325 each measured, 2026-11-18).</summary>
    private const int InnerCapLeast = 3000;

    private static readonly Vector3 AxleView = new(1, 0.3f, 0);

    /// <summary>What a frame should show, worked from the core: the ray test's solid, the camera, and each face's shade.</summary>
    private sealed class Expected
    {
        public required Model Model { get; init; }
        public required string? Part { get; init; }
        public required int? Level { get; init; }
        public required Solid Solid { get; init; }
        public required Camera Camera { get; init; }
        public required PolygonMesh Mesh { get; init; }

        /// <summary>A face's pixel in 8 bit sRGB from <see cref="ColourRule.Lit(string, Vector3, Vector3)"/>; an uncoloured face is <see cref="ColourRule.Grey"/>.</summary>
        public (int R, int G, int B) Shade(int face)
        {
            var normal = Solid.Normal(face);
            var lit = Mesh.Style?.ColourOf(face) is { } hex
                ? ColourRule.Lit(hex, normal, Camera.Direction)
                : ColourRule.Lit(ColourRule.Grey.X, ColourRule.Grey.Y, ColourRule.Grey.Z, normal, Camera.Direction);
            return (ColourRule.Encode(lit[0]), ColourRule.Encode(lit[1]), ColourRule.Encode(lit[2]));
        }

        /// <summary>
        /// How near a face's pixel must be to <see cref="Shade"/>: <see cref="DarkWheelWithin"/> for the wheels' dark faces
        /// (the rover's only #3C3C3C faces, RoverSpec), <see cref="Within"/> for every other.
        /// </summary>
        public int Tolerance(int face) => Mesh.Style?.ColourOf(face) == RoverSpec.DarkGrey ? DarkWheelWithin : Within;

        /// <summary>The faces of <paramref name="name"/>'s own occurrence in this draw (an instance's are its copy of the source's).</summary>
        public Range FacesOf(string name)
        {
            var at = Model.OffsetOf(name, Part, Level) ?? throw new InvalidOperationException($"{name} is not drawn");
            var source = Model.FindPart(name)?.InstanceOf ?? name;
            var count = (Level is { } n ? Model.LevelMesh(source, n) : Model.Meshes.First(m => m.Name == source).Mesh)!.Faces.Count;
            return new Range(at.Faces, at.Faces + count);
        }
    }

    private static Expected Expect(Model model, string? part, int? level, Vector3 direction)
    {
        var mesh = model.CombinedOf(part, level) ?? throw new InvalidOperationException("nothing is drawn");
        var live = mesh.LiveVertices.Select(v => mesh.Vertices[v]).ToArray();
        var (min, max) = (live.Aggregate(Vector3.Min), live.Aggregate(Vector3.Max));
        var vertices = Enumerable.Range(0, mesh.Vertices.Count).Select(v => mesh.IsVertexDeleted(v) ? Vector3.Zero : mesh.Vertices[v]).ToArray();
        var faces = mesh.Faces
            .Select(f => f.IsDeleted ? (Array.Empty<int>(), Array.Empty<int[]>()) : (f.Loop.ToArray(), f.Holes.Select(h => h.ToArray()).ToArray()))
            .ToArray();
        return new Expected
        {
            Model = model, Part = part, Level = level, Mesh = mesh,
            Solid = new Solid(vertices, faces), Camera = new Camera(min, max, direction, Size),
        };
    }

    private sealed record Frame(ImageResult Image, string Caption)
    {
        public (int R, int G, int B) At(int x, int y)
        {
            var i = (y * Image.Width + x) * 4;
            return (Image.Data[i], Image.Data[i + 1], Image.Data[i + 2]);
        }
    }

    private static Frame Render(JsonElement result, string keepAs)
    {
        if (IsError(result)) Assert.Fail(TextOf(result));
        var content = result.GetProperty("content");
        var png = Convert.FromBase64String(content[0].GetProperty("data").GetString()!);
        File.WriteAllBytes(Path.Combine(Frames, keepAs + ".png"), png);
        var caption = TextOf(result, 1);
        File.WriteAllText(Path.Combine(Frames, keepAs + ".txt"), caption);
        var image = ImageResult.FromMemory(png, ColorComponents.RedGreenBlueAlpha);
        Assert.Equal(Size, image.Width);
        Assert.Equal(Size, image.Height);
        return new Frame(image, caption);
    }

    /// <summary>The dark background (the shed's rule).</summary>
    private static bool IsBackground((int R, int G, int B) p) => p.R is >= 47 and <= 49 && p.G is >= 51 and <= 53 && p.B is >= 55 and <= 57;

    private static bool Near((int R, int G, int B) p, (int R, int G, int B) q, int within = Within) =>
        Math.Abs(p.R - q.R) <= within && Math.Abs(p.G - q.G) <= within && Math.Abs(p.B - q.B) <= within;

    /// <summary>
    /// Every grid pixel the ray test is sure of: the background where it meets nothing, else its face's shade within its
    /// tolerance (<see cref="Expected.Tolerance"/>).
    /// Returns how many it was sure of and what was wrong.
    /// </summary>
    private static (int Sure, List<string> Wrong) Judge(Expected expected, Func<int, int, (int R, int G, int B)> pixel)
    {
        var (sure, wrong) = (0, new List<string>());
        for (var y = 4; y < Size - 4; y += Step)
        for (var x = 4; x < Size - 4; x += Step)
        {
            var seen = expected.Solid.Seen(expected.Camera, Size, x, y);
            if (seen == -2) continue;
            sure++;
            var p = pixel(x, y);
            if (seen == -1 ? !IsBackground(p) : !Near(p, expected.Shade(seen), expected.Tolerance(seen)))
                wrong.Add($"({x}, {y}) is {p}, expected {(seen == -1 ? "the background" : $"face {seen} n {expected.Solid.Normal(seen):F2} colour {expected.Mesh.Style?.ColourOf(seen)} {expected.Shade(seen)}")}");
        }

        return (sure, wrong);
    }

    private static void AllRight(Expected expected, Frame frame, string name, int least)
    {
        var (sure, wrong) = Judge(expected, frame.At);
        Assert.True(sure >= least, $"{name}: the ray test is sure of {sure} grid pixels, fewer than {least}");
        Assert.True(wrong.Count == 0, $"{name}: {wrong.Count} of {sure} grid pixels wrong: {string.Join("; ", wrong.Take(8))}");
    }

    /// <summary>
    /// The probe at <paramref name="at"/> is sure (SeenNarrow) to show a face of <paramref name="part"/>'s occurrence, and the
    /// frame shows its shade there. The same probe's pixel turned left for right, or top for bottom, about the framing's
    /// centre shows something of another shade, so a mirrored or upside-down camera fails it.
    /// </summary>
    private static void Probe(Expected expected, Frame frame, string part, Vector3 at, string what)
    {
        var (x, y) = expected.Camera.Pixel(at);
        var seen = expected.Solid.SeenNarrow(expected.Camera, Size, x, y);
        Assert.True(In(expected.FacesOf(part), seen), $"{what}: the ray test at ({x}, {y}) gives face {seen}, not one of {part}'s");
        var shade = expected.Shade(seen);
        Assert.True(Near(frame.At(x, y), shade, expected.Tolerance(seen)), $"{what} at ({x}, {y}) is {frame.At(x, y)}, expected {shade}");

        foreach (var (mx, my, how) in new[] { (Size - 1 - x, y, "mirrored"), (x, Size - 1 - y, "upside down") })
        {
            var there = expected.Solid.SeenNarrow(expected.Camera, Size, mx, my);
            Assert.True(there != -2, $"{what}: the {how} pixel ({mx}, {my}) is not sure");
            var other = there == -1 ? frame.At(4, 4) : expected.Shade(there);
            Assert.False(Near(other, shade, 6), $"{what}: a {how} camera would show {other} at ({x}, {y}), too near {shade} to tell");
        }
    }

    private static bool In(Range range, int face) => face >= range.Start.Value && face < range.End.Value;

    private async Task<(CarveProcess Carve, Model Model)> RoverAsync()
    {
        var (harness, _) = await StartAsync(mesh: null);
        await using (harness)
        {
            await RoverTools.BuildAsync(harness.CallToolAsync);
            var model = harness.Store.Current.Model;
            var (carve, _) = await StartProcessAsync(mesh: null);
            await RoverTools.BuildAsync(carve.CallToolAsync);
            return (carve, model);
        }
    }

    [Fact]
    public async Task The_rover_draws_every_part_where_it_is_placed_live_and_at_level_0()
    {
        var (carve, model) = await RoverAsync();
        using var _ = carve;

        var live = Expect(model, null, null, Front);
        var frame = Render(await carve.CallToolAsync("render", "{}"), "rover-front-live");
        Assert.EndsWith($"{model.Combined!.LiveVertexCount} vertices, {model.Combined.LiveFaceCount} faces", frame.Caption);
        AllRight(live, frame, "rover-front-live", 4000);
        // The cab at +X, right of centre (its top's far corner, well off the view's centre line, so the mirrored pixel is
        // not the cab again: from the front corner the cab's +Z side sits on the centre line); the mast at -X; the lamp up on the mast. From the framing's bounds.
        Probe(live, frame, "Cab", new Vector3(0.75f, 1.4f, -0.35f), "the cab's top");
        Probe(live, frame, "Mast", new Vector3(-0.6f, 1.2f, 0.05f), "the mast post");
        // The rear left wheel's boss (an instance of Wheel_FL, at -X): the front left boss sits on the view's centre line,
        // where a mirrored camera shows the same boss, so the probe is the rear one, far left of centre.
        Probe(live, frame, "Wheel_RL", new Vector3(-0.7f, 0.4f, 0.87f), "the rear left hub boss");

        var blockout = Expect(model, null, 0, Front);
        var level0 = Render(await carve.CallToolAsync("render", """{"level":0}"""), "rover-front-level-0");
        Assert.Contains("view front, level 0, ", level0.Caption);
        AllRight(blockout, level0, "rover-front-level-0", 4000);
        Probe(blockout, level0, "Cab", new Vector3(0.75f, 1.4f, -0.35f), "level 0: the cab's top");
        Probe(blockout, level0, "Mast", new Vector3(-0.6f, 1.2f, 0.05f), "level 0: the mast post");
        // Level 0's wheel has no boss: the hub cap is flat and dark (the rear one, for the reason above).
        Probe(blockout, level0, "Wheel_RL", new Vector3(-0.66f, 0.46f, 0.82f), "level 0: the rear left hub cap");
    }

    [Fact]
    public async Task Part_Axle_F_draws_its_subtree_on_its_own_bounds_with_both_bosses_outward()
    {
        var (carve, model) = await RoverAsync();
        using var _ = carve;

        var axle = Expect(model, "Axle_F", null, AxleView);
        var frame = Render(await carve.CallToolAsync("render", """{"part":"Axle_F","direction":[1,0.3,0]}"""), "axle-f");
        Assert.Contains("part Axle_F, ", frame.Caption);
        AllRight(axle, frame, "axle-f", 2000);

        // The person's ruling of 2026-11-18: from [1, 0.3, 0] no boss is in sight (each stands on its wheel's outer cap,
        // seen edge-on), so "both bosses outward" is checked as both wheels' inner caps seen with no boss on them. Every
        // pixel the ray test is sure (SeenNarrow) shows a wheel's inner cap is drawn in that cap's dark shade, not the
        // light boss, and there are at least InnerCapLeast for each wheel; a boss turned inward, or an instance drawn
        // unturned, stands on the inner cap and fails.
        File.Delete(Path.Combine(Frames, "axle-f-inner-caps.txt"));
        foreach (var wheel in new[] { "Wheel_FL", "Wheel_FR" })
        {
            var cap = axle.FacesOf(wheel).Start.Value + RoverSpec.WheelInnerCap;
            var (shown, wrong) = (0, new List<string>());
            for (var y = 4; y < Size - 4; y++)
            for (var x = 4; x < Size - 4; x++)
            {
                if (axle.Solid.SeenNarrow(axle.Camera, Size, x, y) != cap) continue;
                shown++;
                if (!Near(frame.At(x, y), axle.Shade(cap), axle.Tolerance(cap))) wrong.Add($"({x}, {y}) is {frame.At(x, y)}, expected face {cap} {axle.Shade(cap)}");
            }

            File.AppendAllText(Path.Combine(Frames, "axle-f-inner-caps.txt"), $"{wheel} inner cap (face {cap}): {shown} sure pixels, {wrong.Count} wrong\n");
            Assert.True(shown >= InnerCapLeast, $"{wheel}: only {shown} pixels are sure to show its inner cap, fewer than {InnerCapLeast}");
            Assert.True(wrong.Count == 0, $"{wheel}'s inner cap: {wrong.Count} of {shown} wrong: {string.Join("; ", wrong.Take(6))}");
        }
    }

    [Fact]
    public async Task Part_Wheel_FL_draws_alone_and_labels_its_own_faces()
    {
        var (carve, model) = await RoverAsync();
        using var _ = carve;

        var wheel = Expect(model, "Wheel_FL", null, Front);
        var plain = Render(await carve.CallToolAsync("render", """{"part":"Wheel_FL"}"""), "wheel-fl");
        Assert.Contains("part Wheel_FL, ", plain.Caption);
        AllRight(wheel, plain, "wheel-fl", 1500);

        var labelled = Render(await carve.CallToolAsync("render", """{"part":"Wheel_FL","labels":"faces"}"""), "wheel-fl-labels-faces");
        Assert.Contains("labels faces", labelled.Caption);
        Assert.EndsWith($"{wheel.Mesh.LiveVertexCount} vertices, {wheel.Mesh.LiveFaceCount} faces", labelled.Caption);
        // The labels only add: every grid pixel the labelled frame gets wrong differs from the plain frame there, and some do.
        var (_, wrong) = Judge(wheel, labelled.At);
        var (_, plainWrong) = Judge(wheel, plain.At);
        Assert.Empty(plainWrong);
        Assert.NotEmpty(wrong);
        var added = 0;
        for (var y = 0; y < Size; y++)
        for (var x = 0; x < Size; x++)
            if (labelled.At(x, y) != plain.At(x, y)) added++;
        Assert.True(added > 200, $"only {added} pixels differ from the plain frame: no labels drawn");
    }
}
