using System.Globalization;
using System.Text;
using System.Text.Json;
using Carve.Core;

namespace Carve.Server;

/// <summary>
/// A game's limits for what <c>export</c> warns on (D-6, Warnings for a game's limits; T-51): each one optional, and a
/// limit that is not set never warns. They live in the game's content folder, in <see cref="FileName"/>, which export
/// finds by looking in the exported file's folder and then each folder above it; with no file there are no limit
/// warnings. Heights are held as float, as the mesh's coordinates are, so a model exactly at a limit is not past it.
/// </summary>
public sealed record GameLimits(int? MaxTriangles, int? MaxSubmeshes, float? MinHeight, float? MaxHeight)
{
    public const string FileName = "carve-limits.json";

    /// <summary>The keys of the file, in the order they are written.</summary>
    public static readonly IReadOnlyList<string> Keys = ["max_triangles", "max_submeshes", "min_height", "max_height"];

    /// <summary>
    /// Orcward's starting numbers (D-6), which <c>write_limits</c> fills into a game's file where a key is missing. They
    /// are a starting point to edit, never a default: with no file nothing warns.
    /// </summary>
    public static readonly GameLimits Starting = new(5000, 8, 1.4f, 2.4f);

    public static readonly GameLimits None = new(null, null, null, null);

    /// <summary>Each key this one leaves unset taken from <paramref name="fill"/>.</summary>
    public GameLimits FilledFrom(GameLimits fill) => new(
        MaxTriangles ?? fill.MaxTriangles, MaxSubmeshes ?? fill.MaxSubmeshes, MinHeight ?? fill.MinHeight, MaxHeight ?? fill.MaxHeight);

    /// <summary>The keys that are set, in file order.</summary>
    public IReadOnlyList<string> SetKeys => [.. Set.Select(e => e.Key)];

    /// <summary>Each key that is set with its value as the file writes it, in <see cref="Keys"/> order.</summary>
    private IEnumerable<(string Key, string Value)> Set =>
        new (string Key, string? Value)[]
        {
            ("max_triangles", MaxTriangles?.ToString(CultureInfo.InvariantCulture)),
            ("max_submeshes", MaxSubmeshes?.ToString(CultureInfo.InvariantCulture)),
            ("min_height", MinHeight?.ToString(CultureInfo.InvariantCulture)),
            ("max_height", MaxHeight?.ToString(CultureInfo.InvariantCulture)),
        }.Where(e => e.Value is not null).Select(e => (e.Key, e.Value!));

    /// <summary>
    /// The warnings for a model past these limits, each past its limit and never at it: the triangles of the whole model;
    /// its submeshes (the primitives written, a primitive for each colour and each material a piece uses) or its distinct colours; and, only for a skeleton holding every bone of
    /// <see cref="SkinnedExport.SyntyRequiredBones"/>, the rest height of all meshes. A static model has no skeleton, so
    /// it is warned on triangles and submeshes alone. <paramref name="wholeBody"/> is false for a piece (T-50), which
    /// skips the height: a hat is not a humanoid's height.
    /// </summary>
    public IReadOnlyList<string> Warnings(Model model, int triangles, int submeshes, bool wholeBody = true)
    {
        static string N(int x) => x.ToString(CultureInfo.InvariantCulture);
        static string F(float x) => x.ToString(CultureInfo.InvariantCulture);
        var warnings = new List<string>();
        if (triangles > MaxTriangles)
            warnings.Add($"{N(triangles)} triangles, above the game's max_triangles {N(MaxTriangles.Value)}");
        if (MaxSubmeshes is { } most)
        {
            var colours = model.Meshes.SelectMany(m => m.Mesh.Style?.Colours.Values.Select(c => c.ToUpperInvariant()) ?? []).Distinct().Count();
            var materials = model.Meshes.SelectMany(m => Model.MaterialsUsedBy(m.Mesh)).Distinct().Count();
            if (submeshes > most || colours > most)
                warnings.Add(materials == 0
                    ? $"{N(submeshes)} submeshes and {N(colours)} colours, above the game's max_submeshes {N(most)}; share colours between pieces where you can"
                    : $"{N(submeshes)} submeshes, {N(colours)} colours and {N(materials)} materials, above the game's max_submeshes {N(most)}; share colours and materials between pieces where you can");
        }
        if (wholeBody && (MinHeight is not null || MaxHeight is not null)
            && model.Skeleton is { } skeleton && SkinnedExport.SyntyRequiredBones.All(skeleton.Names.Contains))
        {
            var ys = model.Meshes.SelectMany(m => m.Mesh.LiveVertices.Select(v => m.Mesh.Vertices[v])).Select(v => v.Y).ToList();
            if (ys.Count > 0)
            {
                var height = (float)((double)ys.Max() - ys.Min());
                if (height < MinHeight)
                    warnings.Add($"the rest pose is {F(height)} m tall, below the game's min_height {F(MinHeight.Value)} m for a humanoid on the Synty bones");
                else if (height > MaxHeight)
                    warnings.Add($"the rest pose is {F(height)} m tall, above the game's max_height {F(MaxHeight.Value)} m for a humanoid on the Synty bones");
            }
        }
        return warnings;
    }

