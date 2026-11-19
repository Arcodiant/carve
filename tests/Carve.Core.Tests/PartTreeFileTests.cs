using System.Numerics;
using System.Text;
using System.Text.Json.Nodes;
using Carve.Rover;
using Xunit;

namespace Carve.Core.Tests;

/// <summary>
/// T-92's Carve file version 7: the part tree and kept levels, written only when a model needs them and read back
/// bit-exact; every committed file still byte for byte; each version 7 refusal by where it is; and the pieces
/// <c>save mesh=</c> and <c>open mesh=</c> carry (<see cref="Model.PartPiece"/>, <see cref="Model.WithPartPieceFrom"/>).
/// </summary>
[Trait("Tier", "unit")]
public class PartTreeFileTests
{
    internal static Model Read(string text)
    {
        var read = CarveFile.Read(text);
        Assert.True(read.Succeeded, string.Join("; ", read.Errors));
        return read.Model!;
    }

    private static int Bits(float f) => BitConverter.SingleToInt32Bits(f);

    internal static string Floats(params float[] values) => string.Join(",", values.Select(v => Bits(v).ToString("x8")));

    /// <summary>A model's content as text: every part's record to the bit, then every mesh and kept level's geometry and looks to the bit.</summary>
    private static string Content(Model model)
    {
        var sb = new StringBuilder();
        void Mesh(PolygonMesh m)
        {
            for (var i = 0; i < m.Vertices.Count; i++)
                sb.Append(m.IsVertexDeleted(i) ? "x" : Floats(m.Vertices[i].X, m.Vertices[i].Y, m.Vertices[i].Z)).Append(';');
            foreach (var f in m.Faces) sb.Append(f.IsDeleted ? "x" : string.Join(" ", f.Corners)).Append(';');
            var style = m.Style ?? FaceStyle.None;
            foreach (var c in style.Colours.OrderBy(c => c.Key)) sb.Append($"c{c.Key}={c.Value};");
            foreach (var c in style.Materials.OrderBy(c => c.Key)) sb.Append($"m{c.Key}={c.Value};");
            foreach (var g in style.Groups) sb.Append($"g{g.Name}={string.Join(" ", g.Faces)};");
            sb.Append(m.ShadingMode).Append(';').Append(string.Join(" ", m.HardEdges)).Append('\n');
        }
        return PartsContent(model, sb, Mesh);
    }

    /// <summary>
    /// Every part's record to the bit, in pre-order, each followed by its live mesh and kept levels as <paramref name="mesh"/>
    /// appends them to <paramref name="sb"/>: the part of <see cref="Content"/> the version 9 tests share.
    /// </summary>
    internal static string PartsContent(Model model, StringBuilder sb, Action<PolygonMesh> mesh)
    {
        foreach (var p in model.Parts)
        {
            sb.Append($"{p.Name}|{p.Parent}|{Floats(p.Translation.X, p.Translation.Y, p.Translation.Z)}|{Floats(p.Rotation.X, p.Rotation.Y, p.Rotation.Z, p.Rotation.W)}|");
            sb.Append($"{Floats(p.Scale.X, p.Scale.Y, p.Scale.Z)}|{(p.Envelope is { } e ? Floats(e.Min.X, e.Min.Y, e.Min.Z, e.Max.X, e.Max.Y, e.Max.Z) : "-")}|{p.InstanceOf}|{p.Moves}\n");
            if (model.Find(p.Name) is { } live) mesh(live);
            foreach (var level in p.Levels)
            {
                sb.Append($"level {level.Number}\n");
                mesh(level.Mesh);
            }
        }
        return sb.ToString();
    }

    // ---- The rover and the tree round-trip ----------------------------------------------------------------------------

