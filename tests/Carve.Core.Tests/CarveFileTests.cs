using System.Globalization;
using System.Numerics;
using Xunit;
using static Carve.Core.Tests.TestMeshes;

namespace Carve.Core.Tests;

/// <summary>The Carve file: a mesh written and read back is the same mesh, bit for bit and id for id; a bad file is refused by element.</summary>
[Trait("Tier", "unit")]
public class CarveFileTests
{
    /// <summary>The cube's version 1 file, T-15's worked example: <c>CarveFile.Write(Cube())</c> as it was before version 2. Version 1 still reads.</summary>
    internal const string CubeText =
        "{\n" +
        "  \"carve\": 1,\n" +
        "  \"vertices\": [\n" +
        "    [0, 0, 0],\n" +
        "    [1, 0, 0],\n" +
        "    [1, 1, 0],\n" +
        "    [0, 1, 0],\n" +
        "    [0, 0, 1],\n" +
        "    [1, 0, 1],\n" +
        "    [1, 1, 1],\n" +
        "    [0, 1, 1]\n" +
        "  ],\n" +
        "  \"faces\": [\n" +
        "    [0, 3, 2, 1],\n" +
        "    [4, 5, 6, 7],\n" +
        "    [0, 1, 5, 4],\n" +
        "    [3, 7, 6, 2],\n" +
        "    [0, 4, 7, 3],\n" +
        "    [1, 2, 6, 5]\n" +
        "  ]\n" +
        "}\n";

    private static PolygonMesh RoundTrip(PolygonMesh mesh)
    {
        var result = CarveFile.Read(CarveFile.Write(mesh));
        Assert.True(result.Succeeded, string.Join("; ", result.Errors));
        return result.Mesh!;
    }

    /// <summary>Same counts, every coordinate equal bit for bit, every face loop equal: no tolerance.</summary>
    private static void AssertSame(PolygonMesh expected, PolygonMesh actual)
    {
        Assert.Equal(expected.Vertices.Count, actual.Vertices.Count);
        Assert.Equal(expected.Faces.Count, actual.Faces.Count);
        for (var i = 0; i < expected.Vertices.Count; i++)
        {
            Assert.Equal(Bits(expected.Vertices[i]), Bits(actual.Vertices[i]));
        }
        for (var f = 0; f < expected.Faces.Count; f++)
            Assert.Equal(expected.Faces[f], actual.Faces[f]);
    }

    private static (int, int, int) Bits(Vector3 v)
        => (BitConverter.SingleToInt32Bits(v.X), BitConverter.SingleToInt32Bits(v.Y), BitConverter.SingleToInt32Bits(v.Z));

    [Fact]
    public void TheCubeRoundTrips() => AssertSame(Cube(), RoundTrip(Cube()));

    [Fact]
    public void TheLBlockRoundTrips() => AssertSame(LBlock(), RoundTrip(LBlock()));

    [Fact]
    public void TheCrateRoundTrips()
    {
        var crate = TestMeshes.Crate();
        AssertSame(crate, RoundTrip(crate));
    }

    /// <summary>The awkward floats, by bit pattern so <c>-0</c> cannot be folded to <c>0</c> on the way in.</summary>
    public static TheoryData<int> AwkwardFloats => new()
    {
        BitConverter.SingleToInt32Bits(0.1f),
        BitConverter.SingleToInt32Bits(1.0199999f),
        BitConverter.SingleToInt32Bits(-0f),
        BitConverter.SingleToInt32Bits(float.Epsilon),
        BitConverter.SingleToInt32Bits(1e-30f),
        BitConverter.SingleToInt32Bits(3.4e38f),
    };

    /// <summary>A unit triangle with one unused vertex at (x, x, x): an unused vertex is a warning, so any finite x is a legal file.</summary>
    private static PolygonMesh TriangleWith(float x)
        => Make([new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(x, x, x)], [[0, 1, 2]]);

