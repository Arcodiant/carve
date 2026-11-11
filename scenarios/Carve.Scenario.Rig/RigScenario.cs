using System.Numerics;
using System.Text.Json;
using Carve.Debug;
using Carve.Extensibility;
using Carve.Scenario.Character;
using Microsoft.Extensions.DependencyInjection;
using static Carve.Scenario.Crate.CameraPixels;
using static Carve.Scenario.Crate.CrateBuild;

namespace Carve.Scenario.Rig;

/// <summary>Registers the scenario, as every scenario extension does.</summary>
public sealed class RigExtension : ICarveExtension
{
    public void ConfigureHost(CarveHostBuilderContext context) => context.Services.AddSingleton<IScenario, RigScenario>();

    public void OnStarted(IServiceProvider services)
    {
    }
}

/// <summary>
/// The gardener of M-16 rigged (M-17): built through the tools from <see cref="GardenerSpec"/> with three planted skinning faults, each found
/// by <c>get_weights</c> or a frame with <c>bones</c> or <c>weights</c>, fixed through the tools and the frame taken again: an unweighted patch
/// on the torso, a hat vertex weighted to the wrong arm, and an elbow bone out of place. <c>export</c> is refused while the patch remains and
/// succeeds once it is gone. Last, the fault <c>weigh</c> makes itself: the body weighed automatically has torso vertices that follow the
/// hanging arm; a <c>set_weights</c> group override fixes it, and a plain <c>weigh</c> keeps the override. Every pixel is worked from the geometry.
/// </summary>
public sealed class RigScenario : IScenario
{
    private const int Size = 512;

    // Torso rings of 8 vertices: ring r is vertex ids 8r..8r+7 and j runs round the octagon (j 2 is the front, +Z; j 3 is front-left, at -X,
    // the image's left). Faces: segment s (between ring s and s+1), j is face 8s + j, its corners j and j + 1.
    private static readonly int[] Patch = [.. new[] { 3, 4 }.SelectMany(r => new[] { 2, 3, 4 }.Select(j => 8 * r + j))];
    private const int PatchFaceLeft = 8 * 3 + 2;   // the faces between rings 3 and 4 wholly inside the patch
    private const int PatchFaceFrontLeft = 8 * 3 + 3;
    private const int PatchFaceRight = 8 * 3 + 0;  // the face at the front right (+X), fully weighted

    private const int StrayHatVertex = 11;         // the brim's top ring, j 3: at (-0.184, 1.355, 0.184)
    private static readonly Vector3 HatDirection = new(0.5f, 0.8f, 1f);
    private static readonly Vector3 HatTarget = new(0, 1.38f, 0);
    private const float HatZoom = 2.5f;

    private static readonly Vector3 ElbowRight = new(0.30f, 0.80f, 0);   // Elbow_L's head where it belongs (the figure's left arm is at +X)
    private static readonly Vector3 ElbowPlanted = new(0.30f, 1.10f, 0);

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
        var (min, max) = GardenerSpec.RestBounds();
        var front = Perspective(min, max, Vector3.UnitZ, Size);
        var close = Perspective(min, max, HatDirection, Size, HatZoom, HatTarget);
        await BuildAsync(driver, body, hat);

        // ---- fault 1: an unweighted patch ----
        // get_weights names it: the summary lists the vertices without weight, and render's weights look shows them magenta.
        var summary = (await CallJsonAsync(driver, "get_weights", new { mesh = "Body", vertices = new[] { Patch[0] } })).GetProperty("summary");
        var listed = summary.GetProperty("unweightedVertices").EnumerateArray().Select(e => e.GetInt32()).OrderBy(i => i).ToArray();
        if (!listed.SequenceEqual(Patch.OrderBy(i => i)) || summary.GetProperty("unweighted").GetInt32() != Patch.Length)
        {
            driver.Fail($"get_weights: expected the unweighted patch {string.Join(",", Patch)}, got {summary}");
        }

        var patchBefore = await driver.CaptureAsync("rig-patch-before-weights-spine", new { view = "+z", weights = "Spine_03" });
        var refused = await ExpectToolErrorAsync(driver, "export", new { path = Path.Combine(models, "gardener-rigged.glb") }, $"{Patch.Length} vertices have no weight");
        if (File.Exists(Path.Combine(models, "gardener-rigged.glb")))
        {
            driver.Fail("export: a file was written although it was refused");
        }

