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
