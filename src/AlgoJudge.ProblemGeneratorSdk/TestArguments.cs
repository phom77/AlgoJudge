namespace AlgoJudge.ProblemGeneratorSdk;

public sealed class TestArguments
{
    internal TestArguments(IReadOnlyList<object?> values)
    {
        Values = values;
    }

    public IReadOnlyList<object?> Values { get; }
}
