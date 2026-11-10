using System.Globalization;
using System.Numerics;

namespace Carve.Core;

/// <summary>
/// The colours and named groups of one mesh's faces (D-6, Colour is a material). Immutable. A colour is sRGB hex,
/// <c>#RRGGBB</c>, kept as given; <see cref="ToLinear"/> converts it for drawing and export. A face without a colour is
/// Carve's surface colour. Groups are named, ordered lists of face ids; a face may be in several.
/// </summary>
public sealed class FaceStyle
{
    private readonly SortedDictionary<int, string> _colours;
    private readonly List<(string Name, int[] Faces)> _groups;

    private FaceStyle(SortedDictionary<int, string> colours, List<(string Name, int[] Faces)> groups)
    {
        _colours = colours;
        _groups = groups;
    }

    /// <summary>The style with no colours and no groups.</summary>
    public static FaceStyle None { get; } = new([], []);

    public bool IsEmpty => _colours.Count == 0 && _groups.Count == 0;

    /// <summary>Face id to its colour as given, for the coloured faces only, in id order.</summary>
    public IReadOnlyDictionary<int, string> Colours => _colours;

    /// <summary>The groups in definition order; a group's faces are in id order.</summary>
    public IReadOnlyList<(string Name, IReadOnlyList<int> Faces)> Groups =>
        _groups.Select(g => (g.Name, (IReadOnlyList<int>)g.Faces)).ToArray();

    public string? ColourOf(int face) => _colours.GetValueOrDefault(face);

    /// <summary>The faces of a group, or null when there is no group of that name.</summary>
    public IReadOnlyList<int>? FacesOf(string group)
    {
        var at = _groups.FindIndex(g => g.Name == group);
        return at < 0 ? null : _groups[at].Faces;
    }

    /// <summary>The group names, quoted and comma-separated.</summary>
    public string GroupNames => NameList.Quoted(_groups.Select(g => g.Name));

    /// <summary>Why <paramref name="name"/> cannot name a group, or null when it can (any text but the empty one).</summary>
    public static string? GroupNameProblem(string name) => name.Length == 0 ? "a group name must not be empty" : null;

    /// <summary>Why <paramref name="face"/> is not a face of a mesh of <paramref name="faceCount"/> faces, or null when it is.</summary>
    public static string? FaceProblem(int faceCount, int face) =>
        face < 0 || face >= faceCount ? $"there is no face {face}: ids run 0..{faceCount - 1}" : null;

    /// <summary>Why <paramref name="text"/> is not a colour, or null when it is <c>#RRGGBB</c>.</summary>
    public static string? ColourProblem(string? text)
    {
        if (text is null || text.Length != 7 || text[0] != '#' || !text.Skip(1).All(char.IsAsciiHexDigit))
            return $"'{text}' is not a colour: write sRGB hex as #RRGGBB, for example #C8503C";
        return null;
    }

    /// <summary>A colour <c>#RRGGBB</c> as sRGB bytes. Throws on malformed text; check with <see cref="ColourProblem"/> first.</summary>
    private static (byte R, byte G, byte B) ToBytes(string hex)
    {
        if (ColourProblem(hex) is { } problem) throw new ArgumentException(problem, nameof(hex));
        byte At(int i) => byte.Parse(hex.AsSpan(1 + 2 * i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return (At(0), At(1), At(2));
    }

    /// <summary>One sRGB channel in [0, 1] as linear light (the IEC 61966-2-1 curve).</summary>
    private static float SrgbToLinear(float c) =>
        c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);

    /// <summary>A colour <c>#RRGGBB</c> as linear RGB with alpha 1.</summary>
    public static Vector4 ToLinear(string hex)
    {
        var (r, g, b) = ToBytes(hex);
        return new Vector4(SrgbToLinear(r / 255f), SrgbToLinear(g / 255f), SrgbToLinear(b / 255f), 1f);
    }

    /// <summary>This style with <paramref name="faces"/> given <paramref name="hex"/>.</summary>
    public FaceStyle WithColour(IEnumerable<int> faces, string hex)
    {
        var colours = new SortedDictionary<int, string>(_colours);
        foreach (var f in faces) colours[f] = hex;
        return new FaceStyle(colours, _groups);
    }

    /// <summary>This style with the group <paramref name="name"/> set to <paramref name="faces"/> (replacing a group of that name in place, or added last).</summary>
    public FaceStyle WithGroup(string name, IEnumerable<int> faces)
    {
        var members = faces.Distinct().Order().ToArray();
        var groups = new List<(string Name, int[] Faces)>(_groups);
        var at = groups.FindIndex(g => g.Name == name);
        if (at >= 0) groups[at] = (name, members); else groups.Add((name, members));
        return new FaceStyle(_colours, groups);
    }

    /// <summary>
    /// The style after an extrude: each wall takes its source face's colour and joins its groups. Faces keep their ids, so
    /// nothing else changes.
    /// </summary>
    internal FaceStyle Carry(IReadOnlyList<ExtrudedFace> extruded) =>
        Carry(extruded.Select(x => (x.Face, x.Walls)).ToList());

    /// <summary>
    /// The style after an edit that appends faces made from existing ones (an extrude's walls, a subdivided face's other
    /// children): each added face takes its source face's colour and joins its groups. Faces keep their ids, so nothing else changes.
    /// </summary>
    internal FaceStyle Carry(IReadOnlyList<(int Face, IReadOnlyList<int> Added)> made)
    {
        if (made.Count == 0) return this;
        var colours = new SortedDictionary<int, string>(_colours);
        var groups = _groups.Select(g => (g.Name, Members: new HashSet<int>(g.Faces), Faces: g.Faces.ToList())).ToList();
        foreach (var (face, added) in made)
        {
            if (_colours.TryGetValue(face, out var hex))
                foreach (var w in added) colours[w] = hex;
            foreach (var g in groups)
                if (g.Members.Contains(face)) g.Faces.AddRange(added);
        }
        return new FaceStyle(colours, groups.Select(g => (g.Name, g.Faces.Order().ToArray())).ToList());
    }

    /// <summary>The colours alone, with every face id moved up by <paramref name="faceOffset"/> (groups are not carried).</summary>
    internal IEnumerable<KeyValuePair<int, string>> ColoursOffset(int faceOffset) =>
        _colours.Select(c => KeyValuePair.Create(c.Key + faceOffset, c.Value));

    /// <summary>A style of these colours and no groups.</summary>
    internal static FaceStyle OfColours(IEnumerable<KeyValuePair<int, string>> colours)
    {
        var all = new SortedDictionary<int, string>();
        foreach (var (face, hex) in colours) all[face] = hex;
        return new FaceStyle(all, []);
    }
}
