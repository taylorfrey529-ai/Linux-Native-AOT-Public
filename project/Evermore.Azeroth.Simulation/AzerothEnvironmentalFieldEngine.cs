using EasternKingdoms.Simulation;

namespace Evermore.Azeroth.Simulation;

public sealed class AzerothEnvironmentalFieldEngine
{
    public const int UnitMicros = 1_000_000;
    public const long MaximumSolarReferenceIrradianceMilliwattsPerSquareMeter = long.MaxValue / UnitMicros;
    public const string Units = "normalized-micro-units";
    public const string Model = "authored-zone-signal-compositor/1.0";
    public const string SpatialAuthority = "authorized-zone-identifiers-only";

    private readonly AzerothSimulationEngine _simulation;

    public AzerothEnvironmentalFieldEngine(AzerothSimulationEngine? simulation = null)
    {
        _simulation = simulation ?? new AzerothSimulationEngine();
    }

    public AzerothEnvironmentalSimulationSnapshot Process(
        AzerothEnvironmentalSimulationInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Simulation);
        ArgumentNullException.ThrowIfNull(input.Environment);
        ArgumentNullException.ThrowIfNull(input.Environment.Zones);

        cancellationToken.ThrowIfCancellationRequested();
        AzerothSimulationSnapshot simulation = _simulation.Process(input.Simulation, cancellationToken);
        ValidateInput(input.Environment, simulation);

        if (simulation.Disposition == AzerothSimulationDisposition.Contained)
        {
            return CreateSnapshot(
                input,
                simulation,
                new AzerothEnvironmentalDrivers(0, 0, 0, 0),
                Array.Empty<AzerothZoneFieldObservation>());
        }

        var drivers = new AzerothEnvironmentalDrivers(
            NormalizeIrradiance(
                simulation.Solune.IrradianceMilliwattsPerSquareMeter,
                input.Environment.SolarReferenceIrradianceMilliwattsPerSquareMeter),
            ValidateDerivedMicros(simulation.Lunara.TideLevelMicros, "Lunara tide"),
            ValidateDerivedMicros(simulation.Lunara.ResonanceMicros, "Lunara resonance"),
            NormalizeFitness(simulation.Omega.AverageFitness));

