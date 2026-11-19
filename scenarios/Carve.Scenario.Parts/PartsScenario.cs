using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Carve.Debug;
using Carve.Extensibility;
using Carve.Rover;
using Carve.Scenario.Shed;
using Microsoft.Extensions.DependencyInjection;
using static Carve.Scenario.Shed.Checks;

namespace Carve.Scenario.Parts;

/// <summary>Registers the scenario, as every scenario extension does.</summary>
public sealed class PartsExtension : ICarveExtension
{
    public void ConfigureHost(CarveHostBuilderContext context) => context.Services.AddSingleton<IScenario, PartsScenario>();

    public void OnStarted(IServiceProvider services)
    {
    }
}

/// <summary>
/// M-9 through the real tools: the agreed rover (<see cref="RoverSpec"/>, typed once in Core's tests and linked here) built as a
/// part tree, blockout first and then refined, and seen in 13 frames, each judged pixel by pixel against the geometry
/// <c>get_mesh</c> gives placed by the spec's tree. The run folder holds <c>frames/</c>, <c>user-data/</c>, <c>run.json</c> and
/// <c>parts/rover.carve</c>, the model as saved after the refinement (what <c>open</c> read back for <c>reopened-front</c>).
/// </summary>
public sealed class PartsScenario : IScenario
{
    private const int Size = 512;
    private static readonly Vector3 Front = new(1.6f, 1.2f, 2.2f);

    /// <summary>The least count of sure inner-cap pixels for each wheel in the Axle_F frame (T-94 measured 3 325 each).</summary>
    private const int InnerCapLeast = 3000;

    private static string V3(Vector3 v) => $"[{v.X}, {v.Y}, {v.Z}]";

    private static float[] Floats(Vector3 v) => [v.X, v.Y, v.Z];

    private static object PartArguments(RoverSpec.PartSpec p)
    {
        var rotation = PartsBreak.Value == "no-instance-rotation" && p.InstanceOf is not null ? Quaternion.Identity : p.Rotation;
        var args = new Dictionary<string, object?>
        {
            ["part"] = p.Name,
            ["parent"] = p.Parent,
            ["translation"] = Floats(p.Translation),
            ["rotation"] = new[] { rotation.X, rotation.Y, rotation.Z, rotation.W },
            ["envelope"] = p.Envelope is { } e ? new { min = Floats(e.Min), max = Floats(e.Max) } : null,
            ["moves"] = p.Moves,
        };
        if (p.InstanceOf is not null) args["instance_of"] = p.InstanceOf;
        return args;
    }

