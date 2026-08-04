public sealed class MergeSortedArraysGenerator : ProblemGenerator
{
    public override void Build(TestPlan plan)
    {
        plan.Edge("empty-and-single", 20, context => context.Ordinal switch
        {
            1 => Args(Array.Empty<int>(), new[] { 1 }),
            2 => Args(new[] { 1 }, Array.Empty<int>()),
            3 => Args(new[] { 1 }, new[] { 1 }),
            _ => Args(new[] { -10000 }, new[] { 10000 })
        });
        plan.Random("random-sorted-pairs", 450, context =>
        {
            var firstLength = context.Int(0, 60);
            var secondLength = context.Int(firstLength == 0 ? 1 : 0, 60);
            return Args(
                context.Arrays.Sorted(firstLength, -1000, 1000),
                context.Arrays.Sorted(secondLength, -1000, 1000));
        });
        plan.Adversarial("interleaved-ranges", 30, context =>
        {
            var count = context.Int(10, 40);
            var first = Enumerable.Range(0, count).Select(index => index * 2).ToArray();
            var second = Enumerable.Range(0, count).Select(index => index * 2 + 1).ToArray();
            return Args(first, second);
        });
    }
}
