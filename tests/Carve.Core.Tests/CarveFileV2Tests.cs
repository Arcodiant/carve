using System.Numerics;
using Xunit;
using static Carve.Core.Tests.TestMeshes;

namespace Carve.Core.Tests;

/// <summary>
/// The Carve file, version 2: the whole model (meshes with colours, groups and weights, skeleton, clips) written in one
/// fixed text and read back bit for bit; a file is refused exactly where the tools would refuse the same thing.
/// </summary>
[Trait("Tier", "unit")]
public class CarveFileV2Tests
{
    /// <summary>define_mesh's unit cube as version 2 writes it (also the text in save's description).</summary>
    internal const string CubeText =
        "{\n" +
        "  \"carve\": 2,\n" +
        "  \"meshes\": [\n" +
        "    {\n" +
        "      \"name\": \"mesh\",\n" +
        "      \"vertices\": [\n" +
        "        [0, 0, 0],\n" +
        "        [1, 0, 0],\n" +
        "        [1, 1, 0],\n" +
        "        [0, 1, 0],\n" +
        "        [0, 0, 1],\n" +
        "        [1, 0, 1],\n" +
        "        [1, 1, 1],\n" +
        "        [0, 1, 1]\n" +
        "      ],\n" +
        "      \"faces\": [\n" +
        "        [0, 3, 2, 1],\n" +
        "        [4, 5, 6, 7],\n" +
        "        [0, 1, 5, 4],\n" +
        "        [3, 7, 6, 2],\n" +
        "        [0, 4, 7, 3],\n" +
        "        [1, 2, 6, 5]\n" +
        "      ],\n" +
        "      \"colours\": [],\n" +
        "      \"groups\": [],\n" +
        "      \"weights\": []\n" +
        "    }\n" +
        "  ],\n" +
        "  \"skeleton\": null,\n" +
        "  \"clips\": []\n" +
        "}\n";

    /// <summary>Every key of version 2 in use: <see cref="Arm"/> as <c>CarveFile.Write</c> gives it, byte for byte.</summary>
    internal const string ArmText =
        "{\n" +
        "  \"carve\": 2,\n" +
        "  \"meshes\": [\n" +
        "    {\n" +
        "      \"name\": \"arm\",\n" +
        "      \"vertices\": [\n" +
        "        [0, 0, 0],\n" +
        "        [1, 0, 0],\n" +
        "        [0, 1, 0]\n" +
        "      ],\n" +
        "      \"faces\": [\n" +
        "        [0, 1, 2]\n" +
        "      ],\n" +
        "      \"colours\": [\n" +
        "        {\"colour\": \"#C8503C\", \"faces\": [0]}\n" +
        "      ],\n" +
        "      \"groups\": [\n" +
        "        {\"name\": \"all\", \"faces\": [0]}\n" +
        "      ],\n" +
        "      \"weights\": [\n" +
        "        {\"vertex\": 0, \"bones\": {\"base\": 1}},\n" +
        "        {\"vertex\": 2, \"bones\": {\"base\": 0.25, \"joint\": 0.75}}\n" +
        "      ]\n" +
        "    }\n" +
        "  ],\n" +
        "  \"skeleton\": [\n" +
        "    {\"name\": \"base\", \"parent\": null, \"position\": [0, 0, 0], \"rotation\": [0, 0, 0, 1]},\n" +
        "    {\"name\": \"joint\", \"parent\": \"base\", \"position\": [0, 1, 0], \"rotation\": [0, 0, 0, 1]}\n" +
        "  ],\n" +
        "  \"clips\": [\n" +
        "    {\n" +
        "      \"name\": \"wave\",\n" +
        "      \"duration\": 1,\n" +
        "      \"loop\": true,\n" +
        "      \"channels\": {\n" +
        "        \"joint\": {\n" +
        "          \"rotation\": {\n" +
        "            \"interpolation\": \"LINEAR\",\n" +
        "            \"keys\": [\n" +
        "              {\"time\": 0, \"value\": [0, 0, 0, 1]},\n" +
        "              {\"time\": 0.5, \"value\": [0, 0, 0.70710677, 0.70710677]},\n" +
        "              {\"time\": 1, \"value\": [0, 0, 0, 1]}\n" +
        "            ]\n" +
        "          },\n" +
        "          \"translation\": {\n" +
        "            \"interpolation\": \"STEP\",\n" +
        "            \"keys\": [\n" +
        "              {\"time\": 0, \"value\": [0, 1, 0]}\n" +
        "            ]\n" +
        "          }\n" +
        "        }\n" +
        "      }\n" +
        "    }\n" +
        "  ]\n" +
        "}\n";