    public async Task RunAsync(ScenarioDriver driver)
    {
        // 1. The blockout: the tree, the level 0 boxes and the 8-sided wheel, the paint, and level 0 kept.
        foreach (var p in RoverSpec.Tree.Where(p => p.InstanceOf is null)) await CallJsonAsync(driver, "define_part", PartArguments(p));
        foreach (var (name, shape) in RoverSpec.Blockout)
            await CallJsonAsync(driver, "define_mesh", new { name, vertices = shape.Positions.Select(Floats), faces = shape.Faces });
        foreach (var p in RoverSpec.Tree.Where(p => p.InstanceOf is not null)) await CallJsonAsync(driver, "define_part", PartArguments(p));
        foreach (var paint in RoverSpec.BlockoutPaint)
            await CallJsonAsync(driver, "set_colour", new { mesh = paint.Part, faces = paint.Faces, colour = paint.Colour });
        await CallJsonAsync(driver, "keep_level", new { level = 0 });

        var world = await RoverWorld.ReadAsync(driver);
        CheckBlockoutGeometry(world);
        await CheckPartsAsync(driver, world, refined: false);

        // Frames 1 and 2: level 0.
        var blockout = world.Draw(null, 0);
        CheckCounts(blockout, RoverSpec.Tree.Where(p => RoverSpec.GeometryParts.Contains(p.InstanceOf ?? p.Name)).Select(p => RoverSpec.Blockout[p.InstanceOf ?? p.Name]).ToArray());
        var frame1 = await driver.CaptureAsync("blockout-front", new { level = 0 });
        CheckWhole(frame1, blockout, Front, "view front, level 0, ", 4000);
        await SheetAsync(driver, blockout, "blockout-sheet");

        // 2. The refinement: the 16-sided wheel over the 8-sided one, the hub boss, the windscreen and the lens, each coloured, then level 1.
        var wheel = RoverSpec.RefinedWheel;
        await CallJsonAsync(driver, "define_mesh", new { name = "Wheel_FL", vertices = wheel.Positions.Select(Floats), faces = wheel.Faces });
        foreach (var cut in RoverSpec.Cuts)
            await CallJsonAsync(driver, "extrude", new { mesh = cut.Part, faces = new[] { cut.Face }, distance = cut.Distance, inset = cut.Inset });
        foreach (var paint in RoverSpec.RefinedPaint)
            await CallJsonAsync(driver, "set_colour", new { mesh = paint.Part, faces = paint.Faces, colour = paint.Colour });
        await CallJsonAsync(driver, "keep_level", new { level = 1, parts = RoverSpec.RefinedParts });

        world = await RoverWorld.ReadAsync(driver);
        Equal("Wheel_FL's faces live", RoverSpec.RefinedPaint[0].Faces.Length, world.Live("Wheel_FL").LiveFaceCount);
        await CheckPartsAsync(driver, world, refined: true);
        foreach (var part in RoverSpec.GeometryParts)
            await CheckSilentAsync(driver, part);

        // Frames 3, 4 and 5: live, from the back left, and from above.
        var live = world.Draw(null, null);
        var frame3 = await driver.CaptureAsync("front", new { });
        CheckWhole(frame3, live, Front, "view front, ", 4000);
        var backLeft = new Vector3(-1, 0.6f, -1);
        var frame4 = await driver.CaptureAsync("back-left", new { direction = new[] { -1f, 0.6f, -1f } });
        CheckWhole(frame4, live, backLeft, "direction (-1, 0.6, -1), ", 4000);
        var frame5 = await driver.CaptureAsync("top", new { view = "+y", projection = "orthographic" });
        CheckWhole(frame5, live, Vector3.UnitY, "view +y, orthographic, ", 4000, orthographic: true, up: -Vector3.UnitZ);

        // Frames 7, 8 and 9: the front axle alone, one wheel alone with its face ids, and that wheel at level 0.
        await AxleAsync(driver, world);
        await WheelAsync(driver, world);

        // Frame 10: the model saved and opened again draws the same bytes as frame 3.
        var path = Path.Combine(driver.RunFolder, "parts", "rover.carve");
        var partsBefore = await CallTextAsync(driver, "get_parts", new { });
        await CallTextAsync(driver, "save", new { path });
        await CallTextAsync(driver, "open", new { path });
        Equal("get_parts after save and open", partsBefore, await CallTextAsync(driver, "get_parts", new { }));
        var frame10 = await driver.CaptureAsync("reopened-front", new { });
        driver.AssertBytesEqual("reopened-front against front", frame3.Png, frame10.Png);

        // The export (T-95's node layout) of the model as saved, into the content folder the viewer and the load checks read (T-97).
        var content = Path.Combine(driver.RunFolder, "game", "Content", "models");
        Directory.CreateDirectory(content);
        var exported = await CallTextAsync(driver, "export", new { path = Path.Combine(content, "rover.glb") });
        if (exported.Contains("warning", StringComparison.OrdinalIgnoreCase) || !exported.Contains("rover.glb") || !exported.Contains("6 nodes"))
            driver.Fail($"export: expected a clean report of rover.glb in 6 nodes, got: {exported}");
        await File.WriteAllTextAsync(Path.Combine(driver.RunFolder, "export-result.txt"), exported);

        // Frame 11: one move on Wheel_FL's hub boss changes all four wheels; undo puts them back, byte for byte.
        await InstanceEditAsync(driver, world, live, frame5);

        // Frame 6: the mast turned a quarter turn swings the lamp from +X to -Z.
        await MastAsync(driver, world);

        // check and get_parts: the rover is silent, Axle_F lowered 0.3 m is exactly the one envelope warning, and an instance refuses an edit.
        await EnvelopeAsync(driver);
        await ExpectToolErrorAsync(driver, "move", new { mesh = "Wheel_FR", faces = new[] { RoverSpec.HubMove.Face }, by = Floats(RoverSpec.HubMove.By) }, "Wheel_FR", "instance", "Wheel_FL");

        if (Failures.Count > 0)
            driver.Fail($"{Failures.Count} check(s) failed:\n" + string.Join("\n", Failures.Select(f => "  " + f)));
    }

