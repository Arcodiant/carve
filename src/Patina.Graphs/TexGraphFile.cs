using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Patina.Graphs.Nodes;

namespace Patina.Graphs;

/// <summary>
/// The <c>.texgraph</c> file, versions 1 and 2: JSON written in a fixed key order (<c>version</c>, <c>size</c>, <c>seed</c>,
/// <c>nodes</c>; in a node <c>type</c>, <c>inputs</c>, <c>params</c>, the last two left out when empty), nodes, inputs
/// and parameters in ordinal key order, one node per line, LF line endings and a final newline, numbers in their
/// shortest round-trip form. A list parameter is an array of numbers or of strings. Write, read and write again gives the
/// same bytes.
/// <para><b>Versions.</b> A file is written in the lowest version that holds the graph, as the Carve file is: version 1
/// unless the graph uses something a later version adds, which a <see cref="TexGraphFormat"/> lists (a top-level
/// <see cref="TexGraphField"/>, written after <c>seed</c> in the format's order, or a node type). Version 2 is reserved for
/// T-139's <c>parameters</c> (<see cref="ParametersField"/>), T-140's <c>graph</c> and <c>input</c> nodes and T-240's <c>image</c>;
/// <see cref="TexGraphFormat.Current"/> holds <c>parameters</c>. A node parameter an exposed parameter drives is not
/// written (its own value is ignored); <see cref="WriteShowingDriven"/> writes it as <c>{"driven_by": "name"}</c>, which
/// is read back too. Both versions are read; a file that uses what its version does not hold is a <see cref="GraphLoadError"/>.</para>
/// <para>A file that is not JSON, has not this shape (an unknown or missing key, a value of the wrong JSON kind, a
/// duplicate key), has a version other than 1 or 2, a size that is not a power of two from 32 to 8192 or a seed outside
/// 0..4294967295 is a <see cref="GraphLoadError"/> with its line and column. Everything else wrong is a
/// <see cref="GraphFault"/>, and the graph still loads.</para>
/// </summary>
public static class TexGraphFile
{
    public const string Extension = ".texgraph";

    /// <summary>The versions this build reads: 1 to <see cref="LatestVersion"/>.</summary>
    public const int LatestVersion = 2;

    public static string Write(TextureGraph graph, TexGraphFormat? format = null) => Write(graph, format, showDriven: false);

    /// <summary>
    /// The graph as <see cref="Write(TextureGraph, TexGraphFormat?)"/> gives it, but with each node parameter an exposed
    /// parameter drives shown in its node as <c>{"driven_by": "&lt;name&gt;"}</c> (get_graph's form). Read takes it back,
    /// giving the same graph.
    /// </summary>
    public static string WriteShowingDriven(TextureGraph graph, TexGraphFormat? format = null) => Write(graph, format, showDriven: true);

