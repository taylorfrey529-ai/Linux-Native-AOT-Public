using EasternKingdoms.Simulation;
using Xunit;

namespace EasternKingdoms.Simulation.Tests;

public sealed class GeoclimateDeepTimeTests
{
    [Fact]
    public void ClimateEpochResolutionIsDeterministicRegardlessOfInputOrder()
    {
        var baseline = Climate("zone.a", 0.5, 0.5);
        var a = Epoch("a", 1, 3, "zone.a", temperature: 0.2, moisture: -0.1);
        var b = Epoch("b", 1, 3, "zone.a", temperature: -0.1, moisture: 0.2);
        var resolver = new PaleoclimateResolver();

        var left = resolver.Resolve(baseline, 2, new[] { b, a });
        var right = resolver.Resolve(baseline, 2, new[] { a, b });

        Assert.Equal(left.Regime, right.Regime);
        Assert.Equal(left.Temperature, right.Temperature);
        Assert.Equal(left.Moisture, right.Moisture);
        Assert.Equal(left.Storminess, right.Storminess);
        Assert.Equal(left.Seasonality, right.Seasonality);
        Assert.Equal(left.ResonanceBackground, right.ResonanceBackground);
        Assert.Equal(left.ClimateStability, right.ClimateStability);
        Assert.Equal(new[] { "a", "b" }, left.ActiveEpochIds);
        Assert.Equal(left.ActiveEpochIds, right.ActiveEpochIds);
    }

    [Fact]
    public void ScenarioCannotReferenceUnauthorizedZone()
    {
        var scenario = new GeoclimateScenarioCatalog();
        scenario.RegisterClimateEpoch(Epoch("bad", 1, 2, "zone.external", 0.1, 0.0));

        Assert.Throws<InvalidOperationException>(() => Runtime(scenario));
    }

    [Fact]
    public void LockedCellFreezesLandscapePhysicalState()
    {
        var scenario = new GeoclimateScenarioCatalog();
        scenario.RegisterDisturbance(new DisturbanceEpochDefinition(
            "shock",
            DisturbanceEpochKind.CompoundCatastrophe,
            2,
            2,
            new HashSet<string>(StringComparer.Ordinal) { "zone.a" },
            1.0));
        var runtime = Runtime(scenario);

        var first = runtime.Advance(Batch(1, Zone("zone.a", CellSimulationState.Warm), Zone("zone.b", CellSimulationState.Warm)));
        var second = runtime.Advance(Batch(2, Zone("zone.a", CellSimulationState.Locked), Zone("zone.b", CellSimulationState.Warm)));
        var a1 = first.Landscapes.Single(x => x.ZoneId == "zone.a");
        var a2 = second.Landscapes.Single(x => x.ZoneId == "zone.a");

        Assert.Equal(a1.ForestCover, a2.ForestCover);
        Assert.Equal(a1.WetlandExtent, a2.WetlandExtent);
        Assert.Equal(a1.RiverConnectivity, a2.RiverConnectivity);
        Assert.Equal(a1.CoastalExposure, a2.CoastalExposure);
        Assert.Equal(a1.TerrainIntegrity, a2.TerrainIntegrity);
        Assert.Equal(a1.DisturbanceLoad, a2.DisturbanceLoad);
    }

    [Fact]
    public void HabitatEnvelopeExpansionRequiresAuthorizedOpenCorridor()
    {
        var scenario = new GeoclimateScenarioCatalog();
        scenario.RegisterClimateEpoch(Epoch("warming", 2, 2, "zone.b", temperature: 0.4, moisture: 0.0));
        scenario.RegisterHabitatEnvelope(new HabitatEnvelopeDefinition(
            "env.a",
            "family.a",
            "lineage.a",
            OptimalTemperature: 0.50,
            TemperatureTolerance: 0.20,
            OptimalMoisture: 0.50,
            MoistureTolerance: 0.30,
            MinimumForestCover: 0.20,
            MinimumWetlandExtent: 0.0,
            MaximumDisturbance: 0.70,
            AllowedZoneIds: new HashSet<string>(StringComparer.Ordinal) { "zone.a", "zone.b" }));

        var runtime = Runtime(
            scenario,
            climates: new[]
            {
                Climate("zone.a", 0.50, 0.50),
                Climate("zone.b", 0.10, 0.50)
            },
            settings: new GeoclimateSettings(EnvelopeSuitabilityThreshold: 0.75));
        runtime.Advance(Batch(1, Zone("zone.a", CellSimulationState.Warm), Zone("zone.b", CellSimulationState.Warm)));
        var second = runtime.Advance(Batch(2, Zone("zone.a", CellSimulationState.Warm), Zone("zone.b", CellSimulationState.Warm)));

        Assert.Contains(second.EnvelopeEvents, x =>
            x.Kind == HabitatEnvelopeShiftKind.ExpansionOpportunity &&
            x.SourceZoneId == "zone.a" &&
            x.DestinationZoneId == "zone.b" &&
            x.RouteId == "route.a-b" &&
            x.PotentialOnly);
    }

