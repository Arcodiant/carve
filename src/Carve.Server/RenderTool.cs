using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Carve.Core;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using StbImageWriteSharp;
using static Carve.Server.MeshToolSupport;

namespace Carve.Server;

internal static class RenderTool
{
    /// <summary>
    /// The tool, built here rather than found by <c>WithToolsFromAssembly</c> so that its generated schema can be closed
    /// (<c>additionalProperties: false</c>). <paramref name="services"/> keeps the injected parameters out of the schema.
    /// </summary>
    public static McpServerTool Create(IServiceProvider services)
    {
        var tool = McpServerTool.Create(typeof(RenderTool).GetMethod(nameof(Run))!, target: null,
            new McpServerToolCreateOptions { Services = services });
        var schema = JsonNode.Parse(tool.ProtocolTool.InputSchema.GetRawText())!;
        schema["properties"]!["highlight"] = HighlightSchema();
        schema["properties"]!["views"] = ViewsSchema();
        schema["properties"]!["pose"] = PoseSchema();
        schema["properties"]!["targets"] = TargetWeightsArgument.Schema();
        schema["properties"]!["clip"] = new JsonObject
        {
            ["description"] = "Draws the model posed by this clip (define_clip) at 'time'. Not with pose. Framing stays on the rest pose.",
            ["type"] = "string",
        };
        schema["properties"]!["time"] = new JsonObject
        {
            ["description"] = "With clip, and required by it: the moment in seconds, from 0 to the clip's duration.",
            ["type"] = "number",
        };
        WithMesh(schema.AsObject(), MeshArgumentText);
        schema["properties"]!["columns"] = new JsonObject
        {
            ["description"] = "With views: tiles to a row, 1 to 3. Default: the number of views, at most 3.",
            ["type"] = "integer",
        };
        tool.ProtocolTool.InputSchema = Closed(JsonSerializer.SerializeToElement(schema));
        return tool;
    }

