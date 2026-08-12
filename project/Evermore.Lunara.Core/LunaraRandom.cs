namespace Evermore.Lunara.Core;

internal sealed class LunaraRandom
{
    private ulong _state;

    public LunaraRandom(ulong seed, int generation)
    {
        _state = seed ^ unchecked((ulong)(uint)generation * 0xD1B54A32D192ED03UL);
    }

    public int NextUnitMicros() => (int)(NextUInt64() % (ulong)LunaraBounds.Unit);

    private ulong NextUInt64()
    {
        unchecked
        {
            ulong value = (_state += 0x9E3779B97F4A7C15UL);
            value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
            value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
            return value ^ (value >> 31);
        }
    }
}
