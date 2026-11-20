using Patina.Graphs.Imaging;

namespace Patina.Graphs.Evaluation;

/// <summary>
/// The tiles one <see cref="GraphEvaluator.Evaluate"/> holds, keyed by node and tile position, every output port of a
/// node at one position held together. Tiles the current step needs are pinned and never dropped; past
/// <see cref="Cap"/> bytes the unpinned ones are dropped least recently used first, so what is held stays at most the
/// cap or what is pinned, whichever is more. No clock: "recent" is the order tiles were last unpinned.
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

    public long Cap { get; } = cap;
    public long Bytes { get; private set; }
    public int Tiles { get; private set; }
    public int MostTiles { get; private set; }
    public long PeakBytes { get; private set; }

    public long Key(int node, int tx, int ty) => ((long)node * tilesAcross + ty) * tilesAcross + tx;

    public Entry? Find(long key) => _entries.GetValueOrDefault(key);

    public void Pin(Entry entry)
    {
        if (entry.Pins++ == 0 && entry.Unpinned is { } node)
        {
            _unpinned.Remove(node);
            entry.Unpinned = null;
        }
    }

    public void Unpin(Entry entry)
    {
        if (--entry.Pins == 0) entry.Unpinned = _unpinned.AddLast(entry);
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
        Trim();
        return entry;
    }

    /// <summary>Drops unpinned tiles, least recently used first, until no more than the cap is held or none is left.</summary>
    public void Trim()
    {
        while (Bytes > Cap && _unpinned.First is { } first)
        {
            _unpinned.RemoveFirst();
            var entry = first.Value;
            entry.Unpinned = null;
            _entries.Remove(entry.Key);
            Bytes -= entry.Bytes;
            Tiles -= entry.Tiles.Length;
        }
    }
}
