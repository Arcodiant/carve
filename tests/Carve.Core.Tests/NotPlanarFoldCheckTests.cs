using Carve.Scenario.Soft;
using Xunit;

namespace Carve.Core.Tests;

/// <summary>The soft export's warning check, shown to pass the right text and to fail each wrong one (T-72; T-197's grouping). Needs no run folder.</summary>
[Trait("Tier", "unit")]
public class NotPlanarFoldCheckTests
{
    private const string Plane = "face is not planar: a vertex is 0.1 off its plane (limit 0.001)";
    private const string Fold = "select faces where non_planar names them";

    private static readonly Dictionary<string, int> Counts = new() { ["Body"] = 13, ["Hat"] = 3 };

    private static IEnumerable<string> Named(string mesh, int n) => Enumerable.Range(0, n).Select(i => $"mesh '{mesh}': faces[{i}]: {Plane}");

    private static string Group(string mesh, int n, int first = 0) =>
        $"mesh '{mesh}': {n} faces are not planar: worst faces[{first + 1}], 0.2 off its plane (limit 0.001); e.g. faces[{first}], faces[{first + 1}], faces[{first + 2}]; {Fold}";

    private static string Good() => string.Join("; ", Named("Body", 0).Append(Group("Body", 13)).Concat(Named("Hat", 3)));

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
    public void A_group_line_that_does_not_count_select_s_faces_fails() =>
        Assert.NotNull(NotPlanarFold.Check(Good().Replace("13 faces", "12 faces"), Counts));

    [Fact]
    public void A_missing_group_line_fails() =>
        Assert.NotNull(NotPlanarFold.Check(string.Join("; ", Named("Hat", 3)), Counts));

    [Fact]
    public void All_faces_listed_one_by_one_fails() =>
        Assert.NotNull(NotPlanarFold.Check(string.Join("; ", Named("Body", 13).Concat(Named("Hat", 3))), Counts));

    [Fact]
    public void A_face_named_beside_its_mesh_s_group_line_fails() =>
        Assert.NotNull(NotPlanarFold.Check(Good() + $"; mesh 'Body': faces[0]: {Plane}", Counts));

    [Fact]
    public void Another_warning_fails() =>
        Assert.NotNull(NotPlanarFold.Check(Good() + "; the model has 6000 triangles, above the 5000 a game character should keep to", Counts));

    [Fact]
    public void A_group_line_for_a_mesh_that_lists_its_faces_fails() =>
        Assert.NotNull(NotPlanarFold.Check(string.Join("; ", Group("Body", 13), Group("Hat", 3)), Counts));

    [Fact]
    public void A_second_group_line_for_one_mesh_fails() =>
        Assert.NotNull(NotPlanarFold.Check(Good() + "; " + Group("Body", 13), Counts));

    // The id overload (RV-52, held by RV-53): Body's non-planar faces are 0 to 12 and Hat's 0 to 2, as Good() names them.
    private static readonly Dictionary<string, IReadOnlyList<int>> Ids = new()
    {
        ["Body"] = Enumerable.Range(0, 13).ToList(),
        ["Hat"] = Enumerable.Range(0, 3).ToList(),
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
    public void A_worst_face_select_does_not_find_fails() =>
        Assert.Contains("worst face", NotPlanarFold.Check(Good().Replace("worst faces[1]", "worst faces[40]"), Ids));

    [Fact]
    public void Samples_out_of_order_fail() =>
        Assert.NotNull(NotPlanarFold.Check(Good().Replace("faces[0], faces[1], faces[2]", "faces[1], faces[0], faces[2]"), Counts));

    // T-198: a smooth mesh's not-planar faces are a note in check and no warning in the export.
    private const string SmoothTail = "; expected on a smooth mesh, which export triangulates";

    private static string Check(string warnings = "", params string[] notes) => System.Text.Json.JsonSerializer.Serialize(new { warnings = warnings.Length == 0 ? [] : new[] { warnings }, notes });

    private static string Right => Check("", "size 1 x 1 x 1 m", "13 faces are not planar: worst faces[1], 0.2 off its plane (limit 0.001); e.g. faces[0], faces[1], faces[2]" + SmoothTail);

    [Fact]
    public void The_smooth_note_passes_against_select_s_faces() => Assert.Null(NotPlanarFold.CheckSmoothNote(Right, Enumerable.Range(0, 13).ToList()));

    [Fact]
    public void A_note_that_does_not_count_select_s_faces_fails() => Assert.NotNull(NotPlanarFold.CheckSmoothNote(Right, Enumerable.Range(0, 12).ToList()));

    [Fact]
    public void A_worst_face_select_does_not_find_fails() => Assert.Contains("worst face", NotPlanarFold.CheckSmoothNote(Right, Enumerable.Range(2, 13).ToList()));

    [Fact]
    public void A_missing_note_fails() => Assert.NotNull(NotPlanarFold.CheckSmoothNote(Check("", "size 1 x 1 x 1 m"), [0, 1]));

    [Fact]
    public void A_not_planar_warning_on_a_smooth_mesh_fails() =>
        Assert.Contains("warns", NotPlanarFold.CheckSmoothNote(Check("13 faces are not planar: worst faces[1]") , Enumerable.Range(0, 13).ToList()));

    [Fact]
    public void A_smooth_export_with_any_warning_fails_and_one_without_passes()
    {
        Assert.Null(NotPlanarFold.CheckSmoothExport("exported arm.glb, 1 clip(s) 'Curl'", 13));
        Assert.NotNull(NotPlanarFold.CheckSmoothExport("exported arm.glb; warning(s): mesh 'mesh': faces[3]: face is not planar: x", 13));
        Assert.NotNull(NotPlanarFold.CheckSmoothExport("exported arm.glb", 0));
    }
}
