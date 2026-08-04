public sealed class TemplateGenerator : ProblemGenerator
{
    public override void Build(TestPlan plan)
    {
        plan.Random("template-placeholder", 450, context => Args(context.Int(0, 1)));
        plan.Edge("template-edge", 20, context => Args(context.Int(0, 1)));
        plan.Adversarial("template-adversarial", 30, context => Args(context.Int(0, 1)));
    }
}
