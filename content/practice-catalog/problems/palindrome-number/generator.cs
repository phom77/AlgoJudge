public sealed class PalindromeNumberGenerator : ProblemGenerator
{
    public override void Build(TestPlan plan)
    {
        var edge = new[] { -1000000000, -1, 0, 1000000000 };
        plan.Edge("integer-boundaries", 20, context =>
            Args(edge[(context.Ordinal - 1) % edge.Length]));
        plan.Random("random-integers", 450, context =>
            Args(context.Int(-1000000000, 1000000000)));
        var adversarial = new[] { 1001, 10001, 1234321, 12345321 };
        plan.Adversarial("matching-outer-digits", 30, context =>
            Args(adversarial[(context.Ordinal - 1) % adversarial.Length]));
    }
}
