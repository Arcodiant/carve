using Carve.Core;
using Carve.Core.Tests;

namespace Carve.Server.Tests;

/// <summary>The store's two material steps, which the tools call: define a material, and change a mesh's style with the model beside it (T-75).</summary>
[Trait("Tier", "unit")]
public class MaterialStoreTests
{
    private static ModelStore Store()
    {
        var store = new ModelStore();
        store.Replace(TestMeshes.Cube());
        return store;
    }

    [Fact]
    public void Defining_a_material_is_one_undo_step_and_a_redefinition_replaces_in_place()
    {
        var store = Store();
        var version = store.Current.Version;
        Assert.NotNull(store.DefineMaterial("define_material", new Material("a", Metallic: 1f), out var replaced, out _));
        Assert.False(replaced);
        store.DefineMaterial("define_material", new Material("b"), out _, out _);
        store.DefineMaterial("define_material", new Material("a", Roughness: 0.5f), out replaced, out _);
        Assert.True(replaced);
        Assert.Equal(["a", "b"], store.Current.Model.Materials.Select(m => m.Name));
        Assert.Equal(0f, store.Current.Model.FindMaterial("a")!.Metallic);
        Assert.Equal(version + 3, store.Current.Version);

        Assert.Equal("define_material", store.Undo()!.Undone);
        Assert.Equal(1f, store.Current.Model.FindMaterial("a")!.Metallic);
    }

    [Fact]
    public void A_material_with_a_problem_is_refused_naming_its_field_and_pushes_nothing()
    {
        var store = Store();
        var version = store.Current.Version;
        Assert.Null(store.DefineMaterial("define_material", new Material("a:b", Roughness: 2f), out _, out var problems));
        Assert.Equal(2, problems.Count);
        Assert.StartsWith("name: ", problems[0]);
        Assert.StartsWith("roughness: ", problems[1]);
        Assert.Equal(version, store.Current.Version);
    }

    [Fact]
    public void A_material_can_be_defined_before_any_mesh()
    {
        var store = new ModelStore();
        Assert.NotNull(store.DefineMaterial("define_material", new Material("a"), out _, out _));
        store.Replace(TestMeshes.Cube());
        Assert.Equal(["a"], store.Current.Model.Materials.Select(m => m.Name));
    }

    [Fact]
    public void A_style_change_that_sees_the_model_sets_the_material_and_undo_takes_it_back()
    {
        var store = Store();
        store.DefineMaterial("define_material", new Material("a"), out _, out _);
        Assert.NotNull(store.Restyle("set_material", null,
            (model, m) => ((m.Style ?? FaceStyle.None).WithMaterial([0, 1], model.FindMaterial("a")!.Name), []), out _, out _));
        Assert.Equal("a", store.Current.Mesh!.Style!.MaterialOf(1));
        Assert.Equal("set_material", store.Undo()!.Undone);
        Assert.Null(store.Current.Mesh!.Style);
    }

    [Fact]
    public void A_refused_style_change_pushes_nothing()
    {
        var store = Store();
        var version = store.Current.Version;
        Assert.Null(store.Restyle("set_material", null, (model, m) => (null, ["no"]), out var refused, out _));
        Assert.Equal(["no"], refused);
        Assert.Equal(version, store.Current.Version);
        Assert.Null(store.Restyle("set_material", "nope", (model, m) => (null, []), out _, out var problem));
        Assert.StartsWith("mesh: no mesh named 'nope'", problem);
    }
}
