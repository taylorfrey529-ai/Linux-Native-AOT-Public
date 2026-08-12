using EasternKingdoms.Simulation;
using Xunit;

namespace EasternKingdoms.Simulation.Tests;

public sealed class DeepTimeMacroevolutionTests
{
    [Fact]
    public void ExtinctionRequiresSustainedZeroPopulation()
    {
        var runtime = new DeepTimeMacroevolutionRuntime(new DeepTimeSettings(ExtinctionPersistenceGenerations: 2));
        runtime.Advance(Batch(1, Lineage("root", null, 1, 10)));
        var firstZero = runtime.Advance(Batch(2, Lineage("root", null, 2, 0)));
        var secondZero = runtime.Advance(Batch(3, Lineage("root", null, 3, 0)));

        Assert.Empty(firstZero.ExtinctionEvents);
        Assert.Single(secondZero.ExtinctionEvents);
        Assert.Equal(DeepTimeLineageStatus.Extinct, secondZero.Lineages.Single().Status);
    }

    [Fact]
    public void RejectedLineageCannotRegainAuthorityByLaterObservation()
    {
        var ledger = new DeepTimeLineageLedger(new DeepTimeSettings());
        ledger.Observe(Lineage("candidate", null, 1, 100, DeepTimeLineageAuthority.Rejected));
        var later = ledger.Observe(Lineage("candidate", null, 2, 100, DeepTimeLineageAuthority.CanonAuthoringAuthorized));

        Assert.Equal(DeepTimeLineageAuthority.Rejected, later.Authority);
    }

    [Fact]
    public void AdaptiveRadiationRequiresMultipleNicheDivergentBranches()
    {
        var settings = new DeepTimeSettings(
            MinimumRadiationBranches: 3,
            MinimumRadiationNicheDivergence: 0.30,
            RadiationWindowGenerations: 10);
        var runtime = new DeepTimeMacroevolutionRuntime(settings);

        runtime.Advance(Batch(1, Lineage("root", null, 1, 100, nicheA: 0.5, nicheB: 0.5)));
        var result = runtime.Advance(Batch(2,
            Lineage("root", null, 2, 90, nicheA: 0.5, nicheB: 0.5),
            Lineage("branch-a", "root", 2, 30, nicheA: 1.0, nicheB: 0.0),
            Lineage("branch-b", "root", 2, 30, nicheA: 0.0, nicheB: 1.0),
            Lineage("branch-c", "root", 2, 30, nicheA: 0.9, nicheB: 0.9)));

        Assert.Contains(result.RadiationEvents, x => x.RootLineageId == "root");
    }

    [Fact]
    public void EcologicalReplacementRequiresPriorExtinctionAndNicheOverlap()
    {
        var runtime = new DeepTimeMacroevolutionRuntime(new DeepTimeSettings(ExtinctionPersistenceGenerations: 1));
        runtime.Advance(Batch(1,
            Lineage("old", null, 1, 100, nicheA: 0.9, nicheB: 0.2),
            Lineage("new", null, 1, 40, nicheA: 0.85, nicheB: 0.25)));

        var result = runtime.Advance(Batch(2,
            Lineage("old", null, 2, 0, nicheA: 0.9, nicheB: 0.2),
            Lineage("new", null, 2, 80, nicheA: 0.85, nicheB: 0.25)));

        Assert.Contains(result.ReplacementEvents, x => x.ExtinctLineageId == "old" && x.ReplacementLineageId == "new");
    }

    [Fact]
    public void CandidateAuthorityRemainsNonCanonicalInDeepTimeGate()
    {
        var gate = new DeepTimeGovernanceGate();
        var candidate = State("candidate", DeepTimeLineageAuthority.CandidateTestHistory);
        var experimental = State("experimental", DeepTimeLineageAuthority.ExperimentalSimulation);

        Assert.False(gate.Assess(candidate).MayEnterExperimentalDeepTime);
        Assert.True(gate.Assess(experimental).MayEnterExperimentalDeepTime);
        Assert.False(gate.Assess(experimental).MayBeUsedForCanonAuthoring);
    }

