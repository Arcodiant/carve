using System.Numerics;
using System.Text.Json;
using Carve.Debug;
using Carve.Extensibility;
using Carve.Scenario.Crate;
using Microsoft.Extensions.DependencyInjection;
using static Carve.Scenario.Crate.CameraPixels;
using static Carve.Scenario.Character.GardenerPixels;
using static Carve.Scenario.Crate.CrateBuild;

namespace Carve.Scenario.Character;

/// <summary>Registers the scenario, as every scenario extension does.</summary>
public sealed class CharacterExtension : ICarveExtension
{
    public void ConfigureHost(CarveHostBuilderContext context) => context.Services.AddSingleton<IScenario, CharacterScenario>();

    public void OnStarted(IServiceProvider services)
    {
    }
}

/// <summary>
/// The thin slice of M-16 proved through the real tools, on the gardener (<see cref="GardenerSpec"/>): built, looked at at
/// rest, in a pose and in each clip at three times, saved, reopened and compared, and exported into <c>game/Content/models/</c>
/// of the run folder (a stand-in content folder) as a model and as clips, with the refusals of an existing file and of an
/// unweighted vertex. <c>tests/Carve.CharacterLoad.Tests</c> then reads that folder as a game would.
/// </summary>
public sealed class CharacterScenario : IScenario
{
    private const int Size = 512;
    private static readonly string[] BodyGroups = ["skin", "shirt", "trousers", "boots", "can"];
    private static readonly string[] HatGroups = ["straw", "band"];

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

        var (min, max) = GardenerSpec.RestBounds();
        var front = Perspective(min, max, Vector3.UnitZ, Size);
        var side = Perspective(min, max, Vector3.UnitX, Size);

        // ---- the frames and what each must show ----
        var rest = await driver.CaptureAsync("gardener-rest-front", new { view = "+z" });
        var restSide = await driver.CaptureAsync("gardener-rest-side", new { view = "+x" });
        await driver.CaptureAsync("gardener-rest-three-quarter", new { view = "front" });
        CheckCorners(driver, rest);
        CheckRest(driver, rest, front, body, hat);

        var posed = await driver.CaptureAsync("gardener-pose-front", new { view = "+z", pose = GardenerSpec.Pose });
        CheckPose(driver, posed, rest, front, body);

        // Idle: at 0 s every key is the rest, so the frame is the rest frame; at 1 s the can has swayed out and the hips dropped.
        var idle = GardenerSpec.Idle;
        var idle0 = await driver.CaptureAsync("gardener-idle-0.0-front", new { view = "+z", clip = "Idle", time = 0.0 });
        driver.AssertBytesEqual("Idle at 0 s, against the rest pose", rest.Pixels, idle0.Pixels);
        foreach (var t in new[] { 1.0f, 1.5f })
        {
            var frame = await driver.CaptureAsync($"gardener-idle-{t:0.0}-front", new { view = "+z", clip = "Idle", time = t });
            CheckCorners(driver, frame);
            var locals = (Func<string, (Quaternion?, Vector3?)>)(b => idle.Sample(b, t));
            CheckCan(driver, frame, front, body, locals, $"Idle at {t} s");
            if (t == 1.0f)
            {
                var (x, y) = Holding(front(new Vector3(-0.43f, 0.39f, 0.02f)));
                ExpectNot(driver, frame, x, y, "can", "where the can rests (it has swayed out by 12 degrees at 1 s)");
            }
            else
            {
                // 1.5 s lies between the keys at 1 s (-12 degrees) and 2 s (0), so the can is at -6. The drum's outer side
                // as at 1 s and its inner side as at 2 s are each about 0.14 m from the drum's centre at -6 (radius 0.09),
                // so a sampler that held either key draws the can on one of them.
                foreach (var (key, drumX) in new[] { (1f, -0.50f), (2f, -0.36f) })
                {
                    Func<string, (Quaternion?, Vector3?)> held = b => idle.Sample(b, key);
                    var (x, y) = Holding(front(GardenerSpec.Posed(new Vector3(drumX, 0.39f, 0.02f), "WateringCan", held)));
                    ExpectNot(driver, frame, x, y, "can", $"Idle at {t} s: where the can's side is when the key at {key} s is held");
                }
            }
        }

