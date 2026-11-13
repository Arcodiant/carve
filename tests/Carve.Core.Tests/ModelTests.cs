using System.Numerics;
using Xunit;

namespace Carve.Core.Tests;

/// <summary>The model of named meshes: naming, order, replacement, which mesh a tool is aimed at, and its refusals.</summary>
[Trait("Tier", "unit")]
public class ModelTests
{
    private static PolygonMesh Triangle(float x = 0) =>
        TestMeshes.Make([new Vector3(x, 0, 0), new Vector3(x + 1, 0, 0), new Vector3(x, 1, 0)], [[0, 1, 2]]);

    [Fact]
    public void An_empty_model_has_no_meshes_no_combined_mesh_and_no_target()
    {
        Assert.True(Model.Empty.IsEmpty);
        Assert.Empty(Model.Empty.Meshes);
        Assert.Null(Model.Empty.Combined);
        Assert.Null(Model.Empty.Target(null, out var problem));
        Assert.Null(problem);
    }

    [Fact]
    public void Meshes_keep_definition_order_and_a_known_name_is_replaced_in_its_place()
    {
        var body = Triangle();
        var coat = Triangle(2);
        var newBody = Triangle(5);
        var model = Model.Empty.With("body", body).With("coat", coat);
        var replaced = model.With("body", newBody);

        Assert.Equal(["body", "coat"], model.Names);
        Assert.Equal(["body", "coat"], replaced.Names);
        Assert.Same(newBody, replaced.Find("body"));
        Assert.Same(coat, replaced.Find("coat"));
        Assert.Same(body, model.Find("body")); // the old model is untouched
        Assert.Null(model.Find("hat"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void An_empty_name_is_refused(string? name)
    {
        Assert.NotNull(Model.NameProblem(name));
        Assert.Throws<ArgumentException>(() => Model.Empty.With(name!, Triangle()));
        Assert.Null(Model.NameProblem(" "));
    }

    [Fact]
    public void A_name_with_a_colon_is_refused()
    {
        // RV-24 B4: '<mesh>:' prefixes a skinned export's material names, so a mesh name may not hold one.
        Assert.Equal("a mesh name must not contain ':' (a skinned export names materials <mesh>:<group>)", Model.NameProblem("a:b"));
        Assert.Throws<ArgumentException>(() => Model.Empty.With("a:b", Triangle()));
        Assert.Null(Model.Of("crate", Triangle()).Target("crate:lid", out var problem));
        Assert.Equal("mesh: a mesh name must not contain ':' (a skinned export names materials <mesh>:<group>)", problem);
    }

    [Fact]
    public void One_mesh_is_the_target_when_no_name_is_given_and_is_its_own_combined_mesh()
    {
        var mesh = Triangle();
        var model = Model.Of("crate", mesh);

        Assert.Same(mesh, model.Target(null, out var problem)!.Mesh);
        Assert.Null(problem);
        Assert.Same(mesh, model.Combined);
    }

    [Fact]
    public void With_several_meshes_leaving_the_name_out_is_refused_listing_every_name_in_order()
    {
        var model = Model.Of("crate", Triangle()).With("lid", Triangle(2));

        Assert.Null(model.Target(null, out var problem));
        Assert.Equal("mesh: required, since the model has 2 meshes: 'crate', 'lid'", problem);
    }

    [Fact]
    public void An_unknown_name_is_refused_listing_the_names()
    {
        var model = Model.Of("crate", Triangle()).With("lid", Triangle(2));

        Assert.Null(model.Target("hat", out var problem));
        Assert.Equal("mesh: no mesh named 'hat'; the model's meshes are 'crate', 'lid'", problem);
        Assert.Null(Model.Empty.Target("hat", out problem));
        Assert.Equal("mesh: no mesh named 'hat'; the model has no meshes yet", problem);
        Assert.Null(model.Target("", out problem));
        Assert.Equal("mesh: a mesh name must not be empty", problem);
    }

    [Fact]
    public void A_named_target_is_that_mesh()
    {
        var lid = Triangle(2);
        var model = Model.Of("crate", Triangle()).With("lid", lid);

        var target = model.Target("lid", out var problem);

        Assert.Null(problem);
        Assert.Equal("lid", target!.Name);
        Assert.Same(lid, target.Mesh);
    }

    [Fact]
    public void The_combined_mesh_is_every_mesh_in_order_with_ids_offset_past_the_ones_before()
    {
        var model = Model.Of("a", Triangle()).With("b", TestMeshes.Cube());

        var combined = model.Combined!;

        Assert.Equal(3 + 8, combined.Vertices.Count);
        Assert.Equal(1 + 6, combined.Faces.Count);
        Assert.Equal(new Vector3(1, 0, 0), combined.Vertices[1]);
        Assert.Equal(TestMeshes.Cube().Vertices[0], combined.Vertices[3]);
        Assert.Equal(TestMeshes.Cube().Faces[0].Loop.Select(v => v + 3), combined.Faces[1].Loop);
        Assert.Same(combined, model.Combined);
    }

    [Fact]
    public void The_combined_mesh_carries_each_mesh_s_uvs_and_seams_with_their_ids_offset()
    {
        var a = Triangle().WithUvs(MeshUvs.None.WithFace(0, [new(0, 0), new(1, 0), new(0, 1)]).WithSeams([(0, 1)], true));
        var cube = TestMeshes.Cube();
        var b = cube.WithUvs(MeshUvs.None.WithFace(2, [new(0.25f, 0), new(1, 0), new(1, 1), new(0, 1)]).WithSeams([(4, 5)], true));
        var model = Model.Of("a", a).With("plain", Triangle(2)).With("b", b);

        var uvs = model.Combined!.Uvs!;

        Assert.Equal([0, 1 + 1 + 2], uvs.MappedFaces);
        Assert.Equal(a.Uvs!.Of(0), uvs.Of(0));
        Assert.Equal(b.Uvs!.Of(2), uvs.Of(4));
        Assert.False(uvs.IsMapped(1));
        Assert.Equal([(0, 1), (3 + 3 + 4, 3 + 3 + 5)], uvs.Seams);
        Assert.Empty(MeshUvs.Check(model.Combined!, uvs));
    }

    [Fact]
    public void A_combined_mesh_has_no_uvs_when_no_mesh_has_them()
    {
        var model = Model.Of("a", Triangle()).With("b", TestMeshes.Cube());

        Assert.Null(model.Combined!.Uvs);
        Assert.Empty(model.Combined!.Seams);
    }
}