    [Fact]
    public void PhylogenyReconstructionIsDeterministic()
    {
        var states = new[]
        {
            State("root", DeepTimeLineageAuthority.ExistingAuthorizedSpecies),
            State("b", DeepTimeLineageAuthority.ExperimentalSimulation, "root"),
            State("a", DeepTimeLineageAuthority.ExperimentalSimulation, "root")
        };
        var reconstructor = new DeepTimePhylogenyReconstructor();

        var first = reconstructor.Reconstruct(states);
        var second = reconstructor.Reconstruct(states.Reverse());

        Assert.Equal(first.RootLineageIds, second.RootLineageIds);
        Assert.Equal(first.Nodes.Select(x => x.LineageId), second.Nodes.Select(x => x.LineageId));
        Assert.Equal(new[] { "a", "b" }, first.Nodes.Single(x => x.LineageId == "root").Children);
    }

    [Fact]
    public void DeepTimeArchiveFormsVerifiableHashChain()
    {
        var archive = new DeepTimeArchive();
        var state = State("root", DeepTimeLineageAuthority.ExistingAuthorizedSpecies);

        var first = archive.Commit(0, 9, new[] { state }, Array.Empty<AdaptiveRadiationEvent>(), Array.Empty<LineageExtinctionEvent>(), Array.Empty<EcologicalReplacementEvent>(), new CladeBuilder().Build(new[] { state }));
        var second = archive.Commit(10, 19, new[] { state with { LastObservedGeneration = 19 } }, Array.Empty<AdaptiveRadiationEvent>(), Array.Empty<LineageExtinctionEvent>(), Array.Empty<EcologicalReplacementEvent>(), new CladeBuilder().Build(new[] { state }));

        Assert.True(archive.Verify());
        Assert.Equal(first.IntegrityHash, second.PreviousStratumHash);
        Assert.Equal(second.IntegrityHash, archive.Snapshot().HeadHash);
    }

    [Fact]
    public void OpenStratumCommitIsDeterministicForSameInputs()
    {
        var a = RunArchive();
        var b = RunArchive();

        Assert.Equal(a.HeadHash, b.HeadHash);
        Assert.Equal(a.Strata.Select(x => x.IntegrityHash), b.Strata.Select(x => x.IntegrityHash));
    }

    private static DeepTimeArchiveSnapshot RunArchive()
    {
        var runtime = new DeepTimeMacroevolutionRuntime(new DeepTimeSettings(StratumSpanGenerations: 10));
        for (int generation = 1; generation <= 4; generation++)
            runtime.Advance(Batch(generation, Lineage("root", null, generation, 100 + generation)));
        runtime.CommitOpenStratum();
        return runtime.Archive.Snapshot();
    }

    private static DeepTimeBatchObservation Batch(int generation, params DeepTimeLineageObservation[] lineages) =>
        new(generation, lineages);

    private static DeepTimeLineageObservation Lineage(
        string id,
        string? parent,
        int generation,
        long population,
        DeepTimeLineageAuthority authority = DeepTimeLineageAuthority.ExperimentalSimulation,
        double nicheA = 0.7,
        double nicheB = 0.3) =>
        new(
            id,
            parent,
            "family.a",
            generation,
            population,
            new HashSet<string>(StringComparer.Ordinal) { "zone.a" },
            new Dictionary<string, double>(StringComparer.Ordinal)
            {
                ["niche.a"] = nicheA,
                ["niche.b"] = nicheB
            },
            authority,
            MeanDivergence: 0.5);

    private static DeepTimeLineageState State(
        string id,
        DeepTimeLineageAuthority authority,
        string? parent = null) =>
        new(
            id,
            parent,
            "family.a",
            1,
            4,
            100,
            new HashSet<string>(StringComparer.Ordinal) { "zone.a" },
            new Dictionary<string, double>(StringComparer.Ordinal) { ["niche.a"] = 0.8 },
            authority,
            DeepTimeLineageStatus.Active,
            0,
            0.4);
}