    /// <summary>
    /// Reads a limits file's text, plain JSON with no comments (write_limits rewrites the file, so a comment would be lost). Null, with <paramref name="error"/> set, when it is not a JSON object, holds a key
    /// that is not one of <see cref="Keys"/>, a count that is not a whole number of at least 1, a height that is not a
    /// number above 0, or a min_height above its max_height.
    /// </summary>
    public static GameLimits? Parse(string text, out string? error)
    {
        error = null;
        JsonDocument document;
        try { document = JsonDocument.Parse(text); }
        catch (JsonException e) { error = "not valid JSON: " + e.Message; return null; }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) { error = "expected a JSON object of " + string.Join(", ", Keys); return null; }
            int? maxTriangles = null, maxSubmeshes = null;
            float? minHeight = null, maxHeight = null;
            var problems = new List<string>();
            foreach (var property in root.EnumerateObject())
            {
                switch (property.Name)
                {
                    case "max_triangles": maxTriangles = Count(property, problems); break;
                    case "max_submeshes": maxSubmeshes = Count(property, problems); break;
                    case "min_height": minHeight = Height(property, problems); break;
                    case "max_height": maxHeight = Height(property, problems); break;
                    default: problems.Add($"{property.Name}: not a limit; the limits are {string.Join(", ", Keys)}"); break;
                }
            }
            var limits = new GameLimits(maxTriangles, maxSubmeshes, minHeight, maxHeight);
            if (problems.Count == 0 && limits.Problem() is { } problem) problems.Add(problem);
            if (problems.Count > 0) { error = string.Join("; ", problems); return null; }
            return limits;
        }
    }

    /// <summary>What makes a set of limits unusable together, or null.</summary>
    public string? Problem() => MinHeight > MaxHeight
        ? $"min_height {MinHeight!.Value.ToString(CultureInfo.InvariantCulture)} is above max_height {MaxHeight!.Value.ToString(CultureInfo.InvariantCulture)}"
        : null;

    /// <summary>The file's text: the keys that are set, in <see cref="Keys"/> order, indented, LF line ends.</summary>
    public string ToFileText()
    {
        var lines = Set.Select(e => $"  \"{e.Key}\": {e.Value}").ToList();
        return lines.Count == 0 ? "{}\n" : "{\n" + string.Join(",\n", lines) + "\n}\n";
    }

    public byte[] ToFileBytes() => new UTF8Encoding(false).GetBytes(ToFileText());

    private static int? Count(JsonProperty property, List<string> problems)
    {
        if (property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetInt32(out var n) && n >= 1) return n;
        problems.Add($"{property.Name}: expected a whole number of at least 1, got {property.Value.GetRawText()}");
        return null;
    }

    private static float? Height(JsonProperty property, List<string> problems)
    {
        if (property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetDouble(out var d) && double.IsFinite(d) && d > 0
            && float.IsFinite((float)d))
            return (float)d;
        problems.Add($"{property.Name}: expected a number of metres above 0, got {property.Value.GetRawText()}");
        return null;
    }
}

/// <summary>
/// Where export's limits come from (T-51): the <c>limits</c> argument (a <c>.json</c> file, or a folder holding
/// <see cref="GameLimits.FileName"/>), else the first <see cref="GameLimits.FileName"/> in the exported file's folder or
/// a folder above it. With <c>write_limits</c>, Orcward's starting numbers are filled in where a key is missing and the
/// file written after the export; when no file was named or found it goes beside the exported file.
/// </summary>
public sealed record LimitsSource(GameLimits Limits, string? Path, bool Found, IReadOnlyList<string> Filled, string SearchedFrom)
{
    /// <summary>The file to write when <c>write_limits</c> filled something, or null.</summary>
    public bool NeedsWrite => Path is not null && (!Found || Filled.Count > 0);

