using EasternKingdoms.Simulation;
using Xunit;

namespace EasternKingdoms.Simulation.Tests;

public sealed class EcologyEvolutionTests
{
    [Fact]
    public void ActiveElvenProfilePassesBiologicalGate()
    {
        var authoring = AzerothSpeciesAuthoringBootstrap.Create();
        var ecology = AzerothEcologyBootstrap.Create();
        var gate = new BiologicalActivationGate(
            new SpeciesActivationGate(authoring),
            ecology);

        var profile = gate.Activate("azeroth.elf.high");
        Assert.Equal(SpeciesProfileStatus.Authored, profile.Status);
    }

    [Fact]
    public void DeferredPlantFailsBiologicalGate()
    {
        var authoring = AzerothSpeciesAuthoringBootstrap.Create();
        var ecology = AzerothEcologyBootstrap.Create();
        var gate = new BiologicalActivationGate(
            new SpeciesActivationGate(authoring),
            ecology);

        Assert.Throws<InvalidOperationException>(
            () => gate.Activate("azeroth.deferred.plant"));
    }

    [Fact]
    public void DerivedFoodWebValuesRemainBounded()
    {
        var zone = AzerothEvolutionBootstrap.CreateNorthernScarCorridor().First();
        var state = new ZoneFoodWebBuilder().Build(zone);
        var profile = AzerothEcologyBootstrap.Create().Get("azeroth.elf.high");
        var outcome = new FoodWebResolver().Resolve(state, profile);

        Assert.True(outcome.FoodWebComplete);
        Assert.InRange(outcome.ResourceSupport, 0.0, 1.0);
        Assert.InRange(outcome.HazardLoad, 0.0, 1.0);
        Assert.InRange(outcome.CarryingCapacityModifier, 0.0, 1.0);
        Assert.InRange(outcome.ReproductiveModifier, 0.0, 1.25);
        Assert.InRange(outcome.ViabilityModifier, 0.0, 1.0);
        Assert.InRange(outcome.MigrationPressure, 0.0, 1.0);
    }

    [Fact]
    public void SameSeedProducesSameCohortEvolution()
    {
        var genomeCatalog = TestGenetics.CreateCatalog();
        var authoring = AzerothSpeciesAuthoringBootstrap.Create();
        var ecology = AzerothEcologyBootstrap.Create();
        var engine = new CohortEvolutionEngine(genomeCatalog, authoring, ecology);
        var zone = AzerothEvolutionBootstrap.CreateNorthernScarCorridor().First();
        var cohort = CreateMixedCohort();

        var left = engine.AdvanceGeneration(cohort, zone, 424242UL);
        var right = engine.AdvanceGeneration(cohort, zone, 424242UL);

        Assert.Equal(left.Next.Population, right.Next.Population);
        Assert.Equal(left.GrowthFactor, right.GrowthFactor);
        Assert.Equal(
            left.Next.AlleleFrequencies.Select(x => (x.LocusId, x.AlleleId, x.Frequency)).ToArray(),
            right.Next.AlleleFrequencies.Select(x => (x.LocusId, x.AlleleId, x.Frequency)).ToArray());
    }

    [Fact]
    public void CohortSelectionChangesAlleleFrequencyWithoutEditingNamedActors()
    {
        var genomeCatalog = TestGenetics.CreateCatalog();
        var authoring = AzerothSpeciesAuthoringBootstrap.Create();
        var ecology = AzerothEcologyBootstrap.Create();
        var engine = new CohortEvolutionEngine(genomeCatalog, authoring, ecology);
        var zone = AzerothEvolutionBootstrap.CreateNorthernScarCorridor().First();
        var cohort = CreateMixedCohort();

        var result = engine.AdvanceGeneration(cohort, zone, 99UL);
        double highAllele = result.Next.AlleleFrequencies
            .Single(x => x.AlleleId == "HE-AP-S")
            .Frequency;

        Assert.NotEqual(0.5, highAllele);
        Assert.Equal(0.5, cohort.AlleleFrequencies.Single(x => x.AlleleId == "HE-AP-S").Frequency);
    }

    [Fact]
    public void LockedCellRejectsCohortEvolution()
    {
        var genomeCatalog = TestGenetics.CreateCatalog();
        var authoring = AzerothSpeciesAuthoringBootstrap.Create();
        var ecology = AzerothEcologyBootstrap.Create();
        var engine = new CohortEvolutionEngine(genomeCatalog, authoring, ecology);
        var baseZone = AzerothEvolutionBootstrap.CreateNorthernScarCorridor().First();
        var locked = baseZone with { SimulationState = CellSimulationState.Locked };

        Assert.Throws<InvalidOperationException>(
            () => engine.AdvanceGeneration(CreateMixedCohort(), locked, 7UL));
    }

    private static EvolutionCohort CreateMixedCohort() =>
        new(
            "cohort.ek.north.eversong.high",
            "azeroth.elf.high",
            "ek.north.eversong",
            Generation: 1,
            Population: 1000,
            AlleleFrequencies: new[]
            {
                new CohortAlleleFrequency("EA05-L001", "HE-AP-S", 0.50),
                new CohortAlleleFrequency("EA05-L001", "SD-AP-C", 0.50)
            });
}
