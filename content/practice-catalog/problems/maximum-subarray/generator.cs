public sealed class MaximumSubarrayGenerator : ProblemGenerator
{
    public override void Build(TestPlan plan)
    {
        plan.Edge("edge-arrays", 20, context => context.Ordinal switch
        {
            1 => Args(new[] { 0 }),
            2 => Args(new[] { 10000 }),
            3 => Args(new[] { -10000 }),
            _ => Args(new[] { 2, -1, 2, -1, 2 })
        });
        plan.Random("random-arrays", 450, context =>
        {
            var length = context.Int(1, 60);
            return Args(context.Arrays.Int32(length, -1000, 1000));
        });
        plan.Adversarial("all-negative", 30, context =>
        {
            var length = context.Int(2, 60);
            return Args(context.Arrays.Int32(length, -10000, -1));
        });
    }
}
