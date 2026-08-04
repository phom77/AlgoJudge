public sealed class LongestCommonPrefixGenerator : ProblemGenerator
{
    public override void Build(TestPlan plan)
    {
        var edge = new[]
        {
            new[] { "" },
            new[] { "a" },
            new[] { "", "abc" },
            new[] { "same", "same", "same" }
        };
        plan.Edge("small-word-lists", 20, context =>
            Args((object)edge[(context.Ordinal - 1) % edge.Length]));
        plan.Random("random-prefixes", 450, context =>
        {
            var prefix = context.Strings.Random(context.Int(0, 6), "abc");
            var count = context.Int(2, 10);
            var words = Enumerable.Range(0, count)
                .Select(_ => prefix + context.Strings.Random(context.Int(1, 10), "defgh"))
                .ToArray();
            return Args((object)words);
        });
        var adversarial = new[]
        {
            new[] { "aaaaaaaaab", "aaaaaaaaac", "aaaaaaaaad" },
            new[] { "prefix", "pre", "prevent" },
            new[] { "zebra", "zen", "zero" },
            new[] { "abc", "xbc", "ybc" }
        };
        plan.Adversarial("late-mismatch", 30, context =>
            Args((object)adversarial[(context.Ordinal - 1) % adversarial.Length]));
    }
}
