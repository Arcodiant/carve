using Carve.Core;

namespace Carve.Server;

/// <summary>Keeps a sheet's tiles on one mesh. Needs no engine, so it is linked into the tests.</summary>
internal static class SheetVersions
{
    /// <summary>
    /// Renders until every frame shows the same version of the mesh: the pump serves each tile the mesh current at its
    /// post or a newer one, so an edit landing mid-sheet would leave its tiles on two meshes. Each try posts afresh, at
    /// the store's version then. An error ends it at once.
    /// </summary>
    internal static async Task<(T[]? Frames, string? Error)> OnOneMesh<T>(Func<Task<(T[]? Frames, string? Error)>> render, Func<T, long> version, int attempts)
    {
        for (var k = 1; ; k++)
        {
            var got = await render();
            if (got.Frames is not { } frames || frames.All(f => version(f) == version(frames[0])))
            {
                return got;
            }

            if (k == attempts)
            {
                return (null, $"The mesh changed while the sheet was drawn, on each of {attempts} tries, so its tiles would show different meshes; call render again once the edits have settled.");
            }
        }
    }

    /// <summary>
    /// Bakes and renders (<paramref name="bakeAndRender"/> resolves the store's model, then posts) until no frame is
    /// <paramref name="unbaked"/>: an edit landing between the bake and the frame is drawn with its graph slots empty
    /// (<see cref="GraphBakes.DrawnOf"/>), so that model is baked and drawn again (RV-82). An error ends it at once.
    /// </summary>
    internal static async Task<(T[]? Frames, string? Error)> Baked<T>(Func<Task<(T[]? Frames, string? Error)>> bakeAndRender, Func<T, bool> unbaked, int attempts,
        string drawn = "its graph slots would be drawn empty")
    {
        for (var k = 1; ; k++)
        {
            var got = await bakeAndRender();
            if (got.Frames is not { } frames || !frames.Any(unbaked))
            {
                return got;
            }

            if (k == attempts)
            {
                return (null, $"The model changed between its bake and its frame on each of {attempts} tries, so {drawn}; call render again once the edits have settled.");
            }
        }
    }

    /// <summary>
    /// render's model looks (RV-83: the tool's own loop, so a test holds what render runs): each try resolves the store's model off
    /// the frame thread (T-163), posts every spec and waits for the frame thread; again while a frame was drawn unbaked
    /// (<see cref="Baked"/>) or the tiles show two meshes (<see cref="OnOneMesh"/>). <paramref name="baked"/> gains what each try baked.
    /// </summary>
    internal static Task<(RenderedFrame[]? Frames, string? Error)> Render(GraphBakes bakes, ModelStore store, RenderQueue queue, IReadOnlyList<RenderSpec> specs,
        Task stopped, Func<string?> fault, List<GraphBakes.Baked> baked, int attempts, CancellationToken cancel)
    {
        async Task<(RenderedFrame[]? Frames, string? Error)> RenderTiles()
        {
            try
            {
                baked.AddRange((await Task.Run(() => bakes.Resolve(store.Current.Model, cancel), cancel)).Baked);
            }
            catch (GraphSlotProblemException e)
            {
                return (null, e.Message);
            }

            var rendering = Task.WhenAll(specs.Select(queue.Post));

            // The frame thread drains the queue; once it has ended, nothing will.
            await Task.WhenAny(rendering, stopped).WaitAsync(cancel);
            if (!rendering.IsCompleted)
            {
                return (null, fault() ?? "The engine has stopped, so nothing can be rendered.");
            }

            return rendering.IsCompletedSuccessfully
                ? (rendering.Result, null)
                : (null, $"The render failed: {rendering.Exception?.GetBaseException().Message}");
        }

        return OnOneMesh(() => Baked(RenderTiles, f => f.Unbaked, attempts), f => f.Version, attempts);
    }

    /// <summary>
    /// render's <c>uvs</c> look (RV-83: the tool's own loop): each try takes the store's snapshot, which <paramref name="check"/> may
    /// refuse or name the mesh of; bakes <paramref name="graph"/> (if any) on it as the picture, else uses <paramref name="picture"/>;
    /// posts <paramref name="spec"/> and waits; again while the frame was drawn on another model than the image was baked on
    /// (<see cref="UvImageStale"/>). Then <paramref name="done"/> makes the answer; every failure is <paramref name="error"/>'s.
    /// </summary>
    internal static async Task<T> Uvs<T>(GraphBakes bakes, ModelStore store, RenderQueue queue, MaterialGraph? graph, MaterialTexture? picture,
        Func<ModelSnapshot, (T? Refused, string? Mesh)> check, Func<ModelSnapshot, string, MaterialTexture?, RenderSpec> spec,
        Func<ModelSnapshot, string, RenderedFrame, MaterialTexture?, List<GraphBakes.Baked>, T> done, Func<string, T> error,
        Task stopped, Func<string?> fault, int attempts, CancellationToken cancel) where T : class
    {
        var (tries, changed) = await Baked(TryOnce, t => t.Stale, attempts, UvImageDrawn);
        return changed is null ? tries![0].Result : error(changed);

        async Task<((T Result, bool Stale)[]? Frames, string? Error)> TryOnce()
        {
            static ((T, bool)[]?, string?) Done(T result, bool stale = false) => ([(result, stale)], null);
            var current = store.Current;
            var (refused, mesh) = check(current);
            if (refused is not null) return Done(refused);

            var shown = picture;
            var baked = new List<GraphBakes.Baked>();
            if (graph is not null)
            {
                try
                {
                    var (texture, bake) = await Task.Run(() => bakes.Image(current.Model, mesh!, graph, cancel), cancel);
                    shown = texture;
                    if (bake is not null) baked.Add(bake);
                }
                catch (GraphSlotProblemException e)
                {
                    return Done(error(e.Message));
                }
            }

            var rendering = queue.Post(spec(current, mesh!, shown));
            await Task.WhenAny(rendering, stopped).WaitAsync(cancel);
            if (!rendering.IsCompleted) return Done(error(fault() ?? "The engine has stopped, so nothing can be rendered."));
            if (!rendering.IsCompletedSuccessfully) return Done(error($"The render failed: {rendering.Exception?.GetBaseException().Message}"));
            var frame = rendering.Result;
            if (UvImageStale(graph is not null, current.Version, frame.Version)) return Done(error(""), stale: true);
            return Done(done(current, mesh!, frame, shown, baked));
        }
    }

    /// <summary>What render's <c>uvs</c> look says when an edit lands between its graph image's bake and its frame on every try (RV-82).</summary>
    internal const string UvImageDrawn = "its graph image would lie under another model's layout";

    /// <summary>A <c>uvs</c> frame drawn on another version than the graph image was baked on: the layout and the picture are of two models (RV-82).</summary>
    internal static bool UvImageStale(bool graphImage, long bakedOn, long drawnOn) => graphImage && drawnOn != bakedOn;

    /// <summary>
    /// Why a render whose ids were moved past the meshes before <paramref name="shiftedFor"/> must be refused, or null:
    /// the offsets were taken from the snapshot the call checked (<paramref name="checkedVersion"/>), and a frame drawn on
    /// another version (<paramref name="drawnVersion"/>) may hold other elements at those ids. Null when no ids were shifted.
    /// </summary>
    internal static string? ShiftedIdsRefusal(string? shiftedFor, long checkedVersion, long drawnVersion) =>
        shiftedFor is null || drawnVersion == checkedVersion
            ? null
            : $"The model changed while the frame was drawn, so the ids given for mesh '{shiftedFor}' may name other elements; call render again once the edits have settled.";
}