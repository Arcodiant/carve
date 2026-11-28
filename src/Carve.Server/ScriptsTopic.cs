namespace Carve.Server;

/// <summary>help {"topic":"scripts"} (M-34, T-232): the .carvescript format and what save_script writes. and how run_script (T-233) runs one.</summary>
public static class ScriptsTopic
{
    public const string Text =
        "A .carvescript is the calls of a session, one statement each, as text a person or an agent can read and edit. "
        + "Grammar: tool(name: <json>, name: <json>), each value one JSON value exactly as the MCP call took it (numbers keep their text: 0.1, -0, 1e3 are written as sent); "
        + "tool and argument names are [a-z_][a-z0-9_]*; a statement is on one line or spread over several (save_script wraps a statement over 120 characters one argument a line, and a list of lists one item a line); "
        + "there are no comments, variables or loops; line ends are LF (CRLF is read too). A malformed statement is refused with its line and column.\n"
        + "What is written: the calls that made an undo step, in order, as called: define_mesh, every edit, define_*, set_*, open, clear and the rest that change the model; never save, export, bake, check, render, help or a query, and never save_script. "
        + "undo takes the call it undid out of the list. clear and a whole-model open start the list again, so a script written after one starts with it; a fresh session's script starts with no clear(). The list is this session's only: it is not in the .carve file and save does not touch it.\n"
        + "Paths: each file a call read (open path, define_target file, define_material's texture fields and their graph, displace's graph) is written as the path from the script's folder to the file, with / separators, so the script and its files can move together; "
        + "a file with no such path (on another drive) refuses the whole save, naming the call, its place in the list and the argument, and nothing is written. "
        + "Ids are literal: a statement names the faces and vertices the call named, and an earlier edit that changes the topology (extrude, loop_cut, delete) shifts the ids a later statement means, so edit a script from the end, or check the ids after each change.\n"
        + "Running: run_script {\"path\":\"x.carvescript\"} runs the statements on the current model, in order, each as a direct call of its tool with the same checks; a script starting with clear() or open(...) rebuilds the model, one that does not adds to it. "
        + "A relative file argument resolves against the script's folder. Any tool that changes the model may be a statement; run_script, undo, save, save_script, export, bake, render, render_glb, select, check, help and the get_ tools are refused before anything runs. "
        + "It is all or nothing: a malformed statement (line and column) or a statement its tool refuses (line, tool and the tool's message) leaves the model, undo history and this list as they were. "
        + "A run is one undo step, undone as \"run_script\", and adds the script's statements to the list (a clear() or whole-model open in it starts the list again there), so explore with calls, then save_script to keep the steps, edit, clear and run again.\n"
        + "Example, define_mesh's unit cube then extrude of face 1, written by save_script {\"path\":\"cube.carvescript\"}:\n"
        + SaveScriptTool.WorkedFile;
}
