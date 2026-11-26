using System.Globalization;
using System.Numerics;

namespace Carve.Core;

/// <summary>The three kinds of element a query selects.</summary>
public enum ElementKind
{
    Vertices,
    Edges,
    Faces,
}

/// <summary>A parsed where-expression, ready for <see cref="MeshQuery.Select"/>. It holds no mesh and may be used on any.</summary>
public sealed class QueryExpression
{
    internal QueryExpression(MeshQuery.Node root, IReadOnlyList<MeshQuery.PropertyUse> uses)
    {
        Root = root;
        Uses = uses;
    }

    internal MeshQuery.Node Root { get; }
    internal IReadOnlyList<MeshQuery.PropertyUse> Uses { get; }
}

/// <summary>
/// The outcome of <see cref="MeshQuery.Parse"/>: an expression, or the errors that stopped it. <see cref="Errors"/> are the
/// ones found from the grammar alone. What a name means depends on the kind asked for, so <see cref="ErrorsFor"/> adds those.
/// </summary>
public sealed class QueryResult
{
    private readonly IReadOnlyList<MeshQuery.Problem> _problems;
    private readonly IReadOnlyList<MeshQuery.PropertyUse> _uses;

    internal QueryResult(QueryExpression? expression, IReadOnlyList<MeshQuery.Problem> problems, IReadOnlyList<MeshQuery.PropertyUse> uses)
    {
        Expression = expression;
        _problems = problems;
        _uses = uses;
        Errors = MeshQuery.Report(problems);
    }

    /// <summary>The expression, or null when the text does not follow the grammar.</summary>
    public QueryExpression? Expression { get; }

    /// <summary>Every grammar error, by character position, each with element <c>where</c>.</summary>
    public IReadOnlyList<MeshError> Errors { get; }

    public bool Succeeded => Expression is not null;

    /// <summary>Every error for a query on <paramref name="kind"/>: the grammar's and the names', by character position.</summary>
    public IReadOnlyList<MeshError> ErrorsFor(ElementKind kind) => MeshQuery.Report([.. _problems, .. MeshQuery.NameProblems(kind, _uses)]);
}

/// <summary>
/// What <see cref="MeshQuery.Select"/> matched: <see cref="Ids"/> for vertices and faces, <see cref="Edges"/> for edges, both
/// ascending (edges by low id, then high id), the other list empty. <see cref="Of"/> is how many elements of that kind the mesh has.
/// </summary>
public sealed record Selection(ElementKind Kind, IReadOnlyList<int> Ids, IReadOnlyList<(int Lo, int Hi)> Edges, int Of)
{
    public int Count => Kind == ElementKind.Edges ? Edges.Count : Ids.Count;
}

/// <summary>
/// The where-language of the <c>select</c> tool. <see cref="Parse"/> reads the text against the grammar, and
/// <see cref="Select"/> runs it on a mesh. Keywords and names are in any case and whitespace is free.
/// <code>
/// expr       := or
/// or         := and ( 'or' and )*
/// and        := not ( 'and' not )*
/// not        := 'not' not | atom
/// atom       := '(' expr ')' | 'all' | 'none' | flag | comparison
/// comparison := property op number
/// op         := '&lt;' | '&lt;=' | '==' | '!=' | '&gt;=' | '&gt;'
/// property   := name | name '.' ('x' | 'y' | 'z')
/// number     := a decimal, with an optional sign and exponent
/// </code>
/// Every comparison is made in float, both sides: the literal is narrowed as define_mesh narrows a coordinate, so
/// <c>y == 1.02</c> matches a vertex stored as <c>1.02f</c>.
/// </summary>
public static class MeshQuery
{
    /// <summary>
    /// How deep <c>not</c> and parentheses may nest. The parser and the evaluator recurse, and a stack overflow would end the
    /// whole process, so the text cannot be allowed to choose the depth.
    /// </summary>
    public const int MaxDepth = 200;

    private const string Operators = "<, <=, ==, !=, >= or >";
    private const string Axes = "use .x, .y or .z";

    /// <summary>Reads <paramref name="where"/> against the grammar, listing every error it can find rather than the first.</summary>
    public static QueryResult Parse(string where)
    {
        if (string.IsNullOrWhiteSpace(where)) return new QueryResult(null, [new Problem(0, "where is empty")], []);
        return new Parser(where).Run();
    }

