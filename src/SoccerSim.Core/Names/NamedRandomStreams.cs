using System.Buffers.Binary;
using System.Text;
using SoccerSim.Core.Simulation;

namespace SoccerSim.Core.Names;

/// <summary>
/// Derives independent PCG32 streams from stable semantic names. Adding a random draw to one
/// name component therefore does not shift the other components.
/// </summary>
public static class NamedRandomStreams
{
    private const ulong FnvOffset64 = 0xcbf29ce484222325UL;
    private const ulong FnvPrime64 = 0x100000001b3UL;
    private const string SeedDomain = "SoccerSim.Names.Seed.v1";
    private const string SequenceDomain = "SoccerSim.Names.Sequence.v1";

    public static NameStreamParameters Derive(ulong worldSeed, ulong personId, string streamName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamName);
        return new NameStreamParameters(
            Hash(SeedDomain, worldSeed, personId, streamName),
            Hash(SequenceDomain, worldSeed, personId, streamName));
    }

    public static Pcg32Random Create(ulong worldSeed, ulong personId, string streamName)
    {
        var parameters = Derive(worldSeed, personId, streamName);
        return new Pcg32Random(parameters.Seed, parameters.Sequence);
    }

    private static ulong Hash(string domain, ulong worldSeed, ulong personId, string streamName)
    {
        var hash = Add(FnvOffset64, Encoding.ASCII.GetBytes(domain));
        hash = AddByte(hash, 0);

        Span<byte> number = stackalloc byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64LittleEndian(number, worldSeed);
        hash = Add(hash, number);
        BinaryPrimitives.WriteUInt64LittleEndian(number, personId);
        hash = Add(hash, number);

        hash = AddByte(hash, 0);
        return Add(hash, Encoding.UTF8.GetBytes(streamName));
    }

    private static ulong Add(ulong hash, ReadOnlySpan<byte> bytes)
    {
        foreach (var value in bytes)
        {
            hash = AddByte(hash, value);
        }
        return hash;
    }

    private static ulong AddByte(ulong hash, byte value) => unchecked((hash ^ value) * FnvPrime64);
}

public readonly record struct NameStreamParameters(ulong Seed, ulong Sequence);
