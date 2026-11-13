using System.Numerics;
using Carve.Scenario.Cuts;
using Carve.Scenario.Gatehouse;
using Carve.Scenario.Shed;
using Xunit;

namespace Carve.Core.Tests;

/// <summary>
/// T-69's gatehouse, worked through Carve.Core before any frame: the seven stages of M-5's sketch run in order, with the
/// counts, tombstones and ids at each, the closed result, and the ray test of every pixel the scenario will ask about
/// (the scenario's own camera, ray test and probes, <see cref="GatehousePlan"/>). A planned count or check that cannot pass
/// on this geometry is a stop, not a change.
/// </summary>
[Trait("Tier", "unit")]
public class GatehouseGeometry
{
    private static Vector3 V(float x, float y, float z) => new(x, y, z);

    private sealed record Stage(string Name, PolygonMesh Mesh);

    private static readonly Lazy<IReadOnlyList<Stage>> Built = new(Build);

    private static PolygonMesh Mesh(int stage) => Built.Value[stage - 1].Mesh;

    private static int FaceWhere(PolygonMesh m, Func<Vector3, bool> all) => m.LiveFaces.Single(f => m.Faces[f].Loop.All(v => all(m.Vertices[v])));

    private static bool Near(float a, float b) => MathF.Abs(a - b) < 1e-4f;

    private static (int Face, int[] Pieces)[] Splits(EditResult r) => r.Split.Select(s => (s.Face, s.Pieces.ToArray())).ToArray();

