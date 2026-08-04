public sealed class SingleNumberGenerator : ProblemGenerator
{
    public override void Build(TestPlan plan)
    {
        var edge = new[]
        {
            new[] { 7 },
            new[] { 2, 2, 1 },
            new[] { -1, 0, -1 },
            new[] { 4, 1, 2, 1, 2 }
        };
        plan.Edge("small-valid-arrays", 20, context =>
            Args(edge[(context.Ordinal - 1) % edge.Length]));
        plan.Random("shuffled-pairs", 450, context =>
            Args(CreateCase(context, context.Int(1, 40), -5000)));
        plan.Adversarial("maximum-length", 30, context =>
            Args(CreateCase(context, 49, 9000)));
    }

    private static int[] CreateCase(
        GenerationContext context,
        int pairCount,
        int start)
    {
        var distinctValues = context.Permutations.Generate(pairCount + 1, start);
        var values = new List<int> { distinctValues[0] };
        for (var index = 1; index <= pairCount; index++)
        {
            values.Add(distinctValues[index]);
            values.Add(distinctValues[index]);
        }

        var order = context.Permutations.Generate(values.Count);
        return order.Select(index => values[index]).ToArray();
    }
}
