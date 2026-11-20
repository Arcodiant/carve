using Patina.Graphs.Imaging;

namespace Patina.Graphs.Evaluation;

/// <summary>
/// The tiles one <see cref="GraphEvaluator.Evaluate"/> holds, keyed by node and tile position, every output port of a
/// node at one position held together. Tiles the current step needs are pinned and never dropped; past
/// <see cref="Bound"/> bytes (the cap, or the most ever pinned at once if that is more, T-155) the unpinned ones are
/// dropped least recently used first, so what is held stays at most the cap or the most one step pinned, whichever is
/// more. No clock: "recent" is the order tiles were last unpinned.
/// <para><b>Cold tiles</b> (T-155). A tile last unpinned by a separable node's row pass is cold: the row pass reads its
/// inputs along one tile row, so once that row is made they are not read again, while its own row tiles are read by
/// every column tile below them. Cold tiles are dropped before any other, least recently used first; pinned again by
/// any reader, a tile is no longer cold. Without it a row's sources, unpinned between its row tiles, push the row
/// tiles out under a tight cap.</para>
/// </summary>
internal sealed class TileCache(int tilesAcross, long cap)
{
    internal sealed class Entry(long key, Tile[] tiles)
    {
        public long Key { get; } = key;
        public Tile[] Tiles { get; } = tiles;
        public long Bytes { get; } = tiles.Sum(t => t.Bytes);
        public int Pins;
        public LinkedListNode<Entry>? Unpinned;
    }

    private readonly Dictionary<long, Entry> _entries = [];
    private readonly LinkedList<Entry> _unpinned = new();
    private readonly LinkedList<Entry> _cold = new();

    public long Cap { get; } = cap;
    public long Bytes { get; private set; }
    public int Tiles { get; private set; }
    public int MostTiles { get; private set; }
    public long PeakBytes { get; private set; }

    /// <summary>The bytes of the tiles pinned now, and the most there have been.</summary>
    public long PinnedBytes { get; private set; }
    public long PeakPinnedBytes { get; private set; }

    /// <summary>
    /// What the cache trims to: the cap, or the most ever pinned at once if that is more. A step that pinned more than
    /// the cap (a strong minify reads its whole input for every tile) has shown it needs that much; keeping it lets the
    /// next step find those tiles rather than make them all again, and holds no more than was held already.
    /// </summary>
    public long Bound => Math.Max(Cap, PeakPinnedBytes);

    public long Key(int node, int tx, int ty) => ((long)node * tilesAcross + ty) * tilesAcross + tx;

    public Entry? Find(long key) => _entries.GetValueOrDefault(key);

    public void Pin(Entry entry)
    {
        if (entry.Pins++ != 0) return;
        if (entry.Unpinned is { } node)
        {
            node.List!.Remove(node);
            entry.Unpinned = null;
        }
        AddPinnedBytes(entry.Bytes);
    }

    /// <summary>Unpins the entry; unpinned by a row pass (<paramref name="cold"/>), it is dropped before any warm tile.</summary>
    public void Unpin(Entry entry, bool cold = false)
    {
        if (--entry.Pins != 0) return;
        entry.Unpinned = (cold ? _cold : _unpinned).AddLast(entry);
        PinnedBytes -= entry.Bytes;
    }

    private void AddPinnedBytes(long bytes)
    {
        PinnedBytes += bytes;
        PeakPinnedBytes = Math.Max(PeakPinnedBytes, PinnedBytes);
    }

    /// <summary>Adds a node's tiles at one position, pinned once, then drops unpinned tiles past the cap.</summary>
    public Entry AddPinned(long key, Tile[] tiles)
    {
        var entry = new Entry(key, tiles) { Pins = 1 };
        _entries.Add(key, entry);
        Bytes += entry.Bytes;
        Tiles += tiles.Length;
        MostTiles = Math.Max(MostTiles, Tiles);
        PeakBytes = Math.Max(PeakBytes, Bytes);
        AddPinnedBytes(entry.Bytes);
        Trim();
        return entry;
    }

    /// <summary>
    /// Drops unpinned tiles, least recently used first, until <paramref name="incoming"/> more bytes fit under the cap
    /// or none is left: called before a node's tiles are allocated, so the cache never holds the bound and a tile more.
    /// </summary>
    public void MakeRoom(long incoming) => Trim(Bound - incoming);

    /// <summary>Drops unpinned tiles, cold ones first, each least recently used first, until no more than the bound is held or none is left.</summary>
    public void Trim() => Trim(Bound);

    private void Trim(long limit)
    {
        while (Bytes > limit && (_cold.First ?? _unpinned.First) is { } first)
        {
            first.List!.Remove(first);
            var entry = first.Value;
            entry.Unpinned = null;
            _entries.Remove(entry.Key);
            Bytes -= entry.Bytes;
            Tiles -= entry.Tiles.Length;
        }
    }
}
