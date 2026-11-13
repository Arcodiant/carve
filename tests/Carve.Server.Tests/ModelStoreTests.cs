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
    public void Undo_restores_uvs_and_seams_with_the_rest_of_the_mesh()
    {
        // T-53: UVs and seams live on the mesh, so the snapshot undo keeps them with no code of its own.
        var store = new ModelStore();
        var cube = TestMeshes.Cube().WithUvs(MeshUvs.None.WithFace(1, [new(0, 0), new(1, 0), new(1, 1), new(0, 1)]).WithSeams([(0, 1)], true));
        store.Push(cube, "define_mesh");
        Assert.NotNull(store.Edit("extrude", m => MeshEdits.Extrude(m, [1], 0.5f), out _));
        Assert.NotSame(cube.Uvs, store.Current.Mesh!.Uvs);
        Assert.NotNull(store.Edit("delete", m => MeshDelete.Delete(m, null, [1]), out _));
        Assert.False(store.Current.Mesh!.Uvs?.IsMapped(1) ?? false);

        store.Undo();
        Assert.True(store.Current.Mesh!.Uvs!.IsMapped(1));
        store.Undo();
        Assert.Same(cube.Uvs, store.Current.Mesh!.Uvs);
        Assert.Equal([(0, 1)], store.Current.Mesh!.Seams);
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
    public void Undoing_a_delete_then_a_new_edit_gives_out_the_undone_ids_again()
    {
        // T-64's "undo then a new edit reusing the undone ids": extrude the cube's top (vertices 8..11, faces 6..9),
        // delete what it made, undo both, and a new extrude of another face takes 8..11 and 6..9 again.
        var store = new ModelStore();
        store.Push(TestMeshes.Cube(), "define_mesh");
        store.Edit("extrude", m => MeshEdits.Extrude(m, [1], 1), out var extruded);
        Assert.Equal([8, 9, 10, 11], extruded!.NewVertices);
        store.Edit("delete", m => MeshDelete.Delete(m, [8, 9, 10, 11], null), out var deleted);
        Assert.True(deleted!.Succeeded);
        Assert.True(store.Current.Mesh!.HasTombstones);

        Assert.Equal("delete", store.Undo()!.Undone);
        Assert.Equal("extrude", store.Undo()!.Undone);
        Assert.False(store.Current.Mesh!.HasTombstones);
        Assert.Equal(8, store.Current.Mesh.Vertices.Count);
        Assert.Equal(6, store.Current.Mesh.Faces.Count);

        store.Edit("extrude", m => MeshEdits.Extrude(m, [0], 1), out var again);
        Assert.Equal([8, 9, 10, 11], again!.NewVertices);
        Assert.Equal([6, 7, 8, 9], again.NewFaces);
        Assert.False(store.Current.Mesh!.HasTombstones);
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
