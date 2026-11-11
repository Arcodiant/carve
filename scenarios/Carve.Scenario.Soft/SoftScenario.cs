using System.Numerics;
using System.Text.Json;
using Carve.Debug;
using Carve.Extensibility;
using Carve.Scenario.Character;
using Carve.Scenario.Crate;
using Microsoft.Extensions.DependencyInjection;
using static Carve.Scenario.Crate.CameraPixels;
using static Carve.Scenario.Crate.CrateBuild;
using static Carve.Scenario.Character.GardenerPixels;

namespace Carve.Scenario.Soft;

/// <summary>Registers the scenario, as every scenario extension does.</summary>
public sealed class SoftExtension : ICarveExtension
{
    public void ConfigureHost(CarveHostBuilderContext context) => context.Services.AddSingleton<IScenario, SoftScenario>();

    public void OnStarted(IServiceProvider services)
    {
    }
}

/// <summary>
/// The gardener of M-16 made soft (M-18): built flat through the tools from <see cref="GardenerSpec"/> and shown, the brim of his
/// hat creased with <c>mark_edges</c>, then both meshes subdivided one level and smoothed, and shown again at rest, in a
/// clip and close on the hat, with the brim uncreased for one frame. Saved as <c>work/gardener-soft.carve</c> (the sample
/// <c>samples/gardener-soft.carve</c> is kept equal to it by content) and exported flat and soft into <c>game/Content/models/</c>
/// of the run folder, which <c>tests/Carve.SoftLoad.Tests</c> then reads as a game would.
/// </summary>
public sealed class SoftScenario : IScenario
{
    private const int Size = 512;
    private static readonly string[] BodyGroups = ["skin", "shirt", "trousers", "boots", "can"];
    private static readonly string[] HatGroups = ["straw", "band"];

    /// <summary>The hat close: from the front and the left of the character (the image's right), above the brim.</summary>
    private static readonly Vector3 HatDirection = new(0.5f, 0.8f, 1f);

    private static readonly Vector3 HatTarget = new(0, 1.38f, 0);
    private const float HatZoom = 2.5f;

