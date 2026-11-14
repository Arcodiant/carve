using Carve.Core;

namespace Carve.Server;

/// <summary>
/// Which look of which mesh is drawn, and the meshes drawing it: the fill, the edge lines, the orange lines and the seam lines
/// (the <c>uvs</c> look's alone, T-58).
/// Generic over the engine's mesh handle, so its sequencing is tested with no device.
/// </summary>
internal sealed class LookHolder<TMesh>(LookHolder<TMesh>.IParts parts)
    where TMesh : class
{
    /// <summary>What the holder asks of the scene, on the frame thread. Each may throw.</summary>
    internal interface IParts
    {
        /// <summary>
        /// The look's meshes, made one at a time in this order: the fill, the edge lines, the orange lines, the seam lines, each
        /// null where the look has none (never the fill). <paramref name="faces"/> is the <c>uvs</c> look's face range, else null.
        /// </summary>
        IReadOnlyList<Func<TMesh?>> Make(PolygonMesh mesh, DrawnSpace space, Look look, Highlight highlight, ElementWindow? faces);

        /// <summary>Puts the new meshes on the scene's entities, in place of the old ones.</summary>
        void Install(TMesh fill, TMesh? edges, TMesh? orange, TMesh? seams);

        void Destroy(TMesh mesh);
    }

    private TMesh? _fill;
    private TMesh? _edges;
    private TMesh? _orange;
    private TMesh? _seams;

    public Look Look { get; private set; }

    public Highlight Highlight { get; private set; } = Highlight.None;

    /// <summary>The <c>uvs</c> look's face range; null for every other look.</summary>
    public ElementWindow? Faces { get; private set; }

    public PolygonMesh? Shown { get; private set; }

    /// <summary>Draws a new mesh in the look already asked for.</summary>
    public void Show(PolygonMesh mesh, DrawnSpace space)
    {
        Put(mesh, space, Look, Highlight, Faces);
        Shown = mesh;
    }

    /// <summary>Takes the look of a request; a camera change alone rebuilds nothing.</summary>
    public void Apply(Look look, Highlight highlight, DrawnSpace space, ElementWindow? faces = null)
    {
        if (Shown is { } shown && (look != Look || highlight != Highlight || faces != Faces))
        {
            Put(shown, space, look, highlight, faces);
        }

        Look = look;
        Highlight = highlight;
        Faces = faces;
    }

    /// <summary>
    /// The new meshes are made first, so a failure leaves the scene as it was and destroys what it had made;
    /// each old handle goes once the entities hold the new ones.
    /// </summary>
    private void Put(PolygonMesh mesh, DrawnSpace space, Look look, Highlight highlight, ElementWindow? faces)
    {
        var made = new List<TMesh?>(4);
        try
        {
            foreach (var make in parts.Make(mesh, space, look, highlight, faces))
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

        var old = new[] { _fill, _edges, _orange, _seams };
        (_fill, _edges, _orange, _seams) = (made[0]!, made[1], made[2], made.Count > 3 ? made[3] : null);
        parts.Install(_fill, _edges, _orange, _seams);
        foreach (var m in old)
        {
            if (m is not null)
            {
                parts.Destroy(m);
            }
        }
    }
}
