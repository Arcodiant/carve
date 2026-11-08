namespace Carve.Core;

/// <summary>One mesh of a <see cref="Model"/> and the name it goes by.</summary>
public sealed record NamedMesh(string Name, PolygonMesh Mesh);

/// <summary>
/// What a model is (D-6): named meshes in the order they were first defined. Immutable: every change returns a new
/// model. Names are non-empty and unique; a mesh keeps its own vertex and face ids. The skeleton and clips arrive in
/// later tasks.
/// </summary>
public sealed class Model
{
    /// <summary>The name a mesh gets when none is given.</summary>
    public const string DefaultName = "mesh";

    /// <summary>The model with no meshes.</summary>
    public static readonly Model Empty = new([]);

    private readonly NamedMesh[] _meshes;
    private PolygonMesh? _combined;

    private Model(NamedMesh[] meshes)
    {
        _meshes = meshes;
        Meshes = Array.AsReadOnly(meshes);
    }

    /// <summary>The meshes in definition order.</summary>
    public IReadOnlyList<NamedMesh> Meshes { get; }

    /// <summary>The names in definition order.</summary>
    public IEnumerable<string> Names => _meshes.Select(m => m.Name);

    /// <summary>True when the model has no meshes.</summary>
    public bool IsEmpty => _meshes.Length == 0;

    /// <summary>The model with one mesh, named <paramref name="name"/>.</summary>
    public static Model Of(string name, PolygonMesh mesh) => Empty.With(name, mesh);

    /// <summary>The mesh called <paramref name="name"/>, or null.</summary>
    public PolygonMesh? Find(string name) => System.Array.Find(_meshes, m => m.Name == name)?.Mesh;

    /// <summary>
    /// The model with <paramref name="mesh"/> under <paramref name="name"/>: it replaces the mesh of that name in its
    /// place, or is added after the others when the name is new.
    /// </summary>
    /// <exception cref="ArgumentException">The name is null or empty.</exception>
    public Model With(string name, PolygonMesh mesh)
    {
        if (NameProblem(name) is { } problem) throw new ArgumentException(problem, nameof(name));
        ArgumentNullException.ThrowIfNull(mesh);
        var at = System.Array.FindIndex(_meshes, m => m.Name == name);
        var next = at >= 0 ? (NamedMesh[])_meshes.Clone() : [.. _meshes, null!];
        next[at >= 0 ? at : next.Length - 1] = new NamedMesh(name, mesh);
        return new Model(next);
    }

    /// <summary>Why <paramref name="name"/> cannot name a mesh, or null when it can.</summary>
    public static string? NameProblem(string? name) =>
        string.IsNullOrEmpty(name) ? "a mesh name must not be empty" : null;

    /// <summary>
    /// The mesh a tool is aimed at: the one named, or the only one when <paramref name="name"/> is null. Null with
    /// <paramref name="problem"/> null when the model is empty; null with a problem naming the meshes when the name is
    /// unknown, or left out while there are several.
    /// </summary>
    public NamedMesh? Target(string? name, out string? problem)
    {
        problem = null;
        if (name is null)
        {
            if (_meshes.Length == 1) return _meshes[0];
            if (_meshes.Length == 0) return null;
            problem = $"mesh: required, since the model has {_meshes.Length} meshes: {ListNames()}";
            return null;
        }

        if (NameProblem(name) is { } bad)
        {
            problem = $"mesh: {bad}";
            return null;
        }

        var found = System.Array.Find(_meshes, m => m.Name == name);
        if (found is null)
        {
            problem = _meshes.Length == 0
                ? $"mesh: no mesh named '{name}'; the model has no meshes yet"
                : $"mesh: no mesh named '{name}'; the model's meshes are {ListNames()}";
        }
        return found;
    }

    /// <summary>The names, quoted and comma-separated, in definition order.</summary>
    public string ListNames() => string.Join(", ", _meshes.Select(m => $"'{m.Name}'"));

    /// <summary>
    /// Where the mesh named <paramref name="name"/> starts in <see cref="Combined"/>: the vertices and faces of the
    /// meshes before it. Null when there is no such mesh.
    /// </summary>
    public (int Vertices, int Faces)? OffsetOf(string name)
    {
        int vertices = 0, faces = 0;
        foreach (var (each, mesh) in _meshes)
        {
            if (each == name) return (vertices, faces);
            vertices += mesh.Vertices.Count;
            faces += mesh.Faces.Count;
        }
        return null;
    }

    /// <summary>
    /// Every mesh as one, for drawing them together and taking bounds over all of them: the meshes' vertices and faces
    /// in definition order, each mesh's ids offset past the ones before. A model of one mesh gives that mesh itself, so
    /// it draws exactly as before; an empty model gives null.
    /// </summary>
    public PolygonMesh? Combined => _meshes.Length switch
    {
        0 => null,
        1 => _meshes[0].Mesh,
        _ => LazyInitializer.EnsureInitialized(ref _combined, Combine),
    };

    private PolygonMesh Combine()
    {
        var vertices = new List<System.Numerics.Vector3>();
        var faces = new List<int[]>();
        foreach (var (_, mesh) in _meshes)
        {
            var offset = vertices.Count;
            vertices.AddRange(mesh.Vertices);
            faces.AddRange(mesh.Faces.Select(f => f.Select(v => v + offset).ToArray()));
        }
        var result = PolygonMesh.Create(vertices, faces);
        return result.Mesh ?? throw new InvalidOperationException(
            "meshes that are each valid did not combine: " + string.Join("; ", result.Errors));
    }
}
