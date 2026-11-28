using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Carve.Core.Scripts;

/// <summary>
/// Writes a script as text that <see cref="ScriptReader"/> reads back to the same statements, every number to the bit:
/// a number is copied as its raw token, never through <see cref="double"/>.
/// </summary>
public static class ScriptWriter
{
    /// <summary>A statement whose one-line form is this many characters or less is written on one line.</summary>
    public const int Width = 120;

    // The Carve file's own string escaping (CarveFile.StringOptions): non-ASCII left as it is.
    private static readonly JsonSerializerOptions StringOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>The script's text: one statement after another, LF line ends and a final newline; empty for no statements.</summary>
    /// <exception cref="ArgumentException">A tool or argument name is not a name (<c>[a-z_][a-z0-9_]*</c>), or one statement repeats an argument, so the text would not read back.</exception>
    public static string Write(CarveScript script)
    {
        ArgumentNullException.ThrowIfNull(script);
        var text = new StringBuilder();
        foreach (var statement in script.Statements)
            text.Append(Write(statement)).Append('\n');
        return text.ToString();
    }

    /// <summary>
    /// One statement, with no line end: <c>tool(a: 1, b: [1, 2])</c> when that is <see cref="Width"/> characters or less, otherwise one
    /// argument a line indented two spaces, and a list whose items include a list or object one item a line indented four.
    /// </summary>
    public static string Write(ScriptStatement statement)
    {
        ArgumentNullException.ThrowIfNull(statement);
        Check(statement);
        var line = $"{statement.Tool}({string.Join(", ", statement.Arguments.Select(a => $"{a.Name}: {Compact(a.Value)}"))})";
        if (line.Length <= Width)
            return line;
        var text = new StringBuilder(statement.Tool).Append("(\n");
        for (var i = 0; i < statement.Arguments.Count; i++)
        {
            var (name, value) = statement.Arguments[i];
            text.Append("  ").Append(name).Append(": ").Append(Wrapped(value)).Append(i < statement.Arguments.Count - 1 ? ",\n" : "\n");
        }
        return text.Append(')').ToString();
    }

    private static void Check(ScriptStatement statement)
    {
        if (!ScriptReader.IsName(statement.Tool))
            throw new ArgumentException($"'{statement.Tool}' is not a tool name", nameof(statement));
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (name, _) in statement.Arguments)
        {
            if (!ScriptReader.IsName(name))
                throw new ArgumentException($"'{name}' in '{statement.Tool}' is not an argument name", nameof(statement));
            if (!seen.Add(name))
                throw new ArgumentException($"the argument '{name}' is given twice in '{statement.Tool}'", nameof(statement));
        }
    }

    private static string Wrapped(JsonElement value) =>
        value.ValueKind == JsonValueKind.Array && value.EnumerateArray().Any(i => i.ValueKind is JsonValueKind.Array or JsonValueKind.Object)
            ? "[\n" + string.Join(",\n", value.EnumerateArray().Select(i => "    " + Compact(i))) + "\n  ]"
            : Compact(value);

    /// <summary>A JSON value on one line with <c>, </c> and <c>: </c> between items; numbers, <c>true</c>, <c>false</c> and <c>null</c> as their raw tokens.</summary>
    private static string Compact(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Array => "[" + string.Join(", ", value.EnumerateArray().Select(Compact)) + "]",
        JsonValueKind.Object => "{" + string.Join(", ", value.EnumerateObject().Select(p => Text(p.Name) + ": " + Compact(p.Value))) + "}",
        JsonValueKind.String => Text(value.GetString()!),
        _ => value.GetRawText(),
    };

    private static string Text(string s) => JsonSerializer.Serialize(s, StringOptions);
}
