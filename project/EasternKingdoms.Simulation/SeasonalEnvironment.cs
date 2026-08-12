namespace EasternKingdoms.Simulation;

public sealed record EnvironmentalPulseDefinition(
    string PulseId,
    int PeriodGenerations,
    int DurationGenerations,
    int PhaseOffset,
    IReadOnlySet<string> TargetZoneIds,
    double PopulationHealthDelta,
    double StabilityDelta,
    double LuminosityDelta,
    double IntegrityDelta,
    double ResonanceDebtDelta,
    IReadOnlyDictionary<string, double> TraitPressureDeltas);

public sealed record AppliedEnvironmentalPulse(
    string PulseId,
    int Generation,
    string ZoneId);

public sealed record SeasonalEnvironmentResult(
    int Generation,
    IReadOnlyList<ZoneEnvironment> Zones,
    IReadOnlyList<AppliedEnvironmentalPulse> AppliedPulses);

public sealed class SeasonalEnvironmentEngine
{
    public SeasonalEnvironmentResult Apply(
        IReadOnlyList<ZoneEnvironment> baseZones,
        int generation,
        IEnumerable<EnvironmentalPulseDefinition> pulses)
    {
        ArgumentNullException.ThrowIfNull(baseZones);
        ArgumentNullException.ThrowIfNull(pulses);
        if (generation < 0)
            throw new ArgumentOutOfRangeException(nameof(generation));

        var zoneMap = baseZones.ToDictionary(x => x.ZoneId, x => x, StringComparer.Ordinal);
        var applied = new List<AppliedEnvironmentalPulse>();

        foreach (var pulse in pulses.OrderBy(x => x.PulseId, StringComparer.Ordinal))
        {
            Validate(pulse);
            if (!IsActive(pulse, generation))
                continue;

            IEnumerable<string> targets = pulse.TargetZoneIds.Count == 0
                ? zoneMap.Keys.OrderBy(x => x, StringComparer.Ordinal)
                : pulse.TargetZoneIds.OrderBy(x => x, StringComparer.Ordinal);

            foreach (string zoneId in targets)
            {
                if (!zoneMap.TryGetValue(zoneId, out var zone))
                    throw new InvalidOperationException(
                        $"Environmental pulse {pulse.PulseId} targets unknown zone {zoneId}.");

                if (zone.SimulationState == CellSimulationState.Locked)
                    continue;

                var pressures = zone.TraitPressures
                    .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
                foreach (var delta in pulse.TraitPressureDeltas.OrderBy(x => x.Key, StringComparer.Ordinal))
                {
                    pressures[delta.Key] = Math.Clamp(
                        pressures.GetValueOrDefault(delta.Key) + delta.Value,
                        -1.0,
                        1.0);
                }

                zoneMap[zoneId] = zone with
                {
                    PopulationHealth = Clamp01(zone.PopulationHealth + pulse.PopulationHealthDelta),
                    Stability = Clamp01(zone.Stability + pulse.StabilityDelta),
                    Luminosity = Clamp01(zone.Luminosity + pulse.LuminosityDelta),
                    Integrity = Clamp01(zone.Integrity + pulse.IntegrityDelta),
                    ResonanceDebt = Clamp01(zone.ResonanceDebt + pulse.ResonanceDebtDelta),
                    TraitPressures = pressures
                };
                applied.Add(new AppliedEnvironmentalPulse(pulse.PulseId, generation, zoneId));
            }
        }

        return new SeasonalEnvironmentResult(
            generation,
            zoneMap.Values.OrderBy(x => x.ZoneId, StringComparer.Ordinal).ToArray(),
            applied.OrderBy(x => x.PulseId, StringComparer.Ordinal)
                .ThenBy(x => x.ZoneId, StringComparer.Ordinal)
                .ToArray());
    }

    private static bool IsActive(EnvironmentalPulseDefinition pulse, int generation)
    {
        int relative = generation - pulse.PhaseOffset;
        if (relative < 0)
            return false;
        int cycle = relative % pulse.PeriodGenerations;
        return cycle < pulse.DurationGenerations;
    }

    private static void Validate(EnvironmentalPulseDefinition pulse)
    {
        if (string.IsNullOrWhiteSpace(pulse.PulseId))
            throw new InvalidOperationException("Environmental pulse requires an ID.");
        if (pulse.PeriodGenerations <= 0)
            throw new InvalidOperationException($"Pulse {pulse.PulseId} requires a positive period.");
        if (pulse.DurationGenerations <= 0 || pulse.DurationGenerations > pulse.PeriodGenerations)
            throw new InvalidOperationException($"Pulse {pulse.PulseId} has an invalid duration.");
        if (pulse.PhaseOffset < 0)
            throw new InvalidOperationException($"Pulse {pulse.PulseId} has a negative phase offset.");
        foreach (var pressure in pulse.TraitPressureDeltas)
        {
            if (string.IsNullOrWhiteSpace(pressure.Key) || pressure.Value is < -1.0 or > 1.0)
                throw new InvalidOperationException($"Pulse {pulse.PulseId} contains an invalid trait pressure delta.");
        }
    }

    private static double Clamp01(double value) => Math.Clamp(value, 0.0, 1.0);
}