        AzerothZoneFieldObservation[] zones = input.Environment.Zones
            .Select(zone => ComposeZone(zone, drivers))
            .OrderBy(zone => zone.ZoneId, StringComparer.Ordinal)
            .ToArray();
        return CreateSnapshot(input, simulation, drivers, zones);
    }

    private static AzerothEnvironmentalSimulationSnapshot CreateSnapshot(
        AzerothEnvironmentalSimulationInput input,
        AzerothSimulationSnapshot simulation,
        AzerothEnvironmentalDrivers drivers,
        IReadOnlyList<AzerothZoneFieldObservation> zones) =>
        new(
            simulation,
            new AzerothEnvironmentalFieldSnapshot(
                simulation.Generation,
                Units,
                Model,
                SpatialAuthority,
                simulation.Disposition,
                simulation.ContainmentReasons.ToArray(),
                drivers,
                zones),
            input);

    private static AzerothZoneFieldObservation ComposeZone(
        AzerothZoneFieldInput zone,
        AzerothEnvironmentalDrivers drivers)
    {
        int soluneAtmosphere = Contribution(
            drivers.SoluneIrradianceMicros,
            zone.SoluneAtmosphereResponseMicros);
        int lunaraAtmosphere = Contribution(
            drivers.LunaraTideMicros,
            zone.LunaraAtmosphereResponseMicros);
        int lunaraMana = Contribution(
            drivers.LunaraResonanceMicros,
            zone.LunaraManaResponseMicros);
        int omegaMana = Contribution(
            drivers.OmegaFitnessMicros,
            zone.OmegaManaResponseMicros);

        return new AzerothZoneFieldObservation(
            zone.ZoneId,
            SaturatingSignal(zone.AtmosphereBaselineMicros, soluneAtmosphere, lunaraAtmosphere),
            SaturatingSignal(zone.ManaBaselineMicros, lunaraMana, omegaMana),
            soluneAtmosphere,
            lunaraAtmosphere,
            lunaraMana,
            omegaMana);
    }

    private static void ValidateInput(
        AzerothEnvironmentalFieldInput field,
        AzerothSimulationSnapshot simulation)
    {
        if (field.Generation != simulation.Generation)
        {
            throw new ArgumentException(
                "The environmental field and composed simulation must observe the same Azeroth generation.",
                nameof(field));
        }
        if (field.SolarReferenceIrradianceMilliwattsPerSquareMeter <= 0 ||
            field.SolarReferenceIrradianceMilliwattsPerSquareMeter >
                MaximumSolarReferenceIrradianceMilliwattsPerSquareMeter)
        {
            throw new ArgumentOutOfRangeException(
                nameof(field),
                $"The authored solar reference must be between 1 and {MaximumSolarReferenceIrradianceMilliwattsPerSquareMeter} mW/m2.");
        }

        AzerothRegionAuthority.RequireExactSet(
            simulation.Azeroth.Zones.Select(zone => zone.ZoneId));
        string[] expectedZones = AzerothRegionAuthority.CreateOrderedZoneIds();
        string[] suppliedZones = field.Zones
            .Select(zone => zone?.ZoneId ?? string.Empty)
            .ToArray();
        if (suppliedZones.Any(string.IsNullOrWhiteSpace) ||
            suppliedZones.Distinct(StringComparer.Ordinal).Count() != suppliedZones.Length ||
            !suppliedZones.SequenceEqual(
                suppliedZones.OrderBy(zoneId => zoneId, StringComparer.Ordinal),
                StringComparer.Ordinal) ||
            !suppliedZones.SequenceEqual(expectedZones, StringComparer.Ordinal))
        {
            throw new ArgumentException(
                "Environmental field zones must exactly match the four negotiated Azeroth region identifiers in wire order.",
                nameof(field));
        }

        foreach (AzerothZoneFieldInput zone in field.Zones)
        {
            ValidateInputMicros(zone.AtmosphereBaselineMicros, zone.ZoneId, nameof(zone.AtmosphereBaselineMicros));
            ValidateInputMicros(zone.ManaBaselineMicros, zone.ZoneId, nameof(zone.ManaBaselineMicros));
            ValidateInputMicros(zone.SoluneAtmosphereResponseMicros, zone.ZoneId, nameof(zone.SoluneAtmosphereResponseMicros));
            ValidateInputMicros(zone.LunaraAtmosphereResponseMicros, zone.ZoneId, nameof(zone.LunaraAtmosphereResponseMicros));
            ValidateInputMicros(zone.LunaraManaResponseMicros, zone.ZoneId, nameof(zone.LunaraManaResponseMicros));
            ValidateInputMicros(zone.OmegaManaResponseMicros, zone.ZoneId, nameof(zone.OmegaManaResponseMicros));
        }
    }

    private static void ValidateInputMicros(int value, string zoneId, string fieldName)
    {
        if (value < 0 || value > UnitMicros)
            throw new ArgumentOutOfRangeException(fieldName, $"Zone {zoneId} values must be between 0 and {UnitMicros} micros.");
    }

    private static int ValidateDerivedMicros(int value, string source)
    {
        if (value < 0 || value > UnitMicros)
            throw new InvalidDataException($"{source} observation is outside normalized micro-unit bounds.");
        return value;
    }

    private static int NormalizeIrradiance(long irradiance, long reference)
    {
        if (irradiance < 0)
            throw new InvalidDataException("Solune irradiance cannot be negative.");
        if (irradiance >= reference)
            return UnitMicros;
        return checked((int)((irradiance * UnitMicros) / reference));
    }

    private static int NormalizeFitness(float fitness)
    {
        if (!float.IsFinite(fitness))
            throw new InvalidDataException("Omega average fitness must be finite.");
        float bounded = Math.Clamp(fitness, 0.0f, 1.0f);
        return checked((int)MathF.Round(bounded * UnitMicros, MidpointRounding.ToEven));
    }

    private static int Contribution(int driver, int response) =>
        checked((int)(((long)driver * response) / UnitMicros));

    private static int SaturatingSignal(int baseline, int first, int second) =>
        Math.Min(UnitMicros, checked(baseline + first + second));
}