    /// <summary>Each level 0 mesh is exactly what the spec sent: <c>get_mesh</c> gives its positions and faces back.</summary>
    private static void CheckBlockoutGeometry(RoverWorld world)
    {
        foreach (var (name, shape) in RoverSpec.Blockout)
        {
            var mesh = world.Live(name);
            Equal($"{name}: vertices at level 0", shape.Positions.Length, mesh.LiveVertexCount);
            Equal($"{name}: faces at level 0", shape.Faces.Length, mesh.LiveFaceCount);
            var worst = shape.Positions.Select((p, i) => Vector3.Distance(p, mesh.Vertices[i])).DefaultIfEmpty().Max();
            if (worst > 1e-5f) Note($"{name}: a vertex is {worst} from the spec's");
        }
    }

    /// <summary>The caption's counts are those of the geometry placed, and at level 0 those of the spec's shapes.</summary>
    private static void CheckCounts(Placed blockout, RoverSpec.Shape[] shapes)
    {
        Equal("level 0: vertices of the draw", shapes.Sum(s => s.Positions.Length), blockout.VertexCount);
        Equal("level 0: faces of the draw", shapes.Sum(s => s.Faces.Length), blockout.FaceCount);
    }

    /// <summary>A whole-rover frame: its caption, every sure grid pixel, and the mirror and up checks.</summary>
    private static void CheckWhole(ScenarioFrame frame, Placed placed, Vector3 direction, string captionTerms, int least, bool orthographic = false, Vector3? up = null)
    {
        Equal($"{frame.Name}: size", (Size, Size), (frame.Width, frame.Height));
        if (frame.Caption is null || !frame.Caption.StartsWith($"{Size}x{Size}, ") || !frame.Caption.Contains(captionTerms) ||
            !frame.Caption.EndsWith($"{placed.VertexCount} vertices, {placed.FaceCount} faces"))
            Note($"{frame.Name}: the caption '{frame.Caption}' should hold '{captionTerms}' and end '{placed.VertexCount} vertices, {placed.FaceCount} faces'");
        var camera = PartsJudge.CameraFor(placed, direction, Size, orthographic, up);
        PartsJudge.AllRight(frame, placed, camera, frame.Name, least, Size);
        PartsJudge.TellsMirrorAndUp(frame, placed, camera, frame.Name, 500, Size);
    }

    /// <summary>Frame 2: level 0 from +x, +z and +y, one tile each (the +y tile with -Z up, as render takes it).</summary>
    private static async Task SheetAsync(ScenarioDriver driver, Placed placed, string name)
    {
        var sheet = await driver.CaptureAsync(name, new { level = 0, views = new[] { "+x", "+z", "+y" } });
        var tile = Size + 2;
        Equal($"{name}: size", (3 * Size + 2 * 2, Size), (sheet.Width, sheet.Height));
        if (sheet.Caption is null || !sheet.Caption.Contains("3x1 tiles of 512x512") || !sheet.Caption.Contains("views +x, +z, +y") || !sheet.Caption.Contains("level 0"))
            Note($"{name}: the caption '{sheet.Caption}' should hold '3x1 tiles of 512x512', 'views +x, +z, +y' and 'level 0'");
        var views = new (Vector3 Direction, Vector3? Up)[] { (Vector3.UnitX, null), (Vector3.UnitZ, null), (Vector3.UnitY, -Vector3.UnitZ) };
        for (var t = 0; t < 3; t++)
        {
            var camera = PartsJudge.CameraFor(placed, views[t].Direction, Size, up: views[t].Up);
            PartsJudge.AllRight(sheet, placed, camera, $"{name} tile {t}", 3000, Size, left: t * tile);
            PartsJudge.TellsMirrorAndUp(sheet, placed, camera, $"{name} tile {t}", 200, Size, left: t * tile);
        }
    }