    /// <summary>
    /// The elements of <paramref name="kind"/> that match, ascending. The expression must be valid for the kind, which
    /// <see cref="QueryResult.ErrorsFor"/> says; a name the kind does not have is an <see cref="ArgumentException"/>.
    /// </summary>
    public static Selection Select(PolygonMesh mesh, ElementKind kind, QueryExpression expression)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(expression);
        var problems = NameProblems(kind, expression.Uses);
        if (problems.Count > 0) throw new ArgumentException(string.Join("; ", Report(problems)), nameof(expression));

        var table = Tables[kind];
        var matches = Compile(expression.Root, table, new Facts(mesh));
        var slots = table.Count(mesh);
        bool Deleted(int i) => kind == ElementKind.Vertices ? mesh.IsVertexDeleted(i) : kind == ElementKind.Faces && mesh.IsFaceDeleted(i);
        var ids = new List<int>();
        var of = 0;
        for (var i = 0; i < slots; i++)
        {
            if (Deleted(i)) continue; // a tombstone is never a row
            of++;
            if (matches(i)) ids.Add(i);
        }

        // Topology.Edges is ordered by (Lo, Hi), so ascending positions are ascending pairs.
        return kind == ElementKind.Edges
            ? new Selection(kind, [], ids.Select(i => (mesh.Topology.Edges[i].Lo, mesh.Topology.Edges[i].Hi)).ToArray(), of)
            : new Selection(kind, ids, [], of);
    }

    internal readonly record struct Problem(int Position, string Message);

    internal static IReadOnlyList<MeshError> Report(IEnumerable<Problem> problems) =>
        problems.OrderBy(p => p.Position)
            .Select(p => new MeshError("where", p.Position == 0 ? p.Message : $"at character {p.Position}: {p.Message}"))
            .ToArray();

    /// <summary>What the names mean for a kind; found here rather than in the parser, which has no kind.</summary>
    internal static List<Problem> NameProblems(ElementKind kind, IEnumerable<PropertyUse> uses)
    {
        var table = Tables[kind];
        var problems = new List<Problem>();
        foreach (var use in uses)
        {
            var hasAxes = table.Numbers.ContainsKey(use.Name + ".x");
            var known = hasAxes || table.Numbers.ContainsKey(use.Name) || table.Flags.ContainsKey(use.Name);
            if (!known || (!hasAxes && use.Axis is not null))
            {
                var typed = use.Axis is null ? use.TypedName : $"{use.TypedName}.{use.TypedAxis}";
                problems.Add(new Problem(use.Position, $"'{typed}' is not a property of {table.Plural}; {table.Plural} have {table.Names}"));
            }
            else if (hasAxes && use.Axis is null)
            {
                problems.Add(new Problem(use.Position, $"'{use.TypedName}' needs an axis; {Axes}"));
            }
            else if (hasAxes && use.Axis is not ("x" or "y" or "z"))
            {
                problems.Add(new Problem(use.AxisPosition, $"'{use.TypedName}.{use.TypedAxis}' is not an axis; {Axes}"));
            }
            else if (table.Flags.ContainsKey(use.Name) && use.Compared)
            {
                problems.Add(new Problem(use.Position, "a flag cannot be compared with a number"));
            }
            else if (table.Numbers.ContainsKey(use.Key) && !use.Compared)
            {
                var typed = use.Axis is null ? use.TypedName : $"{use.TypedName}.{use.TypedAxis}";
                problems.Add(new Problem(use.Position, $"'{typed}' is a number; compare it with {Operators}"));
            }
        }
        return problems;
    }

    internal enum Op
    {
        Less,
        LessOrEqual,
        Equal,
        NotEqual,
        GreaterOrEqual,
        Greater,
    }

    internal abstract record Node;

    private sealed record Constant(bool Value) : Node;

    private sealed record Negation(Node Inner) : Node;

    private sealed record AnyOf(Node[] Parts) : Node;

    private sealed record EveryOf(Node[] Parts) : Node;

    /// <summary>A name in the text, a flag or a comparison, with where it stands. Which of the two it may be depends on the kind.</summary>
    internal sealed record PropertyUse(string TypedName, string? TypedAxis, int Position, int AxisPosition, bool Compared, Op Op, float Number) : Node
    {
        public string Name { get; } = TypedName.ToLowerInvariant();
        public string? Axis { get; } = TypedAxis?.ToLowerInvariant();
        public string Key => Axis is null ? Name : $"{Name}.{Axis}";
    }

    private static Func<int, bool> Compile(Node node, Table table, Facts facts)
    {
        switch (node)
        {
            case Constant constant:
                return _ => constant.Value;
            case Negation negation:
                var inner = Compile(negation.Inner, table, facts);
                return i => !inner(i);
            case AnyOf any:
                var anyParts = any.Parts.Select(p => Compile(p, table, facts)).ToArray();
                return i => anyParts.Any(p => p(i));
            case EveryOf every:
                var everyParts = every.Parts.Select(p => Compile(p, table, facts)).ToArray();
                return i => everyParts.All(p => p(i));
            case PropertyUse { Compared: true } comparison:
                var number = table.Numbers[comparison.Key];
                return i => Compare(number(facts, i), comparison.Op, comparison.Number);
            case PropertyUse use:
                var flag = table.Flags[use.Key];
                return i => flag(facts, i);
            default:
                throw new InvalidOperationException($"not a query node: {node}");
        }
    }

    /// <summary>Both sides are floats by now; nothing here may widen them.</summary>
    private static bool Compare(float left, Op op, float right) => op switch
    {
        Op.Less => left < right,
        Op.LessOrEqual => left <= right,
        Op.Equal => left == right,
        Op.NotEqual => left != right,
        Op.GreaterOrEqual => left >= right,
        Op.Greater => left > right,
        _ => throw new InvalidOperationException($"not an operator: {op}"),
    };

    /// <summary>What one query on one mesh works out once rather than per element, and only if the query asks.</summary>
    private sealed class Facts(PolygonMesh mesh)
    {
        private int[]? _edgesAtVertex;
        private bool[]? _boundary;
        private HashSet<string>? _nonPlanar;

        public PolygonMesh Mesh { get; } = mesh;

        public MeshEdge Edge(int i) => Mesh.Topology.Edges[i];

        public IEnumerable<Vector3> Corners(int face) => Mesh.Faces[face].Corners.Select(v => Mesh.Vertices[v]);

        public int EdgesAt(int vertex)
        {
            if (_edgesAtVertex is null)
            {
                var counts = new int[Mesh.Vertices.Count];
                foreach (var edge in Mesh.Topology.Edges)
                {
                    counts[edge.Lo]++;
                    counts[edge.Hi]++;
                }
                _edgesAtVertex = counts;
            }
            return _edgesAtVertex[vertex];
        }

        public bool OnOpenEdge(int vertex)
        {
            if (_boundary is null)
            {
                var boundary = new bool[Mesh.Vertices.Count];
                foreach (var edge in Mesh.Topology.Edges.Where(e => e.Faces.Count == 1))
                    boundary[edge.Lo] = boundary[edge.Hi] = true;
                _boundary = boundary;
            }
            return _boundary[vertex];
        }

        /// <summary>The rule is MeshReport's and is asked of it, so a face is non-planar here exactly when the report measures it so (smooth or flat).</summary>
        public bool NonPlanar(int face)
        {
            _nonPlanar ??= MeshReport.Create(Mesh).NonPlanarFaces.Select(w => w.Element).ToHashSet();
            return _nonPlanar.Contains($"faces[{face}]");
        }
    }

    /// <summary>One kind's properties, keyed by lower-case name (<c>normal.x</c> for an axis). <see cref="Names"/> is how errors list them.</summary>
    private sealed record Table(string Plural, string Names, Func<PolygonMesh, int> Count,
        Dictionary<string, Func<Facts, int, float>> Numbers, Dictionary<string, Func<Facts, int, bool>> Flags);

    private static readonly Dictionary<ElementKind, Table> Tables = new()
    {
        [ElementKind.Vertices] = new Table("vertices", "id, x, y, z, min, max, faces, edges, unattached, boundary", m => m.Vertices.Count,
            Numbers(
                (f, i) => f.Mesh.Vertices[i], (f, i) => f.Mesh.Vertices[i], (f, i) => f.Mesh.Vertices[i],
                new()
                {
                    ["id"] = (_, i) => i,
                    ["faces"] = (f, i) => f.Mesh.Topology.VertexFaces(i).Count,
                    ["edges"] = (f, i) => f.EdgesAt(i),
                }),
            new()
            {
                ["unattached"] = (f, i) => f.Mesh.Topology.VertexFaces(i).Count == 0,
                ["boundary"] = (f, i) => f.OnOpenEdge(i),
            }),

        [ElementKind.Edges] = new Table("edges", "x, y, z, min, max, a, b, length, faces, open, manifold, nonmanifold", m => m.Topology.Edges.Count,
            Numbers(
                (f, i) => Midpoint(f.Mesh.Vertices[f.Edge(i).Lo], f.Mesh.Vertices[f.Edge(i).Hi]),
                (f, i) => Vector3.Min(f.Mesh.Vertices[f.Edge(i).Lo], f.Mesh.Vertices[f.Edge(i).Hi]),
                (f, i) => Vector3.Max(f.Mesh.Vertices[f.Edge(i).Lo], f.Mesh.Vertices[f.Edge(i).Hi]),
                new()
                {
                    ["a"] = (f, i) => f.Edge(i).Lo,
                    ["b"] = (f, i) => f.Edge(i).Hi,
                    ["length"] = (f, i) => Length(f.Mesh.Vertices[f.Edge(i).Lo], f.Mesh.Vertices[f.Edge(i).Hi]),
                    ["faces"] = (f, i) => f.Edge(i).Faces.Count,
                }),
            new()
            {
                ["open"] = (f, i) => f.Edge(i).Faces.Count == 1,
                ["manifold"] = (f, i) => f.Edge(i).IsManifold,
                ["nonmanifold"] = (f, i) => f.Edge(i).Faces.Count > 2,
            }),

        [ElementKind.Faces] = new Table("faces", "id, x, y, z, min, max, normal, vertices, area, non_planar, open", m => m.Faces.Count,
            Numbers(
                (f, i) => f.Mesh.FaceCentre(i),
                (f, i) => f.Corners(i).Aggregate(Vector3.Min),
                (f, i) => f.Corners(i).Aggregate(Vector3.Max),
                new()
                {
                    ["id"] = (_, i) => i,
                    ["normal.x"] = (f, i) => f.Mesh.FaceNormal(i).X,
                    ["normal.y"] = (f, i) => f.Mesh.FaceNormal(i).Y,
                    ["normal.z"] = (f, i) => f.Mesh.FaceNormal(i).Z,
                    ["vertices"] = (f, i) => f.Mesh.Faces[i].Corners.Count,
                    ["area"] = (f, i) => (float)f.Mesh.FaceArea(i),
                }),
            new()
            {
                ["non_planar"] = (f, i) => f.NonPlanar(i),
                ["open"] = (f, i) => f.Mesh.Topology.FaceEdges(i).Any(e => e.Faces.Count == 1),
            }),
    };

    /// <summary>The numbers every kind has, from its representative point and extent, added to the kind's own.</summary>
    private static Dictionary<string, Func<Facts, int, float>> Numbers(Func<Facts, int, Vector3> point, Func<Facts, int, Vector3> min, Func<Facts, int, Vector3> max,
        Dictionary<string, Func<Facts, int, float>> own)
    {
        own["x"] = (f, i) => point(f, i).X;
        own["y"] = (f, i) => point(f, i).Y;
        own["z"] = (f, i) => point(f, i).Z;
        own["min.x"] = (f, i) => min(f, i).X;
        own["min.y"] = (f, i) => min(f, i).Y;
        own["min.z"] = (f, i) => min(f, i).Z;
        own["max.x"] = (f, i) => max(f, i).X;
        own["max.y"] = (f, i) => max(f, i).Y;
        own["max.z"] = (f, i) => max(f, i).Z;
        return own;
    }

    /// <summary>In double, so two large coordinates cannot overflow on the way, and narrowed once.</summary>
    private static Vector3 Midpoint(Vector3 a, Vector3 b) =>
        new((float)(((double)a.X + b.X) / 2), (float)(((double)a.Y + b.Y) / 2), (float)(((double)a.Z + b.Z) / 2));

    private static float Length(Vector3 a, Vector3 b) =>
        (float)PolygonMesh.Length(((double)a.X - b.X, (double)a.Y - b.Y, (double)a.Z - b.Z));

    private enum TokenKind
    {
        Word,
        Number,
        Operator,
        Open,
        Close,
        Dot,
        Bad,
        End,
    }

    private readonly record struct Token(TokenKind Kind, string Text, int Position);

    private static bool IsDigit(char c) => c is >= '0' and <= '9';

    private static bool IsWordStart(char c) => c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or '_';

    private static List<Token> Tokens(string text)
    {
        var tokens = new List<Token>();
        var i = 0;
        char At(int index) => index < text.Length ? text[index] : '\0';
        bool NumberAt(int index) => IsDigit(At(index)) || (At(index) == '.' && IsDigit(At(index + 1)));

        while (i < text.Length)
        {
            var c = text[i];
            var start = i;
            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            // A dot straight after a word is the axis's, whatever follows it: normal.5 is a bad axis, not a word and a number.
            var afterWord = tokens.Count > 0 && tokens[^1].Kind == TokenKind.Word && tokens[^1].Position - 1 + tokens[^1].Text.Length == i;
            TokenKind kind;
            if (IsWordStart(c))
            {
                while (IsWordStart(At(i)) || IsDigit(At(i))) i++;
                kind = TokenKind.Word;
            }
            else if (!(c == '.' && afterWord) && (NumberAt(i) || (c is '+' or '-' && NumberAt(i + 1))))
            {
                if (c is '+' or '-') i++;
                while (IsDigit(At(i))) i++;
                if (At(i) == '.')
                {
                    i++;
                    while (IsDigit(At(i))) i++;
                }
                var mantissa = At(i + 1) is '+' or '-' ? 2 : 1;
                if (At(i) is 'e' or 'E' && IsDigit(At(i + mantissa)))
                {
                    i += mantissa;
                    while (IsDigit(At(i))) i++;
                }
                kind = TokenKind.Number;
            }
            else if (c is '<' or '>' or '=' or '!')
            {
                while (At(i) is '<' or '>' or '=' or '!') i++;
                kind = TokenKind.Operator;
            }
            else
            {
                i++;
                kind = c switch
                {
                    '(' => TokenKind.Open,
                    ')' => TokenKind.Close,
                    '.' => TokenKind.Dot,
                    _ => TokenKind.Bad,
                };
            }
            tokens.Add(new Token(kind, text[start..i], start + 1));
        }

        tokens.Add(new Token(TokenKind.End, "", text.Length + 1));
        return tokens;
    }

    /// <summary>
    /// Recursive descent over the grammar. On an error it notes the problem, steps past the token at fault and goes on, so
    /// that one call reports everything; what it builds after an error is thrown away.
    /// </summary>
    private sealed class Parser(string text)
    {
        private readonly List<Token> _tokens = Tokens(text);
        private readonly List<Problem> _problems = [];
        private readonly List<PropertyUse> _uses = [];
        private int _next;
        private int _depth;
        private bool _stopped;

        private Token Peek => _tokens[_next];

        private bool AtWord(string word) => Peek.Kind == TokenKind.Word && Peek.Text.Equals(word, StringComparison.OrdinalIgnoreCase);

        private bool AtConnective => AtWord("and") || AtWord("or");

        private void Advance()
        {
            if (Peek.Kind != TokenKind.End) _next++;
        }

        /// <summary>
        /// Notes a problem. The same one again at the same place is the first one seen on the way out of a rule; a
        /// different one there, such as a missing ')' after a missing number, is listed too.
        /// </summary>
        private void Fail(int position, string message)
        {
            if (_stopped || (_problems.Count > 0 && _problems[^1].Position == position && _problems[^1].Message == message)) return;
            _problems.Add(new Problem(position, message));
        }

        public QueryResult Run()
        {
            var root = ParseOr();
            while (Peek.Kind != TokenKind.End)
            {
                // A token that already failed where an atom was expected, such as a stray ')', is not reported twice.
                if (_problems.Count == 0 || _problems[^1].Position != Peek.Position)
                    Fail(Peek.Position, "expected 'and', 'or' or the end of the expression");
                while (Peek.Kind is not (TokenKind.End or TokenKind.Word or TokenKind.Open)) Advance();
                if (Peek.Kind != TokenKind.End) ParseOr();
            }
            return new QueryResult(_problems.Count == 0 ? new QueryExpression(root, _uses) : null, _problems, _uses);
        }

        private Node ParseOr()
        {
            var parts = new List<Node> { ParseAnd() };
            while (AtWord("or"))
            {
                Advance();
                parts.Add(ParseAnd());
            }
            return parts.Count == 1 ? parts[0] : new AnyOf([.. parts]);
        }

        private Node ParseAnd()
        {
            var parts = new List<Node> { ParseNot() };
            while (AtWord("and"))
            {
                Advance();
                parts.Add(ParseNot());
            }
            return parts.Count == 1 ? parts[0] : new EveryOf([.. parts]);
        }

        private Node ParseNot()
        {
            if (!AtWord("not")) return ParseAtom();
            if (!Enter()) return new Constant(false);
            Advance();
            var inner = ParseNot();
            _depth--;
            return new Negation(inner);
        }

        /// <summary>One level deeper, or the end of the parse: past the limit there is nothing sound left to read.</summary>
        private bool Enter()
        {
            if (++_depth <= MaxDepth) return true;
            Fail(Peek.Position, $"nested too deeply; 'not' and '(' go {MaxDepth} levels deep at most");
            _stopped = true;
            _next = _tokens.Count - 1;
            return false;
        }

        private Node ParseAtom()
        {
            if (Peek.Kind == TokenKind.Open)
            {
                if (!Enter()) return new Constant(false);
                Advance();
                var inner = ParseOr();
                if (Peek.Kind == TokenKind.Close) Advance();
                else Fail(Peek.Position, "expected ')'");
                _depth--;
                return inner;
            }

            if (AtWord("all") || AtWord("none"))
            {
                var value = AtWord("all");
                Advance();
                return new Constant(value);
            }

            if (Peek.Kind == TokenKind.Word && !AtConnective) return ParseProperty();

            Fail(Peek.Position, "expected a property, a flag, all, none, not or '('");
            if (Peek.Kind != TokenKind.Close && !AtConnective) Advance();
            return new Constant(false);
        }

        private Node ParseProperty()
        {
            var name = Peek;
            Advance();

            string? axis = null;
            var axisPosition = 0;
            if (Peek.Kind == TokenKind.Dot)
            {
                Advance();
                axisPosition = Peek.Position;
                if (Peek.Kind is TokenKind.Word or TokenKind.Number)
                {
                    axis = Peek.Text;
                    Advance();
                }
            }

            var compared = false;
            var op = Op.Equal;
            var number = 0f;
            if (Peek.Kind == TokenKind.Operator)
            {
                compared = true;
                op = ParseOperator();
                number = ParseNumber();
            }

            var use = new PropertyUse(name.Text, axis, name.Position, axisPosition, compared, op, number);
            _uses.Add(use);
            return use;
        }

        private Op ParseOperator()
        {
            var token = Peek;
            Advance();
            switch (token.Text)
            {
                case "<": return Op.Less;
                case "<=": return Op.LessOrEqual;
                case "==": return Op.Equal;
                case "!=": return Op.NotEqual;
                case ">=": return Op.GreaterOrEqual;
                case ">": return Op.Greater;
                default:
                    Fail(token.Position, $"'{token.Text}' is not an operator; use {Operators}");
                    return Op.Equal;
            }
        }

        private float ParseNumber()
        {
            var token = Peek;
            if (token.Kind != TokenKind.Number)
            {
                Fail(token.Position, "expected a number");
                if (token.Kind is not (TokenKind.Open or TokenKind.Close or TokenKind.End) && !AtConnective) Advance();
                return 0;
            }

            Advance();
            // Through double, as a JSON coordinate reaches a float in define_mesh, so a literal and a stored value round alike.
            var value = (float)double.Parse(token.Text, NumberStyles.Float, CultureInfo.InvariantCulture);
            if (!float.IsFinite(value))
                Fail(token.Position, $"{token.Text} is out of range: a number must lie within ±{float.MaxValue.ToString("G9", CultureInfo.InvariantCulture)}");
            return value;
        }
    }
}
