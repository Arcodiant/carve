using System.Numerics;
using System.Text;
using System.Text.Json.Nodes;
using Xunit;

namespace Carve.Core.Tests;

/// <summary>
/// T-132's Carve file version 9: a part tree and kept levels beside morph targets, version 8 with version 7's <c>parts</c> and
/// <c>levels</c>, written only when a model needs both and read back bit-exact; a kept level keeps its own targets (the
/// person's ruling, 2026-11-18); each version 9 refusal by where it is; and pieces of a part with targets.
/// The sample is <see cref="PartTreeSample.TargetedTree"/>.
/// </summary>
[Trait("Tier", "unit")]
public class PartsBesideTargetsFileTests
{
    private static Model Read(string text) => PartTreeFileTests.Read(text);

    private static string Floats(params float[] values) => PartTreeFileTests.Floats(values);

    /// <summary>Every part's record, and each mesh and kept level's vertices, faces and targets, to the bit.</summary>
    private static string Content(Model model)
    {
        var sb = new StringBuilder();
        void Mesh(PolygonMesh m)
        {
            for (var i = 0; i < m.Vertices.Count; i++) sb.Append(Floats(m.Vertices[i].X, m.Vertices[i].Y, m.Vertices[i].Z)).Append(';');
            foreach (var f in m.Faces) sb.Append(string.Join(" ", f.Corners)).Append(';');
            foreach (var t in m.Targets?.All ?? [])
            {
                sb.Append($"\n  target {t.Name}:");
                foreach (var (id, d) in t.Deltas.OrderBy(x => x.Key)) sb.Append($" {id}={Floats(d.X, d.Y, d.Z)}");
            }
            sb.Append('\n');
        }
        return PartTreeFileTests.PartsContent(model, sb, Mesh);
    }

    private static string VersionLine(int version) => $"{{\n  \"carve\": {version},\n";

    // ---- The round trip ----------------------------------------------------------------------------------------------

    [Fact]
    public void A_part_tree_with_targets_round_trips_by_content_and_writes_the_same_text_twice()
    {
        var model = PartTreeSample.TargetedTree();
        var text = CarveFile.Write(model);
        var back = Read(text);
        Assert.Equal(Content(model), Content(back));
        Assert.Equal(text, CarveFile.Write(back));
        Assert.Equal(["Bulge"], back.Find("Body")!.Targets!.Names);
        Assert.Equal(["Bulge"], back.FindPart("Body")!.Levels.Single(l => l.Number == 1).Mesh.Targets!.Names);
        Assert.Null(back.FindPart("Body")!.Levels.Single(l => l.Number == 0).Mesh.Targets);
        Assert.Equal(["Wave"], back.Find("Arm")!.Targets!.Names);
        Assert.Contains("Bulge", Content(back));
    }

    [Fact]
    public void It_writes_version_9_with_targets_before_levels_and_a_levels_targets_last()
    {
        var text = CarveFile.Write(PartTreeSample.TargetedTree());
        Assert.StartsWith(VersionLine(9), text);
        Assert.Equal(9, CarveFile.Read(text).Version);
        var body = text[text.IndexOf("\"name\": \"Body\"", StringComparison.Ordinal)..text.IndexOf("\"name\": \"Arm\"", StringComparison.Ordinal)];
        // The live mesh's targets, then its levels; in level 1, its own targets as the last key.
        var targets = body.IndexOf("\n      \"targets\": [\n        {\"name\": \"Bulge\", \"deltas\": {\"0\": [0, 0.25, 0], \"6\": [0.1, 0, -0.3]}}\n      ],\n      \"levels\": [\n", StringComparison.Ordinal);
        Assert.True(targets > 0, body);
        Assert.Contains("          \"targets\": [\n            {\"name\": \"Bulge\", \"deltas\": {\"0\": [0, 0.25, 0], \"6\": [0.1, 0, -0.3]}}\n          ]\n        }\n      ]\n", body);
        // The parts after the meshes, as version 7 writes them.
        Assert.Contains("\n  ],\n  \"parts\": [\n    {\"name\": \"Body\", \"parent\": null,", text);
    }

