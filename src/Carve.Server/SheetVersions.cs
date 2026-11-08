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
}
