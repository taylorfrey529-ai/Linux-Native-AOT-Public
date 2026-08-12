using System.Text.Json.Serialization;

namespace Evermore.Azeroth.Simulation;

public sealed record AzerothOrbitalSceneInput(
    [property: JsonPropertyOrder(0), JsonRequired] AzerothEnvironmentalSimulationInput Environment,
    [property: JsonPropertyOrder(1), JsonRequired] AzerothOrbitalCameraInput Camera);

public sealed record AzerothOrbitalCameraInput(
    [property: JsonPropertyOrder(0), JsonRequired] int OrbitAzimuthMicros,
    [property: JsonPropertyOrder(1), JsonRequired] int OrbitElevationMicros,
    [property: JsonPropertyOrder(2), JsonRequired] int ZoomLevel);

public sealed record AzerothNormalizedScaleObservation(
    [property: JsonPropertyOrder(0), JsonRequired] int EarthReferenceMicros,
    [property: JsonPropertyOrder(1), JsonRequired] int AzerothReferenceMicros,
    [property: JsonPropertyOrder(2), JsonRequired] string LinearRatio,
    [property: JsonPropertyOrder(3), JsonRequired] string PhysicalScale);

public sealed record AzerothOrbitalCameraObservation(
    [property: JsonPropertyOrder(0), JsonRequired] int OrbitAzimuthMicros,
    [property: JsonPropertyOrder(1), JsonRequired] int OrbitElevationMicros,
    [property: JsonPropertyOrder(2), JsonRequired] int ZoomLevel,
    [property: JsonPropertyOrder(3), JsonRequired] int ViewportReferenceWidthMicros,
    [property: JsonPropertyOrder(4), JsonRequired] string CoordinateMeaning);

public sealed record AzerothOrbitalLightingObservation(
    [property: JsonPropertyOrder(0), JsonRequired] int SoluneOrbitPhaseMicros,
    [property: JsonPropertyOrder(1), JsonRequired] int LunaraIlluminationMicros,
    [property: JsonPropertyOrder(2), JsonRequired] string LunaraPhase,
    [property: JsonPropertyOrder(3), JsonRequired] int AtmosphereShellSignalMicros,
    [property: JsonPropertyOrder(4), JsonRequired] int ManaVisibilitySignalMicros,
    [property: JsonPropertyOrder(5), JsonRequired] string SolunePresentation,
    [property: JsonPropertyOrder(6), JsonRequired] string LunaraPresentation);

public sealed record AzerothOrbitalZoneObservation(
    [property: JsonPropertyOrder(0), JsonRequired] string ZoneId,
    [property: JsonPropertyOrder(1), JsonRequired] int AtmosphereSignalMicros,
    [property: JsonPropertyOrder(2), JsonRequired] int ManaSignalMicros,
    [property: JsonPropertyOrder(3), JsonRequired] string GlobePlacement);

public sealed record AzerothOrbitalSceneSnapshot(
    [property: JsonPropertyOrder(0), JsonRequired] int Generation,
    [property: JsonPropertyOrder(1), JsonRequired] string Classification,
    [property: JsonPropertyOrder(2), JsonRequired] string GeometryAuthority,
    [property: JsonPropertyOrder(3), JsonRequired] AzerothSimulationDisposition Disposition,
    [property: JsonPropertyOrder(4), JsonRequired] IReadOnlyList<string> ContainmentReasons,
    [property: JsonPropertyOrder(5), JsonRequired] AzerothNormalizedScaleObservation Scale,
    [property: JsonPropertyOrder(6), JsonRequired] AzerothOrbitalCameraObservation Camera,
    [property: JsonPropertyOrder(7), JsonRequired] AzerothOrbitalLightingObservation Lighting,
    [property: JsonPropertyOrder(8), JsonRequired] IReadOnlyList<AzerothOrbitalZoneObservation> Zones,
    [property: JsonPropertyOrder(9), JsonRequired] AzerothEnvironmentalSimulationSnapshot Environment,
    [property: JsonPropertyOrder(10), JsonRequired] AzerothOrbitalSceneInput Input);

public sealed record AzerothOrbitalSceneEnvelope(
    [property: JsonPropertyOrder(0), JsonRequired] string SchemaVersion,
    [property: JsonPropertyOrder(1), JsonRequired] string Authority,
    [property: JsonPropertyOrder(2), JsonRequired] string PayloadSha256,
    [property: JsonPropertyOrder(3), JsonRequired] AzerothOrbitalSceneSnapshot Payload);