    public async Task RunAsync(ScenarioDriver driver)
    {
        var work = Path.Combine(driver.RunFolder, "work");
        var models = Path.Combine(driver.RunFolder, "game", "Content", "models");
        foreach (var owned in new[] { work, Path.Combine(driver.RunFolder, "game") })
        {
            if (Directory.Exists(owned))
            {
                Directory.Delete(owned, recursive: true);
            }
        }

        var (body, hat) = GardenerSpec.Build();
        await BuildAsync(driver, body, hat);
        var (flatMin, flatMax) = GardenerSpec.RestBounds();

        // ---- flat: the gardener as T-36 built him ----
        var flat = new Looks(await Capture(driver, "soft-flat-rest-front", new { view = "+z" }),
            await Capture(driver, "soft-flat-walk-0.125-side", new { view = "+x", clip = "Walk", time = 0.125 }),
            await Capture(driver, "soft-flat-hat-close", HatView));
        var flatExport = await CallTextAsync(driver, "export", new { path = Path.Combine(models, "gardener-flat.glb") });
        await File.WriteAllTextAsync(Path.Combine(driver.RunFolder, "export-flat-result.txt"), flatExport);

        // ---- the brim creased, then both meshes subdivided one level and smoothed ----
        var brim = Enumerable.Range(0, 8).SelectMany(j => new[] { new[] { j, (j + 1) % 8 }, [8 + j, 8 + (j + 1) % 8] }).ToArray();
        var marked = await CallJsonAsync(driver, "mark_edges", new { mesh = "Hat", edges = brim });
        if (marked.GetProperty("hard_edges").GetArrayLength() != 16)
        {
            driver.Fail($"mark_edges: expected the 16 edges of the brim's two rims hard, got {marked}");
        }

        var bounds = new List<(Vector3 Min, Vector3 Max)>();
        var triangles = 0;
        var hardEdges = "";
        foreach (var name in new[] { "Body", "Hat" })
        {
            var sub = await CallJsonAsync(driver, "subdivide", new { mesh = name, levels = 1 });
            bounds.Add((Vec(sub.GetProperty("bounds").GetProperty("min")), Vec(sub.GetProperty("bounds").GetProperty("max"))));
            triangles = sub.GetProperty("triangles").GetInt32();
            var shaded = await CallJsonAsync(driver, "set_shading", new { mesh = name, shading = "smooth" });
            if (name == "Hat")
            {
                hardEdges = shaded.GetProperty("hard_edges").GetRawText();
            }
        }

        if (triangles > 5000 || triangles <= 680 || hardEdges == "[]")
        {
            driver.Fail($"subdivide: {triangles} triangles (expected more than 680 and within 5000), hat hard edges {hardEdges}");
        }

        var (softMin, softMax) = (Vector3.Min(bounds[0].Min, bounds[1].Min), Vector3.Max(bounds[0].Max, bounds[1].Max));
        await File.WriteAllTextAsync(Path.Combine(driver.RunFolder, "triangles.txt"), $"{triangles}\n");

        var soft = new Looks(await Capture(driver, "soft-smooth-rest-front", new { view = "+z" }),
            await Capture(driver, "soft-smooth-walk-0.125-side", new { view = "+x", clip = "Walk", time = 0.125 }),
            await Capture(driver, "soft-smooth-hat-close", HatView));

        // The brim uncreased for one frame, then creased again with the very edges set_shading listed.
        var edgeList = JsonSerializer.Deserialize<int[][]>(hardEdges)!;
        await CallJsonAsync(driver, "mark_edges", new { mesh = "Hat", edges = edgeList, hard = false });
        var uncreased = await Capture(driver, "soft-smooth-hat-close-uncreased", HatView);
        await CallJsonAsync(driver, "mark_edges", new { mesh = "Hat", edges = edgeList, hard = true });
        var again = await Capture(driver, "soft-smooth-hat-close-recreased", HatView);
        driver.AssertBytesEqual("the hat close after the brim was uncreased and creased again", soft.Hat.Pixels, again.Pixels);

        // ---- what each frame must show ----
        foreach (var (look, frames, min, max) in new[] { ("flat", flat, flatMin, flatMax), ("soft", soft, softMin, softMax) })
        {
            var front = Perspective(min, max, Vector3.UnitZ, Size);
            var side = Perspective(min, max, Vector3.UnitX, Size);
            var close = Perspective(min, max, HatDirection, Size, HatZoom, HatTarget);
            CheckCorners(driver, frames.Rest);
            CheckCorners(driver, frames.Walk);
            CheckCorners(driver, frames.Hat);
            CheckRest(driver, frames.Rest, front, body, hat);
            CheckTorso(driver, frames.Rest, front, look == "flat");
            CheckWalk(driver, frames.Walk, side);
            CheckHat(driver, frames.Hat, close, creased: true);
            CheckCanInHatClose(driver, frames.Hat, close);
            if (look == "soft")
            {
                CheckCorners(driver, uncreased);
                CheckCanInHatClose(driver, uncreased, close);
                CheckHat(driver, uncreased, close, creased: false);
            }
        }

        foreach (var (a, b) in new[] { (flat.Rest, soft.Rest), (flat.Walk, soft.Walk), (flat.Hat, soft.Hat), (soft.Hat, uncreased) })
        {
            if (a.Pixels.SequenceEqual(b.Pixels))
            {
                driver.Fail($"{a.Name} is the same as {b.Name}");
            }
        }

        // ---- save, reopen, export ----
        var path = Path.Combine(work, "gardener-soft.carve");
        var meshes = new[] { "Body", "Hat" };
        var before = new List<string>();
        foreach (var m in meshes)
        {
            before.Add(await CallTextAsync(driver, "get_mesh", new { mesh = m }));
        }

        await CallTextAsync(driver, "save", new { path });
        var bytes = await File.ReadAllBytesAsync(path);
        await CallJsonAsync(driver, "define_mesh", new { name = "Body", vertices = new[] { new[] { 0f, 0, 0 }, [1f, 0, 0], [0f, 1, 0] }, faces = new[] { new[] { 0, 1, 2 } } });
        var opened = await CallJsonAsync(driver, "open", new { path });
        if (opened.GetProperty("bones").GetInt32() != 22 || opened.GetProperty("clips").GetArrayLength() != 2)
        {
            driver.Fail($"open: expected 22 bones and 2 clips, got {opened}");
        }

        for (var i = 0; i < meshes.Length; i++)
        {
            if (await CallTextAsync(driver, "get_mesh", new { mesh = meshes[i] }) != before[i])
            {
                driver.Fail($"open: get_mesh of {meshes[i]} differs from the one taken before the save");
            }
        }

        driver.AssertBytesEqual("soft rest, after save and open (shape, colours and smooth shading came back)", soft.Rest.Pixels, (await Capture(driver, "soft-smooth-rest-front-reopened", new { view = "+z" })).Pixels);
        driver.AssertBytesEqual("soft hat close, after save and open (the creased brim came back)", soft.Hat.Pixels, (await Capture(driver, "soft-smooth-hat-close-reopened", HatView)).Pixels);
        driver.AssertBytesEqual("soft Walk at 0.125 s, after save and open (the weights came back)", soft.Walk.Pixels, (await Capture(driver, "soft-smooth-walk-0.125-side-reopened", new { view = "+x", clip = "Walk", time = 0.125 })).Pixels);
        await CallTextAsync(driver, "save", new { path });
        driver.AssertBytesEqual("gardener-soft.carve, saved again after open", bytes, await File.ReadAllBytesAsync(path));

        var exported = await CallTextAsync(driver, "export", new { path = Path.Combine(models, "gardener-soft.glb") });
        await File.WriteAllTextAsync(Path.Combine(driver.RunFolder, "export-result.txt"), exported);
        if (!exported.Contains("22 bone(s)") || !exported.Contains("2 clip(s)") || !exported.Contains($"{triangles} triangles"))
        {
            driver.Fail($"export: expected 22 bones, 2 clips and {triangles} triangles, got {exported}");
        }

        // Every warning is a face that is not planar (what subdivide makes of a quad, which the export still lists face by face); a limit
        // (triangles, primitives, height) must not be one. Their count is select's non_planar faces over both meshes, so a
        // regression that adds some is caught (the person, 2026-11-10, until T-72 lands). select's results are kept in the run
        // folder for SoftLoad.Tests.
        var nonPlanar = 0;
        foreach (var name in new[] { "Body", "Hat" })
        {
            var selected = await CallTextAsync(driver, "select", new { kind = "faces", where = "non_planar", mesh = name });
            await File.WriteAllTextAsync(Path.Combine(driver.RunFolder, $"select-non-planar-{name}.txt"), selected);
            nonPlanar += JsonDocument.Parse(selected).RootElement.GetProperty("count").GetInt32();
        }
        var warned = exported.IndexOf("warning(s): ", StringComparison.Ordinal);
        var warnings = warned < 0 ? [] : exported[(warned + "warning(s): ".Length)..].Split("; ");
        if (warnings.Any(w => !w.Contains("face is not planar")))
        {
            driver.Fail($"export: a warning that is not about a face being non-planar: {warnings.First(w => !w.Contains("face is not planar"))}");
        }
        if (nonPlanar == 0 || warnings.Length != nonPlanar)
        {
            driver.Fail($"export: expected one warning per face select finds non_planar ({nonPlanar}), got {warnings.Length}");
        }

        if (flatExport.Contains("warning(s)"))
        {
            driver.Fail($"export of the flat gardener: expected no warnings, got {flatExport}");
        }
    }

