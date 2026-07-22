namespace AlgoJudge.ProblemGeneratorSdk;

public abstract class ProblemGenerator
{
    public abstract void Build(TestPlan plan);

    protected static TestArguments Args(params object?[] values) => new(values);
}
