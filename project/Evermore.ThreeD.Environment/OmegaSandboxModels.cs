using System.Text.Json.Serialization;

namespace Evermore.ThreeD.Environment;

public static class OmegaSandboxDimensions
{
    public const int Count = 18;
    public const int MinimumValue = -1_000_000;
    public const int MaximumValue = 1_000_000;

    public const int X = 0;
    public const int Y = 1;
    public const int Z = 2;
    public const int TemporalPhase = 3;
    public const int Mana = 4;
    public const int Atmosphere = 5;
    public const int Radiance = 6;
    public const int Tidal = 7;
    public const int Resonance = 8;
    public const int Entropy = 9;
    public const int Dominion = 10;
    public const int Whispers = 11;
    public const int VoidPulse = 12;
    public const int Echo = 13;
    public const int Ascension = 14;
    public const int Decay = 15;
    public const int Flux = 16;
    public const int ContainmentIntegrity = 17;

    public static string[] CreateNames() =>
    [
        "X",
        "Y",
        "Z",
        "TemporalPhase",
        "Mana",
        "Atmosphere",
        "Radiance",
        "Tidal",
        "Resonance",
        "Entropy",
        "Dominion",
        "Whispers",
        "VoidPulse",
        "Echo",
        "Ascension",
        "Decay",
        "Flux",
        "ContainmentIntegrity"
    ];
}

public static class OmegaSandboxBodyKinds
{
    public const string Hearthstar = "hearthstar";
    public const string World = "world";
    public const string Moon = "moon";
}

public sealed record OmegaSandboxInput(
    ulong Seed,
    int Generations,
    int SystemCount,
    int WorldsPerSystem,
    int MoonsPerWorld);

public enum OmegaSandboxDisposition
{
    Completed,
    Contained
}

public sealed record OmegaSandboxOmegaObservation(
    string Authority,
    string Disposition,
    ulong Seed,
    int RequestedGenerations,
    int ProcessedGenerations,
    int AverageFitnessMicros,
    string DominantTrait,
    int ResonanceEventCount,
    string? ContainmentReason);

public sealed record OmegaSandboxBody(
    string BodyId,
    string BodyKind,
    string? ParentBodyId,
    int Ordinal,
    int ScaleMicros,
    int[] DimensionVector);

public sealed record OmegaSandboxSystem(
    string SystemId,
    OmegaSandboxBody Hearthstar,
    OmegaSandboxBody[] Worlds,
    OmegaSandboxBody[] Moons);

public sealed record OmegaSandboxSnapshot(
    OmegaSandboxInput Input,
    OmegaSandboxDisposition Disposition,
    string Authority,
    string Classification,
    string CoordinateMeaning,
    string PhysicalScale,
    string[] DimensionNames,
    OmegaSandboxOmegaObservation Omega,
    int SystemCount,
    int WorldCount,
    int MoonCount,
    int TotalBodyCount,
    string? ContainmentReason,
    OmegaSandboxSystem[] Systems,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] int SimulationTick = 0);

public sealed record OmegaSandboxEnvelope(
    string SchemaVersion,
    string Authority,
    string PayloadSha256,
    OmegaSandboxSnapshot Payload);
