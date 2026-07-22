using System.Text;

namespace AlgoJudge.ProblemGeneratorSdk;

internal sealed class DeterministicRandom
{
    private ulong _state;

    public DeterministicRandom(int seed)
    {
        _state = unchecked((uint)seed) + 0x9e3779b97f4a7c15UL;
    }

    public static int DeriveSeed(int rootSeed, string name, int index)
    {
        ulong hash = 14695981039346656037UL;
        foreach (var value in Encoding.UTF8.GetBytes(name))
        {
            hash ^= value;
            hash *= 1099511628211UL;
        }

        hash ^= unchecked((uint)rootSeed);
        hash *= 1099511628211UL;
        hash ^= unchecked((uint)index);
        hash *= 1099511628211UL;
        return unchecked((int)(hash ^ (hash >> 32)));
    }

    public int NextInt32(int minimum, int maximum)
    {
        if (minimum > maximum)
            throw new ArgumentOutOfRangeException(nameof(minimum));
        var width = (ulong)((long)maximum - minimum) + 1UL;
        return checked((int)(minimum + (long)NextBounded(width)));
    }

    public long NextInt64(long minimum, long maximum)
    {
        if (minimum > maximum)
            throw new ArgumentOutOfRangeException(nameof(minimum));
        var width = unchecked((ulong)(maximum - minimum)) + 1UL;
        if (width == 0)
            return unchecked((long)NextUInt64());
        return unchecked(minimum + (long)NextBounded(width));
    }

    public bool NextBoolean() => (NextUInt64() & 1UL) != 0;

    public void Shuffle<T>(IList<T> values)
    {
        for (var index = values.Count - 1; index > 0; index--)
        {
            var selected = NextInt32(0, index);
            (values[index], values[selected]) = (values[selected], values[index]);
        }
    }

    private ulong NextBounded(ulong bound)
    {
        var threshold = unchecked(0UL - bound) % bound;
        while (true)
        {
            var value = NextUInt64();
            if (value >= threshold)
                return value % bound;
        }
    }

    private ulong NextUInt64()
    {
        _state += 0x9e3779b97f4a7c15UL;
        var value = _state;
        value = (value ^ (value >> 30)) * 0xbf58476d1ce4e5b9UL;
        value = (value ^ (value >> 27)) * 0x94d049bb133111ebUL;
        return value ^ (value >> 31);
    }
}
