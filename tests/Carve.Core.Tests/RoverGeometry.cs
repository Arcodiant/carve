using System.Numerics;
using Carve.Rover;
using Xunit;

namespace Carve.Core.Tests;

/// <summary>
/// M-9's rover worked through Carve.Core before any frame (T-91): per part and level, the counts and world bounds; the wheels
/// clear of the chassis sides; which faces of different parts share a plane; each instance where its source is, moved, with
/// its boss outward; the turned mast's lamp at -Z; the envelopes silent, and the one warning when Axle_F is lowered; and level
/// 1 drawing the Mast at its level 0. Every number T-94, T-95 and T-96 check a frame or an export against comes from here.
/// Level -1 in a row means live.
/// </summary>
[Trait("Tier", "unit")]
public class RoverGeometry
{
    private const float Eps = 1e-5f;
    private const int Live = -1;
    private static readonly Model Rover = RoverSpec.Build();

    private static PolygonMesh? MeshOf(Model model, string name, int level) => level == Live
        ? model.Find(model.FindPart(name)!.InstanceOf ?? name)
        : model.LevelMesh(name, level);

    /// <summary>The part's own occurrence at the level, placed in the model's space, or null when it draws nothing there.</summary>
    private static (PolygonMesh Mesh, Vector3[] Points)? Placed(Model model, string name, int level)
    {
        if (MeshOf(model, name, level) is not { } mesh) return null;
        var world = model.WorldTransform(name)!.Value;
        return (mesh, [.. mesh.Vertices.Select(v => Vector3.Transform(v, world))]);
    }

    private static (Vector3 Min, Vector3 Max) Bounds(IEnumerable<Vector3> points) =>
        points.Aggregate((Min: new Vector3(float.MaxValue), Max: new Vector3(float.MinValue)), (b, p) => (Vector3.Min(b.Min, p), Vector3.Max(b.Max, p)));

    private static void Near(Vector3 expected, Vector3 actual, string what)
    {
        Assert.True(Vector3.Distance(expected, actual) <= Eps, $"{what}: expected {expected}, got {actual}");
    }

    private static PolygonMesh? Whole(Model model, int level) => level == Live ? model.Combined : model.CombinedAt(level);

    // ---- Counts -----------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("Chassis", 0, 8, 6, 12)]
    [InlineData("Chassis", 1, 8, 6, 12)]
    [InlineData("Chassis", Live, 8, 6, 12)]
    [InlineData("Cab", 0, 8, 6, 12)]
    [InlineData("Cab", 1, 16, 14, 28)]
    [InlineData("Cab", Live, 16, 14, 28)]
    [InlineData("Mast", 0, 8, 6, 12)]
    [InlineData("Mast", 1, 8, 6, 12)]
    [InlineData("Mast", Live, 8, 6, 12)]
    [InlineData("Lamp", 0, 8, 6, 12)]
    [InlineData("Lamp", 1, 16, 14, 28)]
    [InlineData("Lamp", Live, 16, 14, 28)]
    [InlineData("Wheel_FL", 0, 16, 10, 28)]
    [InlineData("Wheel_FL", 1, 64, 50, 124)]
    [InlineData("Wheel_FL", Live, 64, 50, 124)]
    [InlineData("Wheel_FR", 0, 16, 10, 28)]
    [InlineData("Wheel_FR", 1, 64, 50, 124)]
    [InlineData("Wheel_RL", 1, 64, 50, 124)]
    [InlineData("Wheel_RR", Live, 64, 50, 124)]
    public void Each_part_has_its_counts_at_each_level(string part, int level, int vertices, int faces, int triangles)
    {
        var mesh = MeshOf(Rover, part, level)!;
        Assert.Equal((vertices, faces, triangles), (mesh.LiveVertexCount, mesh.LiveFaceCount, Triangulator.Triangulate(mesh).Count));
    }

    [Theory]
    [InlineData(0, 96, 64, 160)]
    [InlineData(1, 304, 240, 576)]
    [InlineData(Live, 304, 240, 576)]
    public void The_whole_rover_has_its_counts_at_each_level(int level, int vertices, int faces, int triangles)
    {
        var whole = Whole(Rover, level)!;
        Assert.Equal((vertices, faces, triangles), (whole.LiveVertexCount, whole.LiveFaceCount, Triangulator.Triangulate(whole).Count));
    }

