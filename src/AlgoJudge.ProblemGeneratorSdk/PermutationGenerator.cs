namespace AlgoJudge.ProblemGeneratorSdk;

public sealed class PermutationGenerator
{
    private readonly DeterministicRandom _random;

    internal PermutationGenerator(DeterministicRandom random)
    {
        _random = random;
    }

    public int[] Generate(int length, int start = 0)
    {
        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length));
        var values = Enumerable.Range(start, length).ToArray();
        _random.Shuffle(values);
        return values;
    }
}
