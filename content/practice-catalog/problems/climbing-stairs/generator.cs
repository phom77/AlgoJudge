public sealed class ClimbingStairsGenerator : ProblemGenerator
{
    public override void Build(TestPlan plan)
    {
        var edge = new[] { 1, 2, 3, 45 };
        plan.Edge("boundary-steps", 20, context =>
            Args(edge[(context.Ordinal - 1) % edge.Length]));
        plan.Random("random-steps", 450, context => Args(context.Int(1, 45)));
        var adversarial = new[] { 10, 20, 30, 44 };
        plan.Adversarial("large-steps", 30, context =>
            Args(adversarial[(context.Ordinal - 1) % adversarial.Length]));
    }
}