        await File.WriteAllTextAsync(Path.Combine(driver.RunFolder, "export-refused.txt"), refused);
        CheckPatch(driver, patchBefore, front, body, fixedUp: false);
        var patchFixed = await CallJsonAsync(driver, "set_weights", new { mesh = "Body", vertices = Patch, weights = new Dictionary<string, double> { ["Spine_03"] = 1 } });
        if (patchFixed.GetProperty("unweighted").GetInt32() != 0)
        {
            driver.Fail($"set_weights on the patch: vertices still unweighted: {patchFixed}");
        }

        var patchAfter = await driver.CaptureAsync("rig-patch-after-weights-spine", new { view = "+z", weights = "Spine_03" });
        CheckPatch(driver, patchAfter, front, body, fixedUp: true);

        // ---- fault 2: a hat vertex on the wrong arm ----
        var followers = await CallJsonAsync(driver, "get_weights", new { mesh = "Hat", bone = "Shoulder_L" });
        var strays = followers.GetProperty("followers").EnumerateArray().Select(f => f.GetProperty("vertex").GetInt32()).ToArray();
        if (!strays.SequenceEqual([StrayHatVertex]))
        {
            driver.Fail($"get_weights bone Shoulder_L on the Hat: expected only vertex {StrayHatVertex}, got {followers}");
        }

        var hatBefore = await driver.CaptureAsync("rig-hat-before-weights-shoulder", new { direction = new[] { HatDirection.X, HatDirection.Y, HatDirection.Z }, zoom = HatZoom, target = new[] { HatTarget.X, HatTarget.Y, HatTarget.Z }, weights = "Shoulder_L" });
        CheckHat(driver, hatBefore, close, hat, fixedUp: false);
        await CallJsonAsync(driver, "set_weights", new { mesh = "Hat", vertices = new[] { StrayHatVertex }, weights = new Dictionary<string, double> { ["Head"] = 1 } });
        var after = await CallJsonAsync(driver, "get_weights", new { mesh = "Hat", bone = "Shoulder_L" });
        if (after.GetProperty("followers").GetArrayLength() != 0)
        {
            driver.Fail($"get_weights bone Shoulder_L on the Hat after the fix: still followed: {after}");
        }

        var hatAfter = await driver.CaptureAsync("rig-hat-after-weights-shoulder", new { direction = new[] { HatDirection.X, HatDirection.Y, HatDirection.Z }, zoom = HatZoom, target = new[] { HatTarget.X, HatTarget.Y, HatTarget.Z }, weights = "Shoulder_L" });
        CheckHat(driver, hatAfter, close, hat, fixedUp: true);

        // ---- fault 3: a bone out of place ----
        // The bones overlay is magenta, as an unweighted vertex is, so it is taken without the weights look.
        var bonesBefore = await driver.CaptureAsync("rig-bones-before", new { view = "+z", bones = true });
        CheckBones(driver, bonesBefore, front, planted: true);
        await CallJsonAsync(driver, "define_skeleton", new { bones = GardenerSpec.SkeletonArgument() });
        var bonesAfter = await driver.CaptureAsync("rig-bones-after", new { view = "+z", bones = true });
        CheckBones(driver, bonesAfter, front, planted: false);
        var skeleton = await CallTextAsync(driver, "get_skeleton", new { });
        await File.WriteAllTextAsync(Path.Combine(driver.RunFolder, "skeleton-fixed.txt"), skeleton);

        // ---- export succeeds once the patch is gone ----
        var exported = await CallTextAsync(driver, "export", new { path = Path.Combine(models, "gardener-rigged.glb") });
        await File.WriteAllTextAsync(Path.Combine(driver.RunFolder, "export-result.txt"), exported);
        if (!exported.Contains("22 bone(s)") || exported.Contains("unweighted", StringComparison.OrdinalIgnoreCase))
        {
            driver.Fail($"export: expected the rigged gardener with 22 bones and nothing unweighted, got {exported}");
        }