    /// <summary>Frame 7: only Axle_F's subtree, framed on it, from [1, 0.3, 0]; each wheel's inner cap is seen with no boss on it.</summary>
    private static async Task AxleAsync(ScenarioDriver driver, RoverWorld world)
    {
        var drawn = world.Draw("Axle_F", null);
        var frame = await driver.CaptureAsync("part-axle-front", new { part = "Axle_F", direction = new[] { 1f, 0.3f, 0f } });
        if (frame.Caption is null || !frame.Caption.Contains("part Axle_F, ") || !frame.Caption.EndsWith($"{drawn.VertexCount} vertices, {drawn.FaceCount} faces"))
            Note($"{frame.Name}: the caption '{frame.Caption}' should hold 'part Axle_F, ' and end '{drawn.VertexCount} vertices, {drawn.FaceCount} faces'");
        var camera = PartsJudge.CameraFor(drawn, new Vector3(1, 0.3f, 0), Size);
        PartsJudge.AllRight(frame, drawn, camera, frame.Name, 2000, Size);
        foreach (var wheel in new[] { "Wheel_FL", "Wheel_FR" })
        {
            var cap = drawn.FacesOf(wheel).Start.Value + RoverSpec.WheelInnerCap;
            var (shown, wrong) = (0, 0);
            for (var y = 4; y < Size - 4; y++)
            for (var x = 4; x < Size - 4; x++)
            {
                if (drawn.Solid.SeenNarrow(camera, Size, x, y) != cap) continue;
                shown++;
                var (r, g, b, _) = frame.Pixel(x, y);
                var shade = drawn.Shade(cap, camera.Direction);
                if (Math.Abs(r - shade.R) > drawn.Tolerance(cap) || Math.Abs(g - shade.G) > drawn.Tolerance(cap) || Math.Abs(b - shade.B) > drawn.Tolerance(cap)) wrong++;
            }

            if (shown < InnerCapLeast) Note($"{frame.Name}: only {shown} pixels are sure to show {wheel}'s inner cap, fewer than {InnerCapLeast}");
            if (wrong > 0) Note($"{frame.Name}: {wrong} of {shown} pixels of {wheel}'s inner cap are not its dark shade (a boss turned inward, or an instance not turned)");
        }
    }

    /// <summary>Frames 8 and 9: Wheel_FL alone with its face ids (1024, beside its plain frame), and at level 0.</summary>
    private static async Task WheelAsync(ScenarioDriver driver, RoverWorld world)
    {
        const int Big = 1024;
        var drawn = world.Draw("Wheel_FL", null);
        var plain = await driver.CaptureAsync("part-wheel-plain", new { part = "Wheel_FL", size = Big });
        var labelled = await driver.CaptureAsync("part-wheel-labels", new { part = "Wheel_FL", labels = "faces", size = Big });
        var camera = PartsJudge.CameraFor(drawn, Front, Big);
        PartsJudge.AllRight(plain, drawn, camera, plain.Name, 20000, Big);
        if (labelled.Caption is null || !labelled.Caption.Contains("labels faces") || !labelled.Caption.Contains("part Wheel_FL, ") || !labelled.Caption.EndsWith($"{drawn.VertexCount} vertices, {drawn.FaceCount} faces"))
            Note($"{labelled.Name}: the caption '{labelled.Caption}' should hold 'labels faces', 'part Wheel_FL, ' and end '{drawn.VertexCount} vertices, {drawn.FaceCount} faces'");
        var (_, wrong) = PartsJudge.Judge(labelled, drawn, camera, Big);
        if (wrong.Count == 0) Note($"{labelled.Name}: no grid pixel differs from the plain wheel's: no labels are drawn");
        var added = 0;
        for (var y = 0; y < Big; y++)
        for (var x = 0; x < Big; x++)
            if (labelled.Pixel(x, y) != plain.Pixel(x, y)) added++;
        if (added < 800) Note($"{labelled.Name}: only {added} pixels differ from the plain frame: the face ids are not drawn");

        var level0 = world.Draw("Wheel_FL", 0);
        var frame = await driver.CaptureAsync("part-wheel-L0", new { part = "Wheel_FL", level = 0 });
        if (frame.Caption is null || !frame.Caption.Contains("part Wheel_FL, level 0, ") || !frame.Caption.EndsWith($"{level0.VertexCount} vertices, {level0.FaceCount} faces"))
            Note($"{frame.Name}: the caption '{frame.Caption}' should hold 'part Wheel_FL, level 0, ' and end '{level0.VertexCount} vertices, {level0.FaceCount} faces'");
        PartsJudge.AllRight(frame, level0, PartsJudge.CameraFor(level0, Front, Size), frame.Name, 1500, Size);
    }

