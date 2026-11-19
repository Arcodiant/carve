using System.Numerics;
using System.Security.Cryptography;
using Carve.Core;
using Carve.Core.Tests;
using Carve.Rover;
using Empyrean;
using SharpGLTF.Schema2;
using PolygonMesh = Carve.Core.PolygonMesh;
using Model = Carve.Core.Model;
using Skeleton = Carve.Core.Skeleton;
using MorphTarget = Carve.Core.MorphTarget;
using MorphTargets = Carve.Core.MorphTargets;

namespace Carve.Server.Tests;

/// <summary>
/// T-95: a part tree exported as glTF nodes (M-9's Export ruling): static parts merged into their nearest moving ancestor or
/// the root, a node per moving part, moving instances sharing one mesh; read back with the engine's split load on the CPU.
/// </summary>
[Trait("Tier", "unit")]
public class GlbPartsTests
{
    private static readonly IModelGeometryLoader Parser = GlbExportTests.Parser;

    private static byte[] Export(Model model, string name)
    {
        var layout = GlbParts.Plan(model, name, out var refusal);
        Assert.True(layout is not null, refusal);
        return GlbParts.Write(model, layout!);
    }

    private static ParsedModel Split(byte[] bytes) => Parser.Parse(bytes, ModelFormat.Gltf, new ModelParseOptions(SplitParts: true));

    private static Model Of(IReadOnlyList<NamedMesh> meshes, params Part[] parts)
    {
        var (model, errors) = Model.Create(meshes, null, [], [], parts);
        Assert.True(model is not null, string.Join("; ", errors.Select(e => e.ToString())));
        return model!;
    }

    private static int Triangles(ParsedPartMesh mesh) => mesh.MeshData.Indices!.Length / 3;

    private static readonly Model Rover = RoverSpec.Build();

    private static ParsedModel RoverSplit() => Split(Export(Rover, "rover"));

    [Fact]
    public void The_rovers_split_load_keeps_the_root_the_mast_and_four_wheels_in_pre_order_with_no_warnings()
    {
        var parsed = RoverSplit();
        Assert.Empty(parsed.Warnings ?? []);
        var parts = parsed.Parts!.Parts;
        Assert.Equal(["rover", "Mast", "Wheel_FL", "Wheel_FR", "Wheel_RL", "Wheel_RR"], parts.Select(p => p.Name));
        Assert.Equal([-1, 0, 0, 0, 0, 0], parts.Select(p => p.ParentIndex));
    }

    [Theory]
    [InlineData("Mast")]
    [InlineData("Wheel_FL")]
    [InlineData("Wheel_FR")]
    [InlineData("Wheel_RL")]
    [InlineData("Wheel_RR")]
    public void Each_moving_parts_translation_and_rotation_are_its_composed_ones(string name)
    {
        var part = RoverSplit().Parts!.Parts.Single(p => p.Name == name);
        Assert.True(Matrix4x4.Decompose(Rover.WorldTransform(name)!.Value, out var scale, out var rotation, out var translation));
        Assert.True(Vector3.Distance(translation, part.Translation) < 1e-6f, $"{part.Translation} against {translation}");
        Assert.True(MathF.Abs(Quaternion.Dot(rotation, part.Rotation)) > 1 - 1e-6f, $"{part.Rotation} against {rotation}");
        Assert.True(Vector3.Distance(scale, part.Scale) < 1e-6f);
    }