        // ---- fault 4: what weigh makes itself ----
        // The body weighed automatically: the torso's side vertices are nearer the hanging arms than the spine, so they follow the arm
        // (the person saw it in T-37's review). Vertices 19 and 20 are the torso at y 0.76 on the figure's right (-X, the image's left).
        await CallJsonAsync(driver, "weigh", new { mesh = "Body", replace = true });
        var arm = await FollowersAsync(driver, "Elbow_R");
        var torsoFollowers = arm.Where(v => v is >= 8 and < 40).ToArray();
        if (!torsoFollowers.Contains(19) || !torsoFollowers.Contains(20))
        {
            driver.Fail($"get_weights Elbow_R after weigh: expected torso vertices 19 and 20 among its followers, got torso ids {string.Join(",", torsoFollowers)}");
        }

        var weighedBefore = await driver.CaptureAsync("rig-weighed-before-weights-elbow", new { view = "+z", weights = "Elbow_R" });
        CheckTorsoSide(driver, weighedBefore, front, body, fixedUp: false);
        // The override: a group of the torso's faces (rings 1 to 4: the faces 8 to 31) bound to the spine.
        var torso = Enumerable.Range(8, 24).ToArray();
        await CallJsonAsync(driver, "define_group", new { mesh = "Body", name = "torso", faces = torso });
        await CallJsonAsync(driver, "set_weights", new { mesh = "Body", group = "torso", weights = new Dictionary<string, double> { ["Spine_02"] = 1 } });
        await ExpectTorsoOverrideAsync(driver, "set_weights");
        var weighedAfter = await driver.CaptureAsync("rig-weighed-after-weights-elbow", new { view = "+z", weights = "Elbow_R" });
        CheckTorsoSide(driver, weighedAfter, front, body, fixedUp: true);
        // A plain weigh fills only what has no weight: the override stays, and nothing is left to weigh.
        var plain = await CallJsonAsync(driver, "weigh", new { mesh = "Body" });
        if (plain.GetProperty("weighed").GetInt32() != 0 || plain.GetProperty("kept").GetInt32() != 320)
        {
            driver.Fail($"weigh without replace after the override: expected 0 weighed and 320 kept, got {plain}");
        }

