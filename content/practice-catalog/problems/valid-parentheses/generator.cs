public sealed class ValidParenthesesGenerator : ProblemGenerator
{
    public override void Build(TestPlan plan)
    {
        var edge = new[] { "(", ")", "{}", "{[()]}" };
        plan.Edge("short-brackets", 20, context =>
            Args(edge[(context.Ordinal - 1) % edge.Length]));
        plan.Random("nested-or-broken", 450, context =>
        {
            var pairs = context.Int(1, 20);
            var valid = new string('(', pairs) + new string(')', pairs);
            if (context.Boolean())
                return Args(valid);
            return Args(valid[..^1] + "(");
        });
        var adversarial = new[] { "([{}])", "([{})]", "((((()))))", "([[[[]]]])}" };
        plan.Adversarial("mixed-nesting", 30, context =>
            Args(adversarial[(context.Ordinal - 1) % adversarial.Length]));
    }
}
