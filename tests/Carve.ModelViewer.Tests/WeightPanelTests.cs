using Xunit;

namespace Carve.ModelViewer.Tests;

/// <summary>The window's panel as M-21's sketch has it: Tab, Left/Right by 0.05, 0 to reset, Space on the first clip.</summary>
public class WeightPanelTests
{
    private static WeightPanel Panel(string? clip = "Curl") => new(["Bulk", "Lean"], [0.6f, 0f], clip);

    [Fact]
    public void A_row_per_target_marks_the_selected_one_and_shows_its_weight_to_2_places()
    {
        var lines = Panel().Text().Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
        Assert.StartsWith("> Bulk", lines[0]);
        Assert.EndsWith("0.60", lines[0]);
        Assert.StartsWith("  Lean", lines[1]);
        Assert.EndsWith("0.00", lines[1]);
        Assert.Contains("Tab: next target    Left / Right: -/+ 0.05", lines);
        Assert.Contains("Space: play Curl", lines[^1]);
        Assert.EndsWith("0: all weights to 0", lines[^1]);
    }

    [Fact]
    public void Tab_wraps_and_the_arrows_move_the_selected_weight_by_0_05_within_0_to_1()
    {
        var panel = Panel();
        panel.Nudge(1);
        Assert.Equal(0.65f, panel.Weights[0]);
        panel.Next();
        panel.Nudge(-1);
        Assert.Equal(0f, panel.Weights[1]);
        for (var i = 0; i < 30; i++) panel.Nudge(1);
        Assert.Equal(1f, panel.Weights[1]);
        panel.Next();
        Assert.Equal(0, panel.Selected);
        Assert.Contains("1.00", panel.Text());
    }

    [Fact]
    public void Zero_resets_every_weight_and_space_plays_and_stops_only_with_a_clip()
    {
        var panel = Panel();
        panel.Reset();
        Assert.All(panel.Weights, w => Assert.Equal(0f, w));

        Assert.True(panel.TogglePlay());
        Assert.Contains("Space: stop Curl", panel.Text());
        Assert.True(panel.TogglePlay());
        Assert.False(Panel(clip: null).TogglePlay());
        Assert.DoesNotContain("Space", Panel(clip: null).Text());
    }
}
