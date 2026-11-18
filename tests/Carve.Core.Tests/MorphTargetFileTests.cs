using System.Numerics;
using System.Text;
using Xunit;

namespace Carve.Core.Tests;

/// <summary>
/// Carve file version 8 (T-101): each mesh's morph targets as sparse deltas by vertex id, read back bit-exact, every fault
/// named by element; and every model without a target written as before, byte for byte.
/// </summary>
[Trait("Tier", "unit")]
public sealed class MorphTargetFileTests
{
    /// <summary>Floats whose text is awkward: a tenth, a subnormal, the largest float, a negative zero beside a non-zero.</summary>
    private static readonly Vector3[] Awkward =
    [
        new(0.1f, -0.2f, 0.3f),
        new(float.Epsilon, 1e-7f, -3.4028235e38f),
        new(-0f, 1f / 3f, 0f),
    ];

    /// <summary>A cube far from the origin, so a delta through a double sum of base and delta is not the delta.</summary>
    private static PolygonMesh FarCube()
    {
        var far = new Vector3(1000.1f, -2000.3f, 4096.7f);
        return TestMeshes.Make(TestMeshes.CubeVertices.Select(v => v + far), TestMeshes.CubeFaces);
    }

    private static Model WithTargets()
    {
        var mesh = FarCube();
        var bulk = MorphTarget.FromDeltas(mesh, "Bulk", [KeyValuePair.Create(0, new Vector3(0.3f, 0.7f, -0.9f)),
            KeyValuePair.Create(3, Awkward[0]), KeyValuePair.Create(5, Awkward[1]), KeyValuePair.Create(7, Awkward[2])]).Target!;
        var lean = MorphTarget.FromPositions(mesh, "Lean", mesh.Vertices.Select(v => v * 0.8f).ToArray()).Target!;
        var empty = MorphTarget.Of("Rest", []);
        return Model.Of("box", mesh.WithTargets(MorphTargets.Of([bulk, lean, empty])))
            .With("plain", TestMeshes.Cube());
    }

    private static string Text() => CarveFile.Write(WithTargets());

    [Fact]
    public void Targets_round_trip_bit_exact_at_version_8()
    {
        var model = WithTargets();
        var text = CarveFile.Write(model);
        var read = CarveFile.Read(text);
        Assert.True(read.Succeeded, string.Join("; ", read.Errors));
        Assert.Equal(8, read.Version);
        Assert.Equal(text, CarveFile.Write(read.Model!));

        var before = model.Meshes[0].Mesh.Targets!;
        var after = read.Model!.Meshes[0].Mesh.Targets!;
        Assert.Equal(before.Names, after.Names);
        for (var t = 0; t < before.Count; t++)
        {
            Assert.Equal(before.All[t].Deltas.Keys, after.All[t].Deltas.Keys);
            foreach (var (v, d) in before.All[t].Deltas)
            {
                var e = after.All[t].Deltas[v];
                Assert.Equal(Bits(d), Bits(e));
            }
        }
        Assert.Null(read.Model.Meshes[1].Mesh.Targets);
    }

    private static (int, int, int) Bits(Vector3 v) =>
        (BitConverter.SingleToInt32Bits(v.X), BitConverter.SingleToInt32Bits(v.Y), BitConverter.SingleToInt32Bits(v.Z));

    [Fact]
    public void Targets_are_written_after_the_last_key_one_per_line_ids_ascending()
    {
        var text = Text();
        Assert.Contains("\"carve\": 8,", text);
        Assert.Contains(
            "      \"hard_edges\": [],\n" +
            "      \"targets\": [\n" +
            "        {\"name\": \"Bulk\", \"deltas\": {\"0\": [0.3, 0.7, -0.9], \"3\": [0.1, -0.2, 0.3], \"5\": [1E-45, 1E-07, -3.4028235E+38], \"7\": [-0, 0.33333334, 0]}},\n",
            text);
        Assert.Contains("        {\"name\": \"Rest\", \"deltas\": {}}\n      ]\n    },\n", text);
        // The mesh without targets has no targets key, even in a version 8 file.
        Assert.Single(text.Split('\n'), l => l.Contains("\"targets\""));
    }