    [Fact]
    public void The_rover_round_trips_by_content_and_writes_the_same_text_twice()
    {
        var rover = RoverSpec.Build();
        var text = CarveFile.Write(rover);
        Assert.StartsWith("{\n  \"carve\": 7,\n", text);
        var read = CarveFile.Read(text);
        Assert.True(read.Succeeded, string.Join("; ", read.Errors));
        Assert.Equal(7, read.Version);
        Assert.Equal(Content(rover), Content(read.Model!));
        Assert.Equal(rover.Parts.Select(p => p.Name), read.Model!.Parts.Select(p => p.Name));
        Assert.Equal(text, CarveFile.Write(read.Model));
        Assert.Equal(text, CarveFile.Write(RoverSpec.Build()));
        // An empty or an instance has no mesh; every part has a record.
        Assert.Equal(new[] { "Cab", "Chassis", "Lamp", "Mast", "Wheel_FL" }, read.Model.Meshes.Select(m => m.Name).Order(StringComparer.Ordinal));
        Assert.Equal(rover.Parts.Count, read.Model.Parts.Count);
        Assert.Equal(rover.CombinedAt(0)!.Vertices, read.Model.CombinedAt(0)!.Vertices);
    }

    [Fact]
    public void The_tree_round_trips_by_content_with_every_key_in_place()
    {
        var tree = PartTreeSample.Tree();
        var text = CarveFile.Write(tree);
        var model = Read(text);
        Assert.Equal(Content(tree), Content(model));
        Assert.Equal(text, CarveFile.Write(model));
        Assert.Contains("  \"parts\": [\n"
            + "    {\"name\": \"Body\", \"parent\": null, \"translation\": [0, 0, 0], \"rotation\": [0, 0, 0, 1], \"scale\": [1, 1, 1], \"envelope\": {\"min\": [-1, -1, -1], \"max\": [3, 3, 3]}},\n"
            + "    {\"name\": \"Arm\", \"parent\": \"Body\", \"translation\": [0.5, 1.25, -0.1], \"rotation\": [0, 0.70710677, 0, 0.70710677], \"scale\": [1, 2, 0.5], \"moves\": true},\n"
            + "    {\"name\": \"Empty\", \"parent\": \"Body\", \"translation\": [0, 2, 0], \"rotation\": [0, 0, 0, 1], \"scale\": [1, 1, 1]},\n"
            + "    {\"name\": \"Copy\", \"parent\": \"Body\", \"translation\": [0, 0, 3], \"rotation\": [0, 0, 0, 1], \"scale\": [1, 1, 1], \"instance_of\": \"Arm\"}\n"
            + "  ],\n  \"skeleton\": null,\n", text);
        // levels after the mesh's last key, each a whole mesh but its name.
        Assert.Contains("      \"hard_edges\": [],\n      \"levels\": [\n        {\n          \"level\": 0,\n          \"vertices\": [\n            [0, 0, 0],\n", text);
        Assert.Contains("          \"hard_edges\": []\n        },\n        {\n          \"level\": 1,\n", text);
        Assert.Contains("          \"hard_edges\": []\n        }\n      ]\n    },\n    {\n      \"name\": \"Arm\",\n", text);
        Assert.Equal(2, model.Meshes.Count); // Empty and Copy have no mesh
    }

    // ---- The lowest version that holds the model ------------------------------------------------------------------------

    [Fact]
    public void A_model_of_untransformed_top_level_meshes_writes_version_6_or_lower()
    {
        var plain = Model.Of("a", TestMeshes.Cube()).With("b", TestMeshes.Crate());
        Assert.False(CarveFile.HasPartTree(plain));
        Assert.StartsWith("{\n  \"carve\": 2,\n", CarveFile.Write(plain));
        Assert.DoesNotContain("parts", CarveFile.Write(plain));
        // A record set to the identity is still plain.
        var same = plain.WithPart(new Part("a") { Translation = Vector3.Zero, Rotation = Quaternion.Identity, Scale = Vector3.One });
        Assert.Equal(CarveFile.Write(plain), CarveFile.Write(same));
    }

    public static TheoryData<string> Triggers => ["parent", "translation", "rotation", "scale", "envelope", "instance_of", "moves", "level", "empty", "order"];