    [Fact]
    public void LandscapeChangeEventsRecordGeomorphicDirection()
    {
        var scenario = new GeoclimateScenarioCatalog();
        scenario.RegisterDisturbance(new DisturbanceEpochDefinition(
            "geomorphic-shock",
            DisturbanceEpochKind.CompoundCatastrophe,
            2,
            2,
            new HashSet<string>(StringComparer.Ordinal) { "zone.a" },
            1.0));
        var runtime = Runtime(scenario, settings: new GeoclimateSettings(LandscapeChangeEventThreshold: 0.01));
        runtime.Advance(Batch(1, Zone("zone.a", CellSimulationState.Warm), Zone("zone.b", CellSimulationState.Warm)));
        var result = runtime.Advance(Batch(2, Zone("zone.a", CellSimulationState.Warm), Zone("zone.b", CellSimulationState.Warm)));

        Assert.Contains(result.LandscapeEvents, x => x.ZoneId == "zone.a" && x.Kind == LandscapeChangeKind.TerrainDegradation);
        Assert.Contains(result.LandscapeEvents, x => x.ZoneId == "zone.a" && x.Delta < 0.0);
    }

    [Fact]
    public void DisturbanceRecoveryRequiresPersistentPostImpactStability()
    {
        var settings = new GeoclimateSettings(
            RecoveryPersistenceGenerations: 2,
            RecoveryClimateStabilityThreshold: 0.10,
            RecoveryLandscapeIntegrityThreshold: 0.10);
        var scenario = new GeoclimateScenarioCatalog();
        scenario.RegisterDisturbance(new DisturbanceEpochDefinition(
            "shock",
            DisturbanceEpochKind.ErosionPulse,
            1,
            1,
            new HashSet<string>(StringComparer.Ordinal) { "zone.a" },
            1.0));
        var runtime = Runtime(scenario, settings: settings);

        GeoclimateGenerationResult? result = null;
        for (int generation = 1; generation <= 6; generation++)
            result = runtime.Advance(Batch(generation, Zone("zone.a", CellSimulationState.Warm), Zone("zone.b", CellSimulationState.Warm)));

        Assert.NotNull(result);
        Assert.Equal(RecoveryStatus.Recovered, result!.Recovery.Single(x => x.ZoneId == "zone.a").Status);
    }

    [Fact]
    public void ClimatePressureIntegratesIntoExistingZoneTraitPressures()
    {
        var scenario = new GeoclimateScenarioCatalog();
        scenario.RegisterClimateEpoch(Epoch("hot-dry", 1, 1, "zone.a", temperature: 0.30, moisture: -0.25));
        var runtime = Runtime(scenario);
        var result = runtime.Advance(Batch(1, Zone("zone.a", CellSimulationState.Warm), Zone("zone.b", CellSimulationState.Warm)));
        var pressure = result.EvolutionPressures.Single(x => x.ZoneId == "zone.a");
        var zone = result.PressureIntegratedZones.Single(x => x.ZoneId == "zone.a");

        Assert.True(pressure.TraitPressures["climate_temperature_tolerance"] > 0.0);
        Assert.True(pressure.TraitPressures["climate_moisture_tolerance"] < 0.0);
        Assert.True(zone.TraitPressures.ContainsKey("disturbance_resilience"));
    }