    /// <summary>The seven stages, each step asserting what it created and deleted.</summary>
    private static IReadOnlyList<Stage> Build()
    {
        var stages = new List<Stage>();
        var m = TestMeshes.Make(
            [V(-1.5f, 0, -.15f), V(1.5f, 0, -.15f), V(1.5f, 4, -.15f), V(-1.5f, 4, -.15f), V(-1.5f, 0, .15f), V(1.5f, 0, .15f), V(1.5f, 4, .15f), V(-1.5f, 4, .15f)],
            TestMeshes.CubeFaces);
        stages.Add(new("slab", m));

        // 2. loop_cut on a vertical edge at 0.5 and 0.55 (y = 2.0 and 2.2); extrude the front band out by 0.08.
        var ring = LoopCut.Cut(m, (4, 7), [0.5, 0.55]);
        m = TestMeshes.Ok(ring.Edit);
        Assert.Equal([(0, [0, 6, 7]), (1, [1, 8, 9]), (4, [4, 10, 11]), (5, [5, 12, 13])], Splits(ring.Edit));
        Assert.Equal([8, 9, 10, 11, 12, 13, 14, 15], ring.Edit.NewVertices);
        var band = FaceWhere(m, p => Near(p.Z, .15f) && p.Y >= 2f - 1e-4f && p.Y <= 2.2f + 1e-4f);
        Assert.Equal(8, band);
        var cornice = MeshEdits.Extrude(m, [band], 0.08f);
        m = TestMeshes.Ok(cornice);
        var extruded = Assert.Single(cornice.Extruded);
        Assert.Equal((8, 8), (extruded.Face, extruded.Cap));
        Assert.Equal([14, 15, 16, 17], extruded.Walls);
        Assert.Equal([16, 17, 18, 19], extruded.NewVertices);
        stages.Add(new("cornice", m));

        // 3. merge_faces three times: the back's three pieces, each end's three.
        foreach (var pieces in new[] { new[] { 0, 6, 7 }, new[] { 4, 10, 11 }, new[] { 5, 12, 13 } })
        {
            var merged = FaceMerge.Merge(m, pieces, out var warnings);
            m = TestMeshes.Ok(merged);
            Assert.Empty(warnings);
            Assert.Empty(merged.Deleted.Vertices);
            Assert.Equal(pieces.Skip(1), merged.Deleted.Faces);
        }

        stages.Add(new("merged", m));

        // 4. plane_cut through (0, 3.4, 0), normal (0.8, 1.5, 0) then (-0.8, 1.5, 0), keeping below with fill.
        var first = PlaneCut.Cut(m, V(0, 3.4f, 0), V((float)(0.8 / 1.7), (float)(1.5 / 1.7), 0), PlaneKeep.Below, true);
        m = TestMeshes.Ok(first.Edit);
        Assert.Empty(first.Warnings);
        Assert.Equal([20, 21, 22, 23], first.Edit.NewVertices);
        Assert.Equal([22], first.Edit.NewFaces);
        Assert.Equal([2, 6], first.Edit.Deleted.Vertices);
        Assert.Equal([18, 19, 20, 21], first.Edit.Deleted.Faces);
        var second = PlaneCut.Cut(m, V(0, 3.4f, 0), V((float)(-0.8 / 1.7), (float)(1.5 / 1.7), 0), PlaneKeep.Below, true);
        m = TestMeshes.Ok(second.Edit);
        Assert.Empty(second.Warnings);
        Assert.Equal([24, 25, 26, 27], second.Edit.NewVertices);
        Assert.Equal([26, 27], second.Edit.NewFaces);
        Assert.Equal([3, 7, 20, 22], second.Edit.Deleted.Vertices);
        // The top face (3) is cut away whole: the first cut leaves a sliver of it, the second takes the sliver.
        Assert.Equal([3, 22, 23, 24, 25], second.Edit.Deleted.Faces);
        stages.Add(new("gable", m));

        // 5. knife, open, the door on the front; its piece is face 28.
        var door = Knife.Cut(m, [V(-1.1f, 0, .15f), V(-1.1f, 1.6f, .15f), V(-.4f, 1.6f, .15f), V(-.4f, 0, .15f)]);
        m = TestMeshes.Ok(door.Edit);
        Assert.Equal([28, 29, 30, 31], door.Edit.NewVertices);
        Assert.Equal([28], door.Edit.NewFaces);
        Assert.Equal([(1, [1, 28])], Splits(door.Edit));
        Assert.Equal(3, door.Edges.Count);
        Assert.Equal([28, 29, 31, 30], m.Faces[28].Loop);
        stages.Add(new("door", m));

        // 6. knife, closed, the window on the front, then the back: each leaves a hole and an inner face.
        var front = Knife.Cut(m, [V(.3f, .8f, .15f), V(1.1f, .8f, .15f), V(1.1f, 1.5f, .15f), V(.7f, 1.75f, .15f), V(.3f, 1.5f, .15f)], true);
        m = TestMeshes.Ok(front.Edit);
        Assert.Equal([32, 33, 34, 35, 36], front.Edit.NewVertices);
        Assert.Equal([29], front.Edit.NewFaces);
        Assert.Equal([(1, [1, 29])], Splits(front.Edit));
        var back = Knife.Cut(m, [V(.3f, .8f, -.15f), V(1.1f, .8f, -.15f), V(1.1f, 1.5f, -.15f), V(.7f, 1.75f, -.15f), V(.3f, 1.5f, -.15f)], true);
        m = TestMeshes.Ok(back.Edit);
        Assert.Equal([37, 38, 39, 40, 41], back.Edit.NewVertices);
        Assert.Equal([30], back.Edit.NewFaces);
        Assert.Equal([(0, [0, 30])], Splits(back.Edit));
        Assert.Equal([[32, 36, 35, 34, 33]], m.Faces[1].Holes.Select(h => h.ToArray()));
        Assert.Equal([[37, 38, 39, 40, 41]], m.Faces[0].Holes.Select(h => h.ToArray()));
        Assert.Equal([32, 33, 34, 35, 36], m.Faces[29].Loop);
        stages.Add(new("windows", m));

        // 7. delete both window faces, then bridge the front and back hole loops: five reveal quads.
        var deleted = MeshDelete.Delete(m, null, [29, 30]);
        m = TestMeshes.Ok(deleted);
        Assert.Equal([29, 30], deleted.Deleted.Faces);
        Assert.Empty(deleted.Deleted.Vertices);
        var bridged = Bridge.Join(m, m.Faces[1].Holes[0], m.Faces[0].Holes[0]);
        m = TestMeshes.Ok(bridged);
        Assert.Equal([31, 32, 33, 34, 35], bridged.NewFaces);
        Assert.Equal([[36, 32, 37, 41], [35, 36, 41, 40], [34, 35, 40, 39], [33, 34, 39, 38], [32, 33, 38, 37]], bridged.NewFaces.Select(f => m.Faces[f].Loop.ToArray()));
        stages.Add(new("bridged", m));
        return stages;
    }