        // Walk, from the side (+X: forward, +Z, is the image's left): the near (left) hand is forward at 0 s, back at 0.5 s.
        var walk = GardenerSpec.Walk;
        var walkFrames = new Dictionary<float, ScenarioFrame>();
        foreach (var t in new[] { 0f, 0.125f, 0.25f, 0.5f })
        {
            var frame = await driver.CaptureAsync($"gardener-walk-{t:0.000}-side", new { view = "+x", clip = "Walk", time = t });
            walkFrames[t] = frame;
            CheckCorners(driver, frame);
            Func<string, (Quaternion?, Vector3?)> locals = b => walk.Sample(b, t);
            var hand = GardenerSpec.Posed(new Vector3(0.415f, 0.60f, 0), "Hand_L", locals);
            var (hx, hy) = Holding(side(hand));
            ExpectGroup(driver, frame, hx, hy, "skin", $"Walk at {t} s: the near hand at {hand}");
            if (t is 0f or 0.5f)
            {
                ExpectNot(driver, frame, Size - 1 - hx, hy, "skin", $"Walk at {t} s: the hand's mirror (the hand is at the image centre at 0.25 s, so there it is not asked)");
                var (gx, gy) = Holding(side(new Vector3(0.09f, 0.05f, 0.05f)));
                if (!IsBackground(frame.Pixel(gx, gy)))
                {
                    driver.Fail($"gardener-walk-{t:0.00}-side: the gap between the feet at ({gx}, {gy}) is {frame.Pixel(gx, gy)}, expected the background");
                }
            }
        }

        // A clip at a key is the pose its keys give, and the loop closes.
        foreach (var t in new[] { 0f, 0.5f })
        {
            var byPose = await driver.CaptureAsync($"gardener-walk-pose-{t:0.00}-side", new { view = "+x", pose = walk.PoseAt(t) });
            driver.AssertBytesEqual($"Walk at {t} s, against the pose of its keys", walkFrames[t].Pixels, byPose.Pixels);
        }

        var loopEnd = await driver.CaptureAsync("gardener-walk-1.00-side", new { view = "+x", clip = "Walk", time = 1.0 });
        driver.AssertBytesEqual("Walk at 1 s, against Walk at 0 s (a looping clip closes)", walkFrames[0f].Pixels, loopEnd.Pixels);
        if (walkFrames[0f].Pixels.SequenceEqual(walkFrames[0.5f].Pixels) || walkFrames[0f].Pixels.SequenceEqual(restSide.Pixels))
        {
            driver.Fail("Walk at 0 s is the same as Walk at 0.5 s or as the rest pose");
        }

