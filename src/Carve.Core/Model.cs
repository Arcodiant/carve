using System.Numerics;

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
    public static readonly Model Empty = new([], null, [], [], []);

    private readonly NamedMesh[] _meshes;
    private readonly Part[] _parts;
    private PolygonMesh? _combined;
    private Part[]? _preOrder;
    private Occurrence[]? _live;

    private readonly Clip[] _clips;
    private readonly Material[] _materials;

    private Model(NamedMesh[] meshes, Skeleton? skeleton, Clip[] clips, Material[] materials, Part[] parts)
    {
        _meshes = meshes;
        _parts = parts;
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
        return new Model(_meshes, Skeleton, _clips, next, _parts);
    }

    /// <summary>This model with <paramref name="source"/>'s part records, for a copy rebuilt mesh by mesh under the same names (<see cref="Skinning.Posed"/>), so it is placed as its source is.</summary>
    internal Model WithPartsFrom(Model source) => new(_meshes, Skeleton, _clips, _materials, source._parts);

    /// <summary>The empty model's meshes with these materials, for a model rebuilt mesh by mesh (<see cref="Skinning.Posed"/>).</summary>
    internal Model WithMaterials(IReadOnlyList<Material> materials) => new(_meshes, Skeleton, _clips, [.. materials], _parts);

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
        return new Model(_meshes, Skeleton, next, _materials, _parts);
    }

    /// <summary>The model with <paramref name="clips"/> in place of its clips.</summary>
    /// <exception cref="ArgumentException">Two clips share a name.</exception>
    public Model WithClips(IReadOnlyList<Clip> clips)
    {
        ArgumentNullException.ThrowIfNull(clips);
        if (clips.GroupBy(c => c.Name).FirstOrDefault(g => g.Count() > 1) is { } twice)
            throw new ArgumentException($"two clips are named '{twice.Key}'; clip names must be unique", nameof(clips));
        return new Model(_meshes, Skeleton, [.. clips], _materials, _parts);
    }

    /// <summary>The model without the clip called <paramref name="name"/> (unchanged when there is none).</summary>
    public Model WithoutClip(string name) => new(_meshes, Skeleton, System.Array.FindAll(_clips, c => c.Name != name), _materials, _parts);

    /// <summary>The meshes in definition order.</summary>
    public IReadOnlyList<NamedMesh> Meshes { get; }

    /// <summary>The skeleton, or null when none has been defined.</summary>
    public Skeleton? Skeleton { get; }

    /// <summary>The model with <paramref name="skeleton"/> (replacing any), its meshes unchanged.</summary>
    public Model WithSkeleton(Skeleton skeleton)
    {
        ArgumentNullException.ThrowIfNull(skeleton);
        return new Model(_meshes, skeleton, _clips, _materials, _parts);
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
    /// <exception cref="ArgumentException">The name is null or empty, a face uses a material the model does not define, or the name is an instance's (T-91).</exception>
    public Model With(string name, PolygonMesh mesh)
    {
        if (NameProblem(name) is { } problem) throw new ArgumentException(problem, nameof(name));
        ArgumentNullException.ThrowIfNull(mesh);
        if (MaterialsUsedBy(mesh).FirstOrDefault(n => FindMaterial(n) is null) is { } undefined)
            throw new ArgumentException($"a face uses the material '{undefined}', which the model does not define", nameof(mesh));
        if (FindPart(name) is { InstanceOf: { } source })
            throw new ArgumentException($"mesh: '{name}' is an instance of '{source}'; edit '{source}', and every instance follows", nameof(name));
        var at = System.Array.FindIndex(_meshes, m => m.Name == name);
        var next = at >= 0 ? (NamedMesh[])_meshes.Clone() : [.. _meshes, null!];
        next[at >= 0 ? at : next.Length - 1] = new NamedMesh(name, mesh);
        // A mesh with no part record yet is a top-level part at the origin (M-9: every mesh is a part).
        return new Model(next, Skeleton, _clips, _materials, FindPart(name) is null ? [.. _parts, Part.Of(name)] : _parts);
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
        IReadOnlyList<Material> materials) => Create(meshes, skeleton, clips, materials, []);

    /// <summary>
    /// <see cref="Create(IReadOnlyList{NamedMesh}, Skeleton?, IReadOnlyList{Clip}, IReadOnlyList{Material})"/> with part records
    /// (T-91), which adds every problem of each part by where it is (<c>parts[3].parent</c>, <c>parts[0].rotation</c>): its own
    /// (<see cref="Part.Problems"/>), a name given twice, and how it sits in the model (<see cref="PartProblems"/>). The parts
    /// come in the order given, then each mesh with no record, in mesh order, as a plain top-level part (<see cref="Part.Of"/>).
    /// </summary>
    public static (Model? Model, IReadOnlyList<MeshError> Errors) Create(IReadOnlyList<NamedMesh> meshes, Skeleton? skeleton, IReadOnlyList<Clip> clips,
        IReadOnlyList<Material> materials, IReadOnlyList<Part> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
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
        // The first part of each name is kept; then each mesh with no record, in mesh order, as a plain top-level part.
        var byName = new Dictionary<string, Part>(StringComparer.Ordinal);
        foreach (var part in parts)
        {
            ArgumentNullException.ThrowIfNull(part, nameof(parts));
            if (NameProblem(part.Name) is null) byName.TryAdd(part.Name, part);
        }
        Part[] all = [.. byName.Values, .. meshes.Where(m => NameProblem(m.Name) is null && !byName.ContainsKey(m.Name)).Select(m => Part.Of(m.Name)).DistinctBy(p => p.Name)];
        byName = all.ToDictionary(p => p.Name, StringComparer.Ordinal);
        var owners = meshes.Select(m => m.Name).ToHashSet(StringComparer.Ordinal);
        for (var i = 0; i < parts.Count; i++)
        {
            var at = $"parts[{i}].";
            errors.AddRange(Part.Problems(parts[i]).Select(p => new MeshError(at + p.Field, p.Problem)));
            if (!byName.TryGetValue(parts[i].Name ?? "", out var kept)) continue;
            if (!ReferenceEquals(kept, parts[i]))
                errors.Add(new MeshError(at + "name", $"a part named '{parts[i].Name}' comes earlier; part names must be unique"));
            else
                errors.AddRange(Placement(parts[i], n => byName.GetValueOrDefault(n), owners.Contains, () => NameList.Quoted(all.Select(p => p.Name)), materialNames.Contains)
                    .Select(p => new MeshError(at + p.Field, p.Problem)));
        }
        return errors.Count > 0 ? (null, errors) : (new Model([.. meshes], skeleton, [.. clips], [.. materials], all), errors);
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
        if (FindPart(name) is { InstanceOf: { } instanced })
        {
            problem = $"mesh: '{name}' is an instance of '{instanced}'; edit '{instanced}', and every instance follows";
            return null;
        }
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
        if (found is null && FindPart(name) is { } part)
        {
            // An instance or an empty is a part with no geometry of its own to edit (M-9).
            problem = part.InstanceOf is { } source
                ? $"mesh: '{name}' is an instance of '{source}'; edit '{source}', and every instance follows"
                : $"mesh: '{name}' is an empty part with no geometry; define_mesh name={name} gives it some";
            return null;
        }
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

    // ---- The part tree (D-2, M-9, T-91) ------------------------------------------------------------------------------

    /// <summary>
    /// Every part in pre-order: each top-level part in definition order, each followed by its children (and theirs), siblings
    /// in definition order. Every named mesh is a part; a mesh with no part record is <see cref="Part.Of"/> its name.
    /// </summary>
    public IReadOnlyList<Part> Parts => LazyInitializer.EnsureInitialized(ref _preOrder, PreOrder);

    /// <summary>The part called <paramref name="name"/> (a named mesh, an empty or an instance), or null.</summary>
    public Part? FindPart(string name) => System.Array.Find(_parts, p => p.Name == name);

    /// <summary>The parent of <paramref name="part"/>, or null for a top-level part (or a parent that does not exist).</summary>
    public Part? ParentOf(Part part) => part.Parent is null ? null : FindPart(part.Parent);

    /// <summary>The geometry <paramref name="part"/> shows: its own, its source's for an instance, or null for an empty.</summary>
    public PolygonMesh? Shown(Part part) => Find(part.InstanceOf ?? part.Name);

    /// <summary>The part names, quoted and comma-separated, in definition order.</summary>
    public string ListPartNames() => NameList.Quoted(_parts.Select(p => p.Name));

    private Part[] PreOrder()
    {
        var children = _parts.Where(p => p.Parent is not null).ToLookup(p => p.Parent!, StringComparer.Ordinal);
        var order = new List<Part>(_parts.Length);
        void Visit(Part p)
        {
            order.Add(p);
            foreach (var child in children[p.Name]) Visit(child);
        }
        foreach (var top in _parts.Where(p => p.Parent is null)) Visit(top);
        return [.. order];
    }

    /// <summary>
    /// What is wrong with <paramref name="part"/> in this model, in place of any part of its name, each by its field: its own
    /// problems (<see cref="Part.Problems"/>), a parent that does not exist or would make the part its own ancestor, an
    /// <c>instance_of</c> on a part that owns geometry or naming a part that has none (or is itself an instance), levels on an
    /// instance or an empty, and a level whose faces use a material the model does not define. Empty when it can be placed.
    /// An existing part's <c>instance_of</c> does not change (the person's ruling, T-93): a part of that name with another
    /// <c>instance_of</c> is refused first, so an instance is not re-pointed or made an empty, and an owner or an empty is not made an instance.
    /// </summary>
    public IReadOnlyList<(string Field, string Problem)> PartProblems(Part part)
    {
        ArgumentNullException.ThrowIfNull(part);
        var own = Part.Problems(part);
        if (own.Any(p => p.Field == "name")) return own;
        IEnumerable<(string Field, string Problem)> changed = FindPart(part.Name) is { } existing && existing.InstanceOf != part.InstanceOf
            ? [("instance_of", existing.InstanceOf is { } was
                ? $"'{part.Name}' is already an instance of '{was}', and an existing part's instance_of does not change"
                : $"'{part.Name}' already exists {(Find(part.Name) is null ? "as an empty part" : "with geometry of its own")}, and an existing part's instance_of does not change")]
            : [];
        return [.. changed, .. own, .. Placement(part, n => n == part.Name ? part : FindPart(n), n => Find(n) is not null,
            ListPartNames, n => FindMaterial(n) is not null)];
    }

    private static IEnumerable<(string Field, string Problem)> Placement(Part part, Func<string, Part?> find, Func<string, bool> owns,
        Func<string> list, Func<string, bool> materialDefined)
    {
        if (part.Parent is { } parent)
        {
            if (find(parent) is null) yield return ("parent", $"no part named '{parent}'; the parts are {list()}");
            else
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                for (var at = parent; at is not null && seen.Add(at); at = find(at)?.Parent)
                    if (at == part.Name)
                    {
                        yield return ("parent", $"'{part.Name}' would be its own ancestor");
                        break;
                    }
            }
        }
        if (part.InstanceOf is { } source)
        {
            if (owns(part.Name)) yield return ("instance_of", "an instance cannot also own geometry");
            else if (source == part.Name) yield return ("instance_of", $"'{part.Name}' cannot be an instance of itself");
            else if (find(source) is not { } shown) yield return ("instance_of", $"no part named '{source}'; the parts are {list()}");
            else if (shown.InstanceOf is { } further)
                yield return ("instance_of", $"'{source}' is itself an instance of '{further}'; name '{further}', the part that owns the geometry");
            else if (!owns(source)) yield return ("instance_of", $"'{source}' has no geometry");
        }
        if (part.Levels.Count > 0)
        {
            if (part.InstanceOf is { } shows)
                yield return ("levels", $"'{part.Name}' is an instance of '{shows}', and shows the levels of '{shows}'; keep levels on '{shows}'");
            else if (!owns(part.Name)) yield return ("levels", $"'{part.Name}' is an empty part with no geometry to keep a level of");
        }
        for (var i = 0; i < part.Levels.Count; i++)
            if (part.Levels[i]?.Mesh is { } mesh && MaterialsUsedBy(mesh).FirstOrDefault(n => !materialDefined(n)) is { } undefined)
                yield return ($"levels[{i}]", $"a face uses the material '{undefined}', which the model does not define");
    }

    /// <summary>The model with <paramref name="part"/>: it replaces the part of that name in its place, or is added after the others (an empty, or an instance).</summary>
    /// <exception cref="ArgumentException">The part has a problem (<see cref="PartProblems"/>), as <c>field: problem</c>.</exception>
    public Model WithPart(Part part) =>
        WithPart(part, out var problem) ?? throw new ArgumentException(problem, nameof(part));

    /// <summary>
    /// The model with <paramref name="part"/>, as <see cref="WithPart(Part)"/>, or null with <paramref name="problem"/> the first
    /// of <see cref="PartProblems"/> as <c>field: problem</c> (<c>parent: no part named 'Axel_F'; the parts are ...</c>).
    /// </summary>
    public Model? WithPart(Part part, out string? problem)
    {
        ArgumentNullException.ThrowIfNull(part);
        problem = PartProblems(part) is [var (field, first), ..] ? $"{field}: {first}" : null;
        if (problem is not null) return null;
        var at = System.Array.FindIndex(_parts, p => p.Name == part.Name);
        var next = at >= 0 ? (Part[])_parts.Clone() : [.. _parts, null!];
        next[at >= 0 ? at : next.Length - 1] = part;
        return new Model(_meshes, Skeleton, _clips, _materials, next);
    }

    /// <summary>The model with the part <paramref name="name"/>'s live mesh kept as level <paramref name="level"/> (as <see cref="WithLevel(string, int, out string?)"/>).</summary>
    /// <exception cref="ArgumentException">The level cannot be kept, as <c>field: problem</c>.</exception>
    public Model WithLevel(string name, int level) =>
        WithLevel(name, level, out var problem) ?? throw new ArgumentException(problem, nameof(level));

    /// <summary>
    /// The model with the live mesh of the part <paramref name="name"/> snapshotted as level <paramref name="level"/> (M-9,
    /// kept levels): N must be 0 or more and at least the part's newest kept level, and an equal N replaces it. Null, with
    /// <paramref name="problem"/> saying why, when there is no such part, it is an instance or an empty, or N is too low.
    /// </summary>
    public Model? WithLevel(string name, int level, out string? problem)
    {
        var part = FindPart(name);
        var newest = part?.NewestLevel?.Number;
        problem = part is null ? $"part: no part named '{name}'; the parts are {ListPartNames()}"
            : part.InstanceOf is { } source ? $"part: '{name}' is an instance of '{source}'; keep a level of '{source}', and every instance shows it"
            : Find(name) is null ? $"part: '{name}' is an empty part with no geometry to keep as a level"
            : level < 0 ? $"level: {level} is negative; a level number is 0 or more"
            : newest > level ? $"level: the newest kept level of '{name}' is {newest}; a new level must be {newest} or more ({newest} replaces it)"
            : null;
        if (problem is not null) return null;
        var kept = new PartLevel(level, Find(name)!);
        PartLevel[] levels = newest == level ? [.. part!.Levels.Take(part.Levels.Count - 1), kept] : [.. part!.Levels, kept];
        return WithPart(part with { Levels = levels });
    }

    /// <summary>The matrix from the part <paramref name="name"/>'s space to the model's (row vectors: a point's model position is <c>Vector3.Transform(p, matrix)</c>), or null when there is no such part.</summary>
    public Matrix4x4? WorldTransform(string name)
    {
        if (FindPart(name) is not { } part) return null;
        var world = part.LocalTransform;
        var seen = new HashSet<string>(StringComparer.Ordinal) { name };
        for (var p = ParentOf(part); p is not null && seen.Add(p.Name); p = ParentOf(p))
            world *= p.LocalTransform;
        return world;
    }

    /// <summary>The newest kept level ≤ <paramref name="level"/> of the part <paramref name="name"/> (an instance's are its source's), or null when it has none or there is no such part.</summary>
    public PolygonMesh? LevelMesh(string name, int level) =>
        FindPart(name) is { } part ? FindPart(part.InstanceOf ?? name)?.LevelAt(level)?.Mesh : null;

    /// <summary>
    /// Every envelope warning (M-9: never a refusal), parts in pre-order: a part's live geometry (every live vertex, in its own
    /// space; an instance's is its source's) leaving its own envelope, then its envelope placed in its parent's space (the
    /// axis-aligned box of its transformed corners) leaving the parent's envelope; each only where both are there to compare.
    /// Axes x, y, z, below before above. On the bound counts as inside (M-4's rule).
    /// </summary>
    public IReadOnlyList<EnvelopeWarning> EnvelopeWarnings()
    {
        var warnings = new List<EnvelopeWarning>();
        foreach (var part in Parts)
        {
            if (part.Envelope is not { } envelope) continue;
            if (Shown(part) is { } mesh)
            {
                var live = Enumerable.Range(0, mesh.Vertices.Count).Where(v => !mesh.IsVertexDeleted(v)).Select(v => mesh.Vertices[v]).ToArray();
                if (live.Length > 0) Leaves(part.Name, null, Bounds(live), envelope, warnings);
            }
            if (part.Parent is { } parent && FindPart(parent)?.Envelope is { } outer)
            {
                var local = part.LocalTransform;
                var corners = Enumerable.Range(0, 8).Select(i => Vector3.Transform(new Vector3(
                    (i & 1) == 0 ? envelope.Min.X : envelope.Max.X, (i & 2) == 0 ? envelope.Min.Y : envelope.Max.Y,
                    (i & 4) == 0 ? envelope.Min.Z : envelope.Max.Z), local)).ToArray();
                Leaves(part.Name, parent, Bounds(corners), outer, warnings);
            }
        }
        return warnings;
    }

    private static PartEnvelope Bounds(Vector3[] points) =>
        new(points.Aggregate(Vector3.Min), points.Aggregate(Vector3.Max));

    private static void Leaves(string part, string? parent, PartEnvelope box, PartEnvelope envelope, List<EnvelopeWarning> warnings)
    {
        foreach (var axis in Part.Axes)
        {
            var below = Part.Component(envelope.Min, axis) - Part.Component(box.Min, axis);
            var above = Part.Component(box.Max, axis) - Part.Component(envelope.Max, axis);
            if (below > 0) warnings.Add(new EnvelopeWarning(part, parent, axis, false, below));
            if (above > 0) warnings.Add(new EnvelopeWarning(part, parent, axis, true, above));
        }
    }

    /// <summary>One drawn occurrence: the part, the mesh it shows, and the matrix that places it in the model.</summary>
    private readonly record struct Occurrence(Part Part, PolygonMesh Mesh, Matrix4x4 World);

    /// <summary>
    /// Every drawn occurrence in pre-order (<see cref="Parts"/>): each part with geometry, and each instance as its source's,
    /// at its own world transform; live meshes when <paramref name="level"/> is null, else each part's <see cref="LevelMesh"/>
    /// (a part with no level ≤ N is not drawn). An empty draws nothing.
    /// </summary>
    private Occurrence[] Occurrences(int? level)
    {
        var result = new List<Occurrence>();
        foreach (var part in Parts)
        {
            var mesh = level is { } n ? LevelMesh(part.Name, n) : Shown(part);
            if (mesh is not null) result.Add(new Occurrence(part, mesh, WorldTransform(part.Name)!.Value));
        }
        return [.. result];
    }

    /// <summary>
    /// Where the part named <paramref name="name"/>'s own occurrence starts in <see cref="Combined"/>: the vertices and faces
    /// of the occurrences before it. Null when there is no such part, or it is an empty and draws nothing.
    /// </summary>
    public (int Vertices, int Faces)? OffsetOf(string name) => OffsetOf(name, null, null);

    /// <summary>
    /// Every drawn occurrence as one mesh placed in the model's space, for drawing them together and taking bounds over all
    /// of them: in pre-order (<see cref="Parts"/>), each part with geometry at its world transform and each instance as a copy
    /// of its source at the instance's, each occurrence's ids offset past the ones before, its colours, materials, UVs, seams
    /// and morph targets with them (a target name on two meshes is one target; deltas turn and scale with the part). A smooth
    /// mesh moved by a transform carries its corner normals by the inverse transpose. A model of untransformed top-level parts
    /// gives exactly what it gave before parts: one mesh gives that mesh itself; an empty model gives null.
    /// </summary>
    public PolygonMesh? Combined => Joined(LazyInitializer.EnsureInitialized(ref _live, () => Occurrences(null)), ref _combined);

    /// <summary>As <see cref="Combined"/> for kept level <paramref name="level"/>: each part's newest kept level ≤ N (<see cref="LevelMesh"/>), a part with none not drawn. Null when nothing is drawn.</summary>
    public PolygonMesh? CombinedAt(int level)
    {
        PolygonMesh? none = null;
        return Joined(Occurrences(level), ref none);
    }

    /// <summary>
    /// T-95: the given meshes joined as <see cref="Combined"/> joins a model's occurrences, each at its transform (row vectors,
    /// part space to the joined space), in the order given: for export, which merges static parts into the node they sit in.
    /// One mesh at the identity gives that mesh itself; none gives null. Weights are not carried (place a single mesh and
    /// put its weights back with <see cref="PolygonMesh.WithWeights"/>).
    /// </summary>
    public static PolygonMesh? Join(IReadOnlyList<(PolygonMesh Mesh, Matrix4x4 Transform)> meshes)
    {
        PolygonMesh? none = null;
        return Joined([.. meshes.Select(m => new Occurrence(Part.Of(""), m.Mesh, m.Transform))], ref none);
    }

    /// <summary>
    /// The part <paramref name="name"/> and every part under it, in pre-order (<see cref="Parts"/>), itself first; empty when
    /// there is no such part (T-94).
    /// </summary>
    public IReadOnlyList<string> SubtreeOf(string name)
    {
        if (FindPart(name) is null) return [];
        bool Under(Part p)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var q = p; q is not null && seen.Add(q.Name); q = ParentOf(q))
                if (q.Name == name) return true;
            return false;
        }
        return [.. Parts.Where(Under).Select(p => p.Name)];
    }

    /// <summary>
    /// What render draws (T-94): <see cref="Combined"/> when <paramref name="part"/> and <paramref name="level"/> are null,
    /// <see cref="CombinedAt"/> for a level, and with a part only the occurrences of its subtree (<see cref="SubtreeOf"/>),
    /// each at its world placement, live or at the level. Null when nothing is drawn.
    /// </summary>
    public PolygonMesh? CombinedOf(string? part, int? level)
    {
        if (part is null) return level is { } n ? CombinedAt(n) : Combined;
        PolygonMesh? none = null;
        return Joined(Drawn(part, level), ref none);
    }

    /// <summary>As <see cref="OffsetOf(string)"/> within <see cref="CombinedOf"/>: where the named part's own occurrence starts in what is drawn, or null when it is not drawn there.</summary>
    public (int Vertices, int Faces)? OffsetOf(string name, string? part, int? level)
    {
        int vertices = 0, faces = 0;
        foreach (var (p, mesh, _) in Drawn(part, level))
        {
            if (p.Name == name) return (vertices, faces);
            vertices += mesh.Vertices.Count;
            faces += mesh.Faces.Count;
        }
        return null;
    }

    /// <summary>
    /// As <see cref="CombinedOf"/> live, with each occurrence of a weighted mesh first placed in the model's space, its weights
    /// kept, and then given to <paramref name="skin"/> to be drawn where it lands: an instance is its own copy at its own place.
    /// This is how the skinned export bakes parts (<c>SkinnedExport.Baked</c>), so a pose skins what the game skins.
    /// </summary>
    internal PolygonMesh? CombinedSkinned(string? part, Func<PolygonMesh, PolygonMesh> skin)
    {
        PolygonMesh? none = null;
        return Joined([.. Drawn(part, null).Select(o => o.Mesh.Weights is not { } weights ? o
            : new Occurrence(o.Part, skin(o.World.IsIdentity ? o.Mesh : Join([(o.Mesh, o.World)])!.WithWeights(weights)), Matrix4x4.Identity))], ref none);
    }

    private Occurrence[] Drawn(string? part, int? level)
    {
        var all = level is null ? LazyInitializer.EnsureInitialized(ref _live, () => Occurrences(null)) : Occurrences(level);
        if (part is null) return all;
        var under = SubtreeOf(part).ToHashSet(StringComparer.Ordinal);
        return [.. all.Where(o => under.Contains(o.Part.Name))];
    }

    private static PolygonMesh? Joined(Occurrence[] occurrences, ref PolygonMesh? cache) => occurrences switch
    {
        [] => null,
        [var only] when only.World.IsIdentity => only.Mesh,
        _ => LazyInitializer.EnsureInitialized(ref cache, () => Combine(occurrences)),
    };

    private static Vector3 Normalised(Vector3 v) => v == Vector3.Zero ? v : Vector3.Normalize(v);

    private static PolygonMesh Combine(Occurrence[] occurrences)
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
        // A smooth mesh moved by a transform gives its own corner normals (the inverse transpose), so they are carried as a posed copy's.
        var posed = occurrences.Any(o => o.Mesh.PosedCorners is not null || (!o.World.IsIdentity && o.Mesh.ShadingMode == ShadingMode.Smooth));
        var smooth = occurrences.Any(o => o.Mesh.ShadingMode == ShadingMode.Smooth);
        foreach (var (_, mesh, world) in occurrences)
        {
            var moved = !world.IsIdentity;
            var normals = Matrix4x4.Identity;
            if (moved && Matrix4x4.Invert(world, out var inverse)) normals = Matrix4x4.Transpose(inverse);
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
                var deltas = target.Offset(offset).Deltas;
                targets[at < 0 ? ^1 : at].Deltas.AddRange(moved ? deltas.Select(d => KeyValuePair.Create(d.Key, Vector3.TransformNormal(d.Value, world))) : deltas);
            }
            if (moved)
                vertices.AddRange(mesh.Vertices.Select((v, i) => mesh.IsVertexDeleted(i) ? v : Vector3.Transform(v, world)));
            else vertices.AddRange(mesh.Vertices);
            deleted.AddRange(Enumerable.Range(0, mesh.Vertices.Count).Select(mesh.IsVertexDeleted));
            faces.AddRange(mesh.Faces.Select(f => f.Map(v => v + offset)));
            if (posed)
                corners.AddRange(moved
                    ? Shading.CornersOf(mesh).Select(face => face.Select(n => Normalised(Vector3.TransformNormal(n, normals))).ToArray())
                    : Shading.CornersOf(mesh));
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

    // ---- Pieces of a part tree (T-92) ---------------------------------------------------------------------------------

    /// <summary>
    /// The piece <c>save mesh=</c> writes (T-92, the person's ruling at M-9's planning): the part <paramref name="name"/>'s own
    /// geometry and kept levels as a top-level part at the origin, without its parent, transform, envelope, children or
    /// instances; the materials its live mesh and its levels use, in the model's order (the person's ruling, 2026-11-16); and
    /// the skeleton and clips that move it when it or a level has weights, with no weights the mesh alone. Null with
    /// <paramref name="problem"/> for an instance or an empty as <see cref="PieceProblem"/> gives it (pointing an instance at
    /// its source to save), or as <see cref="Target"/> gives it for a name that is not a part.
    /// </summary>
    public Model? PartPiece(string name, out string? problem)
    {
        if (PieceProblem(name, "save") is { } notGeometry)
        {
            problem = $"mesh: {notGeometry}";
            return null;
        }
        if (Target(name, out problem) is not { } found)
        {
            problem ??= "mesh: the model has no meshes yet";
            return null;
        }
        var levels = FindPart(name)?.Levels ?? [];
        PolygonMesh[] all = [found.Mesh, .. levels.Select(l => l.Mesh)];
        var used = all.SelectMany(MaterialsUsedBy).ToHashSet(StringComparer.Ordinal);
        var materials = System.Array.FindAll(_materials, m => used.Contains(m.Name));
        Part[] part = [Part.Of(name) with { Levels = levels }];
        return all.All(m => m.Weights is null) ? new Model([found], null, [], materials, part) : new Model([found], Skeleton, _clips, materials, part);
    }

    /// <summary>
    /// This model with the part <paramref name="name"/>'s own geometry and kept levels taken from <paramref name="source"/>, as
    /// <c>open mesh=</c> takes a piece (T-92): it replaces the geometry and levels of this model's part of that name and keeps
    /// that part's placement (parent, transform, envelope, moves), or is added as a new top-level part. Each level comes as
    /// <see cref="WithMeshFrom"/> brings the live mesh (weights by bone name, materials added or matched). Null with
    /// <paramref name="problem"/> when the name is an instance or an empty here or in the file, or as <see cref="WithMeshFrom"/> refuses.
    /// </summary>
    public Model? WithPartPieceFrom(Model source, string name, out string? problem)
    {
        ArgumentNullException.ThrowIfNull(source);
        problem = PieceProblem(this, "the model's", "open into", "every instance follows", name) ?? PieceProblem(source, "the file's", "open", "every instance follows", name);
        if (problem is not null) problem = $"mesh: {problem}";
        if (problem is not null) return null;
        var levels = source.FindPart(name)?.Levels ?? [];
        var target = this;
        var kept = new List<PartLevel>(levels.Count);
        foreach (var level in levels)
        {
            if (target.WithMeshFrom(source.With(name, level.Mesh), name, out problem) is not { } next)
            {
                problem = problem!.Replace("mesh: the file's mesh", FormattableString.Invariant($"mesh: level {level.Number} of the file's mesh"), StringComparison.Ordinal);
                return null;
            }
            target = next;
            kept.Add(new PartLevel(level.Number, target.Find(name)!));
        }
        if (target.WithMeshFrom(source, name, out problem) is not { } placed) return null;
        return placed.WithPart(placed.FindPart(name)! with { Levels = kept });

    }

    /// <summary>
    /// Why a piece of the part <paramref name="name"/> cannot be taken (<c>save mesh=</c>, <c>export mode=piece</c>; <c>open mesh=</c>
    /// says the same): it is an instance, which is pointed at its source to <paramref name="verb"/>, or an empty, with no geometry
    /// of its own. No field prefix. Null when the name is no part, or a part that owns geometry.
    /// </summary>
    public string? PieceProblem(string name, string verb) => PieceProblem(this, "the model's", verb, "every instance shows it", name);

    private static string? PieceProblem(Model model, string whose, string verb, string instances, string name) =>
        model.FindPart(name) is not { } part || model.Find(name) is not null ? null
        : part.InstanceOf is { } shown ? $"{whose} part '{name}' is an instance of '{shown}', with no geometry of its own; {verb} '{shown}', and {instances}"
        : $"{whose} part '{name}' is an empty part with no geometry; a piece is one part's own geometry, so name a part that has some";
}
