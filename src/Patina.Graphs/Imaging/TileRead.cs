namespace Patina.Graphs.Imaging;

/// <summary>
/// What a node reads one input through: the tiles of that input covering the rectangle the node asked for
/// (<see cref="Nodes.NodeType.InputRect"/>), read by logical texel. A coordinate wraps by the image's size, as every
/// texture tiles, so <c>(x - 1, y)</c> at <c>x = 0</c> reads column <c>size - 1</c>. A texel is readable when, wrapped,
/// it lies in <see cref="Asked"/>: <c>Wrap(x - Asked.X, size) &lt; Asked.Width</c>, and the same for y. Reading any other
/// throws, so a node that asks for less than it reads fails at once rather than giving a quiet wrong value.
/// <para>An axis asked <c>size</c> or more across is held as the whole axis, <c>[0, size)</c>, so any read on it is
/// allowed.</para>
/// <para>A read may hold several rectangles (<see cref="Nodes.NodeType.InputRects"/>, T-153): a texel is readable when
/// it lies in any of them, the first tried first.</para>
/// </summary>
public sealed class TileRead
{
    private readonly int _x, _y, _width, _height, _size, _tile, _shift, _mask, _coverX, _coverY, _columns, _channels;
    private readonly Tile[] _tiles;
    private readonly TileRead[] _rest = [];

    /// <summary>
    /// <paramref name="tiles"/> are the canonical tiles of side <paramref name="tileSize"/> covering
    /// <c>Clamp(asked, size)</c>, row-major over <see cref="Cover"/>: a tile index <c>(c, r)</c> of the cover is the
    /// image's tile <c>(Wrap(c, size / tileSize), Wrap(r, size / tileSize))</c>.
    /// </summary>
    public TileRead(PortKind kind, int size, int tileSize, TexelRect asked, Tile[] tiles)
    {
        if (kind == PortKind.Any) throw new ArgumentException("a read is greyscale or colour", nameof(kind));
        if (tileSize < 1 || (tileSize & (tileSize - 1)) != 0 || size % tileSize != 0)
            throw new ArgumentOutOfRangeException(nameof(tileSize), tileSize, "a tile's side is a power of two dividing the size");
        Kind = kind; Size = size;
        Asked = Clamp(asked, size);
        Parts = [Asked];
        var cover = Cover(Asked, size, tileSize);
        if (tiles.Length != cover.Width * cover.Height)
            throw new ArgumentException($"the cover of {Asked} is {cover.Width} x {cover.Height} tiles, but {tiles.Length} were given", nameof(tiles));
        _tiles = tiles;
        _x = Asked.X; _y = Asked.Y; _width = Asked.Width; _height = Asked.Height; _size = size;
        _tile = tileSize; _shift = System.Numerics.BitOperations.Log2((uint)tileSize); _mask = tileSize - 1;
        _coverX = cover.X * tileSize; _coverY = cover.Y * tileSize; _columns = cover.Width;
        _channels = PortKinds.Channels(kind);
    }

    /// <summary>
    /// A read holding several rectangles, each with the tiles covering it as the single-rectangle constructor takes them:
    /// a texel is readable when it lies in any. There is at least one.
    /// </summary>
    public TileRead(PortKind kind, int size, int tileSize, IReadOnlyList<(TexelRect Asked, Tile[] Tiles)> parts)
        : this(kind, size, tileSize, parts.Count > 0 ? parts[0].Asked : throw new ArgumentException("a read holds at least one rectangle", nameof(parts)), parts[0].Tiles)
    {
        _rest = [.. parts.Skip(1).Select(p => new TileRead(kind, size, tileSize, p.Asked, p.Tiles))];
        Parts = [Asked, .. _rest.Select(r => r.Asked)];
    }

    public PortKind Kind { get; }
    public int Size { get; }

    /// <summary>The rectangle asked for, each axis <c>size</c> or more across clamped to <c>[0, size)</c>.</summary>
    public TexelRect Asked { get; }

    /// <summary>Every rectangle the read holds, <see cref="Asked"/> first, each clamped as it is.</summary>
    public IReadOnlyList<TexelRect> Parts { get; } = [];

    /// <summary><paramref name="asked"/> with each axis <paramref name="size"/> or more across made <c>[0, size)</c>.</summary>
    public static TexelRect Clamp(TexelRect asked, int size) => new(
        asked.Width >= size ? 0 : asked.X, asked.Height >= size ? 0 : asked.Y,
        Math.Min(asked.Width, size), Math.Min(asked.Height, size));