    private static string Write(TextureGraph graph, TexGraphFormat? format, bool showDriven)
    {
        format ??= TexGraphFormat.Current;
        // A format without parameters cannot hold a graph's, nor the node values they drive, so it is refused rather than dropping both (RV-76).
        if (graph.Parameters.Count > 0 && format.Field(ParametersField.Instance.Name) is null)
            throw new ArgumentException($"the graph has exposed parameters ({string.Join(", ", graph.Parameters.Keys.Order(StringComparer.Ordinal))}), which this format does not hold", nameof(format));
        // A driven parameter's own value is ignored, so it is not written; get_graph's form names its driver instead.
        var driven = new Dictionary<(string, string), string>();
        foreach (var (name, p) in graph.Parameters.OrderBy(p => p.Key, StringComparer.Ordinal))
            foreach (var drive in p.Drives)
                if (GraphParameter.TryParseDrive(drive, out var n, out var q)) driven.TryAdd((n, q), name);
        var s = new StringBuilder();
        s.Append("{\n");
        s.Append("  \"version\": ").Append(format.VersionOf(graph).ToString(CultureInfo.InvariantCulture)).Append(",\n");
        s.Append("  \"size\": ").Append(graph.Size.ToString(CultureInfo.InvariantCulture)).Append(",\n");
        s.Append("  \"seed\": ").Append(graph.Seed.ToString(CultureInfo.InvariantCulture)).Append(",\n");
        foreach (var field in format.Fields.Where(f => f.IsIn(graph)))
            s.Append("  ").Append(Quote(field.Name)).Append(": ").Append(field.Write(graph)).Append(",\n");
        if (graph.Nodes.Count == 0)
        {
            s.Append("  \"nodes\": {}\n}\n");
            return s.ToString();
        }
        s.Append("  \"nodes\": {\n");
        var i = 0;
        foreach (var (id, node) in graph.Nodes.OrderBy(p => p.Key, StringComparer.Ordinal)) // ordinal whatever the dictionary's own comparer
        {
            s.Append("    ").Append(Quote(id)).Append(": { \"type\": ").Append(Quote(node.Type));
            if (node.Inputs.Count > 0)
                s.Append(", \"inputs\": { ").Append(string.Join(", ", node.Inputs.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => Quote(p.Key) + ": " + Quote(p.Value)))).Append(" }");
            var shown = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var (name, value) in node.Params)
                if (!driven.ContainsKey((id, name))) shown[name] = Value(value);
            if (showDriven)
                foreach (var ((n, q), by) in driven)
                    if (n == id) shown[q] = "{ \"driven_by\": " + Quote(by) + " }";
            if (shown.Count > 0)
                s.Append(", \"params\": { ").Append(string.Join(", ", shown.Select(p => Quote(p.Key) + ": " + p.Value))).Append(" }");
            s.Append(" }").Append(++i < graph.Nodes.Count ? ",\n" : "\n");
        }
        s.Append("  }\n}\n");
        return s.ToString();
    }

    /// <summary>
    /// Reads a file's text, and validates what it holds against <paramref name="registry"/> (the built-in types when null).
    /// <paramref name="folder"/> becomes the graph's <see cref="TextureGraph.Folder"/>, what its <c>graph</c> nodes'
    /// paths resolve against (T-140; null: the working directory).
    /// </summary>
    public static TexGraphReadResult Read(string text, NodeRegistry? registry = null, TexGraphFormat? format = null, string? folder = null)
    {
        var parsed = Parse(text, format, folder);
        return parsed.Graph is { } graph ? new(graph, null, GraphValidator.Validate(graph, registry ?? NodeRegistry.BuiltIn)) : parsed;
    }

    /// <summary>Reads a file's text without validating it (T-140: expansion reads a nested file this way, and validates it with the chain of files above it).</summary>
    internal static TexGraphReadResult Parse(string text, TexGraphFormat? format, string? folder)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        try
        {
            var graph = new Parser(bytes, format ?? TexGraphFormat.Current).Parse();
            if (folder is not null) graph = graph with { Folder = folder };
            return new(graph, null, []);
        }
        catch (JsonException e)
        {
            var message = e.Message;
            var cut = message.IndexOf(" LineNumber:", StringComparison.Ordinal);
            if (cut > 0) message = message[..cut];
            var line = (int)(e.LineNumber ?? 0);
            return new(null, new(line + 1, Column(bytes, line, e.BytePositionInLine ?? 0), "not JSON: " + message.TrimEnd('.', ' ')), []);
        }
        catch (ShapeException e)
        {
            var (line, column) = Position(bytes, e.Offset);
            return new(null, new(line, column, e.Message), []);
        }
    }

    /// <summary>Reads and validates a file; its folder is the graph's <see cref="TextureGraph.Folder"/>.</summary>
    public static TexGraphReadResult ReadFile(string path, NodeRegistry? registry = null, TexGraphFormat? format = null) =>
        Read(File.ReadAllText(path), registry, format, Path.GetDirectoryName(Path.GetFullPath(path)));

    public static void WriteFile(string path, TextureGraph graph, TexGraphFormat? format = null) => File.WriteAllText(path, Write(graph, format), new UTF8Encoding(false));

    /// <summary>A number as the file writes it: shortest round-trip, invariant culture.</summary>
    public static string FormatNumber(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    internal static string Value(ParamValue value) => value.Kind switch
    {
        ParamValueKind.Number => FormatNumber(value.Number),
        ParamValueKind.Text => Quote(value.Text!),
        ParamValueKind.Texts => "[" + string.Join(", ", value.Texts.Select(Quote)) + "]",
        _ => "[" + string.Join(", ", value.Numbers.Select(FormatNumber)) + "]",
    };

    internal static string Quote(string text) => "\"" + JsonEncodedText.Encode(text, JavaScriptEncoder.UnsafeRelaxedJsonEscaping) + "\"";

    private static (int Line, int Column) Position(byte[] bytes, long offset)
    {
        int line = 1, column = 1;
        for (long i = 0; i < offset && i < bytes.Length; i++)
            if (bytes[i] == '\n') { line++; column = 1; }
            else if (!IsContinuation(bytes[i])) column++;
        return (line, column);
    }

    /// <summary>The column, counted in characters, of the place <paramref name="bytesIn"/> UTF-8 bytes into line <paramref name="line"/> (from 0).</summary>
    private static int Column(byte[] bytes, int line, long bytesIn)
    {
        long start = 0;
        for (var seen = 0; seen < line && start < bytes.Length; start++)
            if (bytes[start] == '\n') seen++;
        var column = 1;
        for (var i = start; i < start + bytesIn && i < bytes.Length; i++)
            if (!IsContinuation(bytes[i])) column++;
        return column;
    }

    // A byte after the first of a UTF-8 character: a column counts characters, not bytes.
    private static bool IsContinuation(byte b) => (b & 0xC0) == 0x80;

    private sealed class ShapeException(long offset, string message) : Exception(message)
    {
        public long Offset { get; } = offset;
    }

    private ref struct Parser
    {
        private Utf8JsonReader _reader;
        private readonly TexGraphFormat _format;
        // What the file uses that a later version adds, and where: checked against its version once the whole file is read.
        private readonly List<(string What, int Version, long At)> _uses = [];
        // Each node parameter given as {"driven_by": name} (get_graph's form), checked against the parameters once read.
        private readonly List<(string Node, string Param, string By, long At)> _drivenBy = [];

        public Parser(byte[] bytes, TexGraphFormat format)
        {
            _reader = new Utf8JsonReader(bytes, new JsonReaderOptions { CommentHandling = JsonCommentHandling.Disallow });
            _format = format;
        }

        public TextureGraph Parse()
        {
            Next();
            Expect(JsonTokenType.StartObject, "the file is one JSON object");
            int? version = null, size = null;
            uint? seed = null;
            ImmutableSortedDictionary<string, GraphNode>? nodes = null;
            var fields = new List<Func<TextureGraph, TextureGraph>>();
            var seen = new HashSet<string>();
            while (Next() == JsonTokenType.PropertyName)
            {
                var key = Key(seen, "");
                var at = _reader.TokenStartIndex;
                Next();
                switch (key)
                {
                    case "version":
                        if (_reader.TokenType != JsonTokenType.Number || !_reader.TryGetInt32(out var v))
                            throw new ShapeException(_reader.TokenStartIndex, "version is a whole number");
                        if (v < 1 || v > LatestVersion)
                            throw new ShapeException(_reader.TokenStartIndex, $"version {v} is not known: this build reads versions 1 to {LatestVersion}");
                        version = v;
                        break;
                    case "size":
                        if (_reader.TokenType != JsonTokenType.Number || !_reader.TryGetInt32(out var z) || !TextureGraph.IsValidSize(z))
                            throw new ShapeException(_reader.TokenStartIndex, "size is " + TextureGraph.SizesText);
                        size = z;
                        break;
                    case "seed":
                        if (_reader.TokenType != JsonTokenType.Number || !_reader.TryGetUInt32(out var d))
                            throw new ShapeException(_reader.TokenStartIndex, "seed is a whole number from 0 to 4294967295");
                        seed = d;
                        break;
                    case "nodes":
                        nodes = Nodes();
                        break;
                    default:
                        if (_format.Field(key) is not { } field)
                            throw new ShapeException(at, $"unknown key '{key}': a graph has {string.Join(", ", ["version", "size", "seed", .. _format.Fields.Select(f => f.Name)])} and nodes");
                        _uses.Add(($"'{key}'", field.Version, at));
                        var start = _reader.TokenStartIndex;
                        try
                        {
                            fields.Add(field.Read(ref _reader));
                        }
                        catch (FormatException e)
                        {
                            throw new ShapeException(start, $"{key}: {e.Message}");
                        }
                        break;
                }
            }
            var end = _reader.TokenStartIndex;
            if (version is null) throw new ShapeException(end, "the file has no version");
            if (size is null) throw new ShapeException(end, "the file has no size");
            if (seed is null) throw new ShapeException(end, "the file has no seed");
            if (nodes is null) throw new ShapeException(end, "the file has no nodes");
            if (_reader.Read()) throw new ShapeException(_reader.TokenStartIndex, "nothing may follow the graph's object");
            foreach (var (what, needs, at) in _uses.Where(u => u.Version > version))
                throw new ShapeException(at, $"{what} needs version {needs}, but the file is version {version}");
            var read = fields.Aggregate(new TextureGraph(size.Value, seed.Value, nodes), (graph, apply) => apply(graph));
            foreach (var (node, param, by, at) in _drivenBy)
                if (!read.Parameters.TryGetValue(by, out var p) || !p.Drives.Contains($"{node}.{param}"))
                    throw new ShapeException(at, $"node '{node}' parameter '{param}': is driven_by '{by}', but no parameter '{by}' drives {node}.{param}");
            return read;
        }

        private ImmutableSortedDictionary<string, GraphNode> Nodes()
        {
            Expect(JsonTokenType.StartObject, "nodes is an object of nodes by id");
            var nodes = ImmutableSortedDictionary.CreateBuilder<string, GraphNode>(StringComparer.Ordinal);
            var seen = new HashSet<string>();
            while (Next() == JsonTokenType.PropertyName)
            {
                var id = Key(seen, "nodes: ");
                Next();
                nodes[id] = Node(id);
            }
            return nodes.ToImmutable();
        }

        private GraphNode Node(string id)
        {
            Expect(JsonTokenType.StartObject, $"node '{id}' is an object");
            var start = _reader.TokenStartIndex;
            string? type = null;
            var inputs = ImmutableSortedDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
            var parameters = ImmutableSortedDictionary.CreateBuilder<string, ParamValue>(StringComparer.Ordinal);
            var seen = new HashSet<string>();
            while (Next() == JsonTokenType.PropertyName)
            {
                var key = Key(seen, $"node '{id}': ");
                var at = _reader.TokenStartIndex;
                Next();
                switch (key)
                {
                    case "type":
                        Expect(JsonTokenType.String, $"node '{id}': type is a string");
                        type = _reader.GetString()!;
                        if (_format.NodeTypeVersion(type) is var needs and > 1) _uses.Add(($"node '{id}' of type {type}", needs, _reader.TokenStartIndex));
                        break;
                    case "inputs":
                        Expect(JsonTokenType.StartObject, $"node '{id}': inputs is an object of input ports to sources");
                        var ports = new HashSet<string>();
                        while (Next() == JsonTokenType.PropertyName)
                        {
                            var port = Key(ports, $"node '{id}' inputs: ");
                            Next();
                            Expect(JsonTokenType.String, $"node '{id}' input '{port}': a source is a string, \"<id>\" or \"<id>.<port>\"");
                            inputs[port] = _reader.GetString()!;
                        }
                        break;
                    case "params":
                        Expect(JsonTokenType.StartObject, $"node '{id}': params is an object of parameters by name");
                        var names = new HashSet<string>();
                        while (Next() == JsonTokenType.PropertyName)
                        {
                            var name = Key(names, $"node '{id}' params: ");
                            Next();
                            if (_reader.TokenType == JsonTokenType.StartObject) DrivenBy(id, name);
                            else parameters[name] = Param(id, name);
                        }
                        break;
                    default:
                        throw new ShapeException(at, $"node '{id}': unknown key '{key}': a node has type, inputs and params");
                }
            }
            if (type is null) throw new ShapeException(start, $"node '{id}' has no type");
            return new GraphNode(type, inputs.ToImmutable(), parameters.ToImmutable());
        }

        /// <summary>Reads <c>{"driven_by": "&lt;name&gt;"}</c>, get_graph's form of a driven parameter: the parameter has no value of its own.</summary>
        private void DrivenBy(string id, string name)
        {
            var at = _reader.TokenStartIndex;
            var what = $"node '{id}' parameter '{name}': an object here is {{\"driven_by\": \"<parameter>\"}}, get_graph's form of a driven parameter";
            if (Next() != JsonTokenType.PropertyName || _reader.GetString() != "driven_by") throw new ShapeException(_reader.TokenStartIndex, what);
            Next();
            Expect(JsonTokenType.String, what);
            var by = _reader.GetString()!;
            if (Next() != JsonTokenType.EndObject) throw new ShapeException(_reader.TokenStartIndex, what);
            _drivenBy.Add((id, name, by, at));
        }

        private ParamValue Param(string id, string name)
        {
            var what = $"node '{id}' parameter '{name}': a parameter is a number, a string, or an array of numbers or of strings";
            switch (_reader.TokenType)
            {
                case JsonTokenType.Number: return ParamValue.Of(Number(what));
                case JsonTokenType.String: return ParamValue.Of(_reader.GetString()!);
                case JsonTokenType.StartArray:
                    var numbers = new List<double>();
                    var texts = new List<string>();
                    while (Next() != JsonTokenType.EndArray)
                    {
                        // An array is all numbers or all strings: its first item says which.
                        if (numbers.Count == 0 && texts.Count == 0 && _reader.TokenType == JsonTokenType.String) texts.Add(_reader.GetString()!);
                        else if (texts.Count > 0)
                        {
                            Expect(JsonTokenType.String, $"node '{id}' parameter '{name}': an array is all numbers or all strings");
                            texts.Add(_reader.GetString()!);
                        }
                        else
                        {
                            Expect(JsonTokenType.Number, numbers.Count == 0 ? what : $"node '{id}' parameter '{name}': an array is all numbers or all strings");
                            numbers.Add(Number(what));
                        }
                    }
                    return texts.Count > 0 ? ParamValue.OfTexts([.. texts]) : ParamValue.Of([.. numbers]);
                default: throw new ShapeException(_reader.TokenStartIndex, what);
            }
        }

        private double Number(string what) =>
            _reader.TryGetDouble(out var d) && double.IsFinite(d) ? d : throw new ShapeException(_reader.TokenStartIndex, what);

        private string Key(HashSet<string> seen, string where)
        {
            var key = _reader.GetString()!;
            if (!seen.Add(key)) throw new ShapeException(_reader.TokenStartIndex, $"{where}'{key}' is given twice");
            return key;
        }

        private JsonTokenType Next()
        {
            if (!_reader.Read()) throw new ShapeException(_reader.BytesConsumed, "the file ends early");
            return _reader.TokenType;
        }

        private readonly void Expect(JsonTokenType type, string message)
        {
            if (_reader.TokenType != type) throw new ShapeException(_reader.TokenStartIndex, message);
        }
    }
}

