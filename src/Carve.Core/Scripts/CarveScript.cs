namespace Carve.Core.Scripts;

/// <summary>A script: its statements in order. <see cref="ScriptReader"/> reads one, <see cref="ScriptWriter"/> writes one.</summary>
public sealed record CarveScript(IReadOnlyList<ScriptStatement> Statements);
