using System.Numerics;

namespace Carve.Core;

/// <summary>
/// One level of Catmull-Clark subdivision (D-6, Subdivide is a destructive edit), by D-1's id rules. Every new point is a
/// stencil, a weighted sum of this level's vertices, and the same stencil gives its position, its bone weights and its morph targets' deltas.
/// </summary>
/// <remarks>
/// <para><b>Ids.</b> Vertex <c>v</c> keeps its id and moves to its vertex point. Appended after it: one face point per face,
/// in face order, then one edge point per edge, in the edge table's order (by lower vertex id, then higher). Face
/// <c>f</c> with corners <c>c0..c(n-1)</c> becomes <c>n</c> quads, child <c>k</c> being
/// <c>[ck, edge point of ck-ck+1, face point, edge point of ck-1-ck]</c>, wound as the face was; child 0 keeps the id
/// <c>f</c> and children 1..n-1 are appended, face by face.</para>
/// <para><b>Sharp edges.</b> An edge is sharp when it is marked hard or is not a manifold edge of two faces wound
/// opposite ways: exactly the edges <see cref="Shading"/> does not smooth across, so an open edge is one. A sharp edge's
/// point is its midpoint, and its two halves are sharp in turn (a hard mark is carried to both halves). A vertex on no
/// sharp edge or one (a dart) takes the smooth rule, on exactly two the crease rule <c>(a + b + 6P) / 8</c> along them,
/// which on an open edge is the boundary rule, and on three or more it is a corner and stays. A vertex no face uses stays.</para>
/// </remarks>
internal static class Subdivision
{
    /// <summary>
    /// A mesh's geometry between levels (a tombstoned face's loop empty), and <see cref="Mesh"/>, the same geometry
    /// unchecked with every layer carried onto it (T-71).
    /// </summary>
    internal sealed record Level(Vector3[] Vertices, int[][] Faces, PolygonMesh Mesh);

    /// <summary>The level a subdivide of <paramref name="mesh"/> starts from (a mesh with no holed face).</summary>
    internal static Level Start(PolygonMesh mesh) =>
        new(mesh.Vertices.ToArray(), mesh.Faces.Select(f => f.Loop.ToArray()).ToArray(), mesh);

