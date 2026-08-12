using System.Text.Json.Serialization;

namespace Evermore.Solune.Core;

[JsonConverter(typeof(JsonStringEnumConverter<SoluneDisposition>))]
public enum SoluneDisposition
{
    Completed,
    Contained
}

[JsonConverter(typeof(JsonStringEnumConverter<EvercyclePhase>))]
public enum EvercyclePhase
{
    Everturn,
    Everwake,
    Stillstar
}

public sealed record SoluneModelState(
    [property: JsonPropertyOrder(0), JsonRequired] string ConstructId,
    [property: JsonPropertyOrder(1), JsonRequired] string ContinuitySignature,
    [property: JsonPropertyOrder(2), JsonRequired] int EvermoreRadiusKm,
    [property: JsonPropertyOrder(3), JsonRequired] int SoluneRadiusKm,
    [property: JsonPropertyOrder(4), JsonRequired] int OrbitRadiusKm,
    [property: JsonPropertyOrder(5), JsonRequired] int OrbitalPeriodTenThousandthDays,
    [property: JsonPropertyOrder(6), JsonRequired] int RevolutionsPerEvercycle,
    [property: JsonPropertyOrder(7), JsonRequired] long LuminosityTerawatts,
    [property: JsonPropertyOrder(8), JsonRequired] int PhotosphereKelvin,
    [property: JsonPropertyOrder(9), JsonRequired] int ApparentDiameterMicrodegrees,
    [property: JsonPropertyOrder(10), JsonRequired] int SiderealRotationMicrohours,
    [property: JsonPropertyOrder(11), JsonRequired] int LocalSolarDayMicrohours,
    [property: JsonPropertyOrder(12), JsonRequired] int TidalForceRatioMicros,
    [property: JsonPropertyOrder(13), JsonRequired] int BarycenterKm,
    [property: JsonPropertyOrder(14), JsonRequired] int OrbitEccentricityMicros,
    [property: JsonPropertyOrder(15), JsonRequired] int OrbitInclinationMicrodegrees,
    [property: JsonPropertyOrder(16), JsonRequired] int ContainmentIntegrityMicros,
    [property: JsonPropertyOrder(17), JsonRequired] bool LocalConstructAuthorized);

public sealed record SoluneHistoryPoint(
    [property: JsonPropertyOrder(0), JsonRequired] int Generation,
    [property: JsonPropertyOrder(1), JsonRequired] string ConstructId,
    [property: JsonPropertyOrder(2), JsonRequired] string ContinuitySignature,
    [property: JsonPropertyOrder(3), JsonRequired] int OrbitRadiusKm,
    [property: JsonPropertyOrder(4), JsonRequired] long LuminosityTerawatts,
    [property: JsonPropertyOrder(5), JsonRequired] int PhotosphereKelvin,
    [property: JsonPropertyOrder(6), JsonRequired] int ContainmentIntegrityMicros);

public sealed record SoluneInput(
    [property: JsonPropertyOrder(0), JsonRequired] int Generation,
    [property: JsonPropertyOrder(1), JsonRequired] int EvercycleStepMicrodays,
    [property: JsonPropertyOrder(2), JsonRequired] SoluneModelState Model,
    [property: JsonPropertyOrder(3), JsonRequired] IReadOnlyList<SoluneHistoryPoint> History);

public sealed record SoluneOrbitState(
    [property: JsonPropertyOrder(0), JsonRequired] int OrbitRadiusKm,
    [property: JsonPropertyOrder(1), JsonRequired] int CalculatedPeriodTenThousandthDays,
    [property: JsonPropertyOrder(2), JsonRequired] int RevolutionIndex,
    [property: JsonPropertyOrder(3), JsonRequired] int PhaseMicros,
    [property: JsonPropertyOrder(4), JsonRequired] int BarycenterKm,
    [property: JsonPropertyOrder(5), JsonRequired] int ImpliedMassRatioMicros,
    [property: JsonPropertyOrder(6), JsonRequired] int EccentricityMicros,
    [property: JsonPropertyOrder(7), JsonRequired] int InclinationMicrodegrees);

