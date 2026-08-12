namespace EasternKingdoms.Simulation;

public sealed class LandscapeDynamicsLedger
{
    private readonly Dictionary<string, LandscapeZoneState> _states = new(StringComparer.Ordinal);

    public IReadOnlyCollection<LandscapeZoneState> States =>
        _states.Values.OrderBy(x => x.ZoneId, StringComparer.Ordinal).ToArray();

    public LandscapeZoneState Advance(
        LandscapeBaseline baseline,
        ClimateZoneState climate,
        ZoneEnvironment zone,
        IEnumerable<DisturbanceEpochDefinition> disturbances,
        int generation)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(climate);
        ArgumentNullException.ThrowIfNull(zone);
        ArgumentNullException.ThrowIfNull(disturbances);
        ValidateBaseline(baseline);
        if (!StringComparer.Ordinal.Equals(baseline.ZoneId, climate.ZoneId) ||
            !StringComparer.Ordinal.Equals(baseline.ZoneId, zone.ZoneId))
            throw new InvalidOperationException("Landscape, climate, and zone IDs must match.");

        var active = disturbances
            .Where(x => generation >= x.StartGeneration && generation <= x.EndGeneration)
            .Where(x => x.TargetZoneIds.Count == 0 || x.TargetZoneIds.Contains(zone.ZoneId))
            .OrderBy(x => x.DisturbanceId, StringComparer.Ordinal)
            .ToArray();

        double activeSeverity = 1.0;
        foreach (var disturbance in active)
            activeSeverity *= 1.0 - disturbance.Severity;
        activeSeverity = 1.0 - activeSeverity;

        _states.TryGetValue(zone.ZoneId, out var previous);
        double previousDisturbance = previous?.DisturbanceLoad ?? 0.0;
        double disturbanceLoad = Math.Clamp(Math.Max(activeSeverity, previousDisturbance * 0.72), 0.0, 1.0);

        double targetForest = Clamp01(
            baseline.ForestPotential * (0.35 + climate.Moisture * 0.65) * (1.0 - disturbanceLoad * 0.60));
        double targetWetland = Clamp01(
            baseline.WetlandPotential * (0.35 + climate.Moisture * 0.65) * (0.80 + climate.Storminess * 0.20));
        double targetRiver = Clamp01(
            baseline.RiverConnectivity + (climate.Moisture - 0.50) * 0.30 + climate.Storminess * 0.10 - disturbanceLoad * 0.15);
        double targetTerrain = Clamp01(
            baseline.TerrainIntegrity * baseline.ErosionResistance +
            climate.ClimateStability * 0.20 -
            climate.Storminess * 0.15 -
            disturbanceLoad * 0.35);
        double targetCoast = baseline.CoastalExposure <= 0.0
            ? 0.0
            : Clamp01(
                baseline.CoastalExposure + climate.Storminess * 0.15 + disturbanceLoad * 0.10 - targetTerrain * 0.10);

        double inertia = zone.SimulationState == CellSimulationState.Locked ? 0.0 : 0.25;
        var next = new LandscapeZoneState(
            zone.ZoneId,
            generation,
            Lerp(previous?.ForestCover ?? baseline.ForestPotential, targetForest, inertia),
            Lerp(previous?.WetlandExtent ?? baseline.WetlandPotential, targetWetland, inertia),
            Lerp(previous?.RiverConnectivity ?? baseline.RiverConnectivity, targetRiver, inertia),
            Lerp(previous?.CoastalExposure ?? baseline.CoastalExposure, targetCoast, inertia),
            Lerp(previous?.TerrainIntegrity ?? baseline.TerrainIntegrity, targetTerrain, inertia),
            zone.SimulationState == CellSimulationState.Locked ? previousDisturbance : disturbanceLoad,
            zone.SimulationState == CellSimulationState.Locked
                ? Array.Empty<string>()
                : active.Select(x => x.DisturbanceId).ToArray());

        _states[zone.ZoneId] = next;
        return next;
    }

    private static void ValidateBaseline(LandscapeBaseline baseline)
    {
        if (string.IsNullOrWhiteSpace(baseline.ZoneId))
            throw new InvalidOperationException("Landscape baseline requires a zone ID.");
        foreach (double value in new[]
                 {
                     baseline.ForestPotential,
                     baseline.WetlandPotential,
                     baseline.RiverConnectivity,
                     baseline.CoastalExposure,
                     baseline.TerrainIntegrity,
                     baseline.ErosionResistance
                 })
        {
            if (value is < 0.0 or > 1.0)
                throw new InvalidOperationException($"Landscape baseline {baseline.ZoneId} contains a value outside [0, 1].");
        }
    }

    private static double Lerp(double from, double to, double amount) =>
        Math.Clamp(from + (to - from) * amount, 0.0, 1.0);

    private static double Clamp01(double value) => Math.Clamp(value, 0.0, 1.0);
}

public sealed class LandscapeChangeTracker
{
    private readonly double _threshold;
    private readonly Dictionary<string, LandscapeZoneState> _previous = new(StringComparer.Ordinal);

    public LandscapeChangeTracker(double threshold)
    {
        if (threshold is <= 0.0 or > 1.0)
            throw new InvalidOperationException("Landscape change threshold must remain inside (0, 1].");
        _threshold = threshold;
    }

    public IReadOnlyList<LandscapeChangeEvent> Observe(LandscapeZoneState current)
    {
        ArgumentNullException.ThrowIfNull(current);
        var events = new List<LandscapeChangeEvent>();
        if (_previous.TryGetValue(current.ZoneId, out var previous))
        {
            Add(events, current, previous.ForestCover, current.ForestCover, LandscapeChangeKind.ForestAdvance, LandscapeChangeKind.ForestRetreat, "forest");
            Add(events, current, previous.WetlandExtent, current.WetlandExtent, LandscapeChangeKind.WetlandExpansion, LandscapeChangeKind.WetlandContraction, "wetland");
            Add(events, current, previous.RiverConnectivity, current.RiverConnectivity, LandscapeChangeKind.RiverConnectivityGain, LandscapeChangeKind.RiverConnectivityLoss, "river");
            Add(events, current, previous.CoastalExposure, current.CoastalExposure, LandscapeChangeKind.CoastalExposureIncrease, LandscapeChangeKind.CoastalExposureDecrease, "coast");
            Add(events, current, previous.TerrainIntegrity, current.TerrainIntegrity, LandscapeChangeKind.TerrainRecovery, LandscapeChangeKind.TerrainDegradation, "terrain");
        }
        _previous[current.ZoneId] = current;
        return events.OrderBy(x => x.EventId, StringComparer.Ordinal).ToArray();
    }

    private void Add(
        ICollection<LandscapeChangeEvent> output,
        LandscapeZoneState current,
        double previous,
        double next,
        LandscapeChangeKind increase,
        LandscapeChangeKind decrease,
        string component)
    {
        double delta = next - previous;
        if (Math.Abs(delta) < _threshold)
            return;
        var kind = delta > 0.0 ? increase : decrease;
        output.Add(new LandscapeChangeEvent(
            $"landscape:{current.ZoneId}:{component}:{current.Generation}:{kind}",
            current.ZoneId,
            kind,
            current.Generation,
            previous,
            next,
            delta));
    }
}
