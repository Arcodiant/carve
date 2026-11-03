using System.Numerics;
using Carve.Core;
using Carve.Core.Tests;

namespace Carve.Server.Tests;

[Trait("Tier", "unit")]
public class ModelStoreTests
{
    private static PolygonMesh Triangle() =>
        TestMeshes.Make([Vector3.Zero, Vector3.UnitX, Vector3.UnitY], [[0, 1, 2]]);

    [Fact]
    public void Starts_empty_at_version_zero_and_each_replace_adds_one()
    {
        var store = new ModelStore();
        Assert.Null(store.Current.Mesh);
        Assert.Equal(0, store.Current.Version);

        var first = Triangle();
        var published = store.Replace(first);

        Assert.Same(first, store.Current.Mesh);
        Assert.Equal(1, published.Version);
        Assert.Equal(2, store.Replace(Triangle()).Version);
    }

    [Fact]
    public void Undo_steps_back_to_the_older_mesh_and_raises_the_version_by_one()
    {
        var store = new ModelStore();
        var first = Triangle();
        var second = Triangle();
        store.Push(first, "define_mesh");
        store.Push(second, "move");

        var undo = store.Undo();

        Assert.NotNull(undo);
        Assert.Equal("move", undo.Undone);
        Assert.Same(first, store.Current.Mesh);
        Assert.Equal(3, store.Current.Version);
        Assert.Equal(3, undo.Current.Version);
        Assert.Null(store.Undo());
        Assert.Equal(3, store.Current.Version);
    }

    [Fact]
    public void Keeps_the_current_mesh_and_the_last_hundred_older_ones()
    {
        var store = new ModelStore();
        for (var i = 0; i < 150; i++) store.Push(Triangle(), "move");

        for (var i = 0; i < ModelStore.UndoDepth; i++) Assert.NotNull(store.Undo());
        Assert.Null(store.Undo());
        Assert.Equal(250, store.Current.Version);
    }

    [Fact]
    public void A_failed_edit_pushes_nothing_and_an_edit_with_no_mesh_reports_none()
    {
        var store = new ModelStore();
        Assert.Null(store.Edit("move", m => MeshEdits.Move(m, [0], null, null, Vector3.UnitX), out var none));
        Assert.Null(none);

        store.Replace(Triangle());
        var snapshot = store.Edit("move", m => MeshEdits.Move(m, [9], null, null, Vector3.UnitX), out var result);

        Assert.False(result!.Succeeded);
        Assert.Equal(1, snapshot!.Version);
        Assert.Equal(1, store.Current.Version);
        Assert.Null(store.Undo());
    }
}
