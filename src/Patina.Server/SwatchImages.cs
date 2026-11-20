using System.Numerics;
using Patina.Graphs.Imaging;

namespace Patina.Server;

/// <summary>
/// swatch's CPU half (T-114): the flat and tiled views made from the evaluated texels, nearest-scaled, and the sheet laid
/// out around them, its labels drawn from a coverage atlas the engine's font drew (<see cref="SwatchSystem"/>). Nothing here
/// touches the engine, so flat and tiled are exact texels and testable with no GPU.
/// </summary>
public static class SwatchImages
{
    /// <summary>The largest evaluation size swatch draws; above it, export writes the PNGs.</summary>
    public const int MaxSize = 2048;

    /// <summary>The largest tiled view, in pixels a side: 3 x size, scaled down to this when larger.</summary>
    public const int MaxTiled = 1536;

    /// <summary>The sheet's flat and tiled cells, square.</summary>
    public const int Cell = 256;

    /// <summary>The sheet's sphere cell, square, and the size of the sphere view.</summary>
    public const int SphereCell = 512;

    /// <summary>The title strip along the sheet's top.</summary>
    public const int Title = 24;

    /// <summary>The slate between cells.</summary>
    public const int Gutter = 2;

    /// <summary>The band along the top of each cell where its label may sit (backing included).</summary>
    public const int LabelBand = 22;

    /// <summary>
    /// Carve's dark slate, <c>(0.03, 0.035, 0.04)</c> linear, as a frame shows it: through the target's half floats, then
    /// sRGB, alpha opaque (carve's <c>SwatchSheet.Srgb</c> of its <c>RenderComposition.Background</c>).
    /// </summary>
    public static readonly Vector4 Background = new(0.03f, 0.035f, 0.04f, 1f);

    public static byte[] Slate { get; } = Srgb(Background);

    /// <summary>
    /// The source index nearest-sampled for destination index <paramref name="at"/> of <paramref name="to"/>, from
    /// <paramref name="from"/>: the source texel under the destination pixel's centre.
    /// </summary>
    public static int Nearest(int at, int to, int from) => (int)((2L * at + 1) * from / (2L * to));

    /// <summary>The <paramref name="size"/>-square image <paramref name="rgba"/> nearest-scaled to <paramref name="to"/> a side.</summary>
    public static byte[] Flat(byte[] rgba, int size, int to)
    {
        if (to == size) return rgba;
        var pixels = new byte[to * to * 4];
        for (var y = 0; y < to; y++)
        {
            var sy = Nearest(y, to, size);
            for (var x = 0; x < to; x++)
            {
                Buffer.BlockCopy(rgba, (sy * size + Nearest(x, to, size)) * 4, pixels, (y * to + x) * 4, 4);
            }
        }
        return pixels;
    }

    /// <summary>
    /// The 3 x 3 repeat of the <paramref name="size"/>-square image, nearest-scaled to <paramref name="to"/> a side.
    /// </summary>
    public static byte[] Tiled(byte[] rgba, int size, int to)
    {
        var pixels = new byte[to * to * 4];
        var across = 3 * size;
        for (var y = 0; y < to; y++)
        {
            var sy = Nearest(y, to, across) % size;
            for (var x = 0; x < to; x++)
            {
                var sx = Nearest(x, to, across) % size;
                Buffer.BlockCopy(rgba, (sy * size + sx) * 4, pixels, (y * to + x) * 4, 4);
            }
        }
        return pixels;
    }

    /// <summary>The tiled view's side for an evaluation size: 3 x size, at most <see cref="MaxTiled"/>.</summary>
    public static int TiledSide(int size) => Math.Min(3 * size, MaxTiled);

    /// <summary>A linear colour as a frame shows it, as carve's sheets work it out.</summary>
    public static byte[] Srgb(Vector4 linear)
    {
        static byte Encode(float value)
        {
            var c = (double)(Half)value;
            return (byte)Math.Round(255 * (c <= 0.0031308 ? 12.92 * c : 1.055 * Math.Pow(c, 1 / 2.4) - 0.055));
        }
        return [Encode(linear.X), Encode(linear.Y), Encode(linear.Z), 255];
    }
}

/// <summary>
/// Where things sit on the sheet of <paramref name="Columns"/> outputs: a column per output in role order, flat over tiled,
/// the sphere cell on the right; a 24 px title strip on top.
/// </summary>
public sealed record SheetLayout(int Columns)
{
    public int Width => Columns * (SwatchImages.Cell + SwatchImages.Gutter) + SwatchImages.SphereCell;

    public int Height => SwatchImages.Title + 2 * SwatchImages.Cell + SwatchImages.Gutter;

    public int ColumnLeft(int column) => column * (SwatchImages.Cell + SwatchImages.Gutter);

    public static int FlatTop => SwatchImages.Title;

    public static int TiledTop => SwatchImages.Title + SwatchImages.Cell + SwatchImages.Gutter;

    public int SphereLeft => ColumnLeft(Columns);

    public static int SphereTop => SwatchImages.Title + 1;
}

