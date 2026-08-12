using EasternKingdoms.Simulation;
using Xunit;

namespace EasternKingdoms.Simulation.Tests;

public sealed class CommunityEvolutionTests
{
    [Fact]
    public void EnvironmentalPulseIsDeterministicAndBounded()
    {
        var zones = AzerothEvolutionBootstrap.CreateNorthernScarCorridor();
        var pulse = new EnvironmentalPulseDefinition(
            "test.pulse",
            PeriodGenerations: 4,
            DurationGenerations: 1,
            PhaseOffset: 1,
            TargetZoneIds: new HashSet<string>(StringComparer.Ordinal) { "ek.north.eversong" },
            PopulationHealthDelta: 0.50,
            StabilityDelta: -0.10,
            LuminosityDelta: 0.40,
            IntegrityDelta: 0.0,
            ResonanceDebtDelta: 0.30,
            TraitPressureDeltas: new Dictionary<string, double>(StringComparer.Ordinal)
            {
                ["arcane_precision"] = 0.20
            });
        var engine = new SeasonalEnvironmentEngine();

        var a = engine.Apply(zones, 1, new[] { pulse });
        var b = engine.Apply(zones, 1, new[] { pulse });
        var eversong = a.Zones.Single(x => x.ZoneId == "ek.north.eversong");

        Assert.Equal(
            a.Zones.Select(x => (x.ZoneId, x.PopulationHealth, x.Stability, x.Luminosity, x.Integrity, x.ResonanceDebt)).ToArray(),
            b.Zones.Select(x => (x.ZoneId, x.PopulationHealth, x.Stability, x.Luminosity, x.Integrity, x.ResonanceDebt)).ToArray());
        Assert.Equal(a.AppliedPulses.Select(x => (x.PulseId, x.ZoneId)).ToArray(), b.AppliedPulses.Select(x => (x.PulseId, x.ZoneId)).ToArray());
        Assert.Single(a.AppliedPulses);
        Assert.InRange(eversong.PopulationHealth, 0.0, 1.0);
        Assert.InRange(eversong.Luminosity, 0.0, 1.0);
        Assert.InRange(eversong.ResonanceDebt, 0.0, 1.0);
        Assert.Equal(1.0, eversong.PopulationHealth);
    }

    [Fact]
    public void SingleStressMetricCannotTriggerCommunityCollapse()
    {
        var tracker = new CommunityResilienceTracker();
        var settings = new CommunityResilienceSettings(
            CollapseMinimumStressDimensions: 3,
            CollapseConsecutiveGenerations: 3);
        var state = CommunityResilienceState.Initial;
        var oneMetric = new CommunityStressMetrics(
            PopulationLoss: 0.0,
            DiseaseLoad: 0.90,
            TrophicStress: 0.0,
            FragmentationStress: 0.0,
            LocalExtinctionStress: 0.0);

        for (int i = 0; i < 8; i++)
            state = tracker.Assess(state, oneMetric, settings).Next;

        Assert.NotEqual(CommunityCondition.Collapsing, state.Condition);
    }

    [Fact]
    public void InteractingSustainedStressCanTriggerCommunityCollapse()
    {
        var tracker = new CommunityResilienceTracker();
        var settings = new CommunityResilienceSettings(
            CollapseMinimumStressDimensions: 3,
            CollapseConsecutiveGenerations: 3);
        var state = CommunityResilienceState.Initial;
        var multiStress = new CommunityStressMetrics(
            PopulationLoss: 0.40,
            DiseaseLoad: 0.70,
            TrophicStress: 0.60,
            FragmentationStress: 0.0,
            LocalExtinctionStress: 0.0);

        for (int i = 0; i < 3; i++)
            state = tracker.Assess(state, multiStress, settings).Next;

        Assert.Equal(CommunityCondition.Collapsing, state.Condition);
    }

    [Fact]
    public void SustainedLowStressTransitionsThroughRecoveryToStable()
    {
        var tracker = new CommunityResilienceTracker();
        var settings = new CommunityResilienceSettings(
            CollapseMinimumStressDimensions: 3,
            CollapseConsecutiveGenerations: 2,
            RecoveryMaximumStressDimensions: 1,
            RecoveryConsecutiveGenerations: 2);
        var stressed = new CommunityStressMetrics(0.40, 0.70, 0.60, 0.0, 0.0);
        var calm = new CommunityStressMetrics(0.0, 0.0, 0.0, 0.0, 0.0);
        var state = CommunityResilienceState.Initial;

        state = tracker.Assess(state, stressed, settings).Next;
        state = tracker.Assess(state, stressed, settings).Next;
        Assert.Equal(CommunityCondition.Collapsing, state.Condition);

        state = tracker.Assess(state, calm, settings).Next;
        Assert.Equal(CommunityCondition.Recovering, state.Condition);
        state = tracker.Assess(state, calm, settings).Next;
        Assert.Equal(CommunityCondition.Stable, state.Condition);
    }

