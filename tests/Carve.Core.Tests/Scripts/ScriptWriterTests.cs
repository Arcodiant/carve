using System.Globalization;
using System.Text;
using System.Text.Json;
using Carve.Core.Scripts;
using Xunit;

namespace Carve.Core.Tests.Scripts;

/// <summary>T-230: a script written reads back to the same statements, every number to the bit; canonical text is written as it was.</summary>
[Trait("Tier", "unit")]
public class ScriptWriterTests
{
    private static JsonElement J(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static ScriptStatement S(string tool, params (string, string)[] arguments) =>
        new(tool, arguments.Select(a => (a.Item1, J(a.Item2))).ToList());

    private static CarveScript Read(string text)
    {
        var (script, error) = ScriptReader.Read(text);
        Assert.True(error is null, error?.ToString());
        return script!;
    }

    // Equal as values: numbers by their raw token, strings by their characters, lists and objects item by item in order.
    private static void AssertSame(JsonElement expected, JsonElement actual, string path)
    {
        Assert.True(expected.ValueKind == actual.ValueKind, $"{path}: {expected.ValueKind} became {actual.ValueKind}");
        switch (expected.ValueKind)
        {
            case JsonValueKind.Number:
                Assert.True(expected.GetRawText() == actual.GetRawText(), $"{path}: {expected.GetRawText()} became {actual.GetRawText()}");
                break;
            case JsonValueKind.String:
                Assert.Equal(expected.GetString(), actual.GetString());
                break;
            case JsonValueKind.Array:
                var (e, a) = (expected.EnumerateArray().ToList(), actual.EnumerateArray().ToList());
                Assert.Equal(e.Count, a.Count);
                for (var i = 0; i < e.Count; i++)
                    AssertSame(e[i], a[i], $"{path}[{i}]");
                break;
            case JsonValueKind.Object:
                var (eo, ao) = (expected.EnumerateObject().ToList(), actual.EnumerateObject().ToList());
                Assert.Equal(eo.Select(p => p.Name), ao.Select(p => p.Name));
                for (var i = 0; i < eo.Count; i++)
                    AssertSame(eo[i].Value, ao[i].Value, $"{path}.{eo[i].Name}");
                break;
        }
    }

    private static void AssertRoundTrip(params ScriptStatement[] statements)
    {
        var back = Read(ScriptWriter.Write(new CarveScript(statements))).Statements;
        Assert.Equal(statements.Length, back.Count);
        for (var s = 0; s < statements.Length; s++)
        {
            Assert.Equal(statements[s].Tool, back[s].Tool);
            Assert.Equal(statements[s].Arguments.Select(a => a.Name), back[s].Arguments.Select(a => a.Name));
            for (var i = 0; i < statements[s].Arguments.Count; i++)
                AssertSame(statements[s].Arguments[i].Value, back[s].Arguments[i].Value, $"{statements[s].Tool}.{statements[s].Arguments[i].Name}");
        }
    }

    internal static readonly string[] AwkwardNumbers = ["0.1", "0.30000000000000004", "-0", "5e-324", "1E308", "3.0", "-1.5e+10", "123456789012345678901234567890"];

    public static TheoryData<string, string[]> Statements => new()
    {
        { "no arguments", [] },
        { "every JSON kind", ["n", "1", "s", "\"wall\"", "t", "true", "f", "false", "z", "null", "l", "[1, 2]", "o", "{\"k\": [1, {\"m\": null}], \"e\": {}}", "e", "[]"] },
        { "nested lists", ["faces", "[[0, 1, 2], [[3], [4, [5]]], []]"] },
        { "unicode and escaped strings", ["a", "\"\u00e9\"", "b", "\"\\u00e9\"", "c", "\"\\n\"", "d", "\"\\\"\"", "e", "\"tab\\there \\\\ \\u0001 \u4e2d\u6587 \ud83d\ude00\""] },
        { "awkward numbers", AwkwardNumbers.SelectMany((n, i) => new[] { $"n{i}", n }).Append("all").Append($"[{string.Join(", ", AwkwardNumbers)}]").ToArray() },
        { "argument named like a JSON keyword", ["null", "1", "true", "false", "false", "null"] },
    };

    [Theory]
    [MemberData(nameof(Statements))]
    public void A_written_statement_reads_back_the_same(string tool, string[] pairs)
    {
        var arguments = pairs.Chunk(2).Select(p => (p[0], p[1])).ToArray();
        AssertRoundTrip(S(tool.Replace(' ', '_').Replace("JSON", "json"), arguments));
        AssertRoundTrip(S("f", arguments), S("clear"), S("g", arguments)); // and among others
    }

    private static ScriptStatement Mesh(int n)
    {
        var vertices = string.Join(", ", Enumerable.Range(0, n).Select(i => FormattableString.Invariant($"[{i * 0.1}, {-i / 3.0}, {i}e-3]")));
        var faces = string.Join(", ", Enumerable.Range(0, n - 2).Select(i => $"[{i}, {i + 1}, {i + 2}]"));
        return S("define_mesh", ("name", "\"wall\""), ("vertices", $"[{vertices}]"), ("faces", $"[{faces}]"));
    }

    [Fact]
    public void A_2000_vertex_define_mesh_reads_back_the_same_one_vertex_a_line()
    {
        var mesh = Mesh(2000);
        AssertRoundTrip(mesh, S("loop_cut", ("mesh", "\"wall\""), ("edge", "12"), ("cuts", "2")));
        var text = ScriptWriter.Write(new CarveScript([mesh]));
        Assert.StartsWith("define_mesh(\n  name: \"wall\",\n  vertices: [\n    [0, 0, 0e-3],\n    [0.1, -0.3333333333333333, 1e-3],\n", text);
        Assert.Contains("    [1997, 1998, 1999]\n  ]\n)\n", text);
        Assert.Equal(2 + 2000 + 2 + 1998 + 2 + 1, text.Count(c => c == '\n'));
    }

    [Fact]
    public void Numbers_come_back_as_written_and_to_the_same_double()
    {
        var statement = S("move", AwkwardNumbers.Select((n, i) => ($"n{i}", n)).ToArray());
        foreach (var wrapped in new[] { false, true })
        {
            var written = wrapped ? S("move", [.. statement.Arguments.Select(a => (a.Name, a.Value.GetRawText())), ("pad", $"\"{new string('x', 120)}\"")]) : statement;
            var back = Read(ScriptWriter.Write(new CarveScript([written]))).Statements[0];
            for (var i = 0; i < AwkwardNumbers.Length; i++)
            {
                Assert.Equal(AwkwardNumbers[i], back.Arguments[i].Value.GetRawText());
                Assert.Equal(BitConverter.DoubleToInt64Bits(double.Parse(AwkwardNumbers[i], CultureInfo.InvariantCulture)), BitConverter.DoubleToInt64Bits(back.Arguments[i].Value.GetDouble()));
            }
        }
    }

    [Fact]
    public void Canonical_text_is_written_back_as_it_was()
    {
        var text =
            "clear()\n" +
            "open(path: \"slab.carve\")\n" +
            "loop_cut(mesh: \"wall\", edge: 12, cuts: 2)\n" +
            "extrude(mesh: \"wall\", faces: [5, 9], distance: 0.3)\n" +
            "set_colour(mesh: \"wall\", faces: [40, 41], colour: \"#6b4a2f\", note: \"caf\u00e9 \\\"x\\\"\\n\", spec: {\"a\": [1, 2], \"b\": null})\n" +
            "define_mesh(\n" +
            "  name: \"wall\",\n" +
            "  vertices: [\n" +
            "    [0, 0, 0],\n" +
            "    [4, 0, 0],\n" +
            "    [4, 3, 0],\n" +
            "    [0, 3, 0],\n" +
            "    null\n" +
            "  ],\n" +
            "  faces: [\n" +
            "    [0, 3, 2, 1]\n" +
            "  ],\n" +
            "  flags: [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33]\n" +
            ")\n";
        Assert.Equal(text, ScriptWriter.Write(Read(text)));
    }

    [Fact]
    public void A_hand_written_script_is_written_in_the_canonical_form()
    {
        var hand = "\uFEFF  clear( )\r\n\r\nloop_cut( mesh :\"wall\" ,edge:12,\r\n   cuts:  2\r\n)\r\nset_colour(faces:[ 40,41 ],colour:\"#6b4a2f\", o: { \"k\" :true,\"l\":[ ] })";
        Assert.Equal(
            "clear()\n" +
            "loop_cut(mesh: \"wall\", edge: 12, cuts: 2)\n" +
            "set_colour(faces: [40, 41], colour: \"#6b4a2f\", o: {\"k\": true, \"l\": []})\n",
            ScriptWriter.Write(Read(hand)));
    }

    [Fact]
    public void A_statement_of_120_characters_stays_on_one_line_and_one_of_121_wraps()
    {
        var at120 = S("f", ("s", $"\"{new string('x', 112)}\""));
        Assert.Equal(120, ScriptWriter.Write(at120).Length);
        var at121 = S("f", ("s", $"\"{new string('x', 113)}\""));
        Assert.Equal($"f(\n  s: \"{new string('x', 113)}\"\n)", ScriptWriter.Write(at121));
        Assert.Equal("", ScriptWriter.Write(new CarveScript([])));
    }

    [Theory]
    [InlineData("Loop_cut", "a")]
    [InlineData("f", "base-colour")]
    [InlineData("f", "")]
    public void A_name_that_would_not_read_back_is_refused(string tool, string argument) =>
        Assert.Throws<ArgumentException>(() => ScriptWriter.Write(S(tool, (argument, "1"))));

    [Fact]
    public void A_repeated_argument_is_refused() =>
        Assert.Throws<ArgumentException>(() => ScriptWriter.Write(S("f", ("a", "1"), ("a", "2"))));
}
