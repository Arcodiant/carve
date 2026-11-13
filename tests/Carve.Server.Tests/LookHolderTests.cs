using Carve.Core;
using Carve.Core.Tests;

namespace Carve.Server.Tests;

/// <summary>
/// When the look's meshes are rebuilt, and that every handle made is destroyed once: what <c>RenderSystem</c> does with
/// the engine's meshes, on named strings, so no device is needed.
/// </summary>
[Trait("Tier", "unit")]
public class LookHolderTests
{
    private static readonly DrawnSpace Space = new(0, 0, 0, 1);

    private sealed class Parts : LookHolder<string>.IParts
    {
        private int _made;

        public List<string> Log { get; } = [];

        /// <summary>The 0-based part (fill, edges, orange) whose making throws, if any.</summary>
        public int? FailAt { get; set; }

        public HashSet<string> Live { get; } = [];

        public IReadOnlyList<Func<string?>> Make(PolygonMesh mesh, DrawnSpace space, Look look, Highlight highlight, ElementWindow? faces) =>
            [.. new[] { "fill", "edges", "orange" }.Select((part, k) => (Func<string?>)(() => Create(part, k, look, highlight)))];

        private string? Create(string part, int k, Look look, Highlight highlight)
        {
            if (FailAt == k)
            {
                throw new InvalidOperationException($"no {part}");
            }

            // As LookMeshes.Compose: edge lines in wireframe only, orange lines with a highlight only.
            if ((k == 1 && look != Look.Wireframe) || (k == 2 && highlight == Highlight.None))
            {
                return null;
            }

            var name = $"{part}#{++_made} {look}";
            Live.Add(name);
            Log.Add($"make {name}");
            return name;
        }

        public void Install(string fill, string? edges, string? orange, string? seams) => Log.Add($"install {fill}, {edges ?? "-"}, {orange ?? "-"}");

        public void Destroy(string mesh)
        {
            Assert.True(Live.Remove(mesh), $"{mesh} destroyed twice or never made");
            Log.Add($"destroy {mesh}");
        }
    }

    private static PolygonMesh Mesh() => TestMeshes.Cube();

    [Fact]
    public void A_camera_change_alone_rebuilds_nothing()
    {
        var parts = new Parts();
        var looks = new LookHolder<string>(parts);
        looks.Show(Mesh(), Space);
        looks.Apply(Look.Wireframe, Highlight.None, Space);
        parts.Log.Clear();

        looks.Apply(Look.Wireframe, Highlight.None, Space);
        looks.Apply(Look.Wireframe, Highlight.Of([], [], []), Space);

        Assert.Empty(parts.Log);
    }

    [Fact]
    public void A_new_mesh_is_drawn_in_the_look_and_highlight_already_asked_for()
    {
        var parts = new Parts();
        var looks = new LookHolder<string>(parts);
        looks.Show(Mesh(), Space);
        var picked = Highlight.Of([], [], [3]);
        looks.Apply(Look.Wireframe, picked, Space);
        parts.Log.Clear();

        looks.Show(Mesh(), Space);

        Assert.Equal(["make fill#5 Wireframe", "make edges#6 Wireframe", "make orange#7 Wireframe"], parts.Log.Take(3));
    }

    [Fact]
    public void Every_old_handle_is_destroyed_once_after_the_new_ones_are_installed()
    {
        var parts = new Parts();
        var looks = new LookHolder<string>(parts);
        looks.Show(Mesh(), Space);
        looks.Apply(Look.Wireframe, Highlight.Of([], [], [3]), Space);
        parts.Log.Clear();

        looks.Apply(Look.Shaded, Highlight.None, Space);

        Assert.Equal(
            [
                "make fill#5 Shaded",
                "install fill#5 Shaded, -, -",
                "destroy fill#2 Wireframe",
                "destroy edges#3 Wireframe",
                "destroy orange#4 Wireframe",
            ],
            parts.Log);
        Assert.Equal(["fill#5 Shaded"], parts.Live);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void A_failed_make_destroys_what_it_made_and_leaves_the_scene_as_it_was(int failAt)
    {
        var parts = new Parts();
        var looks = new LookHolder<string>(parts);
        looks.Show(Mesh(), Space);
        parts.Log.Clear();
        parts.FailAt = failAt;

        Assert.Throws<InvalidOperationException>(() => looks.Apply(Look.Wireframe, Highlight.Of([], [], [3]), Space));

        Assert.DoesNotContain(parts.Log, line => line.StartsWith("install", StringComparison.Ordinal));
        Assert.Equal(["fill#1 Shaded"], parts.Live);
        Assert.Equal(Look.Shaded, looks.Look);
    }
}
