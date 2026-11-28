using System.Text;
using System.Text.Json;

namespace Carve.Core.Scripts;

/// <summary>
/// Reads a <c>.carvescript</c> (D-8, <i>The statement format</i>): statements separated by newlines, each
/// <c>tool(name: &lt;json&gt;, ...)</c>, whitespace allowed between any two tokens. A UTF-8 BOM is skipped; CRLF and LF both read.
/// </summary>
public static class ScriptReader
{
    /// <summary>The statements, or the first error, by line and column; exactly one of the two is null. Empty or blank text is a script of no statements.</summary>
    public static (CarveScript? Script, ScriptError? Error) Read(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.StartsWith('﻿'))
            text = text[1..];
        try
        {
            return (new Parser(Encoding.UTF8.GetBytes(text)).Script(), null);
        }
        catch (Refusal r)
        {
            return (null, r.Error);
        }
    }

    /// <summary>A tool or argument name: <c>[a-z_][a-z0-9_]*</c>.</summary>
    public static bool IsName(string s) =>
        s.Length > 0 && (s[0] is (>= 'a' and <= 'z') or '_') && s.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_');

    private sealed class Refusal(ScriptError error) : Exception(error.ToString())
    {
        public ScriptError Error { get; } = error;
    }

    // Works on the UTF-8 bytes, which the JSON reader takes; every token of the grammar is ASCII. Columns are counted back in chars.
    private sealed class Parser(byte[] b)
    {
        private readonly List<int> lineStarts = [0, .. b.Select((c, i) => (c, i)).Where(x => x.c == '\n').Select(x => x.i + 1)];
        private int p;

        public CarveScript Script()
        {
            var statements = new List<ScriptStatement>();
            SkipSpace();
            while (p < b.Length)
            {
                statements.Add(Statement());
                SkipSpace();
            }
            return new CarveScript(statements);
        }

        private ScriptStatement Statement()
        {
            var line = Line(p);
            var tool = Name("a tool name");
            SkipBlank();
            if (At('\n'))
                throw Fail(p, $"expected '(' on the line of the tool name '{tool}': a statement may not break before its '('");
            Expect('(', $"'(' after the tool name '{tool}'");
            var arguments = new List<(string Name, JsonElement Value)>();
            SkipSpace();
            if (At(')'))
                p++;
            else
                while (true)
                {
                    SkipSpace();
                    if (At(')') && arguments.Count > 0)
                        throw Fail(p, $"expected an argument name after ',' in '{tool}': a trailing comma is not allowed");
                    var at = p;
                    var name = Name("an argument name");
                    if (arguments.Any(a => a.Name == name))
                        throw Fail(at, $"the argument '{name}' is given twice in '{tool}'");
                    SkipSpace();
                    Expect(':', $"':' after the argument name '{name}'");
                    SkipSpace();
                    arguments.Add((name, Value(name)));
                    SkipSpace();
                    if (At(','))
                    {
                        p++;
                        continue;
                    }
                    Expect(')', $"',' or ')' after the value of '{name}'");
                    break;
                }
            SkipBlank();
            if (p < b.Length && b[p] != '\n')
                throw IsWord(b[p])
                    ? Fail(p, $"a second statement on the line of '{tool}': statements are separated by a newline")
                    : Expected($"a newline after the ')' that ends '{tool}'");
            return new ScriptStatement(tool, arguments, line);
        }

        private string Name(string what)
        {
            var start = p;
            while (p < b.Length && IsWord(b[p]))
                p++;
            if (p == start)
                throw Expected(what);
            var name = Encoding.ASCII.GetString(b, start, p - start);
            if (!IsName(name))
                throw Fail(start, $"'{name}' is not {what}: a name is lower-case letters, digits and '_', and does not start with a digit");
            return name;
        }

        private JsonElement Value(string name)
        {
            // A number or literal ends where its run of letters, digits and signs ends: the JSON reader would refuse the ')' after it.
            var end = b.Length;
            if (p < b.Length && b[p] is not ((byte)'[' or (byte)'{' or (byte)'"'))
            {
                end = p;
                while (end < b.Length && (IsWord(b[end]) || b[end] is (byte)'-' or (byte)'+' or (byte)'.'))
                    end++;
            }
            if (end == p)
                throw Expected($"a JSON value for '{name}'");
            var reader = new Utf8JsonReader(b.AsSpan(p, end - p), new JsonReaderOptions { AllowMultipleValues = true });
            try
            {
                var value = JsonElement.ParseValue(ref reader);
                p += (int)reader.BytesConsumed;
                return value;
            }
            catch (JsonException e)
            {
                // The JSON reader's own message, less its position (given as the script's) and with the text it quotes kept on one line.
                var message = e.Message;
                foreach (var cut in new[] { " LineNumber:", " Path:" })
                    if (message.IndexOf(cut, StringComparison.Ordinal) is var i and >= 0)
                        message = message[..i];
                throw Fail(JsonOffset(e), $"the value of '{name}' is not JSON: {message.Trim().ReplaceLineEndings(" ")}");
            }
        }

        // The JSON reader's line and byte-in-line are relative to where the value starts.
        private int JsonOffset(JsonException e)
        {
            var start = p;
            for (var line = 0L; line < (e.LineNumber ?? 0); line++)
                start = Array.IndexOf(b, (byte)'\n', start) + 1;
            return Math.Min(b.Length, start + (int)(e.BytePositionInLine ?? 0));
        }

        private void Expect(char c, string what)
        {
            if (!At(c))
                throw Expected(what);
            p++;
        }

        private Refusal Expected(string what)
        {
            if (p >= b.Length)
                return Fail(p, $"expected {what}, found the end of the file");
            return Known(b[p])
                ? Fail(p, $"expected {what}")
                : Fail(p, $"unknown character {Shown()}: expected {what}");
        }

        private string Shown()
        {
            Rune.DecodeFromUtf8(b.AsSpan(p), out var rune, out _);
            return rune.Value < 0x20 ? $"U+{rune.Value:X4}" : $"'{rune}'";
        }

        private Refusal Fail(int offset, string message)
        {
            var line = Line(offset);
            var start = lineStarts[line - 1];
            return new Refusal(new ScriptError(line, Encoding.UTF8.GetCharCount(b, start, offset - start) + 1, message));
        }

        private int Line(int offset)
        {
            var i = lineStarts.BinarySearch(offset);
            return i >= 0 ? i + 1 : ~i;
        }

        private bool At(char c) => p < b.Length && b[p] == c;

        private void SkipSpace()
        {
            while (p < b.Length && b[p] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
                p++;
        }

        // Within a line: a '\r' is a CRLF's first half.
        private void SkipBlank()
        {
            while (p < b.Length && b[p] is (byte)' ' or (byte)'\t' or (byte)'\r')
                p++;
        }

        private static bool IsWord(byte c) => c is (>= (byte)'a' and <= (byte)'z') or (>= (byte)'A' and <= (byte)'Z') or (>= (byte)'0' and <= (byte)'9') or (byte)'_';

        // The characters the grammar and JSON use outside a string; anything else is an unknown character.
        private static bool Known(byte c) => IsWord(c) || "()[]{}:,\"-+.".Contains((char)c);
    }
}
