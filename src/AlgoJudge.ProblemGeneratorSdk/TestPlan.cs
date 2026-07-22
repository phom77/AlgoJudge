namespace AlgoJudge.ProblemGeneratorSdk;

public sealed class TestPlan
{
    private readonly List<PlanEntry> _entries = [];
    private readonly HashSet<string> _caseNames = new(StringComparer.Ordinal);

    public void Handwritten(string name, TestArguments arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        AddEntry(name, "handwritten", 1, _ => arguments);
    }

    public void Edge(
        string name,
        int count,
        Func<GenerationContext, TestArguments> factory) =>
        AddEntry(name, "edge", count, factory);

    public void Random(
        string name,
        int count,
        Func<GenerationContext, TestArguments> factory) =>
        AddEntry(name, "random", count, factory);

    public void Adversarial(
        string name,
        int count,
        Func<GenerationContext, TestArguments> factory) =>
        AddEntry(name, "adversarial", count, factory);

    public void Stress(
        string name,
        int count,
        Func<GenerationContext, TestArguments> factory) =>
        AddEntry(name, "stress", count, factory);

    public IReadOnlyList<GeneratedPlanCase> Execute(int rootSeed, int maximumCaseCount)
    {
        if (maximumCaseCount < 0)
            throw new ArgumentOutOfRangeException(nameof(maximumCaseCount));

        var requestedCount = _entries.Sum(entry => (long)entry.Count);
        if (requestedCount > maximumCaseCount)
        {
            throw new InvalidOperationException(
                $"Test plan requests {requestedCount} cases; the limit is {maximumCaseCount}.");
        }

        var cases = new List<GeneratedPlanCase>((int)requestedCount);
        var ordinal = 1;
        foreach (var entry in _entries)
        {
            for (var index = 0; index < entry.Count; index++)
            {
                var seed = DeterministicRandom.DeriveSeed(rootSeed, entry.Name, index);
                var context = new GenerationContext(ordinal, entry.Group, entry.Name, seed);
                var arguments = entry.Factory(context) ??
                    throw new InvalidOperationException(
                        $"Generator case {entry.Name} returned null arguments.");
                cases.Add(new GeneratedPlanCase(
                    ordinal,
                    entry.Count == 1 ? entry.Name : $"{entry.Name}-{index + 1}",
                    entry.Group,
                    seed,
                    arguments));
                ordinal++;
            }
        }

        return cases;
    }

    private void AddEntry(
        string name,
        string group,
        int count,
        Func<GenerationContext, TestArguments> factory)
    {
        if (!IsKebabCase(name))
            throw new ArgumentException("Case names must use lowercase kebab-case.", nameof(name));
        if (!_caseNames.Add(name))
            throw new ArgumentException($"Duplicate test-plan case name: {name}.", nameof(name));
        if (count <= 0)
            throw new ArgumentOutOfRangeException(nameof(count));
        ArgumentNullException.ThrowIfNull(factory);
        _entries.Add(new PlanEntry(name, group, count, factory));
    }

    private static bool IsKebabCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value[0] == '-' || value[^1] == '-')
            return false;

        var previousHyphen = false;
        foreach (var character in value)
        {
            if (character == '-')
            {
                if (previousHyphen)
                    return false;
                previousHyphen = true;
                continue;
            }

            if (!(character is >= 'a' and <= 'z' or >= '0' and <= '9'))
                return false;
            previousHyphen = false;
        }

        return true;
    }

    private sealed record PlanEntry(
        string Name,
        string Group,
        int Count,
        Func<GenerationContext, TestArguments> Factory);
}