    [Fact]
    public void Only_a_kept_level_with_targets_is_enough_for_version_9()
    {
        var model = PartTreeSample.Tree();
        model = model.With("Body", model.Find("Body")!.WithTargets(PartTreeSample.Bulge())).WithLevel("Body", 1).With("Body", PartTreeSample.Big());
        Assert.Null(model.Find("Body")!.Targets);
        var text = CarveFile.Write(model);
        Assert.StartsWith(VersionLine(9), text);
        Assert.Equal(Content(model), Content(Read(text)));
    }

    // ---- The lowest version that holds the model ---------------------------------------------------------------------

    [Fact]
    public void Removing_the_targets_drops_it_to_version_7()
    {
        var model = PartTreeSample.TargetedTree();
        model = model.With("Body", model.Find("Body")!.WithTargets(null)).WithLevel("Body", 1).With("Arm", model.Find("Arm")!.WithTargets(null));
        var text = CarveFile.Write(model);
        Assert.StartsWith(VersionLine(7), text);
        Assert.Equal(CarveFile.Write(PartTreeSample.Tree()), text);
    }

    [Fact]
    public void Removing_the_part_tree_drops_it_to_version_8()
    {
        var targeted = Model.Of("a", TestMeshes.Cube().WithTargets(PartTreeSample.Bulge()));
        var plain = CarveFile.Write(targeted);
        Assert.StartsWith(VersionLine(8), plain);
        var placed = targeted.WithPart(targeted.FindPart("a")! with { Translation = new(1, 0, 0) });
        Assert.StartsWith(VersionLine(9), CarveFile.Write(placed));
        // Set back to the identity: plain again, and the very bytes of version 8.
        Assert.Equal(plain, CarveFile.Write(placed.WithPart(placed.FindPart("a")! with { Translation = Vector3.Zero })));
    }

    // ---- Refusals ----------------------------------------------------------------------------------------------------

    /// <summary>The version 9 refusals: (mutation, the element named, a piece of its message). Meshes: [0] Body, [1] Arm; Body's levels[1] has a Bulge.</summary>
    public static TheoryData<string, string, string> Refusals => new()
    {
        { "parts missing", "parts", "a version 9 Carve file" },
        { "mesh target vertex unknown", "meshes[1].targets[0].deltas", "" },
        { "level target vertex unknown", "meshes[0].levels[1].targets[0].deltas", "" },
        { "level target unknown key", "meshes[0].levels[1].targets[0].weight", "unknown key" },
        { "level target name twice", "meshes[0].levels[1].targets[1].name", "comes earlier" },
        { "level target zero delta", "meshes[0].levels[1].targets[0].deltas", "zero" },
        { "level targets not a list", "meshes[0].levels[1].targets", "not an array" },
        { "level targets in version 7", "meshes[0].levels[1].targets", "unknown key" },
        { "levels in version 8", "meshes[0].levels", "unknown key" },
        { "parts in version 8", "parts", "unknown key" },
    };

    private static void Mutate(string which, JsonObject file)
    {
        var body = file["meshes"]![0]!.AsObject();
        var level = body["levels"]![1]!.AsObject();
        JsonObject LevelTarget() => level["targets"]![0]!.AsObject();
        switch (which)
        {
            case "parts missing": file.Remove("parts"); break;
            case "mesh target vertex unknown": file["meshes"]![1]!["targets"]![0]!["deltas"]!["99"] = new JsonArray(1, 0, 0); break;
            case "level target vertex unknown": LevelTarget()["deltas"]!["99"] = new JsonArray(1, 0, 0); break;
            case "level target unknown key": LevelTarget()["weight"] = 1; break;
            case "level target name twice": level["targets"]!.AsArray().Add(LevelTarget().DeepClone()); break;
            case "level target zero delta": LevelTarget()["deltas"]!["0"] = new JsonArray(0, 0, 0); break;
            case "level targets not a list": level["targets"] = "Bulge"; break;
            case "level targets in version 7":
                // Only the level's targets are left, so they alone are the unknown key.
                file["carve"] = 7;
                body.Remove("targets");
                file["meshes"]![1]!.AsObject().Remove("targets");
                break;
            case "levels in version 8": file["carve"] = 8; file.Remove("parts"); break;
            case "parts in version 8": file["carve"] = 8; body.Remove("levels"); break;
            default: throw new ArgumentException(which);
        }
    }

