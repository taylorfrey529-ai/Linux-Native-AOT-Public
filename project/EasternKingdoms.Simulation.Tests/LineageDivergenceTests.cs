using EasternKingdoms.Simulation;
using Xunit;

namespace EasternKingdoms.Simulation.Tests;

public sealed class LineageDivergenceTests
{
    [Fact]
    public void EcotypeRequiresPersistenceAcrossGenerations()
    {
        var tracker = new EcotypeTracker(new LineageDivergenceSettings(EcotypePersistenceGenerations: 2));

        var first = tracker.Observe("species.a", "zone.a", "lineage.a", 1, 0.7);
        var second = tracker.Observe("species.a", "zone.a", "lineage.a", 2, 0.72);

        Assert.Equal(EcotypeStatus.Observed, first.Status);
        Assert.Equal(EcotypeStatus.Persistent, second.Status);
        Assert.Equal(2, second.ConsecutiveObservations);
    }

    [Fact]
    public void LowDivergenceDoesNotCreateSpeciationCandidate()
    {
        var runtime = new LineageDivergenceOrchestrator();
        var same = Cohort("species.a", "zone.a", "A", "B", 0.5, 0.5);
        var other = Cohort("species.a", "zone.b", "A", "B", 0.5, 0.5);

        var result = runtime.Observe(Observation(same, other, generation: 1,
            traitDivergence: 0.1,
            ecologicalSeparation: 0.1,
            geographicIsolation: 0.1,
            isolation: 0.9));

        Assert.Null(result.Candidate);
    }

    [Fact]
    public void HighDivergenceWithLowIsolationDoesNotCreateCandidate()
    {
        var runtime = new LineageDivergenceOrchestrator();
        var a = Cohort("species.a", "zone.a", "A", "B", 1.0, 0.0);
        var b = Cohort("species.a", "zone.b", "A", "B", 0.0, 1.0);

        var result = runtime.Observe(Observation(a, b, generation: 1,
            traitDivergence: 0.95,
            ecologicalSeparation: 0.95,
            geographicIsolation: 0.95,
            isolation: 0.05));

        Assert.Null(result.Candidate);
        Assert.True(result.Divergence.CompositeDivergence > 0.8);
        Assert.True(result.Isolation.CompositeIsolation < new LineageDivergenceSettings().IsolationThreshold);
    }

    [Fact]
    public void ViableFertileHybridZoneCanRemainStable()
    {
        var evaluator = new HybridZoneEvaluator(new LineageDivergenceSettings());

        var result = evaluator.Evaluate(
            "zone.border",
            "lineage.a",
            "lineage.b",
            hybridFrequency: 0.20,
            hybridViability: 0.80,
            hybridFertility: 0.75,
            introgressionRate: 0.10,
            firstGeneration: 1,
            currentGeneration: 4);

        Assert.Equal(HybridZoneDisposition.Stable, result.Disposition);
    }

    [Fact]
    public void LowHybridViabilityReinforcesIsolation()
    {
        var evaluator = new HybridZoneEvaluator(new LineageDivergenceSettings());

        var result = evaluator.Evaluate(
            "zone.border",
            "lineage.a",
            "lineage.b",
            hybridFrequency: 0.20,
            hybridViability: 0.10,
            hybridFertility: 0.15,
            introgressionRate: 0.01,
            firstGeneration: 1,
            currentGeneration: 4);

        Assert.Equal(HybridZoneDisposition.ReinforcingIsolation, result.Disposition);
    }

    [Fact]
    public void CandidateRemainsHeldWithoutReview()
    {
        var runtime = MakeReviewReadyRuntime(out string candidateId);

        Assert.Equal(SpeciationActivationDisposition.Held, runtime.ReviewGate.Resolve(candidateId));
        Assert.False(runtime.ReviewGate.MayEnterExperimentalSimulation(candidateId));
    }

