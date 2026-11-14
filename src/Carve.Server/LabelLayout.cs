using System.Globalization;
using System.Numerics;
using Carve.Core;
using Empyrean;
using PolygonMesh = Carve.Core.PolygonMesh;

namespace Carve.Server;

/// <summary>Which ids <c>render</c> draws on the image.</summary>
public enum LabelSet
{
    None,
    Faces,
    Vertices,
    Both,
}

/// <summary>
/// One thing written on an image: <paramref name="Text"/> with its top left at <paramref name="TextAt"/>,
/// on the backing <paramref name="Backing"/>, and for a vertex the centre of its dot. When <paramref name="BackingPieces"/> is
/// set, the backing is drawn as those rectangles instead: a <c>uvs</c> corner label's backing clipped to the margin.
/// </summary>
public readonly record struct OverlayLabel(string Text, Vector2 TextAt, ScreenRect Backing, Vector2? Dot = null, ScreenRect[]? BackingPieces = null);

/// <summary>
/// One mesh's run of ids within the drawn model: labels are written for it alone, each id counted from its start,
/// so an id on the image is the one the mesh's own tools take.
/// </summary>
public readonly record struct ElementWindow(int FirstVertex, int FirstFace, int FaceCount);

/// <summary>
/// Pure CPU: what the overlay of one frame says and where. Text is placed on whole pixels, so the 8x16
/// bitmap stays crisp; a dot stays where its vertex projects, since it is anti-aliased.
/// </summary>
public static class LabelLayout
{
    /// <summary>The backing's margin round the text.</summary>
    private const int Padding = 2;

    public const float DotRadius = 3;

    /// <summary>Where a tile's caption text starts.</summary>
    private static readonly Vector2 CaptionAt = new(4, 4);

    /// <summary>A vertex's id, from its dot.</summary>
    private static readonly Vector2 VertexOffset = new(5, -18);

    public static readonly Vector4 TextColour = new(1, 1, 1, 1);

    public static readonly Vector4 BackingColour = new(0, 0, 0, 0.6f);

    /// <summary>
    /// The labels of a frame in draw order: faces, then vertices, ascending id. A face counts when it faces the
    /// camera; a vertex when it is on such a face. Nearer geometry is not considered, so a label can land on a
    /// hidden face. <paramref name="direction"/> is the camera's, used by orthographic, where the eye's place
    /// says nothing about what faces it.
    /// </summary>
    public static IReadOnlyList<OverlayLabel> Plan(
        PolygonMesh mesh,
        DrawnSpace space,
        ViewFrame frame,
        Vector3 direction,
        Projection projection,
        LabelSet labels,
        int size,
        Func<string, Vector2> measure,
        ElementWindow? window = null)
    {
        var plan = new List<OverlayLabel>();
        if (labels == LabelSet.None)
        {
            return plan;
        }

        Vector2 Pixel(Vector3 point) => ScreenProjection.ToPixel(space.Apply(point), frame.View, frame.Projection, size);

        var faces = window is { } w ? Enumerable.Range(w.FirstFace, w.FaceCount) : Enumerable.Range(0, mesh.Faces.Count);
        var facing = faces.Where(f => !mesh.IsFaceDeleted(f) &&
            Vector3.Dot(mesh.FaceNormal(f), projection == Projection.Orthographic ? direction : frame.Eye - space.Apply(mesh.FaceCentre(f))) > 0).ToList();

        if (labels is LabelSet.Faces or LabelSet.Both)
        {
            foreach (var face in facing)
            {
                var text = (face - (window?.FirstFace ?? 0)).ToString(CultureInfo.InvariantCulture);
                var extent = measure(text);
                var label = At(text, Whole(Pixel(mesh.FaceLabelPoint(face)) - extent / 2), extent);
                if (OnImage(label.Backing, size))
                {
                    plan.Add(label);
                }
            }
        }

        if (labels is LabelSet.Vertices or LabelSet.Both)
        {
            foreach (var vertex in facing.SelectMany(f => mesh.Faces[f].Corners).Distinct().Order())
            {
                var text = (vertex - (window?.FirstVertex ?? 0)).ToString(CultureInfo.InvariantCulture);
                var dot = Pixel(mesh.Vertices[vertex]);
                var label = At(text, Whole(dot + VertexOffset), measure(text)) with { Dot = dot };

                // The dot is part of the label: one on the image keeps it, though its id runs off the edge.
                var dotBox = new ScreenRect(dot.X - DotRadius, dot.Y - DotRadius, 2 * DotRadius, 2 * DotRadius);
                if (OnImage(label.Backing, size) || OnImage(dotBox, size))
                {
                    plan.Add(label);
                }
            }
        }

        return plan;
    }

