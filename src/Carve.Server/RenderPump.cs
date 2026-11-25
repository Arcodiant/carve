using System.Numerics;
using Carve.Core;

namespace Carve.Server;

/// <summary>What the pump asks of the scene, on the frame thread. Each may throw.</summary>
internal interface IRenderScene
{
    /// <summary>Puts this mesh in the scene. If it throws, the scene must not count it as shown.</summary>
    void ShowMesh(PolygonMesh mesh);

    /// <summary>Puts <paramref name="mesh"/> in the scene framed on the bounds of <paramref name="frameOn"/> (the rest pose, for a posed model: D-6, framing).</summary>
    void ShowMesh(PolygonMesh mesh, PolygonMesh frameOn) => ShowMesh(mesh);

    /// <summary>As above, with the weight of one bone at each vertex of <paramref name="mesh"/> (NaN where unweighted), for the weights look.</summary>
    void ShowMesh(PolygonMesh mesh, PolygonMesh frameOn, float[] weights) => ShowMesh(mesh, frameOn);

    /// <summary>The model's materials (T-78), given before each mesh of that model is shown, for its faces that use one.</summary>
    void ShowMaterials(IReadOnlyList<Material> materials)
    {
    }

    /// <summary>Draws this loaded <c>.glb</c> in place of the model, framed on its own rest bounds, or (null) the model again.</summary>
    void ShowGlb(GlbScene? glb) { }

    /// <summary>Points the cameras and the light for this spec of the mesh shown, and draws its look if that changed.</summary>
    void ApplyView(RenderSpec spec);

    /// <summary>Draws the mesh shown and answers the request, whatever happens.</summary>
    void Render(RenderRequest request);

    void Report(Exception exception, string message);
}

/// <summary>
/// Decides what the frame thread does next: pure sequencing, so that every interleaving of the
/// tools with the frame thread can be tested without a device. A frame reads the lighting and
/// transforms published earlier in the same loop iteration, so a change of the scene ends the
/// <see cref="Step"/> and the requests waiting on it stay queued for a later one. A request needs
/// the mesh current at its post or a newer one, never the newest, so each step opens by serving a
/// head the shown mesh satisfies: however fast the store moves, the queue moves too.
/// </summary>
internal sealed class RenderPump(RenderQueue queue, ModelStore store, IRenderScene scene, GraphBakes? bakes = null)
{
    // What GraphBakes had resolved for the model shown when it was put in (T-163): another means its graph bakes changed.
    private Model? _shownBakes;
    private Model? _bakedFor;
    // The model shown has graph slots no render had baked, so they are drawn empty (DrawnOf); its frames say so.
    private bool _unbaked;
    private long _version;
    private RenderSpec _spec = new(CameraSpec.ForView(RenderView.Front));
    private bool _viewApplied = true;
    private string? _meshFault;
    private (Vector3 Min, Vector3 Max)? _shownBounds;
    private PolygonMesh? _shown;
    private Model? _shownModel;
    private Pose? _shownPose;
    private TargetWeights? _shownTargets;
    private string? _shownWeights;
    private string? _shownPart;
    private int? _shownLevel;
    private GlbScene? _shownGlb;