/// <summary>
/// A top-level field a later version of the <c>.texgraph</c> file adds (T-139's <c>parameters</c> is the first): its
/// key, the lowest version that holds it, and how it is written and read. A graph that has it is written in that version
/// at least; a file of an earlier version that gives it does not load.
/// </summary>
public abstract class TexGraphField
{
    /// <summary>The key, <c>[a-z][a-z0-9_]*</c>, not one of version, size, seed or nodes.</summary>
    public abstract string Name { get; }

    /// <summary>The lowest version that holds the field, 2 or more.</summary>
    public abstract int Version { get; }

    /// <summary>True when <paramref name="graph"/> has the field, so it is written (and needs <see cref="Version"/>).</summary>
    public abstract bool IsIn(TextureGraph graph);

    /// <summary>The field's JSON value as the file writes it, in a fixed form: written, read and written again it gives the same text. A line after the first is indented as the file's own (two spaces a level, the field's key at one level).</summary>
    public abstract string Write(TextureGraph graph);

    /// <summary>
    /// Reads the value: <paramref name="reader"/> is on its first token, and is left on its last. Returns what the field
    /// sets on the graph read. A value of the wrong shape throws <see cref="FormatException"/>, whose message the load
    /// error gives after the key.
    /// </summary>
    public abstract Func<TextureGraph, TextureGraph> Read(ref Utf8JsonReader reader);
}

