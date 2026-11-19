using System.Numerics;
using Xunit;

namespace Carve.Core.Tests;

/// <summary>
/// T-91's part tree in the Core (D-2, M-9): every mesh is a part, the pre-order, the refusals of <see cref="Model.WithPart(Part)"/>,
/// <see cref="Model.Create(IReadOnlyList{NamedMesh}, Skeleton?, IReadOnlyList{Clip}, IReadOnlyList{Material}, IReadOnlyList{Part})"/>,
/// <see cref="Model.Target"/> and <see cref="Model.WithLevel(string, int)"/>, world transforms, kept levels, a world-placed
/// <see cref="Model.Combined"/>, and envelope warnings.
/// </summary>
[Trait("Tier", "unit")]
public class PartTreeTests
{
    private static readonly Quaternion Turned = new(0, 1, 0, 0); // 180° about +Y

    private static Model Cube(string name) => Model.Of(name, TestMeshes.Cube());

    /// <summary>A small tree: Body (a cube) with an empty Axle under it, a Wheel cube under the Axle and an instance of it.</summary>
    private static Model Tree() => Cube("Body")
        .WithPart(new Part("Axle") { Parent = "Body", Translation = new(0, 0.5f, 0) })
        .With("Wheel", TestMeshes.Cube())
        .WithPart(new Part("Wheel") { Parent = "Axle", Translation = new(0, 0, 2) })
        .WithPart(new Part("Wheel_R") { Parent = "Axle", Translation = new(0, 0, -2), Rotation = Turned, InstanceOf = "Wheel" });

    private static string Refusal(Model model, Part part)
    {
        Assert.Null(model.WithPart(part, out var problem));
        Assert.Throws<ArgumentException>(() => model.WithPart(part));
        return problem!;
    }

    private static void Near(Vector3 expected, Vector3 actual, float within = 1e-5f) =>
        Assert.True(Vector3.Distance(expected, actual) <= within, $"expected {expected}, got {actual}");

    // ---- Every mesh is a part --------------------------------------------------------------------------------------

    [Fact]
    public void A_plain_named_mesh_is_a_top_level_part_at_the_origin()
    {
        var model = Cube("a").With("b", TestMeshes.Cube());
        Assert.Equal(["a", "b"], model.Parts.Select(p => p.Name));
        var a = model.FindPart("a")!;
        Assert.True(a.IsPlain);
        Assert.Equal(Part.Of("a").Rotation, a.Rotation);
        Assert.Equal(Vector3.One, a.Scale);
        Assert.Null(a.Envelope);
        Assert.Empty(a.Levels);
        Assert.False(a.Moves);
        Assert.Equal(Matrix4x4.Identity, model.WorldTransform("a"));
        Assert.Null(model.FindPart("c"));
        Assert.Null(model.WorldTransform("c"));
    }

    [Fact]
    public void Parts_are_in_pre_order_with_siblings_in_definition_order()
    {
        var model = Cube("B")
            .WithPart(new Part("A"))
            .WithPart(new Part("C") { Parent = "A" })
            .WithPart(new Part("D") { Parent = "B" })
            .WithPart(new Part("E") { Parent = "A" })
            .WithPart(new Part("F") { Parent = "C" });
        Assert.Equal(["B", "D", "A", "C", "F", "E"], model.Parts.Select(p => p.Name));
        // Replacing a part keeps its place among its siblings.
        Assert.Equal(["B", "D", "A", "C", "F", "E"], model.WithPart(new Part("C") { Parent = "A", Moves = true }).Parts.Select(p => p.Name));
        // A child given before its parent in a file still comes after it.
        var (made, errors) = Model.Create([new NamedMesh("Kid", TestMeshes.Cube())], null, [], [], [new Part("Kid") { Parent = "Top" }, new Part("Top")]);
        Assert.Empty(errors);
        Assert.Equal(["Top", "Kid"], made!.Parts.Select(p => p.Name));
    }

    // ---- Refusals ---------------------------------------------------------------------------------------------------

