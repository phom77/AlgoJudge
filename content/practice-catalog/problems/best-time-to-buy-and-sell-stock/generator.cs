public sealed class StockProfitGenerator : ProblemGenerator
{
    public override void Build(TestPlan plan)
    {
        plan.Edge("price-boundaries", 20, context => context.Ordinal switch
        {
            1 => Args(new[] { 0 }),
            2 => Args(new[] { 0, 10000 }),
            3 => Args(new[] { 10000, 0 }),
            _ => Args(new[] { 5, 5, 5, 5 })
        });
        plan.Random("random-prices", 450, context =>
            Args(context.Arrays.Int32(context.Int(1, 80), 0, 10000)));
        plan.Adversarial("many-local-gains", 30, context =>
        {
            var length = context.Int(10, 40);
            var values = new int[length];
            for (var i = 0; i < length; ++i)
                values[i] = i % 2 == 0 ? context.Int(0, 100) : context.Int(101, 200);
            return Args(values);
        });
    }
}