    private static readonly Quaternion Quarter = new(0, 0, 0.70710677f, 0.70710677f);

    internal static Skeleton Chain(float jointY = 1, Quaternion? jointRotation = null) => Skeleton.Create(
    [
        new BoneSpec("base", null, Vector3.Zero, Quaternion.Identity),
        new BoneSpec("joint", "base", new Vector3(0, jointY, 0), jointRotation ?? Quaternion.Identity),
    ]).Skeleton!;

    private static Clip Wave(Skeleton skeleton) => Clip.Create("wave", 1, true,
    [
        new BoneChannelsSpec("joint",
            new ChannelSpec<Quaternion>("LINEAR", [new(0, Quaternion.Identity), new(0.5f, Quarter), new(1, Quaternion.Identity)]),
            new ChannelSpec<Vector3>("STEP", [new(0, new Vector3(0, 1, 0))])),
    ], skeleton).Clip!;

    private static PolygonMesh Triangle() => Make([new(0, 0, 0), new(1, 0, 0), new(0, 1, 0)], [[0, 1, 2]]);

    /// <summary>One triangle, coloured, grouped and weighted, on a two-bone chain with one clip.</summary>
    internal static Model Arm()
    {
        var skeleton = Chain();
        var mesh = Triangle()
            .WithStyle(FaceStyle.None.WithColour([0], "#C8503C").WithGroup("all", [0]))
            .WithWeights(VertexWeights.Of([KeyValuePair.Create(0, new[] { new Influence(0, 1) }),
                KeyValuePair.Create(2, new[] { new Influence(0, 0.25), new Influence(1, 0.75) })]));
        return Model.Create([new NamedMesh("arm", mesh)], skeleton, [Wave(skeleton)]).Model!;
    }

    /// <summary>
    /// Every field set and awkward: two meshes, colours given in both cases and spread over faces, groups out of order,
    /// weights from <see cref="VertexWeights.Normalise"/> of thirds and sevenths, bones and keys at awkward floats and -0.
    /// </summary>
    private static Model Awkward()
    {
        var odd = BitConverter.Int32BitsToSingle(1065520987); // 1.0199999f
        var skeleton = Skeleton.Create(
        [
            new BoneSpec("Hips", null, new Vector3(0.1f, odd, -0f), Quaternion.Normalize(new Quaternion(0.1f, 0.2f, 0.3f, 0.9f))),
            new BoneSpec("Spine", "Hips", new Vector3(1e-30f, 0.3f, float.Epsilon), Quaternion.Identity),
            new BoneSpec("Head \"top\" ü", "Spine", new Vector3(0, 0.4f, 0), Quaternion.Normalize(new Quaternion(0, 1, 0, 3))),
        ]).Skeleton!;
        var crate = Crate();
        var style = FaceStyle.None.WithColour([5, 1, 9], "#c8503c").WithColour([0, 3], "#3C78C8").WithGroup("rim", [9, 2, 2]).WithGroup("all", Enumerable.Range(0, crate.Faces.Count));
        var thirds = VertexWeights.Normalise(new Dictionary<int, double> { [0] = 1, [1] = 1, [2] = 1 }, out _);
        var sevenths = VertexWeights.Normalise(new Dictionary<int, double> { [0] = 1, [1] = 2, [2] = 4 }, out _);
        var weights = VertexWeights.Of([KeyValuePair.Create(0, thirds), KeyValuePair.Create(3, sevenths), KeyValuePair.Create(7, new[] { new Influence(2, 1) })]);
        var clip = Clip.Create("Walk", 0.8333333f, false,
        [
            new BoneChannelsSpec("Hips", null, new ChannelSpec<Vector3>(null, [new(0, new Vector3(0, odd, 0)), new(0.41666666f, new Vector3(-0f, 1.1f, 1e-7f))])),
            new BoneChannelsSpec("Head \"top\" ü", new ChannelSpec<Quaternion>("STEP", [new(0.1f, Quaternion.Normalize(new Quaternion(0.3f, 0, 0, 1)))]), null),
        ], skeleton).Clip!;
        var idle = Clip.Create("Idle", 2, true, [new BoneChannelsSpec("Spine", new ChannelSpec<Quaternion>("LINEAR", [new(0, Quaternion.Identity)]), null)], skeleton).Clip!;
        return Model.Create(
            [new NamedMesh("Body", crate.WithStyle(style).WithWeights(weights)), new NamedMesh("Hat", LBlock())],
            skeleton, [clip, idle]).Model!;
    }

