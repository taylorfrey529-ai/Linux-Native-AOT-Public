using EasternKingdoms.Simulation;

namespace Evermore.Azeroth.Simulation;

public sealed class AzerothOrbitalSceneEngine
{
    public const int UnitMicros = 1_000_000;
    public const int MinimumElevationMicros = -250_000;
    public const int MaximumElevationMicros = 250_000;
    public const int MaximumZoomLevel = 8;
    public const string Classification = "ConstrainedSimulationConceptOnly";
    public const string GeometryAuthority = "authored-globe-vector-required";
    public const string LinearRatio = "1.000000 : 1.000000";
    public const string PhysicalScale = "unavailable-no-authority";
    public const string CoordinateMeaning = "renderer-orbit-reference-only-not-latitude-longitude";
    public const string MissingGlobePlacement = "missing-authored-globe-vector";

    private readonly AzerothEnvironmentalFieldEngine _environment;

    public AzerothOrbitalSceneEngine(AzerothEnvironmentalFieldEngine? environment = null)
    {
        _environment = environment ?? new AzerothEnvironmentalFieldEngine();
    }

    public AzerothOrbitalSceneSnapshot Process(
        AzerothOrbitalSceneInput input,
        CancellationToken cancellationToken = default)
    {
        ValidateInput(input);
        cancellationToken.ThrowIfCancellationRequested();

        AzerothEnvironmentalSimulationSnapshot environment =
            _environment.Process(input.Environment, cancellationToken);
        AzerothEnvironmentalFieldSnapshot field = environment.Environment;
        var scale = new AzerothNormalizedScaleObservation(
            UnitMicros,
            UnitMicros,
            LinearRatio,
            PhysicalScale);
        var camera = new AzerothOrbitalCameraObservation(
            input.Camera.OrbitAzimuthMicros,
            input.Camera.OrbitElevationMicros,
            input.Camera.ZoomLevel,
            UnitMicros / (1 << input.Camera.ZoomLevel),
            CoordinateMeaning);

        if (field.Disposition == AzerothSimulationDisposition.Contained)
        {
            return new AzerothOrbitalSceneSnapshot(
                field.Generation,
                Classification,
                GeometryAuthority,
                field.Disposition,
                field.ContainmentReasons.ToArray(),
                scale,
                camera,
                CreateLighting(environment, 0, 0),
                Array.Empty<AzerothOrbitalZoneObservation>(),
                environment,
                input);
        }

        AzerothOrbitalZoneObservation[] zones = field.Zones
            .Select(zone => new AzerothOrbitalZoneObservation(
                zone.ZoneId,
                zone.AtmosphereSignalMicros,
                zone.ManaSignalMicros,
                MissingGlobePlacement))
.OrderBy(zone => zone.ZoneId, StringComparer.Ordinal)
            .ToArray();
        AzerothRegionAuthority.RequireExactSet(zones.Select(zone => zone.ZoneId));
        int atmosphere = Average(zones.Select(zone => zone.AtmosphereSignalMicros));
        int mana = Average(zones.Select(zone => zone.ManaSignalMicros));

        return new AzerothOrbitalSceneSnapshot(
            field.Generation,
            Classification,
            GeometryAuthority,
            field.Disposition,
            field.ContainmentReasons.ToArray(),
            scale,
            camera,
            CreateLighting(environment, atmosphere, mana),
            zones,
            environment,
            input);
    }

    private static AzerothOrbitalLightingObservation CreateLighting(
        AzerothEnvironmentalSimulationSnapshot environment,
        int atmosphere,
        int mana) =>
        new(
            environment.Simulation.Solune.OrbitPhaseMicros,
            environment.Simulation.Lunara.IlluminationMicros,
            environment.Simulation.Lunara.LunarPhase,
            atmosphere,
            mana,
            "phase-observation-only",
            "illumination-observation-only-unpositioned");

    private static void ValidateInput(AzerothOrbitalSceneInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Environment);
        ArgumentNullException.ThrowIfNull(input.Camera);
        if (input.Camera.OrbitAzimuthMicros < 0 || input.Camera.OrbitAzimuthMicros >= UnitMicros)
        {
            throw new ArgumentOutOfRangeException(
                nameof(input),
                $"Orbit azimuth must be between 0 and {UnitMicros - 1} normalized micro-units.");
        }
        if (input.Camera.OrbitElevationMicros < MinimumElevationMicros ||
            input.Camera.OrbitElevationMicros > MaximumElevationMicros)
        {
            throw new ArgumentOutOfRangeException(
                nameof(input),
                $"Orbit elevation must be between {MinimumElevationMicros} and {MaximumElevationMicros} normalized micro-units.");
        }
        if (input.Camera.ZoomLevel < 0 || input.Camera.ZoomLevel > MaximumZoomLevel)
        {
            throw new ArgumentOutOfRangeException(
                nameof(input),
                $"Orbital zoom level must be between 0 and {MaximumZoomLevel}.");
        }
    }

    private static int Average(IEnumerable<int> values)
    {
        int[] materialized = values.ToArray();
        if (materialized.Length == 0)
            throw new InvalidDataException("A completed environmental field must contain authorized zone observations.");
        return checked((int)(materialized.Aggregate(0L, (sum, value) => sum + value) / materialized.Length));
    }
}

