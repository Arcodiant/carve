using Carve.Core;

namespace Carve.Server;

/// <summary>
/// Which look of which mesh is drawn, and the meshes drawing it: the fill, the light grey lines and the orange lines.
/// Generic over the engine's mesh handle, so its sequencing is tested with no device.
/// </summary>
internal sealed class LookHolder<TMesh>(LookHolder<TMesh>.IParts parts)
    where TMesh : class
{
    /// <summary>What the holder asks of the scene, on the frame thread. Each may throw.</summary>
    internal interface IParts
    {
        /// <summary>The look's meshes, made one at a time in this order: the fill, the edge lines, the orange lines, each null where the look has none (never the fill).</summary>
        IReadOnlyList<Func<TMesh?>> Make(PolygonMesh mesh, DrawnSpace space, Look look, Highlight highlight);

        /// <summary>Puts the new meshes on the scene's entities, in place of the old ones.</summary>
        void Install(TMesh fill, TMesh? edges, TMesh? orange);

        void Destroy(TMesh mesh);
    }

    private TMesh? _fill;
    private TMesh? _edges;
    private TMesh? _orange;

    public Look Look { get; private set; }

    public Highlight Highlight { get; private set; } = Highlight.None;

    public PolygonMesh? Shown { get; private set; }

    /// <summary>Draws a new mesh in the look already asked for.</summary>
    public void Show(PolygonMesh mesh, DrawnSpace space)
    {
        Put(mesh, space, Look, Highlight);
        Shown = mesh;
    }

    /// <summary>Takes the look of a request; a camera change alone rebuilds nothing.</summary>
    public void Apply(Look look, Highlight highlight, DrawnSpace space)
    {
        if (Shown is { } shown && (look != Look || highlight != Highlight))
        {
            Put(shown, space, look, highlight);
        }

        Look = look;
        Highlight = highlight;
    }

    /// <summary>
    /// The new meshes are made first, so a failure leaves the scene as it was and destroys what it had made;
    /// each old handle goes once the entities hold the new ones.
    /// </summary>
    private void Put(PolygonMesh mesh, DrawnSpace space, Look look, Highlight highlight)
    {
        var made = new List<TMesh?>(3);
        try
        {
            foreach (var make in parts.Make(mesh, space, look, highlight))
            {
                made.Add(make());
            }
        }
        catch
        {
            foreach (var m in made)
            {
                if (m is not null)
                {
                    parts.Destroy(m);
                }
            }

            throw;
        }

        var old = new[] { _fill, _edges, _orange };
        (_fill, _edges, _orange) = (made[0]!, made[1], made[2]);
        parts.Install(_fill, _edges, _orange);
        foreach (var m in old)
        {
            if (m is not null)
            {
                parts.Destroy(m);
            }
        }
    }
}
