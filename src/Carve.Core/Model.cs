namespace Carve.Core;

/// <summary>One mesh of a <see cref="Model"/> and the name it goes by.</summary>
public sealed record NamedMesh(string Name, PolygonMesh Mesh);

/// <summary>
/// What a model is (D-6): named meshes in the order they were first defined. Immutable: every change returns a new
/// model. Names are non-empty, hold no ':' and are unique; a mesh keeps its own vertex and face ids. It may have a Skeleton, kept by every
/// change to the meshes, and named clips (D-6, clips), kept likewise; no two clips share a name.
/// </summary>
public sealed class Model
{
    /// <summary>The name a mesh gets when none is given.</summary>
    public const string DefaultName = "mesh";

    /// <summary>The model with no meshes.</summary>
    public static readonly Model Empty = new([], null, []);

    private readonly NamedMesh[] _meshes;
    private PolygonMesh? _combined;

    private readonly Clip[] _clips;

    private Model(NamedMesh[] meshes, Skeleton? skeleton, Clip[] clips)
    {
        _meshes = meshes;
        Skeleton = skeleton;
        _clips = clips;
        Meshes = Array.AsReadOnly(meshes);
        Clips = Array.AsReadOnly(clips);
    }

    /// <summary>The clips in the order they were first defined.</summary>
    public IReadOnlyList<Clip> Clips { get; }

    /// <summary>The clip called <paramref name="name"/>, or null.</summary>
    public Clip? FindClip(string name) => System.Array.Find(_clips, c => c.Name == name);

    /// <summary>The model with <paramref name="clip"/>: it replaces the clip of that name in its place, or is added after the others.</summary>
    public Model WithClip(Clip clip)
    {
        ArgumentNullException.ThrowIfNull(clip);
        var at = System.Array.FindIndex(_clips, c => c.Name == clip.Name);
        var next = at >= 0 ? (Clip[])_clips.Clone() : [.. _clips, null!];
        next[at >= 0 ? at : next.Length - 1] = clip;
        return new Model(_meshes, Skeleton, next);
    }

    /// <summary>The model with <paramref name="clips"/> in place of its clips.</summary>
    /// <exception cref="ArgumentException">Two clips share a name.</exception>
    public Model WithClips(IReadOnlyList<Clip> clips)
    {
        ArgumentNullException.ThrowIfNull(clips);
        if (clips.GroupBy(c => c.Name).FirstOrDefault(g => g.Count() > 1) is { } twice)
            throw new ArgumentException($"two clips are named '{twice.Key}'; clip names must be unique", nameof(clips));
        return new Model(_meshes, Skeleton, [.. clips]);
    }

    /// <summary>The model without the clip called <paramref name="name"/> (unchanged when there is none).</summary>
    public Model WithoutClip(string name) => new(_meshes, Skeleton, System.Array.FindAll(_clips, c => c.Name != name));

    /// <summary>The meshes in definition order.</summary>
    public IReadOnlyList<NamedMesh> Meshes { get; }

    /// <summary>The skeleton, or null when none has been defined.</summary>
    public Skeleton? Skeleton { get; }

