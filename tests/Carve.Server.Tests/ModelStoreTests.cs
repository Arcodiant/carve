using System.Numerics;
using Carve.Core;
using Carve.Core.Tests;

namespace Carve.Server.Tests;

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
}
