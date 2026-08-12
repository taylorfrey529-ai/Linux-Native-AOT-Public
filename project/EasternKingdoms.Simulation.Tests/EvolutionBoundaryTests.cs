using EasternKingdoms.Simulation;
using Evermore.Genetics;
using Xunit;

namespace EasternKingdoms.Simulation.Tests;

public sealed class EvolutionBoundaryTests
{
    [Fact]
    public void DeferredSpeciesCannotEnterAuthoritativeSimulation()
    {
        var life = AzerothEvolutionBootstrap.CreateLifeRegistry();
        life.RegisterSpecies(AzerothEvolutionBootstrap.CreateDeferredSpecies(
            "azeroth.species.unresolved",
            BeingKind.Other,
            "deferred.schema"));

        var genome = TestGenetics.CreateGenome("deferred-genome", ElvenLineage.Ancestral, "HE-AP-S", "HE-AP-S");
        Assert.Throws<InvalidOperationException>(() => life.AddBeing(new BeingGenome(
            "deferred-being", "azeroth.species.unresolved", "unresolved", genome, 0, "ek.north.eversong")));
    }

    [Fact]
    public void LockedCellProducesNoBirthWrites()
    {
        var setup = TestGenetics.CreateEngine();
        var zone = new ZoneEnvironment(
            "ek.north.eversong", CellSimulationState.Locked,
            1.0, 1.0, 1.0, 1.0, 0.0,
            new Dictionary<string, double>());

        var snapshot = setup.Engine.AdvancePhase(20, new[] { zone }, 1234UL);
        Assert.DoesNotContain(snapshot.Events, x => x.EventType == "birth_committed");
    }

    [Fact]
    public void SameSeedProducesSameAuthoritativeBirthEvents()
    {
        var a = TestGenetics.CreateEngine();
        var b = TestGenetics.CreateEngine();
        var zone = new ZoneEnvironment(
            "ek.north.eversong", CellSimulationState.Hot,
            1.0, 1.0, 1.0, 1.0, 0.0,
            new Dictionary<string, double>());

        var left = a.Engine.AdvancePhase(20, new[] { zone }, 999UL).Events
            .Where(x => x.EventType == "birth_committed").Select(x => x.Detail).ToArray();
        var right = b.Engine.AdvancePhase(20, new[] { zone }, 999UL).Events
            .Where(x => x.EventType == "birth_committed").Select(x => x.Detail).ToArray();

        Assert.Equal(left, right);
    }
}

internal static class TestGenetics
{
    internal sealed record Setup(EasternKingdomsEvolutionEngine Engine);

    public static Setup CreateEngine()
    {
        var catalog = CreateCatalog();
        var life = AzerothEvolutionBootstrap.CreateLifeRegistry();
        life.AddBeing(new BeingGenome(
            "A", "azeroth.elf.high", "high", CreateGenome("A-genome", ElvenLineage.HighElf, "HE-AP-S", "HE-AP-S"),
            0, "ek.north.eversong"));
        life.AddBeing(new BeingGenome(
            "B", "azeroth.elf.sindorei", "sindorei", CreateGenome("B-genome", ElvenLineage.SinDorei, "SD-AP-C", "SD-AP-C"),
            0, "ek.north.eversong"));

        return new Setup(new EasternKingdomsEvolutionEngine(
            life,
            catalog,
            new GenomeRecombiner(new LinkedGameteGenerator(), new MutationEngine()),
            new GenomeValidator(),
            new ExpressionEngine()));
    }

    public static GenomeCatalog CreateCatalog()
    {
        var locus = new LocusDefinition(
            "EA05-L001", "ARC_PRECISION", 1000,
            new[] { "HE-AP-S", "SD-AP-C" });
        var chromosome = new ChromosomeDefinition("EA-05", "Arcane Regulation", new[] { locus });
        var alleles = new[]
        {
            new AlleleDefinition("HE-AP-S", locus.Id, AlleleInteraction.Codominant, 0.88,
                new Dictionary<string, double> { ["arcane_precision"] = 0.46, ["arcane_conservation"] = 0.38 }),
            new AlleleDefinition("SD-AP-C", locus.Id, AlleleInteraction.Codominant, 0.84,
                new Dictionary<string, double> { ["arcane_precision"] = 0.40, ["resonance_amplification"] = 0.44 })
        };
        return new GenomeCatalog(new[] { chromosome }, alleles);
    }

    public static GenomeDefinition CreateGenome(string id, ElvenLineage lineage, string a, string b) =>
        new(id, lineage, 1,
            new[]
            {
                new SequencedChromosome("EA-05",
                    new[] { new SequencedLocus("EA05-L001", new AllelePair(a, b)) })
            });
}
