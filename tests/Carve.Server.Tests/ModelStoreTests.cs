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

    [Fact]
    public void A_new_name_adds_a_mesh_and_undo_steps_back_across_meshes()
    {
        var store = new ModelStore();
        var crate = Triangle();
        var lid = Triangle();
        store.Define("crate", crate, "define_mesh");
        store.Define("lid", lid, "define_mesh");
        Assert.Equal(["crate", "lid"], store.Current.Model.Names);

        var moved = store.Edit("move", "lid", m => MeshEdits.Move(m, [0], null, null, Vector3.UnitZ), out var result, out var problem);
        Assert.Null(problem);
        Assert.True(result!.Succeeded);
        Assert.Same(crate, moved!.Model.Find("crate"));
        Assert.Equal(Vector3.UnitZ, moved.Model.Find("lid")!.Vertices[0]);

        Assert.Equal("move", store.Undo()!.Undone);
        Assert.Same(lid, store.Current.Model.Find("lid"));
        Assert.Equal("define_mesh", store.Undo()!.Undone);
        Assert.Equal(["crate"], store.Current.Model.Names);
        Assert.Same(crate, store.Current.Mesh);
        Assert.Null(store.Undo());
    }

    [Fact]
    public void An_edit_that_names_no_mesh_of_several_pushes_nothing_and_says_which_names_there_are()
    {
        var store = new ModelStore();
        store.Define("crate", Triangle(), "define_mesh");
        store.Define("lid", Triangle(), "define_mesh");
        var before = store.Current;

        Assert.Null(store.Edit("move", null, m => throw new InvalidOperationException("not run"), out var result, out var problem));
        Assert.Null(result);
        Assert.Equal("mesh: required, since the model has 2 meshes: 'crate', 'lid'", problem);
        Assert.Same(before, store.Current);
    }

    [Fact]
    public void The_undo_depth_counts_model_steps_whichever_mesh_they_change()
    {
        var store = new ModelStore();
        for (var i = 0; i < 150; i++) store.Define(i % 2 == 0 ? "a" : "b", Triangle(), "define_mesh");

        for (var i = 0; i < ModelStore.UndoDepth; i++) Assert.NotNull(store.Undo());
        Assert.Null(store.Undo());
        Assert.Equal(["a", "b"], store.Current.Model.Names);
    }
}