    [Theory]
    [MemberData(nameof(Triggers))]
    public void Each_part_key_alone_moves_the_file_to_version_7(string key)
    {
        var model = Model.Of("a", TestMeshes.Cube()).With("b", TestMeshes.Cube());
        var b = model.FindPart("b")!;
        model = key switch
        {
            "parent" => model.WithPart(b with { Parent = "a" }),
            "translation" => model.WithPart(b with { Translation = new(0, 0, 1) }),
            "rotation" => model.WithPart(b with { Rotation = new(0, 1, 0, 0) }),
            "scale" => model.WithPart(b with { Scale = new(1, 1, 2) }),
            "envelope" => model.WithPart(b with { Envelope = new(Vector3.Zero, Vector3.One) }),
            "instance_of" => model.WithPart(new Part("c") { InstanceOf = "b" }),
            "moves" => model.WithPart(b with { Moves = true }),
            "level" => model.WithLevel("b", 0),
            // A bare top-level empty: plain but for having no mesh, so only "parts" holds it (RV-71).
            "empty" => model.WithPart(new Part("c")),
            // Plain parts recorded in another order than their meshes: only "parts" holds the drawing order (RV-71).
            _ => Model.Empty.WithPart(new Part("a")).WithPart(new Part("b")).With("b", TestMeshes.Cube()).With("a", TestMeshes.Cube()),
        };
        var text = CarveFile.Write(model);
        Assert.StartsWith("{\n  \"carve\": 7,\n", text);
        Assert.Equal(Content(model), Content(Read(text)));
        Assert.Equal(text, CarveFile.Write(Read(text)));
    }

    [Fact]
    public void Plain_parts_in_another_order_than_their_meshes_draw_the_same_after_save_and_open()
    {
        var model = Model.Empty.WithPart(new Part("A")).WithPart(new Part("B"))
            .With("B", TestMeshes.Cube()).With("A", TestMeshes.Crate());
        var back = Read(CarveFile.Write(model));
        Assert.Equal(["A", "B"], back.Parts.Select(p => p.Name));
        Assert.Equal(model.OffsetOf("A"), back.OffsetOf("A"));
        Assert.Equal(model.OffsetOf("B"), back.OffsetOf("B"));
        Assert.Equal(model.Combined!.Vertices, back.Combined!.Vertices);
    }

    [Fact]
    public void A_bare_empty_part_comes_back_from_save_and_open()
    {
        var back = Read(CarveFile.Write(Model.Of("A", TestMeshes.Cube()).WithPart(new Part("E"))));
        Assert.NotNull(back.FindPart("E"));
        Assert.Equal(["A", "E"], back.Parts.Select(p => p.Name));
    }

    // ---- Every version 7 refusal, by where it is --------------------------------------------------------------------

    public static TheoryData<string, string, string> Refusals => new()
    {
        { "parts missing", "parts", "missing" },
        { "parts not a list", "parts", "not an array" },
        { "part unknown key", "parts[1].colour", "unknown key" },
        { "part not an object", "parts[2]", "not a part" },
        { "part name missing", "parts[0].name", "missing" },
        { "part name bad", "parts[2].name", "':'" },
        { "parent not a string", "parts[1].parent", "not a part name or null" },
        { "parent unknown", "parts[1].parent", "no part named 'Nobody'" },
        { "parent cycle", "parts[0].parent", "its own ancestor" },
        { "translation short", "parts[1].translation", "has 2 numbers" },
        { "translation not finite", "parts[1].translation[0]", "not finite" },
        { "rotation not unit", "parts[1].rotation", "not a unit quaternion" },
        { "rotation missing", "parts[1].rotation", "missing" },
        { "scale mirrored", "parts[1].scale", "mirroring scale is refused" },
        { "envelope inverted", "parts[0].envelope", "min is above max on x" },
        { "envelope unknown key", "parts[0].envelope.mid", "unknown key" },
        { "envelope max missing", "parts[0].envelope.max", "missing" },
        { "instance_of unknown", "parts[3].instance_of", "no part named 'Nobody'" },
        { "instance_of an owner", "parts[1].instance_of", "cannot also own geometry" },
        { "instance_of an empty", "parts[3].instance_of", "'Empty' has no geometry" },
        { "instance with a mesh", "parts[3].instance_of", "cannot also own geometry" },
        { "moves not a boolean", "parts[1].moves", "not true or false" },
        { "part twice", "parts[2].name", "comes earlier" },
        { "levels not a list", "meshes[0].levels", "not an array" },
        { "level number missing", "meshes[0].levels[1].level", "missing" },
        { "level number not an integer", "meshes[0].levels[1].level", "not an integer" },
        { "level number negative", "meshes[0].levels[0].level", "negative" },
        { "levels out of order", "meshes[0].levels[1].level", "comes after level 0" },
        { "level face bad", "meshes[0].levels[1].faces[3]", "" },
        { "level unknown key", "meshes[0].levels[0].name", "unknown key" },
        { "level material undefined", "meshes[0].levels[0].materials", "" },
        { "parts in version 6", "parts", "unknown key" },
        { "levels in version 8", "meshes[0].levels", "unknown key" },
    };