    [Fact]
    public void EachStageHasItsCountsAndTombstones()
    {
        // (name, live vertices, vertex slots, live faces, face slots)
        (string, int, int, int, int)[] expected =
        [
            ("slab", 8, 8, 6, 6),
            ("cornice", 20, 20, 18, 18),
            ("merged", 20, 20, 12, 18),
            ("gable", 22, 28, 13, 28),
            ("door", 26, 32, 14, 29),
            ("windows", 36, 42, 16, 31),
            ("bridged", 36, 42, 19, 36),
        ];
        Assert.Equal(expected, Built.Value.Select(s => (s.Name, s.Mesh.LiveVertexCount, s.Mesh.Vertices.Count, s.Mesh.LiveFaceCount, s.Mesh.Faces.Count)).ToArray());
        // Slots are never reused: the deleted vertices are 2, 3, 6, 7, 20, 22 and the deleted faces everything merged or cut away.
        var last = Mesh(7);
        Assert.Equal([2, 3, 6, 7, 20, 22], Enumerable.Range(0, last.Vertices.Count).Where(last.IsVertexDeleted));
        Assert.Equal([3, 6, 7, 10, 11, 12, 13, 18, 19, 20, 21, 22, 23, 24, 25, 29, 30], Enumerable.Range(0, last.Faces.Count).Where(last.IsFaceDeleted));
    }

    [Fact]
    public void EveryStageIsClosedAndTheLastCheckIsClean()
    {
        foreach (var stage in Built.Value)
        {
            Assert.All(stage.Mesh.Topology.Edges, e => Assert.True(e.IsManifold, $"{stage.Name}: {e.Name}"));
        }

        var health = MeshHealth.Create(Mesh(7));
        Assert.True(health.Ok);
        Assert.Empty(health.Problems);
        Assert.Empty(health.Warnings);
        Assert.True(health.Stats.Closed);
        Assert.Equal(1, health.Stats.Shells);
        Assert.Equal((36, 19), (health.Stats.Vertices, health.Stats.Faces));
    }

    [Fact]
    public void TheDoorTheWindowAndTheGableAreWhereTheSketchPutThem()
    {
        var m = Mesh(7);
        // The door: a face on the front, x -1.1..-0.4, y 0..1.6, wholly on the left of the middle.
        Assert.All(m.Faces[28].Loop, v => Assert.True(Near(m.Vertices[v].Z, .15f) && m.Vertices[v].X is >= -1.1f - 1e-4f and <= -.4f + 1e-4f && m.Vertices[v].Y <= 1.6f + 1e-4f));
        // The window's reveal: five quads, each from a front corner to its back corner, 0.3 deep.
        foreach (var f in new[] { 31, 32, 33, 34, 35 })
        {
            Assert.Equal(4, m.Faces[f].Loop.Count);
            Assert.Equal(2, m.Faces[f].Loop.Count(v => Near(m.Vertices[v].Z, .15f)));
            Assert.Equal(2, m.Faces[f].Loop.Count(v => Near(m.Vertices[v].Z, -.15f)));
        }

        // The gable: the front's upper piece (9) is a pentagon with the apex (0, 3.4), and nothing is higher.
        Assert.Equal(5, m.Faces[9].Loop.Count);
        Assert.Contains(m.Faces[9].Loop, v => Near(m.Vertices[v].X, 0) && Near(m.Vertices[v].Y, 3.4f));
        Assert.Equal(3.4f, m.LiveVertices.Max(v => m.Vertices[v].Y), 1e-4f);
        // The cornice stands 0.08 out of the front.
        Assert.All(m.Faces[8].Loop, v => Assert.True(Near(m.Vertices[v].Z, .23f)));
    }

    /// <summary>What the scenario's camera and ray test are sure of at each stage and direction: the sure probes, and the faces they show.</summary>
    public static TheoryData<int, string, int, string> RayTests
    {
        get
        {
            var data = new TheoryData<int, string, int, string>();
            foreach (var ((stage, view), (sure, faces)) in GatehousePlan.Expected)
            {
                data.Add(stage, view, sure, faces);
            }

            return data;
        }
    }