    /// <summary>The model with <paramref name="skeleton"/> (replacing any), its meshes unchanged.</summary>
    public Model WithSkeleton(Skeleton skeleton)
    {
        ArgumentNullException.ThrowIfNull(skeleton);
        return new Model(_meshes, skeleton, _clips);
    }

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
        return new Model(next, Skeleton, _clips);
    }

    /// <summary>
    /// A whole model from its parts, as the Carve file reads one, or every problem found, each named by where it is
    /// (<c>meshes[1].name</c>, <c>clips[0].name</c>): a mesh name that is empty, holds ':' or is given twice, a mesh with weights while
    /// there is no skeleton or on a bone the skeleton does not have, or two clips of one name. The meshes, skeleton and
    /// clips must each already be valid on their own (made by <see cref="PolygonMesh.Create"/>, <see cref="Skeleton.Create"/>
    /// and <see cref="Clip.Create"/>).
    /// </summary>
    public static (Model? Model, IReadOnlyList<MeshError> Errors) Create(IReadOnlyList<NamedMesh> meshes, Skeleton? skeleton, IReadOnlyList<Clip> clips)
    {
        ArgumentNullException.ThrowIfNull(meshes);
        ArgumentNullException.ThrowIfNull(clips);
        var errors = new List<MeshError>();
        var names = new HashSet<string>();
        for (var i = 0; i < meshes.Count; i++)
        {
            var (name, mesh) = meshes[i];
            if (NameProblem(name) is { } problem) errors.Add(new MeshError($"meshes[{i}].name", problem));
            else if (!names.Add(name)) errors.Add(new MeshError($"meshes[{i}].name", $"a mesh named '{name}' comes earlier; mesh names must be unique"));
            if (mesh.Weights is { } weights)
            {
                var bones = skeleton?.Bones.Count ?? 0;
                if (skeleton is null) errors.Add(new MeshError($"meshes[{i}].weights", "the model has no skeleton, and weights name its bones"));
                else if (weights.Vertices.SelectMany(weights.Of).Any(x => x.Bone < 0 || x.Bone >= bones))
                    errors.Add(new MeshError($"meshes[{i}].weights", "a weight is on a bone the skeleton does not have"));
            }
        }
        var clipNames = new HashSet<string>();
        for (var i = 0; i < clips.Count; i++)
            if (!clipNames.Add(clips[i].Name))
                errors.Add(new MeshError($"clips[{i}].name", $"two clips are named '{clips[i].Name}'; clip names must be unique"));
        return errors.Count > 0 ? (null, errors) : (new Model([.. meshes], skeleton, [.. clips]), errors);
    }

    /// <summary>
    /// The model a Carve file of the one mesh <paramref name="name"/> holds, so that it opens on its own: that mesh, and
    /// when it has weights the skeleton they name and the clips that move it; with no weights, the mesh alone. Null when
    /// there is no mesh of that name.
    /// </summary>
    public Model? Piece(string name)
    {
        var found = System.Array.Find(_meshes, m => m.Name == name);
        if (found is null) return null;
        return found.Mesh.Weights is null ? new Model([found], null, []) : new Model([found], Skeleton, _clips);
    }

    /// <summary>
    /// This model with the mesh <paramref name="name"/> taken from <paramref name="source"/> (a model a file holds): it
    /// replaces the mesh of that name, or is added after the others. Its weights are moved onto this model's skeleton by
    /// bone name, every weight kept to the bit. Null, with <paramref name="problem"/> saying why, when the source has no mesh
    /// of that name, or the mesh has weights on bones this model's skeleton does not have; this model is unchanged.
    /// </summary>
    public Model? WithMeshFrom(Model source, string name, out string? problem)
    {
        ArgumentNullException.ThrowIfNull(source);
        problem = null;
        var found = System.Array.Find(source._meshes, m => m.Name == name);
        if (found is null)
        {
            problem = source._meshes.Length == 0
                ? $"mesh: the file has no mesh named '{name}'; it has no meshes (a clip library)"
                : $"mesh: the file has no mesh named '{name}'; its meshes are {source.ListNames()}";
            return null;
        }
        var mesh = found.Mesh;
        if (mesh.Weights is { } weights)
        {
            var from = source.Skeleton!.Bones;
            var to = Skeleton?.Names.Select((n, i) => (n, i)).ToDictionary(p => p.n, p => p.i) ?? [];
            var used = weights.Vertices.SelectMany(weights.Of).Select(x => from[x.Bone].Name).Distinct().ToArray();
            var missing = used.Where(b => !to.ContainsKey(b)).ToArray();
            if (missing.Length > 0)
            {
                var lacking = NameList.Quoted(missing);
                problem = Skeleton is null
                    ? $"mesh: the file's mesh '{name}' has weights on the bones {lacking}, and the model has no skeleton; open the whole file, or define_skeleton with those bones first"
                    : $"mesh: the file's mesh '{name}' has weights on bones the model's skeleton does not have: {lacking}; the model's bones are {NameList.Quoted(Skeleton.Names)}";
                return null;
            }
            mesh = mesh.WithWeights(VertexWeights.Of(weights.Vertices.Select(v => KeyValuePair.Create(v,
                weights.Of(v).Select(x => new Influence(to[from[x.Bone].Name], x.Weight)).OrderBy(x => x.Bone).ToArray()))));
        }
        return With(name, mesh);
    }

    /// <summary>
    /// Why <paramref name="name"/> cannot name a mesh, or null when it can: it must not be empty, and must not hold a
    /// colon, so that <c>&lt;mesh&gt;:</c> is an unambiguous prefix of a skinned export's material names.
    /// </summary>
    public static string? NameProblem(string? name) =>
        string.IsNullOrEmpty(name) ? "a mesh name must not be empty"
        : name.Contains(':') ? "a mesh name must not contain ':' (a skinned export names materials <mesh>:<group>)"
        : null;

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
    public string ListNames() => NameList.Quoted(_meshes.Select(m => m.Name));

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
        var faces = new List<Face>();
        var colours = new List<KeyValuePair<int, string>>();
        var hard = new List<(int, int)>();
        var corners = new List<System.Numerics.Vector3[]>();
        var flat = new List<bool>();
        var posed = _meshes.Any(m => m.Mesh.PosedCorners is not null);
        var smooth = _meshes.Any(m => m.Mesh.ShadingMode == ShadingMode.Smooth);
        foreach (var (_, mesh) in _meshes)
        {
            var offset = vertices.Count;
            if (mesh.Style is { } style) colours.AddRange(style.ColoursOffset(faces.Count));
            vertices.AddRange(mesh.Vertices);
            faces.AddRange(mesh.Faces.Select(f => f.Map(v => v + offset)));
            if (posed) corners.AddRange(Shading.CornersOf(mesh));
            flat.AddRange(Enumerable.Repeat(!Shading.HasCornerNormals(mesh), mesh.Faces.Count));
            if (!smooth) continue;
            // Shading is per mesh: a flat piece of a mixed model has every edge hard, a smooth piece its own.
            var edges = mesh.ShadingMode == ShadingMode.Smooth ? mesh.HardEdges : mesh.Topology.Edges.Select(e => (e.Lo, e.Hi));
            hard.AddRange(edges.Select(e => (e.Item1 + offset, e.Item2 + offset)));
        }
        // Each mesh was checked when it was made (a posed one is a copy to draw, not checked), so they are joined as they are.
        var shade = smooth ? MeshShade.Of(ShadingMode.Smooth, hard) : null;
        // A flat piece keeps its face-centre spikes beside a smooth one, so the combined mesh says which faces are flat.
        var combined = PolygonMesh.Assemble([.. vertices], [.. faces], shade, posed ? [.. corners] : null,
            (smooth || posed) && flat.Contains(true) ? [.. flat] : null);
        return colours.Count == 0 ? combined : combined.WithStyle(FaceStyle.OfColours(colours));
    }
}
