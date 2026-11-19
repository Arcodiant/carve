using System.Numerics;
using Xunit;
using static Carve.Core.Tests.TestMeshes;

namespace Carve.Core.Tests;

/// <summary>
/// Morph targets in the Core (T-99, D-6 Morph targets): the constructors and their refusals, sparse storage, evaluation,
/// morph before skin, subdivide exactness on M-21's arm cage and a hard-rimmed cylinder, and a combined model.
/// </summary>
[Trait("Tier", "unit")]
public class MorphTargetsTests
{
    private static readonly double[] ArmRadii = [.050, .055, .058, .055, .048, .040, .046, .048, .042, .034, .028];

    /// <summary>
    /// M-21's arm cage: 11 rings of 8 along +X at x = 0.00..0.60 step 0.06, ring i's vertex k (id 8i + k) at
    /// (x, r cos a, r sin a), a = 2πk/8, rounded to 6 decimals; sides wound outward, then the shoulder cap (x = 0, facing -X)
    /// and the wrist cap (facing +X), both 8-gons. Ring radii are scaled by <paramref name="scale"/> (ring index to factor).
    /// </summary>
    internal static PolygonMesh Arm(Func<int, double>? scale = null)
    {
        var vertices = new List<Vector3>();
        for (var i = 0; i < 11; i++)
            for (var k = 0; k < 8; k++)
            {
                var a = 2 * Math.PI * k / 8;
                var r = ArmRadii[i] * (scale?.Invoke(i) ?? 1);
                vertices.Add(new Vector3((float)Math.Round(0.06 * i, 6), (float)Math.Round(r * Math.Cos(a), 6), (float)Math.Round(r * Math.Sin(a), 6)));
            }
        var faces = new List<int[]>();
        for (var i = 0; i < 10; i++)
            for (var k = 0; k < 8; k++)
                faces.Add([8 * i + k, 8 * i + (k + 1) % 8, 8 * (i + 1) + (k + 1) % 8, 8 * (i + 1) + k]);
        faces.Add([7, 6, 5, 4, 3, 2, 1, 0]);
        faces.Add([80, 81, 82, 83, 84, 85, 86, 87]);
        return Make(vertices, faces).WithShade(MeshShade.Of(ShadingMode.Smooth, []));
    }

    private static readonly int[] BulkRings = [1, 2, 3, 6, 7, 8];

    /// <summary>The sketch's Bulk: rings 1-3 and 6-8 at 1.35 times their radius, given sparse (48 vertices).</summary>
    private static MorphTarget Bulk(PolygonMesh arm)
    {
        var bulked = Arm(i => BulkRings.Contains(i) ? 1.35 : 1);
        var given = BulkRings.SelectMany(i => Enumerable.Range(8 * i, 8)).ToDictionary(v => v, v => bulked.Vertices[v]);
        return Made(MorphTarget.FromPositions(arm, "Bulk", given));
    }

    private static MorphTarget Made(MorphTargetResult result)
    {
        Assert.True(result.Succeeded, string.Join("; ", result.Errors));
        return result.Target!;
    }

    private static string Refusal(MorphTargetResult result)
    {
        Assert.False(result.Succeeded);
        return string.Join("; ", result.Errors);
    }

    private static PolygonMesh With(PolygonMesh mesh, params MorphTarget[] targets) => mesh.WithTargets(MorphTargets.Of(targets));

    private static Dictionary<string, float> W(params (string Name, float Weight)[] w) => w.ToDictionary(p => p.Name, p => p.Weight);

    // --- Constructors ---

    [Fact]
    public void Full_positions_give_given_minus_base_and_store_no_zero_delta()
    {
        var cube = Cube();
        var given = cube.Vertices.ToArray();
        given[2] += new Vector3(0, 0.5f, 0);
        given[5] = new Vector3(given[5].X * 2, given[5].Y, given[5].Z);
        var target = Made(MorphTarget.FromPositions(cube, "Push", given));

        Assert.Equal("Push", target.Name);
        Assert.Equal(2, target.Count);
        Assert.Equal(given[2] - cube.Vertices[2], target.DeltaOf(2));
        Assert.Equal(given[5] - cube.Vertices[5], target.DeltaOf(5));
        Assert.Equal(Vector3.Zero, target.DeltaOf(0));
        Assert.False(target.Deltas.ContainsKey(0));
    }

