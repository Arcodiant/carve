using System.Numerics;
using Carve.Debug;
using Carve.Scenario.Orcward;
using Carve.Scenario.Uvs;
using static Carve.Scenario.Crate.CrateBuild;
using static Carve.Scenario.Weathered.WeatheredSets;

namespace Carve.Scenario.Weathered;

/// <summary>
/// Stage 3: the re-bakes, each asserting <c>baked</c> in render's answer and the frame, all from stage 2's front at 512. The same render again
/// (nothing baked, byte-identical); the top hoop's rim moved outward 0.01 (baked, the staves beside it darker, the bottom hoop's pixels unchanged);
/// undone (nothing baked, byte-identical); the hoops unwrapped with another margin (baked, within D-6's rule, the <c>uvs</c> look different), undone;
/// the run folder's iron graph given <c>wear_amount</c> 0 (baked, the rim at most 2 luma over the hoop top's middle, ruled 2026-11-24), restored
/// (byte-identical).
/// </summary>
internal sealed class RebakeStage : IWeatheredStage
{
    public async Task RunAsync(WeatheredRun run)
    {
        var driver = run.Driver;
        var mesh = await Scene.ReadOneAsync(driver, "mesh");
        var scene = await Scene.ReadAsync(driver, "mesh");
        var sets = Of(mesh, WeatheredRun.BakeSize);
        var front = new Camera(SeenStage.Front);
        var stage2 = run.Frames[$"w{WeatheredRun.BakeSize}-front"];

        var again = await driver.CaptureAsync("rebake-again", front.Args());
        Baked(run, "the same render again", again, false);
        Same(run, "the same render again", stage2, again);

        // The top hoop's faces, each moved outward from the axis by 0.01 (extrude gave every hoop face its own four vertices).
        var top = Barrel.Hoops.Where(f => mesh.Centre(f).Y > (mesh.BoundsMin.Y + mesh.BoundsMax.Y) / 2).ToArray();
        foreach (var f in top)
        {
            var c = mesh.Centre(f);
            var by = Vector3.Normalize(new Vector3(c.X, 0, c.Z)) * 0.01f;
            await CallJsonAsync(driver, "move", new { faces = new[] { f }, by = new[] { by.X, by.Y, by.Z } });
        }

        var moved = await Scene.ReadOneAsync(driver, "mesh");
        if (moved.BoundsMin != mesh.BoundsMin || moved.BoundsMax != mesh.BoundsMax) run.Note($"the move changed the barrel's bounds ({mesh.BoundsMin}..{mesh.BoundsMax} to {moved.BoundsMin}..{moved.BoundsMax}), so the frames are framed anew");
        var after = await driver.CaptureAsync("rebake-moved", front.Args());
        Baked(run, "after the move", after, true);
        // The staves beside the moved hoop, in stage 2's close on them (zoom 4, so few pixels lie on the hoop's edge), read darker. The stale break:
        // the checks read stage 2's frames instead of the moved ones.
        var stave = SeenStage.Closes(sets).Stave;
        var close = await driver.CaptureAsync("rebake-moved-close-stave", stave.Args());
        var closeBefore = run.Frames[$"w{WeatheredRun.BakeSize}-close-stave"];
        var (read, readClose) = WeatheredRun.Break == "stale" ? (stage2, closeBefore) : (after, close);
        // The points in sight after the move as well as before: the raised hoop hides some of the staves beside it.
        var movedScene = await Scene.ReadAsync(driver, "mesh");
        var now = Seen(readClose, movedScene, moved, stave, sets[Beside].Where(t => t.Top));
        var kept = now.Select(e => e.Texel).ToHashSet();
        var was = Seen(closeBefore, scene, mesh, stave, sets[Beside].Where(t => t.Top && kept.Contains(t)));
        double M((Texel, (byte R, byte G, byte B, byte A) P)[] s) => s.Select(e => Luma(e.P)).DefaultIfEmpty(double.NaN).Average();
        run.Log.Add($"after the move: {Beside} of the top hoop in the close, {was.Length} seen, luma {F(M(was))} before and {F(M(now))} after");
        // Ruled 2026-11-24: darker by at least 4 luma in this close (measured 7.9). The front is not held to it: the raised hoop hides those pixels there.
        if (was.Length < 8) run.Note($"after the move: only {was.Length} staves beside the moved hoop seen both before and after, at least 8 needed");
        run.AtLeast("after the move: close on the stave, staves beside the moved hoop, stage 2's minus now", M(was), M(now), 4);
        // The grime texels beside the moved hoop, before (stage 2's look) and after, to tell the bake from the shading.
        var grimeAfter = await driver.CaptureAsync("rebake-moved-uvs-grime", new { mode = "uvs", image = new { graph = Barrel.Graph(run, "weathered-staves"), output = "grime", size = WeatheredRun.BakeSize }, size = UvFrame });
        var besideTop = sets[Beside].Where(t => t.Top).ToArray();
        run.Log.Add($"after the move: grime of the {besideTop.Length} {Beside} texels of the top hoop, mean {F(Values(run.Frames[$"w{WeatheredRun.BakeSize}-uvs-grime"], besideTop).Average())} before and {F(Values(grimeAfter, besideTop).Average())} after");
        var bottom = Names.Where(n => n != StaveMiddle).SelectMany(n => sets[n].Where(t => !t.Top)).ToArray();
        var (b0, b1) = (Seen(stage2, scene, mesh, front, bottom), Seen(read, scene, mesh, front, bottom));
        var changed = b0.Zip(b1).Count(e => e.First.Pixel != e.Second.Pixel);
        run.Log.Add($"after the move: the bottom hoop's {b0.Length} seen points, {changed} changed");
        if (b0.Length < 8 || changed != 0) run.Note($"after the move: {changed} of the bottom hoop's {b0.Length} seen points changed (none may, and at least 8 must be seen)");

        foreach (var _ in top) await CallJsonAsync(driver, "undo", new { });
        var undone = await driver.CaptureAsync("rebake-undone", front.Args());
        Baked(run, "after undo", undone, false);
        Same(run, "after undo", stage2, undone);

        // The UVs changed: the hoops and their walls (not the caps, whose UVs stay with the staves') unwrapped again with another margin.
        var uvsBefore = await driver.CaptureAsync("rebake-uvs-before", new { mode = "uvs", size = Shaded });
        var hoops = Barrel.Rest(mesh.Faces.Length).Where(f => !Barrel.Caps.Contains(f)).ToArray();
        // Into the widest part of the rest band clear of the caps' UVs, since the bake refuses a hoop texel that also shows a cap.
        var capUvs = Barrel.Caps.SelectMany(f => mesh.Uvs[f]).ToArray();
        var (c0, c1) = (capUvs.Aggregate(Vector2.Min) - new Vector2(0.01f), capUvs.Aggregate(Vector2.Max) + new Vector2(0.01f));
        var (r0, r1) = (new Vector2(Barrel.RestBand[0], Barrel.RestBand[1]), new Vector2(Barrel.RestBand[2], Barrel.RestBand[3]));
        var into = new[] { new[] { r0.X, r0.Y, c0.X, r1.Y }, [c1.X, r0.Y, r1.X, r1.Y], [r0.X, r0.Y, r1.X, c0.Y], [r0.X, c1.Y, r1.X, r1.Y] }
            .MaxBy(b => Math.Max(0, b[2] - b[0]) * Math.Max(0, b[3] - b[1]))!;
        run.Log.Add($"the unwrap: caps' UVs {c0}..{c1}, the hoops into [{string.Join(", ", into.Select(v => F(v)))}]");
        await CallJsonAsync(driver, "unwrap", new { faces = hoops, into, margin = 0.03 });
        var uvsAfter = await driver.CaptureAsync("rebake-uvs-after", new { mode = "uvs", size = Shaded });
        if (uvsBefore.Pixels.AsSpan().SequenceEqual(uvsAfter.Pixels)) run.Note("the unwrap with another margin left the uvs look unchanged");
        var unwrapped = await driver.CaptureAsync("rebake-unwrapped", front.Args());
        Baked(run, "after the unwrap", unwrapped, true);
        var whole = Comparison.Measure("rebake-unwrapped", "good", stage2, unwrapped);
        run.Log.Add($"after the unwrap: {whole.Differing} of {whole.Total} pixels differ from stage 2's front, largest patch {whole.LargestPatch} (the hoops' noise is laid out in UV space)");
        Comparison.WriteTriptych(run.InRun(Path.Combine("compare", "rebake-unwrapped.png")), stage2, unwrapped);
        // Ruled 2026-11-24: D-6's rule over the pixels that show a stave or a cap (their UVs did not change), clear of the hoops by HoopClear px.
        var mask = StavesAndCaps(mesh, front.Predicted(mesh.BoundsMin, mesh.BoundsMax));
        var (differing, total, patch) = Masked(stage2, unwrapped, mask);
        run.Log.Add($"after the unwrap: on the {total} stave and cap pixels {HoopClear} px clear of the hoops, {differing} differ, largest patch {patch}");
        if (total < 1000 || (long)differing * 200 > total || patch > Comparison.PatchLimit) run.Note($"after the unwrap: the front's {total} stave and cap pixels differ from stage 2's in {differing}, largest patch {patch} (D-6's rule: 0.5 % or a patch of {Comparison.PatchLimit}; at least 1000 pixels)");
        // And the hoops held to the ruled threshold (1) again on the new layout.
        run.AtLeast("after the unwrap: front, shaded, hoop rim minus hoop top middle", M(Seen(unwrapped, scene, mesh, front, sets[Rim])), M(Seen(unwrapped, scene, mesh, front, sets[HoopMiddle])), 8);
        await CallJsonAsync(driver, "undo", new { });

        // The run folder's iron graph given wear_amount 0, written here; the product is not told.
        var graph = Barrel.Graph(run, "weathered-iron");
        var text = File.ReadAllText(graph);
        const string Worn = "\"wear_amount\": { \"value\": 1,";
        if (!text.Contains(Worn, StringComparison.Ordinal)) run.Note($"weathered-iron.texgraph: no '{Worn}' to rewrite");
        File.WriteAllText(graph, text.Replace(Worn, "\"wear_amount\": { \"value\": 0,", StringComparison.Ordinal));
        var clean = await driver.CaptureAsync("rebake-no-wear", front.Args());
        // The no-rebake break: nothing expected baked after the graph edit.
        Baked(run, "after wear_amount 0", clean, WeatheredRun.Break != "no-rebake");
        var (rim, middle) = (Seen(clean, scene, mesh, front, sets[Rim]), Seen(clean, scene, mesh, front, sets[HoopMiddle]));
        run.AtMost("after wear_amount 0: shaded, hoop rim minus hoop top middle", M(rim), M(middle), 2);

        File.WriteAllText(graph, text);
        var restored = await driver.CaptureAsync("rebake-restored", front.Args());
        // The restored graph's bake is in the cache: nothing is baked (T-167's record; RV-82).
        Baked(run, "after the graph's restore", restored, false);
        Same(run, "after the graph's restore", stage2, restored);
    }