        await SaveReopenAsync(driver, work, rest, restSide, walkFrames[0.25f]);
        await ExportAsync(driver, work, models);
    }

    // ---- build, through the tools ----

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

            await CallTextAsync(driver, "check", new { mesh = m.Name });
        }

        await ExpectToolErrorAsync(driver, "set_weights", new { mesh = "Body", vertices = new[] { 0 }, weights = new { Tail = 1 } }, "Tail", "Hips");
        foreach (var clip in new[] { GardenerSpec.Idle, GardenerSpec.Walk })
        {
            var defined = await CallJsonAsync(driver, "define_clip", new { name = clip.Name, duration = clip.Duration, loop = clip.Loop, channels = clip.Argument() });
            if (defined.GetProperty("replaced").GetBoolean() || !defined.GetProperty("loop").GetBoolean())
            {
                driver.Fail($"define_clip {clip.Name}: expected a new looping clip, got {defined}");
            }
        }

        await ExpectToolErrorAsync(driver, "render", new { view = "+z", clip = "Run", time = 0.5 }, "Idle", "Walk");
    }

    // ---- pixel checks ----

    private static void ExpectBackground(ScenarioDriver driver, ScenarioFrame frame, int x, int y, string what)
    {
        if (x >= 0 && y >= 0 && x < frame.Width && y < frame.Height && !IsBackground(frame.Pixel(x, y)))
        {
            driver.Fail($"{frame.Name}: {what} at ({x}, {y}) is {frame.Pixel(x, y)}, expected the background");
        }
    }

    /// <summary>
    /// Rest, from +Z (the gardener faces the camera, his left at the image's right): the centre of one front face of each colour's
    /// group, projected from the mesh, reads as that colour, and the can, at -X, is on the image's LEFT with the background where a
    /// frame mirrored in X would put it (the image mirrored, and the world mirrored).
    /// </summary>
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

        ExpectBackground(driver, frame, Size - 1 - cx, cy, "where a frame mirrored in X would show the can");
        var (mx, my) = Holding(project(new Vector3(-can.X, can.Y, can.Z)));
        ExpectBackground(driver, frame, mx, my, "the can's mirror in the world (+X, the left hand's side)");
        ExpectNot(driver, frame, mx, my, "can", "the can's mirror in the world");
    }

    /// <summary>The can face of <paramref name="body"/> at a posed place (worked from the keys and the rest skeleton) reads as the can.</summary>
    private static void CheckCan(ScenarioDriver driver, ScenarioFrame frame, Func<Vector3, Vector2> project, GardenerSpec.MeshSpec body,
        Func<string, (Quaternion? Rotation, Vector3? Translation)> locals, string what)
    {
        var rest = body.Centroid(body.NearestFace("can", new Vector3(-0.435f, 0.39f, 0.30f)));
        var at = GardenerSpec.Posed(rest, "WateringCan", locals);
        var (x, y) = Holding(project(at));
        ExpectGroup(driver, frame, x, y, "can", $"{what}: the can face, posed to {at}");
        var (mx, my) = Holding(project(new Vector3(-at.X, at.Y, at.Z)));
        ExpectNot(driver, frame, mx, my, "can", $"{what}: the can's mirror in X");
    }

    private static void CheckPose(ScenarioDriver driver, ScenarioFrame frame, ScenarioFrame rest, Func<Vector3, Vector2> project, GardenerSpec.MeshSpec body)
    {
        CheckCorners(driver, frame);
        if (frame.Pixels.SequenceEqual(rest.Pixels))
        {
            driver.Fail($"{frame.Name}: the same as the rest pose");
        }

        Func<string, (Quaternion?, Vector3?)> locals = b => b switch
        {
            "Shoulder_R" => (GardenerSpec.PoseRotation('Z', -35), null),
            "Head" => (GardenerSpec.PoseRotation('Y', 30), null),
            "UpperLeg_L" => (GardenerSpec.PoseRotation('X', -25), null),
            _ => (null, null),
        };
        CheckCan(driver, frame, project, body, locals, "the pose");

        // The arm swung out and up: the can's old place is empty, and the posed corners are inside the frame framed on the rest bounds.
        var (x, y) = Holding(project(new Vector3(-0.43f, 0.30f, 0.02f)));
        ExpectBackground(driver, frame, x, y, "where the can rested, before the arm was raised");
        var posedAt = GardenerSpec.Posed(body.Centroid(body.NearestFace("can", new Vector3(-0.435f, 0.39f, 0.30f))), "WateringCan", locals);
        var (px, py) = Holding(project(posedAt));
        if (px < 0 || py < 0 || px >= Size || py >= Size)
        {
            driver.Fail($"{frame.Name}: the posed can falls at ({px}, {py}), outside the image framed on the rest bounds");
        }
    }

    // ---- save, reopen, export ----

    private static async Task SaveReopenAsync(ScenarioDriver driver, string work, ScenarioFrame rest, ScenarioFrame restSide, ScenarioFrame walkQuarter)
    {
        var path = Path.Combine(work, "gardener.carve");
        var meshes = new[] { "Body", "Hat" };
        var before = new List<string>();
        foreach (var m in meshes)
        {
            before.Add(await CallTextAsync(driver, "get_mesh", new { mesh = m }));
        }

        var skeletonBefore = await CallTextAsync(driver, "get_skeleton", new { });
        var saved = await CallTextAsync(driver, "save", new { path });
        if (!saved.Contains("gardener.carve") || saved.Contains("replaced"))
        {
            driver.Fail($"save: expected a new gardener.carve, got {saved}");
        }

        var bytes = await File.ReadAllBytesAsync(path);

        // Something else is opened over the model's name, then the file brings the gardener back whole.
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

        if (await CallTextAsync(driver, "get_skeleton", new { }) != skeletonBefore)
        {
            driver.Fail("open: get_skeleton differs from the one taken before the save");
        }

        var rest2 = await driver.CaptureAsync("gardener-rest-front-reopened", new { view = "+z" });
        var side2 = await driver.CaptureAsync("gardener-rest-side-reopened", new { view = "+x" });
        var walk2 = await driver.CaptureAsync("gardener-walk-0.25-side-reopened", new { view = "+x", clip = "Walk", time = 0.25 });
        driver.AssertBytesEqual("rest from the front, after save and open (colours and shape came back; a rest frame cannot show weights: walk2 and the re-saved bytes do)", rest.Pixels, rest2.Pixels);
        driver.AssertBytesEqual("rest from the side, after save and open", restSide.Pixels, side2.Pixels);
        driver.AssertBytesEqual("Walk at 0.25 s, after save and open (the clip came back)", walkQuarter.Pixels, walk2.Pixels);

        var again = await CallTextAsync(driver, "save", new { path });
        if (!again.Contains("(replaced)"))
        {
            driver.Fail($"save (again): expected replaced, got {again}");
        }

        driver.AssertBytesEqual("gardener.carve, saved again after open", bytes, await File.ReadAllBytesAsync(path));
    }

    private static async Task ExportAsync(ScenarioDriver driver, string work, string models)
    {
        var glb = Path.Combine(models, "gardener.glb");
        var clips = Path.Combine(models, "gardener.clips.glb");

        var exported = await CallTextAsync(driver, "export", new { path = glb });
        foreach (var mention in new[] { "gardener.glb", "22 bone(s)", "2 clip(s)", "'Idle'", "'Walk'" })
        {
            if (!exported.Contains(mention))
            {
                driver.Fail($"export: expected '{mention}' in {exported}");
            }
        }

        if (exported.Contains("warning(s)") || exported.Contains("replaced"))
        {
            driver.Fail($"export: expected no warnings and a new file, got {exported}");
        }

        await File.WriteAllTextAsync(Path.Combine(driver.RunFolder, "export-result.txt"), exported);
        var glbBytes = await File.ReadAllBytesAsync(glb);
        await ExpectToolErrorAsync(driver, "export", new { path = glb }, "already exists", "overwrite");
        driver.AssertBytesEqual("gardener.glb, refused a second export", glbBytes, await File.ReadAllBytesAsync(glb));
        await CallTextAsync(driver, "export", new { path = glb, overwrite = true });
        driver.AssertBytesEqual("gardener.glb, exported again with overwrite", glbBytes, await File.ReadAllBytesAsync(glb));

        var clipsResult = await CallTextAsync(driver, "export", new { path = clips, mode = "clips" });
        if (!clipsResult.Contains("as clips, with no mesh: 22 bone(s)") || !clipsResult.Contains("2 clip(s)"))
        {
            driver.Fail($"export mode clips: got {clipsResult}");
        }

        var clipsBytes = await File.ReadAllBytesAsync(clips);
        await ExpectToolErrorAsync(driver, "export", new { path = clips, mode = "clips" }, "already exists", "overwrite");
        await CallTextAsync(driver, "export", new { path = clips, mode = "clips", overwrite = true });
        driver.AssertBytesEqual("gardener.clips.glb, exported again with overwrite", clipsBytes, await File.ReadAllBytesAsync(clips));

        // A vertex with no weight: extruding the hat's top face makes new ones. Refused and nothing written, then bound to the first root bone.
        var hatBefore = await CallTextAsync(driver, "get_mesh", new { mesh = "Hat" });
        await CallJsonAsync(driver, "extrude", new { mesh = "Hat", faces = new[] { GardenerSpec.HatTopFace }, distance = 0.02 });
        var loose = Path.Combine(work, "gardener-unweighted.glb");
        var refused = await ExpectToolErrorAsync(driver, "export", new { path = loose }, "Hat", "unweighted");
        if (File.Exists(loose))
        {
            driver.Fail("export: a refused export left a file");
        }

        var allowed = await CallTextAsync(driver, "export", new { path = loose, allow_unweighted = true });
        if (!allowed.Contains("bound to the first root bone 'Hips'") || !File.Exists(loose))
        {
            driver.Fail($"export allow_unweighted: expected the vertices bound to Hips and a file, got {allowed} (refusal was {refused})");
        }

        await CallJsonAsync(driver, "undo", new { });
        if (await CallTextAsync(driver, "get_mesh", new { mesh = "Hat" }) != hatBefore)
        {
            driver.Fail("undo of the extrude: the hat is not as it was");
        }

        await CallTextAsync(driver, "export", new { path = glb, overwrite = true });
        driver.AssertBytesEqual("gardener.glb, exported after the extrude was undone", glbBytes, await File.ReadAllBytesAsync(glb));
    }
}
