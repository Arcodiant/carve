using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Patina.Graphs.Nodes;

namespace Patina.Graphs;

/// <summary>
/// The <c>.texgraph</c> file, version 1: JSON written in a fixed key order (<c>version</c>, <c>size</c>, <c>seed</c>,
/// <c>nodes</c>; in a node <c>type</c>, <c>inputs</c>, <c>params</c>, the last two left out when empty), nodes, inputs
/// and parameters in ordinal key order, one node per line, LF line endings and a final newline, numbers in their
/// shortest round-trip form. Write, read and write again gives the same bytes.
/// <para>A file that is not JSON, has not this shape (an unknown or missing key, a value of the wrong JSON kind, a
/// duplicate key), has a version other than 1, a size that is not a power of two from 32 to 8192 or a seed outside
/// 0..4294967295 is a <see cref="GraphLoadError"/> with its line and column. Everything else wrong is a
/// <see cref="GraphFault"/>, and the graph still loads.</para>
/// </summary>
public static class TexGraphFile
{
    public const string Extension = ".texgraph";

    public static string Write(TextureGraph graph)
    {
        var s = new StringBuilder();
        s.Append("{\n");
        s.Append("  \"version\": ").Append(TextureGraph.Version).Append(",\n");
        s.Append("  \"size\": ").Append(graph.Size.ToString(CultureInfo.InvariantCulture)).Append(",\n");
        s.Append("  \"seed\": ").Append(graph.Seed.ToString(CultureInfo.InvariantCulture)).Append(",\n");
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
            if (node.Params.Count > 0)
                s.Append(", \"params\": { ").Append(string.Join(", ", node.Params.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => Quote(p.Key) + ": " + Value(p.Value)))).Append(" }");
            s.Append(" }").Append(++i < graph.Nodes.Count ? ",\n" : "\n");
        }
        s.Append("  }\n}\n");
        return s.ToString();
    }

    /// <summary>Reads a file's text, and validates what it holds against <paramref name="registry"/> (the built-in types when null).</summary>
    public static TexGraphReadResult Read(string text, NodeRegistry? registry = null)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        try
        {
            var graph = new Parser(bytes).Parse();
            return new(graph, null, GraphValidator.Validate(graph, registry ?? NodeRegistry.BuiltIn));
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

    public static TexGraphReadResult ReadFile(string path, NodeRegistry? registry = null) => Read(File.ReadAllText(path), registry);

    public static void WriteFile(string path, TextureGraph graph) => File.WriteAllText(path, Write(graph), new UTF8Encoding(false));

    /// <summary>A number as the file writes it: shortest round-trip, invariant culture.</summary>
    public static string FormatNumber(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static string Value(ParamValue value) => value.Kind switch
    {
        ParamValueKind.Number => FormatNumber(value.Number),
        ParamValueKind.Text => Quote(value.Text!),
        _ => "[" + string.Join(", ", value.Numbers.Select(FormatNumber)) + "]",
    };

    private static string Quote(string text) => "\"" + JsonEncodedText.Encode(text, JavaScriptEncoder.UnsafeRelaxedJsonEscaping) + "\"";

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

        public Parser(byte[] bytes) =>
            _reader = new Utf8JsonReader(bytes, new JsonReaderOptions { CommentHandling = JsonCommentHandling.Disallow });

        public TextureGraph Parse()
        {
            Next();
            Expect(JsonTokenType.StartObject, "the file is one JSON object");
            int? version = null, size = null;
            uint? seed = null;
            ImmutableSortedDictionary<string, GraphNode>? nodes = null;
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
                        if (v != TextureGraph.Version)
                            throw new ShapeException(_reader.TokenStartIndex, $"version {v} is not known: this build reads version {TextureGraph.Version}");
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
                        throw new ShapeException(at, $"unknown key '{key}': a graph has version, size, seed and nodes");
                }
            }
            var end = _reader.TokenStartIndex;
            if (version is null) throw new ShapeException(end, "the file has no version");
            if (size is null) throw new ShapeException(end, "the file has no size");
            if (seed is null) throw new ShapeException(end, "the file has no seed");
            if (nodes is null) throw new ShapeException(end, "the file has no nodes");
            if (_reader.Read()) throw new ShapeException(_reader.TokenStartIndex, "nothing may follow the graph's object");
            return new TextureGraph(size.Value, seed.Value, nodes);
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
                            parameters[name] = Param(id, name);
                        }
                        break;
                    default:
                        throw new ShapeException(at, $"node '{id}': unknown key '{key}': a node has type, inputs and params");
                }
            }
            if (type is null) throw new ShapeException(start, $"node '{id}' has no type");
            return new GraphNode(type, inputs.ToImmutable(), parameters.ToImmutable());
        }

        private ParamValue Param(string id, string name)
        {
            var what = $"node '{id}' parameter '{name}': a parameter is a number, a string or an array of numbers";
            switch (_reader.TokenType)
            {
                case JsonTokenType.Number: return ParamValue.Of(Number(what));
                case JsonTokenType.String: return ParamValue.Of(_reader.GetString()!);
                case JsonTokenType.StartArray:
                    var numbers = new List<double>();
                    while (Next() != JsonTokenType.EndArray)
                    {
                        Expect(JsonTokenType.Number, what);
                        numbers.Add(Number(what));
                    }
                    return ParamValue.Of([.. numbers]);
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