    /// <summary>Notes a render whose answer has (or lacks) render's <c>baked:</c> line against <paramref name="expected"/>.</summary>
    public static void Baked(WeatheredRun run, string what, ScenarioFrame frame, bool expected)
    {
        var line = frame.Caption?.Split('\n').FirstOrDefault(l => l.StartsWith("baked: ", StringComparison.Ordinal));
        run.Log.Add($"{what}: {line ?? "nothing baked"}");
        if ((line is not null) != expected) run.Note($"{what}: expected {(expected ? "a bake" : "nothing baked")}, render said {line ?? "nothing baked"}");
    }

    /// <summary>The margin, in pixels of the 512 front, by which a stave or cap pixel must be clear of every pixel showing a hoop face: rule P2's 3 px at 512.</summary>
    public const int HoopClear = 3;

    /// <summary>
    /// The pixels of a 512 frame whose centre's ray meets a stave or a cap first (a ray test over every face of <c>get_mesh</c>'s geometry), with no
    /// pixel showing a hoop face (a hoop top or wall) within <see cref="HoopClear"/> px.
    /// </summary>
    public static bool[] StavesAndCaps(SceneMesh mesh, Camera camera)
    {
        var faces = Enumerable.Range(0, mesh.Faces.Length).Select(f => new Carve.Scenario.Materials.FaceGeo(f, mesh.Faces[f].Select(v => mesh.Vertices[v]).ToArray(), mesh.Uvs.TryGetValue(f, out var uv) ? uv : null)).ToArray();
        var shot = new Carve.Scenario.Materials.Shot(faces, mesh.BoundsMin, mesh.BoundsMax, camera.Direction, camera.Size);
        var n = camera.Size;
        var face = new int[n * n];
        for (var y = 0; y < n; y++)
        for (var x = 0; x < n; x++)
            face[y * n + x] = shot.At(x, y)?.Face.Face ?? -1;

        bool Kept(int f) => Barrel.Staves.Contains(f) || Barrel.Caps.Contains(f);
        var mask = face.Select(Kept).ToArray();
        for (var p = 0; p < face.Length; p++)
        {
            if (face[p] < 0 || Kept(face[p])) continue;
            int px = p % n, py = p / n;
            for (var dy = -HoopClear; dy <= HoopClear; dy++)
            for (var dx = -HoopClear; dx <= HoopClear; dx++)
            {
                int qx = px + dx, qy = py + dy;
                if (qx >= 0 && qy >= 0 && qx < n && qy < n && dx * dx + dy * dy <= HoopClear * HoopClear) mask[qy * n + qx] = false;
            }
        }

        return mask;
    }

    /// <summary>D-6's measure over the masked pixels only: how many differ, of how many, and the largest 8-connected patch of them.</summary>
    public static (int Differing, int Total, int Patch) Masked(ScenarioFrame a, ScenarioFrame b, bool[] mask)
    {
        var differs = new bool[mask.Length];
        var count = 0;
        for (var p = 0; p < mask.Length; p++)
            if (mask[p] && Comparison.Differs(a, b, p * 4)) (differs[p], count) = (true, count + 1);
        return (count, mask.Count(m => m), Comparison.LargestPatchOf(differs, a.Width, a.Height));
    }

    public static void Same(WeatheredRun run, string what, ScenarioFrame expected, ScenarioFrame actual)
    {
        if (!expected.Pixels.AsSpan().SequenceEqual(actual.Pixels)) run.Note($"{what}: the frame {actual.Name} is not byte-identical to {expected.Name}");
    }
}