    private static Camera CameraFor(Solid solid, string view)
    {
        var (min, max) = GatehousePlan.Bounds(solid);
        return view switch
        {
            "front" => new Camera(min, max, GatehousePlan.FromFront, GatehousePlan.Size),
            "back" => new Camera(min, max, GatehousePlan.FromBack, GatehousePlan.Size),
            _ => new Camera(min, max, GatehousePlan.Straight, GatehousePlan.Size, orthographic: true),
        };
    }

    private static Solid SolidOf(PolygonMesh m) => new(
        Enumerable.Range(0, m.Vertices.Count).Select(v => m.IsVertexDeleted(v) ? Vector3.Zero : m.Vertices[v]).ToArray(),
        Enumerable.Range(0, m.Faces.Count).Select(f => m.IsFaceDeleted(f)
            ? (Array.Empty<int>(), Array.Empty<int[]>())
            : (m.Faces[f].Loop.ToArray(), m.Faces[f].Holes.Select(h => h.ToArray()).ToArray())).ToArray());

    [Theory]
    [MemberData(nameof(RayTests))]
    public void TheRayTestIsSureOfEnoughPixelsAndShowsTheFacesPlanned(int stage, string view, int sure, string faces)
    {
        var solid = SolidOf(Mesh(stage));
        var camera = CameraFor(solid, view);
        var probes = GatehousePlan.Probes(solid).Select(p => (p, seen: GatehousePlan.Seen(solid, camera, p))).ToArray();
        Assert.Equal(sure, probes.Count(x => x.seen != -2));
        Assert.Equal(faces, string.Join(" ", probes.Where(x => x.seen >= 0).Select(x => x.seen).Distinct().Order()));
        // The middle of a big face the camera sees is that face.
        Assert.All(probes.Where(x => x.p.Name == "middle" && x.seen >= 0 && x.p.Face is 1 or 5 or 8 or 9 or 28 && faces.Split(' ').Contains($"{x.p.Face}")),
            x => Assert.Equal(x.p.Face, x.seen));
    }

    [Fact]
    public void TheFrontDirectionShowsThePlusXEndAndTheBackOneTheMinusXEnd()
    {
        // The asymmetry a mirrored camera fails: from [0.6, 0.35, 1] the +x end (5) and never the -x end (4), from
        // [-0.6, 0.35, -1] the -x end and never the +x end, at every stage.
        for (var stage = 1; stage <= 7; stage++)
        {
            var solid = SolidOf(Mesh(stage));
            foreach (var (view, shown, hidden) in new[] { ("front", 5, 4), ("back", 4, 5) })
            {
                var camera = CameraFor(solid, view);
                var seen = GatehousePlan.Probes(solid).Select(p => GatehousePlan.Seen(solid, camera, p)).Where(s => s >= 0).ToHashSet();
                Assert.Contains(shown, seen);
                Assert.DoesNotContain(hidden, seen);
            }
        }
    }

    /// <summary>
    /// The sheet's +y and -y tiles (256 px, orthographic, up -Z as <c>render</c> has it for a view along Y): what the ray test
    /// is sure of, and the faces it shows. From above the roof (26, 27) and the cornice's top (15); from below the slab's bottom
    /// (2) and the cornice's underside (17), which share a normal. The scenario holds both frames to these faces' shades.
    /// </summary>
    [Theory]
    [InlineData("+y", 46, "15 26 27")]
    [InlineData("-y", 46, "2 17")]
    public void TheAxisTilesAlongYShowTheRoofFromAboveAndTheBottomFromBelow(string view, int sure, string faces)
    {
        var solid = SolidOf(Mesh(7));
        var (min, max) = GatehousePlan.Bounds(solid);
        var camera = new Camera(min, max, view == "+y" ? Vector3.UnitY : -Vector3.UnitY, 256, orthographic: true, up: -Vector3.UnitZ);
        var seen = GatehousePlan.Probes(solid).Select(p => camera.Pixel(p.At) is var (x, y) && p.Narrow ? solid.SeenNarrow(camera, 256, x, y) : solid.Seen(camera, 256, x, y)).ToArray();
        Assert.Equal((sure, faces), (seen.Count(s => s != -2), string.Join(" ", seen.Where(s => s >= 0).Distinct().Order())));
        Assert.True(sure >= 10, "the scenario's Look needs ten sure probes in a tile");
    }

