public sealed class LeftmostBinarySearchGenerator : ProblemGenerator
{
    public override void Build(TestPlan plan)
    {
        plan.Edge("boundary-targets", 20, context => context.Ordinal switch
        {
            1 => Args(new[] { 1 }, 1),
            2 => Args(new[] { 1 }, 0),
            3 => Args(new[] { 2, 2, 2, 2 }, 2),
            _ => Args(new[] { -3, -1, 0, 0, 7 }, 0)
        });
        plan.Random("sorted-values", 450, context =>
        {
            var nums = context.Arrays.Sorted(context.Int(1, 80), -50, 50);
            var target = context.Boolean()
                ? nums[context.Int(0, nums.Length - 1)]
                : context.Int(-60, 60);
            return Args(nums, target);
        });
        plan.Adversarial("duplicate-runs", 30, context =>
        {
            var value = context.Int(-10, 10);
            return Args(context.Arrays.AllEqual(context.Int(20, 80), value), value);
        });
    }
}