    /// <summary>Frame 11, from above: one <c>move</c> of Wheel_FL's hub boss cap, and every wheel's boss changes; undo restores the frame.</summary>
    private static async Task InstanceEditAsync(ScenarioDriver driver, RoverWorld before, Placed live, ScenarioFrame top)
    {
        var (part, face, by) = RoverSpec.HubMove;
        var cap = before.Live(part).Faces[face];
        await CallJsonAsync(driver, "move", new { mesh = part, faces = new[] { face }, by = Floats(by) });
        var world = await RoverWorld.ReadAsync(driver);
        var (was, now) = (before.Live(part), world.Live(part));
        for (var v = 0; v < was.Vertices.Length; v++)
        {
            var expected = was.Vertices[v] + (cap.Contains(v) ? by : Vector3.Zero);
            if (Vector3.Distance(expected, now.Vertices[v]) > 1e-5f) Note($"instance-edit: vertex {v} of {part} is {V3(now.Vertices[v])}, expected {V3(expected)}");
        }

        var drawn = world.Draw(null, null);
        var frame = await driver.CaptureAsync("instance-edit", new { view = "+y", projection = "orthographic" });
        var camera = PartsJudge.CameraFor(drawn, Vector3.UnitY, Size, orthographic: true, up: -Vector3.UnitZ);
        if (frame.Caption is null || !frame.Caption.EndsWith($"{drawn.VertexCount} vertices, {drawn.FaceCount} faces"))
            Note($"{frame.Name}: the caption '{frame.Caption}' should end '{drawn.VertexCount} vertices, {drawn.FaceCount} faces'");
        PartsJudge.AllRight(frame, drawn, camera, frame.Name, 4000, Size);
        PartsJudge.TellsMirrorAndUp(frame, drawn, camera, frame.Name, 500, Size);

        // All four wheels changed: every pixel in the wheel's own screen box, one by one, where the new geometry shows the wheel and the old
        // geometry (the same camera) a shade more than 6 away. A boss moved 0.1 m is a few pixels wide from above, so the grid is too sparse.
        var old = before.Draw(null, null);
        foreach (var wheel in new[] { "Wheel_FL", "Wheel_FR", "Wheel_RL", "Wheel_RR" })
        {
            var faces = drawn.FacesOf(wheel);
            var boxes = drawn.Occurrences.Single(o => o.Part == wheel);
            var corners = Enumerable.Range(boxes.Start, boxes.Count).SelectMany(f => drawn.Solid.Faces[f].Loop).Select(v => camera.Pixel(drawn.Solid.Vertices[v])).ToArray();
            var (x0, x1, y0, y1) = (corners.Min(c => c.X) - 2, corners.Max(c => c.X) + 2, corners.Min(c => c.Y) - 2, corners.Max(c => c.Y) + 2);
            var changed = 0;
            for (var y = Math.Max(0, y0); y <= Math.Min(Size - 1, y1); y++)
            for (var x = Math.Max(0, x0); x <= Math.Min(Size - 1, x1); x++)
            {
                var (origin, ray) = camera.Ray(x, y);
                var seen = drawn.Solid.FirstHit(origin, ray);
                if (seen < faces.Start.Value || seen >= faces.End.Value) continue;
                var was2 = old.Solid.FirstHit(origin, ray);
                var other = was2 < 0 ? (48, 52, 56) : old.Shade(was2, camera.Direction);
                var shade = drawn.Shade(seen, camera.Direction);
                if (Math.Abs(other.Item1 - shade.R) > 6 || Math.Abs(other.Item2 - shade.G) > 6 || Math.Abs(other.Item3 - shade.B) > 6) changed++;
            }

            Console.Error.WriteLine($"parts: {wheel}: {changed} changed pixels");
            if (changed < 20) Note($"{frame.Name}: only {changed} pixels of {wheel} differ from the unedited rover's, fewer than 20: the edit did not reach it");
        }

        await CallJsonAsync(driver, "undo", new { });
        var back = await driver.CaptureAsync("instance-undone", new { view = "+y", projection = "orthographic" });
        driver.AssertBytesEqual("instance-undone against top", top.Png, back.Png);
        _ = live;
    }