    private sealed record Looks(ScenarioFrame Rest, ScenarioFrame Walk, ScenarioFrame Hat);

    private static object HatView => new { direction = new[] { HatDirection.X, HatDirection.Y, HatDirection.Z }, zoom = HatZoom, target = new[] { HatTarget.X, HatTarget.Y, HatTarget.Z } };

    private static Task<ScenarioFrame> Capture(ScenarioDriver driver, string name, object arguments) => driver.CaptureAsync(name, arguments);

    private static Vector3 Vec(JsonElement e) => new(e[0].GetSingle(), e[1].GetSingle(), e[2].GetSingle());

    // ---- pixel checks ----

    private static int Luma(ScenarioFrame frame, int x, int y)
    {
        var p = frame.Pixel(x, y);
        return (p.R * 299 + p.G * 587 + p.B * 114) / 1000;
    }

    /// <summary>The centre of a front face of each colour reads as that colour; the can (at -X) is on the image's left, and a frame mirrored in X would put it on the right.</summary>
    private static void CheckRest(ScenarioDriver driver, ScenarioFrame frame, Func<Vector3, Vector2> project, GardenerSpec.MeshSpec body, GardenerSpec.MeshSpec hat)
    {
        var targets = new (GardenerSpec.MeshSpec Mesh, string Group, Vector3 Near)[]
        {
            (body, "shirt", new Vector3(0, 0.85f, 0.3f)), (body, "trousers", new Vector3(0, 0.57f, 0.3f)), (body, "skin", new Vector3(0.07f, 1.27f, 0.3f)),
            (body, "boots", new Vector3(0.09f, 0.05f, 0.4f)), (body, "can", new Vector3(-0.435f, 0.39f, 0.30f)),
            (hat, "straw", new Vector3(0, 1.485f, 0.3f)), (hat, "band", new Vector3(0, 1.43f, 0.3f)),
        };
        foreach (var (mesh, group, near) in targets)
        {
            var c = mesh.Centroid(mesh.NearestFace(group, near));
            var (x, y) = Holding(project(c));
            ExpectGroup(driver, frame, x, y, group, $"the {group} face at {c}");
        }

        var can = body.Centroid(body.NearestFace("can", new Vector3(-0.435f, 0.39f, 0.30f)));
        var (cx, cy) = Holding(project(can));
        if (cx >= Size / 2)
        {
            driver.Fail($"{frame.Name}: the can (at -X) is at x {cx}, on the image's right half; a mirrored frame draws it there");
        }

        ExpectNot(driver, frame, Size - 1 - cx, cy, "can", "where a frame mirrored in X would show the can");
        var (mx, my) = Holding(project(new Vector3(-can.X, can.Y, can.Z)));
        ExpectNot(driver, frame, mx, my, "can", "the can's mirror in the world (+X)");
    }