    [Fact]
    public void Targets_after_uvs_and_seams_and_with_materials()
    {
        var unwrapped = Unwrap.Run(TestMeshes.SeamedBarrel()).Item1!.Mesh;
        var target = MorphTarget.FromDeltas(unwrapped, "Bulge", [KeyValuePair.Create(1, new Vector3(0, 0.5f, 0))]).Target!;
        var model = Model.Empty.WithMaterial(new Material("wood"))
            .With("barrel", unwrapped.WithStyle(FaceStyle.None.WithMaterial([0], "wood")).WithTargets(MorphTargets.Of([target])));
        var text = CarveFile.Write(model);
        Assert.Contains("\n      ],\n      \"targets\": [\n        {\"name\": \"Bulge\", \"deltas\": {\"1\": [0, 0.5, 0]}}\n      ]\n    }\n", text);
        var read = CarveFile.Read(text);
        Assert.True(read.Succeeded, string.Join("; ", read.Errors));
        Assert.Equal(text, CarveFile.Write(read.Model!));
    }

    [Fact]
    public void Save_of_one_mesh_carries_its_targets()
    {
        var piece = WithTargets().Piece("box")!;
        var read = CarveFile.Read(CarveFile.Write(piece));
        Assert.Equal(["Bulk", "Lean", "Rest"], read.Model!.Meshes.Single().Mesh.Targets!.Names);
        Assert.Equal(2, CarveFile.Read(CarveFile.Write(WithTargets().Piece("plain")!)).Version);
    }

    [Theory]
    [InlineData("\"name\": \"Lean\"", "\"name\": \"Bulk\"", "meshes[0].targets[1].name", "a target named 'Bulk' comes earlier")]
    [InlineData("\"name\": \"Lean\"", "\"name\": \"\"", "meshes[0].targets[1].name", "must not be empty")]
    [InlineData("\"name\": \"Lean\"", "\"name\": \"a:b\"", "meshes[0].targets[1].name", "must not contain ':'")]
    [InlineData("\"name\": \"Lean\"", "\"name\": 3", "meshes[0].targets[1].name", "not a string")]
    [InlineData("\"3\": [0.1", "\"99\": [0.1", "meshes[0].targets[0].deltas.99", "there is no vertex 99")]
    [InlineData("\"3\": [0.1", "\"-1\": [0.1", "meshes[0].targets[0].deltas.-1", "is not a vertex id")]
    [InlineData("\"3\": [0.1", "\"03\": [0.1", "meshes[0].targets[0].deltas.03", "is not a vertex id")]
    [InlineData("\"3\": [0.1", "\"x\": [0.1", "meshes[0].targets[0].deltas.x", "is not a vertex id")]
    [InlineData("\"3\": [0.1", "\"0\": [0.1", "meshes[0].targets[0].deltas.0", "given a delta earlier")]
    [InlineData("[0.1, -0.2, 0.3]", "[1e39, -0.2, 0.3]", "meshes[0].targets[0].deltas.3[0]", "not finite")]
    [InlineData("[0.1, -0.2, 0.3]", "[0, 0, 0]", "meshes[0].targets[0].deltas.3", "zero delta is never written")]
    [InlineData("[0.1, -0.2, 0.3]", "[-0, 0, -0]", "meshes[0].targets[0].deltas.3", "zero delta is never written")]
    [InlineData("[0.1, -0.2, 0.3]", "[0.1, -0.2]", "meshes[0].targets[0].deltas.3", "three numbers")]
    [InlineData("[0.1, -0.2, 0.3]", "\"up\"", "meshes[0].targets[0].deltas.3", "three numbers")]
    [InlineData("{\"name\": \"Rest\", \"deltas\": {}}", "{\"name\": \"Rest\", \"deltas\": []}", "meshes[0].targets[2].deltas", "an object of vertex id")]
    [InlineData("{\"name\": \"Rest\", \"deltas\": {}}", "{\"name\": \"Rest\"}", "meshes[0].targets[2].deltas", "missing")]
    [InlineData("{\"name\": \"Rest\", \"deltas\": {}}", "{\"deltas\": {}}", "meshes[0].targets[2].name", "missing")]
    [InlineData("{\"name\": \"Rest\", \"deltas\": {}}", "{\"name\": \"Rest\", \"deltas\": {}, \"weight\": 1}", "meshes[0].targets[2].weight", "unknown key")]
    [InlineData("{\"name\": \"Rest\", \"deltas\": {}}", "7", "meshes[0].targets[2]", "not a target")]
    public void A_bad_target_is_refused_by_element(string from, string to, string element, string message)
    {
        var text = Text();
        var at = text.IndexOf(from, StringComparison.Ordinal);
        Assert.True(at >= 0, from);
        var read = CarveFile.Read(text[..at] + to + text[(at + from.Length)..]);
        Assert.False(read.Succeeded);
        var error = Assert.Single(read.Errors);
        Assert.Equal(element, error.Element);
        Assert.Contains(message, error.Message);
    }

