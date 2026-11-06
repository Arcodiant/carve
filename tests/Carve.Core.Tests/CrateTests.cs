using System.Numerics;
using Xunit;
using static Carve.Core.Tests.TestMeshes;

namespace Carve.Core.Tests;

/// <summary>The planning probe's crate, built through <see cref="MeshEdits"/> alone: the seed cube and 11 edits.</summary>
[Trait("Tier", "unit")]
public class CrateTests
{
    [Fact]
    public void TheCrate()
    {
        var mesh = Crate();
        var report = MeshReport.Create(mesh);
        Assert.Equal(72, report.VertexCount);
        Assert.Equal(70, report.FaceCount);
        Assert.True(report.Closed);
        Assert.Empty(report.Warnings);
        Assert.True(Vector3.Distance(new Vector3(-0.02f, 0, 0), report.BoundsMin) < 1e-5f, $"min {report.BoundsMin}");
        Assert.True(Vector3.Distance(new Vector3(1.02f, 0.8f, 1), report.BoundsMax) < 1e-5f, $"max {report.BoundsMax}");
    }
}