    [Fact]
    public void Full_positions_skip_a_tombstone_slot_and_refuse_the_wrong_count_and_a_non_finite_coordinate()
    {
        var mesh = Ok(MeshDelete.Delete(Grid(2, 2), [8], []));
        Assert.True(mesh.IsVertexDeleted(8));
        var given = mesh.Vertices.ToArray();
        given[8] = new Vector3(float.NaN, 5, 5); // a tombstone's slot is skipped, whatever it holds
        Assert.Equal(0, Made(MorphTarget.FromPositions(mesh, "Same", given)).Count);

        Assert.Equal("positions: 8 positions given; the mesh has 9 vertex slots, and each needs one (a deleted slot's is ignored)",
            Refusal(MorphTarget.FromPositions(mesh, "Short", given.Take(8).ToArray())));

        given[3] = new Vector3(0, float.PositiveInfinity, 0);
        Assert.Equal("positions[3]: a coordinate is not finite", Refusal(MorphTarget.FromPositions(mesh, "Bad", given)));
    }

    [Fact]
    public void Sparse_positions_refuse_an_unknown_or_deleted_id_and_a_non_finite_coordinate_by_positions_id()
    {
        var mesh = Ok(MeshDelete.Delete(Grid(2, 2), [8], []));
        var refusal = Refusal(MorphTarget.FromPositions(mesh, "Bad", new Dictionary<int, Vector3>
        {
            [8] = Vector3.One,
            [9] = Vector3.One,
            [-1] = Vector3.One,
            [2] = new(float.NaN, 0, 0),
        }));
        Assert.Equal("positions[8]: deleted; positions[9]: there is no vertex 9: ids run 0..8; " +
            "positions[-1]: there is no vertex -1: ids run 0..8; positions[2]: a coordinate is not finite", refusal);

        var made = Made(MorphTarget.FromPositions(mesh, "Lift", new Dictionary<int, Vector3> { [4] = mesh.Vertices[4] + Vector3.UnitZ, [0] = mesh.Vertices[0] }));
        Assert.Equal([4], made.Deltas.Keys);
        Assert.Equal(Vector3.UnitZ, made.DeltaOf(4));
    }

    [Fact]
    public void Positions_whose_delta_overflows_a_float_are_refused_by_the_slot_full_or_sparse()
    {
        // RV-64: two finite coordinates far apart subtract to infinity in float; the file could not write it (T-101).
        var cube = Cube();
        var far = cube.WithVertices(cube.Vertices.Select((p, v) => v == 2 ? new Vector3(-3e38f, p.Y, p.Z) : p).ToArray());
        var given = far.Vertices.ToArray();
        given[2] = new Vector3(3e38f, given[2].Y, given[2].Z);
        Assert.True(float.IsInfinity(given[2].X - far.Vertices[2].X));
        const string Overflow = "positions[2]: the delta from the base position is not finite (the two are too far apart for a float)";
        Assert.Equal(Overflow, Refusal(MorphTarget.FromPositions(far, "Far", given)));
        Assert.Equal(Overflow, Refusal(MorphTarget.FromPositions(far, "Far", new Dictionary<int, Vector3> { [2] = given[2] })));
    }

    [Fact]
    public void Sparse_deltas_are_kept_as_given_and_refused_by_deltas_id()
    {
        var cube = Cube();
        var made = Made(MorphTarget.FromDeltas(cube, "D", new Dictionary<int, Vector3> { [1] = new(0.25f, 0, 0), [3] = Vector3.Zero }));
        Assert.Equal([1], made.Deltas.Keys);
        Assert.Equal(new Vector3(0.25f, 0, 0), made.DeltaOf(1));
        Assert.Equal("deltas[8]: there is no vertex 8: ids run 0..7",
            Refusal(MorphTarget.FromDeltas(cube, "D", new Dictionary<int, Vector3> { [8] = Vector3.One })));
    }