    /// <summary>
    /// Finds and reads the limits for an export to <paramref name="target"/>. Null, with <paramref name="error"/> a whole
    /// tool error, when the named file is missing (and not to be written), is unreadable or is not a valid limits file.
    /// </summary>
    public static LimitsSource? Find(string tool, string? argument, bool write, string target, out string? error, string? baseDirectory = null)
    {
        error = null;
        // A relative argument is looked at from baseDirectory, as ToolPaths.Resolve reads it; null is the working directory.
        string Rooted(string relative) => baseDirectory is null ? relative : System.IO.Path.Combine(baseDirectory, relative);
        var from = System.IO.Path.GetDirectoryName(target)!;
        string? path = null;
        if (argument is not null)
        {
            // The path rules export's own path keeps (ToolPaths.Resolve), so a limits argument that could never be read or
            // written is refused here, before the .glb is written.
            if (string.IsNullOrWhiteSpace(argument))
            {
                error = $"{tool}: limits is empty; give a folder holding {GameLimits.FileName} or a .json file. Nothing was written.";
                return null;
            }
            var trimmed = argument.TrimEnd('/', '\\');
            if (File.Exists(Rooted(argument)) && !argument.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                error = $"{tool}: limits: \"{argument}\" is a file but not a .json one; give a folder holding {GameLimits.FileName} or a .json file. Nothing was written.";
                return null;
            }
            var candidate = trimmed.EndsWith(".json", StringComparison.OrdinalIgnoreCase) && !Directory.Exists(Rooted(trimmed))
                ? argument
                : System.IO.Path.Combine(argument, GameLimits.FileName);
            if (ToolPaths.Resolve($"{tool}: limits", candidate, ".json", out var pathError, baseDirectory, verb: "reads") is not { } resolved)
            {
                error = $"{pathError}. Nothing was written.";
                return null;
            }
            path = resolved.FullPath;
            if (Directory.Exists(path))
            {
                error = $"{tool}: limits: {path} is a folder, not a limits file. Nothing was written.";
                return null;
            }
            // The .glb is written first, so a limits path at or under it could not be written afterwards (RV-41).
            if (string.Equals(path, target, StringComparison.OrdinalIgnoreCase)
                || path.StartsWith(target.TrimEnd(System.IO.Path.DirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                error = $"{tool}: limits: {path} is at or under {target}, the file being exported. Nothing was written.";
                return null;
            }
            for (var above = System.IO.Path.GetDirectoryName(path); above is not null; above = System.IO.Path.GetDirectoryName(above))
            {
                if (File.Exists(above))
                {
                    error = $"{tool}: limits: {above} is a file, so no limits file can be at {path}. Nothing was written.";
                    return null;
                }
            }
            if (!File.Exists(path) && !write)
            {
                error = $"{tool}: limits: no limits file at {path}; write_limits: true creates it with the starting numbers. Nothing was written.";
                return null;
            }
        }
        else
        {
            for (var folder = new DirectoryInfo(from); folder is not null; folder = folder.Parent)
            {
                var candidate = System.IO.Path.Combine(folder.FullName, GameLimits.FileName);
                if (File.Exists(candidate)) { path = candidate; break; }
            }
        }

        var found = path is not null && File.Exists(path);
        var limits = GameLimits.None;
        if (found)
        {
            string text;
            try { text = File.ReadAllText(path!); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                error = $"{tool}: limits file {path}: {e.Message} Nothing was written.";
                return null;
            }
            var read = GameLimits.Parse(text, out var problem);
            if (read is null)
            {
                error = $"{tool}: limits file {path}: {problem}. Nothing was written.";
                return null;
            }
            limits = read;
        }

        if (!write) return new LimitsSource(limits, path, found, [], from);

        path ??= System.IO.Path.Combine(from, GameLimits.FileName);
        var filled = limits.FilledFrom(GameLimits.Starting);
        if (filled.Problem() is { } clash)
        {
            error = $"{tool}: limits file {path}: with the starting numbers filled in, {clash}; set both heights yourself. Nothing was written.";
            return null;
        }
        IReadOnlyList<string> added = [.. filled.SetKeys.Except(limits.SetKeys)];
        return new LimitsSource(filled, path, found, added, from);
    }

    /// <summary>Writes the file when <see cref="NeedsWrite"/>; false with <paramref name="error"/> on an I/O failure.</summary>
    public bool Write(string tool, out string? error)
    {
        error = null;
        if (!NeedsWrite) return true;
        var target = new ToolPath(Path!, System.IO.Path.GetFileName(Path!));
        return ToolPaths.Write(tool, target, Limits.ToFileBytes(), out _, out error);
    }

    /// <summary>What the result line says of the limits, starting with "; ".</summary>
    public string Describe(bool written)
    {
        if (Path is null)
            return $"; no game limits: no {GameLimits.FileName} in {SearchedFrom} or a folder above it (write_limits: true starts one there)";
        var said = $"; limits from {Path}";
        if (written && !Found) said += " (created with the starting numbers)";
        else if (written) said += $" (filled {string.Join(", ", Filled)} with the starting numbers)";
        return said;
    }
}
