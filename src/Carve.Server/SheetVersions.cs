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