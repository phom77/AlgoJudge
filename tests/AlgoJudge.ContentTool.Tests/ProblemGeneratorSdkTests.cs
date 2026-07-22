using AlgoJudge.ProblemGeneratorSdk;

namespace AlgoJudge.ContentTool.Tests;

public sealed class ProblemGeneratorSdkTests
{
    [Fact]
    public void SameRootSeedProducesSamePlanAndDifferentSeedChangesRandomCases()
    {
        var plan = new TestPlan();
        new RepresentativeGenerator().Build(plan);

        var first = plan.Execute(1234, 20);
        var repeated = plan.Execute(1234, 20);
        var changed = plan.Execute(5678, 20);

        Assert.Equal(first.Select(item => item.Seed), repeated.Select(item => item.Seed));
        Assert.Equal(
            first.Select(item => item.Arguments.Values.Single()),
            repeated.Select(item => item.Arguments.Values.Single()));
        Assert.NotEqual(first[1].Seed, changed[1].Seed);
        Assert.Equal(["handwritten", "random", "stress"], first.Select(item => item.Group).Distinct());
    }

    [Fact]
    public void HelpersProduceBoundedArraysStringsPermutationsAndGraphs()
    {
        var plan = new TestPlan();
        plan.Random("helpers", 1, context => TestGenerator.CreateArguments(
            context.Arrays.Int32(20, -5, 5),
            context.Arrays.Sorted(20, -5, 5),
            context.Arrays.AllEqual(4, 7),
            context.Strings.Random(12, "ab"),
            context.Permutations.Generate(20),
            context.Graphs.Tree(10),
            context.Graphs.Dag(8, 10),
            context.Graphs.Connected(8, 4)));

        var values = plan.Execute(42, 1).Single().Arguments.Values;

        Assert.All((int[])values[0]!, value => Assert.InRange(value, -5, 5));
        Assert.Equal(((int[])values[1]!).Order(), (int[])values[1]!);
        Assert.Equal([7, 7, 7, 7], (int[])values[2]!);
        Assert.All((string)values[3]!, value => Assert.Contains(value, "ab"));
        Assert.Equal(Enumerable.Range(0, 20), ((int[])values[4]!).Order());
        Assert.Equal(9, ((GeneratedGraph)values[5]!).Edges.Count);
        Assert.All(((GeneratedGraph)values[6]!).Edges, edge => Assert.True(edge.From < edge.To));
        Assert.Equal(11, ((GeneratedGraph)values[7]!).Edges.Count);
    }

    [Fact]
    public void PlanRejectsDuplicateNamesAndCaseCountOverflow()
    {
        var plan = new TestPlan();
        plan.Random("same-name", 2, _ => TestGenerator.CreateArguments(1));

        Assert.Throws<ArgumentException>(() =>
            plan.Stress("same-name", 1, _ => TestGenerator.CreateArguments(2)));
        Assert.Throws<InvalidOperationException>(() => plan.Execute(1, 1));
    }

    private sealed class RepresentativeGenerator : ProblemGenerator
    {
        public override void Build(TestPlan plan)
        {
            plan.Handwritten("minimum", Args(0));
            plan.Random("random-values", 2, context => Args(context.Int(-100, 100)));
            plan.Stress("maximum", 1, _ => Args(int.MaxValue));
        }
    }

    private sealed class TestGenerator : ProblemGenerator
    {
        public override void Build(TestPlan plan)
        {
        }

        public static TestArguments CreateArguments(params object?[] values) => Args(values);
    }
}
