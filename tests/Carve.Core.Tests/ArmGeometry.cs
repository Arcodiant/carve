using System.Numerics;
using Carve.Scenario.Morph;
using Xunit;

namespace Carve.Core.Tests;

/// <summary>
/// T-105's arm, worked through Carve.Core before any frame (as <see cref="GatehouseGeometry"/> works the gatehouse): the counts, the silhouette's
/// half-height at x = 0.12 and 0.42 for every frame's weights, the planted fault, and the ray test of the pixels the scenario will ask about
/// (<see cref="ArmPlan"/>, the scenario's own camera and ray test). A planned count or check that cannot pass on this geometry is a stop, not a change.
/// </summary>
[Trait("Tier", "unit")]
public class ArmGeometry
{
    private const int Size = 512;

    private static Vector3 V(double[] v) => new((float)v[0], (float)v[1], (float)v[2]);

    /// <summary>The cage with smooth shading, targets Bulk and Lean, and the rig's weights (Shoulder 0, Elbow 1).</summary>
    private static PolygonMesh Cage()
    {
        var mesh = TestMeshes.Make(ArmPlan.Cage().Select(V), ArmPlan.Faces()).WithShade(MeshShade.Of(ShadingMode.Smooth, []));
        var bulk = MorphTarget.FromPositions(mesh, "Bulk", ArmPlan.Bulk().Select(p => KeyValuePair.Create(int.Parse(p.Key), V(p.Value))));
        Assert.True(bulk.Succeeded, string.Join("; ", bulk.Errors));
        var lean = MorphTarget.FromPositions(mesh, "Lean", ArmPlan.Cage(ArmPlan.LeanScale).Select(V).ToArray());
        Assert.True(lean.Succeeded, string.Join("; ", lean.Errors));
        var byVertex = new List<KeyValuePair<int, Influence[]>>();
        foreach (var (vertices, weights) in ArmPlan.RigWeights())
            foreach (var v in vertices)
                byVertex.Add(KeyValuePair.Create(v, weights.Select(w => new Influence(w.Key == "Shoulder" ? 0 : 1, w.Value)).ToArray()));
        return mesh.WithTargets(MorphTargets.Of([bulk.Target!, lean.Target!])).WithWeights(VertexWeights.Of(byVertex));
    }

    private static PolygonMesh Subdivided() => TestMeshes.Ok(MeshEdits.Subdivide(Cage(), 1));

    private static ArmPlan.Soup Soup(PolygonMesh m) =>
        new([.. m.Vertices], [.. m.LiveFaces.Select(f => m.Faces[f].Loop.ToArray())]);

    private static Dictionary<string, float> W(params (string, float)[] w) => w.ToDictionary(p => p.Item1, p => p.Item2);

    private static readonly (string Name, Dictionary<string, float> Weights, float Ratio)[] Looks =
    [
        ("rest", W(), 1f),
        ("bulk 1", W(("Bulk", 1)), 1.35f),
        ("bulk .5 lean .5", W(("Bulk", .5f), ("Lean", .5f)), 1.075f),
        ("lean 1", W(("Lean", 1)), 0.8f),
    ];

    [Fact]
    public void CountsOfTheCageAndTheSubdividedArm()
    {
        var cage = Cage();
        Assert.Equal((88, 82), (cage.LiveVertexCount, cage.LiveFaceCount));
        var edges = cage.LiveFaces.SelectMany(f => cage.Faces[f].Loop.Select((v, k) => (Math.Min(v, cage.Faces[f].Loop[(k + 1) % cage.Faces[f].Loop.Count]), Math.Max(v, cage.Faces[f].Loop[(k + 1) % cage.Faces[f].Loop.Count])))).Distinct().Count();
        Assert.Equal(168, edges);
        Assert.Equal(48, cage.Targets!.Find("Bulk")!.Count);
        Assert.Equal(["Bulk", "Lean"], cage.Targets.Names);

        var sub = Subdivided();
        Assert.Equal((338, 336), (sub.LiveVertexCount, sub.LiveFaceCount));
        Assert.All(sub.LiveFaces, f => Assert.Equal(4, sub.Faces[f].Loop.Count));
        Assert.Equal(672, sub.LiveFaces.Sum(f => sub.Faces[f].Loop.Count - 2));
        Assert.Equal(ShadingMode.Smooth, sub.ShadingMode);
        Assert.Equal(["Bulk", "Lean"], sub.Targets!.Names);
        Assert.True(sub.Targets.Find("Bulk")!.Count >= 48);
    }

    [Fact]
    public void SilhouetteHalfHeightAtTheTwoRingsFollowsTheRingFactor()
    {
        var sub = Subdivided();
        var rest = Soup(sub);
        foreach (var x in new[] { 0.12f, 0.42f })
        {
            var half = ArmPlan.Silhouette(rest, x).High;
            foreach (var (name, weights, ratio) in Looks)
            {
                var soup = rest.With([.. sub.Morphed(weights).Vertices]);
                var (low, high) = ArmPlan.Silhouette(soup, x);
                Assert.True(MathF.Abs(high - ratio * half) < 1e-5f, $"{name} at x {x}: {high} against {ratio} x {half}");
                Assert.True(MathF.Abs(low + high) < 1e-5f, $"{name} at x {x}: the silhouette is not symmetric ({low}, {high})");
            }
        }

        // The measured numbers, pinned so a change of the arm or of subdivide is seen.
        Assert.Equal(0.05306f, ArmPlan.Silhouette(rest, 0.12f).High, 1e-5f);
        Assert.Equal(0.04356f, ArmPlan.Silhouette(rest, 0.42f).High, 1e-5f);
    }