    [Fact]
    public void TheDoorIsSeenFromTheFrontOnlyAndLeftOfTheWindow()
    {
        for (var stage = 5; stage <= 7; stage++)
        {
            var solid = SolidOf(Mesh(stage));
            var (front, back) = (CameraFor(solid, "front"), CameraFor(solid, "back"));
            var door = GatehousePlan.Probes(solid).Where(p => p.Face == 28).ToArray();
            Assert.Equal(5, door.Length);
            Assert.All(door, p => Assert.Equal(28, GatehousePlan.Seen(solid, front, p)));
            Assert.All(door, p => Assert.Equal(0, GatehousePlan.Seen(solid, back, p)));
            // Left of the window in the front frame, and so right of it in the back one.
            Assert.True(front.Pixel(V(-.75f, .8f, .15f)).X < front.Pixel(V(.7f, 1.2f, .15f)).X);
            Assert.True(back.Pixel(V(-.75f, .8f, .15f)).X > back.Pixel(V(.7f, 1.2f, -.15f)).X);
        }
    }

    [Fact]
    public void SeenStraightThroughTheWindowIsTheBackground()
    {
        var solid = SolidOf(Mesh(7));
        var camera = CameraFor(solid, "straight");
        var probes = GatehousePlan.WindowBackground().ToArray();
        Assert.Equal(6, probes.Length);
        Assert.All(probes, p => Assert.Equal(-1, GatehousePlan.Seen(solid, camera, p)));
        // Before the window's faces are deleted the same points show the window's inner face on the front (29).
        var before = SolidOf(Mesh(6));
        var cameraBefore = CameraFor(before, "straight");
        Assert.All(probes, p => Assert.Equal(29, GatehousePlan.Seen(before, cameraBefore, p)));
    }

    [Fact]
    public void TheWireframesHighlightTheStagesNewEdges()
    {
        // (stage, new edges, in sight from the front among them, in sight from the back among them)
        (int, int, int, int)[] expected = [(2, 16, 10, 7), (4, 7, 5, 5), (6, 10, 5, 5)];
        foreach (var (stage, added, fromFront, fromBack) in expected)
        {
            var (before, after) = (SolidOf(Mesh(stage - 1)), SolidOf(Mesh(stage)));
            var fresh = GatehousePlan.NewEdges(before, after);
            Assert.Equal(added, fresh.Length);
            Assert.Equal(fromFront, GatehousePlan.InSight(after, CameraFor(after, "front"), GatehousePlan.FromFront).Count(fresh.Contains));
            Assert.Equal(fromBack, GatehousePlan.InSight(after, CameraFor(after, "back"), GatehousePlan.FromBack).Count(fresh.Contains));
        }
    }

    [Fact]
    public void TheEdgeUnderTheCorniceIsNotInSightFromTheFront()
    {
        var after = SolidOf(Mesh(2));
        var seen = GatehousePlan.InSight(after, CameraFor(after, "front"), GatehousePlan.FromFront);
        Assert.DoesNotContain((12, 14), seen);
    }

    [Fact]
    public void TheSampleIsTheGatehouseBuiltInCoreByContent()
    {
        // The door's face is coloured, as the scenario's set_colour does. Compared by content: each is read, then written the one way.
        var built = Mesh(7).WithStyle(FaceStyle.None.WithColour([28], "#6b4423"));
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(root.FullName, "samples", "gatehouse.carve")))
        {
            root = root.Parent ?? throw new FileNotFoundException("samples/gatehouse.carve");
        }

        var sample = CarveFile.Read(File.ReadAllText(Path.Combine(root.FullName, "samples", "gatehouse.carve"))).Model!;
        Assert.Equal(CarveFile.Write(Model.Of("mesh", built)), CarveFile.Write(sample));
    }
}