    [Theory]
    [InlineData("", "name: a target name must not be empty")]
    [InlineData(null, "name: a target name must not be empty")]
    [InlineData("arm:Bulk", "name: a target name must not contain ':' (as a mesh name must not)")]
    public void A_bad_name_is_refused_by_every_constructor(string? name, string expected)
    {
        var cube = Cube();
        Assert.Equal(expected, Refusal(MorphTarget.FromPositions(cube, name, cube.Vertices)));
        Assert.Equal(expected, Refusal(MorphTarget.FromPositions(cube, name, new Dictionary<int, Vector3>())));
        Assert.Equal(expected, Refusal(MorphTarget.FromDeltas(cube, name, new Dictionary<int, Vector3>())));
        Assert.Equal(expected, Refusal(MorphTarget.FromMesh(cube, name, cube)));
    }

    [Fact]
    public void Names_are_unique_and_case_sensitive()
    {
        var a = MorphTarget.Of("Bulk", []);
        var b = MorphTarget.Of("bulk", []);
        var both = MorphTargets.Of([a, b])!;
        Assert.Equal(["Bulk", "bulk"], both.Names);
        Assert.Same(b, both.Find("bulk"));
        Assert.Null(both.Find("BULK"));
        Assert.Throws<ArgumentException>(() => MorphTargets.Of([a, MorphTarget.Of("Bulk", [])]));
        Assert.Null(MorphTargets.Of([]));

        var replaced = both.With(MorphTarget.Of("Bulk", [KeyValuePair.Create(0, Vector3.One)]), out var was);
        Assert.True(was);
        Assert.Equal(["Bulk", "bulk"], replaced.Names);
        Assert.Equal(1, replaced.Find("Bulk")!.Count);
        Assert.Equal(["bulk", "Lean"], replaced.Without("Bulk")!.With(MorphTarget.Of("Lean", [])).Names);
        Assert.Null(MorphTargets.Of([a])!.Without("Bulk"));
    }

    [Fact]
    public void A_mesh_of_the_same_topology_gives_its_positions_as_the_target()
    {
        var arm = Arm();
        var lean = Arm(_ => 0.8);
        var target = Made(MorphTarget.FromMesh(arm, "Lean", lean));
        Assert.Equal(88, target.Count);
        var morphed = arm.WithTargets(MorphTargets.Of([target])).Morphed(W(("Lean", 1)));
        for (var v = 0; v < 88; v++) Assert.Equal(lean.Vertices[v], morphed.Vertices[v]);
    }

    [Fact]
    public void A_topology_mismatch_names_the_first_difference()
    {
        var cube = Cube();
        Assert.Equal("vertices: 8 here, 9 in the other mesh",
            Refusal(MorphTarget.FromMesh(cube, "T", Make(cube.Vertices.Append(new Vector3(5, 5, 5)), cube.Faces.Select(f => f.Loop.ToArray())))));

        Assert.Equal("faces: 6 here, 5 in the other mesh",
            Refusal(MorphTarget.FromMesh(cube, "T", Open(cube))));

        var swapped = cube.Faces.Select(f => f.Loop.ToArray()).ToArray();
        var loop = swapped[3];
        swapped[3] = [loop[1], loop[2], loop[3], loop[0]];
        Assert.Equal($"faces[3][0]: vertex {loop[0]} here, {loop[1]} in the other mesh",
            Refusal(MorphTarget.FromMesh(cube, "T", Make(cube.Vertices, swapped))));

        var tri = Make([new(0, 0, 0), new(1, 0, 0), new(1, 1, 0), new(0, 1, 0)], [[0, 1, 2, 3]]);
        var split = Make(tri.Vertices, [[0, 1, 2]]);
        Assert.Equal("faces[0]: 4 corners here, 3 in the other mesh", Refusal(MorphTarget.FromMesh(tri, "T", split)));

        var deleted = Ok(MeshDelete.Delete(Grid(2, 2), [8], []));
        Assert.Equal("vertices[8]: live here, deleted in the other mesh", Refusal(MorphTarget.FromMesh(Grid(2, 2), "T", deleted)));
        Assert.Equal("vertices[8]: deleted here, live in the other mesh", Refusal(MorphTarget.FromMesh(deleted, "T", Grid(2, 2))));

        // The first difference only: the face count differs too, after the vertex slots.
        Assert.Single(MorphTarget.FromMesh(cube, "T", Make(cube.Vertices.Append(Vector3.One), cube.Faces.Take(5).Select(f => f.Loop.ToArray()))).Errors);
    }

