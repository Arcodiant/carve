using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;

namespace Carve.Core;

/// <summary>
/// The outcome of <see cref="CarveFile.Read"/>: a mesh and the warnings it carries, or the errors that stopped one.
/// On failure <see cref="Mesh"/> is null and <see cref="Warnings"/> is empty.
/// </summary>
public sealed class CarveFileResult
{
    internal CarveFileResult(PolygonMesh? mesh, IReadOnlyList<MeshError> errors, IReadOnlyList<MeshWarning> warnings)
    {
        Mesh = mesh;
        Errors = errors;
        Warnings = warnings;
    }

    /// <summary>The mesh the file holds, every vertex and face at its id, or null when the file was refused.</summary>
    public PolygonMesh? Mesh { get; }

    /// <summary>Every problem found, each named by where it is in the file: <c>file</c>, <c>carve</c>, <c>vertices[3]</c>, <c>faces[2][1]</c>, or an unknown key.</summary>
    public IReadOnlyList<MeshError> Errors { get; }

    /// <summary>The mesh's warnings, exactly as <see cref="MeshReport"/> gives them for the same mesh made by <c>define_mesh</c>.</summary>
    public IReadOnlyList<MeshWarning> Warnings { get; }

    public bool Succeeded => Mesh is not null;
}

/// <summary>
/// The Carve file: a <see cref="PolygonMesh"/>'s geometry as versioned JSON text, read back bit-exact with every vertex and
/// face at its id. Undo history is not saved.
/// </summary>
/// <remarks>
/// <para>The layout, which <see cref="Write"/> always gives byte for byte: the keys <c>carve</c> (the format version),
/// <c>vertices</c> and <c>faces</c> in that order, two-space indent, one vertex <c>[x, y, z]</c> or one face (its vertex-id
/// loop, counter-clockwise seen from outside) per line, <c>\n</c> line ends and a newline at the end. Each coordinate is the
/// shortest text that reads back to the same <see cref="float"/>.</para>
/// <para>Reading accepts any JSON with those three keys in any order and any whitespace, and holds the geometry to exactly
/// what <see cref="PolygonMesh.Create"/> accepts. An unknown key or another version is refused, so a newer file is never
/// half-read.</para>
/// </remarks>
public static class CarveFile
{
    /// <summary>The version <see cref="Write"/> writes.</summary>
    public const int Version = 1;

    /// <summary>The versions <see cref="Read"/> reads.</summary>
    public static IReadOnlyList<int> ReadableVersions { get; } = [1];

    private const string VersionKey = "carve";
    private const string VerticesKey = "vertices";
    private const string FacesKey = "faces";

    /// <summary>The file's text for a mesh. The same mesh always gives the same text.</summary>
    public static string Write(PolygonMesh mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        var sb = new StringBuilder();
        sb.Append("{\n");
        sb.Append("  \"").Append(VersionKey).Append("\": ").Append(Version.ToString(CultureInfo.InvariantCulture)).Append(",\n");
        sb.Append("  \"").Append(VerticesKey).Append("\": ");
        AppendList(sb, mesh.Vertices, v => $"[{Number(v.X)}, {Number(v.Y)}, {Number(v.Z)}]");
        sb.Append(",\n");
        sb.Append("  \"").Append(FacesKey).Append("\": ");
        AppendList(sb, mesh.Faces, f => "[" + string.Join(", ", f.Select(i => i.ToString(CultureInfo.InvariantCulture))) + "]");
        sb.Append('\n');
        sb.Append("}\n");
        return sb.ToString();
    }

    private static void AppendList<T>(StringBuilder sb, IReadOnlyList<T> items, Func<T, string> line)
    {
        if (items.Count == 0)
        {
            sb.Append("[]");
            return;
        }
        sb.Append("[\n");
        for (var i = 0; i < items.Count; i++)
        {
            sb.Append("    ").Append(line(items[i]));
            if (i < items.Count - 1) sb.Append(',');
            sb.Append('\n');
        }
        sb.Append("  ]");
    }

    /// <summary>The shortest text that reads back to the same float; <c>-0</c> stays <c>-0</c>.</summary>
    private static string Number(float value) => value.ToString("R", CultureInfo.InvariantCulture);