    internal static void Mutate(string which, JsonObject file)
    {
        var parts = file["parts"]!.AsArray();
        JsonObject Part(int i) => parts[i]!.AsObject();
        var levels = file["meshes"]![0]!["levels"]!.AsArray();
        JsonObject Level(int i) => levels[i]!.AsObject();
        switch (which)
        {
            case "parts missing": file.Remove("parts"); break;
            case "parts not a list": file["parts"] = 3; break;
            case "part unknown key": Part(1)["colour"] = "#FF0000"; break;
            case "part not an object": parts[2] = "Empty"; break;
            case "part name missing": Part(0).Remove("name"); break;
            case "part name bad": Part(2)["name"] = "Em:pty"; break;
            case "parent not a string": Part(1)["parent"] = 5; break;
            case "parent unknown": Part(1)["parent"] = "Nobody"; break;
            case "parent cycle": Part(0)["parent"] = "Arm"; break;
            case "translation short": Part(1)["translation"] = new JsonArray(1, 2); break;
            case "translation not finite": Part(1)["translation"] = JsonNode.Parse("[1e40, 0, 0]"); break;
            case "rotation not unit": Part(1)["rotation"] = new JsonArray(0, 0, 0, 2); break;
            case "rotation missing": Part(1).Remove("rotation"); break;
            case "scale mirrored": Part(1)["scale"] = new JsonArray(1, -1, 1); break;
            case "envelope inverted": Part(0)["envelope"]!["min"] = new JsonArray(4, -1, -1); break;
            case "envelope unknown key": Part(0)["envelope"]!["mid"] = new JsonArray(0, 0, 0); break;
            case "envelope max missing": Part(0)["envelope"]!.AsObject().Remove("max"); break;
            case "instance_of unknown": Part(3)["instance_of"] = "Nobody"; break;
            case "instance_of an owner": Part(1)["instance_of"] = "Body"; break;
            case "instance_of an empty": Part(3)["instance_of"] = "Empty"; break;
            case "instance with a mesh":
                var copy = file["meshes"]![1]!.DeepClone().AsObject();
                copy["name"] = "Copy";
                file["meshes"]!.AsArray().Add(copy);
                break;
            case "moves not a boolean": Part(1)["moves"] = "yes"; break;
            case "part twice": Part(2)["name"] = "Arm"; break;
            case "levels not a list": file["meshes"]![0]!["levels"] = new JsonObject(); break;
            case "level number missing": Level(1).Remove("level"); break;
            case "level number not an integer": Level(1)["level"] = 1.5; break;
            case "level number negative": Level(0)["level"] = -1; break;
            case "levels out of order": Level(1)["level"] = 0; break;
            case "level face bad": Level(1)["faces"]![3] = new JsonArray(0, 1, 99); break;
            case "level unknown key": Level(0)["name"] = "Body"; break;
            case "level material undefined":
                Level(0)["materials"] = JsonNode.Parse("""[{"material": "steel", "faces": [0]}]""");
                break;
            case "parts in version 6": file["carve"] = 6; Level(0).Parent!.Parent!.AsObject().Remove("levels"); break;
            case "levels in version 8": file["carve"] = 8; file.Remove("parts"); break;
            default: throw new ArgumentException(which);
        }
    }

