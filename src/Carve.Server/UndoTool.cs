using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using static Carve.Server.MeshToolSupport;

namespace Carve.Server;

public sealed class UndoTool(ModelStore store)
{
    public const string Text =
        "Steps the model back one change: the last define_mesh, open, move or extrude is taken back and the mesh before it is current again, with the ids it had. "
        + "The ids the undone step created are free again and the next edit gives them out to new vertices and faces, so forget any you kept from its report. "
        + "Takes no arguments; any argument is a tool error. The last 100 changes can be undone, one call each; there is no redo, and an undo cannot itself be undone. "
        + "A call that failed was never recorded, so it is not a step. "
        + "Returns the same JSON as define_mesh for the mesh now current ({vertices, faces, edges, closed, bounds:{min,max}, warnings:[]}; while the model has several meshes, {meshes:[{name, and that report}], in model order}) "
        + "plus undone, the name of the tool whose change was taken back, e.g. \"extrude\". "
        + "open is a step like define_mesh: undo takes it back to the mesh before it (undone is \"open\"). The first mesh a process has, whether from define_mesh or from open, has nothing before it, so undo straight after an open that gave the process its first mesh is a tool error, while after an open in a process that had a mesh before, undo works. Past 100 changes the oldest drop off and undo stops at whatever is then the oldest. "
        + "When there is nothing older to go back to it is a tool error saying so. "
        + "Worked example, straight after an extrude (returns undone \"extrude\"):\n"
        + ExampleMarker + "{}";

    public static McpServerTool Create() => MeshToolSupport.Create<UndoTool>("undo", Text, null);

    public CallToolResult Run(RequestContext<CallToolRequestParams> context)
    {
        if (UnknownArguments("undo", context) is { Count: > 0 } unknown) return Failed("undo", unknown);
        var undo = store.Undo();
        if (undo is null) return Error("undo: nothing to undo; there is no older state of the mesh");
        var model = undo.Current.Model;
        var body = model.Meshes.Count == 1
            ? Report(model.Meshes[0].Mesh)
            : new Dictionary<string, object>
            {
                ["meshes"] = model.Meshes.Select(m =>
                {
                    var one = new Dictionary<string, object> { ["name"] = m.Name };
                    foreach (var (k, v) in Report(m.Mesh)) one[k] = v;
                    return one;
                }).ToArray(),
            };
        body["undone"] = undo.Undone;
        return Ok(body);
    }
}