/// <summary>
/// What a later version of the file adds: its top-level fields, in the order they are written (after <c>seed</c>, before
/// <c>nodes</c>), and its node types, each with the lowest version that holds it. A graph is written in the highest
/// version anything it uses needs, 1 when it uses nothing. <see cref="Current"/> is what this build reads and writes:
/// T-139 adds its <c>parameters</c> field there, and T-140 its <c>graph</c> and <c>input</c> node types at version 2.
/// </summary>
public sealed class TexGraphFormat
{
    private TexGraphFormat(ImmutableArray<TexGraphField> fields, ImmutableSortedDictionary<string, int> nodeTypes)
    {
        Fields = fields; NodeTypes = nodeTypes;
    }

    /// <summary>The format with nothing a later version adds: every graph is version 1.</summary>
    public static TexGraphFormat Version1 { get; } = new([], ImmutableSortedDictionary.Create<string, int>(StringComparer.Ordinal));

    /// <summary>The format this build uses: version 2 adds <c>parameters</c> (T-139).</summary>
    public static TexGraphFormat Current { get; } = Version1.With(ParametersField.Instance).WithNodeType(NestedGraphNode.TypeName, 2).WithNodeType(InputNode.TypeName, 2).WithNodeType(ImageNode.TypeName, 2);

