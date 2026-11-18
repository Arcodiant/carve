namespace Carve.Core;

/// <summary>One mesh of a <see cref="Model"/> and the name it goes by.</summary>
public sealed record NamedMesh(string Name, PolygonMesh Mesh);

/// <summary>
/// What a model is (D-6): named meshes in the order they were first defined. Immutable: every change returns a new
/// model. Names are non-empty, hold no ':' and are unique; a mesh keeps its own vertex and face ids. It may have a Skeleton, kept by every
/// change to the meshes, and named clips (D-6, clips), kept likewise; no two clips share a name. It holds named PBR
/// materials (M-7, T-74) in definition order, kept likewise; any mesh's faces may use any of them by name
/// (<see cref="FaceStyle.MaterialOf"/>), and every material a face uses is defined.
/// </summary>
public sealed class Model
{
    /// <summary>The name a mesh gets when none is given.</summary>
    public const string DefaultName = "mesh";

    /// <summary>The model with no meshes.</summary>
    public static readonly Model Empty = new([], null, [], []);

    private readonly NamedMesh[] _meshes;
    private PolygonMesh? _combined;

    private readonly Clip[] _clips;
    private readonly Material[] _materials;

    private Model(NamedMesh[] meshes, Skeleton? skeleton, Clip[] clips, Material[] materials)
    {
        _meshes = meshes;
        Skeleton = skeleton;
        _clips = clips;
        _materials = materials;
        Meshes = Array.AsReadOnly(meshes);
        Clips = Array.AsReadOnly(clips);
        Materials = Array.AsReadOnly(materials);
    }

    /// <summary>The materials in the order they were first defined (T-74).</summary>
    public IReadOnlyList<Material> Materials { get; }

    /// <summary>The material called <paramref name="name"/>, or null.</summary>
    public Material? FindMaterial(string name) => System.Array.Find(_materials, m => m.Name == name);

    /// <summary>The model with <paramref name="material"/>: it replaces the material of that name in its place, or is added after the others.</summary>
    /// <exception cref="ArgumentException">The material has a problem (<see cref="Material.Problems"/>).</exception>
    public Model WithMaterial(Material material) => WithMaterial(material, out _);

    /// <summary>The model with <paramref name="material"/>, and whether it <paramref name="replaced"/> one of that name (in its place) rather than being added last.</summary>
    /// <exception cref="ArgumentException">The material has a problem (<see cref="Material.Problems"/>).</exception>
    public Model WithMaterial(Material material, out bool replaced)
    {
        ArgumentNullException.ThrowIfNull(material);
        if (Material.Problems(material) is [var (field, problem), ..]) throw new ArgumentException($"{field}: {problem}", nameof(material));
        var at = System.Array.FindIndex(_materials, m => m.Name == material.Name);
        replaced = at >= 0;
        var next = replaced ? (Material[])_materials.Clone() : [.. _materials, null!];
        next[replaced ? at : next.Length - 1] = material;
        return new Model(_meshes, Skeleton, _clips, next);
    }

    /// <summary>The empty model's meshes with these materials, for a model rebuilt mesh by mesh (<see cref="Skinning.Posed"/>).</summary>
    internal Model WithMaterials(IReadOnlyList<Material> materials) => new(_meshes, Skeleton, _clips, [.. materials]);

    /// <summary>The material names, quoted and comma-separated, in definition order.</summary>
    public string ListMaterialNames() => NameList.Quoted(_materials.Select(m => m.Name));

