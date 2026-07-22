namespace AlgoJudge.ProblemGeneratorSdk;

public sealed class GraphGenerator
{
    private readonly DeterministicRandom _random;

    internal GraphGenerator(DeterministicRandom random)
    {
        _random = random;
    }

    public GeneratedGraph Tree(int vertexCount)
    {
        EnsureVertexCount(vertexCount);
        var edges = new List<GraphEdge>(Math.Max(0, vertexCount - 1));
        for (var vertex = 1; vertex < vertexCount; vertex++)
            edges.Add(new GraphEdge(_random.NextInt32(0, vertex - 1), vertex));
        _random.Shuffle(edges);
        return new GeneratedGraph(vertexCount, edges);
    }

    public GeneratedGraph Dag(int vertexCount, int edgeCount)
    {
        EnsureVertexCount(vertexCount);
        var maximum = (long)vertexCount * (vertexCount - 1) / 2;
        if (edgeCount < 0 || edgeCount > maximum)
            throw new ArgumentOutOfRangeException(nameof(edgeCount));

        var selected = new HashSet<(int From, int To)>();
        var edges = new List<GraphEdge>(edgeCount);
        while (edges.Count < edgeCount)
        {
            var from = _random.NextInt32(0, vertexCount - 1);
            var to = _random.NextInt32(0, vertexCount - 1);
            if (from == to)
                continue;
            var normalized = Normalize(from, to);
            if (!selected.Add(normalized))
                continue;
            edges.Add(new GraphEdge(normalized.From, normalized.To));
        }
        return new GeneratedGraph(vertexCount, edges);
    }

    public GeneratedGraph Connected(int vertexCount, int extraEdgeCount = 0)
    {
        var tree = Tree(vertexCount);
        var edges = tree.Edges.ToList();
        var existing = edges
            .Select(edge => Normalize(edge.From, edge.To))
            .ToHashSet();
        var maximumExtra = (long)vertexCount * (vertexCount - 1) / 2 - edges.Count;
        if (extraEdgeCount < 0 || extraEdgeCount > maximumExtra)
            throw new ArgumentOutOfRangeException(nameof(extraEdgeCount));

        while (extraEdgeCount > 0)
        {
            var from = _random.NextInt32(0, vertexCount - 1);
            var to = _random.NextInt32(0, vertexCount - 1);
            if (from == to || !existing.Add(Normalize(from, to)))
                continue;
            edges.Add(new GraphEdge(from, to));
            extraEdgeCount--;
        }

        _random.Shuffle(edges);
        return new GeneratedGraph(vertexCount, edges);
    }

    private static (int From, int To) Normalize(int from, int to) =>
        from < to ? (from, to) : (to, from);

    private static void EnsureVertexCount(int vertexCount)
    {
        if (vertexCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(vertexCount));
    }
}