    private static Model RoundTrip(Model model)
    {
        var result = CarveFile.Read(CarveFile.Write(model));
        Assert.True(result.Succeeded, string.Join("; ", result.Errors));
        return result.Model!;
    }

    internal static (int, int, int) Bits(Vector3 v)
        => (BitConverter.SingleToInt32Bits(v.X), BitConverter.SingleToInt32Bits(v.Y), BitConverter.SingleToInt32Bits(v.Z));

    private static (int, int, int, int) Bits(Quaternion q)
        => (BitConverter.SingleToInt32Bits(q.X), BitConverter.SingleToInt32Bits(q.Y), BitConverter.SingleToInt32Bits(q.Z), BitConverter.SingleToInt32Bits(q.W));

    /// <summary>Every field of two models equal, floats and doubles bit for bit.</summary>
    internal static void AssertSameModel(Model expected, Model actual)
    {
        Assert.Equal(expected.Names, actual.Names);
        for (var m = 0; m < expected.Meshes.Count; m++)
        {
            var (e, a) = (expected.Meshes[m].Mesh, actual.Meshes[m].Mesh);
            Assert.Equal(e.Vertices.Select(Bits), a.Vertices.Select(Bits));
            Assert.Equal(e.Faces.Select(f => f.ToArray()), a.Faces.Select(f => f.ToArray()));
            Assert.Equal(e.Style?.Colours.ToArray(), a.Style?.Colours.ToArray());
            Assert.Equal(e.Style?.Groups.Select(g => (g.Name, g.Faces.ToArray())), a.Style?.Groups.Select(g => (g.Name, g.Faces.ToArray())));
            Assert.Equal(e.Weights is null, a.Weights is null);
            if (e.Weights is { } ew)
            {
                Assert.Equal(ew.Vertices, a.Weights!.Vertices);
                foreach (var v in ew.Vertices)
                    Assert.Equal(ew.Of(v).Select(x => (x.Bone, BitConverter.DoubleToInt64Bits(x.Weight))),
                        a.Weights.Of(v).Select(x => (x.Bone, BitConverter.DoubleToInt64Bits(x.Weight))));
            }
        }
        Assert.Equal(expected.Skeleton is null, actual.Skeleton is null);
        if (expected.Skeleton is { } s)
            Assert.Equal(s.Bones.Select(b => (b.Name, b.Parent, Bits(b.Position), Bits(b.Rotation))),
                actual.Skeleton!.Bones.Select(b => (b.Name, b.Parent, Bits(b.Position), Bits(b.Rotation))));
        Assert.Equal(expected.Clips.Count, actual.Clips.Count);
        for (var c = 0; c < expected.Clips.Count; c++)
        {
            var (e, a) = (expected.Clips[c], actual.Clips[c]);
            Assert.Equal((e.Name, BitConverter.SingleToInt32Bits(e.Duration), e.Loop), (a.Name, BitConverter.SingleToInt32Bits(a.Duration), a.Loop));
            Assert.Equal(e.Channels.Select(Describe), a.Channels.Select(Describe));
        }

        static string Describe(BoneChannels c) =>
            $"{c.Bone} R{c.Rotation?.Interpolation}:{string.Join(",", c.Rotation?.Keys.Select(k => $"{BitConverter.SingleToInt32Bits(k.Time)}{Bits(k.Value)}") ?? [])}"
            + $" T{c.Translation?.Interpolation}:{string.Join(",", c.Translation?.Keys.Select(k => $"{BitConverter.SingleToInt32Bits(k.Time)}{Bits(k.Value)}") ?? [])}";
    }

