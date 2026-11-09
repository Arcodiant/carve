using Carve.Core;

namespace Carve.Server;

/// <summary>One published state of the store: the model and the version it was published as.</summary>
public sealed record ModelSnapshot(Model Model, long Version)
{
    /// <summary>Every mesh of the model as one, as render draws it: the only mesh itself when there is one, null while empty.</summary>
    public PolygonMesh? Mesh => Model.Combined;
}

/// <summary>What <see cref="ModelStore.Undo"/> did: the tool whose change was taken back, and the snapshot now current.</summary>
public sealed record UndoResult(string Undone, ModelSnapshot Current);

/// <summary>
/// The current model and the steps behind it. Written on the MCP thread, read on the frame thread, so it publishes
/// immutable values only: a <see cref="Model"/> never changes, and the model and its
/// version are swapped together as one <see cref="ModelSnapshot"/>. Each step is a whole model, so undo steps across
/// meshes as it does across edits.
/// </summary>
public sealed class ModelStore
{
    /// <summary>How many steps back <see cref="Undo"/> can go: the current model plus this many older ones are kept.</summary>
    public const int UndoDepth = 100;

    private readonly object _write = new();
    private readonly List<(Model Model, string Tool)> _history = [];
    private ModelSnapshot _current = new(Model.Empty, 0);

    /// <summary>
    /// The current mesh (null until the first success) and its version. Version is 0 while empty and
    /// rises by exactly 1 on every change, an undo included; a reader that kept the last version it drew
    /// redraws when <c>Version</c> differs. Safe to read from any thread, never torn.
    /// </summary>
    public ModelSnapshot Current => Volatile.Read(ref _current);

    /// <summary>
    /// Puts <paramref name="mesh"/> in the model under <paramref name="name"/> as the result of <paramref name="tool"/>:
    /// it replaces the mesh of that name, or is added when the name is new. One step, kept for undo.
    /// </summary>
    public ModelSnapshot Define(string name, PolygonMesh mesh, string tool)
    {
        lock (_write)
        {
            return PushLocked(_current.Model.With(name, mesh), tool);
        }
    }

    /// <summary>Puts <paramref name="mesh"/> in the model under the default name as the result of <paramref name="tool"/>.</summary>
    public ModelSnapshot Push(PolygonMesh mesh, string tool) => Define(Model.DefaultName, mesh, tool);

    /// <summary>Puts the mesh under the default name as a <c>define_mesh</c> step and returns the snapshot now current.</summary>
    public ModelSnapshot Replace(PolygonMesh mesh) => Push(mesh, "define_mesh");

    /// <summary>
    /// Like <see cref="Define"/>, but the name is resolved against the model current under the lock: the one named, or
    /// when <paramref name="name"/> is null the only mesh (the default name while the model is empty). Null, with
    /// <paramref name="problem"/> set and nothing pushed, when it is left out while the model has several.
    /// </summary>
    public ModelSnapshot? DefineTarget(string? name, PolygonMesh mesh, string tool, out string? problem)
    {
        lock (_write)
        {
            problem = null;
            var model = _current.Model;
            if (name is null && !model.IsEmpty)
            {
                var target = model.Target(null, out problem);
                if (target is null) return null;
                name = target.Name;
            }
            return PushLocked(model.With(name ?? Model.DefaultName, mesh), tool);
        }
    }

    /// <summary>Makes <paramref name="model"/> (a whole Carve file) the current model as the result of <paramref name="tool"/>: one step, kept for undo.</summary>
    public ModelSnapshot OpenModel(Model model, string tool)
    {
        ArgumentNullException.ThrowIfNull(model);
        lock (_write)
        {
            return PushLocked(model, tool);
        }
    }

    /// <summary>
    /// Takes the mesh <paramref name="name"/> from <paramref name="file"/> into the current model under the lock, as one step
    /// (<see cref="Model.WithMeshFrom"/>). Null, pushing nothing, with <paramref name="problem"/> saying why, when the file
    /// has no such mesh or its weights name bones the current skeleton does not have.
    /// </summary>
    public ModelSnapshot? OpenMesh(Model file, string name, string tool, out string? problem)
    {
        lock (_write)
        {
            var next = _current.Model.WithMeshFrom(file, name, out problem);
            return next is null ? null : PushLocked(next, tool);
        }
    }

