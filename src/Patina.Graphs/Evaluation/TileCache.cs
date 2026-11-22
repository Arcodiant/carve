using Patina.Graphs.Imaging;

namespace Patina.Graphs.Evaluation;

/// <summary>
/// The tiles one <see cref="GraphEvaluator.Evaluate"/> holds, keyed by node and tile position, the ports of a node at
/// one position that the evaluation reads held together (RV-76: a node's other ports are filled into arrays released
/// once it is made, so a strong minify of one port of <c>tiles</c> pins that port alone). Tiles the current step needs
/// are pinned and never dropped; past <see cref="Bound"/> bytes (the cap, or the most ever pinned at once if that is
/// more, T-155) the unpinned ones are dropped least recently used first, so the tiles held stay at most the cap or the
/// most one step pinned, whichever is more. No clock: "recent" is the order tiles were last unpinned.
/// <para><b>Past the tiles</b> (RV-76) the cache keeps two more kinds of array reachable, outside the bound:
/// dropped arrays waiting to be taken again, at most <see cref="FreeLimit"/> bytes, and the scratch of the node being
/// made (<see cref="TakeScratch"/>) with the last tile's, kept for the next. <see cref="PeakBytes"/> counts the tiles alone; <see cref="PeakHeldBytes"/>
/// counts all three. Putting the waiting arrays and scratch under the bound was tried (T-173) and took their room from
/// the tiles: a blur at 1024 in tiles of 64 under 4 MiB made 12,640 tiles against 1,840.</para>
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

        /// <summary>The tile of each port, null for a port the evaluation does not read.</summary>
        public Tile[] Tiles { get; } = tiles;
        public long Bytes { get; } = tiles.Sum(t => t?.Bytes ?? 0);
        public int Count { get; } = tiles.Count(t => t is not null);
        public int Pins;
        public LinkedListNode<Entry>? Unpinned;
    }

    private readonly Dictionary<long, Entry> _entries = [];
    private readonly LinkedList<Entry> _unpinned = new();
    private readonly LinkedList<Entry> _cold = new();
    private readonly Dictionary<long, Stack<float[]>> _free = [];
    private long _freeBytes, _scratchBytes, _keptScratchBytes;
    private List<float[]> _keptScratch = []; // the last tile's scratch, outside FreeLimit: the next tile asks for the same

    public long Cap { get; } = cap;

    /// <summary>The bytes of the cache's tiles, with those of a tile being made (<see cref="Allocate"/>).</summary>
    public long Bytes { get; private set; }
    public int Tiles { get; private set; }
    public int MostTiles { get; private set; }
    public long PeakBytes { get; private set; }

    /// <summary>The most bytes the cache kept reachable at once: its tiles, the dropped arrays waiting and a node's scratch.</summary>
    public long PeakHeldBytes { get; private set; }

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

    /// <summary>
    /// Arrays of these lengths, zeroed, for the ports of a tile being made, counted as held and pinned: first drops
    /// unpinned tiles, least recently used first, until they fit under the bound or none is left, so the cache never
    /// holds the bound and a tile more. Each array is handed back by <see cref="AddPinned"/> or <see cref="Release"/>.
    /// </summary>
    public float[][] Allocate(IReadOnlyList<long> lengths)
    {
        var bytes = lengths.Sum() * sizeof(float);
        Trim(Bound - bytes);
        var arrays = lengths.Select(Take).ToArray();
        Bytes += bytes;
        PeakBytes = Math.Max(PeakBytes, Bytes);
        AddPinnedBytes(bytes);
        NoteHeld();
        return arrays;
    }

    /// <summary>Hands back the arrays of ports from <see cref="Allocate"/> that the evaluation does not read.</summary>
    public void Release(IEnumerable<float[]> arrays)
    {
        foreach (var data in arrays)
        {
            var bytes = data.LongLength * sizeof(float);
            Bytes -= bytes;
            PinnedBytes -= bytes;
            Free(data);
        }
    }

    /// <summary>A zeroed array for a node's scratch while it fills a tile: a waiting array of that length, or a new one.</summary>
    public float[] TakeScratch(long length)
    {
        var kept = _keptScratch.FindIndex(a => a.LongLength == length);
        float[] data;
        if (kept < 0) data = Take(length);
        else
        {
            data = _keptScratch[kept];
            _keptScratch.RemoveAt(kept);
            _keptScratchBytes -= length * sizeof(float);
            Array.Clear(data);
        }
        _scratchBytes += length * sizeof(float);
        NoteHeld();
        return data;
    }

    /// <summary>
    /// Hands back scratch from <see cref="TakeScratch"/> once the tile is made: kept whole for the next tile's (blur's
    /// at 8192 is two arrays of about 4 MB, past <see cref="FreeLimit"/>), the scratch kept before it waiting as dropped
    /// arrays do.
    /// </summary>
    public void ReleaseScratch(List<float[]> arrays)
    {
        if (arrays.Count == 0) return;
        foreach (var data in _keptScratch) Free(data);
        _keptScratch = arrays;
        _keptScratchBytes = 0;
        foreach (var data in arrays)
        {
            _scratchBytes -= data.LongLength * sizeof(float);
            _keptScratchBytes += data.LongLength * sizeof(float);
        }
    }

    /// <summary>Adds a node's tiles at one position, pinned once: their arrays came from <see cref="Allocate"/>, so they are already counted.</summary>
    public Entry AddPinned(long key, Tile[] tiles)
    {
        var entry = new Entry(key, tiles) { Pins = 1 };
        _entries.Add(key, entry);
        Tiles += entry.Count;
        MostTiles = Math.Max(MostTiles, Tiles);
        return entry;
    }

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
            Tiles -= entry.Count;
            foreach (var tile in entry.Tiles)
                if (tile is not null) Free(tile.Data);
        }
    }

    // Dropped tiles' arrays, and scratch, kept for the next tiles made (T-155, RV-76). A tile is a large object,
    // collected only in the runtime's rare full collections; made and dropped by the thousand, they piled up as garbage
    // past the cap (the strong minify at 8192 peaked over 2 GB with 768 MiB held). At most FreeLimit bytes wait here.
    public long FreeLimit => Math.Max(4L << 20, Bound / 16);

    private void Free(float[] data)
    {
        var bytes = data.LongLength * sizeof(float);
        if (_freeBytes + bytes > FreeLimit) return;
        if (!_free.TryGetValue(data.LongLength, out var stack)) _free[data.LongLength] = stack = new Stack<float[]>();
        stack.Push(data);
        _freeBytes += bytes;
    }

    // An array of `length` zeros: a waiting one, cleared, or a new one.
    private float[] Take(long length)
    {
        if (!_free.TryGetValue(length, out var stack) || !stack.TryPop(out var data)) return new float[length];
        _freeBytes -= length * sizeof(float);
        Array.Clear(data);
        return data;
    }

    private void NoteHeld() => PeakHeldBytes = Math.Max(PeakHeldBytes, Bytes + _freeBytes + _scratchBytes + _keptScratchBytes);
}