    /// <summary>
    /// The torso, from the front, at the height 0.80 m: its octagon's two side facet edges (world x of plus and minus 0.1687). Flat, the
    /// shade steps across each (30 and 31 on a 6 pixel span); soft, it runs on across them (5 to 9 on the same span).
    /// </summary>
    private static void CheckTorso(ScenarioDriver driver, ScenarioFrame frame, Func<Vector3, Vector2> project, bool flat)
    {
        foreach (var sign in new[] { -1f, 1f })
        {
            var (x, y) = Holding(project(new Vector3(sign * 0.1687f, 0.80f, 0.1687f)));
            ExpectGroup(driver, frame, x - 3, y, "shirt", "the torso, left of a facet edge");
            ExpectGroup(driver, frame, x + 3, y, "shirt", "the torso, right of a facet edge");
            var step = Math.Abs(Luma(frame, x + 3, y) - Luma(frame, x - 3, y));
            if (flat ? step < 20 : step > 12)
            {
                driver.Fail($"{frame.Name}: across the torso's facet edge at ({x}, {y}) the shade steps by {step}; {(flat ? "a flat mesh steps by 20 or more" : "a smooth one runs on, by 12 or less")}");
            }
        }

        // Between the facet edge and the lit centre the shade is a ramp when smooth, however many facets the mesh has (a run of equal
        // shades of at most 5 pixels), and a plateau per facet when not (flat: 17 or more; flat and subdivided: about 11).
        var (from, row) = Holding(project(new Vector3(-0.16f, 0.80f, 0.17f)));
        var (to, _) = Holding(project(new Vector3(-0.08f, 0.80f, 0.17f)));
        int run = 1, longest = 1;
        for (var x = from + 1; x <= to; x++)
        {
            run = Luma(frame, x, row) == Luma(frame, x - 1, row) ? run + 1 : 1;
            longest = Math.Max(longest, run);
        }

        if (flat ? longest < 10 : longest > 5)
        {
            driver.Fail($"{frame.Name}: the torso's shade over x {from} to {to} at row {row} has a run of {longest} equal pixels; {(flat ? "a flat mesh holds one shade per facet (10 or more)" : "a smooth one is a ramp (5 or fewer)")}");
        }
    }

    /// <summary>The near (left) hand at 0.125 s, worked from the clip's keys, is skin, and its image mirror is not.</summary>
    private static void CheckWalk(ScenarioDriver driver, ScenarioFrame frame, Func<Vector3, Vector2> project)
    {
        Func<string, (Quaternion?, Vector3?)> locals = b => GardenerSpec.Walk.Sample(b, 0.125f);
        var hand = GardenerSpec.Posed(new Vector3(0.37f, 0.60f, 0), "Hand_L", locals);
        var (hx, hy) = Holding(project(hand));
        ExpectGroup(driver, frame, hx, hy, "skin", $"Walk at 0.125 s: the near hand at {hand}");
        ExpectNot(driver, frame, Size - 1 - hx, hy, "skin", "Walk at 0.125 s: the hand's mirror in the image");
    }