    /// <summary>
    /// Runs <paramref name="edit"/> on the current mesh and, if it succeeds, pushes the result, all under the write lock so
    /// two calls cannot edit the same mesh. Returns null (and pushes nothing) when there is no mesh yet; otherwise the snapshot
    /// now current, with the edit's own result in <paramref name="result"/>.
    /// </summary>
    public ModelSnapshot? Edit(string tool, Func<PolygonMesh, EditResult> edit, out EditResult? result) =>
        Edit(tool, null, edit, out result, out _);

    /// <summary>
    /// Runs <paramref name="edit"/> on the mesh <paramref name="name"/> picks (see <see cref="Model.Target"/>) and, if it
    /// succeeds, puts the result back under that name, all under the write lock. Null, pushing nothing, when the model is
    /// empty (<paramref name="problem"/> null) or the name does not pick a mesh (<paramref name="problem"/> says why);
    /// otherwise the snapshot now current, with the edit's own result in <paramref name="result"/>.
    /// </summary>
    public ModelSnapshot? Edit(string tool, string? name, Func<PolygonMesh, EditResult> edit, out EditResult? result, out string? problem)
    {
        lock (_write)
        {
            result = null;
            var model = _current.Model;
            var target = model.Target(name, out problem);
            if (target is null) return null;
            result = edit(target.Mesh);
            return result.Succeeded ? PushLocked(model.With(target.Name, result.Mesh!), tool) : _current;
        }
    }

    /// <summary>
    /// Changes the face colours and groups of the mesh <paramref name="name"/> picks, under the write lock: <paramref name="change"/>
    /// returns the new style, or the problems that refuse it. One step, kept for undo. Null, pushing nothing, when the model is
    /// empty, the name picks no mesh (<paramref name="problem"/> says why) or the change is refused (<paramref name="refused"/>).
    /// </summary>
    public ModelSnapshot? Restyle(string tool, string? name, Func<PolygonMesh, (FaceStyle? Style, List<string> Problems)> change,
        out List<string> refused, out string? problem)
    {
        lock (_write)
        {
            refused = [];
            var model = _current.Model;
            var target = model.Target(name, out problem);
            if (target is null) return null;
            var (style, problems) = change(target.Mesh);
            refused = problems;
            if (style is null || problems.Count > 0) return null;
            return PushLocked(model.With(target.Name, target.Mesh.WithStyle(style)), tool);
        }
    }

    /// <summary>
    /// Sets the skeleton as the result of <paramref name="tool"/>, one step for undo. Once a mesh has weights, a skeleton
    /// whose bone names or order differ (<see cref="Skeleton.SameNamesAs"/>) is refused under the lock: null, nothing
    /// pushed, <paramref name="refusal"/> says why. (Clips, in T-33, join the rule here.)
    /// </summary>
    public ModelSnapshot? DefineSkeleton(Skeleton skeleton, string tool, out string? refusal)
    {
        lock (_write)
        {
            refusal = null;
            var model = _current.Model;
            var weighted = model.Meshes.Where(m => m.Mesh.Weights is not null).Select(m => m.Name).ToArray();
            if (weighted.Length > 0 && !skeleton.SameNamesAs(model.Skeleton))
            {
                refusal = $"bones: the model has weights ({NameList.Quoted(weighted)}), which depend on the bones' names and order, "
                    + $"so the skeleton must keep them: {string.Join(", ", model.Skeleton!.Names)}. Change only the rest transforms, or define the meshes again to drop their weights";
                return null;
            }
            if (model.Clips.Count > 0 && !skeleton.SameNamesAs(model.Skeleton))
            {
                refusal = $"bones: the model has clips ({NameList.Quoted(model.Clips.Select(c => c.Name))}), which depend on the bones' names and order, "
                    + $"so the skeleton must keep them: {string.Join(", ", model.Skeleton!.Names)}. Change only the rest transforms, or delete_clip the clips first";
                return null;
            }
            return PushLocked(model.WithSkeleton(skeleton), tool);
        }
    }

