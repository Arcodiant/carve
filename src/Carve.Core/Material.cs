using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Globalization;

namespace Carve.Core;

/// <summary>
/// What a texture slot of a <see cref="Material"/> holds (T-160): exactly one of a PNG (<see cref="MaterialTexture"/>) or a
/// texture graph's output (<see cref="MaterialGraph"/>), told apart by type. <see cref="Path"/> is the file's absolute path.
/// </summary>
public abstract record MaterialSlot(string Path);

/// <summary>
/// A PNG texture of a <see cref="Material"/> (M-7, T-74): its absolute path, its bytes as read when the material was
/// defined, and its size from the PNG's <c>IHDR</c>. The bytes are held so that export and render are deterministic: a file
/// changed on disk later does not change the model until the material is defined again or the model is reopened. The Core
/// reads the signature and <c>IHDR</c> only and decodes nothing else. Equal when the path, the size and every byte are.
/// </summary>
public sealed record MaterialTexture(string Path, ImmutableArray<byte> Png, int Width, int Height) : MaterialSlot(Path)
{
    /// <summary>The largest width or height a texture may have.</summary>
    public const int MaxSize = 4096;

    private static readonly byte[] Signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public bool Equals(MaterialTexture? other) =>
        other is not null && Path == other.Path && Width == other.Width && Height == other.Height
        && Png.AsSpan().SequenceEqual(other.Png.AsSpan());

    public override int GetHashCode() => HashCode.Combine(Path, Width, Height, Png.Length);

    /// <summary>
    /// Why <paramref name="bytes"/> are not a PNG a material may use, or null when they are: they start with the PNG
    /// signature, the first chunk is <c>IHDR</c>, and its width and height are each 1 to <see cref="MaxSize"/>.
    /// </summary>
    public static string? PngProblem(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < Signature.Length || !bytes[..Signature.Length].SequenceEqual(Signature))
            return "the file is not a PNG: it does not start with the PNG signature";
        if (bytes.Length < 24 || bytes[12] != 'I' || bytes[13] != 'H' || bytes[14] != 'D' || bytes[15] != 'R')
            return "the PNG is malformed: its first chunk is not IHDR";
        var (width, height) = SizeOf(bytes);
        if (width < 1 || width > MaxSize || height < 1 || height > MaxSize)
            return string.Create(CultureInfo.InvariantCulture,
                $"the PNG is {width} x {height}; a texture is 1 to {MaxSize} pixels on each side");
        return null;
    }

    private static (long Width, long Height) SizeOf(ReadOnlySpan<byte> bytes) =>
        (BinaryPrimitives.ReadUInt32BigEndian(bytes[16..]), BinaryPrimitives.ReadUInt32BigEndian(bytes[20..]));

    /// <summary>
    /// The texture at <paramref name="absolutePath"/> with <paramref name="bytes"/>, or null with <paramref name="problem"/>
    /// saying why: the path is not absolute, or <see cref="PngProblem"/>.
    /// </summary>
    public static MaterialTexture? FromPng(string absolutePath, byte[] bytes, out string? problem)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (string.IsNullOrEmpty(absolutePath) || !System.IO.Path.IsPathFullyQualified(absolutePath))
        {
            problem = $"'{absolutePath}' is not an absolute path";
            return null;
        }
        problem = PngProblem(bytes);
        if (problem is not null) return null;
        var (width, height) = SizeOf(bytes);
        return new MaterialTexture(absolutePath, [.. bytes], (int)width, (int)height);
    }

    /// <summary>
    /// The texture read from <paramref name="absolutePath"/>, or null with <paramref name="problem"/> saying why (the file
    /// cannot be read, or <see cref="FromPng"/>'s problems). The Core's only file read.
    /// </summary>
    public static MaterialTexture? Load(string absolutePath, out string? problem)
    {
        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(absolutePath);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            problem = e.Message;
            return null;
        }
        return FromPng(absolutePath, bytes, out problem);
    }
}

