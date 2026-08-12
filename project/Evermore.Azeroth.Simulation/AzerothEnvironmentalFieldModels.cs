using System.Text.Json.Serialization;

namespace Evermore.Azeroth.Simulation;

public sealed record AzerothEnvironmentalSimulationInput(
    [property: JsonPropertyOrder(0), JsonRequired] AzerothSimulationInput Simulation,
    [property: JsonPropertyOrder(1), JsonRequired] AzerothEnvironmentalFieldInput Environment);

public sealed record AzerothEnvironmentalFieldInput(
    [property: JsonPropertyOrder(0), JsonRequired] int Generation,
    [property: JsonPropertyOrder(1), JsonRequired] long SolarReferenceIrradianceMilliwattsPerSquareMeter,
    [property: JsonPropertyOrder(2), JsonRequired] IReadOnlyList<AzerothZoneFieldInput> Zones);

public sealed record AzerothZoneFieldInput(
    [property: JsonPropertyOrder(0), JsonRequired] string ZoneId,
    [property: JsonPropertyOrder(1), JsonRequired] int AtmosphereBaselineMicros,
    [property: JsonPropertyOrder(2), JsonRequired] int ManaBaselineMicros,
    [property: JsonPropertyOrder(3), JsonRequired] int SoluneAtmosphereResponseMicros,
    [property: JsonPropertyOrder(4), JsonRequired] int LunaraAtmosphereResponseMicros,
    [property: JsonPropertyOrder(5), JsonRequired] int LunaraManaResponseMicros,
    [property: JsonPropertyOrder(6), JsonRequired] int OmegaManaResponseMicros);

public sealed record AzerothEnvironmentalDrivers(
    [property: JsonPropertyOrder(0), JsonRequired] int SoluneIrradianceMicros,
    [property: JsonPropertyOrder(1), JsonRequired] int LunaraTideMicros,
    [property: JsonPropertyOrder(2), JsonRequired] int LunaraResonanceMicros,
    [property: JsonPropertyOrder(3), JsonRequired] int OmegaFitnessMicros);

public sealed record AzerothZoneFieldObservation(
    [property: JsonPropertyOrder(0), JsonRequired] string ZoneId,
    [property: JsonPropertyOrder(1), JsonRequired] int AtmosphereSignalMicros,
    [property: JsonPropertyOrder(2), JsonRequired] int ManaSignalMicros,
    [property: JsonPropertyOrder(3), JsonRequired] int SoluneAtmosphereContributionMicros,
    [property: JsonPropertyOrder(4), JsonRequired] int LunaraAtmosphereContributionMicros,
    [property: JsonPropertyOrder(5), JsonRequired] int LunaraManaContributionMicros,
    [property: JsonPropertyOrder(6), JsonRequired] int OmegaManaContributionMicros);

public sealed record AzerothEnvironmentalFieldSnapshot(
    [property: JsonPropertyOrder(0), JsonRequired] int Generation,
    [property: JsonPropertyOrder(1), JsonRequired] string Units,
    [property: JsonPropertyOrder(2), JsonRequired] string Model,
    [property: JsonPropertyOrder(3), JsonRequired] string SpatialAuthority,
    [property: JsonPropertyOrder(4), JsonRequired] AzerothSimulationDisposition Disposition,
    [property: JsonPropertyOrder(5), JsonRequired] IReadOnlyList<string> ContainmentReasons,
    [property: JsonPropertyOrder(6), JsonRequired] AzerothEnvironmentalDrivers Drivers,
    [property: JsonPropertyOrder(7), JsonRequired] IReadOnlyList<AzerothZoneFieldObservation> Zones);

public sealed record AzerothEnvironmentalSimulationSnapshot(
    [property: JsonPropertyOrder(0), JsonRequired] AzerothSimulationSnapshot Simulation,
    [property: JsonPropertyOrder(1), JsonRequired] AzerothEnvironmentalFieldSnapshot Environment,
    [property: JsonPropertyOrder(2), JsonRequired] AzerothEnvironmentalSimulationInput Input);

public sealed record AzerothEnvironmentalSimulationEnvelope(
    [property: JsonPropertyOrder(0), JsonRequired] string SchemaVersion,
    [property: JsonPropertyOrder(1), JsonRequired] string Authority,
    [property: JsonPropertyOrder(2), JsonRequired] string PayloadSha256,
    [property: JsonPropertyOrder(3), JsonRequired] AzerothEnvironmentalSimulationSnapshot Payload);