    public ImmutableArray<TexGraphField> Fields { get; }

    /// <summary>Node types a later version adds, with the version.</summary>
    public ImmutableSortedDictionary<string, int> NodeTypes { get; }

    /// <summary>This format with <paramref name="field"/> after its own fields.</summary>
    public TexGraphFormat With(TexGraphField field)
    {
        if (field.Name is "version" or "size" or "seed" or "nodes" || !GraphNames.IsValid(field.Name) || Field(field.Name) is not null)
            throw new ArgumentException($"'{field.Name}' cannot be a field: it is taken or not a name", nameof(field));
        CheckVersion(field.Version);
        return new([.. Fields, field], NodeTypes);
    }

    /// <summary>This format with node type <paramref name="type"/> held from <paramref name="version"/> on.</summary>
    public TexGraphFormat WithNodeType(string type, int version)
    {
        CheckVersion(version);
        return new(Fields, NodeTypes.SetItem(type, version));
    }

    public TexGraphField? Field(string name) => Fields.FirstOrDefault(f => f.Name == name);

    /// <summary>The lowest version that holds node type <paramref name="type"/>: 1 unless a later version adds it.</summary>
    public int NodeTypeVersion(string type) => NodeTypes.TryGetValue(type, out var v) ? v : 1;

    /// <summary>The lowest version that holds <paramref name="graph"/>: the highest any field it has or node type it uses needs, or 1.</summary>
    public int VersionOf(TextureGraph graph) =>
        Fields.Where(f => f.IsIn(graph)).Select(f => f.Version)
            .Concat(graph.Nodes.Values.Select(n => NodeTypeVersion(n.Type)))
            .DefaultIfEmpty(1).Max();

    private static void CheckVersion(int version)
    {
        if (version < 2 || version > TexGraphFile.LatestVersion)
            throw new ArgumentOutOfRangeException(nameof(version), version, $"a later version is 2 to {TexGraphFile.LatestVersion}");
    }
}
