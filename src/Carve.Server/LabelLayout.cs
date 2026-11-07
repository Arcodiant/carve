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
/// on the backing <paramref name="Backing"/>, and for a vertex the centre of its dot.
/// </summary>
public readonly record struct OverlayLabel(string Text, Vector2 TextAt, ScreenRect Backing, Vector2? Dot = null);

/// <summary>
/// Pure CPU: what the overlay of one frame says and where. Text is placed on whole pixels, so the 8x16
/// bitmap stays crisp; a dot stays where its vertex projects, since it is anti-aliased.
/// </summary>
public static class LabelLayout
{
    /// <summary>The backing's margin round the text.</summary>
    public const int Padding = 2;

    public const float DotRadius = 3;

    /// <summary>Where a tile's caption text starts.</summary>
    public static readonly Vector2 CaptionAt = new(4, 4);

    /// <summary>A vertex's id, from its dot.</summary>
    public static readonly Vector2 VertexOffset = new(5, -18);

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
        Func<string, Vector2> measure)
    {
        var plan = new List<OverlayLabel>();
        if (labels == LabelSet.None)
        {
            return plan;
        }

        Vector2 Pixel(Vector3 point) => ScreenProjection.ToPixel(space.Apply(point), frame.View, frame.Projection, size);

        var facing = Enumerable.Range(0, mesh.Faces.Count).Where(f =>
            Vector3.Dot(mesh.FaceNormal(f), projection == Projection.Orthographic ? direction : frame.Eye - space.Apply(mesh.FaceCentre(f))) > 0).ToList();

        if (labels is LabelSet.Faces or LabelSet.Both)
        {
            foreach (var face in facing)
            {
                var text = face.ToString(CultureInfo.InvariantCulture);
                var extent = measure(text);
                var label = At(text, Whole(Pixel(mesh.FaceCentre(face)) - extent / 2), extent);
                if (OnImage(label.Backing, size))
                {
                    plan.Add(label);
                }
            }
        }

        if (labels is LabelSet.Vertices or LabelSet.Both)
        {
            foreach (var vertex in facing.SelectMany(f => mesh.Faces[f]).Distinct().Order())
            {
                var text = vertex.ToString(CultureInfo.InvariantCulture);
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

    /// <summary>A tile's caption, at its fixed place; it runs off the tile's edge if it is longer.</summary>
    public static OverlayLabel Caption(string text, Func<string, Vector2> measure) => At(text, CaptionAt, measure(text));

    private static OverlayLabel At(string text, Vector2 at, Vector2 extent) =>
        new(text, at, new ScreenRect(at.X - Padding, at.Y - Padding, extent.X + 2 * Padding, extent.Y + 2 * Padding));

    private static Vector2 Whole(Vector2 at) => new(MathF.Round(at.X), MathF.Round(at.Y));

    private static bool OnImage(ScreenRect box, int size) =>
        box.X < size && box.Y < size && box.X + box.Width > 0 && box.Y + box.Height > 0;
}
