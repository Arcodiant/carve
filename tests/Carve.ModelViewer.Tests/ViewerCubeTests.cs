using System.Numerics;
using System.Text.Json;
using System.Xml.Linq;
using Xunit;

namespace Carve.ModelViewer.Tests;

/// <summary>The fixture cube and its runs, made once for the class: each run starts a Vulkan device.</summary>
public sealed class CubeRuns : IDisposable
{
    public const string Front = "front=1.6,1.2,2.2";
    public const string MinusX = "minus-x=-2.2,1.2,1.6";

    private readonly string _temp = Directory.CreateTempSubdirectory("carve-viewer-").FullName;

    public CubeRuns()
    {
        Content = Path.Combine(_temp, "Content");
        Directory.CreateDirectory(Path.Combine(Content, "models"));
        File.WriteAllBytes(Path.Combine(Content, "models/cube.glb"), CubeFixture.Write());

        // A queued run keeps what's in CAIRN_ARTIFACTS, which is what `cairn test artifacts` returns.
        var keep = Environment.GetEnvironmentVariable("CAIRN_ARTIFACTS");
        var root = keep is null ? _temp : Path.Combine(keep, "viewer-cube");

        FirstRun = ViewerProcess.Run(Content, "models/cube.glb", Path.Combine(root, "first"), Front);
        SecondRun = ViewerProcess.Run(Content, "models/cube.glb", Path.Combine(root, "second"), Front);
        MinusXRun = ViewerProcess.Run(Content, "models/cube.glb", Path.Combine(root, "minus-x"), MinusX);
    }

    public string Content { get; }

    internal ViewerResult FirstRun { get; }

    internal ViewerResult SecondRun { get; }

    internal ViewerResult MinusXRun { get; }

    public string Scratch(string name) => Path.Combine(_temp, name);

    public void Dispose() => Directory.Delete(_temp, recursive: true);
}

[Trait("Tier", "regression")]
public class ViewerCubeTests(CubeRuns runs) : IClassFixture<CubeRuns>
{
    private static readonly Vector3 Centre = new(0.5f, 0.5f, 0.5f);

    /// <summary>The middle of each face: as far from every edge as the face allows, so no anti-aliased pixel.</summary>
    private static readonly Vector3 Top = new(0.5f, 1f, 0.5f);
    private static readonly Vector3 PlusZ = new(0.5f, 0.5f, 1f);
    private static readonly Vector3 PlusX = new(1f, 0.5f, 0.5f);
    private static readonly Vector3 MinusXFace = new(0f, 0.5f, 0.5f);

    private const int Tolerance = 10;