/// <summary>
/// A texture slot drawn from a texture graph (T-160): the <c>.texgraph</c> file's absolute path, the output's name and the
/// size it is evaluated at. Three plain values, equal by value: the Core holds no graph content, and the graph is read again
/// at each render and export (M-29's planning), unlike a PNG, whose bytes are held.
/// </summary>
public sealed record MaterialGraph(string Path, string Output, int Size) : MaterialSlot(Path)
{
    /// <summary>The extension of a texture graph file.</summary>
    public const string Extension = ".texgraph";

    /// <summary>The smallest size a graph slot may have; the largest is <see cref="MaterialTexture.MaxSize"/>.</summary>
    public const int MinSize = 64;

    /// <summary>Why <paramref name="size"/> cannot be a graph slot's size, or null when it is a power of two from 64 to 4096.</summary>
    public static string? SizeProblem(int size) =>
        size >= MinSize && size <= MaterialTexture.MaxSize && int.IsPow2(size) ? null
        : string.Create(CultureInfo.InvariantCulture, $"size is {size}; it is a power of two from {MinSize} to {MaterialTexture.MaxSize}");

    /// <summary>Why <paramref name="output"/> cannot name a graph output, or null when it matches <c>[a-z][a-z0-9_]*</c>.</summary>
    public static string? OutputProblem(string? output) =>
        !string.IsNullOrEmpty(output) && output[0] is >= 'a' and <= 'z' && output.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '_') ? null
        : $"output '{output}' is not an output name: a lowercase letter, then lowercase letters, digits and '_'";

    /// <summary>Why this reference is malformed, or null: its path is absolute and ends in .texgraph, its output and size are well formed.</summary>
    public string? Problem() =>
        !System.IO.Path.IsPathFullyQualified(Path ?? "") ? $"'{Path}' is not an absolute path"
        : !Path!.EndsWith(Extension, StringComparison.OrdinalIgnoreCase) ? $"'{Path}' does not end in {Extension}"
        : OutputProblem(Output) ?? SizeProblem(Size);
}