    private static void AssertRefused(JsonObject file, string element, string message)
    {
        var read = CarveFile.Read(file.ToJsonString());
        Assert.False(read.Succeeded);
        Assert.Null(read.Model);
        var found = read.Errors.FirstOrDefault(e => e.Element.StartsWith(element, StringComparison.Ordinal) && e.Message.Contains(message, StringComparison.Ordinal));
        Assert.True(found is not null, $"no {element} ({message}) among: {string.Join("; ", read.Errors)}");
    }

    [Theory]
    [MemberData(nameof(Refusals))]
    public void Every_version_9_problem_is_refused_where_it_is(string which, string element, string message)
    {
        var file = JsonNode.Parse(CarveFile.Write(PartTreeSample.TargetedTree()))!.AsObject();
        Mutate(which, file);
        AssertRefused(file, element, message);
    }

    /// <summary>Every version 7 refusal (T-92's, the parts' own and <c>Model.Create</c>'s) holds in version 9 as well.</summary>
    [Theory]
    [MemberData(nameof(PartTreeFileTests.Refusals), MemberType = typeof(PartTreeFileTests))]
    public void Every_version_7_problem_is_refused_in_version_9_too(string which, string element, string message)
    {
        var file = JsonNode.Parse(CarveFile.Write(PartTreeSample.TargetedTree()))!.AsObject();
        PartTreeFileTests.Mutate(which, file);
        AssertRefused(file, element, message);
    }

    [Fact]
    public void The_unmutated_sample_reads_so_each_refusal_is_its_own()
        => Assert.True(CarveFile.Read(CarveFile.Write(PartTreeSample.TargetedTree())).Succeeded);

    // ---- Pieces ------------------------------------------------------------------------------------------------------

    [Fact]
    public void A_piece_of_a_part_with_targets_carries_its_targets_and_levels()
    {
        var model = PartTreeSample.TargetedTree();
        var piece = model.PartPiece("Body", out var problem)!;
        Assert.Null(problem);
        var text = CarveFile.Write(piece);
        Assert.StartsWith(VersionLine(9), text);
        var back = Read(text);
        var part = Assert.Single(back.Parts);
        Assert.Equal(new Part("Body") { Levels = part.Levels }, part);
        Assert.Equal(Content(piece), Content(back));
        Assert.Equal(["Bulge"], back.Find("Body")!.Targets!.Names);
        Assert.Equal(["Bulge"], part.Levels[1].Mesh.Targets!.Names);

        // A part with targets and no levels is written as a version 8 mesh is.
        var arm = model.PartPiece("Arm", out _)!;
        Assert.Equal(CarveFile.Write(Model.Of("Arm", model.Find("Arm")!)), CarveFile.Write(arm));
        Assert.StartsWith(VersionLine(8), CarveFile.Write(arm));
    }

    [Fact]
    public void Opening_one_part_of_a_version_9_file_takes_its_targets_and_levels_not_its_place()
    {
        var model = PartTreeSample.TargetedTree();
        var read = CarveFile.Read(CarveFile.Write(model), null, "Body");
        Assert.True(read.Succeeded, string.Join("; ", read.Errors));
        Assert.Equal(Content(model.PartPiece("Body", out _)!), Content(read.Model!));

        // Into a model whose Body has no targets: its geometry, targets and levels are replaced, its place kept.
        var into = PartTreeSample.Tree();
        var opened = into.WithPartPieceFrom(read.Model!, "Body", out var problem)!;
        Assert.Null(problem);
        Assert.Equal(into.FindPart("Body")!.Envelope, opened.FindPart("Body")!.Envelope);
        Assert.Equal(["Bulge"], opened.Find("Body")!.Targets!.Names);
        Assert.Equal(["Bulge"], opened.FindPart("Body")!.Levels[1].Mesh.Targets!.Names);
        Assert.StartsWith(VersionLine(9), CarveFile.Write(opened));
    }
}