    /// <summary>Frame 6: define_part on the Mast turns it; the lamp is at -Z, and the frame is judged against the mast placed turned.</summary>
    private static async Task MastAsync(ScenarioDriver driver, RoverWorld world)
    {
        var turned = RoverSpec.MastTurned;
        await CallJsonAsync(driver, "define_part", new { part = "Mast", rotation = new[] { turned.X, turned.Y, turned.Z, turned.W } });
        world.MastRotation = turned;
        var drawn = world.Draw(null, null);
        var lamp = drawn.FacesOf("Lamp");
        var lampVertices = Enumerable.Range(lamp.Start.Value, lamp.End.Value - lamp.Start.Value).SelectMany(f => drawn.Solid.Faces[f].Loop).Select(v => drawn.Solid.Vertices[v]).ToArray();
        var lampSide = lampVertices.Max(v => v.Z);
        if (lampSide > 0.001f) Note($"mast-turned: the lamp reaches z {lampSide}, not entirely on the -Z side");
        var frame = await driver.CaptureAsync("mast-turned", new { });
        CheckWhole(frame, drawn, Front, "view front, ", 4000);
        var parts = await CallJsonAsync(driver, "get_parts", new { });
        var lampPart = parts.GetProperty("parts").EnumerateArray().Single(p => p.GetProperty("name").GetString() == "Lamp");
        var bounds = lampPart.GetProperty("world_bounds");
        var (min, max) = (LocalMesh.V(bounds.GetProperty("min")), LocalMesh.V(bounds.GetProperty("max")));
        if (max.Z > 0.001f || min.Z > -0.25f) Note($"mast-turned: get_parts gives the lamp's world z as {min.Z}..{max.Z}, not behind the mast at -Z");
        var (lo, hi) = (lampVertices.Aggregate(Vector3.Min), lampVertices.Aggregate(Vector3.Max));
        if (Vector3.Distance(lo, min) > 1e-3f || Vector3.Distance(hi, max) > 1e-3f)
            Note($"mast-turned: get_parts gives the lamp's world bounds {V3(min)}..{V3(max)}, the geometry placed gives {V3(lo)}..{V3(hi)}");
    }

    /// <summary>check on each part: ok, no problems, no warnings.</summary>
    private static async Task CheckSilentAsync(ScenarioDriver driver, string mesh)
    {
        var check = await CallJsonAsync(driver, "check", new { mesh });
        if (!check.GetProperty("ok").GetBoolean() || check.GetProperty("warnings").GetArrayLength() != 0 || check.GetProperty("problems").GetArrayLength() != 0)
            Note($"check {mesh}: not silent: {check}");
    }

    /// <summary>Axle_F lowered 0.3 m gives exactly T-91's envelope warning, in check and in get_parts, and the scenario puts it back.</summary>
    private static async Task EnvelopeAsync(ScenarioDriver driver)
    {
        var axle = RoverSpec.Find("Axle_F");
        var drop = axle.Translation.Y - RoverSpec.AxleLoweredTranslation.Y;
        var warning = $"'Axle_F': its envelope, placed in 'Chassis', goes below the envelope of 'Chassis' on y by {drop:0.###} m";
        await CallJsonAsync(driver, "define_part", new { part = "Axle_F", translation = Floats(RoverSpec.AxleLoweredTranslation) });
        foreach (var mesh in new[] { "Chassis", "Wheel_FL" })
        {
            var check = await CallJsonAsync(driver, "check", new { mesh });
            SameList($"check {mesh} with Axle_F lowered", [warning], check.GetProperty("warnings").EnumerateArray().Select(w => w.GetString()!));
            if (check.GetProperty("problems").GetArrayLength() != 0) Note($"check {mesh} with Axle_F lowered: problems {check.GetProperty("problems")}");
        }

        var listed = (await CallJsonAsync(driver, "get_parts", new { })).GetProperty("parts").EnumerateArray().ToArray();
        SameList("get_parts' Axle_F warnings", [warning], listed.Single(p => p.GetProperty("name").GetString() == "Axle_F").GetProperty("warnings").EnumerateArray().Select(w => w.GetString()!));
        Equal("get_parts' other warnings", 0, listed.Where(p => p.GetProperty("name").GetString() != "Axle_F").Sum(p => p.GetProperty("warnings").GetArrayLength()));

        await CallJsonAsync(driver, "define_part", new { part = "Axle_F", translation = Floats(axle.Translation) });
        foreach (var mesh in RoverSpec.GeometryParts) await CheckSilentAsync(driver, mesh);
        Equal("get_parts' warnings once Axle_F is back", 0, (await CallJsonAsync(driver, "get_parts", new { })).GetProperty("parts").EnumerateArray().Sum(p => p.GetProperty("warnings").GetArrayLength()));
    }