    /// <summary>
    /// The tiles covering <paramref name="asked"/> (clamped first), in tile units and not wrapped: tile columns
    /// <c>X</c> to <c>Right - 1</c>, from <c>floor(asked.X / tileSize)</c> to <c>floor((asked.Right - 1) / tileSize)</c>.
    /// </summary>
    public static TexelRect Cover(TexelRect asked, int size, int tileSize)
    {
        asked = Clamp(asked, size);
        int c0 = FloorDiv(asked.X, tileSize), c1 = FloorDiv(asked.Right - 1, tileSize);
        int r0 = FloorDiv(asked.Y, tileSize), r1 = FloorDiv(asked.Bottom - 1, tileSize);
        return new(c0, r0, c1 - c0 + 1, r1 - r0 + 1);
    }

    private static int FloorDiv(int v, int d) => v >= 0 ? v / d : -((-v + d - 1) / d);

    // The offset of v from the cover's first texel on one axis, after the wrap; false when, wrapped, it is not asked for.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private bool TryOffset(int v, int origin, int extent, int cover, out int offset)
    {
        var a = v - origin;
        if ((uint)a >= (uint)extent)
        {
            a = Tile.Wrap(a, _size);
            if (a >= extent) { offset = 0; return false; }
        }
        offset = origin + a - cover;
        return true;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private bool TryIndex(int x, int y, out Tile tile, out int index)
    {
        if (!TryOffset(x, _x, _width, _coverX, out var lx) || !TryOffset(y, _y, _height, _coverY, out var ly))
        {
            tile = null!; index = 0;
            return false;
        }
        tile = _tiles[(ly >> _shift) * _columns + (lx >> _shift)];
        index = (((ly & _mask) << _shift) + (lx & _mask)) * _channels;
        return true;
    }

    private int Index(int x, int y, out Tile tile)
    {
        if (TryIndex(x, y, out tile, out var index)) return index;
        foreach (var part in _rest)
            if (part.TryIndex(x, y, out tile, out index)) return index;
        throw Outside(x, y);
    }

    private ArgumentOutOfRangeException Outside(int x, int y) => new("x",
        $"texel ({x}, {y}) is not in the rectangle asked for ({string.Join("; ", Parts)}, wrapping by {_size})");

    /// <summary>The greyscale value at logical (x, y), wrapping. A colour input has none, and throws.</summary>
    public float Grey(int x, int y)
    {
        if (Kind != PortKind.Grey) throw new InvalidOperationException("a colour input has no single grey value");
        var i = Index(x, y, out var tile);
        return tile.Data[i];
    }

    /// <summary>The colour at logical (x, y), wrapping; a greyscale input is spread to grey.</summary>
    public (float R, float G, float B) Colour(int x, int y)
    {
        var i = Index(x, y, out var tile);
        var d = tile.Data;
        return Kind == PortKind.Grey ? (d[i], d[i], d[i]) : (d[i], d[i + 1], d[i + 2]);
    }

    /// <summary>
    /// Copies texels <paramref name="x"/> to <paramref name="x"/> + <paramref name="count"/> - 1 of logical row
    /// <paramref name="y"/>, <see cref="PortKinds.Channels"/> floats each, into <paramref name="to"/>, a run of
    /// <see cref="Row"/> for each tile they cross (T-155: the separable passes read long rows this way).
    /// </summary>
    public void CopyRow(int y, int x, int count, Span<float> to)
    {
        var side = 1 << _shift;
        var at = 0;
        while (count > 0)
        {
            var n = Math.Min(count, side - (Tile.Wrap(x, _size) & (side - 1)));
            Row(y, x, n).CopyTo(to[at..]);
            at += n * _channels; x += n; count -= n;
        }
    }

    /// <summary>
    /// Texels <paramref name="x"/> to <paramref name="x"/> + <paramref name="count"/> - 1 of logical row
    /// <paramref name="y"/>, <see cref="PortKinds.Channels"/> floats each. They must lie in one tile (a pointwise node
    /// reading its own tile's row always does); otherwise it throws.
    /// </summary>
    public ReadOnlySpan<float> Row(int y, int x, int count)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        if (!TryOffset(x, _x, _width, _coverX, out var lx) || !TryOffset(y, _y, _height, _coverY, out var ly))
        {
            foreach (var part in _rest)
                if (part.TryOffset(x, part._x, part._width, part._coverX, out _) && part.TryOffset(y, part._y, part._height, part._coverY, out _))
                    return part.Row(y, x, count);
            throw Outside(x, y);
        }
        if (lx + _coverX - _x + count > _width || (lx >> _shift) != ((lx + count - 1) >> _shift))
            throw new ArgumentOutOfRangeException(nameof(count), $"texels {x} to {x + count - 1} of row {y} are not in one tile of the rectangle asked for ({Asked})");
        var tile = _tiles[(ly >> _shift) * _columns + (lx >> _shift)];
        return tile.Data.AsSpan((((ly & _mask) << _shift) + (lx & _mask)) * _channels, count * _channels);
    }
}
