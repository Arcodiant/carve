namespace Carve.Core;

/// <summary>How an error or a result lists names: each in single quotes, joined by ", ".</summary>
public static class NameList
{
    /// <summary>The names as <c>'a', 'b', 'c'</c>.</summary>
    public static string Quoted(IEnumerable<string> names) => string.Join(", ", names.Select(n => $"'{n}'"));
}