    public void Step()
    {
        for (var opening = true; ; opening = false)
        {
            // The peek comes first. A request posted after define_mesh returned is then only seen
            // with the store already showing that mesh or a newer one, including the very first.
            var waiting = queue.TryPeek(out var next);
            var current = store.Current;
            var due = opening && waiting && _version >= next!.PostedVersion;
            if (current.Version != _version && !due)
            {
                ShowMesh(current);
                return;
            }

            if (!waiting)
            {
                return;
            }

            // A loaded glb (render_glb) is drawn in place of the model. Putting it in, or taking it out, is a change of
            // the scene: it ends the step, and the view is applied again, on the new framing, before anything is drawn.
            if (!ReferenceEquals(next!.Spec.Glb, _shownGlb))
            {
                ShowGlb(next);
                return;
            }

            // The model's own checks are not this request's: it draws the file, not the store.
            if (next.Spec.Glb is not null)
            {
                if (next.Spec != _spec || !_viewApplied)
                {
                    ChangeView(next);
                    return;
                }

                queue.TryTake(out _);
                next.ServedVersion = _version;
                scene.Render(next);
                continue;
            }

            if (_meshFault is { } fault)
            {
                queue.TryTake(out _);
                next!.Fail(new InvalidOperationException(fault));
                continue;
            }

            // The target was checked at the call against the mesh then current; the mesh shown may be a
            // newer one, so it is checked again against that mesh, with the same text.
            if (next!.Spec.Camera.Target is { } target && _shownBounds is { } bounds
                && TargetCheck.Refusal(target, bounds.Min, bounds.Max) is { } refusal)
            {
                queue.TryTake(out _);
                next.Fail(new InvalidOperationException(refusal));
                continue;
            }

            // The same for a highlight: its ids were checked against the mesh of the call.
            if (_shown is { } shown && next.Spec.Highlight.Unknown(shown) is { Count: > 0 } unknown)
            {
                queue.TryTake(out _);
                next.Fail(new InvalidOperationException(string.Join("; ", unknown)));
                continue;
            }

            // And for a weights look: its bone was checked against the model of the call. Only this request fails;
            // the mesh shown stays shaded, so a later plain render is served.
            if (next.Spec.Weights is { } bone && _shownModel is { } model && WeightHeat.Refusal(model, bone) is { } noBone)
            {
                queue.TryTake(out _);
                next.Fail(new InvalidOperationException(noBone));
                continue;
            }

            // The pose is part of what is shown: a request for another one puts the mesh in the scene again, posed.
            if (!Equals(next.Spec.Pose, _shownPose))
            {
                _shownPose = next.Spec.Pose;
                ShowMesh(store.Current);
                continue;
            }

            // So are the target weights: another set puts the mesh in the scene again, morphed.
            if (!Equals(next.Spec.Targets, _shownTargets))
            {
                _shownTargets = next.Spec.Targets;
                ShowMesh(store.Current);
                continue;
            }

            // So is the bone whose weights are drawn.
            if (next.Spec.Weights != _shownWeights)
            {
                _shownWeights = next.Spec.Weights;
                ShowMesh(store.Current);
                continue;
            }

            // And the part and level drawn (T-94): another puts that part of the model in, framed on it.
            if (next.Spec.Part != _shownPart || next.Spec.Level != _shownLevel)
            {
                (_shownPart, _shownLevel) = (next.Spec.Part, next.Spec.Level);
                ShowMesh(store.Current);
                continue;
            }

            // And the bakes of its graph materials: a render that baked again (a graph file edited) puts the mesh in again with them.
            if (bakes is not null && _bakedFor is { } bakedFor && !ReferenceEquals(bakes.Peek(bakedFor), _shownBakes))
            {
                ShowMesh(store.Current);
                continue;
            }

            if (next.Spec != _spec || !_viewApplied)
            {
                ChangeView(next);
                return;
            }

            queue.TryTake(out _);
            next.ServedVersion = _version;
            next.ServedUnbaked = _unbaked;
            scene.Render(next);
        }
    }

