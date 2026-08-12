namespace Omega.Recursive;

public sealed class OmegaRandom
{
    private ulong _state;

    public OmegaRandom(ulong seed)
    {
        _state = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;
    }

    public ulong NextUInt64()
    {
        unchecked
        {
            ulong value = (_state += 0x9E3779B97F4A7C15UL);
            value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
            value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
            return value ^ (value >> 31);
        }
    }

    public int NextInt32(int exclusiveMaximum)
    {
        if (exclusiveMaximum <= 0)
            throw new ArgumentOutOfRangeException(nameof(exclusiveMaximum), "The maximum must be positive.");

        return (int)(NextUInt64() % (ulong)exclusiveMaximum);
    }

    public float NextSingle() => (NextUInt64() >> 40) * (1.0f / (1U << 24));

    public bool Chance(float probability) => NextSingle() < Math.Clamp(probability, 0f, 1f);
}