    /// <summary>Reads a file's text into a mesh, or every error that stops one, each named by where it is in the file.</summary>
    public static CarveFileResult Read(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(text, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
            });
        }
        catch (JsonException e)
        {
            return Refused(new MeshError("file", NotJsonMessage(e)));
        }

        using (document)
        {
            return Read(document.RootElement);
        }
    }

    private static CarveFileResult Read(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            return Refused(new MeshError("file", $"the file is a JSON {Kind(root)}; a Carve file is a JSON object"));

        var errors = new List<MeshError>();
        JsonElement? version = null, vertices = null, faces = null;
        foreach (var property in root.EnumerateObject())
        {
            switch (property.Name)
            {
                case VersionKey: Take(ref version, property); break;
                case VerticesKey: Take(ref vertices, property); break;
                case FacesKey: Take(ref faces, property); break;
                default:
                    errors.Add(new MeshError(property.Name,
                        $"unknown key: a version {Version} Carve file has only the keys \"{VersionKey}\", \"{VerticesKey}\" and \"{FacesKey}\""));
                    break;
            }
        }

        void Take(ref JsonElement? slot, JsonProperty property)
        {
            if (slot is not null)
                errors.Add(new MeshError(property.Name, "key appears more than once"));
            else
                slot = property.Value;
        }

        // The version first: a file of another version is refused on that alone, since its other keys may mean something else.
        if (version is not { } v)
            errors.Add(new MeshError(VersionKey, $"missing: the file has no format version; this build reads {Versions()}"));
        else if (v.ValueKind != JsonValueKind.Number || !v.TryGetInt32(out var number))
            errors.Add(new MeshError(VersionKey, $"the format version is {Describe(v)}, not an integer; this build reads {Versions()}"));
        else if (!ReadableVersions.Contains(number))
            return Refused(new MeshError(VersionKey, Invariant($"version {number} is not one this build reads; it reads {Versions()}")));

        var verts = ReadVertices(vertices, errors);
        var fcs = ReadFaces(faces, errors);
        if (errors.Count > 0)
            return Refused(errors);

        var created = PolygonMesh.Create(verts!, fcs!);
        if (!created.Succeeded)
            return Refused(created.Errors);
        return new CarveFileResult(created.Mesh, [], MeshReport.Create(created.Mesh!).Warnings);
    }

    private static Vector3[]? ReadVertices(JsonElement? element, List<MeshError> errors)
    {
        if (element is not { } array)
        {
            errors.Add(new MeshError(VerticesKey, "missing: the file has no vertices array"));
            return null;
        }
        if (array.ValueKind != JsonValueKind.Array)
        {
            errors.Add(new MeshError(VerticesKey, $"vertices is {Describe(array)}, not an array"));
            return null;
        }

        var result = new Vector3[array.GetArrayLength()];
        var i = 0;
        foreach (var vertex in array.EnumerateArray())
        {
            var name = Invariant($"vertices[{i}]");
            if (vertex.ValueKind != JsonValueKind.Array)
                errors.Add(new MeshError(name, $"vertex is {Describe(vertex)}; a vertex is [x, y, z]"));
            else if (vertex.GetArrayLength() != 3)
                errors.Add(new MeshError(name, Invariant($"vertex has {vertex.GetArrayLength()} coordinates; a vertex is [x, y, z]")));
            else
            {
                var xyz = new float[3];
                var k = 0;
                foreach (var coordinate in vertex.EnumerateArray())
                {
                    var at = Invariant($"vertices[{i}][{k}]");
                    if (coordinate.ValueKind != JsonValueKind.Number)
                        errors.Add(new MeshError(at, $"coordinate is {Describe(coordinate)}, not a number"));
                    else
                    {
                        // The token's own text, parsed straight to float: correctly rounded once, and -0 kept.
                        var raw = coordinate.GetRawText();
                        var value = float.Parse(raw, NumberStyles.Float, CultureInfo.InvariantCulture);
                        if (!float.IsFinite(value))
                            errors.Add(new MeshError(at, $"coordinate {raw} is not finite: it is outside the range of a float"));
                        xyz[k] = value;
                    }
                    k++;
                }
                result[i] = new Vector3(xyz[0], xyz[1], xyz[2]);
            }
            i++;
        }
        return result;
    }

    private static int[][]? ReadFaces(JsonElement? element, List<MeshError> errors)
    {
        if (element is not { } array)
        {
            errors.Add(new MeshError(FacesKey, "missing: the file has no faces array"));
            return null;
        }
        if (array.ValueKind != JsonValueKind.Array)
        {
            errors.Add(new MeshError(FacesKey, $"faces is {Describe(array)}, not an array"));
            return null;
        }

        var result = new int[array.GetArrayLength()][];
        var f = 0;
        foreach (var face in array.EnumerateArray())
        {
            var name = Invariant($"faces[{f}]");
            if (face.ValueKind != JsonValueKind.Array)
            {
                errors.Add(new MeshError(name, $"face is {Describe(face)}; a face is an array of vertex ids"));
                result[f++] = [];
                continue;
            }

            var loop = new int[face.GetArrayLength()];
            var k = 0;
            foreach (var index in face.EnumerateArray())
            {
                if (index.ValueKind != JsonValueKind.Number || !index.TryGetInt32(out loop[k]))
                    errors.Add(new MeshError(Invariant($"faces[{f}][{k}]"), $"index is {Describe(index)}, not an integer vertex id"));
                k++;
            }
            result[f++] = loop;
        }
        return result;
    }

    private static string NotJsonMessage(JsonException e)
    {
        // The parser's own words, without its zero-based position suffix; the position is given one-based instead.
        var message = e.Message;
        var cut = message.IndexOf(" LineNumber:", StringComparison.Ordinal);
        if (cut >= 0) message = message[..cut];
        return e.LineNumber is { } line && e.BytePositionInLine is { } column
            ? Invariant($"the text is not JSON: at line {line + 1}, column {column + 1}: {message}")
            : $"the text is not JSON: {message}";
    }

    private static string Kind(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Object => "object",
        JsonValueKind.Array => "array",
        JsonValueKind.String => "string",
        JsonValueKind.Number => "number",
        JsonValueKind.True or JsonValueKind.False => "boolean",
        _ => "null",
    };

    /// <summary>A short account of a value for an error: a number or literal as written, a string, array or object by kind.</summary>
    private static string Describe(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null => e.GetRawText(),
        JsonValueKind.String => "a string",
        JsonValueKind.Array => "an array",
        _ => "an object",
    };

    private static string Versions() => "version " + string.Join(", ", ReadableVersions.Select(n => n.ToString(CultureInfo.InvariantCulture)));

    private static string Invariant(FormattableString s) => s.ToString(CultureInfo.InvariantCulture);

    private static CarveFileResult Refused(params IReadOnlyList<MeshError> errors) => new(null, errors, []);
}