    private static void CheckCanInHatClose(ScenarioDriver driver, ScenarioFrame frame, Func<Vector3, Vector2> project)
    {
        var (x, y) = Holding(project(new Vector3(-0.43f, 0.48f, 0.02f)));
        ExpectGroup(driver, frame, x, y, "can", "the top of the watering can (at -X)");
        ExpectNot(driver, frame, Size - 1 - x, y, "can", "where a frame mirrored in X would show the can");
    }

    /// <summary>
    /// The brim's top face, in front of the crown. Creased, its shade holds as far as the rim (the same at 0.18 and 0.226 m from the axis, a
    /// hundredth short of the rim); uncreased, it rolls off toward the rim and no step down the rim is as big.
    /// </summary>
    private static void CheckHat(ScenarioDriver driver, ScenarioFrame frame, Func<Vector3, Vector2> project, bool creased)
    {
        var (ix, iy) = Holding(project(new Vector3(0.06f, 1.355f, 0.18f)));
        var (ox, oy) = Holding(project(new Vector3(0.06f, 1.355f, 0.226f)));
        ExpectGroup(driver, frame, ix, iy, "straw", "the brim's top, inside");
        ExpectGroup(driver, frame, ox, oy, "straw", "the brim's top, near the rim");
        var (inner, outer) = (Luma(frame, ix, iy), Luma(frame, ox, oy));
        if (creased && Math.Abs(inner - outer) > 2)
        {
            driver.Fail($"{frame.Name}: the creased brim's top is {inner} inside and {outer} near the rim; it must hold its shade (within 2)");
        }

        // Down the image from the brim's top: the wall under the rim is lit by its own normal (darker). Creased, the shade breaks there
        // by a step of one pixel; uncreased, it rolls off over the pixels either side of the rim and no step is as big.
        var (x, y) = (ox, oy);
        var step = 0;
        var profile = new List<int> { Luma(frame, ox, oy) };
        while (y + 1 < frame.Height && Classify(frame, x, y + 1).Group == "straw")
        {
            step = Math.Max(step, Math.Abs(Luma(frame, x, y + 1) - Luma(frame, x, y)));
            y++;
            profile.Add(Luma(frame, x, y));
        }

        if (y - oy < 3)
        {
            driver.Fail($"{frame.Name}: only {y - oy} straw pixels below the brim's top at ({ox}, {oy}), expected the wall under the rim");
        }

        // The first step of 10 or more: one pixel's jump when creased (28 flat; the wall's own shade), a stage of the roll-off when not (12).
        var first = Enumerable.Range(1, profile.Count - 1).Select(k => Math.Abs(profile[k] - profile[k - 1])).FirstOrDefault(d => d >= 10);
        if (creased ? first < 20 : first >= 20)
        {
            driver.Fail($"{frame.Name}: down the rim from ({ox}, {oy}) the shades are {string.Join(" ", profile)} (the first step of 10 or more is {first}); {(creased ? "a creased rim breaks at once, by 20 or more" : "an uncreased rim rolls off, by less than 20 at a step")}");
        }
    }
    // ---- build, through the tools (as T-36's scenario built it) ----

    private static async Task BuildAsync(ScenarioDriver driver, GardenerSpec.MeshSpec body, GardenerSpec.MeshSpec hat)
    {
        foreach (var m in new[] { body, hat })
        {
            await CallJsonAsync(driver, "define_mesh", new { name = m.Name, vertices = m.Vertices, faces = m.Faces });
        }

        var skeleton = await CallJsonAsync(driver, "define_skeleton", new { bones = GardenerSpec.SkeletonArgument() });
        if (skeleton.ToString().Contains("error", StringComparison.OrdinalIgnoreCase))
        {
            driver.Fail($"define_skeleton: {skeleton}");
        }

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
            await CallJsonAsync(driver, "define_clip", new { name = clip.Name, duration = clip.Duration, loop = clip.Loop, channels = clip.Argument() });
        }
    }
}