    [Fact]
    public void The_empties_draw_nothing_and_the_levels_are_kept_as_agreed()
    {
        foreach (var axle in new[] { "Axle_F", "Axle_R" })
            foreach (var level in new[] { 0, 1, Live })
                Assert.Null(Placed(Rover, axle, level));
        Assert.Equal(["Chassis", "Cab", "Mast", "Lamp", "Axle_F", "Wheel_FL", "Wheel_FR", "Axle_R", "Wheel_RL", "Wheel_RR"], Rover.Parts.Select(p => p.Name));
        foreach (var part in Rover.Parts)
            Assert.Equal(RoverSpec.RefinedParts.Contains(part.Name) ? [0, 1] : RoverSpec.GeometryParts.Contains(part.Name) ? [0] : (int[])[],
                part.Levels.Select(l => l.Number));
        // The blockout's colours and the refinement's: the boss is light grey on its cap and walls, the lens yellow, the ring dark.
        var wheel = Rover.Find("Wheel_FL")!.Style!.Colours;
        Assert.All(RoverSpec.HubBossFaces, f => Assert.Equal(RoverSpec.LightGrey, wheel[f]));
        Assert.All(Enumerable.Range(2, 32), f => Assert.Equal(RoverSpec.DarkGrey, wheel[f]));
        Assert.Equal(RoverSpec.Yellow, Rover.Find("Lamp")!.Style!.Colours[RoverSpec.BoxPlusX]);
        Assert.All(Enumerable.Range(0, 14), f => Assert.Equal(RoverSpec.Sand, Rover.Find("Cab")!.Style!.Colours[f]));
    }

    // ---- Bounds -----------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("Chassis", 0, -1, 0.4f, -0.6f, 1, 0.8f, 0.6f)]
    [InlineData("Chassis", Live, -1, 0.4f, -0.6f, 1, 0.8f, 0.6f)]
    [InlineData("Cab", 0, 0.1f, 0.8f, -0.5f, 0.9f, 1.4f, 0.5f)]
    [InlineData("Cab", 1, 0.1f, 0.8f, -0.5f, 0.9f, 1.4f, 0.5f)]
    [InlineData("Mast", 0, -0.65f, 0.8f, -0.05f, -0.55f, 1.6f, 0.05f)]
    [InlineData("Mast", 1, -0.65f, 0.8f, -0.05f, -0.55f, 1.6f, 0.05f)]
    [InlineData("Lamp", 0, -0.6f, 1.5f, -0.1f, -0.3f, 1.7f, 0.1f)]
    [InlineData("Lamp", Live, -0.6f, 1.5f, -0.1f, -0.3f, 1.7f, 0.1f)]
    [InlineData("Wheel_FL", 0, 0.3f, 0, 0.62f, 1.1f, 0.8f, 0.82f)]
    [InlineData("Wheel_FL", 1, 0.3f, 0, 0.62f, 1.1f, 0.8f, 0.87f)]
    [InlineData("Wheel_FL", Live, 0.3f, 0, 0.62f, 1.1f, 0.8f, 0.87f)]
    [InlineData("Wheel_FR", 0, 0.3f, 0, -0.82f, 1.1f, 0.8f, -0.62f)]
    [InlineData("Wheel_FR", 1, 0.3f, 0, -0.87f, 1.1f, 0.8f, -0.62f)]
    [InlineData("Wheel_RL", 0, -1.1f, 0, 0.62f, -0.3f, 0.8f, 0.82f)]
    [InlineData("Wheel_RL", 1, -1.1f, 0, 0.62f, -0.3f, 0.8f, 0.87f)]
    [InlineData("Wheel_RR", 0, -1.1f, 0, -0.82f, -0.3f, 0.8f, -0.62f)]
    [InlineData("Wheel_RR", Live, -1.1f, 0, -0.87f, -0.3f, 0.8f, -0.62f)]
    public void Each_part_has_its_world_bounds_at_each_level(string part, int level, float x0, float y0, float z0, float x1, float y1, float z1)
    {
        var (min, max) = Bounds(Placed(Rover, part, level)!.Value.Points);
        Near(new(x0, y0, z0), min, $"{part} at {level} min");
        Near(new(x1, y1, z1), max, $"{part} at {level} max");
    }

