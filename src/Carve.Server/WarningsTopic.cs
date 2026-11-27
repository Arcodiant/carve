namespace Carve.Server;

/// <summary>
/// The text of <c>help {"topic":"warnings"}</c> (T-199): every warning, its fix and how warnings are grouped. check's
/// description held the catalogue's sentences until then; they are here in the same words. Each kind's heading is its
/// <c>MeshWarningKind</c> name, which a test holds.
/// </summary>
public static class WarningsTopic
{
    public const string Text =
        "Warnings. The mesh warnings are the ones define_mesh, open and the edits report; the UV, material, model and graph material warnings only check gives. A warning says what a game will make of the mesh and how to fix it; ok is false while there is any, and notes never make it false.\n"
        + "\n"
        + "Mesh warnings, by kind (the heading is the kind's name; each warning is by element id):\n"
        + "OpenEdge: \"edge belongs to only one face\" (a hole; collision built from the mesh breaks later). Fix: close the hole with define_mesh, extrude or bridge. Names them all: select edges where open.\n"
        + "NonManifoldEdge: \"edge is shared by N faces\" (non-manifold: collision breaks the same way). Fix: call define_mesh again without the extra face. Names them all: select edges where nonmanifold.\n"
        + "InconsistentWinding: \"two faces cross this edge in the same direction\" (a face points the wrong way and is culled). Fix: reverse that face's loop and call define_mesh again. An edge with this warning and a non-manifold one usually means a duplicate face, not a flipped one. No select query names these edges; the line gives three.\n"
        + "NonPlanarFace: \"face is not planar\" (a lifted corner, so the face folds along a diagonal the game chooses). Fix: move the vertex back onto the plane with move. Names them all: select faces where non_planar.\n"
        + "UnusedVertex: \"no face uses this vertex\" (a stray that a game loads for nothing). Fix: call define_mesh again without it. Names them all: select vertices where unattached.\n"
        + "UV warnings, only for a mesh that has UVs on some face (a mesh with none gets none), each by face id:\n"
        + "UvUnmapped: \"no UVs, so the face samples the texture's corner\". Fix: project_uvs, unwrap or set_uvs it. A face whose material has a texture gets \"material 'wood' has a texture but the face has no UVs, so it samples the texture's corner\" instead, even in a mesh with no UVs at all.\n"
        + "UvFlipped: \"UVs flipped, so the texture is mirrored on the face\". Fix: set_uvs, or project_uvs again.\n"
        + "UvZeroArea: \"UVs of zero area, so the face is one smeared texel\". Fix: project_uvs or unwrap it, or set_uvs corners that span an area.\n"
        + "UvOverlap: \"UVs overlap faces[m], so the two faces show the same texels\" (painting one paints the other; each pair is listed once, under the lower id; sharing an edge is not an overlap; faces whose two materials differ are not warned of). Fix: unwrap, which separates islands; mirrored halves sharing a texture area are deliberate, so read the pair first.\n"
        + "Material warning, for a material the mesh uses, with UVs or without:\n"
        + "MaterialNoEffect: \"occlusion is 0.5 but the material has no occlusion_texture, so it has no effect (glTF scales only the map)\", by material. Fix: give the material an occlusion_texture with define_material, or leave the strength at 1.\n"
        + "\n"
        + "Model warnings, which check adds: a part leaving its envelope (move the part or widen the envelope with define_part).\n"
        + "Graph material warnings, by material (materials['iron'].base_colour_texture): each graph slot of a material the mesh uses that cannot be baked as the model is now, in the words render gives (a missing graph file, a fault in the graph naming its node, an output the graph lacks or the slot cannot take), and for a graph that reads the mesh, no UVs, UVs that overlap, a mesh_mask naming a group or material the mesh lacks, and \"faces[2, 3] of 'barrel' are unmapped, so 'iron' bakes nothing onto them\". Fix the graph file or define_material again, or give the mesh UVs that do not overlap. check casts no rays and evaluates no graph, so it is quick.\n"
        + "Export's limit warnings and the clip warnings are in those tools' own entries.\n"
        + "\n"
        + "How they are grouped. A kind with 1 to 3 warnings is listed as above. With 4 or more it is one line: the count (for UvOverlap, grouping is by pairs, so 1 to 3 pairs are listed, and the line counts the faces involved, both faces of each pair once), the worst case where the kind measures something (the face furthest off its plane over its limit; the edge shared by the most faces), three samples by lowest id and the select query that names them all. Example:\n"
        + "252 faces are not planar: worst faces[10], 6.03E-05 off its plane (limit 1.07E-05); e.g. faces[0], faces[1], faces[2]; select faces where non_planar names them\n"
        + "Smooth shading: not planar on a smooth-shaded mesh (set_shading) is not a warning; check gives one note with the same line ending \"; expected on a smooth mesh, which export triangulates\", and ok can be true; every other tool says nothing. select faces where non_planar names every face either way.\n"
        + "\n"
        + "Verbosity, the argument of the tools that change the model or write a file, and of check: \"silent\" answers only {\"ok\":true} (check answers its own ok); \"summary\", the default, is the result with each kind of warning grouped as above; \"full\" is the summary with every warning one per line. A tool error is whole at every level.";
}