    [Fact]
    public void WithPart_refuses_a_change_to_an_existing_parts_instance_of()
    {
        var model = Tree().With("Other", TestMeshes.Cube());
        var copy = model.FindPart("Wheel_R")!;
        Assert.Equal("instance_of: 'Wheel_R' is already an instance of 'Wheel', and an existing part's instance_of does not change",
            Refusal(model, copy with { InstanceOf = "Other" }));
        Assert.Equal("instance_of: 'Wheel_R' is already an instance of 'Wheel', and an existing part's instance_of does not change",
            Refusal(model, copy with { InstanceOf = null }));
        Assert.Equal("instance_of: 'Axle' already exists as an empty part, and an existing part's instance_of does not change",
            Refusal(model, model.FindPart("Axle")! with { InstanceOf = "Wheel" }));
        Assert.Equal("instance_of: 'Other' already exists with geometry of its own, and an existing part's instance_of does not change",
            Refusal(model, model.FindPart("Other")! with { InstanceOf = "Wheel" }));
        // The same instance_of, with any other change, is placed.
        Assert.NotNull(model.WithPart(copy with { Translation = new(0, 0, -3) }, out _));
    }

    [Fact]
    public void WithPart_refuses_each_bad_field_by_name()
    {
        var model = Tree();
        Assert.Equal("parent: no part named 'Axel_F'; the parts are 'Body', 'Axle', 'Wheel', 'Wheel_R'",
            Refusal(model, new Part("X") { Parent = "Axel_F" }));
        Assert.StartsWith("rotation: not a unit quaternion: its length is 2,", Refusal(model, new Part("X") { Rotation = new(0, 0, 0, 2) }));
        Assert.Equal("scale: each component must be > 0 (a mirroring scale is refused: Empyrean's split load refuses a mirrored node)",
            Refusal(model, new Part("X") { Scale = new(1, -1, 1) }));
        Assert.Equal("scale: each component must be > 0 (a mirroring scale is refused: Empyrean's split load refuses a mirrored node)",
            Refusal(model, new Part("X") { Scale = new(1, 0, 1) }));
        Assert.Equal("translation: must be three finite numbers", Refusal(model, new Part("X") { Translation = new(float.NaN, 0, 0) }));
        Assert.Equal("envelope: min is above max on y; min must be at or below max on every axis",
            Refusal(model, new Part("X") { Envelope = new(new(0, 1, 0), new(1, 0, 1)) }));
        Assert.Equal("name: a part name must not be empty", Refusal(model, new Part("")));
        Assert.Equal("instance_of: 'Axle' has no geometry", Refusal(model, new Part("X") { InstanceOf = "Axle" }));
        // An owner made an instance is refused first as a changed instance_of (RV-71); the file's check still says "cannot also own geometry".
        Assert.Equal("instance_of: 'Body' already exists with geometry of its own, and an existing part's instance_of does not change",
            Refusal(model, new Part("Body") { InstanceOf = "Wheel" }));
        Assert.Equal("instance_of: 'Wheel_R' is itself an instance of 'Wheel'; name 'Wheel', the part that owns the geometry",
            Refusal(model, new Part("X") { InstanceOf = "Wheel_R" }));
        Assert.Equal("instance_of: 'X' cannot be an instance of itself", Refusal(model, new Part("X") { InstanceOf = "X" }));
        Assert.Equal("instance_of: no part named 'Nope'; the parts are 'Body', 'Axle', 'Wheel', 'Wheel_R'",
            Refusal(model, new Part("X") { InstanceOf = "Nope" }));
        Assert.Equal("parent: 'Body' would be its own ancestor", Refusal(model, new Part("Body") { Parent = "Wheel" }));
        Assert.Equal("parent: 'Axle' would be its own ancestor", Refusal(model, new Part("Axle") { Parent = "Axle" }));
        var level = new PartLevel(0, TestMeshes.Cube());
        Assert.Equal("levels: 'Wheel_R' is an instance of 'Wheel', and shows the levels of 'Wheel'; keep levels on 'Wheel'",
            Refusal(model, model.FindPart("Wheel_R")! with { Levels = [level] }));
        Assert.Equal("levels: 'Axle' is an empty part with no geometry to keep a level of",
            Refusal(model, model.FindPart("Axle")! with { Levels = [level] }));
        Assert.Equal("levels[1]: level 0 comes after level 0; levels are kept in ascending order, each number once",
            Refusal(model, model.FindPart("Body")! with { Levels = [level, level] }));
        var styled = TestMeshes.Cube().WithStyle(FaceStyle.None.WithMaterial([0], "steel"));
        Assert.Equal("levels[0]: a face uses the material 'steel', which the model does not define",
            Refusal(model, model.FindPart("Body")! with { Levels = [new PartLevel(0, styled)] }));
        // A rotation within 0.001 of unit length is kept as given.
        var nearly = new Quaternion(0, 0, 0, 1.0009f);
        Assert.Equal(nearly, model.WithPart(new Part("X") { Rotation = nearly }).FindPart("X")!.Rotation);
    }