    // ---- Writing and reading back ------------------------------------------------------------------------------------

    [Fact]
    public void TheArmIsWrittenAsDocumented() => Assert.Equal(ArmText, CarveFile.Write(Arm()));

    [Fact]
    public void TheDocumentedArmReads() => AssertSameModel(Arm(), CarveFile.Read(ArmText).Model!);

    [Fact]
    public void TheCubeIsOneMeshNamedMesh() => Assert.Equal(CubeText, CarveFile.Write(Cube()));

    [Fact]
    public void EveryFieldRoundTripsBitForBit()
    {
        var model = Awkward();
        AssertSameModel(model, RoundTrip(model));
    }

    [Fact]
    public void TheSameModelWritesTheSameText()
    {
        var text = CarveFile.Write(Awkward());
        Assert.Equal(text, CarveFile.Write(Awkward()));
        Assert.Equal(text, CarveFile.Write(RoundTrip(Awkward())));
        Assert.DoesNotContain("\r", text);
        Assert.EndsWith("}\n", text);
    }

    [Fact]
    public void NamesAreWrittenAsTheyAreWithOnlyQuotesAndBackslashesEscaped()
    {
        var text = CarveFile.Write(Awkward());
        Assert.Contains("\"Head \\\"top\\\" ü\"", text);
    }

    /// <summary>Normalised weights of many shapes read back to the bit, since reading keeps weights already normalised.</summary>
    [Fact]
    public void NormalisedWeightsReadBackToTheBit()
    {
        var random = new Random(35);
        var skeleton = Skeleton.Create(Enumerable.Range(0, 6).Select(i => new BoneSpec($"b{i}", null, Vector3.Zero, Quaternion.Identity)).ToArray()).Skeleton!;
        var mesh = Crate();
        var byVertex = new List<KeyValuePair<int, Influence[]>>();
        for (var v = 0; v < mesh.Vertices.Count; v++)
        {
            var raw = Enumerable.Range(0, 1 + random.Next(6)).ToDictionary(b => b, _ => random.NextDouble() * 10 + 1e-3);
            byVertex.Add(KeyValuePair.Create(v, VertexWeights.Normalise(raw, out _)));
        }
        var model = Model.Create([new NamedMesh("m", mesh.WithWeights(VertexWeights.Of(byVertex)))], skeleton, []).Model!;
        AssertSameModel(model, RoundTrip(model));
    }

    [Fact]
    public void WeightsNotYetNormalisedAreNormalisedAsSetWeightsDoes()
    {
        var text = ArmText.Replace("{\"base\": 0.25, \"joint\": 0.75}", "{\"base\": 1, \"joint\": 3, \"tip\": 0}")
            .Replace("\"joint\", \"parent\": \"base\", \"position\": [0, 1, 0], \"rotation\": [0, 0, 0, 1]}",
                "\"joint\", \"parent\": \"base\", \"position\": [0, 1, 0], \"rotation\": [0, 0, 0, 1]},\n    {\"name\": \"tip\", \"parent\": \"joint\", \"position\": [0, 1, 0], \"rotation\": [0, 0, 0, 1]}");
        var read = CarveFile.Read(text);
        Assert.True(read.Succeeded, string.Join("; ", read.Errors));
        var expected = VertexWeights.Normalise(new Dictionary<int, double> { [0] = 1, [1] = 3 }, out _);
        Assert.Equal(expected, read.Model!.Meshes[0].Mesh.Weights!.Of(2));
    }