    [Theory]
    [InlineData(0, 0.82f)]
    [InlineData(1, 0.87f)]
    [InlineData(Live, 0.87f)]
    public void The_whole_rover_has_its_world_bounds_and_draws_its_parts_in_pre_order(int level, float halfDepth)
    {
        var whole = Whole(Rover, level)!;
        var (min, max) = Bounds(whole.Vertices);
        Near(new(-1.1f, 0, -halfDepth), min, "whole min");
        Near(new(1.1f, 1.7f, halfDepth), max, "whole max");
        // The combined mesh is each drawn occurrence, placed, in pre-order: at level 1 the Mast (kept at level 0 only) is its level 0.
        var expected = Rover.Parts.Select(p => Placed(Rover, p.Name, level)).Where(o => o is not null).SelectMany(o => o!.Value.Points).ToArray();
        Assert.Equal(expected.Length, whole.Vertices.Count);
        for (var i = 0; i < expected.Length; i++) Near(expected[i], whole.Vertices[i], $"vertex {i}");
    }

    [Fact]
    public void Level_1_draws_the_Mast_and_the_Chassis_at_their_level_0()
    {
        foreach (var part in new[] { "Mast", "Chassis" })
            Assert.Same(Rover.LevelMesh(part, 0), Rover.LevelMesh(part, 1));
        foreach (var part in RoverSpec.RefinedParts)
            Assert.NotSame(Rover.LevelMesh(part, 0), Rover.LevelMesh(part, 1));
        // The Mast's occurrence in CombinedAt(1) starts after the Chassis (8) and the Cab's level 1 (16), and is its level 0, placed.
        var mast = Placed(Rover, "Mast", 0)!.Value.Points;
        var at1 = Rover.CombinedAt(1)!;
        for (var i = 0; i < mast.Length; i++) Near(mast[i], at1.Vertices[24 + i], $"Mast vertex {i}");
        Assert.Null(Rover.CombinedAt(-1));
    }

    // ---- Clearance and coplanar faces -------------------------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void The_wheels_clear_the_chassis_sides(int level)
    {
        var chassis = Bounds(Placed(Rover, "Chassis", level)!.Value.Points);
        Assert.Equal(0.6f, chassis.Max.Z, 5);
        foreach (var wheel in new[] { "Wheel_FL", "Wheel_FR", "Wheel_RL", "Wheel_RR" })
        {
            var (min, max) = Bounds(Placed(Rover, wheel, level)!.Value.Points);
            // The wheel overlaps the chassis in x and y, so only its z keeps it clear: its inner face at |z| 0.62.
            Assert.True(min.X < chassis.Max.X && max.X > chassis.Min.X && min.Y < chassis.Max.Y && max.Y > chassis.Min.Y, wheel);
            var inner = min.Z > 0 ? min.Z : -max.Z;
            Assert.Equal(0.62f, inner, 5);
            Assert.True(inner > chassis.Max.Z, $"{wheel}'s inner face at |z| {inner} is not clear of the chassis side at {chassis.Max.Z}");
        }
    }