    [Theory]
    [MemberData(nameof(Refusals))]
    public void Every_version_7_problem_is_refused_where_it_is(string which, string element, string message)
    {
        var file = JsonNode.Parse(CarveFile.Write(PartTreeSample.Tree()))!.AsObject();
        Mutate(which, file);
        var read = CarveFile.Read(file.ToJsonString());
        Assert.False(read.Succeeded);
        Assert.Null(read.Model);
        var found = read.Errors.FirstOrDefault(e => e.Element.StartsWith(element, StringComparison.Ordinal) && e.Message.Contains(message, StringComparison.Ordinal));
        Assert.True(found is not null, $"no {element} ({message}) among: {string.Join("; ", read.Errors)}");
    }

    [Fact]
    public void The_unmutated_tree_reads_so_each_refusal_is_its_own()
    {
        var file = JsonNode.Parse(CarveFile.Write(PartTreeSample.Tree()))!.AsObject();
        Assert.True(CarveFile.Read(file.ToJsonString()).Succeeded);
    }

    // ---- Pieces --------------------------------------------------------------------------------------------------------

    [Fact]
    public void A_piece_is_the_parts_own_geometry_and_levels_at_the_origin()
    {
        var tree = PartTreeSample.Tree();
        var body = tree.PartPiece("Body", out var problem)!;
        Assert.Null(problem);
        var part = Assert.Single(body.Parts);
        Assert.Equal("Body", part.Name);
        Assert.Null(part.Envelope);
        Assert.Null(part.Parent);
        Assert.False(part.HasTransform);
        Assert.Equal([0, 1], part.Levels.Select(l => l.Number));
        Assert.Same(tree.FindPart("Body")!.Levels[0].Mesh, part.Levels[0].Mesh);
        var text = CarveFile.Write(body);
        Assert.StartsWith("{\n  \"carve\": 7,\n", text); // the levels need version 7
        Assert.Contains("  \"parts\": [\n    {\"name\": \"Body\", \"parent\": null, \"translation\": [0, 0, 0], \"rotation\": [0, 0, 0, 1], \"scale\": [1, 1, 1]}\n  ],\n", text);

        // A placed, moving part with no levels: its geometry alone, written as before (version 2).
        var arm = tree.PartPiece("Arm", out _)!;
        Assert.True(Assert.Single(arm.Parts).IsPlain);
        Assert.Equal(CarveFile.Write(Model.Of("Arm", TestMeshes.Cube())), CarveFile.Write(arm));
    }

    [Theory]
    [InlineData("Copy", "mesh: the model's part 'Copy' is an instance of 'Arm', with no geometry of its own; save 'Arm', and every instance shows it")]
    [InlineData("Empty", "mesh: the model's part 'Empty' is an empty part with no geometry; a piece is one part's own geometry, so name a part that has some")]
    [InlineData("Nobody", "mesh: no mesh named 'Nobody'; the model's meshes are 'Body', 'Arm'")]
    public void A_piece_of_an_instance_or_an_empty_is_refused(string name, string expected)
    {
        Assert.Null(PartTreeSample.Tree().PartPiece(name, out var problem));
        Assert.Equal(expected, problem);
    }

