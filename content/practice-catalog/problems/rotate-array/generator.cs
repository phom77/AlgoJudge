public sealed class RotateArrayGenerator : ProblemGenerator
{
    public override void Build(TestPlan plan)
    {
        plan.Edge("rotation-boundaries", 20, context => context.Ordinal switch
        {
            1 => Args(new[] { 1 }, 0),
            2 => Args(new[] { 1 }, 1000),
            3 => Args(new[] { 1, 2, 3 }, 3),
            _ => Args(new[] { 1, 2, 3 }, 4)
        });
        plan.Random("random-rotations", 450, context =>
        {
            var length = context.Int(1, 80);
            return Args(context.Arrays.Int32(length, -1000, 1000), context.Int(0, 1000));
        });
        plan.Adversarial("large-k", 30, context =>
        {
            var length = context.Int(20, 80);
            return Args(context.Permutations.Generate(length, -40), 1000 - context.Int(0, 10));
        });
    }
}