    [Fact]
    public void FiveInfluencesAreCappedAsSetWeightsCapsThem()
    {
        var model = RoundTrip(Model.Create([], null, []).Model!.With("m", Triangle()).WithSkeleton(Skeleton.Create(
            Enumerable.Range(0, 5).Select(i => new BoneSpec($"b{i}", null, Vector3.Zero, Quaternion.Identity)).ToArray()).Skeleton!));
        var text = CarveFile.Write(model).Replace("\"weights\": []",
            "\"weights\": [\n        {\"vertex\": 1, \"bones\": {\"b0\": 1, \"b1\": 2, \"b2\": 3, \"b3\": 4, \"b4\": 5}}\n      ]");
        var read = CarveFile.Read(text);
        Assert.True(read.Succeeded, string.Join("; ", read.Errors));
        var raw = new Dictionary<int, double> { [0] = 1, [1] = 2, [2] = 3, [3] = 4, [4] = 5 };
        Assert.Equal(VertexWeights.Normalise(raw, out _), read.Model!.Meshes[0].Mesh.Weights!.Of(1));
    }

    [Fact]
    public void OptionalKeysMayBeLeftOut()
    {
        var text = "{\"carve\": 2, \"meshes\": [{\"name\": \"m\", \"vertices\": [[0,0,0],[1,0,0],[0,1,0]], \"faces\": [[0,1,2]]}]}";
        var read = CarveFile.Read(text);
        Assert.True(read.Succeeded, string.Join("; ", read.Errors));
        Assert.Null(read.Model!.Skeleton);
        Assert.Null(read.Mesh!.Style);
        Assert.Null(read.Mesh.Weights);
    }

    [Fact]
    public void AVersion1FileOpensAsOneMeshNamedMesh()
    {
        var read = CarveFile.Read(CarveFileTests.CubeText);
        Assert.True(read.Succeeded);
        Assert.Equal([Model.DefaultName], read.Model!.Names);
        AssertSameModel(Model.Of(Model.DefaultName, Cube()), read.Model);
    }

    [Fact]
    public void WarningsAreEachMeshsAsDefineMeshGivesThem()
    {
        var open = Make([new(0, 0, 0), new(1, 0, 0), new(0, 1, 0)], [[0, 1, 2]]);
        var read = RoundTrip(Model.Of("a", Cube()).With("b", open));
        Assert.Equal(MeshReport.Create(open).Warnings, CarveFile.Read(CarveFile.Write(read)).Warnings);
    }

    // ---- Refusals: the same as the tools' -----------------------------------------------------------------------------

    private static void Refused(string text, string element, string message)
    {
        var result = CarveFile.Read(text);
        Assert.False(result.Succeeded);
        Assert.Null(result.Model);
        Assert.Contains(new MeshError(element, message), result.Errors);
    }

    private static string ArmWith(string from, string to)
    {
        Assert.Contains(from, ArmText);
        return ArmText.Replace(from, to);
    }

    [Theory]
    [InlineData("{\"base\": 1}}", "{\"hand\": 1}}", "meshes[0].weights[0].bones.hand", "no bone named 'hand'; the bones are 'base', 'joint'")]
    [InlineData("{\"base\": 1}}", "{\"base\": -1}}", "meshes[0].weights[0].bones.base", "-1 is not a weight; weights are 0 or more")]
    [InlineData("{\"base\": 1}}", "{\"base\": 0}}", "meshes[0].weights[0].bones", "every weight is 0; at least one must be above 0")]
    [InlineData("{\"base\": 1}}", "{}}", "meshes[0].weights[0].bones", "give at least one bone and its weight")]
    [InlineData("{\"vertex\": 0,", "{\"vertex\": 3,", "meshes[0].weights[0].vertex", "there is no vertex 3: ids run 0..2")]
    [InlineData("{\"vertex\": 2,", "{\"vertex\": 0,", "meshes[0].weights[1].vertex", "vertex 0 is given weights earlier, at weights[0]; give each vertex once")]
    [InlineData("{\"base\": 1}}", "{\"base\": \"1\"}}", "meshes[0].weights[0].bones.base", "weight is a string, not a number")]
    public void BadWeightsAreRefused(string from, string to, string element, string message)
        => Refused(ArmWith(from, to), element, message);