    [Fact]
    public void SimulationApprovalDoesNotAuthorizeCanonAuthoring()
    {
        var runtime = MakeReviewReadyRuntime(out string candidateId);

        var updated = runtime.Review(new SpeciationReviewRecord(
            "review-001",
            candidateId,
            "continuity-review",
            SpeciationReviewDecision.ApproveSimulation,
            "Approved for experimental branches only.",
            ReviewGeneration: 3));

        Assert.Equal(SpeciationCandidateStatus.SimulationAuthorized, updated.Status);
        Assert.True(runtime.ReviewGate.MayEnterExperimentalSimulation(candidateId));
        Assert.False(runtime.ReviewGate.MayBeAuthoredIntoCanon(candidateId));
    }

    [Fact]
    public void EvolutionaryTreeSnapshotIsDeterministic()
    {
        var runtimeA = MakeReviewReadyRuntime(out _);
        var runtimeB = MakeReviewReadyRuntime(out _);

        var a = runtimeA.Snapshot().EvolutionaryTree;
        var b = runtimeB.Snapshot().EvolutionaryTree;

        Assert.Equal(a.IntegrityHash, b.IntegrityHash);
        Assert.Equal(a.Nodes, b.Nodes);
    }

    private static LineageDivergenceOrchestrator MakeReviewReadyRuntime(out string candidateId)
    {
        var runtime = new LineageDivergenceOrchestrator(
            new LineageDivergenceSettings(CandidatePersistenceGenerations: 3));

        for (int generation = 1; generation <= 3; generation++)
        {
            var a = Cohort("species.a", "zone.a", "A", "B", 1.0, 0.0, generation);
            var b = Cohort("species.a", "zone.b", "A", "B", 0.0, 1.0, generation);
            runtime.Observe(Observation(a, b, generation,
                traitDivergence: 0.95,
                ecologicalSeparation: 0.90,
                geographicIsolation: 0.90,
                isolation: 0.90));
        }

        candidateId = "speciation:species.a:lineage.new";
        Assert.True(runtime.Candidates.TryGet(candidateId, out var candidate));
        Assert.Equal(SpeciationCandidateStatus.ReviewReady, candidate.Status);
        return runtime;
    }

    private static EvolutionCohort Cohort(
        string speciesId,
        string zoneId,
        string alleleA,
        string alleleB,
        double frequencyA,
        double frequencyB,
        int generation = 1)
    {
        return new EvolutionCohort(
            $"{speciesId}:{zoneId}",
            speciesId,
            zoneId,
            generation,
            Population: 100,
            new[]
            {
                new CohortAlleleFrequency("L1", alleleA, frequencyA),
                new CohortAlleleFrequency("L1", alleleB, frequencyB)
            });
    }

    private static LineageDivergenceObservation Observation(
        EvolutionCohort a,
        EvolutionCohort b,
        int generation,
        double traitDivergence,
        double ecologicalSeparation,
        double geographicIsolation,
        double isolation)
    {
        return new LineageDivergenceObservation(
            a,
            b,
            ProposedLineageId: "lineage.new",
            HybridZoneId: "zone.border",
            LocalAdaptationA: 0.75,
            LocalAdaptationB: 0.80,
            AdaptiveTraitsA: new Dictionary<string, double>(StringComparer.Ordinal)
            {
                ["resilience"] = 0.8
            },
            AdaptiveTraitsB: new Dictionary<string, double>(StringComparer.Ordinal)
            {
                ["resilience"] = -0.4
            },
            traitDivergence,
            ecologicalSeparation,
            geographicIsolation,
            PrezygoticIsolation: isolation,
            PostzygoticIsolation: isolation,
            BehavioralIsolation: isolation,
            TemporalIsolation: isolation,
            HybridFrequency: 0.10,
            HybridViability: 0.30,
            HybridFertility: 0.30,
            IntrogressionRate: 0.05,
            generation);
    }
}