    private static JsonObject PoseSchema() => new()
    {
        ["description"] = "Draws the model posed: an object of bone name to {rotation: [x, y, z, w] unit quaternion, translation: [x, y, z]}, both local to the parent and absolute, each optional (a bone or part left out rests). Needs a skeleton (define_skeleton); only weighted vertices move, so a mesh with no weights (set_weights or weigh) is drawn at rest, with no error. Framing stays on the rest pose.",
        ["type"] = "object",
        ["additionalProperties"] = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["rotation"] = new JsonObject { ["type"] = "array", ["items"] = new JsonObject { ["type"] = "number" }, ["minItems"] = 4, ["maxItems"] = 4 },
                ["translation"] = new JsonObject { ["type"] = "array", ["items"] = new JsonObject { ["type"] = "number" }, ["minItems"] = 3, ["maxItems"] = 3 },
            },
        },
    };

    /// <summary>The clip and time arguments: the clip's name (null when absent) and the time; problems are added to <paramref name="errors"/>.</summary>
    private static (string? Name, float Time) ReadClip(JsonElement? clip, JsonElement? time, JsonElement? pose, List<string> errors)
    {
        if (Absent(clip))
        {
            if (!Absent(time)) errors.Add("time: only with clip; give the clip to draw at this time");
            return (null, 0);
        }
        var before = errors.Count;
        if (!Absent(pose)) errors.Add("clip: not with pose; give a pose, or a clip and a time");
        if (clip!.Value.ValueKind != JsonValueKind.String) errors.Add($"clip: expected a clip name, got {Describe(clip.Value)}");
        var at = 0f;
        if (Absent(time)) errors.Add("time: required with clip; the moment in seconds, from 0 to the clip's duration");
        else if (!IsFinite(time!.Value))
            errors.Add($"time: expected a finite number of seconds, got {Describe(time.Value)}");
        else at = (float)time.Value.GetDouble();
        return errors.Count > before ? (null, 0) : (clip.Value.GetString(), at);
    }

    /// <summary>The pose argument: null when absent, else a pose; problems are added to <paramref name="errors"/>.</summary>
    private static Pose? ReadPose(JsonElement? pose, List<string> errors)
    {
        if (Absent(pose)) return null;
        if (pose!.Value.ValueKind != JsonValueKind.Object)
        {
            errors.Add($"pose: expected an object of bone name to {{rotation, translation}}, got {Describe(pose.Value)}");
            return null;
        }
        var bones = new Dictionary<string, (Quaternion?, Vector3?)>();
        var before = errors.Count;
        foreach (var b in pose.Value.EnumerateObject())
        {
            var at = $"pose.{b.Name}";
            if (b.Value.ValueKind != JsonValueKind.Object) { errors.Add($"{at}: expected an object {{rotation, translation}}, got {Describe(b.Value)}"); continue; }
            Quaternion? rotation = null;
            Vector3? translation = null;
            foreach (var f in b.Value.EnumerateObject())
            {
                if (f.Name is not ("rotation" or "translation")) { errors.Add($"{at}.{f.Name}: not a field of a pose bone; its fields are rotation, translation"); continue; }
                var want = f.Name == "rotation" ? 4 : 3;
                var values = new List<float>();
                var ok = f.Value.ValueKind == JsonValueKind.Array && f.Value.GetArrayLength() == want;
                if (ok)
                    foreach (var n in f.Value.EnumerateArray())
                    {
                        if (!IsFinite(n)) { ok = false; break; }
                        values.Add((float)n.GetDouble());
                    }
                if (!ok) { errors.Add($"{at}.{f.Name}: expected {want} finite numbers"); continue; }
                if (want == 4)
                {
                    var q = new Quaternion(values[0], values[1], values[2], values[3]);
                    if (MathF.Abs(q.Length() - 1) > Skeleton.UnitTolerance) errors.Add(FormattableString.Invariant($"{at}.rotation: not a unit quaternion: its length is {q.Length():G6}, and it must be 1 (within {Skeleton.UnitTolerance})"));
                    else rotation = q;
                }
                else translation = new Vector3(values[0], values[1], values[2]);
            }
            bones[b.Name] = (rotation, translation);
        }
        return errors.Count > before ? null : new Pose(bones);
    }

    private static JsonObject HighlightSchema()
    {
        static JsonObject Ints() => new() { ["type"] = "array", ["items"] = new JsonObject { ["type"] = "integer" } };
        return new JsonObject
        {
            ["description"] = "What to show in orange, in the shape select returns and move takes; each list optional.",
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["vertices"] = Ints(),
                ["edges"] = new JsonObject
                {
                    ["type"] = "array",
                    ["items"] = new JsonObject
                    {
                        ["type"] = "array",
                        ["items"] = new JsonObject { ["type"] = "integer" },
                        ["minItems"] = 2,
                        ["maxItems"] = 2,
                    },
                },
                ["faces"] = Ints(),
            },
        };
    }

    private static JsonObject ViewsSchema()
    {
        static JsonObject Numbers() => new() { ["type"] = "array", ["items"] = new JsonObject { ["type"] = "number" } };
        return new JsonObject
        {
            ["description"] = "2 to 6 tiles of one sheet, each a view name or {view or direction, zoom, target}. Not with view, direction, zoom or target.",
            ["type"] = "array",
            ["items"] = new JsonObject
            {
                ["anyOf"] = new JsonArray(
                    new JsonObject { ["type"] = "string" },
                    new JsonObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JsonObject
                        {
                            ["view"] = new JsonObject { ["type"] = "string" },
                            ["direction"] = Numbers(),
                            ["zoom"] = new JsonObject { ["type"] = "number" },
                            ["target"] = Numbers(),
                        },
                    }),
            },
        };
    }

    private const string Text =
        "Renders the current mesh, as set by define_mesh or open and changed by the edits (move, extrude, subdivide, delete, merge_faces, bridge, plane_cut, loop_cut, knife) or undo (with several meshes, every mesh together, framed on the bounds of them all; labels and highlight apply to the one mesh 'mesh' names, with its own ids), and returns one PNG image, 512x512 pixels unless 'size' says otherwise, followed by one caption line, "
        + "e.g. '512x512, view front, 8 vertices, 6 faces'. The image is shaded by Empyrean, the engine games built on this model use. "
        + "No file is written. Nothing is drawn but the mesh, and the look's lines and spikes, the highlight's orange lines and stars, and the labels and tile captions you ask for: no grid or axes, a dark background, about 8% margin around the mesh. "
        + "Views: 'front' (the default) looks from the +X +Y +Z corner, showing the mesh's top, its +Z side on the left and its +X side on the right; "
        + "'back' looks from the opposite corner, -X -Y -Z, upward from below, showing its underside, -Z side and -X side. "
        + "Six more view names look head-on along an axis, as that direction would: '+x' shows the +X side, +Z on the left; '-x' the -X side, +Z on the right; "
        + "'+z' the +Z side, +X on the right; '-z' the -Z side, +X on the left; all four with +Y up. "
        + "'+y' looks down on the top, +X on the right; '-y' looks up at the underside, +X on the left; both with -Z up. "
        + "Or give 'direction' instead of 'view': [x, y, z], pointing from the point looked at toward the eye, in model space's axes, any length. "
        + "Up on the image is +Y, or -Z when the direction is within 1 degree of straight up or down. "
        + "Examples: [1,0,0] looks from +X, [-1,0,0] from -X, [0,1,0] from above, down at the mesh, [0,-1,0] from below, [0,0,1] from +Z, [0,0,-1] from -Z. "
        + "A head-on axis view shows every face pointing one way as one flat shade, so a slight tilt such as [0.4, 2, 0.8] reads better from above. "
        + "'zoom' (1 to 8, default 1) narrows the view about 'target' without moving the camera; 'target' is the point [x, y, z] to look at and zoom on, "
        + "in model space, every part where it is placed, and inside the bounds of what is drawn (default: the centre of the bounds). "
        + "The camera frames the whole mesh each time, so a render shows shape, not size; zoom in on a target to see a detail. "
        + "The caption then reads e.g. '512x512, direction (2, 1, 1), zoom 3, target (1.02, 0.4, 0.5), 72 vertices, 70 faces', "
        + "with zoom and target only when you set them. "
        + "'projection' is 'perspective' (the default, what a game shows) or 'orthographic', which keeps parallel edges parallel and shows true proportions: "
        + "the one for reading dimensions and for a sheet of axis views. It frames the mesh by the same margin; zoom and target work the same. "
        + "The caption says so after the view: '512x512, view +x, orthographic, 8 vertices, 6 faces'. "
        + "'size' is the image's width and height: 256 for a quick look or a sheet tile, 512 (the default), 1024 for a dense mesh with labels. "
        + "The caption opens with it: '256x256, view front, 8 vertices, 6 faces'. "
        + "'mode' is the look: 'shaded' (the default), 'wireframe', 'normals', 'checker' or 'uvs'. Neither wireframe nor normals depends on the light. "
        + "In 'shaded', a face with a material (define_material, set_material) is drawn as the game's PBR shading draws it, its textures sampled at the face's UVs (a face with no UVs samples the texture's corner at (0, 0), as the game does), "
        + "lit by Carve's light and a fixed sky so that metal reads; a game's own lighting will differ. Only material faces take the sky's light, so a matte white material face is brighter and bluer on top than an uncoloured or coloured face. Every other look ignores materials, as it ignores colours. "
        + "'wireframe' draws every edge in light grey over faces filled near black: use it to count and place edges and see the face loops. It is hidden-line, not see-through: "
        + "an edge behind a face is hidden, so look from another side to see it. "
        + "'normals' colours each face by the way it faces and stands a light grey spike on it along its normal, at its centre, or for a face with holes at a point on the face clear of the holes: use it to find a face pointing the wrong way, or a non-planar face, whose spike is off. On a smooth mesh (set_shading) it shows the corner normals instead: the colour blends across a soft edge and breaks at a hard one, and a spike stands on each corner along its normal. "
        + "The colour code, as linear red, green, blue, is (normal + 1) / 2: +X (1, 0.5, 0.5) pink, -X (0, 0.5, 0.5) teal, +Y (0.5, 1, 0.5) green, -Y (0.5, 0, 0.5) purple, +Z (0.5, 0.5, 1) blue, -Z (0.5, 0.5, 0) olive; "
        + "a face reversed shows the opposite colour. A face wound the wrong way is still culled as in shaded, so from outside it shows as MISSING, and its opposite colour is seen from inside, through the gap. "
        + "The caption says the mode after the projection: '512x512, view front, wireframe, 72 vertices, 70 faces'. "
        + "'highlight' shows what a select picked, before you edit it: {\"vertices\":[ids],\"edges\":[[a,b]],\"faces\":[ids]}, each list optional, the shape select returns and move takes. Only those three keys are read, so a whole select result works as a highlight and any other key, a misspelt one too, is ignored: check the caption's highlight counts. The server holds no selection, so pass the ids each time. "
        + "All of it is orange: a highlighted face is shaded orange (an orange fill in wireframe; in normals it keeps its normal colour and the edges of its loop turn orange), an edge is an orange line, a vertex a small orange star. "
        + "Faces hide them as they hide anything, so a highlight on the far side needs another view. "
        + "The caption counts it after the mode: '512x512, view front, wireframe, highlight 2 faces, 72 vertices, 70 faces', or 'highlight 3 faces, 2 edges, 1 vertex'. "
        + "'labels' writes element ids on the image, to read which face or vertex is which: 'none' (the default), 'faces', 'vertices' or 'both'. A face's id sits on the face, at its centre, or for a face with holes at a point on the face clear of the holes; a vertex is a white dot with its id just above and to the right. Each id is white on a dark backing, and is the id move, extrude and highlight take. "
        + "Only what faces the camera is labelled: a face turned away has no label, and a vertex has one when it is on a face turned toward you. Nearer geometry is not considered, so on a concave mesh a label may sit on a face hidden behind another; look from another side to be sure. Edges have no labels: name an edge by its two vertex ids. "
        + "Labels crowd on a dense mesh: use size 1024, or zoom in on a target. The caption says so after the mode: '512x512, view front, labels both, 8 vertices, 6 faces'. "
        + "'views' asks for a contact sheet instead of one view: one image of 2 to 6 tiles, so one call shows a mesh all round. Each entry is a view name, or an object with 'view' or 'direction' and optional 'zoom' and 'target', e.g. [\"front\", \"+x\", {\"direction\": [2, 1, 1], \"zoom\": 2, \"target\": [1, 0.4, 0.5]}]. "
        + "'views' replaces 'view', 'direction', 'zoom' and 'target'; 'mode', 'labels', 'size', 'projection' and 'highlight' apply to every tile. 'columns' (1 to 3, default the number of views, at most 3) sets the tiles to a row, filled in the order given; a tile left over is background. 'columns' applies only with 'views': a single render ignores it. Every tile of a sheet shows the same version of the mesh: if an edit lands while the tiles are drawn, the sheet is drawn again, up to 3 tries in all, and if the mesh changed on each of them it is a tool error. "
        + "Each tile is a full render at 'size', captioned in its top left corner with its view name or direction as you gave it, then any zoom and target, cut off at the tile's edge if longer. Tiles are 2 pixels apart, so a sheet is columns x size + (columns - 1) x 2 wide and rows x size + (rows - 1) x 2 high. "
        + "The quick all-round look is the six axis views, orthographic, at 256: {\"views\": [\"+x\", \"-x\", \"+y\", \"-y\", \"+z\", \"-z\"], \"projection\": \"orthographic\", \"size\": 256}, captioned '3x2 tiles of 256x256, orthographic, views +x, -x, +y, -y, +z, -z, 8 vertices, 6 faces'. "
        + "A sheet's caption gives columns by rows, the tile size, the parts a single caption has, and every tile's caption: '2x2 tiles of 512x512, wireframe, labels faces, views front, (2, 1, 1), +x, -z, 72 vertices, 70 faces'. "
        + "The light turns with the view, and faces turned different ways show as different brightnesses. "
        + "Faces wound the wrong way (clockwise seen from outside) show as MISSING: you see through them to whatever is behind. "
        + "An EMPTY result, an image of only the dark background with no error, means no face points at the camera: "
        + "the faces are wound the wrong way, or the mesh is an open surface seen from behind. "
        + "Try the other view, or fix the winding and call define_mesh again (move and extrude keep each face's winding, so they cannot fix it). "
        + "'checker' draws the model lit with a 256x256 checker of 8x8 cells at each face's corner UVs (set_uvs, project_uvs, unwrap), with the same normals as shaded. "
        + "Each column has its own hue, red at u = 0 through orange, yellow, green, cyan, blue and indigo to violet at u = 1; light and dark cells alternate, and the cell at uv (0,0), the texture's top-left, is white. "
        + "Stretched or squashed cells mean stretched UVs; hues running violet to red along u, or the white cell at the wrong corner of an island, mean it is mirrored or turned; magenta is a face with no UVs. UVs outside 0 to 1 repeat the checker. Face colours are not drawn; a highlighted face is the checker in orange. "
        + "Example: on define_mesh's unit cube, face 1 is [4,5,6,7], the +Z face, whose loop starts at vertex 4, its bottom-left corner seen from outside, and runs counterclockwise: bottom-right, top-right, top-left. Given [[0,1],[1,1],[1,0],[0,0]] in that order (from vertex 4), it shows the 64 cells upright, white at its top-left and the hues red to violet left to right, and check reports nothing; given [[1,1],[0,1],[0,0],[1,0]] (also from vertex 4) it shows violet at its top-left and white at its top-right: mirrored, and check reports the face flipped. "
        + "The caption names the mode, and says when no face has UVs: '512x512, view front, checker, no face has UVs, 8 vertices, 6 faces'. "
        + "'uvs' draws one mesh's UV layout flat, as the texture is laid out: u right, v down, (0,0) top-left, the unit square outlined in white with its corners labelled (0,0), (1,0), (0,1) and (1,1), the window grown, square, to take in UVs outside it, with a margin of a sixteenth of the window each side. The checker is tiled dimmed (40%) across the window; each mapped face is the checker at full brightness where its UVs lie, so bright is texture in use and dim is empty. Every face edge is white and a seam (mark_edges seam) yellow, on both islands it divides. Faces sharing texture overlap exactly, so an overlap shows as edges crossing inside a bright area, not as a colour (check reports overlaps). A face with no UVs is not drawn, and the caption counts them. labels 'faces' writes each face's id at the centre of its UV outline; 'vertices' and 'both' are refused. A highlighted face is solid orange; highlight takes faces only. 'mesh' is required when the model has several meshes. Refused in this mode, each named: view, direction, zoom, target, projection, views, columns, pose, clip, time, bones, bone_labels and weights. " + ImageLookText + "A mesh with no UVs is a tool error naming set_uvs, project_uvs and unwrap. Example: a cube after project_uvs box shows six bright squares in a 3 x 2 grid with dim gaps; labels 'faces' names each; a square with the white cell at its top-right rather than its top-left is mirrored. The caption: '512x512, uvs crate, labels faces, highlight 1 face, 2 faces unmapped, 72 vertices, 70 faces', the counts the named mesh's. "
        + "Tool errors, with the mesh untouched: both 'view' and 'direction' given; a view that is not one of the eight names; a direction of zero or not three finite numbers; "
        + "a zoom outside 1 to 8; a target that is not three finite numbers or lies outside the mesh's bounds; a projection other than 'perspective' or 'orthographic'; a size other than 256, 512 or 1024; a mode other than 'shaded', 'wireframe', 'normals', 'checker' or 'uvs'; a highlight that is not an object, or whose lists are not lists of ids and [a, b] pairs; "
        + "a highlight naming a vertex or face the mesh does not have, or a pair that is not an edge, each listed by element as move does; labels other than 'none', 'faces', 'vertices' or 'both'; "
        + "views together with view, direction, zoom or target; fewer than 2 views or more than 6; an entry of views that is neither a view name nor an object with 'view' or 'direction', or one with both; an entry's view, direction, zoom or target that would be an error on its own, named by its place, e.g. 'the 3rd listed entry'; columns other than 1, 2 or 3; an argument render does not take; a sheet whose tiles would show different meshes, because the mesh changed while it was drawn on each of 3 tries (\"The mesh changed while the sheet was drawn, on each of 3 tries, so its tiles would show different meshes; call render again once the edits have settled.\"); labels or highlight given with 'mesh' among several meshes, when an edit lands while the frame is drawn, since their ids may then name other elements (\"The model changed while the frame was drawn, so the ids given for mesh '<name>' may name other elements; call render again once the edits have settled.\"). "
        + "'pose' draws the model posed by its skeleton: an object of bone name to {rotation: [x, y, z, w], translation: [x, y, z]}, each part optional and absolute (local to the parent, replacing the rest value; a bone or part left out rests), skinned by the weights set_weights or weigh gave. A vertex with no weight stays where it is. The camera frames the REST pose, so the frame does not move between poses and a pose that swings far can leave it. Errors: a bone the skeleton lacks, a rotation that is not a unit quaternion, no skeleton. "
        + "'targets' draws the model at morph target weights (define_target): an object of target name to weight, 0 to 1, applied to every mesh that has a target of that name, before the pose or clip skins it, so a morphed arm bends at the elbow. It works with pose, clip and time, views sheets and every look except uvs; the camera frames the REST bounds, so the frame does not move with the weights, and the normals are worked out from the morphed positions. Errors: a name no mesh has (the targets are listed), a weight below 0 or above 1 or not a number. "
        + "'bones' draws the skeleton over the model, in the pose or clip of the frame: magenta lines, 3 pixels wide, over the model and not hidden by it. A bone with children is a line from its head to each child's head; a leaf bone is a line along its parent's direction for half its parent's length; every head has a dot; a lone bone with no parent and no child is a dot. 'bone_labels' (needs bones: true) writes each bone's name white on a dark backing, up and to the right of its head; names crowd on a dense skeleton, so use size 1024 or zoom. The caption says 'bones' or 'bones, bone labels' after the labels and highlight. Both are independent of labels, highlight, mode and mesh. Errors: bones or bone_labels with no skeleton, bone_labels without bones: true. "
        + "'weights' is a bone's name: the model is drawn unlit, each face corner coloured by that bone's weight at its vertex: dark blue at 0, through green at 0.5, to red at 1, so red is what the bone wholly moves and blue what it leaves alone. A vertex with no weight at all (not even to another bone) is magenta, and magenta means the vertex needs fixing: call set_weights or weigh, since export refuses it. It follows the frame's pose or clip, replaces mode (not with wireframe, normals or checker), and draws on the model's faces, so with bones: true the magenta bone lines are on top of the same magenta a bad vertex has: tell them apart by shape, the bone lines being thin lines with dots at the joints and an unweighted vertex a patch of a face. The caption says 'weights <bone>' where the mode would be. Errors: no skeleton, a bone the skeleton lacks (the bones are listed). "
        + "'clip' and 'time' draw the model as the clip of that name (define_clip) poses it at that time in seconds, interpolated as define_clip says; framed on the rest pose like pose. Errors: clip together with pose, clip without time or time without clip, no clip of that name, a time outside 0 to the clip's duration. "
        + PartsText
        + "Calling render with no mesh (before define_mesh or open) is a tool error saying so. A machine with no working Vulkan device gives a tool error naming Vulkan; "
        + "define_mesh, open, save, export, move, extrude, get_mesh, undo, select, check, define_group, set_colour, define_skeleton, get_skeleton, set_weights, weigh, get_weights, define_clip and delete_clip still work there.";

    /// <summary>T-94's paragraph of render's text: the placed model, part and level.</summary>
    public const string PartsText =
        "The model is drawn as the game places it: every part at its world placement (get_parts), each instance as a copy of its source. "
        + "'part' names one part: only it and the parts under it are drawn, each where it is placed, and the camera frames their bounds; an instance draws its source's geometry. "
        + "'level' draws a kept level (keep_level): each part at its newest kept level at or below it, a part with none not drawn; not with pose, clip, targets or weights, since a level is drawn as it was kept, at rest. "
        + "With part, labels and highlight apply to 'mesh', which must be drawn with it, or with mesh left out to the one part under it with geometry of its own; the ids are that part's own (at a level, the level's), shown on its own occurrence and not on its instances. "
        + "The caption names them before the counts: '512x512, view front, part Axle_F, level 0, 32 vertices, 20 faces'. "
        + "Errors: a part the model lacks (the parts are listed), a level below 0, a part or level that draws nothing, and a mesh not drawn with the part or at the level. ";

    /// <summary>The description of render's <c>part</c>.</summary>
    public const string PartArgumentText = "A part's name (get_parts): draws only it and the parts under it, each where it is placed, framed on them. Default: the whole model.";

    /// <summary>The description of render's <c>level</c>.</summary>
    public const string LevelArgumentText = "A kept level (keep_level), 0 or more: each part at its newest kept level at or below it; a part with none is not drawn. Not with pose, clip, targets or weights.";

    /// <summary>The text of render's <c>mesh</c>: it aims labels and highlight, never what is drawn.</summary>
    private const string MeshArgumentText =
        "The mesh labels and highlight apply to, as named to define_mesh; ids are that mesh's own, as its edit tools take them. "
        + "Every mesh is drawn either way. May be left out when the model has one mesh, or when neither labels nor highlight is asked for; "
        + "left out while there are several and they are asked for, the call is a tool error that lists the names. In mode 'uvs', the one mesh drawn, required when there are several.";

    /// <summary>The sentences render's description gains for <c>image</c> (T-121), kept apart so the unchanged-tools digest can take them out.</summary>
    internal const string ImageLookText = "With image (a PNG path), the PNG is drawn under the layout to show a bake or a texture where it lands on the mesh: it covers the unit square, u right and v down, drawn as stored inside the islands (where the checker is bright) and at half brightness outside them, each channel halved, so padding shows as a fringe of island colour on a darker ground; beyond the unit square the dim checker stays. It is sampled nearest, unlit, with no colour conversion (a grey or single-channel PNG shows as grey), and the edges, seams, labels and highlight are drawn over it. The caption names it: 'image wrench.occlusion.png 1024 x 1024'. A PNG that is missing (the folder's PNGs are listed), is not a PNG or is over 4096 is refused as define_material refuses a texture. ";

    /// <summary>The text of render's <c>image</c> (T-121).</summary>
    internal const string ImageArgumentText =
        "A PNG path, mode 'uvs' only (refused with any other mode): draws that image under the layout, to see a bake or a texture where it lands on the mesh's UVs. "
        + "Resolved as define_material's texture paths are (relative to the working directory) and read under its rules: a PNG of 1 to 4096 pixels on each side. A missing file lists the .png files in its folder.";

    [McpServerTool(Name = "render"), Description(Text)]
    public static async Task<CallToolResult> Run(
        RequestContext<CallToolRequestParams> context,
        EngineStatus status,
        ModelStore store,
        RenderQueue queue,
        [Description("Where to look from: the corner 'front' (the default when no direction is given) or 'back', or head-on along an axis: '+x', '-x', '+y', '-y', '+z', '-z'. Not with direction."), AllowedValues("front", "back", "+x", "-x", "+y", "-y", "+z", "-z")] string? view = null,
        [Description("[x, y, z]: from the point looked at toward the eye. Up is +Y, or -Z within 1 degree of vertical. Not with view.")] double[]? direction = null,
        [Description("1 to 8: how far to zoom in about the target. The default 1 frames the whole mesh.")] double zoom = 1,
        [Description("[x, y, z] in model space, inside the bounds of what is drawn: the point to look at. Default: the centre.")] double[]? target = null,
        [Description("'perspective' (the default), what a game shows, or 'orthographic': parallel edges stay parallel, true proportions."), AllowedValues("perspective", "orthographic")] string? projection = null,
        [Description("The image's width and height: 256, 512 (the default) or 1024."), AllowedValues(256, 512, 1024)] double size = RenderSpec.DefaultSize,
        [Description("The look: 'shaded' (the default), 'wireframe' (every visible edge over a flat fill) , 'normals' (faces coloured by facing, with spikes), 'checker' (lit, a hued checker at each face's UVs, white cell at uv (0,0), magenta where a face has none) or 'uvs' (one mesh's UV layout flat: u right, v down, bright where faces use the texture, dim where empty, white edges, yellow seams)."), AllowedValues("shaded", "wireframe", "normals", "checker", "uvs")] string? mode = null,
        JsonElement? highlight = null,
        [Description("Element ids to write on the image: 'none' (the default), 'faces', 'vertices' or 'both'. Only what faces the camera."), AllowedValues("none", "faces", "vertices", "both")] string? labels = null,
        JsonElement? views = null,
        double? columns = null,
        JsonElement? mesh = null,
        JsonElement? pose = null,
        JsonElement? clip = null,
        JsonElement? time = null,
        JsonElement? targets = null,
        [Description("true draws the skeleton over the model, in the frame's pose: a line from each bone's head to each child's head, a dot on every head, and a leaf bone as a line along its parent's direction for half its parent's length. Magenta, drawn over the model and not hidden by it.")] bool? bones = null,
        [Description("true writes each bone's name beside its head, white on a dark backing. Needs bones: true.")] bool? bone_labels = null,
        [Description("A bone's name: draws the model unlit with that bone's weight at each vertex, dark blue at 0 through green (0.5) to red at 1; a vertex with no weight at all is magenta and needs fixing (set_weights or weigh). Needs a skeleton; follows pose or clip; not with mode wireframe, normals or checker.")] string? weights = null,
        [Description(PartArgumentText)] string? part = null,
        [Description(LevelArgumentText)] int? level = null,
        [Description(ImageArgumentText)] string? image = null,
        ToolFolder? toolFolder = null,
        CancellationToken cancellationToken = default)
    {
        if (UnknownArguments("render", context, "view", "direction", "zoom", "target", "projection", "size", "mode", "highlight", "labels", "views", "columns", "mesh", "pose", "clip", "time", "targets", "bones", "bone_labels", "weights", "part", "level", "image") is { Count: > 0 } unknown)
        {
            return Failed("render", unknown);
        }

        if (mode == "uvs")
        {
            return await RunUvs(context, status, store, queue, size, labels, highlight, mesh, image, toolFolder?.Directory, cancellationToken);
        }

        // RV-74: the image's refusal is listed with the call's other problems, in its own words, and alone when it is the only one.
        var imageProblem = image is null ? null : $"image: not with mode '{mode ?? "shaded"}'; it is the picture drawn under the layout of mode 'uvs'.";
        CallToolResult Refuse(string problem) => imageProblem is null ? Error(problem) : Failed("render", [imageProblem, problem]);

        var sheet = !Absent(views);
        var given = context.Params?.Arguments;
        // A null, or zoom at its default of 1, is absent, so a client that sends nulls or fills in the schema's defaults can ask for a sheet.
        bool Given(string name) =>
            given is not null && given.TryGetValue(name, out var value) && !Absent(value)
            && !(name == "zoom" && value.ValueKind == JsonValueKind.Number && value.GetDouble() == 1);
        if (sheet && new[] { "view", "direction", "zoom", "target" }.Any(Given))
        {
            return Refuse("views: not with view, direction, zoom or target; give those in each entry instead.");
        }

        if (columns is { } asked && asked is not (1 or 2 or 3))
        {
            return Refuse($"columns: {Join([asked])} is not 1, 2 or 3.");
        }

        if (!sheet && CameraRefusal(view, direction, zoom, target) is { } refused)
        {
            return Refuse(refused);
        }

        string? tileProblem = null;
        var tiles = sheet ? Tiles(views!.Value, out tileProblem) : [new Tile(view, direction, zoom, target)];
        if (tileProblem is not null)
        {
            return Refuse(tileProblem);
        }

        if (projection is not (null or "perspective" or "orthographic"))
        {
            return Refuse($"projection: '{projection}' is not a projection; use 'perspective' or 'orthographic'.");
        }

        if (!RenderComposition.Sizes.Any(s => s == size))
        {
            return Refuse($"size: {Join([size])} is not a size; use 256, 512 or 1024");
        }

        if (mode is not (null or "shaded" or "wireframe" or "normals" or "checker"))
        {
            return Refuse($"mode: '{mode}' is not a mode; use 'shaded', 'wireframe', 'normals', 'checker' or 'uvs'.");
        }

        if (labels is not (null or "none" or "faces" or "vertices" or "both"))
        {
            return Refuse($"labels: '{labels}' is not a label set; use 'none', 'faces', 'vertices' or 'both'.");
        }

        if (bone_labels == true && bones != true)
        {
            return Refuse("bone_labels: needs bones: true; the names are written beside the bones.");
        }

        if (weights is not null && mode is "wireframe" or "normals" or "checker")
        {
            return Refuse($"weights: not with mode '{mode}'; the weights look draws the faces itself.");
        }

        var problems = new List<string>();
        var meshName = MeshName(mesh, problems);
        var posed = ReadPose(pose, problems);
        var (clipName, clipTime) = ReadClip(clip, time, pose, problems);
        var morph = TargetWeightsArgument.Read(targets, store.Current.Model, problems);
        var listed = HighlightLists(highlight, problems);
        if (level is < 0) problems.Add($"level: {level} is not a level; a kept level is 0 or more (keep_level)");
        if (level is not null)
        {
            foreach (var (name, on) in new[] { ("pose", posed is not null), ("clip", clipName is not null), ("targets", morph is not null), ("weights", weights is not null) })
                if (on) problems.Add($"level: not with {name}; a kept level is drawn as it was kept, at rest");
        }

        if (imageProblem is not null) problems.Insert(0, imageProblem);
        if (problems.Count > 1 || (problems.Count == 1 && imageProblem is null))
        {
            return Failed("render", problems);
        }
        if (imageProblem is not null)
        {
            return Error(imageProblem);
        }

        // The part is checked against the model of the call before the engine is waited on, so a wrong name needs no device.
        if (part is not null && PartRefusal(store.Current.Model, part, meshName) is { } partRefused)
        {
            return Failed("render", [partRefused]);
        }

        var projected = projection == "orthographic" ? Projection.Orthographic : Projection.Perspective;
        var look = mode switch { "wireframe" => Look.Wireframe, "normals" => Look.Normals, "checker" => Look.Checker, _ => Look.Shaded };
        if (weights is not null) look = Look.Weights;
        var labelled = labels switch { "faces" => LabelSet.Faces, "vertices" => LabelSet.Vertices, "both" => LabelSet.Both, _ => LabelSet.None };

        await status.WhenSettled.WaitAsync(cancellationToken);
        if (status.FaultMessage is { } fault)
        {
            return Error(fault);
        }

        var current = store.Current;
        if (current.Mesh is not { } drawn)
        {
            return Error("There is no mesh yet: call define_mesh first, or open to read a saved .carve file, then render.");
        }

        if (clipName is not null)
        {
            if (current.Model.FindClip(clipName) is not { } sampled)
                return Failed("render", [current.Model.Clips.Count == 0
                    ? $"clip: there is no clip named '{clipName}'; the model has no clips (define_clip makes one)"
                    : $"clip: there is no clip named '{clipName}'; the clips are {NameList.Quoted(current.Model.Clips.Select(c => c.Name))}"]);
            if (clipTime < 0 || clipTime > sampled.Duration)
                return Failed("render", [FormattableString.Invariant($"time: {clipTime} s is outside the clip '{clipName}', which runs from 0 to {sampled.Duration} s")]);
            posed = sampled.Sample(clipTime);
        }

        if (part is not null && PartRefusal(current.Model, part, meshName) is { } partGone)
        {
            return Failed("render", [partGone]);
        }

        if (part is not null || level is not null)
        {
            if (current.Model.CombinedOf(part, level) is not { } selected)
            {
                return Failed("render", [level is { } none
                    ? $"level: nothing is drawn at level {none}: no part{(part is null ? "" : $" under '{part}'")} has a kept level at or below {none}; keep_level keeps one"
                    : $"part: '{part}' draws nothing: neither it nor any part under it has geometry"]);
            }

            drawn = selected;
        }

        if (posed is not null)
        {
            if (current.Model.Skeleton is not { } rig) return Failed("render", ["pose: the model has no skeleton; call define_skeleton first"]);
            if (Skinning.Unknown(rig, posed).ToArray() is { Length: > 0 } stray)
                return Failed("render", [$"pose: no bone named {NameList.Quoted(stray)}; the bones are {NameList.Quoted(rig.Names)}"]);
        }

        if (weights is not null)
        {
            if (current.Model.Skeleton is not { } weightsRig) return Failed("render", ["weights: the model has no skeleton; call define_skeleton first"]);
            if (!weightsRig.Names.Contains(weights))
                return Failed("render", [$"weights: no bone named '{weights}'; the bones are {NameList.Quoted(weightsRig.Names)}"]);
        }

        BoneDraw? skeleton = null;
        if (bones == true)
        {
            if (current.Model.Skeleton is not { } bonesRig) return Failed("render", ["bones: the model has no skeleton; call define_skeleton first"]);
            skeleton = BoneDraw.Of(bonesRig, posed, bone_labels == true);
        }

        // Every mesh is drawn, as one. Labels and highlight name ids within one mesh, so with several they are aimed
        // by mesh, as the edit tools are; the ids are moved past the meshes before it in the drawn whole.
        var wantsIds = labelled != LabelSet.None || listed.Vertices.Count > 0 || listed.Edges.Count > 0 || listed.Faces.Count > 0;
        var idsIn = drawn;
        ElementWindow? window = null;
        string? shiftedFor = null;
        (int Vertices, int Faces) offset = (0, 0);
        // With part, labels and highlight left unaimed go to the one part under it with geometry of its own (T-94).
        var aimName = meshName ?? (part is not null && wantsIds ? SoleOwner(current.Model, part) : null);
        // A single mesh drawn as more than itself (its instances, or its placement) is aimed too: its ids are its own occurrence's (RV-71).
        var alone = current.Model.Meshes is [var only] && ReferenceEquals(drawn, only.Mesh);
        if (aimName is not null || (wantsIds && (part is not null || level is not null || !alone)))
        {
            if (current.Model.Target(aimName, out var aimed) is not { } named)
            {
                return Failed("render", [aimName is null ? aimed + "; labels and highlight name ids within one mesh" : aimed!]);
            }

            var own = level is { } at ? current.Model.LevelMesh(named.Name, at) : named.Mesh;
            if (own is null || (part is not null && !current.Model.SubtreeOf(part).Contains(named.Name)))
            {
                return Failed("render", [own is null
                    ? $"mesh: '{named.Name}' has no kept level at or below {level}, so it is not drawn at level {level}"
                    : $"mesh: '{named.Name}' is not drawn with part '{part}'; the parts under it are {NameList.Quoted(current.Model.SubtreeOf(part!))}"]);
            }

            if (!ReferenceEquals(drawn, own))
            {
                idsIn = own;
                offset = current.Model.OffsetOf(named.Name, part, level)!.Value;
                window = new ElementWindow(offset.Vertices, offset.Faces, own.Faces.Count);
                // Only ids asked for are shifted; a render naming the mesh with none has nothing a later model could misplace.
                shiftedFor = wantsIds ? named.Name : null;
            }
        }

        var bounds = MeshReport.Create(drawn);
        // The checker's note, over the mesh named, else every mesh drawn.
        var noUvs = idsIn.Uvs is not { MappedFaces.Count: > 0 };
        for (var k = 0; k < tiles.Count; k++)
        {
            if (tiles[k].TargetVector is { } t && TargetCheck.Refusal(t, bounds.BoundsMin, bounds.BoundsMax) is { } refusal)
            {
                return Error(sheet ? $"views: the {Ordinal(k)} listed entry: {refusal}" : refusal);
            }
        }

        if (MeshEdits.UnknownElements(idsIn, listed.Vertices, listed.Edges, listed.Faces) is { Count: > 0 } missing)
        {
            return Failed("render", missing.Select(e => e.ToString()));
        }

        var picked = Highlight.Of(
            listed.Vertices.Select(v => v + offset.Vertices),
            listed.Edges.Select(e => (e.A + offset.Vertices, e.B + offset.Vertices)),
            listed.Faces.Select(f => f + offset.Faces));
        async Task<(RenderedFrame[]? Frames, string? Error)> RenderTiles()
        {
            var rendering = Task.WhenAll(tiles
                .Select(tile => queue.Post(new RenderSpec(tile.Camera, projected, (int)size, look, labelled) { Pose = posed, Targets = morph, Highlight = picked, TileCaption = sheet ? tile.Caption : null, LabelWindow = window, Bones = skeleton, Weights = weights, Part = part, Level = level })));

            // The frame thread drains the queue; once it has ended, nothing will.
            await Task.WhenAny(rendering, status.WhenStopped).WaitAsync(cancellationToken);
            if (!rendering.IsCompleted)
            {
                return (null, status.FaultMessage ?? "The engine has stopped, so nothing can be rendered.");
            }

            return rendering.IsCompletedSuccessfully
                ? (rendering.Result, null)
                : (null, $"The render failed: {rendering.Exception?.GetBaseException().Message}");
        }

        var (rendered, failure) = await SheetVersions.OnOneMesh(RenderTiles, f => f.Version, SheetAttempts);
        if (rendered is not { } frames)
        {
            return Error(failure!);
        }
        // The offsets are the checked snapshot's; a frame drawn on a later model may have other elements at those ids.
        if (SheetVersions.ShiftedIdsRefusal(shiftedFor, current.Version, frames[0].Version) is { } stale)
        {
            return Error(stale);
        }
        if (sheet)
        {
            var across = (int)(columns ?? Math.Min(3, tiles.Count));
            var composed = SheetComposer.Compose(
                [.. frames.Select(f => f.Pixels.ToDecodedImage().Pixels)], across, (int)size, SheetComposer.Srgb(RenderComposition.Background));
            return new CallToolResult
            {
                Content =
                [
                    ImageContentBlock.FromBytes(EncodePng(composed.Pixels, composed.Width, composed.Height), "image/png"),
                    new TextContentBlock { Text = SheetCaption(frames[^1], across, tiles, projected, look, labelled, picked, skeleton, weights, noUvs, part, level) },
                ],
            };
        }

        var frame = frames[0];
        var pixels = frame.Pixels.ToDecodedImage();
        return new CallToolResult
        {
            Content =
            [
                ImageContentBlock.FromBytes(EncodePng(pixels.Pixels, pixels.Width, pixels.Height), "image/png"),
                new TextContentBlock { Text = Caption(frame, view, direction, projected, look, labelled, picked, zoom, target, skeleton, weights, noUvs, part, level) },
            ],
        };
    }

    /// <summary>The arguments <c>mode: "uvs"</c> refuses, each with why: the layout has no camera, pose or skeleton.</summary>
    private static readonly (string Name, string Why)[] UvRefused =
    [
        ("view", "the layout is always seen flat, from above the texture"),
        ("direction", "the layout is always seen flat, from above the texture"),
        ("zoom", "the layout always shows the whole window"),
        ("target", "the layout always shows the whole window"),
        ("projection", "the layout is always flat, u right and v down"),
        ("views", "the layout is one image of one mesh"),
        ("columns", "the layout is one image of one mesh"),
        ("pose", "UVs do not move with a pose"),
        ("targets", "UVs do not move with a target"),
        ("clip", "UVs do not move with a clip"),
        ("time", "UVs do not move with a clip"),
        ("bones", "the layout draws no skeleton"),
        ("bone_labels", "the layout draws no skeleton"),
        ("weights", "the layout draws the checker, not weights"),
        ("part", "the layout is one mesh's, named by mesh"),
        ("level", "the layout is the live mesh's"),
    ];

    /// <summary>
    /// <c>mode: "uvs"</c> (T-58): one mesh's UV layout, flat. Every argument it cannot use is refused by name, all at once;
    /// then the mesh is picked (required among several) and must have a mapped face.
    /// </summary>
    private static async Task<CallToolResult> RunUvs(
        RequestContext<CallToolRequestParams> context, EngineStatus status, ModelStore store, RenderQueue queue,
        double size, string? labels, JsonElement? highlight, JsonElement? mesh, string? image, string? baseDirectory, CancellationToken cancellationToken)
    {
        var given = context.Params?.Arguments;
        bool Given(string name) =>
            given is not null && given.TryGetValue(name, out var value) && !Absent(value)
            && !(name == "zoom" && value.ValueKind == JsonValueKind.Number && value.GetDouble() == 1);
        var problems = UvRefused.Where(r => Given(r.Name)).Select(r => $"{r.Name}: not with mode 'uvs'; {r.Why}").ToList();
        if (!RenderComposition.Sizes.Any(s => s == size))
        {
            problems.Add($"size: {Join([size])} is not a size; use 256, 512 or 1024");
        }

        if (labels is "vertices" or "both")
        {
            problems.Add($"labels: '{labels}' is not drawn in mode 'uvs'; use 'faces' or 'none'");
        }
        else if (labels is not (null or "none" or "faces"))
        {
            problems.Add($"labels: '{labels}' is not a label set; in mode 'uvs' use 'faces' or 'none'");
        }

        var meshName = MeshName(mesh, problems);
        var listed = HighlightLists(highlight, problems);
        if (listed.Vertices.Count > 0 || listed.Edges.Count > 0)
        {
            problems.Add("highlight: only faces in mode 'uvs'; a vertex or an edge can lie in several places on the layout");
        }

        MaterialTexture? picture = null;
        if (image is not null && !DefineMaterialTool.LoadTexture("render", "image", image, baseDirectory, out picture, out var imageProblem))
        {
            problems.Add(imageProblem!);
        }

        if (problems.Count > 0)
        {
            return Failed("render", problems);
        }

        await status.WhenSettled.WaitAsync(cancellationToken);
        if (status.FaultMessage is { } fault)
        {
            return Error(fault);
        }

        var current = store.Current;
        if (current.Mesh is null)
        {
            return Error("There is no mesh yet: call define_mesh first, or open to read a saved .carve file, then render.");
        }

        if (current.Model.Target(meshName, out var aimed) is not { } named)
        {
            return Failed("render", [meshName is null ? aimed + "; mode 'uvs' draws one mesh's layout" : aimed!]);
        }

        if (named.Mesh.Uvs is not { MappedFaces.Count: > 0 })
        {
            return Error($"mesh '{named.Name}' has no UVs; give it some with set_uvs, project_uvs or unwrap");
        }

        if (MeshEdits.UnknownElements(named.Mesh, [], [], listed.Faces) is { Count: > 0 } missing)
        {
            return Failed("render", missing.Select(e => e.ToString()));
        }

        var offset = current.Model.OffsetOf(named.Name)!.Value;
        var faces = new ElementWindow(offset.Vertices, offset.Faces, named.Mesh.Faces.Count);
        var labelled = labels == "faces" ? LabelSet.Faces : LabelSet.None;
        var picked = Highlight.Of([], [], listed.Faces.Select(f => f + offset.Faces));
        var spec = new RenderSpec(CameraSpec.ForView(RenderView.Front), Projection.Orthographic, (int)size, Look.Uvs, labelled) { Highlight = picked, UvFaces = faces, UvImage = picture };

        var rendering = queue.Post(spec);
        await Task.WhenAny(rendering, status.WhenStopped).WaitAsync(cancellationToken);
        if (!rendering.IsCompleted)
        {
            return Error(status.FaultMessage ?? "The engine has stopped, so nothing can be rendered.");
        }

        if (!rendering.IsCompletedSuccessfully)
        {
            return Error($"The render failed: {rendering.Exception?.GetBaseException().Message}");
        }

        var frame = rendering.Result;
        // The face range is the checked snapshot's; a frame drawn on a later model may hold other faces there.
        if (current.Model.Meshes.Count > 1 && SheetVersions.ShiftedIdsRefusal(named.Name, current.Version, frame.Version) is { } stale)
        {
            return Error(stale);
        }

        var report = MeshReport.Create(named.Mesh);
        var unmapped = named.Mesh.LiveFaces.Count(f => !named.Mesh.Uvs!.IsMapped(f));
        var pixels = frame.Pixels.ToDecodedImage();
        return new CallToolResult
        {
            Content =
            [
                ImageContentBlock.FromBytes(EncodePng(pixels.Pixels, pixels.Width, pixels.Height), "image/png"),
                new TextContentBlock { Text = UvCaption((int)size, named.Name, labelled, picked, unmapped, report.VertexCount, report.FaceCount, picture) },
            ],
        };
    }

    /// <summary>The <c>uvs</c> caption: <c>512x512, uvs crate, labels faces, highlight 1 face, 2 faces unmapped, 8 vertices, 6 faces</c>, the named mesh's counts.</summary>
    internal static string UvCaption(int size, string mesh, LabelSet labels, Highlight highlight, int unmapped, int vertices, int faces, MaterialTexture? image = null)
    {
        var parts = new List<string> { $"{size}x{size}", $"uvs {mesh}" };
        if (image is not null) parts.Add($"image {Path.GetFileName(image.Path)} {image.Width} x {image.Height}");
        if (labels == LabelSet.Faces) parts.Add("labels faces");
        if (!highlight.IsEmpty) parts.Add($"highlight {highlight.Counts()}");
        if (unmapped > 0) parts.Add(unmapped == 1 ? "1 face unmapped" : $"{unmapped} faces unmapped");
        parts.Add(vertices == 1 ? "1 vertex" : $"{vertices} vertices");
        parts.Add(faces == 1 ? "1 face" : $"{faces} faces");
        return string.Join(", ", parts);
    }

    /// <summary>How many times a sheet is drawn before an edit racing every try is reported.</summary>
    private const int SheetAttempts = 3;

    /// <summary>One camera of a render, as given: the whole of a single render, or an entry of <c>views</c>.</summary>
    private sealed record Tile(string? View, double[]? Direction, double Zoom, double[]? Target)
    {
        /// <summary>Taken in the mesh's own precision (float), so a target typed as the reported bound is inside (B-1).</summary>
        public Vector3? TargetVector => Target is null ? null : Vector(Target);

        public CameraSpec Camera => new(
            Direction is { } d ? Vector(d) : ViewFraming.NamedViews[View ?? "front"],
            (float)Zoom,
            TargetVector);

        /// <summary>What the tile says of itself: the name as given or <c>(x, y, z)</c>, then zoom and target as the single caption has them.</summary>
        public string Caption
        {
            get
            {
                var parts = new List<string> { Direction is null ? View ?? "front" : $"({Join(Direction)})" };
                AddZoomAndTarget(parts, Zoom, Target);
                return string.Join(", ", parts);
            }
        }
    }

    /// <summary>What is wrong with one camera's arguments, in the words of the argument; null when nothing is.</summary>
    internal static string? CameraRefusal(string? view, double[]? direction, double zoom, double[]? target)
    {
        if (view is not null && direction is not null)
        {
            return "view and direction: give one, not both.";
        }

        if (view is not null && !ViewFraming.NamedViews.ContainsKey(view))
        {
            return $"view: '{view}' is not a view; use 'front', 'back', '+x', '-x', '+y', '-y', '+z' or '-z'.";
        }

        if (direction is not null)
        {
            if (!Finite3(direction))
            {
                return "direction: must be three finite numbers [x, y, z].";
            }

            var length = Vector(direction).Length();
            if (!float.IsFinite(length) || length < 1e-20f)
            {
                return "direction: must not be zero, and must be a usable size.";
            }
        }

        if (!(zoom >= 1 && zoom <= 8))
        {
            return $"zoom: {Join([zoom])} is outside 1 to 8.";
        }

        if (target is not null && !Finite3(target))
        {
            return "target: must be three finite numbers [x, y, z].";
        }

        return null;
    }

    /// <summary>The entries of <c>views</c>; <paramref name="problem"/> is the first wrong one, by its place.</summary>
    private static List<Tile> Tiles(JsonElement views, out string? problem)
    {
        problem = null;
        if (views.ValueKind != JsonValueKind.Array)
        {
            problem = $"views: expected a list of 2 to 6 views, got {Describe(views)}";
            return [];
        }

        var count = views.GetArrayLength();
        if (count is < 2 or > 6)
        {
            problem = $"views: {count} given; a sheet takes 2 to 6.";
            return [];
        }

        var tiles = new List<Tile>();
        foreach (var entry in views.EnumerateArray())
        {
            var tile = Entry(entry, out var wrong);
            wrong ??= CameraRefusal(tile!.View, tile.Direction, tile.Zoom, tile.Target);
            if (wrong is not null)
            {
                problem = $"views: the {Ordinal(tiles.Count)} listed entry: {wrong}";
                return [];
            }

            tiles.Add(tile!);
        }

        return tiles;
    }

    private static Tile? Entry(JsonElement entry, out string? problem)
    {
        const string Shape = "expected a view name or an object with 'view' or 'direction' and optional 'zoom' and 'target'";
        problem = null;
        if (entry.ValueKind == JsonValueKind.String)
        {
            return new Tile(entry.GetString(), null, 1, null);
        }

        if (entry.ValueKind != JsonValueKind.Object)
        {
            problem = $"{Shape}, got {Describe(entry)}";
            return null;
        }

        foreach (var key in entry.EnumerateObject().Select(p => p.Name))
        {
            if (key is not ("view" or "direction" or "zoom" or "target"))
            {
                problem = $"{Shape}, got '{key}'";
                return null;
            }
        }

        JsonElement? Part(string name) => entry.TryGetProperty(name, out var part) && !Absent(part) ? part : null;
        var (view, direction, zoom, target) = (Part("view"), Part("direction"), Part("zoom"), Part("target"));
        if (view is null && direction is null)
        {
            problem = $"{Shape}, got neither 'view' nor 'direction'";
            return null;
        }

        if (view is { ValueKind: not JsonValueKind.String })
        {
            problem = $"view: expected a string, got {Describe(view.Value)}";
            return null;
        }

        if (zoom is { ValueKind: not JsonValueKind.Number })
        {
            problem = $"zoom: expected a number, got {Describe(zoom.Value)}";
            return null;
        }

        var numbers = (Direction: Numbers(direction), Target: Numbers(target));
        if (direction is not null && numbers.Direction is null)
        {
            problem = "direction: must be three finite numbers [x, y, z].";
            return null;
        }

        if (target is not null && numbers.Target is null)
        {
            problem = "target: must be three finite numbers [x, y, z].";
            return null;
        }

        return new Tile(view?.GetString(), numbers.Direction, zoom?.GetDouble() ?? 1, numbers.Target);
    }

    private static double[]? Numbers(JsonElement? value) =>
        value is { ValueKind: JsonValueKind.Array } list && list.EnumerateArray().All(x => x.ValueKind == JsonValueKind.Number)
            ? [.. list.EnumerateArray().Select(x => x.GetDouble())]
            : null;

    /// <summary>
    /// The size; <c>view front</c> for a view or <c>direction (x, y, z)</c> as given for a direction; then
    /// each thing asked for beyond the default, only where set: the projection, the mode, the labels, the
    /// highlight, zoom, target. The counts close it, so a part added here goes before them.
    /// </summary>
    internal static string Caption(RenderedFrame frame, string? view, double[]? direction, Projection projection, Look look, LabelSet labels, Highlight highlight, double zoom, double[]? target, BoneDraw? bones = null, string? weights = null, bool noUvs = false, string? part = null, int? level = null)
    {
        var parts = new List<string>
        {
            $"{frame.Pixels.Width}x{frame.Pixels.Height}",
            direction is null ? $"view {view ?? "front"}" : $"direction ({Join(direction)})",
        };

        AddLookParts(parts, projection, look, labels, highlight, bones, weights, noUvs);
        AddZoomAndTarget(parts, zoom, target);
        AddPartAndLevel(parts, part, level);
        return WithCounts(parts, frame);
    }

    /// <summary>
    /// <c>3x2 tiles of 256x256</c>, columns by rows; the parts every tile shares, as the single caption has them;
    /// then <c>views</c> and each tile's own caption, so a caption cut off at a tile's edge is whole here; the counts.
    /// </summary>
    private static string SheetCaption(RenderedFrame frame, int columns, IReadOnlyList<Tile> tiles, Projection projection, Look look, LabelSet labels, Highlight highlight, BoneDraw? bones = null, string? weights = null, bool noUvs = false, string? part = null, int? level = null)
    {
        var parts = new List<string> { $"{columns}x{SheetComposer.Rows(tiles.Count, columns)} tiles of {frame.Pixels.Width}x{frame.Pixels.Height}" };
        AddLookParts(parts, projection, look, labels, highlight, bones, weights, noUvs);
        parts.Add($"views {string.Join(", ", tiles.Select(t => t.Caption))}");
        AddPartAndLevel(parts, part, level);
        return WithCounts(parts, frame);
    }

    /// <summary>What is drawn, when not the whole live model (T-94): <c>part Axle_F, level 0</c>, before the counts.</summary>
    private static void AddPartAndLevel(List<string> parts, string? part, int? level)
    {
        if (part is not null) parts.Add($"part {part}");
        if (level is not null) parts.Add($"level {level}");
    }

    /// <summary>Why <c>part</c> cannot be drawn on <paramref name="model"/>, with <c>mesh</c> as given, or null.</summary>
    internal static string? PartRefusal(Model model, string part, string? mesh)
    {
        if (model.FindPart(part) is null)
            return model.Parts.Count == 0
                ? $"part: there is no part named '{part}'; the model has no parts yet (define_mesh or define_part makes one)"
                : $"part: there is no part named '{part}'; the parts are {model.ListPartNames()}";
        if (mesh is not null && model.FindPart(mesh) is not null && !model.SubtreeOf(part).Contains(mesh))
            return $"mesh: '{mesh}' is not drawn with part '{part}'; the parts under it are {NameList.Quoted(model.SubtreeOf(part))}";
        return null;
    }

    /// <summary>The one part in <paramref name="part"/>'s subtree with geometry of its own, or null when there are none or several.</summary>
    private static string? SoleOwner(Model model, string part) =>
        model.SubtreeOf(part).Where(n => model.FindPart(n)!.InstanceOf is null && model.Find(n) is not null).ToArray() is [var only] ? only : null;

    /// <summary>The caption's parts closed by the counts, joined.</summary>
    private static string WithCounts(List<string> parts, RenderedFrame frame)
    {
        parts.Add($"{frame.Vertices} vertices");
        parts.Add($"{frame.Faces} faces");
        return string.Join(", ", parts);
    }

    private static void AddLookParts(List<string> parts, Projection projection, Look look, LabelSet labels, Highlight highlight, BoneDraw? bones = null, string? weights = null, bool noUvs = false)
    {
        if (projection == Projection.Orthographic)
        {
            parts.Add("orthographic");
        }

        if (look == Look.Weights)
        {
            parts.Add($"weights {weights}");
        }
        else if (look != Look.Shaded)
        {
            parts.Add(look switch { Look.Wireframe => "wireframe", Look.Checker => "checker", _ => "normals" });
            if (look == Look.Checker && noUvs) parts.Add("no face has UVs");
        }

        if (labels != LabelSet.None)
        {
            parts.Add($"labels {labels.ToString().ToLowerInvariant()}");
        }

        if (!highlight.IsEmpty)
        {
            parts.Add($"highlight {highlight.Counts()}");
        }

        if (bones is not null)
        {
            parts.Add(bones.Labels ? "bones, bone labels" : "bones");
        }
    }

    internal static void AddZoomAndTarget(List<string> parts, double zoom, double[]? target)
    {
        if (zoom != 1)
        {
            parts.Add($"zoom {Join([zoom])}");
        }

        if (target is not null)
        {
            parts.Add($"target ({Join(target)})");
        }
    }

    /// <summary>The lists of a <c>highlight</c> as given, so that an error can name an entry by its place; empty when absent.</summary>
    private static (List<int> Vertices, List<(int A, int B)> Edges, List<int> Faces) HighlightLists(JsonElement? highlight, List<string> errors)
    {
        if (Absent(highlight))
        {
            return ([], [], []);
        }

        var value = highlight!.Value;
        if (value.ValueKind != JsonValueKind.Object)
        {
            errors.Add($"highlight: expected an object with any of vertices, edges and faces, got {Describe(value)}");
            return ([], [], []);
        }

        JsonElement? Part(string name) => value.TryGetProperty(name, out var part) ? part : null;
        return (Ids("highlight.vertices", Part("vertices"), errors) ?? [], Pairs("highlight.edges", Part("edges"), errors) ?? [], Ids("highlight.faces", Part("faces"), errors) ?? []);
    }

    private static bool Finite3(double[] v) => v.Length == 3 && v.All(double.IsFinite);

    /// <summary>Three numbers as given, narrowed to the mesh's own precision.</summary>
    internal static Vector3 Vector(double[] v) => new((float)v[0], (float)v[1], (float)v[2]);

    internal static string Join(IEnumerable<double> v) =>
        string.Join(", ", v.Select(x => x.ToString("G", CultureInfo.InvariantCulture)));

    // Interim until engine need 2: the engine encodes a PNG only into a file (WritePngAsync), so this
    // calls an encoder library directly, on the bytes that method would write.
    internal static byte[] EncodePng(byte[] pixels, int width, int height)
    {
        using var png = new MemoryStream();
        new ImageWriter().WritePng(pixels, width, height, ColorComponents.RedGreenBlueAlpha, png);
        return png.ToArray();
    }
}

/// <summary>T-94: render's part paragraph, public for the tools/list guard (RenderTool itself is internal).</summary>
public static class RenderPartTexts
{
    public const string Image = RenderTool.ImageLookText, ImageArgument = RenderTool.ImageArgumentText;

    public const string Parts = RenderTool.PartsText;
}
