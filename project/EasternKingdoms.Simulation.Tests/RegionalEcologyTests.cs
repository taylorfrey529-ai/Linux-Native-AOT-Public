using EasternKingdoms.Simulation;
using Xunit;

namespace EasternKingdoms.Simulation.Tests;

public sealed class RegionalEcologyTests
{
    [Fact]
    public void GuildScaffoldBuildsProducerGrazerPredatorAndDecomposerLinks()
    {
        var ecology = CreateTrophicTestCatalog();
        var network = new TrophicNetworkCompiler().Compile(
            DefaultTrophicGuildScaffold.Create(),
            ecology,
            "test.network");

        Assert.Contains(network.Links, x => x.ConsumerSpeciesId == "test.producer" && x.SourceId == "soil_nutrients");
        Assert.Contains(network.Links, x => x.ConsumerSpeciesId == "test.grazer" && x.SourceId == "test.producer" && x.Relation == TrophicRelationKind.Grazing);
        Assert.Contains(network.Links, x => x.ConsumerSpeciesId == "test.predator" && x.SourceId == "test.grazer" && x.Relation == TrophicRelationKind.Predation);
        Assert.Contains(network.Links, x => x.ConsumerSpeciesId == "test.decomposer" && x.SourceId == "detritus" && x.Relation == TrophicRelationKind.Decomposition);
    }

    [Fact]
    public void PredationCreatesPressureOnPrey()
    {
        var ecology = CreateTrophicTestCatalog();
        var network = new TrophicNetworkCompiler().Compile(
            DefaultTrophicGuildScaffold.Create(),
            ecology,
            "test.network");
        var zone = AzerothEvolutionBootstrap.CreateNorthernScarCorridor().First();
        var state = new ZoneFoodWebBuilder().Build(
            zone,
            new Dictionary<string, double>(StringComparer.Ordinal)
            {
                ["test.producer"] = 0.80,
                ["test.grazer"] = 0.60,
                ["test.predator"] = 0.30,
                ["test.decomposer"] = 0.50
            });

        var result = new TrophicNetworkResolver().Resolve(network, ecology, state);
        var grazer = result.Single(x => x.SpeciesId == "test.grazer");

        Assert.True(grazer.PredationLoad > 0.0);
        Assert.InRange(grazer.NetSupportModifier, 0.0, 1.25);
    }

    [Fact]
    public void SameSeedProducesSameRouteBoundMigration()
    {
        var zones = AzerothEvolutionBootstrap.CreateNorthernScarCorridor();
        var from = zones.Single(x => x.ZoneId == "ek.north.eversong");
        var to = zones.Single(x => x.ZoneId == "ek.north.ghostlands");
        var route = NorthernScarPopulationRoutes.Create().Get("ek.route.eversong-ghostlands");
        var cohort = CreateCohort("ek.north.eversong", 1000);
        var engine = new CohortMigrationEngine();

        var left = engine.Move(cohort, null, route, from, to, 250, 7171UL);
        var right = engine.Move(cohort, null, route, from, to, 250, 7171UL);

        Assert.Equal(left.Departed, right.Departed);
        Assert.Equal(left.Arrived, right.Arrived);
        Assert.Equal(left.TransitLosses, right.TransitLosses);
        Assert.True(left.Recolonized);
        Assert.Equal(cohort.Population, left.SourceAfter.Population + left.DestinationAfter.Population + left.TransitLosses);
    }

    [Fact]
    public void LockedDestinationRejectsMigration()
    {
        var zones = AzerothEvolutionBootstrap.CreateNorthernScarCorridor();
        var from = zones.Single(x => x.ZoneId == "ek.north.eversong");
        var to = zones.Single(x => x.ZoneId == "ek.north.ghostlands") with { SimulationState = CellSimulationState.Locked };
        var route = NorthernScarPopulationRoutes.Create().Get("ek.route.eversong-ghostlands");

        Assert.Throws<InvalidOperationException>(() =>
            new CohortMigrationEngine().Move(CreateCohort(from.ZoneId, 100), null, route, from, to, 20, 5UL));
    }

