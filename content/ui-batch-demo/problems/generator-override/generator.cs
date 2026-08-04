public sealed class OverrideIdentityGenerator : ProblemGenerator
{
    public override void Build(TestPlan plan)
    {
        plan.Random("override-random", 4, context => Args(context.Int(50, 100)));
    }
}