    /// <summary>
    /// Adds the clip <paramref name="build"/> makes from the current skeleton, replacing one of the same name, under the
    /// lock, as one step. Null, pushing nothing, when there is no skeleton or the clip is refused (<paramref name="problems"/>
    /// says why). <paramref name="replaced"/> says whether a clip of that name was there before.
    /// </summary>
    public ModelSnapshot? DefineClip(string tool, Func<Skeleton, (Clip? Clip, IReadOnlyList<MeshError> Errors)> build,
        out Clip? clip, out bool replaced, out List<string> problems)
    {
        lock (_write)
        {
            clip = null;
            replaced = false;
            problems = [];
            var model = _current.Model;
            if (model.Skeleton is not { } skeleton)
            {
                problems.Add("channels: the model has no skeleton yet; call define_skeleton first");
                return null;
            }
            var (made, errors) = build(skeleton);
            problems.AddRange(errors.Select(e => e.ToString()));
            if (made is null) return null;
            clip = made;
            replaced = model.FindClip(made.Name) is not null;
            return PushLocked(model.WithClip(made), tool);
        }
    }

    /// <summary>Removes the clip <paramref name="name"/> as one step; null, with <paramref name="refusal"/>, when there is none.</summary>
    public ModelSnapshot? DeleteClip(string tool, string name, out string? refusal)
    {
        lock (_write)
        {
            refusal = null;
            var model = _current.Model;
            if (model.FindClip(name) is null)
            {
                refusal = model.Clips.Count == 0
                    ? $"name: there is no clip named '{name}'; the model has no clips"
                    : $"name: there is no clip named '{name}'; the clips are {NameList.Quoted(model.Clips.Select(c => c.Name))}";
                return null;
            }
            return PushLocked(model.WithoutClip(name), tool);
        }
    }

    /// <summary>
    /// Gives the vertices <paramref name="vertices"/> of the mesh <paramref name="name"/> picks the influences <paramref name="influences"/>
    /// (a bone's name to its weight), under the lock, as one step. <paramref name="change"/> says how many vertices lost an influence to
    /// the cap and how many are still unweighted. Null, pushing nothing, when the model is empty, the name picks no mesh, or the call is
    /// refused (<paramref name="problems"/> says why).
    /// </summary>
    public ModelSnapshot? SetWeights(string tool, string? name, Func<PolygonMesh, (IReadOnlyList<int>? Ids, List<string> Problems)> vertices,
        IReadOnlyDictionary<string, double> influences, out WeightsChange? change, out List<string> problems)
    {
        lock (_write)
        {
            change = null;
            problems = [];
            var model = _current.Model;
            var target = model.Target(name, out var problem);
            if (problem is not null) problems.Add(problem);
            if (target is null) return null;
            if (model.Skeleton is not { } skeleton)
            {
                problems.Add("weights: the model has no skeleton yet; call define_skeleton first");
                return null;
            }

            var errors = new List<MeshError>();
            var raw = VertexWeights.Raw(skeleton, influences, "weights", errors);
            problems.AddRange(errors.Select(e => e.ToString()));
            var (ids, idProblems) = vertices(target.Mesh);
            problems.AddRange(idProblems);
            if (problems.Count > 0 || ids is null) return null;

            var normal = VertexWeights.Normalise(raw!, out var capped);
            var weights = (target.Mesh.Weights ?? VertexWeights.None).With(ids, normal);
            var mesh = target.Mesh.WithWeights(weights);
            change = new WeightsChange(target.Name, ids.Distinct().Count(), capped ? ids.Distinct().Count() : 0,
                mesh.Vertices.Count - weights.Count, normal);
            return PushLocked(model.With(target.Name, mesh), tool);
        }
    }

    /// <summary>Steps back one change, or null when there is nothing older.</summary>
    public UndoResult? Undo()
    {
        lock (_write)
        {
            if (_history.Count < 2) return null;
            var undone = _history[^1].Tool;
            _history.RemoveAt(_history.Count - 1);
            var next = new ModelSnapshot(_history[^1].Model, _current.Version + 1);
            Volatile.Write(ref _current, next);
            return new UndoResult(undone, next);
        }
    }

    private ModelSnapshot PushLocked(Model model, string tool)
    {
        _history.Add((model, tool));
        if (_history.Count > UndoDepth + 1) _history.RemoveAt(0);
        var next = new ModelSnapshot(model, _current.Version + 1);
        Volatile.Write(ref _current, next);
        return next;
    }
}

/// <summary>What a <c>set_weights</c> did: the mesh, how many vertices were set, how many of those had influences dropped by the cap of four, how many vertices of the mesh are still unweighted, and the normalised influences given.</summary>
public sealed record WeightsChange(string Mesh, int Set, int Capped, int Unweighted, IReadOnlyList<Influence> Influences);
