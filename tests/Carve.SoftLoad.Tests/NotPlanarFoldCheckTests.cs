using Carve.Scenario.Soft;
using Xunit;

namespace Carve.SoftLoad.Tests;

/// <summary>The soft export's warning check, shown to pass the right text and to fail each wrong one (T-72). Needs no run folder.</summary>
[Trait("Tier", "unit")]
public class NotPlanarFoldCheckTests
{
    private const string Plane = "face is not planar: a vertex is 0.1 off its plane (limit 0.001)";
    private const string Fold = "select faces where non_planar names them";

    private static readonly Dictionary<string, int> Counts = new() { ["Body"] = 13, ["Hat"] = 4 };

    private static IEnumerable<string> Named(string mesh, int n) => Enumerable.Range(0, n).Select(i => $"mesh '{mesh}': faces[{i}]: {Plane}");

    private static string Good() => string.Join("; ", Named("Body", 10).Append($"mesh 'Body': and 3 more faces are not planar; {Fold}").Concat(Named("Hat", 4)));

    [Fact]
    public void The_right_text_passes() => Assert.Null(NotPlanarFold.Check(Good(), Counts));

    [Fact]
    public void A_fold_that_does_not_add_up_fails() =>
        Assert.NotNull(NotPlanarFold.Check(Good().Replace("and 3 more", "and 2 more"), Counts));

    [Fact]
    public void A_missing_fold_fails() =>
        Assert.NotNull(NotPlanarFold.Check(string.Join("; ", Named("Body", 10).Concat(Named("Hat", 4))), Counts));

    [Fact]
    public void All_faces_listed_one_by_one_fails() =>
        Assert.NotNull(NotPlanarFold.Check(string.Join("; ", Named("Body", 13).Concat(Named("Hat", 4))), Counts));

    [Fact]
    public void Another_warning_fails() =>
        Assert.NotNull(NotPlanarFold.Check(Good() + "; the model has 6000 triangles, above the 5000 a game character should keep to", Counts));

    [Fact]
    public void A_fold_for_a_mesh_that_fits_fails() =>
        Assert.NotNull(NotPlanarFold.Check(Good() + $"; mesh 'Hat': and 1 more face is not planar; {Fold}", Counts));
}