    [Theory]
    [MemberData(nameof(AwkwardFloats))]
    public void AnAwkwardFloatRoundTripsBitForBit(int bits)
    {
        var x = BitConverter.Int32BitsToSingle(bits);
        var mesh = TriangleWith(x);
        var back = RoundTrip(mesh);
        AssertSame(mesh, back);
        Assert.Equal(bits, BitConverter.SingleToInt32Bits(back.Vertices[3].X));
    }

    [Fact]
    public void MinusZeroIsWrittenAsMinusZero()
    {
        var text = CarveFile.Write(TriangleWith(-0f));
        Assert.Contains("    [-0, -0, -0]\n", text);
    }

    /// <summary>The writer's format, "R", against the .NET default: both are the shortest round trip, and agree on every value here.</summary>
    [Theory]
    [MemberData(nameof(AwkwardFloats))]
    public void RoundTripFormatIsShortestAndExact(int bits)
    {
        var x = BitConverter.Int32BitsToSingle(bits);
        var r = x.ToString("R", CultureInfo.InvariantCulture);
        Assert.Equal(x.ToString(CultureInfo.InvariantCulture), r);
        Assert.Equal(bits, BitConverter.SingleToInt32Bits(float.Parse(r, NumberStyles.Float, CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void ShortFloatsStayShort()
    {
        var text = CarveFile.Write(TriangleWith(0.12f));
        Assert.Contains("    [0.12, 0.12, 0.12]\n", text);
        Assert.Contains("    [1, 0, 0],\n", text);
    }

    [Fact]
    public void WriteIsStable()
    {
        Assert.Equal(CarveFile.Write(TestMeshes.Crate()), CarveFile.Write(TestMeshes.Crate()));
        Assert.Equal(CarveFileV2Tests.CubeText, CarveFile.Write(Cube()));
    }

    [Fact]
    public void TheWorkedExampleReads() => AssertSame(Cube(), Read(CubeText).Mesh!);

    [Fact]
    public void AnOpenBoxReadsBackWithItsWarnings()
    {
        var open = Make(CubeVertices, CubeFaces.Skip(1));
        var expected = MeshReport.Create(open).Warnings;
        Assert.NotEmpty(expected);
        var result = Read(CarveFile.Write(open));
        Assert.True(result.Succeeded, string.Join("; ", result.Errors));
        Assert.Equal(expected, result.Warnings);
    }

    [Fact]
    public void ACleanMeshReadsBackWithNoWarnings() => Assert.Empty(Read(CubeText).Warnings);

    private static CarveFileResult Read(string text) => CarveFile.Read(text);

    /// <summary>The cube's file with one line replaced, for refusals.</summary>
    private static string CubeWith(string line, string replacement)
    {
        Assert.Contains(line, CubeText);
        return CubeText.Replace(line, replacement);
    }

    /// <summary>The file is refused, with exactly one error, on <paramref name="element"/>; returns its message.</summary>
    private static string Refused(string text, string element)
    {
        var result = Read(text);
        Assert.False(result.Succeeded);
        Assert.Null(result.Mesh);
        Assert.Empty(result.Warnings);
        var error = Assert.Single(result.Errors);
        Assert.Equal(element, error.Element);
        return error.Message;
    }

    [Fact]
    public void TextThatIsNotJsonIsRefusedWithItsLineAndColumn()
    {
        var message = Refused(CubeWith("    [1, 0, 0],\n", "    [1, 0, 0],,\n"), "file");
        Assert.StartsWith("the text is not JSON: at line 5, column 15: ", message);
    }

    [Fact]
    public void ATopLevelThatIsNotAnObjectIsRefused()
        => Assert.Equal("the file is a JSON array; a Carve file is a JSON object", Refused("[1, 2]", "file"));

    [Fact]
    public void AMissingVersionIsRefused()
        => Assert.Equal("missing: the file has no format version; this build reads versions 1, 2, 3, 4, 5, 6, 7, 8, 9 and 10",
            Refused(CubeWith("  \"carve\": 1,\n", ""), "carve"));

    [Theory]
    [InlineData("1.5", "the format version is 1.5, not an integer; this build reads versions 1, 2, 3, 4, 5, 6, 7, 8, 9 and 10")]
    [InlineData("\"1\"", "the format version is a string, not an integer; this build reads versions 1, 2, 3, 4, 5, 6, 7, 8, 9 and 10")]
    public void ANonIntegerVersionIsRefused(string version, string expected)
        => Assert.Equal(expected, Refused(CubeWith("\"carve\": 1,", $"\"carve\": {version},"), "carve"));

    [Theory]
    [InlineData(11)]
    [InlineData(0)]
    public void AnotherVersionIsRefused(int version)
        => Assert.Equal($"version {version} is not one this build reads; it reads versions 1, 2, 3, 4, 5, 6, 7, 8, 9 and 10",
            Refused(CubeWith("\"carve\": 1,", $"\"carve\": {version},"), "carve"));

    /// <summary>A newer file is refused on its version alone, even when its other keys would also be refused.</summary>
    [Fact]
    public void AnotherVersionIsRefusedBeforeItsKeys()
        => Refused(CubeWith("\"carve\": 1,", "\"carve\": 11, \"uvs\": [],"), "carve");

    [Fact]
    public void MissingVerticesAreRefused()
    {
        var text = "{\n  \"carve\": 1,\n  \"faces\": [[0, 1, 2]]\n}\n";
        Assert.Equal("missing: the file has no vertices array", Refused(text, "vertices"));
    }

    [Fact]
    public void MissingFacesAreRefused()
    {
        var text = "{\n  \"carve\": 1,\n  \"vertices\": [[0, 0, 0], [1, 0, 0], [0, 1, 0]]\n}\n";
        Assert.Equal("missing: the file has no faces array", Refused(text, "faces"));
    }

    [Fact]
    public void VerticesThatAreNotAnArrayAreRefused()
        => Assert.Equal("vertices is an object, not an array",
            Refused("{\"carve\": 1, \"vertices\": {}, \"faces\": [[0, 1, 2]]}", "vertices"));

    [Fact]
    public void FacesThatAreNotAnArrayAreRefused()
        => Assert.Equal("faces is 3, not an array",
            Refused("{\"carve\": 1, \"vertices\": [[0, 0, 0], [1, 0, 0], [0, 1, 0]], \"faces\": 3}", "faces"));

    [Theory]
    [InlineData("[1, 0]", "vertex has 2 coordinates; a vertex is [x, y, z]")]
    [InlineData("[1, 0, 0, 0]", "vertex has 4 coordinates; a vertex is [x, y, z]")]
    [InlineData("\"1, 0, 0\"", "vertex is a string; a vertex is [x, y, z]")]
    public void AVertexThatIsNotThreeNumbersIsRefused(string vertex, string expected)
        => Assert.Equal(expected, Refused(CubeWith("    [1, 0, 0],\n", $"    {vertex},\n"), "vertices[1]"));

    [Theory]
    [InlineData("[1, null, 0]", "vertices[1][1]", "coordinate is null, not a number")]
    [InlineData("[1, 0, \"0\"]", "vertices[1][2]", "coordinate is a string, not a number")]
    [InlineData("[1e39, 0, 0]", "vertices[1][0]", "coordinate 1e39 is not finite: it is outside the range of a float")]
    public void ACoordinateThatIsNotAFiniteNumberIsRefused(string vertex, string element, string expected)
        => Assert.Equal(expected, Refused(CubeWith("    [1, 0, 0],\n", $"    {vertex},\n"), element));

    [Fact]
    public void AFaceThatIsNotAnArrayIsRefused()
        => Assert.Equal("face is 7; a face is an array of vertex ids",
            Refused(CubeWith("    [3, 7, 6, 2],\n", "    7,\n"), "faces[3]"));

    [Theory]
    [InlineData("[3, 7.5, 6, 2]", "faces[3][1]", "index is 7.5, not an integer vertex id")]
    [InlineData("[3, 7, \"6\", 2]", "faces[3][2]", "index is a string, not an integer vertex id")]
    [InlineData("[3, 7, 6, 4294967298]", "faces[3][3]", "index is 4294967298, not an integer vertex id")]
    public void AFaceIndexThatIsNotAnIntegerIsRefused(string face, string element, string expected)
        => Assert.Equal(expected, Refused(CubeWith("    [3, 7, 6, 2],\n", $"    {face},\n"), element));

    [Theory]
    [InlineData("{\"\\ud800\": 1}", "keys[0]")]
    [InlineData("{\"carve\": 1, \"vertices\": [], \"faces\": [], \"\\udc00x\": 1}", "keys[3]")]
    [InlineData("{\"\\udbff\\ud800\": 1}", "keys[0]")]
    public void ALoneSurrogateEscapeInAKeyIsRefusedByItsPlace(string text, string element)
    {
        var result = Read(text);

        Assert.False(result.Succeeded);
        var error = Assert.Single(result.Errors, e => e.Element == element);
        Assert.Contains("lone surrogate", error.Message);
    }

    [Fact]
    public void AValidSurrogatePairInAKeyIsAnUnknownKeyNotACrash()
    {
        var result = Read(CubeText.Replace("  \"carve\": 1,\n", "  \"carve\": 1,\n  \"\\ud83d\\ude00\": 1,\n"));

        Assert.Contains(result.Errors, e => e.Element == "\U0001F600" && e.Message.StartsWith("unknown key"));
    }

    [Fact]
    public void AnUnknownKeyIsRefused()
        => Assert.Equal("unknown key: a version 1 Carve file has only the keys \"carve\", \"vertices\" and \"faces\"",
            Refused(CubeWith("  \"carve\": 1,\n", "  \"carve\": 1,\n  \"uvs\": [],\n"), "uvs"));

    [Fact]
    public void ARepeatedKeyIsRefused()
        => Assert.Equal("key appears more than once",
            Refused(CubeWith("  \"carve\": 1,\n", "  \"carve\": 1,\n  \"carve\": 1,\n"), "carve"));

    /// <summary>The geometry is held to what <c>define_mesh</c> accepts: <see cref="PolygonMesh.Create"/>'s errors, by element.</summary>
    [Fact]
    public void CreateErrorsPassThroughWithTheirElements()
    {
        var text = CubeWith("    [3, 7, 6, 2],\n", "    [3, 7, 6, 8],\n");
        var expected = PolygonMesh.Create(CubeVertices, CubeFaces.Select((f, i) => i == 3 ? new[] { 3, 7, 6, 8 } : f)).Errors;
        Assert.NotEmpty(expected);
        var result = Read(text);
        Assert.Null(result.Mesh);
        Assert.Equal(expected, result.Errors);
        Assert.Equal("faces[3][3]", result.Errors[0].Element);
    }

    [Fact]
    public void AFileWithNoFacesGetsCreatesError()
    {
        var result = Read("{\"carve\": 1, \"vertices\": [], \"faces\": []}");
        var error = Assert.Single(result.Errors);
        Assert.Equal(new MeshError("faces", "mesh has no faces"), error);
    }

    /// <summary>Every problem in a file is named at once, not only the first.</summary>
    [Fact]
    public void EveryErrorIsNamed()
    {
        var text = "{\"carve\": 1, \"extra\": 0, \"vertices\": [[0, 0], [0, 0, 0]], \"faces\": [[0, \"x\"]]}";
        Assert.Equal(["extra", "vertices[0]", "faces[0][1]"], Read(text).Errors.Select(e => e.Element));
    }
}