    /// <summary>Every pair of faces of different parts that lie in one plane and overlap with area, as "A[f] B[g] same|opposite".</summary>
    private static List<string> CoplanarOverlaps(Model model, int level)
    {
        var faces = new List<(string Part, int Face, Vector3[] Loop, Vector3 Normal)>();
        foreach (var part in model.Parts)
        {
            if (Placed(model, part.Name, level) is not { } placed) continue;
            for (var f = 0; f < placed.Mesh.Faces.Count; f++)
            {
                Vector3[] loop = [.. placed.Mesh.Faces[f].Loop.Select(v => placed.Points[v])];
                var n = Vector3.Zero;
                for (var i = 0; i < loop.Length; i++)
                {
                    var (a, b) = (loop[i], loop[(i + 1) % loop.Length]);
                    n += new Vector3((a.Y - b.Y) * (a.Z + b.Z), (a.Z - b.Z) * (a.X + b.X), (a.X - b.X) * (a.Y + b.Y));
                }
                faces.Add((part.Name, f, loop, Vector3.Normalize(n)));
            }
        }
        var found = new List<string>();
        for (var i = 0; i < faces.Count; i++)
            for (var j = i + 1; j < faces.Count; j++)
            {
                var (a, b) = (faces[i], faces[j]);
                if (a.Part == b.Part) continue;
                var dot = Vector3.Dot(a.Normal, b.Normal);
                if (MathF.Abs(dot) < 1 - 1e-5f || b.Loop.Any(p => MathF.Abs(Vector3.Dot(a.Normal, p - a.Loop[0])) > Eps)) continue;
                if (OverlapWithArea(a.Loop, b.Loop, a.Normal))
                    found.Add($"{a.Part}[{a.Face}] {b.Part}[{b.Face}] {(dot > 0 ? "same" : "opposite")}");
            }
        return found;
    }

    /// <summary>Two convex polygons in one plane overlap with area: no edge normal of either separates them (or meets them only at a line).</summary>
    private static bool OverlapWithArea(Vector3[] a, Vector3[] b, Vector3 normal)
    {
        foreach (var poly in new[] { a, b })
            for (var i = 0; i < poly.Length; i++)
            {
                var axis = Vector3.Cross(poly[(i + 1) % poly.Length] - poly[i], normal);
                var (amin, amax) = (a.Min(p => Vector3.Dot(p, axis)), a.Max(p => Vector3.Dot(p, axis)));
                var (bmin, bmax) = (b.Min(p => Vector3.Dot(p, axis)), b.Max(p => Vector3.Dot(p, axis)));
                if (MathF.Min(amax, bmax) - MathF.Max(amin, bmin) <= Eps * axis.Length()) return false;
            }
        return true;
    }