    /// <summary>The material names <paramref name="mesh"/>'s faces use, each once, in the order of the faces that first use them.</summary>
    public static IEnumerable<string> MaterialsUsedBy(PolygonMesh mesh) => mesh.Style?.Materials.Values.Distinct() ?? [];

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
        return new Model(_meshes, Skeleton, next, _materials);
    }

    /// <summary>The model with <paramref name="clips"/> in place of its clips.</summary>
    /// <exception cref="ArgumentException">Two clips share a name.</exception>
    public Model WithClips(IReadOnlyList<Clip> clips)
    {
        ArgumentNullException.ThrowIfNull(clips);
        if (clips.GroupBy(c => c.Name).FirstOrDefault(g => g.Count() > 1) is { } twice)
            throw new ArgumentException($"two clips are named '{twice.Key}'; clip names must be unique", nameof(clips));
        return new Model(_meshes, Skeleton, [.. clips], _materials);
    }

    /// <summary>The model without the clip called <paramref name="name"/> (unchanged when there is none).</summary>
    public Model WithoutClip(string name) => new(_meshes, Skeleton, System.Array.FindAll(_clips, c => c.Name != name), _materials);

    /// <summary>The meshes in definition order.</summary>
    public IReadOnlyList<NamedMesh> Meshes { get; }

    /// <summary>The skeleton, or null when none has been defined.</summary>
    public Skeleton? Skeleton { get; }

    /// <summary>The model with <paramref name="skeleton"/> (replacing any), its meshes unchanged.</summary>
    public Model WithSkeleton(Skeleton skeleton)
    {
        ArgumentNullException.ThrowIfNull(skeleton);
        return new Model(_meshes, skeleton, _clips, _materials);
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
    /// <exception cref="ArgumentException">The name is null or empty, or a face uses a material the model does not define.</exception>
    public Model With(string name, PolygonMesh mesh)
    {
        if (NameProblem(name) is { } problem) throw new ArgumentException(problem, nameof(name));
        ArgumentNullException.ThrowIfNull(mesh);
        if (MaterialsUsedBy(mesh).FirstOrDefault(n => FindMaterial(n) is null) is { } undefined)
            throw new ArgumentException($"a face uses the material '{undefined}', which the model does not define", nameof(mesh));
        var at = System.Array.FindIndex(_meshes, m => m.Name == name);
        var next = at >= 0 ? (NamedMesh[])_meshes.Clone() : [.. _meshes, null!];
        next[at >= 0 ? at : next.Length - 1] = new NamedMesh(name, mesh);
        return new Model(next, Skeleton, _clips, _materials);
    }

    /// <summary>
    /// A whole model from its parts, as the Carve file reads one, or every problem found, each named by where it is
    /// (<c>meshes[1].name</c>, <c>clips[0].name</c>): a mesh name that is empty, holds ':' or is given twice, a mesh with weights while
    /// there is no skeleton or on a bone the skeleton does not have, or two clips of one name. The meshes, skeleton and
    /// clips must each already be valid on their own (made by <see cref="PolygonMesh.Create"/>, <see cref="Skeleton.Create"/>
    /// and <see cref="Clip.Create"/>).
    /// </summary>
    public static (Model? Model, IReadOnlyList<MeshError> Errors) Create(IReadOnlyList<NamedMesh> meshes, Skeleton? skeleton, IReadOnlyList<Clip> clips) =>
        Create(meshes, skeleton, clips, []);

    /// <summary>
    /// <see cref="Create(IReadOnlyList{NamedMesh}, Skeleton?, IReadOnlyList{Clip})"/> with materials (T-74), which adds the
    /// problems of each material (<c>materials[0].roughness</c>), two of one name (<c>materials[1].name</c>), and a face whose
    /// material is not among them (<c>meshes[0].materials</c>).
    /// </summary>
    public static (Model? Model, IReadOnlyList<MeshError> Errors) Create(IReadOnlyList<NamedMesh> meshes, Skeleton? skeleton, IReadOnlyList<Clip> clips,
        IReadOnlyList<Material> materials)
    {
        ArgumentNullException.ThrowIfNull(meshes);
        ArgumentNullException.ThrowIfNull(clips);
        ArgumentNullException.ThrowIfNull(materials);
        var errors = new List<MeshError>();
        var materialNames = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < materials.Count; i++)
        {
            errors.AddRange(Material.Problems(materials[i]).Select(p => new MeshError($"materials[{i}].{p.Field}", p.Problem)));
            if (materials[i].Name is { } n && !materialNames.Add(n))
                errors.Add(new MeshError($"materials[{i}].name", $"a material named '{n}' comes earlier; material names must be unique"));
        }
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
            foreach (var (face, used) in mesh.Style?.Materials ?? new Dictionary<int, string>())
                if (!materialNames.Contains(used))
                    errors.Add(new MeshError($"meshes[{i}].materials", $"face {face} uses the material '{used}', which is not defined"));
        }
        var clipNames = new HashSet<string>();
        for (var i = 0; i < clips.Count; i++)
            if (!clipNames.Add(clips[i].Name))
                errors.Add(new MeshError($"clips[{i}].name", $"two clips are named '{clips[i].Name}'; clip names must be unique"));
        return errors.Count > 0 ? (null, errors) : (new Model([.. meshes], skeleton, [.. clips], [.. materials]), errors);
    }

    /// <summary>
    /// The model a Carve file of the one mesh <paramref name="name"/> holds, so that it opens on its own: that mesh, and
    /// when it has weights the skeleton they name and the clips that move it; with no weights, the mesh alone. It keeps only
    /// the materials that mesh's faces use, in the model's order (the person's ruling, 2026-11-16). Null when there is no
    /// mesh of that name.
    /// </summary>
    public Model? Piece(string name)
    {
        var found = System.Array.Find(_meshes, m => m.Name == name);
        if (found is null) return null;
        var used = MaterialsUsedBy(found.Mesh).ToHashSet(StringComparer.Ordinal);
        var materials = System.Array.FindAll(_materials, m => used.Contains(m.Name));
        return found.Mesh.Weights is null ? new Model([found], null, [], materials) : new Model([found], Skeleton, _clips, materials);
    }

    /// <summary>
    /// This model with the mesh <paramref name="name"/> taken from <paramref name="source"/> (a model a file holds): it
    /// replaces the mesh of that name, or is added after the others. Its weights are moved onto this model's skeleton by
    /// bone name, every weight kept to the bit. The materials its faces use come with it: one this model lacks is added after
    /// the others, and one this model defines the same is kept. Null, with <paramref name="problem"/> saying why, when the
    /// source has no mesh of that name, the mesh has weights on bones this model's skeleton does not have, or it uses a
    /// material this model defines differently under the same name (the person's ruling, 2026-11-16); this model is unchanged.
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
        var target = this;
        foreach (var used in MaterialsUsedBy(mesh))
        {
            var theirs = source.FindMaterial(used)!;
            if (FindMaterial(used) is not { } ours) target = target.WithMaterial(theirs);
            else if (ours != theirs)
            {
                problem = $"mesh: the file's mesh '{name}' uses the material '{used}', which the model defines differently; "
                    + "define one of them again under another name, or open the whole file";
                return null;
            }
        }
        return target.With(name, mesh);
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

    /// <summary>
    /// Every morph target name of the model's meshes, each once, in first-definition order (mesh by mesh, each mesh's in its
    /// own order): the model's dials, since the same name on two meshes is one target (D-6, Morph targets).
    /// </summary>
    public IReadOnlyList<string> TargetNames =>
        _meshes.SelectMany(m => m.Mesh.Targets?.Names ?? []).Distinct(StringComparer.Ordinal).ToArray();

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
    /// in definition order, each mesh's ids offset past the ones before, its colours, materials, UVs, seams and morph targets with them (a target name on two meshes is one target). A model of one mesh gives that mesh itself, so
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
        var materials = new List<KeyValuePair<int, string>>();
        var hard = new List<(int, int)>();
        var corners = new List<System.Numerics.Vector3[]>();
        var flat = new List<bool>();
        var deleted = new List<bool>();
        var uvFaces = new List<KeyValuePair<int, IReadOnlyList<System.Numerics.Vector2>?>>();
        var seams = new List<(int, int)>();
        // One target per name across the meshes (a name is one dial), in first-definition order, each mesh's ids offset.
        var targets = new List<(string Name, List<KeyValuePair<int, System.Numerics.Vector3>> Deltas)>();
        var posed = _meshes.Any(m => m.Mesh.PosedCorners is not null);
        var smooth = _meshes.Any(m => m.Mesh.ShadingMode == ShadingMode.Smooth);
        foreach (var (_, mesh) in _meshes)
        {
            var offset = vertices.Count;
            if (mesh.Style is { } style)
            {
                var looks = style.LooksOffset(faces.Count);
                colours.AddRange(looks.Colours);
                materials.AddRange(looks.Materials);
            }
            if (mesh.Uvs is { } uvs)
            {
                // Each mesh's UVs and seams offset as its faces and vertices are, so a mapped face stays mapped.
                var faceOffset = faces.Count;
                uvFaces.AddRange(uvs.MappedFaces.Select(f => KeyValuePair.Create(f + faceOffset, uvs.Of(f))));
                seams.AddRange(uvs.Seams.Select(e => (e.Lo + offset, e.Hi + offset)));
            }
            foreach (var target in mesh.Targets?.All ?? [])
            {
                var at = targets.FindIndex(t => t.Name == target.Name);
                if (at < 0) targets.Add((target.Name, []));
                targets[at < 0 ? ^1 : at].Deltas.AddRange(target.Offset(offset).Deltas);
            }
            vertices.AddRange(mesh.Vertices);
            deleted.AddRange(Enumerable.Range(0, mesh.Vertices.Count).Select(mesh.IsVertexDeleted));
            faces.AddRange(mesh.Faces.Select(f => f.Map(v => v + offset)));
            if (posed) corners.AddRange(Shading.CornersOf(mesh));
            flat.AddRange(Enumerable.Repeat(mesh.ShadingMode != ShadingMode.Smooth, mesh.Faces.Count));
            if (!smooth) continue;
            // Shading is per mesh: a flat piece of a mixed model has every edge hard, a smooth piece its own.
            var edges = mesh.ShadingMode == ShadingMode.Smooth ? mesh.HardEdges : mesh.Topology.Edges.Select(e => (e.Lo, e.Hi));
            hard.AddRange(edges.Select(e => (e.Item1 + offset, e.Item2 + offset)));
        }
        // Each mesh was checked when it was made (a posed one is a copy to draw, not checked), so they are joined as they are.
        var shade = smooth ? MeshShade.Of(ShadingMode.Smooth, hard) : null;
        // A flat piece keeps its face-centre spikes beside a smooth one, so the combined mesh says which faces are flat.
        var combined = PolygonMesh.Assemble([.. vertices], [.. faces], shade, posed ? [.. corners] : null,
            (smooth || posed) && flat.Contains(true) ? [.. flat] : null, deleted.Contains(true) ? [.. deleted] : null);
        var styled = colours.Count == 0 && materials.Count == 0 ? combined : combined.WithStyle(FaceStyle.OfLooks(colours, materials));
        // No mesh with UVs or seams gives a combined mesh without them, as before.
        var mapped = uvFaces.Count == 0 && seams.Count == 0
            ? styled
            : styled.WithUvs(MeshUvs.None.WithFaces(uvFaces).WithSeams(seams, seam: true));
        return targets.Count == 0 ? mapped : mapped.WithTargets(MorphTargets.Of(targets.Select(t => MorphTarget.Of(t.Name, t.Deltas))));
    }
}