/// <summary>
/// The labels' text, drawn white on black by the engine's font one to a row of the atlas (<see cref="Pitch"/> apart, text
/// at <see cref="Inset"/>); the sheet reads each row's red channel as the text's coverage.
/// </summary>
public static class LabelAtlas
{
    /// <summary>
    /// The atlas's side: 32 rows, so a title, 15 columns' two labels and the sphere's. It was 512 (16 rows, 7 columns) until
    /// T-134; rows are where they were, so a sheet of 7 columns or fewer draws the same pixels.
    /// </summary>
    public const int Size = 1024;

    /// <summary>The widest a label is drawn, its text and padding: as it was when the atlas was 512 a side.</summary>
    public const int LabelWidth = 512 - Inset + Padding;

    public const int Pitch = 32;

    /// <summary>Where a row's text starts, from the row's top-left corner, both ways.</summary>
    public const int Inset = 4;

    /// <summary>Carve's label backing (black at 0.6) and its padding around the text.</summary>
    public const float Backing = 0.6f;

    public const int Padding = 2;

    /// <summary>The rows the atlas holds.</summary>
    public const int Rows = Size / Pitch;
}

/// <summary>Composes the sheet from the evaluated outputs, the sphere frame and the label atlas.</summary>
public static class SwatchSheet
{
    /// <summary>The labels in atlas order: the title, then per column the flat and the tiled cell's, then the sphere's.</summary>
    public static IReadOnlyList<string> Labels(string title, IReadOnlyList<OutputRole> roles, float angle) =>
        Labels(title, [.. roles.Select(OutputRoles.Name)], angle);

    /// <summary>The labels for a sheet whose columns are <paramref name="outputs"/>, roles' names and named outputs' names, in sheet order.</summary>
    public static IReadOnlyList<string> Labels(string title, IReadOnlyList<string> outputs, float angle) =>
        [title, .. outputs.SelectMany(r => new[] { r, r + " 3 x 3" }),
            $"sphere, Pbr, carve lights, angle {angle.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}"];

    /// <param name="images">Each output's RGBA, <paramref name="size"/> a side, in role order.</param>
    /// <param name="sphere">The sphere view, <see cref="SwatchImages.SphereCell"/> a side, RGBA.</param>
    /// <param name="atlas">The label atlas, <see cref="LabelAtlas.Size"/> a side, RGBA, in <see cref="Labels"/>' order.</param>
    /// <param name="extents">Each label's text extent in pixels, as the font measured it.</param>
    public static (byte[] Pixels, SheetLayout Layout) Compose(IReadOnlyList<byte[]> images, int size, byte[] sphere,
        byte[] atlas, IReadOnlyList<Vector2> extents)
    {
        var layout = new SheetLayout(images.Count);
        var width = layout.Width;
        var pixels = new byte[width * layout.Height * 4];
        for (var at = 0; at < pixels.Length; at += 4) SwatchImages.Slate.CopyTo(pixels, at);

        void Place(byte[] image, int side, int left, int top)
        {
            for (var row = 0; row < side; row++)
            {
                Buffer.BlockCopy(image, row * side * 4, pixels, ((top + row) * width + left) * 4, side * 4);
            }
        }

        // Draws atlas row `row`'s text with its top-left at (left, top), over a backing when asked.
        void Label(int row, int left, int top, bool backed)
        {
            var extent = extents[row];
            var w = Math.Min((int)MathF.Ceiling(extent.X) + 2 * LabelAtlas.Padding, LabelAtlas.LabelWidth);
            var h = Math.Min((int)MathF.Ceiling(extent.Y) + 2 * LabelAtlas.Padding, SwatchImages.LabelBand - LabelAtlas.Padding);
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w && left + x < width; x++)
                {
                    var ax = LabelAtlas.Inset - LabelAtlas.Padding + x;
                    var ay = row * LabelAtlas.Pitch + LabelAtlas.Inset - LabelAtlas.Padding + y;
                    var coverage = atlas[(ay * LabelAtlas.Size + ax) * 4] / 255f;
                    var p = ((top + y) * width + left + x) * 4;
                    for (var c = 0; c < 3; c++)
                    {
                        var under = backed ? pixels[p + c] * (1 - LabelAtlas.Backing) : pixels[p + c];
                        pixels[p + c] = (byte)MathF.Round(under + (255 - under) * coverage);
                    }
                }
            }
        }

        for (var column = 0; column < images.Count; column++)
        {
            var left = layout.ColumnLeft(column);
            Place(SwatchImages.Flat(images[column], size, SwatchImages.Cell), SwatchImages.Cell, left, SheetLayout.FlatTop);
            Place(SwatchImages.Tiled(images[column], size, SwatchImages.Cell), SwatchImages.Cell, left, SheetLayout.TiledTop);
            Label(1 + 2 * column, left + LabelAtlas.Padding, SheetLayout.FlatTop + LabelAtlas.Padding, true);
            Label(2 + 2 * column, left + LabelAtlas.Padding, SheetLayout.TiledTop + LabelAtlas.Padding, true);
        }
        Place(sphere, SwatchImages.SphereCell, layout.SphereLeft, SheetLayout.SphereTop);
        Label(1 + 2 * images.Count, layout.SphereLeft + LabelAtlas.Padding, SheetLayout.SphereTop + LabelAtlas.Padding, true);
        Label(0, LabelAtlas.Padding, LabelAtlas.Padding, false);
        return (pixels, layout);
    }
}
