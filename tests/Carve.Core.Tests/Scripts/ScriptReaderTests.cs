using System.Text;
using Carve.Core.Scripts;
using Xunit;

namespace Carve.Core.Tests.Scripts;

/// <summary>T-230: a <c>.carvescript</c> reads to its statements, each with the line it starts on; a bad one is refused by line and column.</summary>
[Trait("Tier", "unit")]
public class ScriptReaderTests
{
    private static CarveScript Script(string text)
    {
        var (script, error) = ScriptReader.Read(text);
        Assert.Null(error);
        return script!;
    }

    [Fact]
    public void A_statement_is_a_tool_with_named_json_arguments_in_order()
    {
        var s = Assert.Single(Script("extrude(mesh: \"wall\", faces: [5, 9], distance: 0.3)\n").Statements);
        Assert.Equal("extrude", s.Tool);
        Assert.Equal(["mesh", "faces", "distance"], s.Arguments.Select(a => a.Name));
        Assert.Equal(["\"wall\"", "[5, 9]", "0.3"], s.Arguments.Select(a => a.Value.GetRawText()));
        Assert.Equal(1, s.Line);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  \n\r\n\t ")]
    [InlineData("\uFEFF")]
    public void Empty_or_blank_text_is_a_script_of_no_statements(string text) => Assert.Empty(Script(text).Statements);

    [Fact]
    public void Whitespace_and_newlines_may_stand_between_any_tokens_and_crlf_reads()
    {
        var script = Script("\uFEFF\r\nclear()\r\n  loop_cut (\r\n mesh\t:\r\n \"wall\" ,\r\n\r\n edge: 12\r\n )  \r\nf( )");
        Assert.Equal(["clear", "loop_cut", "f"], script.Statements.Select(s => s.Tool));
        Assert.Equal([2, 3, 9], script.Statements.Select(s => s.Line));
        Assert.Equal(["mesh", "edge"], script.Statements[1].Arguments.Select(a => a.Name));
    }

    [Fact]
    public void A_statement_after_a_40_line_define_mesh_is_on_its_own_line()
    {
        var text = new StringBuilder("define_mesh(\n  name: \"wall\",\n  vertices: [\n");
        for (var i = 0; i < 34; i++)
            text.Append($"    [{i}, 0, 0],\n");
        text.Append("    [9, 9, 9]\n  ]\n)\n"); // lines 1..40
        var good = Script(text + "loop_cut(mesh: \"wall\", edge: 12)\n");
        Assert.Equal([1, 41], good.Statements.Select(s => s.Line));

        var (_, error) = ScriptReader.Read(text + "loop_cut(mesh: \"wall\", mesh: \"x\")\n");
        Assert.Equal(new ScriptError(41, 24, "the argument 'mesh' is given twice in 'loop_cut'"), error);
    }

    public static TheoryData<string, int, int, string> Errors => new()
    {
        // an unknown character
        { "clear()\n#note", 2, 1, "unknown character '#': expected a tool name" },
        { "f(a: 1);", 1, 8, "unknown character ';': expected a newline after the ')' that ends 'f'" },
        { "f(a\u00a0: 1)", 1, 4, "unknown character '\u00a0': expected ':' after the argument name 'a'" },
        // a name that is not an identifier
        { "Loop_cut(edge: 1)", 1, 1, "'Loop_cut' is not a tool name: a name is lower-case letters, digits and '_', and does not start with a digit" },
        { "f(2d: 1)", 1, 3, "'2d' is not an argument name: a name is lower-case letters, digits and '_', and does not start with a digit" },
        { "f(\"a\": 1)", 1, 3, "expected an argument name" },
        // a missing '(', ':', ',' or ')'
        { "loop_cut mesh: 1)", 1, 10, "expected '(' after the tool name 'loop_cut'" },
        { "define_mesh(\n  name: \"w\",\n  vertices: [],\n  faces          [[0, 1, 2]]\n)", 4, 18, "expected ':' after the argument name 'faces'" },
        { "f(a: 1 b: 2)", 1, 8, "expected ',' or ')' after the value of 'a'" },
        { "f(a: 1", 1, 7, "expected ',' or ')' after the value of 'a', found the end of the file" },
        { "f(", 1, 3, "expected an argument name, found the end of the file" },
        // a value that is not JSON, the JSON reader's message at the script's line and column
        { "f(a: [1, 2,])", 1, 12, "the value of 'a' is not JSON: The JSON array contains a trailing comma at the end which is not supported in this mode. Change the reader options." },
        { "f(a: [\n  1,\n  tru\n])", 3, 6, "the value of 'a' is not JSON: 'tru ])' is an invalid JSON literal. Expected the literal 'true'." },
        { "f(a: [1,\n 2, 3x])", 2, 6, "the value of 'a' is not JSON: 'x' is an invalid end of a number. Expected a delimiter." },
        { "f(s: \"\u00e9\u00e9\", b: 'x')", 1, 15, "unknown character ''': expected a JSON value for 'b'" },
        { "f(a: )", 1, 6, "expected a JSON value for 'a'" },
        // a repeated argument name in one statement
        { "f(a: 1, a: 2)", 1, 9, "the argument 'a' is given twice in 'f'" },
        // two statements on one line
        { "f() g()", 1, 5, "a second statement on the line of 'f': statements are separated by a newline" },
        // text after the last ')'
        { "f(a: 1))", 1, 8, "expected a newline after the ')' that ends 'f'" },
        { "f()\n)", 2, 1, "expected a tool name" },
        // what the grammar does not settle is refused: a trailing comma, a break before '('
        { "f(a: 1,)", 1, 8, "expected an argument name after ',' in 'f': a trailing comma is not allowed" },
        { "f\n(a: 1)", 1, 2, "expected '(' on the line of the tool name 'f': a statement may not break before its '('" },
        // the column counts characters, not bytes, and CRLF does not move it
        { "f(s: \"\u00e9\u00e9\u00e9\" b: 1)", 1, 12, "expected ',' or ')' after the value of 's'" },
        { "f\r\n(a: 1)", 1, 2, "expected '(' on the line of the tool name 'f': a statement may not break before its '('" },
        { "f()\r\ng(b 2)", 2, 5, "expected ':' after the argument name 'b'" },
        { "\uFEFF#", 1, 1, "unknown character '#': expected a tool name" },
    };

    [Theory]
    [MemberData(nameof(Errors))]
    public void A_bad_script_is_refused_with_its_line_column_and_what_was_expected(string text, int line, int column, string message)
    {
        var (script, error) = ScriptReader.Read(text);
        Assert.Null(script);
        Assert.Equal(new ScriptError(line, column, message), error);
    }

    [Fact]
    public void An_error_reads_as_line_column_and_message() =>
        Assert.Equal("line 4, column 18: expected ':' after the argument name 'faces'", new ScriptError(4, 18, "expected ':' after the argument name 'faces'").ToString());
}