    /// <summary>
    /// Bone names (T-39): each beside its head, up and to the right of it as a vertex's id is, in bone order. The bones
    /// are drawn on top of the model, so none is left out for being behind it; one wholly off the image is.
    /// </summary>
    public static IReadOnlyList<OverlayLabel> BoneLabels(
        IReadOnlyList<string> names, IReadOnlyList<Vector3> heads, DrawnSpace space, ViewFrame frame, int size, Func<string, Vector2> measure)
    {
        var plan = new List<OverlayLabel>();
        for (var i = 0; i < names.Count; i++)
        {
            var head = ScreenProjection.ToPixel(space.Apply(heads[i]), frame.View, frame.Projection, size);
            var label = At(names[i], Whole(head + VertexOffset), measure(names[i]));
            if (OnImage(label.Backing, size))
            {
                plan.Add(label);
            }
        }

        return plan;
    }

    /// <summary>How far a corner label of the <c>uvs</c> look stands off its corner, diagonally outward, in pixels.</summary>
    public const int CornerGap = 3;

    /// <summary>
    /// The <c>uvs</c> look's labels (T-58): the unit square's corners, (0,0) (1,0) (0,1) (1,1), each in the margin diagonally
    /// off its corner, always; then each face id centred on its UV point. A label wholly off the image is dropped.
    /// </summary>
    public static IReadOnlyList<OverlayLabel> UvLabels(UvWindow window, int size, IEnumerable<(string Text, Vector2 Uv)> faces, Func<string, Vector2> measure)
    {
        var plan = new List<OverlayLabel>();
        var (low, high) = (window.Pixel(new Vector2(0, 0), size), window.Pixel(new Vector2(1, 1), size));
        var square = (Left: MathF.Min(low.X, high.X), Top: MathF.Min(low.Y, high.Y), Right: MathF.Max(low.X, high.X), Bottom: MathF.Max(low.Y, high.Y));
        foreach (var (u, v) in new[] { (0, 0), (1, 0), (0, 1), (1, 1) })
        {
            var text = $"({u},{v})";
            var extent = measure(text);
            var corner = window.Pixel(new Vector2(u, v), size);
            var at = new Vector2(
                u == 0 ? corner.X - CornerGap - Padding - extent.X : corner.X + CornerGap + Padding,
                v == 0 ? corner.Y - CornerGap - Padding - extent.Y : corner.Y + CornerGap + Padding);

            // A margin narrower than the label (a sixteenth of the window: 32 px at 512) would cut it at the frame's edge,
            // so it slides inward along its margin until whole; its row stays off the square.
            at = new Vector2(
                Math.Clamp(at.X, Padding, Math.Max(Padding, size - Padding - extent.X)),
                Math.Clamp(at.Y, Padding, Math.Max(Padding, size - Padding - extent.Y)));
            var label = At(text, Whole(at), extent);

            // RV-43 (the person, 2026-11-14): where the slid label's backing reaches the square (at 256, 4 rows), it is clipped
            // to the margin; the text is unchanged.
            label = label with { BackingPieces = Outside(label.Backing, square) };
            if (OnImage(label.Backing, size)) plan.Add(label);
        }

        foreach (var (text, uv) in faces)
        {
            var extent = measure(text);
            var label = At(text, Whole(window.Pixel(uv, size) - extent / 2), extent);
            if (OnImage(label.Backing, size)) plan.Add(label);
        }

        return plan;
    }

    /// <summary>A tile's caption, at its fixed place; it runs off the tile's edge if it is longer.</summary>
    public static OverlayLabel Caption(string text, Func<string, Vector2> measure) => At(text, CaptionAt, measure(text));

    private static OverlayLabel At(string text, Vector2 at, Vector2 extent) =>
        new(text, at, new ScreenRect(at.X - Padding, at.Y - Padding, extent.X + 2 * Padding, extent.Y + 2 * Padding));

    /// <summary>The parts of <paramref name="box"/> outside <paramref name="square"/>, disjoint; the box alone when they do not meet.</summary>
    internal static ScreenRect[] Outside(ScreenRect box, (float Left, float Top, float Right, float Bottom) square)
    {
        var (left, top, right, bottom) = (box.X, box.Y, box.X + box.Width, box.Y + box.Height);
        if (right <= square.Left || left >= square.Right || bottom <= square.Top || top >= square.Bottom)
        {
            return [box];
        }

        var pieces = new List<ScreenRect>();
        void Add(float x0, float y0, float x1, float y1)
        {
            if (x1 > x0 && y1 > y0) pieces.Add(new ScreenRect(x0, y0, x1 - x0, y1 - y0));
        }

        var (bandTop, bandBottom) = (MathF.Max(top, square.Top), MathF.Min(bottom, square.Bottom));
        Add(left, top, right, MathF.Min(bottom, square.Top));
        Add(left, bandTop, MathF.Min(right, square.Left), bandBottom);
        Add(MathF.Max(left, square.Right), bandTop, right, bandBottom);
        Add(left, MathF.Max(top, square.Bottom), right, bottom);
        return [.. pieces];
    }

    private static Vector2 Whole(Vector2 at) => new(MathF.Round(at.X), MathF.Round(at.Y));

    private static bool OnImage(ScreenRect box, int size) =>
        box.X < size && box.Y < size && box.X + box.Width > 0 && box.Y + box.Height > 0;
}
