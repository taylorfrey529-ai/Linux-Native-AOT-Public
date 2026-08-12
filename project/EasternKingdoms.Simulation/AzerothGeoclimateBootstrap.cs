using Evermore.Genetics;

namespace EasternKingdoms.Simulation;

public sealed record AzerothGeoclimateEvolutionRuntime(
    AzerothBiogeographicEvolutionRuntime BiogeographicEvolution,
    DeepTimeGeoclimateRuntime Geoclimate,
    GeoclimateScenarioCatalog Scenario);

public static class AzerothGeoclimateEvolutionBootstrap
{
    public static AzerothGeoclimateEvolutionRuntime Create(
        GenomeCatalog genomeCatalog,
        LineageDivergenceSettings? lineageSettings = null,
        DeepTimeSettings? deepTimeSettings = null,
        BiogeographySettings? biogeographySettings = null,
        GeoclimateSettings? geoclimateSettings = null,
        GeoclimateScenarioCatalog? scenario = null)
    {
        ArgumentNullException.ThrowIfNull(genomeCatalog);
        var zones = AzerothEvolutionBootstrap.CreateNorthernScarCorridor();
        var routes = NorthernScarPopulationRoutes.Create();
        var activeScenario = scenario ?? new GeoclimateScenarioCatalog();

        return new AzerothGeoclimateEvolutionRuntime(
            AzerothBiogeographicEvolutionBootstrap.Create(
                genomeCatalog,
                lineageSettings,
                deepTimeSettings,
                biogeographySettings),
            new DeepTimeGeoclimateRuntime(
                zones.Select(x => x.ZoneId),
                routes,
                CreatePrototypeNeutralClimateBaselines(zones),
                CreatePrototypeNeutralLandscapeBaselines(zones),
                activeScenario,
                geoclimateSettings),
            activeScenario);
    }

    public static IReadOnlyList<ClimateZoneBaseline> CreatePrototypeNeutralClimateBaselines(
        IEnumerable<ZoneEnvironment> zones) =>
        zones.OrderBy(x => x.ZoneId, StringComparer.Ordinal)
            .Select(x => new ClimateZoneBaseline(
                x.ZoneId,
                Temperature: 0.50,
                Moisture: 0.50,
                Storminess: 0.25,
                Seasonality: 0.50,
                ResonanceBackground: Math.Clamp(x.ResonanceDebt, 0.0, 1.0)))
            .ToArray();

    public static IReadOnlyList<LandscapeBaseline> CreatePrototypeNeutralLandscapeBaselines(
        IEnumerable<ZoneEnvironment> zones) =>
        zones.OrderBy(x => x.ZoneId, StringComparer.Ordinal)
            .Select(x => new LandscapeBaseline(
                x.ZoneId,
                ForestPotential: 0.50,
                WetlandPotential: 0.25,
                RiverConnectivity: 0.25,
                CoastalExposure: 0.0,
                TerrainIntegrity: 0.75,
                ErosionResistance: 0.70))
            .ToArray();
}