public sealed record SoluneRadianceState(
    [property: JsonPropertyOrder(0), JsonRequired] long DeclaredLuminosityTerawatts,
    [property: JsonPropertyOrder(1), JsonRequired] long CalculatedBlackbodyLuminosityTerawatts,
    [property: JsonPropertyOrder(2), JsonRequired] int LuminosityDeviationMicros,
    [property: JsonPropertyOrder(3), JsonRequired] long IrradianceMilliwattsPerSquareMeter,
    [property: JsonPropertyOrder(4), JsonRequired] int DeclaredApparentDiameterMicrodegrees,
    [property: JsonPropertyOrder(5), JsonRequired] int CalculatedApparentDiameterMicrodegrees,
    [property: JsonPropertyOrder(6), JsonRequired] int ApparentDiameterDeviationMicrodegrees,
    [property: JsonPropertyOrder(7), JsonRequired] int DeclaredTidalForceRatioMicros,
    [property: JsonPropertyOrder(8), JsonRequired] long CalculatedTidalForceRatioMicros,
    [property: JsonPropertyOrder(9), JsonRequired] long TidalForceDeviationMicros,
    [property: JsonPropertyOrder(10), JsonRequired] int DeclaredLocalSolarDayMicrohours,
    [property: JsonPropertyOrder(11), JsonRequired] long CalculatedLocalSolarDayMicrohours,
    [property: JsonPropertyOrder(12), JsonRequired] long SolarDayDeviationMicrohours,
    [property: JsonPropertyOrder(13), JsonRequired] int PhotosphereKelvin);

public sealed record EvercycleState(
    [property: JsonPropertyOrder(0), JsonRequired] int EvercycleStepMicrodays,
    [property: JsonPropertyOrder(1), JsonRequired] EvercyclePhase Phase,
    [property: JsonPropertyOrder(2), JsonRequired] int EverturnNumber,
    [property: JsonPropertyOrder(3), JsonRequired] int DayWithinEverturn,
    [property: JsonPropertyOrder(4), JsonRequired] int PhaseProgressMicros,
    [property: JsonPropertyOrder(5), JsonRequired] int BaseCalendarDays,
    [property: JsonPropertyOrder(6), JsonRequired] int ReconciliationLengthMicrodays,
    [property: JsonPropertyOrder(7), JsonRequired] int EvercycleLengthMicrodays);

public sealed record SoluneOperationDirective(
    [property: JsonPropertyOrder(0), JsonRequired] string Code,
    [property: JsonPropertyOrder(1), JsonRequired] string ConstructId,
    [property: JsonPropertyOrder(2), JsonRequired] int Generation,
    [property: JsonPropertyOrder(3), JsonRequired] long TargetIrradianceMilliwattsPerSquareMeter,
    [property: JsonPropertyOrder(4), JsonRequired] string Authority);

public sealed record SoluneEvent(
    [property: JsonPropertyOrder(0), JsonRequired] string Code,
    [property: JsonPropertyOrder(1), JsonRequired] string SubjectId,
    [property: JsonPropertyOrder(2), JsonRequired] long Value,
    [property: JsonPropertyOrder(3), JsonRequired] string Detail);

public sealed record SoluneSnapshot(
    [property: JsonPropertyOrder(0), JsonRequired] SoluneDisposition Disposition,
    [property: JsonPropertyOrder(1), JsonRequired] string? ContainmentCode,
    [property: JsonPropertyOrder(2), JsonRequired] string? ContainmentReason,
    [property: JsonPropertyOrder(3), JsonRequired] int Generation,
    [property: JsonPropertyOrder(4), JsonRequired] int EvercycleStepMicrodays,
    [property: JsonPropertyOrder(5), JsonRequired] SoluneModelState Model,
    [property: JsonPropertyOrder(6), JsonRequired] IReadOnlyList<SoluneHistoryPoint> History,
    [property: JsonPropertyOrder(7), JsonRequired] SoluneOrbitState Orbit,
    [property: JsonPropertyOrder(8), JsonRequired] SoluneRadianceState Radiance,
    [property: JsonPropertyOrder(9), JsonRequired] EvercycleState Calendar,
    [property: JsonPropertyOrder(10), JsonRequired] IReadOnlyList<SoluneOperationDirective> OperationDirectives,
    [property: JsonPropertyOrder(11), JsonRequired] IReadOnlyList<SoluneEvent> Events);

public sealed record SoluneSnapshotEnvelope(
    [property: JsonPropertyOrder(0), JsonRequired] string SchemaVersion,
    [property: JsonPropertyOrder(1), JsonRequired] string Authority,
    [property: JsonPropertyOrder(2), JsonRequired] string PayloadSha256,
    [property: JsonPropertyOrder(3), JsonRequired] SoluneSnapshot Payload);
