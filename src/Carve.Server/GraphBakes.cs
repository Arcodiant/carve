using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using Carve.Core;
using Carve.Core.Baking;
using Carve.Graphs;

namespace Carve.Server;

/// <summary>
/// The bakes of graph slots (M-29, T-163), cached in memory by content (<see cref="GraphSlotBake.Key"/>), never in the Carve
/// file: nothing has to be told that something changed. <see cref="Resolve"/> reads and hashes every graph file again and
/// gives the model to draw, each graph slot a <see cref="MaterialTexture"/> of its bake's PNG, so <see cref="PbrMaterials"/>
/// uploads it as a PNG material's. A material whose graphs read the mesh becomes one copy per mesh, <c>&lt;material&gt;@&lt;mesh&gt;</c>
/// (a kept level's <c>&lt;material&gt;@&lt;part&gt;@&lt;level&gt;</c>), and that mesh's faces are renamed to it. Capped at
/// <see cref="CapBytes"/> of PNG and decoded bytes, the least recently used dropped first. Safe to share between threads.
/// </summary>
internal sealed class GraphBakes(Func<double>? clockMs = null)
{
    public const long CapBytes = 512L << 20;
    public const double WarnMs = 5000;

    private readonly Func<double> _clock = clockMs ?? (() => Stopwatch.GetTimestamp() * 1000.0 / Stopwatch.Frequency);
    private readonly Dictionary<string, LinkedListNode<(string Key, byte[] Png, long Bytes)>> _entries = [];
    private readonly LinkedList<(string Key, byte[] Png, long Bytes)> _lru = new();
    private readonly ConditionalWeakTable<Model, Drawn> _drawn = [];
    private readonly Lock _lock = new();
    private long _bytes;

    /// <summary>
    /// One slot baked in a call: <paramref name="Mesh"/> null for a slot that does not read the mesh; the mesh map that cost most,
    /// if any; <paramref name="On"/> the mesh of the material's copy its time counts toward (null for a material that does not read the mesh).
    /// </summary>
    internal sealed record Baked(string Material, string? Mesh, string Slot, int Size, double Ms, string? Node, double NodeMs, string? On = null);

    /// <summary>The model to draw, the key of every bake it uses, what this call baked, and a warning per material over <see cref="WarnMs"/>.</summary>
    internal sealed record Drawn(Model Model, string Key, IReadOnlyList<Baked> Baked, IReadOnlyList<string> Warnings);

    /// <summary>The cache's bytes (PNG and decoded) and entries now.</summary>
    public long Bytes { get { lock (_lock) return _bytes; } }

    public int Count { get { lock (_lock) return _entries.Count; } }

    /// <summary>The model last resolved for <paramref name="model"/>, or null: never reads or bakes (the frame thread's check).</summary>
    public Model? Peek(Model model) => _drawn.TryGetValue(model, out var d) ? d.Model : null;

    /// <summary>
    /// The model to draw for <paramref name="model"/> (the frame thread's; never bakes): itself without graph slots, else the
    /// one <see cref="Resolve"/> gave it, else, not yet resolved (an edit shown before any render baked it), its graph slots
    /// left empty. No render draws that: render resolves before it queues, and the pump puts the mesh in again once
    /// <see cref="Peek"/> changes.
    /// </summary>
    public Model DrawnOf(Model model)
    {
        if (!model.Materials.Any(m => m.HasGraph)) return model;
        if (Peek(model) is { } drawn) return drawn;
        static MaterialSlot? Png(MaterialSlot? slot) => slot is MaterialGraph ? null : slot;
        foreach (var m in model.Materials.Where(m => m.HasGraph))
            model = model.WithMaterial(m with
            {
                BaseColourTexture = Png(m.BaseColourTexture),
                MetallicRoughnessTexture = Png(m.MetallicRoughnessTexture),
                NormalTexture = Png(m.NormalTexture),
                EmissiveTexture = Png(m.EmissiveTexture),
                OcclusionTexture = Png(m.OcclusionTexture),
            });
        return model;
    }