    [Fact]
    public void BulkAndLeanBreakNothingAtWeightOneAndBadFlipsFaces()
    {
        var sub = Subdivided();
        Assert.DoesNotContain(MeshHealth.Create(sub).Problems, p => p.Check.StartsWith("target-"));

        var ids = ArmPlan.BadVertices();
        Assert.Equal(3, ids.Length);
        Assert.Equal([Id(2, 0), Id(2, 1), Id(2, 7)], ids);
        var bad = MorphTarget.FromPositions(sub, "Bad", ids.Select(i => KeyValuePair.Create(i, new Vector3(sub.Vertices[i].X, (float)ArmPlan.BadY, sub.Vertices[i].Z))));
        Assert.True(bad.Succeeded, string.Join("; ", bad.Errors));
        var faulty = sub.WithTargets(sub.Targets!.With(bad.Target!));
        var problems = MeshHealth.Create(faulty).Problems.Where(p => p.Check.StartsWith("target-")).ToArray();
        Assert.Contains(problems, p => p.Check == "target-flips-face" && p.Element == "targets['Bad']");
        Assert.All(problems, p => Assert.Equal("targets['Bad']", p.Element));
        Assert.DoesNotContain(MeshHealth.Create(faulty.WithTargets(faulty.Targets!.Without("Bad"))).Problems, p => p.Check.StartsWith("target-"));
    }

    private static int Id(int ring, int side) => ArmPlan.Id(ring, side);

    private static ArmPlan.Cam Side(ArmPlan.Soup rest, bool mirror = false)
    {
        var (min, max) = rest.Bounds();
        return ArmPlan.Orthographic(min, max, Size, mirror);
    }

    private static int Wrong(ArmPlan.Soup soup, ArmPlan.Cam cam, IEnumerable<ArmPlan.Probe> probes) =>
        probes.Count(p => ArmPlan.Seen(soup, cam, p.X, p.Y, ArmPlan.ProbeReach) is { } seen && seen != p.Drawn);

    [Fact]
    public void EveryFrameSilhouettePixelIsCertainAndTheBreaksAreCaught()
    {
        var sub = Subdivided();
        var rest = Soup(sub);
        var cam = Side(rest);
        var mirrored = Side(rest, true);
        var lines = new List<string>();
        var shown = new Dictionary<string, (ArmPlan.Soup Soup, List<ArmPlan.Probe> Probes)>();
        foreach (var (name, weights, _) in Looks)
        {
            var soup = rest.With([.. sub.Morphed(weights).Vertices]);
            var probes = ArmPlan.SilhouetteProbes(soup, cam, ArmPlan.ProbeMargin);
            Assert.Equal(ArmPlan.ProbeXs().Length * 4, probes.Count);
            // Each probe pixel is one the ray test is certain of and agrees with: nothing sits on an edge.
            foreach (var p in probes) Assert.True(ArmPlan.Seen(soup, cam, p.X, p.Y, ArmPlan.ProbeReach) == p.Drawn, $"{name}: {p.What} at pixel ({p.X}, {p.Y}) is not certain");
            shown[name] = (soup, probes);
            // A camera from the other side puts the wrist at the left: some probe must disagree.
            var mirroredWrong = Wrong(soup, mirrored, probes);
            lines.Add($"{name}: {probes.Count} silhouette probes, mirrored camera disagrees at {mirroredWrong}, sweep {ArmPlan.Sweep(soup, cam, 8).Count}");
            Assert.True(mirroredWrong >= 8, $"{name}: a mirrored camera disagrees at only {mirroredWrong} probes");
        }

        // Weights ignored: each weighted frame's probes, asked of the rest drawing, must fail.
        foreach (var (name, _, _) in Looks.Skip(1))
        {
            var wrong = Wrong(rest, cam, shown[name].Probes);
            lines.Add($"{name}: weights ignored disagrees at {wrong}");
            Assert.True(wrong >= 4, $"{name}: weights ignored disagrees at only {wrong} probes");
        }

        File.WriteAllLines(Path.Combine(Path.GetTempPath(), "arm-explore.txt"), lines);
    }

    private static float[] ElbowWeights(PolygonMesh mesh) =>
        [.. Enumerable.Range(0, mesh.Vertices.Count).Select(v => (float)mesh.Weights!.Of(v).Where(i => i.Bone == 1).Sum(i => i.Weight))];

