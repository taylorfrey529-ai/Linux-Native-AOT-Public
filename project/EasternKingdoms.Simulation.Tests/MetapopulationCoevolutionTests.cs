using EasternKingdoms.Simulation;
using Xunit;

namespace EasternKingdoms.Simulation.Tests;

public sealed class MetapopulationCoevolutionTests
{
    [Fact]
    public void FounderSamplingIsDeterministicAndNormalized()
    {
        var zones = AzerothEvolutionBootstrap.CreateNorthernScarCorridor();
        var from = zones.Single(x => x.ZoneId == "ek.north.eversong");
        var to = zones.Single(x => x.ZoneId == "ek.north.ghostlands");
        var route = new PopulationRoute(
            "test.founder",
            from.ZoneId,
            to.ZoneId,
            Bidirectional: true,
            PopulationCapacityPerPhase: 10,
            Condition: 1.0,
            TravelRisk: 0.0,
            DiseaseTransmissionModifier: 0.0);
        var source = Cohort(from.ZoneId, 1000, 0.50, 0.50);
        var engine = new CohortMigrationEngine();

        var a = engine.Move(source, null, route, from, to, 1, 123UL);
        var b = engine.Move(source, null, route, from, to, 1, 123UL);

        Assert.True(a.Recolonized);
        Assert.Equal(a.DestinationAfter.AlleleFrequencies, b.DestinationAfter.AlleleFrequencies);
        Assert.InRange(a.DestinationAfter.AlleleFrequencies.Sum(x => x.Frequency), 0.999999, 1.000001);

        var founder = new FounderEffectAnalyzer().Analyze(a);
        Assert.InRange(founder.DiversityRetention, 0.0, 1.0);
        Assert.True(founder.FounderDiversity <= founder.SourceDiversity + 0.000001);
    }

    [Fact]
    public void DivergenceDetectsFragmentedAlleleFrequencies()
    {
        var a = Cohort("ek.north.eversong", 500, 0.90, 0.10);
        var b = Cohort("ek.north.ghostlands", 500, 0.10, 0.90);

        var result = new GeneticDivergenceAnalyzer().Compare(a, b);

        Assert.True(result.MeanTotalVariationDistance > 0.70);
        Assert.True(result.SimulationFst > 0.0);
    }

    [Fact]
    public void BrokenRouteCreatesSeparateHabitatComponents()
    {
        var routes = new PopulationRouteCatalog();
        routes.Register(new PopulationRoute(
            "test.fragmented",
            "ek.north.eversong",
            "ek.north.ghostlands",
            Bidirectional: true,
            PopulationCapacityPerPhase: 100,
            Condition: 0.10,
            TravelRisk: 0.0,
            DiseaseTransmissionModifier: 0.0));

        var report = new HabitatFragmentationAnalyzer().Analyze(
            "azeroth.elf.high",
            new[]
            {
                Cohort("ek.north.eversong", 100, 0.50, 0.50),
                Cohort("ek.north.ghostlands", 100, 0.50, 0.50)
            },
            routes,
            minimumRouteCondition: 0.50);

        Assert.Equal(2, report.ConnectedComponents);
        Assert.Equal(2, report.IsolatedZones.Count);
    }

    [Fact]
    public void ReciprocalCoevolutionProducesBoundedTraitPressure()
    {
        var catalog = TestGenetics.CreateCatalog();
        var engine = new ReciprocalCoevolutionEngine(catalog);
        var outcome = engine.Resolve(
            new[]
            {
                Cohort("ek.north.eversong", 100, 1.0, 0.0, "azeroth.elf.high"),
                Cohort("ek.north.ghostlands", 100, 0.0, 1.0, "azeroth.elf.sindorei")
            },
            new[]
            {
                new ReciprocalSelectionRule(
                    "azeroth.elf.high",
                    "arcane_precision",
                    "azeroth.elf.sindorei",
                    "resonance_amplification",
                    CouplingStrength: 1.0)
            });

        double highPressure = outcome.TraitPressures["azeroth.elf.high"]["arcane_precision"];
        double sinPressure = outcome.TraitPressures["azeroth.elf.sindorei"]["resonance_amplification"];
        Assert.InRange(highPressure, -1.0, 1.0);
        Assert.InRange(sinPressure, -1.0, 1.0);
    }

    [Fact]
    public void HostPathogenCoevolutionIsSeedDeterministic()
    {
        var catalog = TestGenetics.CreateCatalog();
        var engine = new HostPathogenCoevolutionEngine(catalog);
        var host = Cohort("ek.north.eversong", 1000, 0.75, 0.25);
        var zone = AzerothEvolutionBootstrap.CreateNorthernScarCorridor()
            .Single(x => x.ZoneId == host.ZoneId);
        var disease = new DiseaseEvolutionState(
            "test.disease",
            Generation: 1,
            TransmissionPotential: 0.45,
            Virulence: 0.30,
            EnvironmentalTolerance: 0.25,
            HostEscape: 0.20);
        var traits = new HashSet<string>(StringComparer.Ordinal) { "arcane_precision" };

        var a = engine.Advance(disease, host, traits, zone, prevalence: 0.30, simulationSeed: 991UL);
        var b = engine.Advance(disease, host, traits, zone, prevalence: 0.30, simulationSeed: 991UL);

        Assert.Equal(a.Next, b.Next);
        Assert.Equal(a.Seed, b.Seed);
        Assert.InRange(a.Next.TransmissionPotential, 0.0, 1.0);
        Assert.InRange(a.Next.Virulence, 0.0, 1.0);
        Assert.InRange(a.Next.EnvironmentalTolerance, 0.0, 1.0);
        Assert.InRange(a.Next.HostEscape, 0.0, 1.0);
    }