    private void ShowMesh(ModelSnapshot snapshot)
    {
        // Taken as seen even on a failure: the next try is the next mesh, not every frame.
        _version = snapshot.Version;
        try
        {
            // A model with graph materials is drawn with their bakes (T-163), resolved by render before it queued the frame.
            var model = bakes?.DrawnOf(snapshot.Model) ?? snapshot.Model;
            _bakedFor = snapshot.Model;
            _shownBakes = bakes?.Peek(snapshot.Model);
            _unbaked = bakes is not null && _shownBakes is null && snapshot.Model.Materials.Any(m => m.HasGraph);
            var whole = _shownPart is null && _shownLevel is null;
            var drawn = whole ? model.Combined! : model.CombinedOf(_shownPart, _shownLevel)
                ?? throw new InvalidOperationException(_shownPart is { } p && model.FindPart(p) is null
                    ? $"part: there is no part named '{p}'" : "nothing is drawn: no part of it has geometry at that level");
            scene.ShowMaterials(model.Materials);
            // The bone of the last weights look may be gone from a newer model (an undo, open or renaming define_skeleton):
            // then the mesh is shown shaded, and only a weights request for that bone fails, at the head of the queue.
            var heat = _shownWeights is null || WeightHeat.Refusal(model, _shownWeights) is not null
                ? null : Within(model, WeightHeat.Of(model, _shownWeights));
            if ((_shownPose is not null && model.Skeleton is not null) || _shownTargets is not null)
            {
                // The morph comes before the skin. With no pose only the morph is applied, so the mesh is not skinned at rest.
                var morphed = _shownTargets is { } weights ? weights.ByName : null;
                // Each weighted occurrence is skinned where it is drawn, in model space, as the skinned export bakes it (RV-71).
                var posed = (_shownPose is { } pose && model.Skeleton is not null
                    ? Skinning.PosedOf(model, pose, morphed, _shownPart)
                    : Skinning.Morphed(model, morphed!).CombinedOf(_shownPart, null))!;
                if (heat is null) scene.ShowMesh(posed, drawn);
                else scene.ShowMesh(posed, drawn, heat);
            }
            else if (heat is not null)
                scene.ShowMesh(drawn, drawn, heat);
            else
                scene.ShowMesh(drawn);
            var report = MeshReport.Create(drawn);
            _shownBounds = (report.BoundsMin, report.BoundsMax);
            _shown = drawn;
            _shownModel = snapshot.Model;
            _meshFault = null;
        }
        catch (Exception e)
        {
            // Until the next mesh, a render is this error, never the last mesh under a new caption.
            _bakedFor = snapshot.Model;
            _shownBakes = bakes?.Peek(snapshot.Model);
            _meshFault = $"The current mesh could not be put in the scene: {e.GetBaseException().Message}";
            scene.Report(e, _meshFault);
            return;
        }

        try
        {
            scene.ApplyView(_spec);
            _viewApplied = true;
        }
        catch (Exception e)
        {
            // The mesh is in, the camera is not to be trusted: the next request sets it again.
            _viewApplied = false;
            scene.Report(e, ViewFault(e));
        }
    }

    /// <summary>
    /// The heat of the shown part's subtree (T-94): <see cref="WeightHeat.Of"/> is per vertex of <see cref="Model.Meshes"/> in
    /// order, so each drawn occurrence takes its mesh's run (an instance its source's), in the order the subtree is drawn.
    /// </summary>
    private float[] Within(Model model, float[] whole)
    {
        if (_shownPart is null) return whole;
        var runs = new Dictionary<string, (int Start, int Count)>(StringComparer.Ordinal);
        var at = 0;
        foreach (var (name, mesh) in model.Meshes) { runs[name] = (at, mesh.Vertices.Count); at += mesh.Vertices.Count; }
        var heat = new List<float>();
        foreach (var name in model.SubtreeOf(_shownPart))
            if (runs.TryGetValue(model.FindPart(name)!.InstanceOf ?? name, out var run)) heat.AddRange(whole[run.Start..(run.Start + run.Count)]);
        return [.. heat];
    }

    private void ShowGlb(RenderRequest next)
    {
        _viewApplied = false;
        try
        {
            scene.ShowGlb(next.Spec.Glb);
            _shownGlb = next.Spec.Glb;
        }
        catch (Exception e)
        {
            // Back to the model, so a failed file leaves nothing of itself in the scene.
            var fault = $"The file could not be put in the scene: {e.GetBaseException().Message}";
            scene.Report(e, fault);
            if (next.Spec.Glb is not null)
            {
                queue.TryTake(out _);
                next.Fail(new InvalidOperationException(fault));
            }

            try
            {
                scene.ShowGlb(null);
            }
            catch (Exception again)
            {
                scene.Report(again, "The model could not be put back after a file failed.");
            }

            _shownGlb = null;
        }
    }

    private void ChangeView(RenderRequest next)
    {
        try
        {
            _spec = next.Spec;
            _viewApplied = false;
            scene.ApplyView(_spec);
            _viewApplied = true;
        }
        catch (Exception e)
        {
            var fault = ViewFault(e);
            queue.TryTake(out _);
            next.Fail(new InvalidOperationException(fault));
            scene.Report(e, fault);
        }
    }

    private string ViewFault(Exception e) =>
        $"The {Name(_spec.Camera)} could not be set up: {e.GetBaseException().Message}";

    private static string Name(CameraSpec camera) =>
        camera == CameraSpec.ForView(RenderView.Front) ? "front view"
        : camera == CameraSpec.ForView(RenderView.Back) ? "back view"
        : "camera";
}