    /// <summary>get_parts gives the tree as the spec has it (the mast as the scenario now holds it), with the geometry's own counts, levels and world bounds.</summary>
    private static async Task CheckPartsAsync(ScenarioDriver driver, RoverWorld world, bool refined)
    {
        var parts = (await CallJsonAsync(driver, "get_parts", new { })).GetProperty("parts").EnumerateArray().ToArray();
        SameList("get_parts' names, in pre-order", RoverSpec.Tree.Select(p => p.Name), parts.Select(p => p.GetProperty("name").GetString()!));
        foreach (var (spec, got) in RoverSpec.Tree.Zip(parts))
        {
            var what = $"get_parts {spec.Name}";
            Equal($"{what}: parent", spec.Parent, got.GetProperty("parent").GetString());
            Equal($"{what}: instance_of", spec.InstanceOf, got.TryGetProperty("instance_of", out var i) ? i.GetString() : null);
            Equal($"{what}: moves", spec.Moves, got.GetProperty("moves").GetBoolean());
            Near($"{what}: translation", Floats(spec.Translation), got.GetProperty("translation"));
            Near($"{what}: rotation", [spec.Rotation.X, spec.Rotation.Y, spec.Rotation.Z, spec.Rotation.W], got.GetProperty("rotation"));
            if (spec.Envelope is { } e)
            {
                Near($"{what}: envelope min", Floats(e.Min), got.GetProperty("envelope").GetProperty("min"));
                Near($"{what}: envelope max", Floats(e.Max), got.GetProperty("envelope").GetProperty("max"));
            }
            else if (got.GetProperty("envelope").ValueKind != JsonValueKind.Null) Note($"{what}: envelope {got.GetProperty("envelope")}, expected none");

            var source = spec.InstanceOf ?? spec.Name;
            var owns = RoverSpec.GeometryParts.Contains(source);
            Equal($"{what}: geometry", spec.InstanceOf is null && owns, got.GetProperty("geometry").GetBoolean());
            int[] levels = !owns ? [] : refined && RoverSpec.RefinedParts.Contains(source) ? [0, 1] : [0];
            SameList($"{what}: levels", levels.Select(n => n.ToString()), got.GetProperty("levels").EnumerateArray().Select(n => n.GetInt32().ToString()));
            if (!owns)
            {
                if (got.GetProperty("world_bounds").ValueKind != JsonValueKind.Null) Note($"{what}: world_bounds {got.GetProperty("world_bounds")}, expected none for an empty");
                continue;
            }

            var mesh = world.Live(source);
            Equal($"{what}: vertices", mesh.LiveVertexCount, got.GetProperty("vertices").GetInt32());
            Equal($"{what}: faces", mesh.LiveFaceCount, got.GetProperty("faces").GetInt32());
            var placed = Enumerable.Range(0, mesh.Vertices.Length).Where(v => mesh.LiveVertex[v]).Select(v => Vector3.Transform(mesh.Vertices[v], world.WorldOf(spec.Name))).ToArray();
            Near($"{what}: world min", Floats(placed.Aggregate(Vector3.Min)), got.GetProperty("world_bounds").GetProperty("min"));
            Near($"{what}: world max", Floats(placed.Aggregate(Vector3.Max)), got.GetProperty("world_bounds").GetProperty("max"));
        }
    }

    private static void Near(string what, float[] expected, JsonElement actual)
    {
        var got = actual.EnumerateArray().Select(x => x.GetSingle()).ToArray();
        if (got.Length != expected.Length || expected.Zip(got).Any(z => Math.Abs(z.First - z.Second) > 1e-4f))
            Note($"{what}: expected [{string.Join(", ", expected)}], got {actual}");
    }
}
