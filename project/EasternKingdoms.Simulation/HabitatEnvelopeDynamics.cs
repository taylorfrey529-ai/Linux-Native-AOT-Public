namespace EasternKingdoms.Simulation;

public sealed class HabitatEnvelopeTracker
{
    private readonly GeoclimateSettings _settings;
    private readonly Dictionary<string, HashSet<string>> _previousSuitable = new(StringComparer.Ordinal);

    public HabitatEnvelopeTracker(GeoclimateSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _settings.Validate();
    }

    public (IReadOnlyList<HabitatEnvelopeZoneState> States, IReadOnlyList<HabitatEnvelopeMigrationEvent> Events) Evaluate(
        HabitatEnvelopeDefinition envelope,
        int generation,
        IReadOnlyDictionary<string, ClimateZoneState> climate,
        IReadOnlyDictionary<string, LandscapeZoneState> landscapes,
        IReadOnlyList<HabitatCorridorState> corridors)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(climate);
        ArgumentNullException.ThrowIfNull(landscapes);
        ArgumentNullException.ThrowIfNull(corridors);

        var states = new List<HabitatEnvelopeZoneState>();
        foreach (string zoneId in climate.Keys.OrderBy(x => x, StringComparer.Ordinal))
        {
            if (envelope.AllowedZoneIds.Count > 0 && !envelope.AllowedZoneIds.Contains(zoneId))
                continue;
            if (!landscapes.TryGetValue(zoneId, out var landscape))
                throw new InvalidOperationException($"Habitat envelope {envelope.EnvelopeId} lacks landscape state for {zoneId}.");

            var c = climate[zoneId];
            double temperatureFit = AxisFit(c.Temperature, envelope.OptimalTemperature, envelope.TemperatureTolerance);
            double moistureFit = AxisFit(c.Moisture, envelope.OptimalMoisture, envelope.MoistureTolerance);
            double forestFit = MinimumFit(landscape.ForestCover, envelope.MinimumForestCover);
            double wetlandFit = MinimumFit(landscape.WetlandExtent, envelope.MinimumWetlandExtent);
            double disturbanceFit = landscape.DisturbanceLoad <= envelope.MaximumDisturbance
                ? 1.0
                : Math.Clamp(1.0 - (landscape.DisturbanceLoad - envelope.MaximumDisturbance) /
                    Math.Max(0.0001, 1.0 - envelope.MaximumDisturbance), 0.0, 1.0);

            double suitability = Math.Clamp(
                temperatureFit * 0.30 +
                moistureFit * 0.30 +
                forestFit * 0.15 +
                wetlandFit * 0.10 +
                disturbanceFit * 0.15,
                0.0,
                1.0);

            states.Add(new HabitatEnvelopeZoneState(
                envelope.EnvelopeId,
                zoneId,
                generation,
                suitability,
                suitability >= _settings.EnvelopeSuitabilityThreshold));
        }

        var current = states.Where(x => x.Suitable).Select(x => x.ZoneId).ToHashSet(StringComparer.Ordinal);
        _previousSuitable.TryGetValue(envelope.EnvelopeId, out var previous);
        previous ??= new HashSet<string>(StringComparer.Ordinal);

        var events = new List<HabitatEnvelopeMigrationEvent>();
        foreach (string lost in previous.Except(current, StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal))
        {
            events.Add(new HabitatEnvelopeMigrationEvent(
                $"envelope:{envelope.EnvelopeId}:contract:{generation}:{lost}",
                envelope.EnvelopeId,
                envelope.SpeciesFamilyId,
                envelope.LineageId,
                HabitatEnvelopeShiftKind.Contraction,
                lost,
                lost,
                null,
                generation,
                PotentialOnly: true));
        }

        foreach (string destination in current.Except(previous, StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal))
        {
            if (previous.Count == 0)
                continue;

            var route = corridors
                .Where(x => x.Open)
                .OrderBy(x => x.RouteId, StringComparer.Ordinal)
                .FirstOrDefault(x => previous.Any(source => Connects(x, source, destination)));

            if (route is not null)
            {
                string source = previous.OrderBy(x => x, StringComparer.Ordinal)
                    .First(x => Connects(route, x, destination));
                events.Add(new HabitatEnvelopeMigrationEvent(
                    $"envelope:{envelope.EnvelopeId}:expand:{generation}:{destination}",
                    envelope.EnvelopeId,
                    envelope.SpeciesFamilyId,
                    envelope.LineageId,
                    HabitatEnvelopeShiftKind.ExpansionOpportunity,
                    source,
                    destination,
                    route.RouteId,
                    generation,
                    PotentialOnly: true));
            }
            else
            {
                events.Add(new HabitatEnvelopeMigrationEvent(
                    $"envelope:{envelope.EnvelopeId}:blocked:{generation}:{destination}",
                    envelope.EnvelopeId,
                    envelope.SpeciesFamilyId,
                    envelope.LineageId,
                    HabitatEnvelopeShiftKind.RouteBlocked,
                    null,
                    destination,
                    null,
                    generation,
                    PotentialOnly: true));
            }
        }

        _previousSuitable[envelope.EnvelopeId] = current;
        return (
            states.OrderBy(x => x.ZoneId, StringComparer.Ordinal).ToArray(),
            events.OrderBy(x => x.EventId, StringComparer.Ordinal).ToArray());
    }

    private static bool Connects(HabitatCorridorState route, string source, string destination) =>
        (StringComparer.Ordinal.Equals(route.FromZoneId, source) && StringComparer.Ordinal.Equals(route.ToZoneId, destination)) ||
        (StringComparer.Ordinal.Equals(route.ToZoneId, source) && StringComparer.Ordinal.Equals(route.FromZoneId, destination));

    private static double AxisFit(double value, double optimum, double tolerance) =>
        Math.Clamp(1.0 - Math.Abs(value - optimum) / Math.Max(0.0001, tolerance), 0.0, 1.0);

    private static double MinimumFit(double value, double minimum) =>
        minimum <= 0.0 ? 1.0 : Math.Clamp(value / minimum, 0.0, 1.0);
}