    [Fact]
    public void WeightsRefusalsAreSetWeightsOwn()
    {
        // The reader and set_weights share VertexWeights.Raw: the message for the same bad weight is the same text.
        var errors = new List<MeshError>();
        VertexWeights.Raw(Chain(), [KeyValuePair.Create("hand", 1.0)], "weights", errors);
        var tool = Assert.Single(errors);
        var file = CarveFile.Read(ArmWith("{\"base\": 1}}", "{\"hand\": 1}}")).Errors.Single(e => e.Element.EndsWith(".hand", StringComparison.Ordinal));
        Assert.Equal(tool.Message, file.Message);
    }

    [Fact]
    public void WeightsWithNoSkeletonAreRefused()
    {
        var start = ArmText.IndexOf("  \"skeleton\": [", StringComparison.Ordinal);
        var end = ArmText.IndexOf("  \"clips\"", StringComparison.Ordinal);
        var text = ArmText[..start] + "  \"skeleton\": null,\n" + ArmText[end..];
        Refused(text, "meshes[0].weights", "the file has no skeleton, and weights name its bones");
        Refused(text, "clips", "the file has no skeleton, and clips move its bones");
    }

    [Theory]
    [InlineData("\"parent\": \"base\"", "\"parent\": \"hip\"", "skeleton[1]", "unknown parent 'hip'; no bone has that name")]
    [InlineData("\"name\": \"joint\", \"parent\": \"base\"", "\"name\": \"base\", \"parent\": \"base\"", "skeleton[1]", "the name 'base' is already skeleton[0]'s; names must be unique")]
    [InlineData("\"position\": [0, 1, 0], \"rotation\": [0, 0, 0, 1]}\n  ]", "\"position\": [0, 1, 0], \"rotation\": [0, 0, 0, 2]}\n  ]", "skeleton[1]", "rotation is not a unit quaternion: its length is 2, and it must be 1 (within 0.001)")]
    [InlineData("\"position\": [0, 1, 0], \"rotation\": [0, 0, 0, 1]}\n  ]", "\"position\": [0, 1], \"rotation\": [0, 0, 0, 1]}\n  ]", "skeleton[1].position", "position has 2 numbers; a position is [x, y, z]")]
    public void BadBonesAreRefused(string from, string to, string element, string message)
        => Refused(ArmWith(from, to), element, message);

    [Fact]
    public void AnEmptySkeletonIsRefusedAsDefineSkeletonRefusesIt()
    {
        var start = ArmText.IndexOf("  \"skeleton\": [", StringComparison.Ordinal);
        var end = ArmText.IndexOf("  \"clips\"", StringComparison.Ordinal);
        Refused(ArmText[..start] + "  \"skeleton\": [],\n" + ArmText[end..], "skeleton", "a skeleton needs at least one bone");
    }

    [Theory]
    [InlineData("\"interpolation\": \"STEP\"", "\"interpolation\": \"step\"", "clips[0].channels.joint.translation.interpolation", "'step' is not offered; use LINEAR or STEP")]
    [InlineData("{\"time\": 1, \"value\": [0, 0, 0, 1]}", "{\"time\": 2, \"value\": [0, 0, 0, 1]}", "clips[0].channels.joint.rotation.keys[2]", "time 2 is outside the clip, which runs from 0 to 1")]
    [InlineData("{\"time\": 1, \"value\": [0, 0, 0, 1]}", "{\"time\": 0.5, \"value\": [0, 0, 0, 1]}", "clips[0].channels.joint.rotation.keys[2]", "time 0.5 is the same as the key before; key times must differ")]
    [InlineData("\"channels\": {\n        \"joint\": {", "\"channels\": {\n        \"hand\": {", "clips[0].channels.hand", "no bone named 'hand'; the bones are 'base', 'joint'")]
    [InlineData("\"duration\": 1,", "\"duration\": 0,", "clips[0].duration", "0 seconds: a duration must be greater than 0")]
    [InlineData("\"name\": \"wave\",", "\"name\": \"\",", "clips[0].name", "a clip name must not be empty")]
    [InlineData("\"loop\": true,", "\"loop\": 1,", "clips[0].loop", "loop is 1, not true or false")]
    [InlineData("{\"time\": 0, \"value\": [0, 1, 0]}", "{\"time\": 0, \"value\": [0, 1e39, 0]}", "clips[0].channels.joint.translation.keys[0].value[1]", "1e39 is not finite: it is outside the range of a float")]
    public void BadClipsAreRefused(string from, string to, string element, string message)
        => Refused(ArmWith(from, to), element, message);

