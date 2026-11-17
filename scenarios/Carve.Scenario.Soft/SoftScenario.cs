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

        // Every warning is a face that is not planar (what subdivide makes of a quad); a limit (triangles, primitives, height) must not
        // be one. By T-72's rule (the person, 2026-11-13) each mesh names its first 10 and folds the rest into one line, and the named
        // lines and the folded count add up to select's non_planar faces of that mesh, the named faces are select's lowest ids and the
        // fold line comes after them, so a regression that adds a warning or loses a
        // face is caught. select's results are kept in the run folder for SoftLoad.Tests.
        var nonPlanar = new Dictionary<string, IReadOnlyList<int>>();
        foreach (var name in new[] { "Body", "Hat" })
        {
            var selected = await CallTextAsync(driver, "select", new { kind = "faces", where = "non_planar", mesh = name });
            await File.WriteAllTextAsync(Path.Combine(driver.RunFolder, $"select-non-planar-{name}.txt"), selected);
            var found = JsonDocument.Parse(selected).RootElement;
            nonPlanar[name] = [.. found.GetProperty("faces").EnumerateArray().Select(f => f.GetInt32())];
            if (nonPlanar[name].Count != found.GetProperty("count").GetInt32())
            {
                driver.Fail($"select non_planar on {name}: count {found.GetProperty("count")} against {nonPlanar[name].Count} faces listed");
            }
        }
        var warned = exported.IndexOf("warning(s): ", StringComparison.Ordinal);
        var problem = warned < 0 ? "no warnings" : NotPlanarFold.Check(exported[(warned + "warning(s): ".Length)..], nonPlanar);
        if (problem is not null)
        {
            driver.Fail($"export: {problem}");
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
        ExpectRestColours(driver, frame, project, body, hat);

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

    // Under M-7's sky the flat torso's facet edges step by 7 and the smooth one's by 2 (the sky's light, about 1.3 to 2.3 times the old
    // ambient, flattens a step the sun makes): the person's ruling of 2026-11-17 ("Accept all", M-19 row 4a), 9 to 5 and 5 to 4, each
    // side clearing by 2.
    private const int FacetStep = 5;
    private const int SmoothStep = 4;

    /// <summary>
    /// The torso, from the front, at the height 0.80 m: its octagon's two side facet edges (world x of plus and minus 0.1687). Flat, the
    /// shade steps across each; soft, it runs on across them. Under LitMesh the steps were 30 and 31 flat and 5 to 9 soft on a 6 pixel
    /// span, against 20 and 12. Since T-82 the material is the engine's PBR one, whose directional light reaches a face as albedo / pi with
    /// the ambient floor unchanged, so a step the light makes is about 0.43 of LitMesh's (the cube's faces, worked from their n.l): flat
    /// about 13, soft 2 to 4. The bounds scale with it, by the person's ruling of 2026-11-13: 9 or more flat, 5 or less soft. The light
    /// model worked over the samples' geometry (see the span below) gives flat steps of 13 and 14 and soft ones of 5 and 5.
    /// </summary>
    private static void CheckTorso(ScenarioDriver driver, ScenarioFrame frame, Func<Vector3, Vector2> project, bool flat)
    {
        foreach (var sign in new[] { -1f, 1f })
        {
            var (x, y) = Holding(project(new Vector3(sign * 0.1687f, 0.80f, 0.1687f)));
            ExpectGroup(driver, frame, x - 3, y, "shirt", "the torso, left of a facet edge");
            ExpectGroup(driver, frame, x + 3, y, "shirt", "the torso, right of a facet edge");
            var step = Math.Abs(Luma(frame, x + 3, y) - Luma(frame, x - 3, y));
            if (flat ? step < FacetStep : step > SmoothStep)
            {
                driver.Fail($"{frame.Name}: across the torso's facet edge at ({x}, {y}) the shade steps by {step}; {(flat ? $"a flat mesh steps by {FacetStep} or more" : $"a smooth one runs on, by {SmoothStep} or less")}");
            }
        }

        // Across the side facet edge, toward the lit centre, the shade is a ramp when smooth, however many facets the mesh has (a run of
        // equal shades of at most 5 pixels), and a plateau per facet when not (10 or more). The span starts near the torso's -X silhouette
        // (T-82, the person's ruling of 2026-11-13: re-pick, else lower), where the surface turns from the camera fastest per pixel. Worked
        // from the samples' geometry and the light model (the +z camera's light toward (-0.02, 0.47, 0.89); the shirt's linear colour times
        // ambient 0.25 plus 0.9 n.l / pi; sRGB; a ray cast per pixel, the soft mesh's corner normals interpolated): flat, the row crosses the
        // facet edge onto the front facet, one shade for the 16 pixels to the span's end; smooth, it ramps 89 to 101 and holds a shade for 5
        // pixels at most, where it nears the lit centre. The old span, -0.16 to -0.08, lies wholly in that gentler part. Spans tried further
        // out leave the soft torso (it is background past about -0.21) or cut the flat plateau to 10.
        var (from, row) = Holding(project(new Vector3(-0.195f, 0.80f, 0.17f)));
        var (to, _) = Holding(project(new Vector3(-0.115f, 0.80f, 0.17f)));
        // Under M-7's sky the smooth ramp is flatter (130 to 136 over the span) and holds a shade for 7 pixels, so the span is judged by its
        // count of distinct shades, not its longest run: smooth 5 or more (measured 7), flat 3 or fewer (measured 2, a plateau per facet).
        // The person's ruling of 2026-11-17 ("Accept all", M-19 row 4b).
        var shades = Enumerable.Range(from, to - from + 1).Select(x => Luma(frame, x, row)).Distinct().Count();
        if (flat ? shades > 3 : shades < 5)
        {
            driver.Fail($"{frame.Name}: the torso's shade over x {from} to {to} at row {row} takes {shades} distinct shades; {(flat ? "a flat mesh holds one shade per facet (3 or fewer)" : "a smooth one is a ramp (5 or more)")}");
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

    // The rim's steps, scaled as the torso's are (T-82, the person's ruling of 2026-11-13): under LitMesh a creased rim broke by 28 in one
    // pixel and an uncreased one rolled off in stages of 12, against a stage of 10 and a break of 20. At about 0.43 of that under PBR the
    // break is about 12 and a stage about 5, so a stage counts from 4 and a break from 9.
    private const int RimStage = 4;
    private const int RimBreak = 9;

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

        // The first step of RimStage or more: one pixel's jump when creased (the wall's own shade), a stage of the roll-off when not.
        var first = Enumerable.Range(1, profile.Count - 1).Select(k => Math.Abs(profile[k] - profile[k - 1])).FirstOrDefault(d => d >= RimStage);
        if (creased ? first < RimBreak : first >= RimBreak)
        {
            driver.Fail($"{frame.Name}: down the rim from ({ox}, {oy}) the shades are {string.Join(" ", profile)} (the first step of {RimStage} or more is {first}); {(creased ? $"a creased rim breaks at once, by {RimBreak} or more" : $"an uncreased rim rolls off, by less than {RimBreak} at a step")}");
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