        await ExpectTorsoOverrideAsync(driver, "weigh");
        var kept = await driver.CaptureAsync("rig-weighed-kept-weights-elbow", new { view = "+z", weights = "Elbow_R" });
        CheckTorsoSide(driver, kept, front, body, fixedUp: true);
        driver.AssertBytesEqual("the elbow weights frame after the override, and after a plain weigh", weighedAfter.Pixels, kept.Pixels);
    }

    private static async Task<int[]> FollowersAsync(ScenarioDriver driver, string bone) =>
        [.. (await CallJsonAsync(driver, "get_weights", new { mesh = "Body", bone })).GetProperty("followers").EnumerateArray().Select(f => f.GetProperty("vertex").GetInt32())];

    /// <summary>Read back with get_weights: the torso's vertices 8 to 39 weigh Spine_02 wholly, and no arm bone has a torso follower.</summary>
    private static async Task ExpectTorsoOverrideAsync(ScenarioDriver driver, string after)
    {
        var all = await CallJsonAsync(driver, "get_weights", new { mesh = "Body", vertices = Enumerable.Range(8, 32).ToArray() });
        foreach (var v in all.GetProperty("vertices").EnumerateArray())
        {
            var w = v.GetProperty("weights");
            if (w.EnumerateObject().Count() != 1 || !w.TryGetProperty("Spine_02", out var one) || Math.Abs(one.GetDouble() - 1) > 1e-6)
            {
                driver.Fail($"get_weights after {after}: vertex {v.GetProperty("vertex")} reads {w}, expected Spine_02 wholly");
            }
        }

        foreach (var bone in new[] { "Elbow_R", "Shoulder_R", "Elbow_L", "Shoulder_L" })
        {
            var torso = (await FollowersAsync(driver, bone)).Where(v => v is >= 8 and < 40).ToArray();
            if (torso.Length > 0)
            {
                driver.Fail($"get_weights bone {bone} after {after}: torso vertices {string.Join(",", torso)} still follow it");
            }
        }
    }

    /// <summary>
    /// The Elbow_R look: the torso face between the rings at 0.62 and 0.76 on the figure's right (corners 11, 12, 19, 20, weights 0.10, 0.30, 0.31 and
    /// 0.54) is not dark blue while it follows the arm, and is dark blue once the override is made. The same face on the other side is blue throughout,
    /// so a mirrored frame fails here.
    /// </summary>
    private static void CheckTorsoSide(ScenarioDriver driver, ScenarioFrame frame, Func<Vector3, Vector2> project, GardenerSpec.MeshSpec body, bool fixedUp)
    {
        CheckCorners(driver, frame);
        var (x, y) = Holding(project(body.Centroid(8 + 3)));
        var (mx, my) = Holding(project(body.Centroid(8 + 1)));
        var (r, g, b, _) = frame.Pixel(x, y);
        var blue = Heat(frame, x, y) == "blue";
        if (blue != fixedUp)
        {
            driver.Fail($"{frame.Name}: the torso's side face at ({x}, {y}) is ({r}, {g}, {b}); expected {(fixedUp ? "dark blue, following no arm" : "not dark blue: it follows the arm")}");
        }

        if (Heat(frame, mx, my) != "blue")
        {
            driver.Fail($"{frame.Name}: the torso's side face at the other side, ({mx}, {my}), is not dark blue; a mirrored frame puts the arm's heat there");
        }
    }

    // ---- looks ----

    private static string Heat(ScenarioFrame frame, int x, int y)
    {
        if (x < 0 || y < 0 || x >= frame.Width || y >= frame.Height)
        {
            return "outside";
        }

        var (r, g, b, _) = frame.Pixel(x, y);
        if (r > 200 && b > 200 && g < 70) return "magenta";
        if (r > 200 && g < 70 && b < 70) return "red";
        if (b is > 120 and < 230 && r < 40 && g < 40) return "blue";
        if (g > 200 && r < 70 && b < 70) return "green";
        return $"other({r},{g},{b})";
    }

    private static void Expect(ScenarioDriver driver, ScenarioFrame frame, Func<Vector3, Vector2> project, Vector3 at, string colour, string what)
    {
        var (x, y) = Holding(project(at));
        var found = Heat(frame, x, y);
        if (found != colour)
        {
            driver.Fail($"{frame.Name}: {what} at {at} -> ({x}, {y}) reads {found}, expected {colour}");
        }
    }

    private static void CheckPatch(ScenarioDriver driver, ScenarioFrame frame, Func<Vector3, Vector2> project, GardenerSpec.MeshSpec body, bool fixedUp)
    {
        CheckCorners(driver, frame);
        var left = body.Centroid(PatchFaceLeft);
        var frontLeft = body.Centroid(PatchFaceFrontLeft);
        var right = body.Centroid(PatchFaceRight);
        var patch = fixedUp ? "red" : "magenta";
        Expect(driver, frame, project, left, patch, "the torso face inside the patch");
        Expect(driver, frame, project, frontLeft, patch, "the torso face inside the patch, to its left");
        // The patch is on the image's left (-X); its mirror is a fully weighted face, red; a mirrored frame draws the patch there.
        Expect(driver, frame, project, right, "red", "the weighted torso face at the patch's mirror");
        // The boots follow an ankle, not the spine: blue. A frame flipped top to bottom puts the patch down here.
        Expect(driver, frame, project, body.Centroid(body.NearestFace("boots", new Vector3(0.09f, 0.05f, 0.4f))), "blue", "a boot");
    }

    private static void CheckHat(ScenarioDriver driver, ScenarioFrame frame, Func<Vector3, Vector2> project, GardenerSpec.MeshSpec hat, bool fixedUp)
    {
        CheckCorners(driver, frame);
        var v = hat.Vertices[StrayHatVertex];
        // A point on the brim's top a little inward of the vertex: red when it follows Shoulder_L, else blue. The brim's other side (+X) is blue
        // in both, so a mirrored frame puts the red there.
        Expect(driver, frame, project, new Vector3(v[0] * 0.92f, v[1], v[2] * 0.92f), fixedUp ? "blue" : "red", "the brim near the stray vertex");
        Expect(driver, frame, project, new Vector3(-v[0] * 0.92f, v[1], v[2] * 0.92f), "blue", "the brim at the stray vertex's mirror");
    }

    /// <summary>Whether a magenta bone dot or line passes within 3 pixels of the point.</summary>
    private static bool MagentaNear(ScenarioFrame frame, Func<Vector3, Vector2> project, Vector3 at)
    {
        var (x, y) = Holding(project(at));
        return Enumerable.Range(-3, 7).Any(dx => Enumerable.Range(-3, 7).Any(dy => Heat(frame, x + dx, y + dy) == "magenta"));
    }

    private static void CheckBones(ScenarioDriver driver, ScenarioFrame frame, Func<Vector3, Vector2> project, bool planted)
    {
        CheckCorners(driver, frame);
        void Is(Vector3 at, bool expected, string what)
        {
            if (MagentaNear(frame, project, at) != expected)
            {
                driver.Fail($"{frame.Name}: {what} at {at}: expected {(expected ? "a bone" : "no bone")} within 3 pixels");
            }
        }

        // Elbow_L (the figure's left, +X). Planted, its head is 0.3 above the sleeve, on the image's right; the planted place has no
        // bone once it is fixed, and the sleeve has none until it is. Nothing is at the planted place's x mirror either way, so a
        // mirrored frame (which would put the bone there) fails the planted frame.
        Is(ElbowPlanted, planted, "Elbow_L's head where it was planted");
        Is(ElbowRight, !planted, "Elbow_L's head where it belongs");
        Is(new Vector3(-ElbowPlanted.X, ElbowPlanted.Y, 0), false, "the x mirror of the planted place");
        Is(new Vector3(-ElbowRight.X, ElbowRight.Y, 0), true, "Elbow_R's head, the other arm");
    }

    // ---- build, through the tools, from the spec with the faults planted ----

    private static async Task BuildAsync(ScenarioDriver driver, GardenerSpec.MeshSpec body, GardenerSpec.MeshSpec hat)
    {
        foreach (var m in new[] { body, hat })
        {
            await CallJsonAsync(driver, "define_mesh", new { name = m.Name, vertices = m.Vertices, faces = m.Faces });
        }

        // Fault 3: Elbow_L's offset from its shoulder is up and to the side, 0.3 above where it belongs.
        var bones = GardenerSpec.SkeletonArgument();
        bones[Array.FindIndex(GardenerSpec.Bones, b => b.Name == "Elbow_L")] =
            new { name = "Elbow_L", parent = "Shoulder_L", position = new[] { 0.13f, 0.10f, 0f }, rotation = new[] { 0f, 0, 0, 1 } };
        var skeleton = await CallJsonAsync(driver, "define_skeleton", new { bones });
        if (skeleton.ToString().Contains("error", StringComparison.OrdinalIgnoreCase))
        {
            driver.Fail($"define_skeleton: {skeleton}");
        }

        foreach (var (m, groups) in new[] { (body, new[] { "skin", "shirt", "trousers", "boots", "can" }), (hat, ["straw", "band"]) })
        {
            foreach (var group in groups)
            {
                await CallJsonAsync(driver, "define_group", new { mesh = m.Name, name = group, faces = m.FacesOf(group) });
                var colour = GardenerSpec.Palette.Single(p => p.Group == group).Colour;
                await CallJsonAsync(driver, "set_colour", new { mesh = m.Name, colour, group });
            }

            // The hand weights of the spec, less the planted faults: no weight on the torso patch (fault 1), one brim vertex on the
            // left shoulder instead of the head (fault 2).
            foreach (var (vertices, weights) in m.Weights)
            {
                var kept = m == body ? vertices.Where(v => !Patch.Contains(v)).ToArray() : vertices.Where(v => v != StrayHatVertex).ToArray();
                if (kept.Length > 0)
                {
                    await CallJsonAsync(driver, "set_weights", new { mesh = m.Name, vertices = kept, weights });
                }
            }
        }

        await CallJsonAsync(driver, "set_weights", new { mesh = "Hat", vertices = new[] { StrayHatVertex }, weights = new Dictionary<string, double> { ["Shoulder_L"] = 1 } });
    }
}