    [Fact]
    public void A_deleted_vertex_holds_no_delta()
    {
        var cube = TestMeshes.Cube();
        var holed = PolygonMesh.Create([.. cube.Vertices, new Vector3(5, 5, 5)], cube.Faces, [8]).Mesh!;
        var text = CarveFile.Write(Model.Of("box", holed.WithTargets(MorphTargets.Of([MorphTarget.FromDeltas(holed, "T", [KeyValuePair.Create(2, Vector3.UnitX)]).Target!]))));
        var read = CarveFile.Read(text.Replace("{\"2\": [1, 0, 0]}", "{\"8\": [1, 0, 0]}"));
        var error = Assert.Single(read.Errors);
        Assert.Equal("meshes[0].targets[0].deltas.8", error.Element);
        Assert.Contains("deleted", error.Message);
    }

    [Theory]
    [InlineData(6)]
    [InlineData(5)]
    [InlineData(3)]
    public void Targets_in_an_older_version_are_an_unknown_key(int version)
    {
        var text = CarveFile.Write(Model.Of("box", TestMeshes.Cube().WithShade(MeshShade.Of(ShadingMode.Smooth, []))))
            .Replace("\"hard_edges\": []", "\"hard_edges\": [],\n      \"targets\": [{\"name\": \"T\", \"deltas\": {\"0\": [1, 0, 0]}}]")
            .Replace("\"carve\": 3", $"\"carve\": {version}");
        var error = Assert.Single(CarveFile.Read(text).Errors);
        Assert.Equal("meshes[0].targets", error.Element);
        Assert.Contains("unknown key", error.Message);
    }

    [Fact]
    public void Version_7_holds_no_targets()
    {
        // Version 7 is the part tree (T-92), which has no targets key: which version holds both is RV-64's open question.
        var read = CarveFile.Read(Text().Replace("\"carve\": 8", "\"carve\": 7"));
        Assert.Contains(read.Errors, e => e.Element == "meshes[0].targets" && e.Message.Contains("unknown key"));
        Assert.Contains(7, CarveFile.ReadableVersions);
        Assert.Contains(8, CarveFile.ReadableVersions);
    }

    /// <summary>
    /// The byte-identical guard over every committed Carve file, whatever is added later: each reads, writes back to its exact
    /// bytes, and is version 8 only when some mesh has a target.
    /// </summary>
    [Fact]
    public void Every_committed_file_rereads_and_rewrites_equal()
    {
        var root = RepoRoot();
        var files = Directory.GetFiles(Path.Combine(root, "samples"), "*.carve")
            .Concat(Directory.GetFiles(Path.Combine(root, "tests", "Carve.Core.Tests", "Fixtures"), "*.carve")).ToArray();
        Assert.True(files.Length >= 7, $"{files.Length} files");
        foreach (var path in files)
        {
            var committed = File.ReadAllText(path).ReplaceLineEndings("\n");
            var folder = Path.GetDirectoryName(path)!;
            var read = CarveFile.Read(committed, folder);
            Assert.True(read.Succeeded, path + ": " + string.Join("; ", read.Errors));
            Assert.Equal(read.Model!.Meshes.Any(m => m.Mesh.Targets is not null), read.Version == 8);
            if (read.Version == 1) continue; // read only; written as version 2
            var written = CarveFile.Write(read.Model, folder);
            Assert.True(committed == written, path + " is not written back byte for byte");
            Assert.Equal(committed, CarveFile.Write(CarveFile.Read(written, folder).Model!, folder));
        }
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(dir.FullName, "samples", "gatehouse.carve")))
            dir = dir.Parent ?? throw new DirectoryNotFoundException("the repo root");
        return dir.FullName;
    }
}
