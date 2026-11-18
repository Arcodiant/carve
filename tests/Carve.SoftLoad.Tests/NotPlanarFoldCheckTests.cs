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

    private static readonly Dictionary<string, IReadOnlyList<int>> None = new() { ["mesh"] = [] };
    private static readonly Dictionary<string, IReadOnlyList<int>> Two = new() { ["mesh"] = [3, 7] };

    [Fact]
    public void A_clean_export_passes_when_select_finds_none() =>
        Assert.Null(NotPlanarFold.CheckExport("exported arm.glb, 1 clip(s) 'Curl'", None));

    [Fact]
    public void A_clean_export_fails_when_select_finds_faces() =>
        Assert.NotNull(NotPlanarFold.CheckExport("exported arm.glb, 1 clip(s) 'Curl'", Two));

    [Fact]
    public void Warnings_for_the_selected_faces_pass_the_export_check() =>
        Assert.Null(NotPlanarFold.CheckExport($"exported arm.glb; warning(s): mesh 'mesh': faces[3]: {Plane}; mesh 'mesh': faces[7]: {Plane}", Two));

    [Fact]
    public void Warnings_when_select_finds_none_fail_the_export_check() =>
        Assert.NotNull(NotPlanarFold.CheckExport($"exported arm.glb; warning(s): mesh 'mesh': faces[3]: {Plane}", None));

    [Fact]
    public void Another_warning_fails_the_export_check() =>
        Assert.NotNull(NotPlanarFold.CheckExport("exported arm.glb; warning(s): the model has 6000 triangles, above the 5000 a game character should keep to", None));

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

    // The id overload (RV-52, held by RV-53): Body's non-planar faces are 0 to 12 and Hat's 0 to 3, as Good() names them.
    private static readonly Dictionary<string, IReadOnlyList<int>> Ids = new()
    {
        ["Body"] = Enumerable.Range(0, 13).ToList(),
        ["Hat"] = Enumerable.Range(0, 4).ToList(),
    };

    [Fact]
    public void The_right_text_passes_against_select_s_ids() => Assert.Null(NotPlanarFold.Check(Good(), Ids));

    [Fact]
    public void Named_faces_that_are_not_select_s_lowest_fail()
    {
        // The same count, in ascending order, but Body's lowest non-planar face is 2, so faces 0 and 1 are not select's.
        var shifted = new Dictionary<string, IReadOnlyList<int>>(Ids) { ["Body"] = Enumerable.Range(2, 13).ToList() };
        Assert.Null(NotPlanarFold.Check(Good(), shifted.ToDictionary(p => p.Key, p => p.Value.Count)));
        Assert.Contains("not select's lowest", NotPlanarFold.Check(Good(), shifted));
    }

    [Fact]
    public void A_face_named_after_its_mesh_s_fold_line_fails()
    {
        // Body's tenth named face moved after its fold line: the count, the order of ids and the total all still hold.
        var late = string.Join("; ", Named("Body", 9).Append($"mesh 'Body': and 3 more faces are not planar; {Fold}")
            .Append($"mesh 'Body': faces[9]: {Plane}").Concat(Named("Hat", 4)));
        Assert.Contains("must come last", NotPlanarFold.Check(late, Counts));
        Assert.Contains("must come last", NotPlanarFold.Check(late, Ids));
    }
}