    [Fact]
    public void TwoClipsOfOneNameAreRefused()
    {
        var model = Arm();
        var text = CarveFile.Write(model.WithClips([model.Clips[0], Clip.Create("other", 1, false, [], model.Skeleton!).Clip!]))
            .Replace("\"name\": \"other\"", "\"name\": \"wave\"");
        Refused(text, "clips[1].name", "two clips are named 'wave'; clip names must be unique");
    }

    [Theory]
    [InlineData("{\"colour\": \"#C8503C\", \"faces\": [0]}", "{\"colour\": \"red\", \"faces\": [0]}", "meshes[0].colours[0].colour", "'red' is not a colour: write sRGB hex as #RRGGBB, for example #C8503C")]
    [InlineData("{\"colour\": \"#C8503C\", \"faces\": [0]}", "{\"colour\": \"#C8503C\", \"faces\": [1]}", "meshes[0].colours[0].faces[0]", "there is no face 1: ids run 0..0")]
    [InlineData("{\"colour\": \"#C8503C\", \"faces\": [0]}", "{\"colour\": \"#C8503C\", \"faces\": []}", "meshes[0].colours[0].faces", "nothing to colour: list at least one face")]
    [InlineData("{\"colour\": \"#C8503C\", \"faces\": [0]}", "{\"colour\": \"#C8503C\", \"faces\": [0]},\n        {\"colour\": \"#3C78C8\", \"faces\": [0]}", "meshes[0].colours[1].faces[0]", "face 0 is given a colour earlier; give each face one colour")]
    [InlineData("{\"name\": \"all\", \"faces\": [0]}", "{\"name\": \"\", \"faces\": [0]}", "meshes[0].groups[0].name", "a group name must not be empty")]
    [InlineData("{\"name\": \"all\", \"faces\": [0]}", "{\"name\": \"all\", \"faces\": [-1]}", "meshes[0].groups[0].faces[0]", "there is no face -1: ids run 0..0")]
    [InlineData("{\"name\": \"all\", \"faces\": [0]}", "{\"name\": \"all\", \"faces\": []}", "meshes[0].groups[0].faces", "nothing to group: list at least one face")]
    [InlineData("{\"name\": \"all\", \"faces\": [0]}", "{\"name\": \"all\", \"faces\": [0]},\n        {\"name\": \"all\", \"faces\": [0]}", "meshes[0].groups[1].name", "a group named 'all' comes earlier; group names must be unique")]
    public void BadColoursAndGroupsAreRefused(string from, string to, string element, string message)
        => Refused(ArmWith(from, to), element, message);

    [Theory]
    [InlineData("\"name\": \"arm\",", "\"name\": \"\",", "meshes[0].name", "a mesh name must not be empty")]
    [InlineData("\"name\": \"arm\",", "\"name\": \"arm:left\",", "meshes[0].name", "a mesh name must not contain ':' (a skinned export names materials <mesh>:<group>)")]
    [InlineData("        [0, 1, 2]\n", "        [0, 1, 7]\n", "meshes[0].faces[0][2]", "index 7 is out of range 0..2")]
    [InlineData("\"name\": \"arm\",", "\"nom\": \"arm\",", "meshes[0].nom", "unknown key: a mesh has only the keys \"name\", \"vertices\", \"faces\", \"colours\", \"groups\" and \"weights\"")]
    [InlineData("\"name\": \"arm\",", "\"vertices\": [],", "meshes[0].vertices", "key appears more than once")]
    [InlineData("  \"clips\": [", "  \"uvs\": [],\n  \"clips\": [", "uvs", "unknown key: a version 2 Carve file has only the keys \"carve\", \"meshes\", \"skeleton\" and \"clips\"")]
    [InlineData("{\"vertex\": 0, \"bones\"", "{\"vertex\": 0, \"bone\"", "meshes[0].weights[0].bone", "unknown key: a weighted vertex has only the keys \"vertex\" and \"bones\"")]
    public void BadMeshesAndKeysAreRefused(string from, string to, string element, string message)
        => Refused(ArmWith(from, to), element, message);