    [Fact]
    public void TheCurledArmStandsOnTheElbowAndItsFrameIsCertain()
    {
        var sub = Subdivided();
        var rest = Soup(sub);
        var cam = Side(rest);
        var bulk = rest.With([.. sub.Morphed(W(("Bulk", 1))).Vertices]);
        var curled = bulk.With(ArmPlan.Curl(bulk.V, ElbowWeights(sub), 90));

        // The forearm (rings 6 to 10, Elbow only) is a rigid quarter turn of the morphed positions about (0.3, 0, 0): ring 10's centre is at (0.3, 0.3).
        var wrist = Enumerable.Range(ArmPlan.Id(10, 0), 8).Select(v => curled.V[v]).Aggregate(Vector3.Zero, (a, b) => a + b) / 8;
        Assert.True(Vector3.Distance(wrist, new Vector3(0.3f, 0.3f - 0.0f, 0)) < 0.02f, $"the wrist ring is at {wrist}");
        Assert.True(wrist.Y > 0.25f && MathF.Abs(wrist.X - 0.3f) < 0.02f);

        foreach (var (what, soup, isCurled) in new[] { ("curled", curled, true), ("at rest", rest, false) })
        {
            var probes = ArmPlan.CurlProbes(cam, isCurled);
            foreach (var p in probes) Assert.True(ArmPlan.Seen(soup, cam, p.X, p.Y, ArmPlan.ProbeReach) == p.Drawn, $"{what}: {p.What} at pixel ({p.X}, {p.Y})");
        }

        // A product that bends the other way, or not at all, fails a guard.
        var back = bulk.With(ArmPlan.Curl(bulk.V, ElbowWeights(sub), -90));
        Assert.Contains(ArmPlan.CurlProbes(cam, true), p => ArmPlan.Seen(back, cam, p.X, p.Y, ArmPlan.ProbeReach) is { } seen && seen != p.Drawn);
        Assert.Contains(ArmPlan.CurlProbes(cam, true), p => ArmPlan.Seen(bulk, cam, p.X, p.Y, ArmPlan.ProbeReach) is { } seen && seen != p.Drawn);

        // The silhouette of the curled arm: the upper arm's rings 0 to 4 are where they were, so the sweep's sure pixels there are those of the morphed rest.
        var sweep = ArmPlan.Sweep(curled, cam, 8);
        Assert.True(sweep.Count > 500, $"{sweep.Count} sure pixels");
        Assert.NotEqual(0, sweep.Count(p => p.Drawn));

        // Morph after skin: Bulk's deltas are radial in the rest pose; applied after the turn they would stretch the forearm along its length, and the forearm's
        // width at ring 7 would stay the rest's. The curled forearm's width there is Bulk's.
        var after = rest.With(ArmPlan.Curl(rest.V, ElbowWeights(sub), 90));
        var afterMorph = after.With([.. after.V.Select((p, i) => p + (bulk.V[i] - rest.V[i]))]);
        var wrongAfter = ArmPlan.Sweep(curled, cam, 8).Count(p => ArmPlan.Seen(afterMorph, cam, p.X, p.Y) is { } seen && seen != p.Drawn);
        Assert.True(wrongAfter >= 10, $"morph after skin disagrees at only {wrongAfter} pixels");
    }

    [Fact]
    public void TheCornerViewAndThePlantedFaultAreCertain()
    {
        var sub = Subdivided();
        var rest = Soup(sub);
        var (min, max) = rest.Bounds();
        var corner = ArmPlan.Perspective(min, max, ArmPlan.FrontDirection, Size);
        var bulk = rest.With([.. sub.Morphed(W(("Bulk", 1))).Vertices]);
        var sweep = ArmPlan.Sweep(bulk, corner, 8);
        Assert.True(sweep.Count > 400, $"{sweep.Count} sure pixels in the corner view");
        Assert.True(sweep.Count(p => p.Drawn) > 100 && sweep.Count(p => !p.Drawn) > 100);
        // Bulk shows in this view: the rest drawing disagrees at some sure pixels.
        Assert.True(sweep.Count(p => ArmPlan.Seen(rest, corner, p.X, p.Y) is { } seen && seen != p.Drawn) >= 20);

        // Bad at 1: ring 2's three vertices at y = -0.08, seen from +Z.
        var side = Side(rest);
        var positions = (Vector3[])rest.V.Clone();
        foreach (var v in ArmPlan.BadVertices()) positions[v] = new Vector3(positions[v].X, (float)ArmPlan.BadY, positions[v].Z);
        var bad = rest.With(positions);
        var probes = ArmPlan.Sure(bad, side, ArmPlan.SilhouetteProbes(bad, side, ArmPlan.ProbeMargin));
        Assert.True(probes.Count >= 60, $"{probes.Count} of 76 silhouette probes are sure with Bad at 1");
        foreach (var g in ArmPlan.BadProbes(side, true)) Assert.True(ArmPlan.Seen(bad, side, g.X, g.Y, ArmPlan.ProbeReach) == true, g.What);
        foreach (var g in ArmPlan.BadProbes(side, false)) Assert.True(ArmPlan.Seen(rest, side, g.X, g.Y, ArmPlan.ProbeReach) == false, g.What);
        Assert.True(ArmPlan.Silhouette(bad, 0.12f).Low < -0.075f);
    }
}
