using System.Text.Json.Serialization;

namespace Evermore.Lunara.Core;

[JsonConverter(typeof(JsonStringEnumConverter<LunarPhaseName>))]
public enum LunarPhaseName
{
    New,
    WaxingCrescent,
    FirstQuarter,
    WaxingGibbous,
    Full,
    WaningGibbous,
    LastQuarter,
    WaningCrescent
}

[JsonConverter(typeof(JsonStringEnumConverter<TideBand>))]
public enum TideBand
{
    Low,
    Ebb,
    Mean,
    Flood,
    High
}

[JsonConverter(typeof(JsonStringEnumConverter<MoonTideDisposition>))]
public enum MoonTideDisposition
{
    Completed,
    Contained
}

public sealed record LunarCycleState(
    [property: JsonPropertyOrder(0), JsonRequired] int PhaseStep,
    [property: JsonPropertyOrder(1), JsonRequired] LunarPhaseName Phase,
    [property: JsonPropertyOrder(2), JsonRequired] bool IsWaxing,
    [property: JsonPropertyOrder(3), JsonRequired] int IlluminationMicros,
    [property: JsonPropertyOrder(4), JsonRequired] int SpringAlignmentMicros);

public sealed record TideState(
    [property: JsonPropertyOrder(0), JsonRequired] int TidePhaseStep,
    [property: JsonPropertyOrder(1), JsonRequired] int SignedWaveMicros,
    [property: JsonPropertyOrder(2), JsonRequired] int AmplitudeMicros,
    [property: JsonPropertyOrder(3), JsonRequired] int LevelMicros,
    [property: JsonPropertyOrder(4), JsonRequired] TideBand Band);

public sealed record LunaraRelicState(
    [property: JsonPropertyOrder(0), JsonRequired] string RelicId,
    [property: JsonPropertyOrder(1), JsonRequired] string SoulSignature,
    [property: JsonPropertyOrder(2), JsonRequired] int ChargeMicros,
    [property: JsonPropertyOrder(3), JsonRequired] int LocalRitualCount);

public sealed record LunaraRitualAttempt(
    [property: JsonPropertyOrder(0), JsonRequired] string RitualId,
    [property: JsonPropertyOrder(1), JsonRequired] string RelicId,
    [property: JsonPropertyOrder(2), JsonRequired] string CultId,
    [property: JsonPropertyOrder(3), JsonRequired] bool LocalCultAuthorized,
    [property: JsonPropertyOrder(4), JsonRequired] bool LocalRitualValid);

public sealed record LunaraApiReward(
    [property: JsonPropertyOrder(0), JsonRequired] string RelicId,
    [property: JsonPropertyOrder(1), JsonRequired] int RewardChargeMicros);

public sealed record LunaraApiResponse(
    [property: JsonPropertyOrder(0), JsonRequired] string ResponseId,
    [property: JsonPropertyOrder(1), JsonRequired] string RitualId,
    [property: JsonPropertyOrder(2), JsonRequired] bool Accepted,
    [property: JsonPropertyOrder(3), JsonRequired] bool ManifestationRequested,
    [property: JsonPropertyOrder(4), JsonRequired] IReadOnlyList<LunaraApiReward> Rewards);

public sealed record LunaraRelicHistoryState(
    [property: JsonPropertyOrder(0), JsonRequired] string RelicId,
    [property: JsonPropertyOrder(1), JsonRequired] string SoulSignature,
    [property: JsonPropertyOrder(2), JsonRequired] int ChargeMicros,
    [property: JsonPropertyOrder(3), JsonRequired] int LocalRitualCount);

public sealed record MoonTideHistoryPoint(
    [property: JsonPropertyOrder(0), JsonRequired] int Generation,
    [property: JsonPropertyOrder(1), JsonRequired] IReadOnlyList<LunaraRelicHistoryState> Relics,
    [property: JsonPropertyOrder(2), JsonRequired] int ManifestationDirectiveCount);

public sealed record MoonTideInput(
    [property: JsonPropertyOrder(0), JsonRequired] ulong Seed,
    [property: JsonPropertyOrder(1), JsonRequired] int Generation,
    [property: JsonPropertyOrder(2), JsonRequired] int LunarPhaseStep,
    [property: JsonPropertyOrder(3), JsonRequired] int TidePhaseStep,
    [property: JsonPropertyOrder(4), JsonRequired] int BaselineTideMicros,
    [property: JsonPropertyOrder(5), JsonRequired] int LocalTideResponseMicros,
    [property: JsonPropertyOrder(6), JsonRequired] IReadOnlyList<LunaraRelicState> Relics,
    [property: JsonPropertyOrder(7), JsonRequired] IReadOnlyList<LunaraRitualAttempt> RitualAttempts,
    [property: JsonPropertyOrder(8), JsonRequired] IReadOnlyList<LunaraApiResponse> ApiResponses,
    [property: JsonPropertyOrder(9), JsonRequired] IReadOnlyList<MoonTideHistoryPoint> History);

public sealed record LunaraManifestationDirective(
    [property: JsonPropertyOrder(0), JsonRequired] string RitualId,
    [property: JsonPropertyOrder(1), JsonRequired] string RelicId,
    [property: JsonPropertyOrder(2), JsonRequired] string SoulSignature,
    [property: JsonPropertyOrder(3), JsonRequired] int ProbabilityMicros,
    [property: JsonPropertyOrder(4), JsonRequired] int RollMicros,
    [property: JsonPropertyOrder(5), JsonRequired] string Authority);

public sealed record LunaraEvent(
    [property: JsonPropertyOrder(0), JsonRequired] string Code,
    [property: JsonPropertyOrder(1), JsonRequired] string SubjectId,
    [property: JsonPropertyOrder(2), JsonRequired] int ValueMicros,
    [property: JsonPropertyOrder(3), JsonRequired] string Detail);

public sealed record MoonTideSnapshot(
    [property: JsonPropertyOrder(0), JsonRequired] MoonTideDisposition Disposition,
    [property: JsonPropertyOrder(1), JsonRequired] string? ContainmentCode,
    [property: JsonPropertyOrder(2), JsonRequired] string? ContainmentReason,
    [property: JsonPropertyOrder(3), JsonRequired] ulong Seed,
    [property: JsonPropertyOrder(4), JsonRequired] int Generation,
    [property: JsonPropertyOrder(5), JsonRequired] LunarCycleState LunarCycle,
    [property: JsonPropertyOrder(6), JsonRequired] TideState Tide,
    [property: JsonPropertyOrder(7), JsonRequired] int ResonanceMicros,
    [property: JsonPropertyOrder(8), JsonRequired] IReadOnlyList<LunaraRelicState> Relics,
    [property: JsonPropertyOrder(9), JsonRequired] IReadOnlyList<LunaraManifestationDirective> ManifestationDirectives,
    [property: JsonPropertyOrder(10), JsonRequired] IReadOnlyList<LunaraEvent> Events);

public sealed record MoonTideSnapshotEnvelope(
    [property: JsonPropertyOrder(0), JsonRequired] string SchemaVersion,
    [property: JsonPropertyOrder(1), JsonRequired] string Authority,
    [property: JsonPropertyOrder(2), JsonRequired] string PayloadSha256,
    [property: JsonPropertyOrder(3), JsonRequired] MoonTideSnapshot Payload);