/// <summary>
/// A named PBR material of a <see cref="Model"/> (M-7, T-74): every field the engine's glTF loader reads, each default
/// glTF's. Colours are sRGB hex <c>#RRGGBB</c>, kept as given, as <see cref="FaceStyle"/> keeps them. Immutable; check one
/// with <see cref="Problems"/>. Materials belong to the model, and any mesh's faces may use any of them.
/// </summary>
public sealed record Material(
    string Name,
    string BaseColour = Material.DefaultBaseColour,
    float Metallic = Material.DefaultMetallic,
    float Roughness = Material.DefaultRoughness,
    string Emissive = Material.DefaultEmissive,
    float Occlusion = Material.DefaultOcclusion,
    bool DoubleSided = false,
    MaterialSlot? BaseColourTexture = null,
    MaterialSlot? MetallicRoughnessTexture = null,
    MaterialSlot? NormalTexture = null,
    MaterialSlot? EmissiveTexture = null,
    MaterialSlot? OcclusionTexture = null)
{
    public const string DefaultBaseColour = "#FFFFFF";
    public const string DefaultEmissive = "#000000";
    public const float DefaultMetallic = 0f;
    public const float DefaultRoughness = 1f;
    public const float DefaultOcclusion = 1f;

    /// <summary>The name an export gives its colour materials' prefix, which no material may take.</summary>
    public const string ReservedName = "carve";

    /// <summary>The five texture slots, each with its field name in the file and the tools, in record order (null when unset): a PNG or a graph output.</summary>
    public IReadOnlyList<(string Field, MaterialSlot? Texture)> Textures =>
    [
        ("base_colour_texture", BaseColourTexture),
        ("metallic_roughness_texture", MetallicRoughnessTexture),
        ("normal_texture", NormalTexture),
        ("emissive_texture", EmissiveTexture),
        ("occlusion_texture", OcclusionTexture),
    ];

    /// <summary>True when any of the five texture slots is set, by a PNG or a graph.</summary>
    public bool HasTexture => Textures.Any(t => t.Texture is not null);

    /// <summary>True when the occlusion strength is not 1 and there is no occlusion map, so it has no effect (glTF scales only the map).</summary>
    public bool OcclusionHasNoEffect => Occlusion != 1f && OcclusionTexture is null;

    /// <summary>True when any texture slot is a graph output (T-160).</summary>
    public bool HasGraph => Textures.Any(t => t.Texture is MaterialGraph);

    /// <summary>
    /// Why <paramref name="name"/> cannot name a material, or null when it can: not empty, no ':', '#' or '@' (T-160), and not
    /// <c>carve</c>, so it never takes the name of an exported colour material (<c>carve</c>, <c>carve #rrggbb</c>,
    /// <c>&lt;mesh&gt;:&lt;group&gt;</c>, <c>&lt;mesh&gt;:&lt;rrggbb&gt;</c>). '@' is refused only in a new name
    /// (<paramref name="existing"/> false): a file that already has one still opens, and its faces can still be given it (T-160).
    /// </summary>
    public static string? NameProblem(string? name, bool existing = false) =>
        string.IsNullOrEmpty(name) ? "a material name must not be empty"
        : name.Contains(':') ? "a material name must not contain ':' (an export names colour materials <mesh>:<group>)"
        : name.Contains('#') ? "a material name must not contain '#' (an export names colour materials carve #rrggbb)"
        : !existing && name.Contains('@') ? "a material name must not contain '@' (an export names a material's per-mesh copies <material>@<mesh>)"
        : string.Equals(name, ReservedName, StringComparison.Ordinal) ? $"a material must not be named '{ReservedName}': an export names its colour material that"
        : null;

    /// <summary>Why <paramref name="value"/> cannot be the factor <paramref name="field"/>, or null when it is finite and in [0, 1].</summary>
    public static string? FactorProblem(string field, float value) =>
        float.IsFinite(value) && value >= 0f && value <= 1f ? null
        : string.Create(CultureInfo.InvariantCulture, $"{field} is {value.ToString("R", CultureInfo.InvariantCulture)}; it is a number from 0 to 1");

    /// <summary>Every problem of <paramref name="material"/> (its name by the rule for an existing one; a graph slot by its shape only), each named by its field in snake case (<c>name</c>, <c>base_colour</c>, <c>metallic</c>, <c>normal_texture</c>), in record order.</summary>
    public static IReadOnlyList<(string Field, string Problem)> Problems(Material material)
    {
        ArgumentNullException.ThrowIfNull(material);
        var problems = new List<(string, string)>();
        if (NameProblem(material.Name, existing: true) is { } name) problems.Add(("name", name));
        if (FaceStyle.ColourProblem(material.BaseColour) is { } baseColour) problems.Add(("base_colour", baseColour));
        if (FactorProblem("metallic", material.Metallic) is { } metallic) problems.Add(("metallic", metallic));
        if (FactorProblem("roughness", material.Roughness) is { } roughness) problems.Add(("roughness", roughness));
        if (FaceStyle.ColourProblem(material.Emissive) is { } emissive) problems.Add(("emissive", emissive));
        if (FactorProblem("occlusion", material.Occlusion) is { } occlusion) problems.Add(("occlusion", occlusion));
        foreach (var (field, texture) in material.Textures)
        {
            if (texture is null) continue;
            if (texture is MaterialGraph graph)
            {
                if (graph.Problem() is { } shape) problems.Add((field, shape));
                continue;
            }
            var png = (MaterialTexture)texture;
            if (MaterialTexture.PngProblem(png.Png.IsDefault ? [] : png.Png.AsSpan()) is { } bad) problems.Add((field, bad));
            else if (!Path.IsPathFullyQualified(texture.Path ?? "")) problems.Add((field, $"'{texture.Path}' is not an absolute path"));
        }
        return problems;
    }
}
