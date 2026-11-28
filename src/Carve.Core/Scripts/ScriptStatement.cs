using System.Text.Json;

namespace Carve.Core.Scripts;

/// <summary>
/// One statement of a <c>.carvescript</c>: a tool call, <c>tool(name: &lt;json&gt;, ...)</c> (D-8, <i>The statement format</i>).
/// </summary>
/// <param name="Tool">The tool's name, <c>[a-z_][a-z0-9_]*</c>. The script knows no tool list; whether the tool exists is the runner's business.</param>
/// <param name="Arguments">The arguments in the order written, each a name (same rule as the tool's) and one JSON value, its numbers as their raw text.</param>
/// <param name="Line">The 1-based line the statement starts on (the line of its tool name), or 0 for one not read from text.</param>
public sealed record ScriptStatement(string Tool, IReadOnlyList<(string Name, JsonElement Value)> Arguments, int Line = 0);