    [Fact]
    public void An_instance_cannot_be_given_geometry()
    {
        var thrown = Assert.Throws<ArgumentException>(() => Tree().With("Wheel_R", TestMeshes.Cube()));
        Assert.StartsWith("mesh: 'Wheel_R' is an instance of 'Wheel'; edit 'Wheel', and every instance follows", thrown.Message);
        Assert.Null(Tree().WithMeshFrom(Cube("Wheel_R"), "Wheel_R", out var problem));
        Assert.Equal("mesh: 'Wheel_R' is an instance of 'Wheel'; edit 'Wheel', and every instance follows", problem);
        // An empty given geometry owns it from then on, its placement kept.
        var filled = Tree().With("Axle", TestMeshes.Cube());
        Assert.NotNull(filled.Target("Axle", out _));
        Assert.Equal(new Vector3(0, 0.5f, 0), filled.FindPart("Axle")!.Translation);
    }

    [Fact]
    public void Target_refuses_an_instance_and_an_empty_with_a_pointer()
    {
        var model = Tree();
        Assert.Null(model.Target("Wheel_R", out var problem));
        Assert.Equal("mesh: 'Wheel_R' is an instance of 'Wheel'; edit 'Wheel', and every instance follows", problem);
        Assert.Null(model.Target("Axle", out problem));
        Assert.Equal("mesh: 'Axle' is an empty part with no geometry; define_mesh name=Axle gives it some", problem);
        Assert.Equal("Wheel", model.Target("Wheel", out problem)!.Name);
        Assert.Null(problem);
        Assert.Null(model.Target("Nope", out problem));
        Assert.Equal("mesh: no mesh named 'Nope'; the model's meshes are 'Body', 'Wheel'", problem);
    }

    [Fact]
    public void Create_names_each_part_problem_by_where_it_is()
    {
        NamedMesh[] meshes = [new("Body", TestMeshes.Cube()), new("Wheel", TestMeshes.Cube())];
        Part[] parts =
        [
            new("Body"), new("Axle") { Parent = "Body" }, new("Wheel") { Parent = "Axle", InstanceOf = "Body" },
            new("Wheel_R") { Parent = "Axel_F", Rotation = new(0, 0, 0, 3) }, new("Axle"), new("Spin") { Parent = "Spin" },
            new("Ghost") { InstanceOf = "Axle" }, new("Hollow") { Levels = [new PartLevel(0, TestMeshes.Cube())] },
        ];
        var (model, errors) = Model.Create(meshes, null, [], [], parts);
        Assert.Null(model);
        Assert.Equal(
        [
            "parts[2].instance_of: an instance cannot also own geometry",
            "parts[3].rotation",
            "parts[3].parent: no part named 'Axel_F'; the parts are 'Body', 'Axle', 'Wheel', 'Wheel_R', 'Spin', 'Ghost', 'Hollow'",
            "parts[4].name: a part named 'Axle' comes earlier; part names must be unique",
            "parts[5].parent: 'Spin' would be its own ancestor",
            "parts[6].instance_of: 'Axle' has no geometry",
            "parts[7].levels: 'Hollow' is an empty part with no geometry to keep a level of",
        ], errors.Select(e => e.Element == "parts[3].rotation" ? e.Element : $"{e.Element}: {e.Message}"));
        // With no part records it is today's model; a mesh with no record follows the records as a plain part.
        var (plain, none) = Model.Create(meshes, null, [], [], [new Part("Wheel") { Translation = new(1, 0, 0) }]);
        Assert.Empty(none);
        Assert.Equal(["Wheel", "Body"], plain!.Parts.Select(p => p.Name));
        Assert.Equal(["Body", "Wheel"], plain.Names);
        Assert.True(plain.FindPart("Body")!.IsPlain);
    }

    // ---- Transforms -------------------------------------------------------------------------------------------------

