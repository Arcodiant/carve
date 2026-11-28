using System.Collections.Immutable;
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
    private readonly List<(Model Model, string Tool, ImmutableList<JournalEntry> Journal)> _history = [];
    private ModelSnapshot _current = new(Model.Empty, 0);
    private ImmutableList<JournalEntry> _journal = [];

    // What another flow sees: _current and _journal as of the last step outside a batch (T-233), so a render never sees a half-run script.
    private ModelSnapshot _published = new(Model.Empty, 0);
    private ImmutableList<JournalEntry> _publishedJournal = [];
    private readonly AsyncLocal<bool> _inBatch = new();

    /// <summary>
    /// The calls that made the current model (T-231): every tool call that pushed a step, since the last whole-model
    /// <c>open</c> or <c>clear</c> (which is the first entry), or since the process started. Undo takes the step's entries
    /// out again. Not capped at <see cref="UndoDepth"/>; never saved. Immutable, so safe to read from any thread.
    /// </summary>
    public IReadOnlyList<JournalEntry> Journal => _inBatch.Value ? _journal : Volatile.Read(ref _publishedJournal);

    /// <summary>
    /// The current mesh (null until the first success) and its version. Version is 0 while empty and
    /// rises by exactly 1 on every change, an undo included; a reader that kept the last version it drew
    /// redraws when <c>Version</c> differs. Safe to read from any thread, never torn.
    /// </summary>
    public ModelSnapshot Current => _inBatch.Value ? _current : Volatile.Read(ref _published);

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

    /// <summary>
    /// As <see cref="Define(string, PolygonMesh, string)"/>, unless <paramref name="refuse"/> gives a reason for the current model, checked
    /// under the same lock (define_mesh's refusal to drop morph targets, as open's is): then null, pushing nothing, with that reason.
    /// </summary>
    public ModelSnapshot? Define(string name, PolygonMesh mesh, string tool, Func<Model, string?> refuse, out string? problem)
    {
        lock (_write)
        {
            problem = refuse(_current.Model);
            return problem is null ? PushLocked(_current.Model.With(name, mesh), tool) : null;
        }
    }

    /// <summary>Puts <paramref name="mesh"/> in the model under the default name as the result of <paramref name="tool"/>.</summary>
    public ModelSnapshot Push(PolygonMesh mesh, string tool) => Define(Model.DefaultName, mesh, tool);

    /// <summary>Puts the mesh under the default name as a <c>define_mesh</c> step and returns the snapshot now current.</summary>
    public ModelSnapshot Replace(PolygonMesh mesh) => Push(mesh, "define_mesh");

    /// <summary>Makes <paramref name="model"/> (a whole Carve file) the current model as the result of <paramref name="tool"/>: one step, kept for undo.</summary>
    public ModelSnapshot OpenModel(Model model, string tool)
    {
        ArgumentNullException.ThrowIfNull(model);
        lock (_write)
        {
            return PushLocked(model, tool, restart: true);
        }
    }

    /// <summary>
    /// As <see cref="OpenModel(Model, string)"/>, unless <paramref name="refuse"/> gives a reason for the current model, checked
    /// under the same lock (open's refusal to replace morph targets, T-101): then null, pushing nothing, with that reason.
    /// </summary>
    public ModelSnapshot? OpenModel(Model model, string tool, Func<Model, string?> refuse, out string? problem)
    {
        ArgumentNullException.ThrowIfNull(model);
        lock (_write)
        {
            problem = refuse(_current.Model);
            return problem is null ? PushLocked(model, tool, restart: true) : null;
        }
    }

    /// <summary>
    /// Takes the part <paramref name="name"/> from <paramref name="file"/> into the current model under the lock, as one step (T-92),
    /// unless <paramref name="refuse"/> gives a reason for the current model, checked under the same lock: the part's own geometry
    /// and kept levels, keeping the placement of the model's part of that name (<see cref="Model.WithPartPieceFrom"/>). Null,
    /// pushing nothing, with <paramref name="problem"/> saying why.
    /// </summary>
    public ModelSnapshot? OpenPiece(Model file, string name, string tool, Func<Model, string?> refuse, out string? problem)
    {
        lock (_write)
        {
            problem = refuse(_current.Model);
            if (problem is not null) return null;
            var next = _current.Model.WithPartPieceFrom(file, name, out problem);
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
        out List<string> refused, out string? problem) =>
        Change(tool, name, change, (mesh, style) => mesh.WithStyle(style), out refused, out problem);

    /// <summary>
    /// As <see cref="Restyle(string, string?, Func{PolygonMesh, ValueTuple{FaceStyle, List{string}}}, out List{string}, out string)"/>, with the model beside the mesh,
    /// for a change that needs the model's materials (set_material).
    /// </summary>
    public ModelSnapshot? Restyle(string tool, string? name, Func<Model, PolygonMesh, (FaceStyle? Style, List<string> Problems)> change,
        out List<string> refused, out string? problem)
    {
        lock (_write)
        {
            refused = [];
            var model = _current.Model;
            var target = model.Target(name, out problem);
            if (target is null) return null;
            var (style, problems) = change(model, target.Mesh);
            refused = problems;
            if (style is null || problems.Count > 0) return null;
            return PushLocked(model.With(target.Name, target.Mesh.WithStyle(style)), tool);
        }
    }

    /// <summary>
    /// Defines <paramref name="material"/> in the model under the lock, replacing the one of that name in place or appending, as one step.
    /// Null, pushing nothing, with <paramref name="problems"/>, when the material has a problem (<see cref="Material.Problems"/>).
    /// <paramref name="replaced"/> says whether a material of that name was there before.
    /// </summary>
    public ModelSnapshot? DefineMaterial(string tool, Material material, out bool replaced, out List<string> problems)
    {
        lock (_write)
        {
            replaced = false;
            problems = Material.Problems(material).Select(p => $"{p.Field}: {p.Problem}").ToList();
            if (problems.Count > 0) return null;
            var next = _current.Model.WithMaterial(material, out replaced);
            return PushLocked(next, tool);
        }
    }

    /// <summary>
    /// Changes the shading (mode and hard edges) of the mesh <paramref name="name"/> picks, under the write lock, as
    /// <see cref="Restyle"/> does for the style: <paramref name="change"/> returns the new shade, or the problems that refuse it.
    /// </summary>
    public ModelSnapshot? Reshade(string tool, string? name, Func<PolygonMesh, (MeshShade? Shade, List<string> Problems)> change,
        out List<string> refused, out string? problem) =>
        Change(tool, name, change, (mesh, shade) => mesh.WithShade(shade), out refused, out problem);

    /// <summary>
    /// Replaces the mesh <paramref name="name"/> picks with the one <paramref name="change"/> returns (or the problems that refuse it),
    /// under the write lock and as one step for undo: for an edit that changes more than one attribute (set_uvs, mark_edges).
    /// </summary>
    public ModelSnapshot? Rework(string tool, string? name, Func<PolygonMesh, (PolygonMesh? Mesh, List<string> Problems)> change,
        out List<string> refused, out string? problem) =>
        Change(tool, name, change, (_, mesh) => mesh, out refused, out problem);

    private ModelSnapshot? Change<T>(string tool, string? name, Func<PolygonMesh, (T? Value, List<string> Problems)> change,
        Func<PolygonMesh, T, PolygonMesh> apply, out List<string> refused, out string? problem)
        where T : class
    {
        lock (_write)
        {
            refused = [];
            var model = _current.Model;
            var target = model.Target(name, out problem);
            if (target is null) return null;
            var (value, problems) = change(target.Mesh);
            refused = problems;
            if (value is null || problems.Count > 0) return null;
            return PushLocked(model.With(target.Name, apply(target.Mesh, value)), tool);
        }
    }

    /// <summary>
    /// Sets the skeleton as the result of <paramref name="tool"/>, one step for undo. Once a mesh has weights or the model
    /// has clips, a skeleton whose bone names or order differ (<see cref="Skeleton.SameNamesAs"/>) is refused under the
    /// lock: null, nothing pushed, <paramref name="refusal"/> says why.
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
                mesh.LiveVertexCount - weights.Count, normal);
            return PushLocked(model.With(target.Name, mesh), tool);
        }
    }

    /// <summary>
    /// Weighs the mesh <paramref name="name"/> picks automatically (<see cref="AutoWeights.Weigh"/>), limited to the bones named in
    /// <paramref name="bones"/> when given, under the lock, as one step. Null, pushing nothing, when the model is empty, the name picks
    /// no mesh, there is no skeleton or a bone name is unknown (<paramref name="problems"/> says why).
    /// </summary>
    public ModelSnapshot? Weigh(string tool, string? name, IReadOnlyList<string>? bones, bool replace, out (string Mesh, WeighOutcome Outcome)? change,
        out List<string> problems)
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
                problems.Add("weigh: the model has no skeleton yet; call define_skeleton first");
                return null;
            }

            List<int>? indices = null;
            if (bones is not null)
            {
                var names = skeleton.Names.ToList();
                indices = [];
                foreach (var b in bones)
                {
                    var i = names.IndexOf(b);
                    if (i < 0) problems.Add($"bones: no bone named '{b}'; the bones are {NameList.Quoted(names)}");
                    else indices.Add(i);
                }
            }
            if (problems.Count > 0) return null;

            // The skeleton is in model space: a placed part is weighed where it is drawn, as the skinned export bakes it (RV-71).
            var placed = model.WorldTransform(target.Name) is { IsIdentity: false } world
                ? Model.Join([(target.Mesh, world)])!.WithWeights(target.Mesh.Weights)
                : target.Mesh;
            var weights = AutoWeights.Weigh(placed, skeleton, indices, replace, out var outcome);
            change = (target.Name, outcome);
            return PushLocked(model.With(target.Name, target.Mesh.WithWeights(weights)), tool);
        }
    }

    /// <summary>
    /// Runs <paramref name="change"/> on the current model under the write lock and pushes the model it gives as one step of
    /// <paramref name="tool"/> (the part tools, T-93). Null, pushing nothing, with <paramref name="problem"/> saying why, when
    /// it gives no model.
    /// </summary>
    public ModelSnapshot? Change(string tool, Func<Model, (Model? Next, string? Problem)> change, out string? problem)
    {
        lock (_write)
        {
            (var next, problem) = change(_current.Model);
            return next is null ? null : PushLocked(next, tool);
        }
    }

    /// <summary>Empties the model (<see cref="Model.Empty"/>) as one step, an empty model included, and starts a new journal with the call (<c>clear</c>).</summary>
    public ModelSnapshot Clear(string tool)
    {
        lock (_write)
        {
            return PushLocked(Model.Empty, tool, restart: true);
        }
    }

    /// <summary>
    /// The seam for a step made of several calls (T-233's run_script): <paramref name="change"/> as <see cref="Change(string, Func{Model, ValueTuple{Model, string}}, out string)"/>,
    /// journalled as <paramref name="entries"/> instead of the call under way, appended, or starting a new journal when
    /// <paramref name="restart"/>. Still one step, which one undo takes back with all its entries.
    /// </summary>
    public ModelSnapshot? Change(string tool, Func<Model, (Model? Next, string? Problem)> change, IReadOnlyList<JournalEntry> entries, bool restart, out string? problem)
    {
        lock (_write)
        {
            (var next, problem) = change(_current.Model);
            return next is null ? null : PushLocked(next, tool, restart, entries);
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
            Set(next, _history[^1].Journal);
            return new UndoResult(undone, next);
        }
    }

    /// <summary>
    /// Pushes the step, journalling <paramref name="entries"/> or else the tool call under way (none outside a call, as when
    /// a test drives the store), appended or, when <paramref name="restart"/>, as a new journal. A call pushes at most once.
    /// </summary>
    private ModelSnapshot PushLocked(Model model, string tool, bool restart = false, IReadOnlyList<JournalEntry>? entries = null)
    {
        var call = JournalCall.Current;
        if (call is not null)
        {
            if (call.Pushed) throw new InvalidOperationException($"{call.Tool} pushed a second undo step in one call; a call makes at most one");
            call.Pushed = true;
        }
        entries ??= call is null ? [] : [call.Entry()];
        var journal = (restart ? ImmutableList<JournalEntry>.Empty : _journal).AddRange(entries);
        _history.Add((model, tool, journal));
        if (_history.Count > UndoDepth + 1) _history.RemoveAt(0);
        var next = new ModelSnapshot(model, _current.Version + 1);
        Set(next, journal);
        return next;
    }

    /// <summary>Makes <paramref name="next"/> and <paramref name="journal"/> current, and publishes them to other flows unless a batch is under way on this one.</summary>
    private void Set(ModelSnapshot next, ImmutableList<JournalEntry> journal)
    {
        _current = next;
        _journal = journal;
        if (_inBatch.Value) return;
        Volatile.Write(ref _publishedJournal, journal);
        Volatile.Write(ref _published, next);
    }

    /// <summary>
    /// Runs <paramref name="run"/> as one step of <paramref name="tool"/>, all or nothing (T-233's run_script). It holds the
    /// write lock throughout, so no other call interleaves, and <paramref name="run"/> must finish on this thread (the lock
    /// is the thread's: the tools it calls take it again). Inside, each call's step is pushed as usual and seen by this flow
    /// only; no other flow (the frame thread, another call) sees any of it. When <paramref name="run"/> gives a problem or
    /// throws, the model, history, journal and version are put back as they were; otherwise the steps are folded into one,
    /// journalled as the entries they made (a clear or whole-model open among them restarting it), published as one version.
    /// </summary>
    public ModelSnapshot? Batch(string tool, Func<string?> run, out string? problem)
    {
        lock (_write)
        {
            if (_inBatch.Value) throw new InvalidOperationException($"{tool}: a batch inside a batch");
            var (current, journal, history) = (_current, _journal, _history.ToList());
            Model model;
            ImmutableList<JournalEntry> entries;
            try
            {
                _inBatch.Value = true;
                problem = run();
            }
            finally
            {
                _inBatch.Value = false;
                (model, entries) = (_current.Model, _journal);
                (_current, _journal) = (current, journal);
                _history.Clear();
                _history.AddRange(history);
            }
            return problem is null ? PushLocked(model, tool, restart: true, entries) : null;
        }
    }
}

/// <summary>What a <c>set_weights</c> did: the mesh, how many vertices were set, how many of those had influences dropped by the cap of four, how many vertices of the mesh are still unweighted, and the normalised influences given.</summary>
public sealed record WeightsChange(string Mesh, int Set, int Capped, int Unweighted, IReadOnlyList<Influence> Influences);
