namespace Carve.Server.Tests;

/// <summary>A sheet's tiles all show one version of the mesh: <c>SheetVersions.OnOneMesh</c> on the versions alone.</summary>
[Trait("Tier", "unit")]
public class SheetOnOneMeshTests
{
    private static Func<Task<(long[]? Frames, string? Error)>> Tries(List<int> calls, params (long[]? Frames, string? Error)[] tries) =>
        () =>
        {
            calls.Add(calls.Count);
            return Task.FromResult(tries[Math.Min(calls.Count - 1, tries.Length - 1)]);
        };

    [Fact]
    public async Task Tiles_on_two_meshes_are_drawn_again_and_the_agreeing_try_is_returned()
    {
        var calls = new List<int>();

        var (frames, error) = await SheetVersions.OnOneMesh(Tries(calls, ([1, 1, 2, 2], null), ([2, 2, 2, 2], null)), v => v, 3);

        Assert.Null(error);
        Assert.Equal([2, 2, 2, 2], frames!);
        Assert.Equal(2, calls.Count);
    }

    [Fact]
    public async Task Tiles_that_agree_are_drawn_once()
    {
        var calls = new List<int>();

        var (frames, _) = await SheetVersions.OnOneMesh(Tries(calls, ([5, 5, 5], null)), v => v, 3);

        Assert.Equal([5, 5, 5], frames!);
        Assert.Single(calls);
    }

    [Fact]
    public async Task An_edit_racing_every_try_is_an_error_not_a_mixed_sheet()
    {
        var calls = new List<int>();

        var (frames, error) = await SheetVersions.OnOneMesh(Tries(calls, ([1, 2], null), ([2, 3], null), ([3, 4], null)), v => v, 3);

        Assert.Null(frames);
        Assert.Equal("The mesh changed while the sheet was drawn, on each of 3 tries, so its tiles would show different meshes; call render again once the edits have settled.", error);
        Assert.Equal(3, calls.Count);
    }

    [Fact]
    public async Task A_failed_render_is_returned_at_once()
    {
        var calls = new List<int>();

        var (frames, error) = await SheetVersions.OnOneMesh(Tries(calls, (null, "The render failed: x")), v => v, 3);

        Assert.Null(frames);
        Assert.Equal("The render failed: x", error);
        Assert.Single(calls);
    }
}