    /// <summary>Reads every graph file again and bakes what the cache lacks. Throws <see cref="GraphSlotProblemException"/> or <see cref="OperationCanceledException"/>.</summary>
    public Drawn Resolve(Model model, CancellationToken cancel)
    {
        if (!model.Materials.Any(m => m.HasGraph)) return new(model, "", [], []);
        var files = new Dictionary<string, GraphSlotFiles>(StringComparer.Ordinal);
        var baked = new List<Baked>();
        var keys = new List<string>();
        var next = model;
        var renamed = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal); // mesh -> material -> copy
        var levels = new Dictionary<(string Part, int Level), Dictionary<string, string>>();
        // One mesh source per drawn mesh in this call, so its maps (each once per size) serve every slot that reads them.
        var sources = new Dictionary<string, IMeshMaps>(StringComparer.Ordinal);

        GraphSlotFiles FilesOf(Material m, string field, MaterialGraph g)
        {
            if (files.TryGetValue(g.Path, out var f)) return f;
            try
            {
                return files[g.Path] = GraphSlotBake.Read(g.Path);
            }
            catch (Exception e) when (GraphSlotProblem.IsCause(e))
            {
                throw GraphSlotProblem.Failed(m.Name, field, e);
            }
        }

        bool ReadsMesh(Material m) => m.Textures.Any(t => t.Texture is MaterialGraph g && FilesOf(m, t.Field, g).ReadsMesh);

        MaterialSlot? Slot(Material m, string field, MaterialSlot? slot, Model source, string? mesh, string? label)
        {
            if (slot is not MaterialGraph g) return slot;
            var f = FilesOf(m, field, g);
            var on = f.ReadsMesh ? mesh : null;
            var key = GraphSlotBake.Key(f, g.Output, g.Size, source, on);
            keys.Add(key);
            if (Get(key) is not { } png)
            {
                cancel.ThrowIfCancellationRequested();
                var timed = new TimedMaps(_clock);
                var start = _clock();
                try
                {
                    png = GraphSlotBake.Png(g, field, source, on, cancel, s => timed.Over(sources.TryGetValue(label!, out var shared) ? shared : sources[label!] = s));
                }
                catch (Exception e) when (GraphSlotProblem.IsCause(e))
                {
                    throw GraphSlotProblem.Failed(m.Name, field, e);
                }
                var (node, nodeMs) = timed.Costliest(f);
                baked.Add(new(m.Name, f.ReadsMesh ? label : null, field, g.Size, _clock() - start, node, nodeMs, label));
                Put(key, png, g.Size);
            }
            return new MaterialTexture(g.Path, [.. png], g.Size, g.Size);
        }

        Material Resolved(Material m, Model source, string? mesh, string? label, string name) => m with
        {
            Name = name,
            BaseColourTexture = Slot(m, GraphSlotBake.BaseColour, m.BaseColourTexture, source, mesh, label),
            MetallicRoughnessTexture = Slot(m, GraphSlotBake.MetallicRoughness, m.MetallicRoughnessTexture, source, mesh, label),
            NormalTexture = Slot(m, GraphSlotBake.Normal, m.NormalTexture, source, mesh, label),
            EmissiveTexture = Slot(m, GraphSlotBake.Emissive, m.EmissiveTexture, source, mesh, label),
            OcclusionTexture = Slot(m, GraphSlotBake.Occlusion, m.OcclusionTexture, source, mesh, label),
        };