    [Fact]
    public void A_mismatch_in_holes_is_named_by_the_hole()
    {
        Vector3[] square = [new(0, 0, 0), new(4, 0, 0), new(4, 4, 0), new(0, 4, 0), new(1, 1, 0), new(1, 3, 0), new(3, 3, 0), new(3, 1, 0)];
        var holed = PolygonMesh.Create(square, [new Face([0, 1, 2, 3], [[4, 5, 6, 7]])]).Mesh!;
        var plain = PolygonMesh.Create(square, [new Face([0, 1, 2, 3])]);
        var other = PolygonMesh.Create(square, [new Face([0, 1, 2, 3], [[5, 6, 7, 4]])]).Mesh!;
        Assert.Equal("faces[0].holes[0][0]: vertex 4 here, 5 in the other mesh", Refusal(MorphTarget.FromMesh(holed, "T", other)));
        Assert.True(plain.Succeeded, string.Join("; ", plain.Errors));
        Assert.Equal("faces[0].holes: 1 here, 0 in the other mesh", Refusal(MorphTarget.FromMesh(holed, "T", plain.Mesh!)));
    }

    /// <summary>The cube with its last face left off: the same vertices, one face fewer.</summary>
    private static PolygonMesh Open(PolygonMesh cube) => Make(cube.Vertices, cube.Faces.Take(5).Select(f => f.Loop.ToArray()));

    // --- Evaluation ---

    [Fact]
    public void Morphed_at_0_half_and_1_and_two_targets_mixed()
    {
        var arm = Arm();
        var bulk = Bulk(arm);
        Assert.Equal(48, bulk.Count);
        var lean = Made(MorphTarget.FromMesh(arm, "Lean", Arm(_ => 0.8)));
        var mesh = With(arm, bulk, lean);

        Assert.Equal(arm.Vertices, mesh.Morphed(W()).Vertices);
        Assert.Equal(arm.Vertices, mesh.Morphed(W(("Bulk", 0), ("Lean", 0))).Vertices);
        Assert.Equal(arm.Vertices, mesh.Morphed(W(("Nobody", 1))).Vertices); // a name the mesh does not have is ignored

        var full = mesh.Morphed(W(("Bulk", 1)));
        var half = mesh.Morphed(W(("Bulk", 0.5f)));
        var mixed = mesh.Morphed(W(("Bulk", 0.5f), ("Lean", 0.5f)));
        for (var v = 0; v < 88; v++)
        {
            var b = arm.Vertices[v];
            Assert.Equal(b + bulk.DeltaOf(v), full.Vertices[v]);
            AssertNear(b + 0.5f * bulk.DeltaOf(v), half.Vertices[v], 1e-7f);
            var expected = new Vector3(
                (float)(b.X + 0.5 * bulk.DeltaOf(v).X + 0.5 * lean.DeltaOf(v).X),
                (float)(b.Y + 0.5 * bulk.DeltaOf(v).Y + 0.5 * lean.DeltaOf(v).Y),
                (float)(b.Z + 0.5 * bulk.DeltaOf(v).Z + 0.5 * lean.DeltaOf(v).Z));
            Assert.Equal(expected, mixed.Vertices[v]);
        }
        // An unmoved vertex keeps its base bits.
        Assert.Equal(arm.Vertices[0], half.Vertices[0]);

        // The morphed copy keeps faces, shading and style, and has no weights and no targets.
        Assert.Same(arm.Faces[0], mixed.Faces[0]);
        Assert.Equal(ShadingMode.Smooth, mixed.ShadingMode);
        Assert.Null(mixed.Targets);
        Assert.Null(mixed.Weights);
    }

    [Fact]
    public void The_sum_is_the_same_bits_every_time()
    {
        var arm = Arm();
        var mesh = With(arm, Bulk(arm), Made(MorphTarget.FromMesh(arm, "Lean", Arm(_ => 0.8))));
        var w = W(("Lean", 0.3f), ("Bulk", 0.7f));
        Assert.Equal(mesh.Morphed(w).Vertices, mesh.Morphed(new Dictionary<string, float>(w.Reverse())).Vertices);
    }

