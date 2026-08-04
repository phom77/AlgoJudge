public sealed class IdentityGenerator : ProblemGenerator
{
    public override void Build(TestPlan plan)
    {
        plan.Random("random-values", 4, context => Args(context.Int(1, 100)));
    }
}
