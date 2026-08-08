namespace SoccerSim.Core.Simulation;

/// <summary>
/// Small deterministic PCG32 implementation with explicit state.
/// The simulation must receive an instance instead of using process-global randomness.
/// </summary>
public sealed class Pcg32Random : IRandomSource
{
    private const ulong Multiplier = 6364136223846793005UL;
    private ulong _state;
    private readonly ulong _increment;

    public Pcg32Random(ulong seed, ulong sequence = 54UL)
    {
        _state = 0UL;
        _increment = (sequence << 1) | 1UL;
        _ = NextUInt32();
        _state = unchecked(_state + seed);
        _ = NextUInt32();
    }

    public ulong State => _state;

    public uint NextUInt32()
    {
        var oldState = _state;
        _state = unchecked(oldState * Multiplier + _increment);
        var xorShifted = (uint)(((oldState >> 18) ^ oldState) >> 27);
        var rotation = (int)(oldState >> 59);
        return (xorShifted >> rotation) | (xorShifted << ((-rotation) & 31));
    }

    public int NextInt(int maxExclusive)
    {
        if (maxExclusive <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxExclusive));
        }

        var bound = (uint)maxExclusive;
        var threshold = unchecked((uint)(0 - bound)) % bound;
        while (true)
        {
            var value = NextUInt32();
            if (value >= threshold)
            {
                return (int)(value % bound);
            }
        }
    }
}
