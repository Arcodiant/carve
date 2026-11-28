using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Carve.Server;

/// <summary>
/// One call in the journal (M-34, T-231): the tool, its arguments as the MCP call sent them (in the order sent, each the
/// raw <see cref="JsonElement"/>, numbers kept as their text), and for each argument naming a file the tool read, the
/// absolute path it was resolved to, keyed by where the argument is (<c>path</c>, <c>file</c>, <c>normal_texture</c>,
/// <c>normal_texture.graph</c>, <c>source.graph</c>).
/// </summary>
public sealed record JournalEntry(string Tool, IReadOnlyList<KeyValuePair<string, JsonElement>> Arguments, IReadOnlyDictionary<string, string> Files);

/// <summary>
/// The tool call under way on this async flow, set by <see cref="JournalledTool"/> for the length of the call, so that
/// <see cref="ModelStore"/> can journal it when it pushes a step and <see cref="ToolPaths.Resolve"/> can say what it read,
/// with no tool changed.
/// </summary>
internal sealed class JournalCall(string tool, IReadOnlyList<KeyValuePair<string, JsonElement>> arguments, IReadOnlyList<string> reads)
{
    private static readonly AsyncLocal<JournalCall?> current = new();
    private readonly Dictionary<string, string> _resolved = [];

    public static JournalCall? Current { get => current.Value; set => current.Value = value; }

    public string Tool => tool;

    /// <summary>Whether this call has pushed its step: a second push in one call is a bug (T-231).</summary>
    public bool Pushed { get; set; }

    /// <summary>Notes that the call read the file <paramref name="given"/> as <paramref name="fullPath"/> (from <see cref="ToolPaths.Resolve"/>).</summary>
    public void Read(string given, string fullPath) => _resolved[given] = fullPath;

    /// <summary>
    /// The call as an entry. Throws when it read a file through an argument its tool does not declare, so a new
    /// file-reading argument is declared (in its tool's <c>Create</c>) rather than journalled without its path.
    /// </summary>
    public JournalEntry Entry()
    {
        var files = new Dictionary<string, string>();
        foreach (var at in reads)
            if (Find(at) is { ValueKind: JsonValueKind.String } value && _resolved.TryGetValue(value.GetString()!, out var full))
                files[at] = full;
        var undeclared = _resolved.Keys.Except(files.Keys.Select(k => Find(k)!.Value.GetString()!)).ToList();
        if (undeclared.Count > 0)
            throw new InvalidOperationException($"{tool} read {string.Join(", ", undeclared)} through an argument it does not declare as one it reads");
        return new JournalEntry(tool, arguments, files);
    }

    /// <summary>The argument at <paramref name="at"/>: a name, or <c>name.property</c> for a field of an object argument.</summary>
    private JsonElement? Find(string at)
    {
        var parts = at.Split('.');
        JsonElement? value = arguments.FirstOrDefault(a => a.Key == parts[0]) is { Key: not null } a ? a.Value : null;
        for (var i = 1; i < parts.Length && value is not null; i++)
            value = value.Value.ValueKind == JsonValueKind.Object && value.Value.TryGetProperty(parts[i], out var p) ? p : null;
        return value;
    }
}

/// <summary>A tool that sets <see cref="JournalCall.Current"/> for each call; <see cref="Reads"/> are the arguments naming a file it reads.</summary>
public sealed class JournalledTool(McpServerTool inner, IReadOnlyList<string> reads) : DelegatingMcpServerTool(inner)
{
    public IReadOnlyList<string> Reads => reads;

    public override async ValueTask<CallToolResult> InvokeAsync(RequestContext<CallToolRequestParams> request, CancellationToken cancellationToken = default)
    {
        var arguments = (request.Params?.Arguments ?? new Dictionary<string, JsonElement>())
            .Select(a => KeyValuePair.Create(a.Key, a.Value.Clone())).ToArray();
        // Set inside this async method, so it is seen by the call and gone again when it returns.
        JournalCall.Current = new JournalCall(ProtocolTool.Name, arguments, reads);
        return await base.InvokeAsync(request, cancellationToken);
    }
}
