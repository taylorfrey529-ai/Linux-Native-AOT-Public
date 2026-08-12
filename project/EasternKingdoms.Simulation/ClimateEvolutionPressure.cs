namespace EasternKingdoms.Simulation;

public sealed class ClimateEvolutionPressureResolver
{
    public ClimateEvolutionPressure Resolve(
        ClimateZoneState climate,
        LandscapeZoneState landscape)
    {
        ArgumentNullException.ThrowIfNull(climate);
        ArgumentNullException.ThrowIfNull(landscape);
        if (!StringComparer.Ordinal.Equals(climate.ZoneId, landscape.ZoneId))
            throw new InvalidOperationException("Climate pressure requires matching zone IDs.");

        var pressures = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["climate_temperature_tolerance"] = Math.Clamp((climate.Temperature - 0.50) * 2.0, -1.0, 1.0),
            ["climate_moisture_tolerance"] = Math.Clamp((climate.Moisture - 0.50) * 2.0, -1.0, 1.0),
            ["disturbance_resilience"] = Math.Clamp(landscape.DisturbanceLoad, 0.0, 1.0),
            ["habitat_flexibility"] = Math.Clamp(1.0 - (landscape.ForestCover + landscape.WetlandExtent) / 2.0, -1.0, 1.0),
            ["terrain_stability"] = Math.Clamp((landscape.TerrainIntegrity - 0.50) * 2.0, -1.0, 1.0)
        };

        double support = Math.Clamp(
            climate.ClimateStability * 0.45 +
            landscape.TerrainIntegrity * 0.35 +
            (1.0 - landscape.DisturbanceLoad) * 0.20,
            0.0,
            1.0);

        return new ClimateEvolutionPressure(
            climate.ZoneId,
            climate.Generation,
            pressures,
            ReproductiveModifier: Math.Clamp(support * (1.0 - landscape.DisturbanceLoad * 0.25), 0.0, 1.0),
            ViabilityModifier: Math.Clamp(support * (1.0 - landscape.DisturbanceLoad * 0.35), 0.0, 1.0));
    }
}

public static class ClimatePressureIntegrator
{
    public static ZoneEnvironment Apply(ZoneEnvironment zone, ClimateEvolutionPressure pressure)
    {
        ArgumentNullException.ThrowIfNull(zone);
        ArgumentNullException.ThrowIfNull(pressure);
        if (!StringComparer.Ordinal.Equals(zone.ZoneId, pressure.ZoneId))
            throw new InvalidOperationException("Climate pressure and zone IDs must match.");

        var merged = zone.TraitPressures.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
        foreach (var item in pressure.TraitPressures.OrderBy(x => x.Key, StringComparer.Ordinal))
            merged[item.Key] = Math.Clamp(merged.GetValueOrDefault(item.Key) + item.Value, -1.0, 1.0);

        return zone with
        {
            PopulationHealth = Math.Clamp(zone.PopulationHealth * (0.75 + pressure.ViabilityModifier * 0.25), 0.0, 1.0),
            Stability = Math.Clamp(zone.Stability * (0.80 + pressure.ReproductiveModifier * 0.20), 0.0, 1.0),
            TraitPressures = merged
        };
    }
}
