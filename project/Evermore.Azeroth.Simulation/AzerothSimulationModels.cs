using System.Text.Json.Serialization;
using Evermore.Lunara.Core;
using Evermore.Solune.Core;
using Omega.Recursive;

namespace Evermore.Azeroth.Simulation;

[JsonConverter(typeof(JsonStringEnumConverter<AzerothSimulationDisposition>))]
public enum AzerothSimulationDisposition
{
    Completed,
    Contained
}

public sealed record AzerothSimulationInput(
    [property: JsonPropertyOrder(0), JsonRequired] OmegaSimulationOptions Omega,
    [property: JsonPropertyOrder(1), JsonRequired] MoonTideInput Lunara,
    [property: JsonPropertyOrder(2), JsonRequired] SoluneInput Solune);

public sealed record AzerothZoneObservation(
    [property: JsonPropertyOrder(0), JsonRequired] string ZoneId,
    [property: JsonPropertyOrder(1), JsonRequired] string Source);

public sealed record AzerothScopeObservation(
    [property: JsonPropertyOrder(0), JsonRequired] string World,
    [property: JsonPropertyOrder(1), JsonRequired] string Region,
    [property: JsonPropertyOrder(2), JsonRequired] string Coverage,
    [property: JsonPropertyOrder(3), JsonRequired] string GlobeModel,
    [property: JsonPropertyOrder(4), JsonRequired] string AtmosphereModel,
    [property: JsonPropertyOrder(5), JsonRequired] string ManaModel,
    [property: JsonPropertyOrder(6), JsonRequired] IReadOnlyList<AzerothZoneObservation> Zones);

public sealed record AzerothOmegaObservation(
    [property: JsonPropertyOrder(0), JsonRequired] string Authority,
    [property: JsonPropertyOrder(1), JsonRequired] string Disposition,
    [property: JsonPropertyOrder(2), JsonRequired] ulong Seed,
    [property: JsonPropertyOrder(3), JsonRequired] int RequestedGenerations,
    [property: JsonPropertyOrder(4), JsonRequired] int ProcessedGenerations,
    [property: JsonPropertyOrder(5), JsonRequired] int Population,
    [property: JsonPropertyOrder(6), JsonRequired] float AverageFitness,
    [property: JsonPropertyOrder(7), JsonRequired] string DominantTrait,
    [property: JsonPropertyOrder(8), JsonRequired] int ArchiveCheckpoints,
    [property: JsonPropertyOrder(9), JsonRequired] int ResonanceEvents,
    [property: JsonPropertyOrder(10), JsonRequired] string? ContainmentReason);

public sealed record AzerothLunaraObservation(
    [property: JsonPropertyOrder(0), JsonRequired] string Authority,
    [property: JsonPropertyOrder(1), JsonRequired] string Disposition,
    [property: JsonPropertyOrder(2), JsonRequired] ulong Seed,
    [property: JsonPropertyOrder(3), JsonRequired] int Generation,
    [property: JsonPropertyOrder(4), JsonRequired] string LunarPhase,
    [property: JsonPropertyOrder(5), JsonRequired] int IlluminationMicros,
    [property: JsonPropertyOrder(6), JsonRequired] int TideLevelMicros,
    [property: JsonPropertyOrder(7), JsonRequired] int ResonanceMicros,
    [property: JsonPropertyOrder(8), JsonRequired] int RelicCount,
    [property: JsonPropertyOrder(9), JsonRequired] int ManifestationDirectiveCount,
    [property: JsonPropertyOrder(10), JsonRequired] string? ContainmentCode,
    [property: JsonPropertyOrder(11), JsonRequired] string? ContainmentReason);

public sealed record AzerothSoluneObservation(
    [property: JsonPropertyOrder(0), JsonRequired] string Authority,
    [property: JsonPropertyOrder(1), JsonRequired] string Disposition,
    [property: JsonPropertyOrder(2), JsonRequired] int Generation,
    [property: JsonPropertyOrder(3), JsonRequired] string ConstructId,
    [property: JsonPropertyOrder(4), JsonRequired] string EvercyclePhase,
    [property: JsonPropertyOrder(5), JsonRequired] int OrbitPhaseMicros,
    [property: JsonPropertyOrder(6), JsonRequired] long IrradianceMilliwattsPerSquareMeter,
    [property: JsonPropertyOrder(7), JsonRequired] int ApparentDiameterMicrodegrees,
    [property: JsonPropertyOrder(8), JsonRequired] int OperationDirectiveCount,
    [property: JsonPropertyOrder(9), JsonRequired] string? ContainmentCode,
    [property: JsonPropertyOrder(10), JsonRequired] string? ContainmentReason);

public sealed record AzerothSimulationSnapshot(
    [property: JsonPropertyOrder(0), JsonRequired] int Generation,
    [property: JsonPropertyOrder(1), JsonRequired] AzerothScopeObservation Azeroth,
    [property: JsonPropertyOrder(2), JsonRequired] AzerothOmegaObservation Omega,
    [property: JsonPropertyOrder(3), JsonRequired] AzerothLunaraObservation Lunara,
    [property: JsonPropertyOrder(4), JsonRequired] AzerothSoluneObservation Solune,
    [property: JsonPropertyOrder(5), JsonRequired] AzerothSimulationDisposition Disposition,
    [property: JsonPropertyOrder(6), JsonRequired] IReadOnlyList<string> ContainmentReasons,
    [property: JsonPropertyOrder(7), JsonRequired] AzerothSimulationInput Input);

public sealed record AzerothSimulationEnvelope(
    [property: JsonPropertyOrder(0), JsonRequired] string SchemaVersion,
    [property: JsonPropertyOrder(1), JsonRequired] string Authority,
    [property: JsonPropertyOrder(2), JsonRequired] string PayloadSha256,
    [property: JsonPropertyOrder(3), JsonRequired] AzerothSimulationSnapshot Payload);