    [Fact]
    public void A_moving_part_under_a_turned_uniformly_scaled_static_parent_takes_the_parents_turn_and_scale()
    {
        // The rover's static ancestors are not turned, so this is the case where a static ancestor's rotation and uniform scale enter the node's.
        var quarter = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2);
        var tilt = Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI / 6);
        var model = Of([new("Base", TestMeshes.Cube()), new("Arm", TestMeshes.Cube())],
            new Part("Base") { Rotation = quarter, Scale = new Vector3(2, 2, 2), Translation = new Vector3(1, 0, 0) },
            new Part("Arm") { Parent = "Base", Moves = true, Rotation = tilt, Translation = new Vector3(0, 0, 1) });
        var arm = Split(Export(model, "arm")).Parts!.Parts.Single(p => p.Name == "Arm");
        Assert.True(Matrix4x4.Decompose(model.WorldTransform("Arm")!.Value, out var scale, out var rotation, out var translation));
        Assert.True(Vector3.Distance(new Vector3(3, 0, 0), translation) < 1e-5f, $"{translation}");
        Assert.True(Vector3.Distance(translation, arm.Translation) < 1e-5f, $"{arm.Translation} against {translation}");
        Assert.True(MathF.Abs(Quaternion.Dot(rotation, arm.Rotation)) > 1 - 1e-6f, $"{arm.Rotation} against {rotation}");
        Assert.True(Vector3.Distance(new Vector3(2, 2, 2), arm.Scale) < 1e-5f, $"{arm.Scale}");
    }

    [Fact]
    public void The_front_left_wheel_and_the_mast_sit_where_the_sketch_puts_them()
    {
        var parts = RoverSplit().Parts!.Parts;
        Assert.True(Vector3.Distance(new Vector3(0.7f, 0.4f, 0.72f), parts.Single(p => p.Name == "Wheel_FL").Translation) < 1e-6f);
        Assert.True(Vector3.Distance(new Vector3(-0.6f, 0.8f, 0f), parts.Single(p => p.Name == "Mast").Translation) < 1e-6f);
        Assert.Equal(Quaternion.Identity, parts.Single(p => p.Name == "rover").Rotation);
        Assert.Equal(Vector3.Zero, parts.Single(p => p.Name == "rover").Translation);
    }

    [Fact]
    public void The_four_wheels_share_one_mesh_and_each_part_has_T91s_triangles()
    {
        var split = RoverSplit().Parts!;
        var parts = split.Parts;
        var wheel = parts.Single(p => p.Name == "Wheel_FL").MeshIndex;
        Assert.All(parts.Where(p => p.Name.StartsWith("Wheel_", StringComparison.Ordinal)), p => Assert.Equal(wheel, p.MeshIndex));
        Assert.Equal(3, split.Meshes.Length);
        // T-91's counts, live: Chassis 12 + Cab 28; Mast 12 + Lamp 28; a wheel 124.
        Assert.Equal(40, Triangles(split.Meshes[parts.Single(p => p.Name == "rover").MeshIndex]));
        Assert.Equal(40, Triangles(split.Meshes[parts.Single(p => p.Name == "Mast").MeshIndex]));
        Assert.Equal(124, Triangles(split.Meshes[wheel]));
    }

    [Fact]
    public void The_merged_meshs_bounds_are_Combineds()
    {
        var parsed = RoverSplit();
        var min = new Vector3(float.PositiveInfinity);
        var max = new Vector3(float.NegativeInfinity);
        foreach (var v in parsed.MeshData.Vertices)
        {
            min = Vector3.Min(min, v.Position);
            max = Vector3.Max(max, v.Position);
        }
        var report = MeshReport.Create(Rover.Combined!);
        Assert.True(Vector3.Distance(report.BoundsMin, min) < 1e-5f, $"{min} against {report.BoundsMin}");
        Assert.True(Vector3.Distance(report.BoundsMax, max) < 1e-5f, $"{max} against {report.BoundsMax}");
        Assert.Equal(576, parsed.MeshData.Indices!.Length / 3);
    }

    [Fact]
    public void A_static_non_uniform_scale_is_folded_into_the_vertices_and_the_normals_by_the_inverse_transpose()
    {
        // A static cube turned 45 degrees about z under a static parent scaled (2, 1, 1), beside a moving cube: its +x face's
        // normal, (cos 45, sin 45, 0) once turned, is (0.5 cos 45, sin 45, 0) normalised by the inverse transpose; (2 cos 45, ...) by the matrix itself.
        var turned = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 4);
        var model = Of([new("Stretch", TestMeshes.Cube()), new("Box", TestMeshes.Cube()), new("Mover", TestMeshes.Cube())],
            new Part("Stretch") { Scale = new Vector3(2, 1, 1) },
            new Part("Box") { Parent = "Stretch", Rotation = turned, Translation = new Vector3(0, 3, 0) },
            new Part("Mover") { Moves = true, Translation = new Vector3(10, 0, 0) });
        var split = Split(Export(model, "stretch")).Parts!;
        var root = split.Meshes[split.Parts[0].MeshIndex].MeshData;
        Assert.Equal(24, root.Indices!.Length / 3);

        var box = Model.Join([(TestMeshes.Cube(), model.WorldTransform("Box")!.Value)])!;
        var expectedPositions = box.Vertices.Select(v => v).ToList();
        Assert.All(root.Vertices.Skip(root.Vertices.Length / 2), v => Assert.Contains(expectedPositions, p => Vector3.Distance(p, v.Position) < 1e-5f));
        var inverse = Vector3.Normalize(new Vector3(0.5f * MathF.Cos(MathF.PI / 4), MathF.Sin(MathF.PI / 4), 0));
        var direct = Vector3.Normalize(new Vector3(2 * MathF.Cos(MathF.PI / 4), MathF.Sin(MathF.PI / 4), 0));
        Assert.Contains(root.Vertices, v => Vector3.Distance(v.Normal, inverse) < 1e-5f);
        Assert.DoesNotContain(root.Vertices, v => Vector3.Distance(v.Normal, direct) < 1e-3f);
    }

    [Fact]
    public void A_smooth_parts_corner_normals_under_a_static_non_uniform_scale_are_moved_by_the_inverse_transpose()
    {
        // A flat face's normal is taken again from its moved corners, so only a smooth mesh's corner normals rest on the fold:
        // the (+x, -y, +z) corner's (1, -1, 1)/sqrt 3, turned 45 degrees about z, then through the parent's (2, 1, 1).
        var turned = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 4);
        var smooth = TestMeshes.Cube().WithShade(MeshShade.Flat.WithMode(ShadingMode.Smooth));
        var model = Of([new("Stretch", TestMeshes.Cube()), new("Box", smooth), new("Mover", TestMeshes.Cube())],
            new Part("Stretch") { Scale = new Vector3(2, 1, 1) },
            new Part("Box") { Parent = "Stretch", Rotation = turned, Translation = new Vector3(0, 3, 0) },
            new Part("Mover") { Moves = true, Translation = new Vector3(10, 0, 0) });
        var split = Split(Export(model, "stretch")).Parts!;
        var root = split.Meshes[split.Parts[0].MeshIndex].MeshData;

        var corner = Vector3.Transform(Vector3.Normalize(new Vector3(1, -1, 1)), turned);
        var inverse = Vector3.Normalize(corner * new Vector3(0.5f, 1, 1));
        var direct = Vector3.Normalize(corner * new Vector3(2, 1, 1));
        Assert.Contains(root.Vertices, v => Vector3.Distance(v.Normal, inverse) < 1e-4f);
        Assert.DoesNotContain(root.Vertices, v => Vector3.Distance(v.Normal, direct) < 1e-3f);
    }

    [Fact]
    public void A_static_instance_under_a_static_parent_is_copied_into_the_root_at_its_place()
    {
        var model = Of([new("Box", TestMeshes.Cube()), new("Mover", TestMeshes.Cube())],
            new Part("Box"),
            new Part("Group") { Translation = new Vector3(5, 0, 0) },
            new Part("Copy") { Parent = "Group", InstanceOf = "Box", Translation = new Vector3(0, 2, 0) },
            new Part("Mover") { Moves = true });
        var split = Split(Export(model, "copies")).Parts!;
        Assert.Equal(["copies", "Mover"], split.Parts.Select(p => p.Name));
        var root = split.Meshes[split.Parts[0].MeshIndex].MeshData;
        Assert.Equal(24, root.Indices!.Length / 3);
        var copy = Model.Join([(TestMeshes.Cube(), model.WorldTransform("Copy")!.Value)])!;
        var max = root.Vertices.Select(v => v.Position).Aggregate(Vector3.Max);
        Assert.Equal(copy.Vertices.Aggregate(Vector3.Max), max);
    }

    [Fact]
    public void A_moving_empty_is_a_node_without_a_mesh_with_its_moving_child_under_it()
    {
        var model = Of([new("Box", TestMeshes.Cube())],
            new Part("Arm") { Moves = true, Translation = new Vector3(0, 1, 0) },
            new Part("Box") { Parent = "Arm", Moves = true, Translation = new Vector3(1, 0, 0) });
        var layout = GlbParts.Plan(model, "arm", out _)!;
        Assert.Null(layout.Root.Mesh);
        Assert.Null(layout.Root.Children.Single().Mesh);
        var split = Split(GlbParts.Write(model, layout)).Parts!;
        Assert.Equal(["arm", "Arm", "Box"], split.Parts.Select(p => p.Name));
        Assert.Equal([-1, -1], split.Parts.Take(2).Select(p => p.MeshIndex));
        Assert.Equal(new Vector3(1, 0, 0), split.Parts[2].Translation);
        Assert.Equal(1, split.Parts[2].ParentIndex);
    }

    [Fact]
    public void A_moving_instance_with_static_children_gets_a_mesh_of_its_own_and_the_others_still_share()
    {
        // The person's ruling of 2026-11-18 (T-95, question 3): the source's geometry plus its static descendants', not shared.
        var model = Of([new("Wheel", TestMeshes.Cube()), new("Cap", TestMeshes.Cube())],
            new Part("Wheel") { Moves = true },
            new Part("A") { InstanceOf = "Wheel", Moves = true, Translation = new Vector3(3, 0, 0) },
            new Part("B") { InstanceOf = "Wheel", Moves = true, Translation = new Vector3(6, 0, 0) },
            new Part("Cap") { Parent = "B", Translation = new Vector3(0, 0, 2) });
        var split = Split(Export(model, "wheels")).Parts!;
        var parts = split.Parts;
        Assert.Equal(["wheels", "Wheel", "A", "B"], parts.Select(p => p.Name));
        Assert.Equal(-1, parts[0].MeshIndex);
        Assert.Equal(parts[1].MeshIndex, parts[2].MeshIndex);
        Assert.NotEqual(parts[1].MeshIndex, parts[3].MeshIndex);
        Assert.Equal(24, Triangles(split.Meshes[parts[3].MeshIndex]));
    }

    [Fact]
    public void A_moving_part_sheared_by_a_static_ancestors_non_uniform_scale_is_refused()
    {
        // The person's ruling of 2026-11-18 (T-95, question 1): refuse, naming the part and the ancestor.
        var model = Of([new("Base", TestMeshes.Cube()), new("Arm", TestMeshes.Cube())],
            new Part("Base") { Scale = new Vector3(2, 1, 1) },
            new Part("Arm") { Parent = "Base", Moves = true, Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 4) });
        Assert.Null(GlbParts.Plan(model, "sheared", out var refusal));
        Assert.Contains("part 'Arm' moves", refusal);
        Assert.Contains("'Base'", refusal);
        Assert.Contains("shear", refusal);
    }

    [Fact]
    public void A_half_turn_under_a_non_uniform_scale_keeps_the_axes_and_is_written()
    {
        var model = Of([new("Base", TestMeshes.Cube()), new("Arm", TestMeshes.Cube())],
            new Part("Base") { Scale = new Vector3(2, 1, 1) },
            new Part("Arm") { Parent = "Base", Moves = true, Rotation = new Quaternion(0, 1, 0, 0), Translation = new Vector3(1, 0, 0) });
        var arm = Split(Export(model, "turned")).Parts!.Parts.Single(p => p.Name == "Arm");
        Assert.True(Vector3.Distance(new Vector3(2, 1, 1), arm.Scale) < 1e-5f, arm.Scale.ToString());
        Assert.True(Vector3.Distance(new Vector3(2, 0, 0), arm.Translation) < 1e-5f, arm.Translation.ToString());
        Assert.True(MathF.Abs(Quaternion.Dot(new Quaternion(0, 1, 0, 0), arm.Rotation)) > 1 - 1e-5f, arm.Rotation.ToString());
    }

    [Fact]
    public void Moving_parts_share_one_glTF_material_per_colour()
    {
        var root = ModelRoot.ParseGLB(Export(Rover, "rover"));
        Assert.Equal(root.LogicalMaterials.Count, root.LogicalMaterials.Select(m => m.Name).Distinct().Count());
        Assert.Equal(3, root.LogicalMeshes.Count);
        Assert.Equal(6, root.LogicalNodes.Count);
    }

    [Fact]
    public void Morph_targets_on_a_moving_part_are_written_on_its_mesh_and_the_merged_load_reads_them()
    {
        var cube = TestMeshes.Cube();
        var bulged = cube.WithTargets(MorphTargets.Of([MorphTarget.Of("Bulge", [KeyValuePair.Create(0, new Vector3(0, 1, 0))])]));
        var model = Of([new("Body", cube), new("Lid", bulged)],
            new Part("Body"),
            new Part("Lid") { Moves = true, Translation = new Vector3(0, 2, 0) });
        var bytes = Export(model, "lid");
        var gltf = ModelRoot.ParseGLB(bytes);
        Assert.Equal(1, gltf.LogicalNodes.Single(n => n.Name == "Lid").Mesh.Primitives[0].MorphTargetsCount);
        Assert.Equal(0, gltf.LogicalNodes.Single(n => n.Name == "lid").Mesh.Primitives[0].MorphTargetsCount);
        Assert.Equal(["Bulge"], Parser.Parse(bytes, ModelFormat.Gltf).MorphTargets!.TargetNames);
        Assert.Throws<NotSupportedException>(() => Split(bytes));
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    [Fact]
    public void A_two_mesh_static_model_writes_todays_bytes()
    {
        // Taken on a2b8878 (M-9 before T-95's export change): two meshes merged into one, through export's static path.
        var model = Of([new("Box", TestMeshes.Cube()), new("Barrel", TestMeshes.Barrel())]);
        Assert.False(GlbParts.HasMoving(model));
        var mesh = model.Combined!;
        Assert.Equal(TwoMeshHash, Hash(GlbExport.Write(mesh, Shading.Triangles(mesh), "two", model.Materials)));
    }

    internal const string TwoMeshHash = "880563D95CE6249848A1232B46535FB6C9B4CC4F99DAA9049AEC1E20F7EDEBFB";

    [Fact]
    public void A_skinned_model_with_no_part_transforms_is_baked_as_itself()
    {
        var path = Path.Combine(Paths.RepoRoot, "samples", "gardener-soft.carve");
        var read = CarveFile.Read(File.ReadAllText(path), Path.GetDirectoryName(path));
        Assert.Same(read.Model!, SkinnedExport.Baked(read.Model!));
        Assert.Null(SkinnedExport.MovesWarning(read.Model!));
    }

    [Fact]
    public void A_skinned_model_bakes_each_part_at_its_place_and_copies_instances()
    {
        var skeleton = Skeleton.Create([new BoneSpec("Root", null, Vector3.Zero, Quaternion.Identity)]).Skeleton!;
        var cube = TestMeshes.Cube();
        var (model, errors) = Model.Create([new("Box", cube)], skeleton, [], [],
            [new Part("Box") { Translation = new Vector3(0, 1, 0), Moves = true }, new Part("Copy") { InstanceOf = "Box", Translation = new Vector3(3, 0, 0) }]);
        Assert.True(model is not null, string.Join("; ", errors));
        var baked = SkinnedExport.Baked(model!);
        Assert.Equal(["Box", "Copy"], baked.Names);
        Assert.Equal(cube.Vertices[0] + new Vector3(0, 1, 0), baked.Find("Box")!.Vertices[0]);
        Assert.Equal(cube.Vertices[0] + new Vector3(3, 0, 0), baked.Find("Copy")!.Vertices[0]);
        Assert.Equal("moves has no effect on a skinned export, which bakes every part into the skinned mesh: 'Box'", SkinnedExport.MovesWarning(model!));
    }

    [Fact]
    public void A_piece_of_a_placed_part_is_its_own_geometry()
    {
        var skeleton = Skeleton.Create([new BoneSpec("Root", null, Vector3.Zero, Quaternion.Identity)]).Skeleton!;
        var cube = TestMeshes.Cube();
        var (model, _) = Model.Create([new("Box", cube)], skeleton, [], [], [new Part("Box") { Translation = new Vector3(0, 1, 0) }]);
        // The piece is what the skinned export writes, after its bake: the part's own geometry, not placed.
        Assert.Equal(cube.Vertices, SkinnedExport.Baked(PieceExport.Select(model!, ["Box"])).Find("Box")!.Vertices);
    }
    /// <summary>A two-bone arm, a cube weighted wholly to its upper bone on a part turned and moved, and an instance of it elsewhere (RV-71).</summary>
    private static Model PlacedRig()
    {
        var skeleton = Skeleton.Create([
            new BoneSpec("Root", null, Vector3.Zero, Quaternion.Identity),
            new BoneSpec("Arm", "Root", new Vector3(0, 1, 0), Quaternion.Identity),
        ]).Skeleton!;
        var cube = TestMeshes.Cube();
        var weights = VertexWeights.None;
        for (var v = 0; v < cube.Vertices.Count; v++) weights = weights.With([v], [new Influence(v % 2, 1)]);
        var (model, errors) = Model.Create([new("Box", cube.WithWeights(weights))], skeleton, [], [],
            [new Part("Box") { Translation = new Vector3(2, 1, 0), Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.7f) },
             new Part("Copy") { InstanceOf = "Box", Translation = new Vector3(-3, 0, 1) }]);
        Assert.True(model is not null, string.Join("; ", errors));
        return model!;
    }

    private static readonly Pose Bent = new(new Dictionary<string, (Quaternion?, Vector3?)>
    {
        ["Arm"] = (Quaternion.CreateFromAxisAngle(Vector3.UnitZ, 0.8f), null),
    });

    [Fact]
    public void A_posed_placed_rigged_part_is_drawn_as_the_skinned_export_bakes_it()
    {
        var model = PlacedRig();
        // The game skins the baked file: each occurrence placed in model space, then skinned against the model-space bones.
        var expected = Skinning.Posed(SkinnedExport.Baked(model), Bent).Combined!;
        var drawn = Skinning.PosedOf(model, Bent, null, null)!;
        Assert.Equal(expected.Vertices, drawn.Vertices);
        Assert.Equal(Skinning.Posed(SkinnedExport.Baked(model), Bent).Find("Copy")!.Vertices,
            Skinning.PosedOf(model, Bent, null, "Copy")!.Vertices);
        // Skinning in part space and then placing, as before, is not what the game draws.
        Assert.NotEqual(expected.Vertices, Skinning.Posed(model, Bent).Combined!.Vertices);
    }

    [Fact]
    public void A_model_with_no_part_transforms_is_drawn_posed_as_before()
    {
        var path = Path.Combine(Paths.RepoRoot, "samples", "gardener-soft.carve");
        var model = CarveFile.Read(File.ReadAllText(path), Path.GetDirectoryName(path)).Model!;
        var pose = new Pose(new Dictionary<string, (Quaternion?, Vector3?)>
        {
            [model.Skeleton!.Bones[1].Name] = (Quaternion.CreateFromAxisAngle(Vector3.UnitX, 0.6f), null),
        });
        var before = Skinning.Posed(model, pose).Combined!;
        var now = Skinning.PosedOf(model, pose, null, null)!;
        Assert.Equal(before.Vertices, now.Vertices);
        Assert.Equal(Shading.Corners(before), Shading.Corners(now));
    }

    [Fact]
    public void Weigh_weighs_a_placed_part_where_it_is_drawn()
    {
        var model = PlacedRig();
        var store = new ModelStore();
        store.OpenModel(model, "open");
        Assert.NotNull(store.Weigh("weigh", "Box", null, true, out _, out var problems));
        Assert.Empty(problems);
        var baked = SkinnedExport.Baked(model).Find("Box")!;
        var expected = AutoWeights.Weigh(baked, model.Skeleton!, null, true, out _);
        var weighed = store.Current.Model.Find("Box")!.Weights!;
        foreach (var v in baked.LiveVertices)
            Assert.Equal(expected.Of(v), weighed.Of(v));
        Assert.NotEqual(AutoWeights.Weigh(model.Find("Box")!, model.Skeleton!, null, true, out _).Of(0), weighed.Of(0));
    }
}