    [Fact]
    public void ArchiveDefensivelyCopiesNestedListsAndVerifiesHashChain()
    {
        var epochs = new List<string> { "epoch.a" };
        var archive = new GeoclimateArchive();
        archive.Commit(
            0,
            1,
            new[]
            {
                new ClimateZoneState("zone.a", 1, ClimateRegimeKind.WarmWet, 0.6, 0.6, 0.2, 0.5, 0.1, 0.8, epochs)
            },
            new[]
            {
                new LandscapeZoneState("zone.a", 1, 0.5, 0.2, 0.3, 0.0, 0.8, 0.1, new[] { "dist.a" })
            },
            Array.Empty<LandscapeChangeEvent>(),
            Array.Empty<HabitatEnvelopeMigrationEvent>(),
            Array.Empty<EcologicalRecoveryRecord>());

        string hash = archive.Snapshot().HeadHash;
        epochs.Add("epoch.mutated");

        Assert.True(archive.Verify());
        Assert.Equal(hash, archive.Snapshot().HeadHash);
        Assert.DoesNotContain("epoch.mutated", archive.Strata[0].Climate[0].ActiveEpochIds);
    }

    [Fact]
    public void ArchiveRejectsOverlappingHistoricalStrata()
    {
        var archive = new GeoclimateArchive();
        archive.Commit(0, 4, Array.Empty<ClimateZoneState>(), Array.Empty<LandscapeZoneState>(), Array.Empty<LandscapeChangeEvent>(), Array.Empty<HabitatEnvelopeMigrationEvent>(), Array.Empty<EcologicalRecoveryRecord>());

        Assert.Throws<InvalidOperationException>(() =>
            archive.Commit(4, 8, Array.Empty<ClimateZoneState>(), Array.Empty<LandscapeZoneState>(), Array.Empty<LandscapeChangeEvent>(), Array.Empty<HabitatEnvelopeMigrationEvent>(), Array.Empty<EcologicalRecoveryRecord>()));
    }

    private static DeepTimeGeoclimateRuntime Runtime(
        GeoclimateScenarioCatalog scenario,
        IReadOnlyList<ClimateZoneBaseline>? climates = null,
        GeoclimateSettings? settings = null)
    {
        var routes = new PopulationRouteCatalog();
        routes.Register(new PopulationRoute(
            "route.a-b",
            "zone.a",
            "zone.b",
            Bidirectional: true,
            PopulationCapacityPerPhase: 1000,
            Condition: 0.95,
            TravelRisk: 0.05,
            DiseaseTransmissionModifier: 0.20));

        return new DeepTimeGeoclimateRuntime(
            new[] { "zone.a", "zone.b" },
            routes,
            climates ?? new[] { Climate("zone.a", 0.50, 0.50), Climate("zone.b", 0.50, 0.50) },
            new[]
            {
                Landscape("zone.a"),
                Landscape("zone.b")
            },
            scenario,
            settings);
    }

    private static GeoclimateBatchObservation Batch(int generation, params ZoneEnvironment[] zones) =>
        new(generation, zones);

    private static ClimateZoneBaseline Climate(string zoneId, double temperature, double moisture) =>
        new(zoneId, temperature, moisture, Storminess: 0.20, Seasonality: 0.50, ResonanceBackground: 0.10);

    private static LandscapeBaseline Landscape(string zoneId) =>
        new(zoneId, ForestPotential: 0.60, WetlandPotential: 0.20, RiverConnectivity: 0.30, CoastalExposure: 0.0, TerrainIntegrity: 0.80, ErosionResistance: 0.75);

    private static ClimateEpochDefinition Epoch(
        string id,
        int start,
        int end,
        string zoneId,
        double temperature,
        double moisture) =>
        new(
            id,
            start,
            end,
            new HashSet<string>(StringComparer.Ordinal) { zoneId },
            temperature,
            moisture,
            StorminessDelta: 0.0,
            SeasonalityDelta: 0.0,
            ResonanceDelta: 0.0);

    private static ZoneEnvironment Zone(string id, CellSimulationState state) =>
        new(
            id,
            state,
            PopulationHealth: 0.90,
            Stability: 0.90,
            Luminosity: 0.70,
            Integrity: 0.90,
            ResonanceDebt: 0.10,
            TraitPressures: new Dictionary<string, double>(StringComparer.Ordinal));
}