    [Fact]
    public void TwoMeshesOfOneNameAreRefused()
        => Refused(CarveFile.Write(Model.Of("a", Cube()).With("b", Cube())).Replace("\"name\": \"b\"", "\"name\": \"a\""),
            "meshes[1].name", "a mesh named 'a' comes earlier; mesh names must be unique");

    [Fact]
    public void AFileWithNoMeshesIsRefused()
        => Refused("{\"carve\": 2, \"meshes\": []}", "meshes", "the file has no meshes; a Carve file holds at least one");

    [Fact]
    public void AMissingMeshNameIsRefused()
        => Refused(ArmWith("      \"name\": \"arm\",\n", ""), "meshes[0].name", "missing: a mesh needs name");

    // ---- One mesh of a model: save's and open's mesh argument -------------------------------------------------------

    [Fact]
    public void APieceWithWeightsCarriesTheSkeletonAndClipsAndOneWithoutDoesNot()
    {
        var model = Arm().With("hat", Cube());
        var arm = model.Piece("arm")!;
        Assert.Equal(["arm"], arm.Names);
        Assert.Same(model.Skeleton, arm.Skeleton);
        Assert.Equal(["wave"], arm.Clips.Select(c => c.Name));
        var hat = model.Piece("hat")!;
        Assert.Equal(["hat"], hat.Names);
        Assert.Null(hat.Skeleton);
        Assert.Empty(hat.Clips);
        Assert.Null(model.Piece("boot"));
        AssertSameModel(arm, RoundTrip(arm));
    }

    [Fact]
    public void AMeshTakenFromAFileHasItsWeightsMovedOntoTheModelsBonesByName()
    {
        // The open model's skeleton has the same bones in another order, and one more.
        var skeleton = Skeleton.Create(
        [
            new BoneSpec("root", null, Vector3.Zero, Quaternion.Identity),
            new BoneSpec("joint", "root", Vector3.UnitY, Quaternion.Identity),
            new BoneSpec("base", null, Vector3.Zero, Quaternion.Identity),
        ]).Skeleton!;
        var open = Model.Of("hat", Cube()).WithSkeleton(skeleton);
        var file = CarveFile.Read(ArmText).Model!;

        var next = open.WithMeshFrom(file, "arm", out var problem)!;
        Assert.Null(problem);
        Assert.Equal(["hat", "arm"], next.Names);
        Assert.Same(skeleton, next.Skeleton);
        Assert.Empty(next.Clips);
        var weights = next.Find("arm")!.Weights!;
        Assert.Equal([new Influence(2, 1)], weights.Of(0));
        Assert.Equal([new Influence(1, 0.75), new Influence(2, 0.25)], weights.Of(2));
        Assert.Equal("#C8503C", next.Find("arm")!.Style!.ColourOf(0));
    }

    [Fact]
    public void AMeshTakenFromAFileIsRefusedWhenTheModelLacksItsBonesOrTheFileLacksTheMesh()
    {
        var file = CarveFile.Read(ArmText).Model!;
        var other = Skeleton.Create([new BoneSpec("base", null, Vector3.Zero, Quaternion.Identity)]).Skeleton!;
        var open = Model.Of("hat", Cube()).WithSkeleton(other);

        Assert.Null(open.WithMeshFrom(file, "arm", out var problem));
        Assert.Equal("mesh: the file's mesh 'arm' has weights on bones the model's skeleton does not have: 'joint'; the model's bones are 'base'", problem);
        Assert.Null(Model.Of("hat", Cube()).WithMeshFrom(file, "arm", out problem));
        Assert.Contains("the model has no skeleton", problem);
        Assert.Null(open.WithMeshFrom(file, "hat", out problem));
        Assert.Equal("mesh: the file has no mesh named 'hat'; its meshes are 'arm'", problem);

        var unweighted = CarveFile.Read(CarveFile.Write(Cube())).Model!;
        Assert.Equal(["hat", "mesh"], Model.Of("hat", Cube()).WithMeshFrom(unweighted, "mesh", out _)!.Names);
    }
}