        foreach (var m in model.Materials.Where(m => m.HasGraph))
        {
            if (!ReadsMesh(m))
            {
                next = next.WithMaterial(Resolved(m, model, null, null, m.Name));
                continue;
            }
            foreach (var (name, mesh) in model.Meshes.Where(n => Model.MaterialsUsedBy(n.Mesh).Contains(m.Name)))
            {
                var copy = $"{m.Name}@{name}";
                next = next.WithMaterial(Resolved(m, model, name, name, copy));
                (renamed.TryGetValue(name, out var r) ? r : renamed[name] = [])[m.Name] = copy;
            }
            // A kept level is its own mesh with its own bake, cast against the model's other live meshes.
            foreach (var part in model.Parts)
                foreach (var level in part.Levels.Where(l => Model.MaterialsUsedBy(l.Mesh).Contains(m.Name)))
                {
                    var copy = $"{m.Name}@{part.Name}@{level.Number}";
                    next = next.WithMaterial(Resolved(m, model.With(part.Name, level.Mesh), part.Name, $"{part.Name} level {level.Number}", copy));
                    (levels.TryGetValue((part.Name, level.Number), out var r) ? r : levels[(part.Name, level.Number)] = [])[m.Name] = copy;
                }
        }
        foreach (var (name, copies) in renamed) next = next.With(name, Renamed(next.Find(name)!, copies));
        foreach (var part in model.Parts.Where(p => p.Levels.Any(l => levels.ContainsKey((p.Name, l.Number)))))
            next = next.WithPart(next.FindPart(part.Name)! with
            {
                Levels = [.. part.Levels.Select(l => levels.TryGetValue((part.Name, l.Number), out var c) ? l with { Mesh = Renamed(l.Mesh, c) } : l)],
            });