    internal static Level Once(Level input)
    {
        var verts = input.Vertices;
        var faces = input.Faces;
        var topology = PolygonMesh.Assemble(verts, faces.Select(f => (Face)f).ToArray()).Topology;
        var edges = topology.Edges;
        int v0 = verts.Length, f0 = faces.Length, e0 = edges.Count;
        var shade = input.Mesh.Shade;

        var sharp = new bool[e0];
        var edgeIndex = new Dictionary<(int, int), int>(e0);
        var vertexEdges = new List<int>?[v0];
        for (var e = 0; e < e0; e++)
        {
            var edge = edges[e];
            edgeIndex[(edge.Lo, edge.Hi)] = e;
            sharp[e] = !edge.IsManifold || (shade?.IsHard(edge.Lo, edge.Hi) ?? false);
            (vertexEdges[edge.Lo] ??= []).Add(e);
            (vertexEdges[edge.Hi] ??= []).Add(e);
        }
        int EdgeOf(int a, int b) => edgeIndex[a < b ? (a, b) : (b, a)];

        // Stencils over this level's vertex ids: face points, then edge points, then vertex points.
        var facePoint = new Stencil[f0];
        for (var f = 0; f < f0; f++)
        {
            var s = new Stencil();
            foreach (var c in faces[f]) s.Add(c, 1.0 / faces[f].Length);
            facePoint[f] = s;
        }

        var edgePoint = new Stencil[e0];
        for (var e = 0; e < e0; e++)
        {
            var edge = edges[e];
            var s = new Stencil();
            if (sharp[e])
            {
                s.Add(edge.Lo, 0.5);
                s.Add(edge.Hi, 0.5);
            }
            else
            {
                s.Add(edge.Lo, 0.25);
                s.Add(edge.Hi, 0.25);
                s.Add(facePoint[edge.Faces[0].Face], 0.25);
                s.Add(facePoint[edge.Faces[1].Face], 0.25);
            }
            edgePoint[e] = s;
        }

        var vertexPoint = new Stencil[v0];
        for (var v = 0; v < v0; v++)
        {
            var s = new Stencil();
            var around = vertexEdges[v] ?? [];
            var creases = around.Where(e => sharp[e]).ToArray();
            if (around.Count == 0 || creases.Length > 2)
            {
                s.Add(v, 1);
            }
            else if (creases.Length == 2)
            {
                s.Add(v, 0.75);
                s.Add(edges[creases[0]].Other(v), 0.125);
                s.Add(edges[creases[1]].Other(v), 0.125);
            }
            else
            {
                // (F + 2R + (n - 3)P) / n, F the mean of the face points round v, R the mean of its edges' midpoints.
                double n = around.Count;
                var vertexFaces = topology.VertexFaces(v);
                foreach (var f in vertexFaces) s.Add(facePoint[f], 1.0 / (vertexFaces.Count * n));
                foreach (var e in around)
                {
                    s.Add(v, 1.0 / (around.Count * n));
                    s.Add(edges[e].Other(v), 1.0 / (around.Count * n));
                }
                s.Add(v, (n - 3) / n);
            }
            vertexPoint[v] = s;
        }

        var stencils = vertexPoint.Concat(facePoint).Concat(edgePoint).ToArray();
        var positions = stencils.Select(s => s.Apply(verts)).ToArray();

        var outFaces = new int[f0][];
        var appended = new List<int[]>();
        var children = new List<(int Face, IReadOnlyList<int> Added)>();
        for (var f = 0; f < f0; f++)
        {
            var loop = faces[f];
            var n = loop.Length;
            if (n == 0) { outFaces[f] = []; continue; } // a tombstone keeps its slot; its face point is used by nothing
            var added = new List<int>();
            for (var k = 0; k < n; k++)
            {
                var next = v0 + f0 + EdgeOf(loop[k], loop[(k + 1) % n]);
                var previous = v0 + f0 + EdgeOf(loop[(k + n - 1) % n], loop[k]);
                int[] quad = [loop[k], next, v0 + f, previous];
                if (k == 0) outFaces[f] = quad;
                else
                {
                    added.Add(f0 + appended.Count);
                    appended.Add(quad);
                }
            }
            children.Add((f, added));
        }

        // Where everything came from (T-71): each point is its stencil, each edge is split at its edge point, and child k of
        // a face takes the face's colour and groups and its corners [k, (k + k+1) / 2, the mean, (k-1 + k) / 2] over the
        // face's own corners, so UVs are linear in their face and never depend on the positions' stencils. A midpoint sums
        // from its first corner and the mean from 0, as before, so a negative zero lands where it did (RV-62).
        var provenance = new EditProvenance();
        for (var i = 0; i < stencils.Length; i++) provenance.Vertex(i, VertexOrigin.Blend(stencils[i]));
        for (var e = 0; e < e0; e++) provenance.Split(edges[e].Lo, edges[e].Hi, [v0 + f0 + e]);
        foreach (var (f, added) in children)
        {
            var n = faces[f].Length;
            var mean = new Stencil(n);
            for (var c = 0; c < n; c++) mean.Add(c, 1);
            for (var k = 0; k < n; k++)
            {
                var next = new Stencil(2, sumsFromFirst: true);
                next.Add(k, 1);
                next.Add((k + 1) % n, 1);
                var previous = new Stencil(2, sumsFromFirst: true);
                previous.Add((k + n - 1) % n, 1);
                previous.Add(k, 1);
                provenance.Face(k == 0 ? f : added[k - 1],
                    new FaceOrigin(f, CornerOriginKind.Stencils) { CornerStencils = [Stencil.Of(k), next, mean, previous] });
            }
        }

        int[][] all = [.. outFaces, .. appended];
        var edited = PolygonMesh.Assemble(positions, all.Select(f => f.Length == 0 ? Face.Deleted : (Face)f).ToArray());
        return new Level(positions, all, LayerRegistry.Standard.Carry(input.Mesh, edited, provenance));
    }
}
