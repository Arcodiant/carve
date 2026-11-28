namespace Carve.Core.Scripts;

/// <summary>Why a script's text was refused, and where: 1-based line and column, the column counted in characters (UTF-16 units), not bytes.</summary>
public sealed record ScriptError(int Line, int Column, string Message)
{
    /// <summary><c>line 4, column 18: expected ':' after the argument name 'faces'</c>.</summary>
    public override string ToString() => $"line {Line}, column {Column}: {Message}";
}