    [Fact]
    public void DiseaseCanPropagateAlongActualMigrationRoute()
    {
        var zones = AzerothEvolutionBootstrap.CreateNorthernScarCorridor();
        var from = zones.Single(x => x.ZoneId == "ek.north.eversong");
        var to = zones.Single(x => x.ZoneId == "ek.north.ghostlands");
        var route = NorthernScarPopulationRoutes.Create().Get("ek.route.eversong-ghostlands");
        var migration = new CohortMigrationEngine().Move(
            CreateCohort(from.ZoneId, 1000),
            CreateCohort(to.ZoneId, 500),
            route,
            from,
            to,
            200,
            81UL);

        var profile = new DiseaseProfile(
            "test.disease",
            new HashSet<string>(StringComparer.Ordinal) { "azeroth.elf.high" },
            BaseTransmission: 0.20,
            RecoveryRate: 0.05,
            MortalityRate: 0.02,
            EnvironmentalSensitivity: 0.30);

        var states = new[]
        {
            new DiseaseState(from.ZoneId, "azeroth.elf.high", profile.DiseaseId, 0.50),
            new DiseaseState(to.ZoneId, "azeroth.elf.high", profile.DiseaseId, 0.00)
        };

        var results = new DiseaseTransmissionEngine().Advance(
            profile,
            states,
            new[] { migration },
            new[] { route },
            zones);

        var destination = results.Single(x => x.Next.ZoneId == to.ZoneId);
        Assert.True(destination.ImportedPressure > 0.0);
        Assert.True(destination.Next.Prevalence > 0.0);
    }

    [Fact]
    public void LowPopulationAndLowViabilityCanBecomeLocallyExtinct()
    {
        var cohort = CreateCohort("ek.north.ghostlands", 5);
        var ecology = new SpeciesEcologyOutcome(
            cohort.ZoneId,
            cohort.SpeciesId,
            ResourceSupport: 0.20,
            HazardLoad: 0.85,
            CarryingCapacityModifier: 0.10,
            ReproductiveModifier: 0.10,
            ViabilityModifier: 0.20,
            MigrationPressure: 0.90,
            FoodWebComplete: true);

        var assessment = new LocalPopulationContinuityEngine().Assess(cohort, ecology, minimumViablePopulation: 10);
        Assert.Equal(LocalPopulationStatus.Extinct, assessment.Status);
    }

    [Fact]
    public void DisturbedZoneCanEnterRecoverySuccession()
    {
        var zone = AzerothEvolutionBootstrap.CreateNorthernScarCorridor()
            .Single(x => x.ZoneId == "ek.north.eversong");
        var foodWeb = new ZoneFoodWebBuilder().Build(zone);
        var previous = new ZoneSuccessionState(
            zone.ZoneId,
            SuccessionStage.Disturbed,
            StablePhases: 0,
            BiomassIndex: 0.20,
            DecomposerIndex: 0.20,
            DisturbanceIndex: 0.80);

        var next = new EcologicalSuccessionEngine().Advance(previous, zone, foodWeb);
        Assert.Equal(SuccessionStage.Recovering, next.Stage);
        Assert.True(next.StablePhases > 0);
    }

    [Fact]
    public void PopulationBoundaryRejectsExternalRoute()
    {
        var gate = PopulationBoundaryGate.NorthernScarCorridor();
        var external = new PopulationRoute(
            "test.external",
            "ek.north.eversong",
            "external.continent",
            Bidirectional: true,
            PopulationCapacityPerPhase: 100,
            Condition: 1.0,
            TravelRisk: 0.0,
            DiseaseTransmissionModifier: 0.0);

        Assert.Throws<InvalidOperationException>(() => gate.Validate(external));
    }

    private static EvolutionCohort CreateCohort(string zoneId, long population) =>
        new(
            $"cohort.{zoneId}.high",
            "azeroth.elf.high",
            zoneId,
            Generation: 3,
            Population: population,
            AlleleFrequencies: new[]
            {
                new CohortAlleleFrequency("EA05-L001", "HE-AP-S", 0.60),
                new CohortAlleleFrequency("EA05-L001", "SD-AP-C", 0.40)
            });

    private static SpeciesEcologyCatalog CreateTrophicTestCatalog()
    {
        var catalog = new SpeciesEcologyCatalog();
        Register(catalog, "test.producer", EcologicalGuild.Producer);
        Register(catalog, "test.grazer", EcologicalGuild.Grazer);
        Register(catalog, "test.predator", EcologicalGuild.Predator);
        Register(catalog, "test.decomposer", EcologicalGuild.Decomposer);
        return catalog;
    }

    private static void Register(
        SpeciesEcologyCatalog catalog,
        string speciesId,
        EcologicalGuild guild)
    {
        catalog.Register(new SpeciesEcologyProfile(
            speciesId,
            SpeciesEcologyStatus.Active,
            guild,
            Array.Empty<FoodWebDependency>(),
            new Dictionary<string, double>(StringComparer.Ordinal),
            DensitySensitivity: 0.30,
            MigrationSensitivity: 0.30,
            ContinuityStatus: "Test Fixture"));
    }
}