    [Fact]
    public void WorldTransform_composes_three_levels_with_rotation_and_non_uniform_scale()
    {
        var quarterY = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2);
        var quarterX = Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI / 2);
        var model = Model.Empty
            .WithPart(new Part("A") { Translation = new(1, 0, 0), Rotation = quarterY, Scale = new(2, 1, 1) })
            .WithPart(new Part("B") { Parent = "A", Translation = new(0, 1, 0), Scale = new(1, 3, 1) })
            .WithPart(new Part("C") { Parent = "B", Translation = new(0, 0, 1), Rotation = quarterX });
        var world = model.WorldTransform("C")!.Value;
        // Worked by hand, C's space to B's to A's to the model's, each part scale, then rotation, then translation:
        // (1,0,0) stays (1,0,0) under C's turn, +(0,0,1) is (1,0,1); B's scale keeps it, +(0,1,0) is (1,1,1);
        // A's scale makes it (2,1,1), a quarter turn about +Y (2,1,-2)... (x, z) -> (z, -x) gives (1,1,-2), +(1,0,0) is (2,1,-2).
        Near(new Vector3(2, 1, -2), Vector3.Transform(new Vector3(1, 0, 0), world));
        // (0,1,0) turns to (0,0,1) about +X, +(0,0,1) is (0,0,2); B: (0,1,2); A: (0,1,2) scaled, turned to (2,1,0), +(1,0,0) is (3,1,0).
        Near(new Vector3(3, 1, 0), Vector3.Transform(new Vector3(0, 1, 0), world));
        Near(new Vector3(1, 0, 0), Vector3.Transform(Vector3.Zero, model.WorldTransform("A")!.Value));
    }

    // ---- Levels -----------------------------------------------------------------------------------------------------

    [Fact]
    public void Levels_are_kept_replaced_and_refused()
    {
        var first = TestMeshes.Cube();
        var model = Tree().With("Wheel", first).WithLevel("Wheel", 0);
        var second = TestMeshes.Make(TestMeshes.CubeVertices.Select(v => v * 2), TestMeshes.CubeFaces);
        model = model.With("Wheel", second).WithLevel("Wheel", 2);
        Assert.Equal([0, 2], model.FindPart("Wheel")!.Levels.Select(l => l.Number));
        Assert.Same(first, model.LevelMesh("Wheel", 0));
        Assert.Same(first, model.LevelMesh("Wheel", 1));
        Assert.Same(second, model.LevelMesh("Wheel", 2));
        Assert.Same(second, model.LevelMesh("Wheel", 9));
        Assert.Null(model.LevelMesh("Wheel", -1));
        Assert.Same(second, model.LevelMesh("Wheel_R", 5)); // an instance shows its source's levels
        Assert.Null(model.LevelMesh("Axle", 0));
        Assert.Null(model.LevelMesh("Body", 0)); // Body kept none

        // An equal level replaces the newest; a lower one is refused.
        var third = TestMeshes.Make(TestMeshes.CubeVertices.Select(v => v * 3), TestMeshes.CubeFaces);
        var replaced = model.With("Wheel", third).WithLevel("Wheel", 2);
        Assert.Equal([0, 2], replaced.FindPart("Wheel")!.Levels.Select(l => l.Number));
        Assert.Same(third, replaced.LevelMesh("Wheel", 2));
        Assert.Null(model.WithLevel("Wheel", 1, out var problem));
        Assert.Equal("level: the newest kept level of 'Wheel' is 2; a new level must be 2 or more (2 replaces it)", problem);
        Assert.Throws<ArgumentException>(() => model.WithLevel("Wheel", 1));
        Assert.Null(model.WithLevel("Body", -1, out problem));
        Assert.Equal("level: -1 is negative; a level number is 0 or more", problem);
        Assert.Null(model.WithLevel("Wheel_R", 3, out problem));
        Assert.Equal("part: 'Wheel_R' is an instance of 'Wheel'; keep a level of 'Wheel', and every instance shows it", problem);
        Assert.Null(model.WithLevel("Axle", 3, out problem));
        Assert.Equal("part: 'Axle' is an empty part with no geometry to keep as a level", problem);
        Assert.Null(model.WithLevel("Nope", 3, out problem));
        Assert.Equal("part: no part named 'Nope'; the parts are 'Body', 'Axle', 'Wheel', 'Wheel_R'", problem);
        // The live mesh is not a level: editing after keeping leaves the level as it was.
        Assert.Same(first, model.With("Wheel", third).LevelMesh("Wheel", 0));
    }

    [Fact]
    public void CombinedAt_draws_each_part_s_newest_level_at_or_below_and_leaves_out_a_part_with_none()
    {
        var model = Tree().WithLevel("Wheel", 0);
        var level0 = model.CombinedAt(0)!;
        // Body kept no level, so only the wheel and its instance draw: 16 vertices, the wheel's first.
        Assert.Equal(16, level0.Vertices.Count);
        Near(new Vector3(0, 0.5f, 2), level0.Vertices[0]);
        Assert.Null(Tree().CombinedAt(0));
        var bigger = model.With("Wheel", TestMeshes.Make(TestMeshes.CubeVertices.Select(v => v * 2), TestMeshes.CubeFaces)).WithLevel("Wheel", 1).WithLevel("Body", 1);
        Near(new Vector3(1, 1.5f, 3), bigger.CombinedAt(0)!.Vertices[6]); // level 0 is still the unit cube
        Near(new Vector3(2, 2.5f, 4), bigger.CombinedAt(1)!.Vertices[8 + 6]); // Body first, then the level 1 wheel
    }

    // ---- Combined, world-placed --------------------------------------------------------------------------------------

    [Fact]
    public void Combined_places_every_occurrence_in_pre_order_and_offsets_name_each_own()
    {
        var model = Tree();
        var combined = model.Combined!;
        Assert.Equal(24, combined.Vertices.Count);
        Assert.Equal((0, 0), model.OffsetOf("Body"));
        Assert.Equal((8, 6), model.OffsetOf("Wheel"));
        Assert.Equal((16, 12), model.OffsetOf("Wheel_R"));
        Assert.Null(model.OffsetOf("Axle")); // an empty draws nothing
        Assert.Null(model.OffsetOf("Nope"));
        for (var v = 0; v < 8; v++)
        {
            var local = TestMeshes.CubeVertices[v];
            Near(local, combined.Vertices[v]);
            Near(local + new Vector3(0, 0.5f, 2), combined.Vertices[8 + v]);
            // Turned half about +Y: (x, y, z) -> (-x, y, -z), then placed at (0, 0.5, -2).
            Near(new Vector3(-local.X, local.Y + 0.5f, -local.Z - 2), combined.Vertices[16 + v]);
        }
        Assert.Equal(model.Combined!.Faces[6].Loop.Select(i => i - 8), TestMeshes.Cube().Faces[0].Loop);
        // A turned instance is still wound outward: its -z face's normal points +z in the model.
        Near(Vector3.UnitZ, combined.FaceNormal(12));
    }

    [Fact]
    public void An_edit_to_the_source_shows_in_every_instance_s_occurrence()
    {
        var model = Tree().WithPart(new Part("Wheel_X") { Translation = new(5, 0, 0), InstanceOf = "Wheel" });
        var moved = TestMeshes.Make(TestMeshes.CubeVertices.Select((v, i) => i == 6 ? v + new Vector3(0, 1, 0) : v), TestMeshes.CubeFaces)
            .WithStyle(FaceStyle.None.WithColour([3], "#ff0000"));
        var edited = model.With("Wheel", moved);
        var combined = edited.Combined!;
        Assert.Equal(32, combined.Vertices.Count);
        Near(new Vector3(1, 2.5f, 3), combined.Vertices[edited.OffsetOf("Wheel")!.Value.Vertices + 6]);
        Near(new Vector3(-1, 2.5f, -3), combined.Vertices[edited.OffsetOf("Wheel_R")!.Value.Vertices + 6]);
        Near(new Vector3(6, 2, 1), combined.Vertices[edited.OffsetOf("Wheel_X")!.Value.Vertices + 6]);
        foreach (var name in new[] { "Wheel", "Wheel_R", "Wheel_X" })
            Assert.Equal("#ff0000", combined.Style!.ColourOf(edited.OffsetOf(name)!.Value.Faces + 3));
        // The model before the edit is unchanged.
        Near(new Vector3(6, 1, 1), model.Combined!.Vertices[model.OffsetOf("Wheel_X")!.Value.Vertices + 6]);
    }

    [Fact]
    public void A_smooth_mesh_under_a_non_uniform_scale_carries_its_normals_by_the_inverse_transpose()
    {
        var smooth = TestMeshes.Cube().WithShade(MeshShade.Of(ShadingMode.Smooth, []));
        var scale = new Vector3(4, 1, 1);
        var model = Model.Of("Box", smooth).WithPart(new Part("Box") { Scale = scale, Translation = new(0, 3, 0) });
        var local = Shading.Triangles(smooth);
        var placed = Shading.Triangles(model.Combined!);
        Assert.Equal(local.Count, placed.Count);
        for (var t = 0; t < local.Count; t++)
        {
            Near(Vector3.Normalize(local[t].NormalA / scale), placed[t].NormalA);
            Near(Vector3.Normalize(local[t].NormalC / scale), placed[t].NormalC);
        }
        // Not the world normals worked again from the stretched corners, which lean otherwise.
        var reworked = Shading.Triangles(TestMeshes.Make(TestMeshes.CubeVertices.Select(v => v * scale), TestMeshes.CubeFaces).WithShade(MeshShade.Of(ShadingMode.Smooth, [])));
        Assert.Contains(Enumerable.Range(0, placed.Count), t => Vector3.Distance(reworked[t].NormalA, placed[t].NormalA) > 1e-3f);
    }

    [Fact]
    public void A_morph_target_turns_with_its_part()
    {
        var mesh = TestMeshes.Cube().WithTargets(MorphTargets.Of([MorphTarget.Of("Push", [KeyValuePair.Create(6, new Vector3(1, 0, 0))])]));
        var model = Model.Of("A", mesh).WithPart(new Part("A") { Rotation = Turned, Scale = new(2, 2, 2) });
        Near(new Vector3(-2, 0, 0), model.Combined!.Targets!.All.Single().DeltaOf(6));
    }

    [Fact]
    public void A_morphed_copy_keeps_the_part_tree()
    {
        var model = Tree().With("Wheel", TestMeshes.Cube().WithTargets(MorphTargets.Of([MorphTarget.Of("Push", [KeyValuePair.Create(6, Vector3.UnitX)])])));
        var morphed = Skinning.Morphed(model, new Dictionary<string, float> { ["Push"] = 1 });
        Assert.NotSame(model, morphed);
        Assert.Equal(model.Parts, morphed.Parts);
        Near(new Vector3(2, 1.5f, 3), morphed.Combined!.Vertices[8 + 6]);
    }

    // ---- Envelopes ---------------------------------------------------------------------------------------------------

    [Fact]
    public void Envelope_warnings_name_the_part_the_axis_the_side_and_how_far()
    {
        var unit = new PartEnvelope(Vector3.Zero, Vector3.One);
        // On the bound counts as inside.
        Assert.Empty(Cube("A").WithPart(new Part("A") { Envelope = unit }).EnvelopeWarnings());
        var tight = Cube("A").WithPart(new Part("A") { Envelope = new(new(0, 0.25f, 0), new(0.5f, 1, 1)) });
        Assert.Equal(["A x above 0.5 own", "A y below 0.25 own"], tight.EnvelopeWarnings().Select(Describe));
        Assert.Equal("'A': its geometry goes below its envelope on y by 0.25 m", tight.EnvelopeWarnings()[1].Message);

        // A child's envelope placed in its parent's space against the parent's envelope; the child's geometry is its own.
        var tree = Cube("Top").WithPart(new Part("Top") { Envelope = new(new(-1, -1, -1), new(2, 2, 2)) })
            .WithPart(new Part("Mid") { Parent = "Top", Translation = new(1.5f, 0, 0), Envelope = unit })
            .WithPart(new Part("Low") { Parent = "Mid", Translation = new(0, -0.5f, 0), Scale = new(1, 1, 3), Envelope = new(new(0, 0, 0), new(1, 1, 0.5f)) });
        Assert.Equal(["Mid x above 0.5 Top", "Low y below 0.5 Mid", "Low z above 0.5 Mid"], tree.EnvelopeWarnings().Select(Describe));
        Assert.Equal("'Mid': its envelope, placed in 'Top', goes above the envelope of 'Top' on x by 0.5 m", tree.EnvelopeWarnings()[0].Message);
        // Only where both envelopes exist: a parent with none says nothing of its child.
        Assert.DoesNotContain(tree.WithPart(tree.FindPart("Mid")! with { Envelope = null }).EnvelopeWarnings(), w => w.Part == "Low");

        // An instance's own envelope is checked against its source's geometry (T-91's recommendation, put to the person).
        var instanced = Tree().WithPart(Tree().FindPart("Wheel_R")! with { Envelope = new(Vector3.Zero, new(1, 0.5f, 1)) });
        Assert.Equal(["Wheel_R y above 0.5 own"], instanced.EnvelopeWarnings().Select(Describe));
    }

    private static string Describe(EnvelopeWarning w) =>
        FormattableString.Invariant($"{w.Part} {w.Axis} {(w.Above ? "above" : "below")} {MathF.Round(w.By, 4)} {w.Parent ?? "own"}");
}