    /// <summary>
    /// No two faces of different parts lie in one plane facing the same way and overlapping, which would fight in depth. Two
    /// pairs touch facing opposite ways: the Cab's and the Mast's bottoms (-y) sit on the Chassis's top (+y) at y 0.8, as the
    /// sketch places them. Each is hidden between two solids, so neither can be seen; they are pinned here, not hidden.
    /// </summary>
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(Live, false)]
    [InlineData(Live, true)]
    public void No_faces_of_different_parts_share_a_plane_facing_the_same_way(int level, bool mastTurned)
    {
        var model = mastTurned ? RoverSpec.TurnMast(Rover) : Rover;
        Assert.Equal(
            [$"Chassis[{RoverSpec.BoxPlusY}] Cab[{RoverSpec.BoxMinusY}] opposite", $"Chassis[{RoverSpec.BoxPlusY}] Mast[{RoverSpec.BoxMinusY}] opposite"],
            CoplanarOverlaps(model, level));
    }

    // ---- Instances, the turned mast and the instance edit -----------------------------------------------------------

    [Theory]
    [InlineData("Wheel_FR", -1)]
    [InlineData("Wheel_RL", 1)]
    [InlineData("Wheel_RR", -1)]
    [InlineData("Wheel_FL", 1)]
    public void Each_wheel_is_its_source_moved_with_the_hub_boss_outward(string wheel, int outward)
    {
        var source = Rover.Find("Wheel_FL")!;
        var world = Rover.WorldTransform(wheel)!.Value;
        // Its occurrence in Combined is the source's vertices at the instance's world transform.
        var start = Rover.OffsetOf(wheel)!.Value.Vertices;
        for (var v = 0; v < source.Vertices.Count; v++)
            Near(Vector3.Transform(source.Vertices[v], world), Rover.Combined!.Vertices[start + v], $"{wheel} vertex {v}");
        // Its world bounds are the source's local box moved.
        var (lmin, lmax) = Bounds(source.Vertices);
        var corners = Enumerable.Range(0, 8).Select(i => Vector3.Transform(
            new Vector3((i & 1) == 0 ? lmin.X : lmax.X, (i & 2) == 0 ? lmin.Y : lmax.Y, (i & 4) == 0 ? lmin.Z : lmax.Z), world));
        var moved = Bounds(corners);
        var placed = Bounds(Placed(Rover, wheel, Live)!.Value.Points);
        Near(moved.Min, placed.Min, $"{wheel} min");
        Near(moved.Max, placed.Max, $"{wheel} max");
        // The boss's cap is on the outside: further from the chassis than the inner cap, on the wheel's own side.
        float CapZ(int face) => source.Faces[face].Loop.Average(v => Vector3.Transform(source.Vertices[v], world).Z);
        var (boss, inner) = (CapZ(RoverSpec.WheelHubCap), CapZ(RoverSpec.WheelInnerCap));
        Assert.Equal(0.87f * outward, boss, 5);
        Assert.Equal(0.62f * outward, inner, 5);
    }

    [Fact]
    public void The_turned_mast_puts_the_lamp_at_minus_Z()
    {
        var turned = RoverSpec.TurnMast(Rover);
        var (min, max) = Bounds(Placed(turned, "Lamp", Live)!.Value.Points);
        Near(new(-0.7f, 1.5f, -0.3f), min, "lamp min");
        Near(new(-0.5f, 1.7f, 0), max, "lamp max");
        var lamp = turned.Find("Lamp")!;
        var world = turned.WorldTransform("Lamp")!.Value;
        var lens = lamp.Faces[RoverSpec.BoxPlusX].Loop.Select(v => Vector3.Transform(lamp.Vertices[v], world)).ToArray();
        Near(new(-0.6f, 1.6f, -0.3f + RoverSpec.LensDepth), lens.Aggregate(Vector3.Add) / lens.Length, "lens centre");
        // The mast, a square post about its own axis, has the same bounds turned.
        Assert.Equal(Bounds(Placed(Rover, "Mast", Live)!.Value.Points).Max, Bounds(Placed(turned, "Mast", Live)!.Value.Points).Max, new Vec3Near());
        Assert.Empty(turned.EnvelopeWarnings());
    }

    [Fact]
    public void One_move_on_the_hub_boss_moves_it_in_all_four_wheels()
    {
        var edited = RoverSpec.EditHub(Rover);
        var cap = Rover.Find("Wheel_FL")!.Faces[RoverSpec.WheelHubCap].Loop.ToHashSet();
        foreach (var wheel in new[] { "Wheel_FL", "Wheel_FR", "Wheel_RL", "Wheel_RR" })
        {
            var (before, after) = (Placed(Rover, wheel, Live)!.Value.Points, Placed(edited, wheel, Live)!.Value.Points);
            for (var v = 0; v < before.Length; v++)
                Near(before[v] + (cap.Contains(v) ? RoverSpec.HubMove.By : Vector3.Zero), after[v], $"{wheel} vertex {v}");
        }
        Assert.Empty(edited.EnvelopeWarnings());
        // Level 1 keeps the boss where it was kept.
        Assert.Equal(Rover.CombinedAt(1)!.Vertices, edited.CombinedAt(1)!.Vertices);
        Assert.Null(Rover.Target("Wheel_FR", out var problem));
        Assert.Equal("mesh: 'Wheel_FR' is an instance of 'Wheel_FL'; edit 'Wheel_FL', and every instance follows", problem);
    }

    // ---- Envelopes --------------------------------------------------------------------------------------------------

    [Fact]
    public void The_rover_has_no_envelope_warnings_at_any_stage()
    {
        Assert.Empty(RoverSpec.BuildBlockout().EnvelopeWarnings());
        Assert.Empty(Rover.EnvelopeWarnings());
    }

    [Fact]
    public void Lowering_Axle_F_gives_exactly_the_one_warning()
    {
        var warnings = RoverSpec.LowerAxle(Rover).EnvelopeWarnings();
        var w = Assert.Single(warnings);
        Assert.Equal(("Axle_F", "Chassis", 'y', false), (w.Part, w.Parent, w.Axis, w.Above));
        Assert.Equal(0.3f, w.By, 5);
    }

    private sealed class Vec3Near : IEqualityComparer<Vector3>
    {
        public bool Equals(Vector3 a, Vector3 b) => Vector3.Distance(a, b) <= Eps;
        public int GetHashCode(Vector3 v) => 0;
    }
}