    [Fact]
    public void MetapopulationAdvanceIsDeterministicAcrossSameSeed()
    {
        var catalog = TestGenetics.CreateCatalog();
        var biologyA = AzerothBiologyBootstrap.CreateAuthoritative();
        var biologyB = AzerothBiologyBootstrap.CreateAuthoritative();
        var routesA = NorthernScarPopulationRoutes.Create();
        var routesB = NorthernScarPopulationRoutes.Create();
        var engineA = new MetapopulationOrchestrator(
            new CohortEvolutionEngine(catalog, biologyA.SpeciesAuthoring, biologyA.Ecology),
            routesA,
            PopulationBoundaryGate.NorthernScarCorridor());
        var engineB = new MetapopulationOrchestrator(
            new CohortEvolutionEngine(catalog, biologyB.SpeciesAuthoring, biologyB.Ecology),
            routesB,
            PopulationBoundaryGate.NorthernScarCorridor());
        var source = new[]
        {
            Cohort("ek.north.eversong", 1000, 0.70, 0.30),
            Cohort("ek.north.ghostlands", 600, 0.55, 0.45)
        };
        var zones = AzerothEvolutionBootstrap.CreateNorthernScarCorridor();

        var a = engineA.AdvanceGeneration(source, zones, 777UL);
        var b = engineB.AdvanceGeneration(source, zones, 777UL);

        Assert.Equal(
            a.Cohorts.Select(x => (x.ZoneId, x.Population, x.Generation)).ToArray(),
            b.Cohorts.Select(x => (x.ZoneId, x.Population, x.Generation)).ToArray());
        Assert.Equal(
            a.GeneFlow.Select(x => (x.RouteId, x.Departed, x.Arrived, x.Recolonized)).ToArray(),
            b.GeneFlow.Select(x => (x.RouteId, x.Departed, x.Arrived, x.Recolonized)).ToArray());
    }


    [Fact]
    public void TrophicPredationLinkCompilesToReciprocalSelectionRule()
    {
        var network = new TrophicNetworkDefinition(
            "test.predation",
            new[]
            {
                new TrophicLink(
                    "test.predator",
                    "test.prey",
                    FoodWebNodeKind.Species,
                    TrophicRelationKind.Predation,
                    Strength: 0.60,
                    AssimilationEfficiency: 0.70,
                    SourceMortalityPressure: 0.30)
            });

        var rules = new TrophicCoevolutionRuleCompiler().Compile(
            network,
            new[]
            {
                new TrophicCoevolutionBinding(
                    "test.predator",
                    "pursuit",
                    "test.prey",
                    "evasion",
                    CouplingStrength: 0.40)
            });

        var rule = Assert.Single(rules);
        Assert.Equal("test.predator", rule.SpeciesAId);
        Assert.Equal("test.prey", rule.SpeciesBId);
    }

    [Fact]
    public void EvolvedDiseaseStateFeedsTransmissionProfileWithinBounds()
    {
        var authored = new DiseaseProfile(
            "test.disease",
            new HashSet<string>(StringComparer.Ordinal) { "azeroth.elf.high" },
            BaseTransmission: 0.40,
            RecoveryRate: 0.10,
            MortalityRate: 0.10,
            EnvironmentalSensitivity: 0.30);
        var evolved = new DiseaseEvolutionState(
            authored.DiseaseId,
            Generation: 4,
            TransmissionPotential: 0.80,
            Virulence: 0.70,
            EnvironmentalTolerance: 0.60,
            HostEscape: 0.50);

        var result = new EvolvedDiseaseProfileAdapter().Apply(authored, evolved);

        Assert.InRange(result.BaseTransmission, 0.0, 1.0);
        Assert.InRange(result.MortalityRate, 0.0, 1.0);
        Assert.InRange(result.EnvironmentalSensitivity, 0.0, 1.0);
        Assert.True(result.BaseTransmission > authored.BaseTransmission);
    }

    private static EvolutionCohort Cohort(
        string zoneId,
        long population,
        double highFrequency,
        double sinFrequency,
        string speciesId = "azeroth.elf.high") =>
        new(
            $"cohort.{zoneId}.{speciesId}",
            speciesId,
            zoneId,
            Generation: 3,
            Population: population,
            AlleleFrequencies: new[]
            {
                new CohortAlleleFrequency("EA05-L001", "HE-AP-S", highFrequency),
                new CohortAlleleFrequency("EA05-L001", "SD-AP-C", sinFrequency)
            });
}