    [Theory]
    [InlineData(0.8)]
    [InlineData(1.35)]
    [InlineData(0.999)]
    [InlineData(0.5)]
    [InlineData(2.0)]
    public void At_weight_1_a_target_within_a_factor_of_2_of_its_base_gives_its_positions_back_bit_for_bit(double scale)
    {
        // given - base is exact in float when given is within a factor of 2 of base (Sterbenz), so base + delta is given.
        var arm = Arm();
        var other = Arm(_ => scale);
        var morphed = With(arm, Made(MorphTarget.FromMesh(arm, "T", other))).Morphed(W(("T", 1)));
        Assert.Equal(other.Vertices, morphed.Vertices);
    }

    [Theory]
    [InlineData(3.7)]
    [InlineData(1e-3)]
    public void Further_from_its_base_the_delta_of_the_sum_rounds_by_at_most_one_unit_in_the_last_place_of_the_larger(double scale)
    {
        // The delta is rounded to float once, so base + delta can come back up to 1 ulp (of the larger of base and given) off the given position (T-99's As built).
        var arm = Arm();
        var other = Arm(_ => scale);
        var morphed = With(arm, Made(MorphTarget.FromMesh(arm, "T", other))).Morphed(W(("T", 1)));
        var off = 0;
        for (var v = 0; v < 88; v++)
            for (var c = 0; c < 3; c++)
            {
                float g = other.Vertices[v][c], m = morphed.Vertices[v][c];
                if (g == m) continue;
                off++;
                var larger = Math.Max(Math.Abs(g), Math.Abs(arm.Vertices[v][c]));
                Assert.True(Math.Abs(g - m) <= MathF.BitIncrement(larger) - larger, $"vertex {v}[{c}]: {g} given, {m} back");
            }
        Assert.True(off > 0, "the rounding shows here; if it stops showing, this test's claim is out of date");
    }

    [Fact]
    public void Every_with_path_carries_the_targets()
    {
        var arm = Arm();
        var mesh = With(arm, Bulk(arm));
        var targets = mesh.Targets;
        Assert.NotNull(targets);
        Assert.Same(targets, mesh.WithStyle(null).Targets);
        Assert.Same(targets, mesh.WithShade(null).Targets);
        Assert.Same(targets, mesh.WithWeights(null).Targets);
        Assert.Same(targets, mesh.WithUvs(null).Targets);
        Assert.Same(targets, mesh.WithVertices(mesh.Vertices).Targets);
        Assert.Null(mesh.WithTargets(null).Targets);
        Assert.Null(arm.Targets);
    }

    // --- Posing ---

    [Fact]
    public void The_morph_comes_before_skinning_on_a_bent_chain()
    {
        var skeleton = Skeleton.Create([
            new("base", null, Vector3.Zero, Quaternion.Identity),
            new("joint", "base", new Vector3(0, 1, 0), Quaternion.Identity),
        ]).Skeleton!;
        var strip = BentStrip();
        var weights = VertexWeights.None;
        foreach (var v in strip.LiveVertices)
            weights = weights.With([v], [new Influence(strip.Vertices[v].Z > 0 ? 1 : 0, 1)]);
        var moved = strip.LiveVertices.ToDictionary(v => v, v => strip.Vertices[v] + new Vector3(0.1f, 0, 0.2f));
        var swell = Made(MorphTarget.FromPositions(strip, "Swell", moved));
        var mesh = With(strip.WithWeights(weights), swell);
        var (model, errors) = Model.Create([new NamedMesh("strip", mesh)], skeleton, []);
        Assert.Empty(errors);
        var bend = new Pose(new Dictionary<string, (Quaternion?, Vector3?)> { ["joint"] = (Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 3), null) });

        var skinOnly = Skinning.Positions(mesh, skeleton, bend);
        // At weight 0 (or with no weights given) the pose is the skin alone, bit for bit.
        Assert.Equal(skinOnly, Skinning.PosedOf(model!, bend, W(("Swell", 0)), null)!.Vertices);
        Assert.Equal(skinOnly, Skinning.PosedOf(model!, bend, null, null)!.Vertices);

