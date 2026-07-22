namespace AlgoJudge.ProblemGeneratorSdk;

public sealed class ArrayGenerator
{
    private readonly DeterministicRandom _random;

    internal ArrayGenerator(DeterministicRandom random)
    {
        _random = random;
    }

    public int[] Int32(int length, int min, int max)
    {
        EnsureLength(length);
        return Enumerable.Range(0, length)
            .Select(_ => _random.NextInt32(min, max))
            .ToArray();
    }

    public long[] Int64(int length, long min, long max)
    {
        EnsureLength(length);
        return Enumerable.Range(0, length)
            .Select(_ => _random.NextInt64(min, max))
            .ToArray();
    }

    public int[] Sorted(int length, int min, int max)
    {
        var values = Int32(length, min, max);
        Array.Sort(values);
        return values;
    }

    public int[] AllEqual(int length, int value)
    {
        EnsureLength(length);
        return Enumerable.Repeat(value, length).ToArray();
    }

    private static void EnsureLength(int length)
    {
        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length));
    }
}
