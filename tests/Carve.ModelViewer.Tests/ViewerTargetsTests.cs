using System.Numerics;
using System.Text.Json;
using Xunit;

namespace Carve.ModelViewer.Tests;

/// <summary>The fixture cube with its <c>Sink</c> target, and the runs at weights 0, 0.5 and 1 (T-108).</summary>
public sealed class SinkRuns : IDisposable
{
    public const string Side = "side=0,0.5,3";

    private readonly string _temp = Directory.CreateTempSubdirectory("carve-viewer-targets-").FullName;

    public SinkRuns()
    {
        Content = Path.Combine(_temp, "Content");
        Directory.CreateDirectory(Path.Combine(Content, "models"));
        File.WriteAllBytes(Path.Combine(Content, "models/sink.glb"), CubeFixture.WriteWithSink());
        File.WriteAllBytes(Path.Combine(Content, "models/cube.glb"), CubeFixture.Write());

        var keep = Environment.GetEnvironmentVariable("CAIRN_ARTIFACTS");
        var root = keep is null ? _temp : Path.Combine(keep, "viewer-targets");
        Plain = ViewerProcess.Run(Content, "models/sink.glb", Path.Combine(root, "plain"), Side);
        Half = ViewerProcess.Run(Content, "models/sink.glb", Path.Combine(root, "half"), Side, "--targets", "Sink=0.5");
        Full = ViewerProcess.Run(Content, "models/sink.glb", Path.Combine(root, "full"), Side, "--targets", "Sink=1");
        Unknown = ViewerProcess.Run(Content, "models/sink.glb", Path.Combine(root, "unknown"), Side, "--targets", "Sunk=1");
        TooBig = ViewerProcess.Run(Content, "models/sink.glb", Path.Combine(root, "too-big"), Side, "--targets", "Sink=1.5");
        NoTargets = ViewerProcess.Run(Content, "models/cube.glb", Path.Combine(root, "no-targets"), Side, "--targets", "Sink=1");
    }

    internal ViewerResult Plain { get; }

    internal ViewerResult Half { get; }

    internal ViewerResult Full { get; }

    internal ViewerResult Unknown { get; }

    internal ViewerResult TooBig { get; }

    internal ViewerResult NoTargets { get; }

    public string Content { get; }

    public void Dispose() => Directory.Delete(_temp, recursive: true);
}

/// <summary>
/// The viewer's <c>--targets</c> on a cube whose top face sinks 0.3 at weight 1. The frame holds the rest bounds, so a point
/// on the +Z face's plane 0.4 above the middle is the wall's colour at rest and background once the weight has sunk the top
/// below it. Each check fails if the weights are ignored.
/// </summary>
[Trait("Tier", "regression")]
public class ViewerTargetsTests(SinkRuns runs) : IClassFixture<SinkRuns>
{
    private static readonly Vector3 Centre = new(0.5f, 0.5f, 0.5f);

    private const int Tolerance = 10;

    private static ViewerFrame Frame(ViewerResult run)
    {
        Assert.True(run.ExitCode == 0, run.Output);
        var view = JsonDocument.Parse(File.ReadAllText(Path.Combine(run.Out, "view.json"))).RootElement;
        var eye = view.GetProperty("views")[0].GetProperty("eye").EnumerateArray().Select(n => n.GetSingle()).ToArray();
        return new ViewerFrame(Path.Combine(run.Out, "side.png"), new Vector3(eye[0], eye[1], eye[2]), Centre);
    }

    /// <summary>Points on the +Z face's plane: 0.9 up is above the half-weight top (0.85), 0.8 is below it and above the full-weight top (0.7).</summary>
    private static readonly Vector3 High = new(0.5f, 0.9f, 1f);

    private static readonly Vector3 Mid = new(0.5f, 0.8f, 1f);

    private static readonly Vector3 Wall = new(0.5f, 0.5f, 1f);

    [Fact]
    public void Weights_sink_the_top_of_the_cube_below_points_that_are_wall_without_them()
    {
        var plain = Frame(runs.Plain);
        var half = Frame(runs.Half);
        var full = Frame(runs.Full);
        var background = plain.Pixel(4, 4);

        bool IsWall(ViewerFrame f, Vector3 p) => ViewerFrame.Difference(f.At(p), f.At(Wall)) < Tolerance;
        bool IsBackground(ViewerFrame f, Vector3 p) => ViewerFrame.Difference(f.At(p), background) < 3;

        Assert.True(IsWall(plain, High) && IsWall(plain, Mid), $"at rest {High} is {plain.At(High)} and {Mid} is {plain.At(Mid)}, the wall is {plain.At(Wall)}");
        Assert.True(IsBackground(half, High) && IsWall(half, Mid), $"at 0.5 {High} is {half.At(High)} (background {background}) and {Mid} is {half.At(Mid)} (wall {half.At(Wall)})");
        Assert.True(IsBackground(full, High) && IsBackground(full, Mid), $"at 1 {High} is {full.At(High)} and {Mid} is {full.At(Mid)}, background {background}");
    }

    [Fact]
    public void View_json_lists_the_starting_weights_only_when_given()
    {
        var plain = JsonDocument.Parse(File.ReadAllText(Path.Combine(runs.Plain.Out, "view.json"))).RootElement;
        Assert.False(plain.TryGetProperty("targets", out _));

        var half = JsonDocument.Parse(File.ReadAllText(Path.Combine(runs.Half.Out, "view.json"))).RootElement;
        Assert.Equal(0.5f, half.GetProperty("targets").GetProperty("Sink").GetSingle());
    }

    [Fact]
    public void A_target_the_file_lacks_or_a_weight_outside_0_to_1_stops_the_run_saying_so()
    {
        Assert.NotEqual(0, runs.Unknown.ExitCode);
        Assert.Contains("no target named 'Sunk'", runs.Unknown.Output);
        Assert.Contains("'Sink'", runs.Unknown.Output);

        Assert.NotEqual(0, runs.TooBig.ExitCode);
        Assert.Contains("outside 0 to 1", runs.TooBig.Output);

        Assert.NotEqual(0, runs.NoTargets.ExitCode);
        Assert.Contains("has no morph targets", runs.NoTargets.Output);
    }
}