    [Fact]
    public void DiseaseNeverCreatesStateForIneligibleSpecies()
    {
        var catalog = TestGenetics.CreateCatalog();
        var engine = new CommunityDiseaseEngine(catalog);
        var zones = AzerothEvolutionBootstrap.CreateNorthernScarCorridor();
        var cohorts = new[]
        {
            Cohort("ek.north.eversong", "azeroth.elf.high", 500),
            Cohort("ek.north.eversong", "azeroth.elf.sindorei", 500)
        };
        var authored = new DiseaseProfile(
            "test.host-gated",
            new HashSet<string>(StringComparer.Ordinal) { "azeroth.elf.high" },
            BaseTransmission: 0.35,
            RecoveryRate: 0.10,
            MortalityRate: 0.05,
            EnvironmentalSensitivity: 0.25);
        var definition = new CommunityDiseaseDefinition(
            authored,
            new DiseaseEvolutionState(authored.DiseaseId, 0, 0.35, 0.20, 0.20, 0.10),
            new HashSet<string>(StringComparer.Ordinal) { "arcane_precision" },
            new HostPathogenCoevolutionSettings());

        var result = engine.Advance(
            new[] { definition },
            new[] { new DiseaseState("ek.north.eversong", "azeroth.elf.high", authored.DiseaseId, 0.30) },
            new[] { definition.InitialEvolution },
            cohorts,
            Array.Empty<CohortMigrationResult>(),
            NorthernScarPopulationRoutes.Create().Routes.ToArray(),
            zones,
            123UL);

        Assert.Contains(result.DiseaseStates, x => x.SpeciesId == "azeroth.elf.high");
        Assert.DoesNotContain(result.DiseaseStates, x => x.SpeciesId == "azeroth.elf.sindorei");
    }

    [Fact]
    public void EligibleHostsCanReceiveCrossHostDiseasePressure()
    {
        var catalog = TestGenetics.CreateCatalog();
        var engine = new CommunityDiseaseEngine(catalog);
        var zones = AzerothEvolutionBootstrap.CreateNorthernScarCorridor();
        var cohorts = new[]
        {
            Cohort("ek.north.eversong", "azeroth.elf.high", 500),
            Cohort("ek.north.eversong", "azeroth.elf.sindorei", 500)
        };
        var hosts = new HashSet<string>(StringComparer.Ordinal)
        {
            "azeroth.elf.high",
            "azeroth.elf.sindorei"
        };
        var authored = new DiseaseProfile(
            "test.spillover",
            hosts,
            BaseTransmission: 0.20,
            RecoveryRate: 0.05,
            MortalityRate: 0.02,
            EnvironmentalSensitivity: 0.20);
        var definition = new CommunityDiseaseDefinition(
            authored,
            new DiseaseEvolutionState(authored.DiseaseId, 0, 0.30, 0.10, 0.10, 0.10),
            new HashSet<string>(StringComparer.Ordinal) { "arcane_precision" },
            new HostPathogenCoevolutionSettings(),
            CrossHostTransmission: 0.20);

        var result = engine.Advance(
            new[] { definition },
            new[]
            {
                new DiseaseState("ek.north.eversong", "azeroth.elf.high", authored.DiseaseId, 0.50),
                new DiseaseState("ek.north.eversong", "azeroth.elf.sindorei", authored.DiseaseId, 0.00)
            },
            new[] { definition.InitialEvolution },
            cohorts,
            Array.Empty<CohortMigrationResult>(),
            NorthernScarPopulationRoutes.Create().Routes.ToArray(),
            zones,
            999UL);

        var sin = result.DiseaseStates.Single(x => x.SpeciesId == "azeroth.elf.sindorei");
        Assert.True(sin.Prevalence > 0.0);
    }

    [Fact]
    public void SameSeedProducesSameLongHorizonCommunityHistory()
    {
        var catalogA = TestGenetics.CreateCatalog();
        var catalogB = TestGenetics.CreateCatalog();
        var runtimeA = AzerothCommunityEvolutionBootstrap.Create(catalogA);
        var runtimeB = AzerothCommunityEvolutionBootstrap.Create(catalogB);
        var initialA = CommunityEvolutionState.CreateInitial(InitialCohorts());
        var initialB = CommunityEvolutionState.CreateInitial(InitialCohorts());
        var a = new CommunityEvolutionRuntime(runtimeA.Orchestrator, runtimeA.Definition, initialA);
        var b = new CommunityEvolutionRuntime(runtimeB.Orchestrator, runtimeB.Definition, initialB);

        var left = a.Run(5, 0xE16UL);
        var right = b.Run(5, 0xE16UL);

        Assert.Equal(left.IntegrityHash, right.IntegrityHash);
        Assert.Equal(
            left.FinalState.Cohorts.Select(x => (x.SpeciesId, x.ZoneId, x.Population, x.Generation)).ToArray(),
            right.FinalState.Cohorts.Select(x => (x.SpeciesId, x.ZoneId, x.Population, x.Generation)).ToArray());
    }

    private static IReadOnlyList<EvolutionCohort> InitialCohorts() =>
        new[]
        {
            Cohort("ek.north.eversong", "azeroth.elf.high", 900),
            Cohort("ek.north.ghostlands", "azeroth.elf.high", 500),
            Cohort("ek.north.eversong", "azeroth.elf.sindorei", 800),
            Cohort("ek.north.ghostlands", "azeroth.elf.sindorei", 450)
        };

    private static EvolutionCohort Cohort(
        string zoneId,
        string speciesId,
        long population) =>
        new(
            $"community.{speciesId}.{zoneId}",
            speciesId,
            zoneId,
            Generation: 0,
            Population: population,
            AlleleFrequencies: new[]
            {
                new CohortAlleleFrequency("EA05-L001", "HE-AP-S", 0.55),
                new CohortAlleleFrequency("EA05-L001", "SD-AP-C", 0.45)
            });
}