        // At weight 1 it is the skin of the morphed mesh: the delta is rotated with its bone, not added after.
        var posed = Skinning.PosedOf(model!, bend, W(("Swell", 1)), null)!;
        var expected = Skinning.Positions(mesh.Morphed(W(("Swell", 1))).WithWeights(weights), skeleton, bend);
        Assert.Equal(expected, posed.Vertices);
        Assert.Null(posed.Targets);
        var bent = strip.LiveVertices.Where(v => weights.Of(v)[0].Bone == 1).ToArray();
        Assert.NotEmpty(bent);
        Assert.All(bent, v => Assert.True(Vector3.Distance(posed.Vertices[v], skinOnly[v] + swell.DeltaOf(v)) > 0.05f, $"vertex {v}"));
    }

    [Fact]
    public void A_model_with_no_skeleton_is_still_morphed()
    {
        var arm = Arm();
        var model = Model.Of("arm", With(arm, Bulk(arm)));
        var posed = Skinning.PosedOf(model, Pose.Rest, W(("Bulk", 1)), null)!;
        Assert.Equal(model.Meshes[0].Mesh.Morphed(W(("Bulk", 1))).Vertices, posed.Vertices);
        Assert.Same(model.Meshes[0].Mesh, Skinning.PosedOf(model, Pose.Rest, null, null));
    }

    // --- Subdivide ---

    [Fact]
    public void The_arm_cage_and_its_first_level_have_the_sketchs_counts()
    {
        var arm = Arm();
        Assert.Equal(88, arm.Vertices.Count);
        Assert.Equal(82, arm.Faces.Count);
        Assert.Equal(168, arm.Topology.Edges.Count);
        var one = Ok(MeshEdits.Subdivide(With(arm, Bulk(arm)), 1));
        Assert.Equal(338, one.Vertices.Count);
        Assert.Equal(336, one.Faces.Count);
        Assert.Equal(["Bulk"], one.Targets!.Names);
    }

    /// <summary>The M-18 hard-rimmed cylinder: 8 sides, 3 rings, both cap rims hard (creases) and one side edge hard, so the
    /// two rim vertices on it are corners.</summary>
    private static PolygonMesh HardCylinder()
    {
        var vertices = new List<Vector3>();
        for (var i = 0; i < 3; i++)
            for (var k = 0; k < 8; k++)
            {
                var a = 2 * Math.PI * k / 8;
                vertices.Add(new Vector3((float)Math.Round(0.5 * Math.Cos(a), 6), 0.5f * i, (float)Math.Round(-0.5 * Math.Sin(a), 6)));
            }
        var faces = new List<int[]>();
        for (var i = 0; i < 2; i++)
            for (var k = 0; k < 8; k++)
                faces.Add([8 * i + k, 8 * i + (k + 1) % 8, 8 * (i + 1) + (k + 1) % 8, 8 * (i + 1) + k]);
        faces.Add([7, 6, 5, 4, 3, 2, 1, 0]);
        faces.Add([16, 17, 18, 19, 20, 21, 22, 23]);
        var hard = Enumerable.Range(0, 8).SelectMany(k => new[] { (k, (k + 1) % 8), (16 + k, 16 + (k + 1) % 8) })
            .Concat([(0, 8), (8, 16)]);
        return Make(vertices, faces).WithShade(MeshShade.Of(ShadingMode.Smooth, hard));
    }

    public static TheoryData<string, int> Exactness => new()
    {
        { "arm", 1 }, { "arm", 2 }, { "cylinder", 1 }, { "cylinder", 2 },
    };

    [Theory]
    [MemberData(nameof(Exactness))]
    public void Subdividing_the_morphed_mesh_is_the_subdivided_base_plus_the_subdivided_delta(string shape, int levels)
    {
        PolygonMesh mesh;
        if (shape == "arm")
        {
            var arm = Arm();
            mesh = With(arm, Bulk(arm));
        }
        else
        {
            var cylinder = HardCylinder();
            // Every vertex moved, differently: a sweep that bulges, twists and lifts.
            var moved = cylinder.Vertices.Select((p, v) => p + new Vector3(0.1f * p.Y * p.X, 0.03f * (v % 5), 0.2f * p.Z * p.Y)).ToArray();
            mesh = With(cylinder, Made(MorphTarget.FromPositions(cylinder, "Sweep", moved)));
        }
        var name = mesh.Targets!.All[0].Name;
        var w = W((name, 1));

        var subdividedMorphed = Ok(MeshEdits.Subdivide(mesh.Morphed(w), levels));
        var subdivided = Ok(MeshEdits.Subdivide(mesh, levels));
        var morphedSubdivided = subdivided.Morphed(w);

        Assert.Equal(subdividedMorphed.Vertices.Count, morphedSubdivided.Vertices.Count);
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        foreach (var p in subdividedMorphed.Vertices) { min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
        var tolerance = 1e-6 * Vector3.Distance(min, max);
        var worst = 0.0;
        for (var v = 0; v < subdividedMorphed.Vertices.Count; v++)
            worst = Math.Max(worst, Vector3.Distance(subdividedMorphed.Vertices[v], morphedSubdivided.Vertices[v]));
        Assert.True(worst <= tolerance, $"worst vertex {worst:G6} apart, tolerance {tolerance:G6}");
        // And at weight 0 the subdivided mesh is the subdivided base exactly.
        Assert.Equal(Ok(MeshEdits.Subdivide(mesh.WithTargets(null), levels)).Vertices, subdivided.Morphed(W()).Vertices);
    }

    [Fact]
    public void Subdivide_keeps_a_tombstone_a_tombstone_with_no_delta()
    {
        var grid = Ok(MeshDelete.Delete(Grid(2, 2), [8], []));
        var target = Made(MorphTarget.FromPositions(grid, "Lift", grid.LiveVertices.ToDictionary(v => v, v => grid.Vertices[v] + Vector3.UnitZ)));
        var made = Ok(MeshEdits.Subdivide(With(grid, target), 1));
        var after = made.Targets!.Find("Lift")!;
        Assert.All(made.Vertices.Select((_, v) => v).Where(made.IsVertexDeleted), v => Assert.False(after.Deltas.ContainsKey(v)));
        Assert.True(made.IsVertexDeleted(8));
        // A uniform lift subdivides to a uniform lift on every live vertex.
        Assert.All(made.LiveVertices, v => AssertNear(Vector3.UnitZ, after.DeltaOf(v), 1e-6f));
    }

    // --- Combined ---

    [Fact]
    public void A_combined_model_offsets_target_ids_and_joins_a_name_on_two_meshes_into_one_target()
    {
        var arm = Arm();
        var cube = Cube();
        var armBulk = Bulk(arm);
        var cubeBulk = Made(MorphTarget.FromDeltas(cube, "Bulk", new Dictionary<int, Vector3> { [0] = Vector3.One }));
        var cubeTwist = Made(MorphTarget.FromDeltas(cube, "Twist", new Dictionary<int, Vector3> { [7] = Vector3.UnitX }));
        var model = Model.Of("arm", With(arm, armBulk)).With("plain", Grid(1, 1)).With("box", With(cube, cubeTwist, cubeBulk));

        Assert.Equal(["Bulk", "Twist"], model.TargetNames);
        var combined = model.Combined!;
        Assert.Equal(["Bulk", "Twist"], combined.Targets!.Names);
        var boxAt = model.OffsetOf("box")!.Value.Vertices;
        Assert.Equal(88 + 4, boxAt);
        var bulk = combined.Targets.Find("Bulk")!;
        Assert.Equal(48 + 1, bulk.Count);
        Assert.Equal(Vector3.One, bulk.DeltaOf(boxAt));
        Assert.Equal(armBulk.DeltaOf(8), bulk.DeltaOf(8));
        Assert.Equal(Vector3.UnitX, combined.Targets.Find("Twist")!.DeltaOf(boxAt + 7));

        // The combined morph is each mesh's morph, side by side.
        var w = W(("Bulk", 1), ("Twist", 0.5f));
        var together = combined.Morphed(w).Vertices;
        var apart = model.Meshes.SelectMany(m => m.Mesh.Morphed(w).Vertices).ToArray();
        Assert.Equal(apart, together);

        Assert.Null(Model.Of("arm", arm).With("box", cube).Combined!.Targets);
        Assert.Empty(Model.Of("arm", arm).TargetNames);
    }

    private static void AssertNear(Vector3 expected, Vector3 actual, float tolerance) =>
        Assert.True(Vector3.Distance(expected, actual) <= tolerance, $"expected {expected}, got {actual}");
}
