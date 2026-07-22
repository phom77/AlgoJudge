namespace AlgoJudge.ProblemGeneratorSdk;

public sealed class GenerationContext
{
    private readonly DeterministicRandom _random;

    internal GenerationContext(
        int ordinal,
        string group,
        string name,
        int seed)
    {
        Ordinal = ordinal;
        Group = group;
        Name = name;
        Seed = seed;
        _random = new DeterministicRandom(seed);
        Arrays = new ArrayGenerator(_random);
        Strings = new StringGenerator(_random);
        Permutations = new PermutationGenerator(_random);
        Graphs = new GraphGenerator(_random);
    }

    public int Ordinal { get; }
    public string Group { get; }
    public string Name { get; }
    public int Seed { get; }
    public ArrayGenerator Arrays { get; }
    public StringGenerator Strings { get; }
    public PermutationGenerator Permutations { get; }
    public GraphGenerator Graphs { get; }

    public int Int(int min, int max) => _random.NextInt32(min, max);

    public long Long(long min, long max) => _random.NextInt64(min, max);

    public bool Boolean() => _random.NextBoolean();
}