    private static JsonElement ViewJson(ViewerResult run) =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(run.Out, "view.json"))).RootElement;

    private static ViewerFrame Frame(ViewerResult run, string view)
    {
        var eye = ViewJson(run).GetProperty("views")[0].GetProperty("eye").EnumerateArray()
            .Select(n => n.GetSingle()).ToArray();
        return new ViewerFrame(Path.Combine(run.Out, view + ".png"), new Vector3(eye[0], eye[1], eye[2]), Centre);
    }

    private static void AssertFacesApart(ViewerFrame frame, Vector3 left, Vector3 right)
    {
        var background = frame.Pixel(4, 4);
        var top = frame.At(Top);
        var leftFace = frame.At(left);
        var rightFace = frame.At(right);

        foreach (var (name, face) in new[] { ("top", top), ("left", leftFace), ("right", rightFace) })
        {
            Assert.True(
                ViewerFrame.Difference(face, background) >= Tolerance,
                $"the {name} face {face} is within {Tolerance}/255 of the background {background}");
        }

        Assert.True(ViewerFrame.Difference(top, leftFace) >= Tolerance, $"top {top} and left {leftFace}");
        Assert.True(ViewerFrame.Difference(top, rightFace) >= Tolerance, $"top {top} and right {rightFace}");
        Assert.True(ViewerFrame.Difference(leftFace, rightFace) >= Tolerance, $"left {leftFace} and right {rightFace}");
    }

    [Fact]
    public void The_run_exits_zero_and_writes_only_to_its_out_folder()
    {
        Assert.True(runs.FirstRun.ExitCode == 0, runs.FirstRun.Output);
        Assert.Equal(
            ["front.png", "view.json"],
            Directory.GetFiles(runs.FirstRun.Out).Select(Path.GetFileName).Order());
        Assert.Empty(Directory.GetFileSystemEntries(runs.FirstRun.WorkingFolder));
        Assert.Equal(["cube.glb"], Directory.GetFiles(Path.Combine(runs.Content, "models")).Select(Path.GetFileName));
    }

    [Fact]
    public void View_json_says_what_was_loaded()
    {
        var view = ViewJson(runs.FirstRun);

        Assert.Equal("models/cube.glb", view.GetProperty("model").GetString());
        Assert.Equal(12, view.GetProperty("triangles").GetInt32());
        Assert.Equal([0f, 0f, 0f], view.GetProperty("bounds").GetProperty("min").EnumerateArray().Select(n => n.GetSingle()));
        Assert.Equal([1f, 1f, 1f], view.GetProperty("bounds").GetProperty("max").EnumerateArray().Select(n => n.GetSingle()));

        var material = Assert.Single(view.GetProperty("materials").EnumerateArray());
        Assert.Equal([0.72f, 0.72f, 0.70f, 1f], material.GetProperty("baseColor").EnumerateArray().Select(n => n.GetSingle()));
        Assert.Equal(0f, material.GetProperty("metallic").GetSingle());
        Assert.Equal(1f, material.GetProperty("roughness").GetSingle());
        Assert.True(material.GetProperty("pbr").GetBoolean());

        Assert.Equal("front", Assert.Single(view.GetProperty("views").EnumerateArray()).GetProperty("name").GetString());
        Assert.Equal(3, view.GetProperty("sun").GetProperty("toSun").GetArrayLength());
    }

    /// <summary>
    /// The frame's corners: the auto-framing leaves a margin on every side, so at 4 pixels in from a
    /// corner nothing of a box can reach, and all four must be the one dark colour.
    /// </summary>
    [Fact]
    public void Corners_are_a_dark_background()
    {
        var frame = Frame(runs.FirstRun, "front");

        Assert.Equal(512, frame.Width);
        Assert.Equal(512, frame.Height);
        var corner = frame.Pixel(4, 4);
        Assert.Equal(corner, frame.Pixel(507, 4));
        Assert.Equal(corner, frame.Pixel(4, 507));
        Assert.Equal(corner, frame.Pixel(507, 507));
        Assert.True(Math.Max(corner.R, Math.Max(corner.G, corner.B)) < 32, $"the background {corner} is not dark");
    }

    /// <summary>Each face at its middle, projected from the eye in view.json by the test's own camera.</summary>
    [Fact]
    public void The_top_plus_z_and_plus_x_faces_differ_from_the_background_and_each_other()
    {
        AssertFacesApart(Frame(runs.FirstRun, "front"), PlusZ, PlusX);
    }

    /// <summary>
    /// What a mirrored or flipped camera would fail, the cube being the same shape every way round:
    /// the sun puts top above +Z above +X, and +Z lands left of +X with the top above both.
    /// </summary>
    [Fact]
    public void The_faces_are_the_right_way_round()
    {
        var frame = Frame(runs.FirstRun, "front");
        var (top, plusZ, plusX) = (frame.At(Top), frame.At(PlusZ), frame.At(PlusX));

        Assert.True(frame.Project(PlusZ).X < frame.Project(PlusX).X, "+Z is not left of +X");
        Assert.True(frame.Project(Top).Y < frame.Project(PlusZ).Y, "the top is not above +Z");
        Assert.True(top.R > plusZ.R, $"top {top} is not brighter than +Z {plusZ}");
        Assert.True(plusZ.R > plusX.R, $"+Z {plusZ} is not brighter than +X {plusX}");
    }

    /// <summary>The second agreed view, whose three faces the same sun must also keep apart.</summary>
    [Fact]
    public void The_sun_keeps_the_faces_of_the_minus_x_view_apart()
    {
        Assert.True(runs.MinusXRun.ExitCode == 0, runs.MinusXRun.Output);
        var frame = Frame(runs.MinusXRun, "minus-x");

        AssertFacesApart(frame, MinusXFace, PlusZ);
        Assert.True(frame.Project(MinusXFace).X < frame.Project(PlusZ).X, "-X is not left of +Z");
        Assert.True(frame.At(PlusZ).R > frame.At(MinusXFace).R, "+Z is not brighter than -X");
    }

    [Fact]
    public void A_second_run_gives_byte_identical_frames()
    {
        Assert.True(runs.SecondRun.ExitCode == 0, runs.SecondRun.Output);
        Assert.Equal(
            File.ReadAllBytes(Path.Combine(runs.FirstRun.Out, "front.png")),
            File.ReadAllBytes(Path.Combine(runs.SecondRun.Out, "front.png")));
    }

    [Fact]
    public void A_missing_model_fails_naming_the_file()
    {
        var run = ViewerProcess.Run(runs.Content, "models/missing.glb", runs.Scratch("missing"));

        Assert.NotEqual(0, run.ExitCode);
        Assert.Contains("models/missing.glb", run.Output);
        Assert.False(File.Exists(Path.Combine(run.Out, "view.json")));
    }

    [Fact]
    public void A_corrupt_glb_fails_naming_the_file()
    {
        File.WriteAllBytes(Path.Combine(runs.Content, "models/corrupt.glb"), [1, 2, 3, 4, 5, 6, 7, 8]);
        try
        {
            var run = ViewerProcess.Run(runs.Content, "models/corrupt.glb", runs.Scratch("corrupt"));

            Assert.NotEqual(0, run.ExitCode);
            Assert.Contains("models/corrupt.glb", run.Output);
            Assert.False(File.Exists(Path.Combine(run.Out, "view.json")));
        }
        finally
        {
            File.Delete(Path.Combine(runs.Content, "models/corrupt.glb"));
        }
    }

    [Theory]
    [InlineData("up=0,1,0", "looks straight along Y")]
    [InlineData("none=0,0,0", "has no direction")]
    [InlineData("../out=1,1,1", "is not <name>=<x,y,z>")]
    public void A_view_that_breaks_the_agreed_look_is_refused(string view, string message)
    {
        var run = ViewerProcess.Run(runs.Content, "models/cube.glb", runs.Scratch("refused"), view);

        Assert.NotEqual(0, run.ExitCode);
        Assert.Contains(message, run.Output);
    }

    [Fact]
    public void The_viewer_references_no_Carve_project()
    {
        var project = XDocument.Load(Path.Combine(ViewerProcess.RepoRoot, "spikes/Carve.ModelViewer/Carve.ModelViewer.csproj"));

        Assert.Empty(project.Descendants("ProjectReference"));
    }
}
