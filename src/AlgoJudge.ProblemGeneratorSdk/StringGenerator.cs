namespace AlgoJudge.ProblemGeneratorSdk;

public sealed class StringGenerator
{
    private const string DefaultAlphabet =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    private readonly DeterministicRandom _random;

    internal StringGenerator(DeterministicRandom random)
    {
        _random = random;
    }

    public string Random(int length, string alphabet = DefaultAlphabet)
    {
        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length));
        ArgumentException.ThrowIfNullOrEmpty(alphabet);

        return string.Create(length, (Random: _random, Alphabet: alphabet), static (span, state) =>
        {
            for (var index = 0; index < span.Length; index++)
            {
                span[index] = state.Alphabet[
                    state.Random.NextInt32(0, state.Alphabet.Length - 1)];
            }
        });
    }
}
