using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace Carve.Core.Tests;

/// <summary>
/// T-160's graph slots: a material texture slot naming a <c>.texgraph</c> output, its shape rules, the '@' rule for new
/// material names, and Carve file version 10 (version 9 with graph slots), written only for a model with a graph slot and
/// read back byte for byte; every other model is written as before.
/// </summary>
[Trait("Tier", "unit")]
public class GraphSlotFileTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "carve-graphslot-" + Guid.NewGuid().ToString("N"));

    public GraphSlotFileTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
        GC.SuppressFinalize(this);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(dir.FullName, "samples", "barrel.carve"))) dir = dir.Parent!;
        return dir.FullName;
    }

    private static string Fixture => Path.Combine(RepoRoot(), "tests", "Carve.Core.Tests", "Fixtures", "graph-slots-v10.carve");

    private MaterialGraph Graph(string output = "base_colour", int size = 1024) =>
        new(Path.Combine(_folder, "graphs", "iron.texgraph"), output, size);

    private Model Iron(Model? onto = null)
    {
        var model = onto ?? Model.Of("box", TestMeshes.Cube());
        return model.WithMaterial(new Material("weathered-iron", Metallic: 1f,
            BaseColourTexture: Graph(), MetallicRoughnessTexture: Graph("roughness", 512)));
    }

    // ---- The slot ----------------------------------------------------------------------------------------------------

    [Fact]
    public void A_graph_slot_is_equal_by_value_and_counts_as_a_texture()
    {
        Assert.Equal(Graph(), Graph());
        Assert.NotEqual(Graph(), Graph(size: 512));
        var m = new Material("m", OcclusionTexture: Graph("occlusion"), Occlusion: 0.5f);
        Assert.True(m.HasTexture);
        Assert.True(m.HasGraph);
        Assert.False(m.OcclusionHasNoEffect);
        Assert.Empty(Material.Problems(m));
        Assert.False(new Material("m").HasGraph);
    }

    [Theory]
    [InlineData("graphs/iron.texgraph", "base_colour", 1024, "'graphs/iron.texgraph' is not an absolute path")]
    [InlineData("ABS/iron.png", "base_colour", 1024, "does not end in .texgraph")]
    [InlineData("ABS/iron.texgraph", "Base", 1024, "output 'Base' is not an output name: a lowercase letter, then lowercase letters, digits and '_'")]
    [InlineData("ABS/iron.texgraph", "1st", 1024, "output '1st' is not an output name")]
    [InlineData("ABS/iron.texgraph", "", 1024, "output '' is not an output name")]
    [InlineData("ABS/iron.texgraph", "wear_2", 32, "size is 32; it is a power of two from 64 to 4096")]
    [InlineData("ABS/iron.texgraph", "wear_2", 100, "size is 100; it is a power of two from 64 to 4096")]
    [InlineData("ABS/iron.texgraph", "wear_2", 8192, "size is 8192; it is a power of two from 64 to 4096")]
    public void Problems_check_a_graph_slots_shape(string path, string output, int size, string expected)
    {
        var full = path.Replace("ABS", _folder, StringComparison.Ordinal);
        var problem = Assert.Single(Material.Problems(new Material("m", NormalTexture: new MaterialGraph(full, output, size))));
        Assert.Equal("normal_texture", problem.Field);
        Assert.Contains(expected, problem.Problem, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(64)]
    [InlineData(4096)]
    public void The_size_range_is_64_to_4096(int size) => Assert.Null(Graph(size: size).Problem());

    [Fact]
    public void At_is_refused_in_a_new_name_only()
    {
        Assert.Equal("a material name must not contain '@' (an export names a material's per-mesh copies <material>@<mesh>)", Material.NameProblem("iron@hoop"));
        Assert.Null(Material.NameProblem("iron@hoop", existing: true));
        Assert.Empty(Material.Problems(new Material("iron@hoop")));
    }

    [Fact]
    public void A_file_with_an_at_in_a_material_name_still_opens()
    {
        var model = Model.Of("box", TestMeshes.Cube()).WithMaterial(new Material("iron@hoop"));
        var text = CarveFile.Write(model);
        var read = CarveFile.Read(text);
        Assert.True(read.Succeeded, string.Join("\n", read.Errors));
        Assert.Equal("iron@hoop", read.Model!.Materials.Single().Name);
    }

    // ---- Version 10 --------------------------------------------------------------------------------------------------

    [Fact]
    public void A_model_with_a_graph_slot_is_written_as_version_10_with_the_path_relative_and_the_size()
    {
        var text = CarveFile.Write(Iron(), _folder);
        Assert.StartsWith("{\n  \"carve\": 10,\n", text);
        Assert.Contains("{\"name\": \"weathered-iron\", \"metallic\": 1, \"base_colour_texture\": {\"graph\": \"graphs/iron.texgraph\", \"output\": \"base_colour\", \"size\": 1024}, \"metallic_roughness_texture\": {\"graph\": \"graphs/iron.texgraph\", \"output\": \"roughness\", \"size\": 512}}", text);
        // Version 9's content: the parts after the meshes.
        Assert.Contains("\n  ],\n  \"parts\": [\n    {\"name\": \"box\", \"parent\": null,", text);
    }

    [Fact]
    public void Version_10_round_trips_byte_for_byte_and_resolves_against_the_files_folder_without_reading_the_graph()
    {
        var text = CarveFile.Write(Iron(), _folder);
        var read = CarveFile.Read(text, _folder);
        Assert.True(read.Succeeded, string.Join("\n", read.Errors));
        Assert.Equal(10, read.Version);
        // No graph file exists: the Core does not read it.
        Assert.Equal(Graph(), read.Model!.Materials.Single().BaseColourTexture);
        Assert.Equal(text, CarveFile.Write(read.Model, _folder));
    }

    [Fact]
    public void A_part_tree_with_targets_and_a_graph_slot_is_version_10_and_round_trips()
    {
        var model = Iron(PartTreeSample.TargetedTree());
        var text = CarveFile.Write(model, _folder);
        Assert.StartsWith("{\n  \"carve\": 10,\n", text);
        var nine = CarveFile.Write(PartTreeSample.TargetedTree());
        // Everything but the version line and the materials is version 9's text.
        Assert.Equal(nine[nine.IndexOf("  \"meshes\"", StringComparison.Ordinal)..], text[text.IndexOf("  \"meshes\"", StringComparison.Ordinal)..]);
        var read = CarveFile.Read(text, _folder);
        Assert.True(read.Succeeded, string.Join("\n", read.Errors));
        Assert.Equal(text, CarveFile.Write(read.Model!, _folder));
    }

    [Fact]
    public void The_committed_version_10_fixture_reads_and_writes_back_byte_for_byte()
    {
        var text = File.ReadAllText(Fixture).ReplaceLineEndings("\n");
        var folder = Path.GetDirectoryName(Fixture)!;
        var read = CarveFile.Read(text, folder);
        Assert.True(read.Succeeded, string.Join("\n", read.Errors));
        Assert.Equal(10, read.Version);
        Assert.Equal(new MaterialGraph(Path.Combine(folder, "graphs", "iron.texgraph"), "base_colour", 1024), read.Model!.Materials.Single().BaseColourTexture);
        Assert.Equal(text, CarveFile.Write(read.Model, folder));
        // It is what this test class's model writes.
        Assert.Equal(text, CarveFile.Write(IronIn(folder), folder));
    }

    private static Model IronIn(string folder)
    {
        var g = (string output, int size) => new MaterialGraph(Path.Combine(folder, "graphs", "iron.texgraph"), output, size);
        return Model.Of("box", TestMeshes.Cube()).WithMaterial(new Material("weathered-iron", Metallic: 1f,
            BaseColourTexture: g("base_colour", 1024), MetallicRoughnessTexture: g("roughness", 512)));
    }

    [Fact]
    public void Save_refuses_a_graph_on_another_drive_in_the_words_of_a_texture()
    {
        var drive = Path.GetPathRoot(_folder)!.StartsWith("Q", StringComparison.OrdinalIgnoreCase) ? "R:\\" : "Q:\\";
        if (!OperatingSystem.IsWindows()) return;
        var model = Model.Of("box", TestMeshes.Cube()).WithMaterial(new Material("far", BaseColourTexture: new MaterialGraph(drive + "iron.texgraph", "base_colour", 256)));
        var problem = CarveFile.TextureProblem(model, _folder);
        Assert.Equal($"materials[0].base_colour_texture: the graph '{drive}iron.texgraph' of the material 'far' has no path relative to '{_folder}' (it is on another drive); save the file on the graph's drive, or copy the graph there and define the material again", problem);
        Assert.Throws<ArgumentException>(() => CarveFile.Write(model, _folder));
        Assert.Throws<ArgumentException>(() => CarveFile.Write(model));
    }

    [Theory]
    [InlineData("{\"graph\": \"g.texgraph\", \"output\": \"base_colour\"}", "materials[0].base_colour_texture.size", "missing: a graph slot needs size")]
    [InlineData("{\"output\": \"base_colour\", \"size\": 64}", "materials[0].base_colour_texture.graph", "missing: a graph slot needs graph")]
    [InlineData("{\"graph\": \"g.texgraph\", \"size\": 64}", "materials[0].base_colour_texture.output", "missing: a graph slot needs output")]
    [InlineData("{\"graph\": \"g.texgraph\", \"output\": \"base_colour\", \"size\": 64, \"seed\": 1}", "materials[0].base_colour_texture.seed", "unknown key")]
    [InlineData("{\"graph\": \"g.texgraph\", \"output\": \"base_colour\", \"size\": 100}", "materials[0].base_colour_texture", "size is 100; it is a power of two from 64 to 4096")]
    [InlineData("{\"graph\": \"g.png\", \"output\": \"base_colour\", \"size\": 64}", "materials[0].base_colour_texture", "does not end in .texgraph")]
    [InlineData("{\"graph\": \"g.texgraph\", \"output\": \"Base\", \"size\": 64}", "materials[0].base_colour_texture", "is not an output name")]
    [InlineData("{\"graph\": 3, \"output\": \"base_colour\", \"size\": 64}", "materials[0].base_colour_texture.graph", "not a string")]
    [InlineData("{\"graph\": \"g.texgraph\", \"output\": \"base_colour\", \"size\": 64.5}", "materials[0].base_colour_texture.size", "is not an integer size")]
    public void A_malformed_graph_slot_is_refused_where_it_is(string slot, string element, string message)
    {
        var text = SlotFile(10, slot);
        var read = CarveFile.Read(text, _folder);
        Assert.False(read.Succeeded);
        Assert.Contains(read.Errors, e => e.Element == element && e.Message.Contains(message, StringComparison.Ordinal));
    }

    [Fact]
    public void A_graph_slot_in_a_version_9_file_is_refused()
    {
        var read = CarveFile.Read(SlotFile(9, "{\"graph\": \"g.texgraph\", \"output\": \"base_colour\", \"size\": 64}"), _folder);
        Assert.False(read.Succeeded);
        Assert.Contains(read.Errors, e => e.Element == "materials[0].base_colour_texture" && e.Message.Contains("not a string", StringComparison.Ordinal));
    }

    /// <summary>The written version 10 text of a cube with one material, its base colour slot replaced by <paramref name="slot"/>, as version <paramref name="version"/>.</summary>
    private string SlotFile(int version, string slot)
    {
        var text = CarveFile.Write(Model.Of("box", TestMeshes.Cube()).WithMaterial(new Material("m", BaseColourTexture: Graph())), _folder);
        var good = "{\"graph\": \"graphs/iron.texgraph\", \"output\": \"base_colour\", \"size\": 1024}";
        Assert.Contains(good, text);
        return text.Replace(good, slot, StringComparison.Ordinal).Replace("\"carve\": 10,", $"\"carve\": {version},", StringComparison.Ordinal);
    }

    // ---- Every other model, as before --------------------------------------------------------------------------------

    [Theory]
    [InlineData("leaky-shed.carve", 2)]
    [InlineData("gardener.carve", 2)]
    [InlineData("arm-lean.carve", 2)]
    [InlineData("gardener-soft.carve", 3)]
    [InlineData("gatehouse.carve", 4)]
    [InlineData("barrel.carve", 5)]
    [InlineData("wrench.carve", 5)]
    [InlineData("barrel-textured.carve", 6)]
    [InlineData("barrel-planks.carve", 6)]
    [InlineData("rover.carve", 7)]
    [InlineData("arm.carve", 8)]
    public void Every_committed_sample_is_written_byte_for_byte_as_it_is(string sample, int version)
    {
        var path = Path.Combine(RepoRoot(), "samples", sample);
        var text = File.ReadAllText(path).ReplaceLineEndings("\n");
        var read = CarveFile.Read(text, Path.GetDirectoryName(path)!);
        Assert.True(read.Succeeded, string.Join("\n", read.Errors));
        Assert.Equal(version, read.Version);
        Assert.Equal(text, CarveFile.Write(read.Model!, Path.GetDirectoryName(path)!));
    }

    /// <summary>
    /// A model of each version 2 to 9, built in code, against the SHA-256 of what main's writer (before T-160) gives it,
    /// taken by running this test with T-160's Core changes set aside.
    /// </summary>
    [Theory]
    [InlineData(2, "4C81FC7BB9930B094D898F75BFC277E1E80659396615A503111979B22299B603")]
    [InlineData(3, "A10DE8BD470590A81FDCB609BCBE4469D19B120F7E6F95B0B1D861413A3A9F84")]
    [InlineData(4, "3127FA6A43DDCBBEC6C8D55AF6AB2F61D290504543E01EA33648DA346C278944")]
    [InlineData(5, "E6AF749BB8EA6AD4E9F7B57CD1EFF8C8912060347D4409B9090E41AD769F0786")]
    [InlineData(6, "30110D77C49EC9A51F532E5C0B3ED0AD26D0A216AC6D0FA13A0525B1D19CC24F")]
    [InlineData(7, "335AA55A1DF0825B7CF0900EE434BABA24BC94E759A784A86D621A2F66646603")]
    [InlineData(8, "B33424B5236517E27C37E0A54E13642811499D93D8F79CC41DAE46F0E66A30F3")]
    [InlineData(9, "389F2A803C75A7617F729C0B1C3FB22966B03A63B389CF5AB1446585264B646C")]
    public void A_model_of_each_older_version_is_written_as_mains_writer_wrote_it(int version, string sha256)
    {
        var text = OlderVersionModel(version);
        Assert.StartsWith($"{{\n  \"carve\": {version},\n", text);
        Assert.Equal(sha256, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))));
    }

    private static string OlderVersionModel(int version)
    {
        var cube = TestMeshes.Cube();
        // Version 4: the cube with a tombstone, a deleted ninth vertex.
        var tombstoned = PolygonMesh.Create([.. cube.Vertices, System.Numerics.Vector3.Zero], cube.Faces, [8]).Mesh!;
        return version switch
        {
            2 => CarveFile.Write(Model.Of("box", cube)),
            3 => CarveFile.Write(Model.Of("box", cube.WithShade(MeshShade.Flat.WithMode(ShadingMode.Smooth)))),
            4 => CarveFile.Write(Model.Of("box", tombstoned)),
            5 => CarveFile.Write(Model.Of("barrel", TestMeshes.SeamedBarrel())),
            6 => CarveFile.Write(Model.Of("box", cube).WithMaterial(new Material("iron", Metallic: 1f))),
            7 => CarveFile.Write(PartTreeSample.Tree()),
            8 => CarveFile.Write(Model.Of("box", cube.WithTargets(PartTreeSample.Bulge()))),
            _ => CarveFile.Write(PartTreeSample.TargetedTree()),
        };
    }
}