    [Fact]
    public void Opening_a_piece_into_a_placed_part_replaces_its_geometry_and_levels_and_keeps_its_place()
    {
        var tree = PartTreeSample.Tree();
        // The piece: a big cube as Arm, kept at level 2.
        var file = Read(CarveFile.Write(Model.Of("Arm", PartTreeSample.Big()).WithLevel("Arm", 2)));
        var opened = tree.WithPartPieceFrom(file, "Arm", out var problem)!;
        Assert.Null(problem);
        var before = tree.FindPart("Arm")!;
        var after = opened.FindPart("Arm")!;
        Assert.Equal(before with { Levels = after.Levels }, after); // parent, transform, envelope, moves kept
        Assert.Equal([2], after.Levels.Select(l => l.Number));
        Assert.Equal(PartTreeSample.Big().Vertices, opened.Find("Arm")!.Vertices);
        Assert.Equal(tree.Parts.Select(p => p.Name), opened.Parts.Select(p => p.Name));
        Assert.Equal(Content(Read(CarveFile.Write(opened))), Content(opened));

        // Body's levels are replaced by the piece's (none), and its envelope and children stay.
        var plain = tree.WithPartPieceFrom(Read(CarveFile.Write(Model.Of("Body", TestMeshes.Cube()))), "Body", out _)!;
        Assert.Empty(plain.FindPart("Body")!.Levels);
        Assert.Equal(tree.FindPart("Body")!.Envelope, plain.FindPart("Body")!.Envelope);
        Assert.Equal("Body", plain.FindPart("Copy")!.Parent);

        // A new name is a new top-level part at the origin, with its levels.
        var fresh = tree.WithPartPieceFrom(file, "Arm", out _)!.WithPartPieceFrom(Read(CarveFile.Write(Model.Of("Lid", PartTreeSample.Big()).WithLevel("Lid", 0))), "Lid", out _)!;
        Assert.Equal(new Part("Lid") { Levels = fresh.FindPart("Lid")!.Levels }, fresh.FindPart("Lid"));
        Assert.Equal("Lid", fresh.Parts[^1].Name);
    }

    [Fact]
    public void Opening_one_part_of_a_whole_version_7_file_takes_its_geometry_and_levels_not_its_place()
    {
        var text = CarveFile.Write(PartTreeSample.Tree());
        var read = CarveFile.Read(text, null, "Body");
        Assert.True(read.Succeeded, string.Join("; ", read.Errors));
        var part = Assert.Single(read.Model!.Parts);
        Assert.Equal(new Part("Body") { Levels = part.Levels }, part);
        Assert.Equal([0, 1], part.Levels.Select(l => l.Number));
        var into = Model.Of("Body", TestMeshes.Crate()).WithPartPieceFrom(read.Model, "Body", out _)!;
        Assert.Equal(PartTreeSample.Big().Vertices, into.Find("Body")!.Vertices);
        Assert.Equal(2, into.FindPart("Body")!.Levels.Count);
    }

    [Theory]
    [InlineData("Copy", "mesh: the model's part 'Copy' is an instance of 'Arm', with no geometry of its own; open into 'Arm', and every instance follows")]
    [InlineData("Empty", "mesh: the model's part 'Empty' is an empty part with no geometry; a piece is one part's own geometry, so name a part that has some")]
    public void Opening_a_piece_into_an_instance_or_an_empty_is_refused(string name, string expected)
    {
        var file = Read(CarveFile.Write(Model.Of(name, TestMeshes.Cube())));
        Assert.Null(PartTreeSample.Tree().WithPartPieceFrom(file, name, out var problem));
        Assert.Equal(expected, problem);
    }

    [Theory]
    [InlineData("Copy", "mesh: the file's part 'Copy' is an instance of 'Arm', with no geometry of its own; open 'Arm', and every instance follows")]
    [InlineData("Empty", "mesh: the file's part 'Empty' is an empty part with no geometry; a piece is one part's own geometry, so name a part that has some")]
    public void Opening_an_instance_or_an_empty_of_a_file_is_refused(string name, string expected)
    {
        var read = CarveFile.Read(CarveFile.Write(PartTreeSample.Tree()), null, name);
        Assert.True(read.Succeeded);
        Assert.Null(Model.Of("Other", TestMeshes.Cube()).WithPartPieceFrom(read.Model!, name, out var problem));
        Assert.Equal(expected, problem);
    }
}