        var drawn = new Drawn(next, string.Join(",", keys), baked, Warnings(baked));
        lock (_lock)
        {
            // The same bakes give the same model, so the frame thread does not put the mesh in again for nothing.
            if (_drawn.TryGetValue(model, out var last) && last.Key == drawn.Key) drawn = drawn with { Model = last.Model };
            _drawn.AddOrUpdate(model, drawn);
        }
        return drawn;
    }

    /// <summary>
    /// One graph output as a picture for render's <c>uvs</c> look (T-165): evaluated on <paramref name="mesh"/> as a base colour slot
    /// takes it (a grey output as grey, <c>roughness</c> as its own grey, <c>normal</c> as its colours), through the same cache.
    /// Throws <see cref="GraphSlotProblemException"/> or <see cref="OperationCanceledException"/>.
    /// </summary>
    public (MaterialTexture Texture, Baked? Baked) Image(Model model, string mesh, MaterialGraph g, CancellationToken cancel)
    {
        var name = Path.GetFileName(g.Path);
        try
        {
            var f = GraphSlotBake.Read(g.Path);
            var on = f.ReadsMesh ? mesh : null;
            // The key has no slot, and this look packs differently from a metallic-roughness slot, so it is told apart.
            var key = GraphSlotBake.Key(f, g.Output, g.Size, model, on) + ":image";
            if (Get(key) is { } hit) return (new MaterialTexture(g.Path, [.. hit], g.Size, g.Size), null);
            var timed = new TimedMaps(_clock);
            var start = _clock();
            var png = GraphSlotBake.Png(g, GraphSlotBake.BaseColour, model, on, cancel, s => timed.Over(s));
            var (node, nodeMs) = timed.Costliest(f);
            Put(key, png, g.Size);
            return (new MaterialTexture(g.Path, [.. png], g.Size, g.Size), new Baked(name, f.ReadsMesh ? mesh : null, "image", g.Size, _clock() - start, node, nodeMs));
        }
        catch (Exception e) when (GraphSlotProblem.IsCause(e))
        {
            throw new GraphSlotProblemException(GraphSlotProblem.Of("image", e), e);
        }
    }

    private static PolygonMesh Renamed(PolygonMesh mesh, IReadOnlyDictionary<string, string> copies)
    {
        var style = mesh.Style!;
        foreach (var group in style.Materials.Where(p => copies.ContainsKey(p.Value)).GroupBy(p => p.Value).ToList())
            style = style.WithMaterial(group.Select(p => p.Key), copies[group.Key]);
        return mesh.WithStyle(style);
    }

    /// <summary>The lines render's text gains: <c>baked: [{material, mesh, slot, size, ms}]</c> and a warning per material over <see cref="WarnMs"/>; empty when nothing was baked.</summary>
    public static string Text(IReadOnlyList<Baked> baked) => baked.Count == 0 ? ""
        : "\nbaked: " + System.Text.Json.JsonSerializer.Serialize(baked.Select(b => new { material = b.Material, mesh = b.Mesh, slot = b.Slot, size = b.Size, ms = (long)Math.Round(b.Ms) }))
          + string.Concat(Warnings(baked).Select(w => "\nwarning: " + w));

    internal static List<string> Warnings(IReadOnlyList<Baked> baked) =>
    [
        .. baked.GroupBy(b => (b.Material, b.On)).Where(g => g.Sum(b => b.Ms) > WarnMs).Select(g =>
        {
            // The node that cost most, in the slot that made its map: a mesh's maps are shared within a bake, so a later slot that reads one is charged nothing for it.
            var worst = g.Any(b => b.NodeMs > 0) ? g.MaxBy(b => b.NodeMs)! : g.MaxBy(b => b.Ms)!;
            var on = g.Key.On is { } mesh ? $" on '{mesh}'" : "";
            var node = worst.Node is { } n ? $": {n} {Seconds(worst.NodeMs)} s" : "";
            return $"material '{g.Key.Material}'{on} took {Seconds(g.Sum(b => b.Ms))} s to bake ({worst.Slot} {worst.Size}{node}); lower its size or the node's rays";
        }),
    ];

    private static string Seconds(double ms) => (ms / 1000).ToString("0.0", CultureInfo.InvariantCulture);

    private byte[]? Get(string key)
    {
        lock (_lock)
        {
            if (!_entries.TryGetValue(key, out var node)) return null;
            _lru.Remove(node);
            _lru.AddFirst(node);
            return node.Value.Png;
        }
    }

    private void Put(string key, byte[] png, int size)
    {
        lock (_lock)
        {
            if (_entries.ContainsKey(key)) return;
            var entry = _lru.AddFirst((key, png, png.Length + (long)size * size * 4));
            _entries[key] = entry;
            _bytes += entry.Value.Bytes;
            while (_bytes > CapBytes && _lru.Last is { } oldest && oldest != entry)
            {
                _lru.RemoveLast();
                _entries.Remove(oldest.Value.Key);
                _bytes -= oldest.Value.Bytes;
            }
        }
    }

    /// <summary>Times each mesh map a bake reads, to name the costliest in the warning.</summary>
    private sealed class TimedMaps(Func<double> clock)
    {
        private readonly List<(string Type, double Ms)> _calls = [];

        public IMeshMaps Over(IMeshMaps inner) => new Timed(inner, this);

        private T Time<T>(string type, Func<T> make)
        {
            var start = clock();
            var made = make();
            lock (_calls) _calls.Add((type, clock() - start));
            return made;
        }

        /// <summary>The costliest map's node, as <c>type 'id'</c> (the first node of that type in the graph's files), and its time.</summary>
        public (string? Node, double Ms) Costliest(GraphSlotFiles files)
        {
            if (_calls.Count == 0) return (null, 0);
            var (type, ms) = _calls.MaxBy(c => c.Ms);
            // shortcut: names the first node of the type across the files; two of one type are told apart only by type.
            var id = files.Files.Prepend(files.Path)
                .Select(f => Patina.Graphs.TexGraphFile.ReadFile(f, MeshNodes.Register(Patina.Graphs.Nodes.NodeRegistry.BuiltIn, null)).Graph)
                .SelectMany(g => g?.Nodes ?? ImmutableSortedDictionary<string, Patina.Graphs.GraphNode>.Empty)
                .FirstOrDefault(n => n.Value.Type == type).Key;
            return (id is null ? type : $"{type} '{id}'", ms);
        }

        private sealed class Timed(IMeshMaps inner, TimedMaps timer) : IMeshMaps
        {
            public BakeImage Curvature(int size, double width) => timer.Time(MeshCurvatureNode.TypeName, () => inner.Curvature(size, width));
            public BakeImage Occlusion(int size, int rays, float reach) => timer.Time(MeshOcclusionNode.TypeName, () => inner.Occlusion(size, rays, reach));
            public BakeImage Mask(int size, MaskSource source, string name) => timer.Time(MeshMaskNode.TypeName, () => inner.Mask(size, source, name));
            public BakeImage Position(int size) => timer.Time(MeshPositionNode.TypeName, () => inner.Position(size));
        }
    }
}
