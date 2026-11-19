using Empyrean;

namespace Carve.Server;

/// <summary>What one render produced: the pixels, and the counts of the mesh they show.</summary>
internal sealed record RenderedFrame(RenderTargetPixels Pixels, int Vertices, int Faces)
{
    /// <summary>The store's version of the mesh drawn, so a sheet can tell whether its tiles agree.</summary>
    public long Version { get; init; }
}

/// <summary>
/// All one render asks for. The pump compares it whole, so a change in any field, a field added here
/// included, is a change of view: applied on one <c>Update</c>, drawn on a later one. A new field takes
/// a default that means today's render.
/// </summary>
internal sealed record RenderSpec(
    CameraSpec Camera,
    Projection Projection = Projection.Perspective,
    int Size = RenderSpec.DefaultSize,
    Look Mode = Look.Shaded,
    LabelSet Labels = LabelSet.None)
{
    public const int DefaultSize = 512;

    /// <summary>What to pick out. Compared by value, so an equal highlight is not a change of view.</summary>
    public Highlight Highlight { get; init; } = Highlight.None;

    /// <summary>The pose to draw the model in; null is the rest pose. Compared by value.</summary>
    public Carve.Core.Pose? Pose { get; init; }

    /// <summary>The morph target weights to draw the model at, applied before the pose; null is every target at 0. Compared by value.</summary>
    internal TargetWeights? Targets { get; init; }

    /// <summary>A sheet tile's caption, written on the image; a single render has none.</summary>
    public string? TileCaption { get; init; }

    /// <summary>The one mesh labels are written for, in a model of several; null labels the whole drawn mesh.</summary>
    public ElementWindow? LabelWindow { get; init; }

    /// <summary>The <c>uvs</c> look's mesh, as its face range in the drawn whole; null for every other look.</summary>
    public ElementWindow? UvFaces { get; init; }

    /// <summary>The bone whose weights are drawn as heat (mode is then <see cref="Look.Weights"/>), or null.</summary>
    public string? Weights { get; init; }

    /// <summary>The part whose subtree alone is drawn and framed on (T-94), or null for the whole model.</summary>
    public string? Part { get; init; }

    /// <summary>The kept level drawn (<see cref="Carve.Core.Model.CombinedAt"/>, T-94), or null for the live geometry.</summary>
    public int? Level { get; init; }

    /// <summary>The skeleton drawn over the model, or null for none. Compared by reference: one per call.</summary>
    internal BoneDraw? Bones { get; init; }

    /// <summary>A loaded <c>.glb</c> drawn in place of the model (render_glb), or null for the model. Compared by reference: one per call.</summary>
    internal GlbScene? Glb { get; init; }
}

/// <summary>One asked-for render, waiting for the frame thread.</summary>
internal sealed class RenderRequest(RenderSpec spec, long postedVersion) : OffscreenRequest<RenderSpec, RenderedFrame>(spec)
{
    /// <summary>The store's version at the post: the oldest mesh this request may show.</summary>
    public long PostedVersion => postedVersion;

    /// <summary>The version of the mesh the frame thread drew it on, set as it is served.</summary>
    public long ServedVersion { get; set; }
}

/// <summary>
/// From the tool to the frame thread. Needs no engine service, so a tool can resolve it with no
/// device; the frames it returns do carry engine pixels.
/// </summary>
internal class RenderQueue(ModelStore store) : OffscreenQueue<RenderRequest>
{
    public Task<RenderedFrame> Post(RenderSpec spec)
    {
        var request = new RenderRequest(spec, store.Current.Version);
        Enqueue(request);
        return request.Result;
    }
}
