namespace EasternKingdoms.Simulation;

public sealed record LineageDivergenceObservation(
    EvolutionCohort PopulationA,
    EvolutionCohort PopulationB,
    string ProposedLineageId,
    string HybridZoneId,
    double LocalAdaptationA,
    double LocalAdaptationB,
    IReadOnlyDictionary<string, double> AdaptiveTraitsA,
    IReadOnlyDictionary<string, double> AdaptiveTraitsB,
    double TraitDivergence,
    double EcologicalSeparation,
    double GeographicIsolation,
    double PrezygoticIsolation,
    double PostzygoticIsolation,
    double BehavioralIsolation,
    double TemporalIsolation,
    double HybridFrequency,
    double HybridViability,
    double HybridFertility,
    double IntrogressionRate,
    int Generation);

public sealed record LineageDivergenceResult(
    EcotypeProfile EcotypeA,
    EcotypeProfile EcotypeB,
    DivergenceMetrics Divergence,
    ReproductiveIsolationState Isolation,
    HybridZoneProfile HybridZone,
    SpeciationCandidate? Candidate);

public sealed record LineageDivergenceHistoryEntry(
    int Generation,
    string SpeciesId,
    string PopulationA,
    string PopulationB,
    double Divergence,
    double Isolation,
    HybridZoneDisposition HybridDisposition,
    string? CandidateId,
    SpeciationCandidateStatus? CandidateStatus);

public sealed record PhaseXVIISaveState(
    IReadOnlyList<EcotypeProfile> Ecotypes,
    IReadOnlyList<SpeciationCandidate> Candidates,
    IReadOnlyList<SpeciationReviewRecord> Reviews,
    IReadOnlyList<LineageDivergenceHistoryEntry> History,
    EvolutionaryTreeSnapshot EvolutionaryTree);

public sealed class LineageDivergenceOrchestrator
{
    private readonly LineageDivergenceSettings _settings;
    private readonly EcotypeTracker _ecotypes;
    private readonly GeneticDivergenceAnalyzer _genetic = new();
    private readonly DivergenceAnalyzer _divergence;
    private readonly ReproductiveIsolationEvaluator _isolation = new();
    private readonly HybridZoneEvaluator _hybrids;
    private readonly SpeciationCandidateRegistry _candidates;
    private readonly SpeciationReviewGate _reviews;
    private readonly EvolutionaryTree _tree;
    private readonly SpeciationGovernanceService _governance;
    private readonly List<LineageDivergenceHistoryEntry> _history = new();

    public LineageDivergenceOrchestrator(LineageDivergenceSettings? settings = null)
    {
        _settings = settings ?? new LineageDivergenceSettings();
        _settings.Validate();
        _ecotypes = new EcotypeTracker(_settings);
        _divergence = new DivergenceAnalyzer(_settings);
        _hybrids = new HybridZoneEvaluator(_settings);
        _candidates = new SpeciationCandidateRegistry(_settings);
        _reviews = new SpeciationReviewGate();
        _tree = new EvolutionaryTree();
        _governance = new SpeciationGovernanceService(_candidates, _reviews, _tree);
    }

    public SpeciationReviewGate ReviewGate => _reviews;
    public SpeciationCandidateRegistry Candidates => _candidates;
    public EvolutionaryTree EvolutionaryTree => _tree;
    public IReadOnlyList<LineageDivergenceHistoryEntry> History => _history.ToArray();

    public LineageDivergenceResult Observe(LineageDivergenceObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        var a = observation.PopulationA;
        var b = observation.PopulationB;

        if (!StringComparer.Ordinal.Equals(a.SpeciesId, b.SpeciesId))
            throw new InvalidOperationException("Lineage divergence observations require cohorts of the same parent species.");
        if (observation.Generation < 0)
            throw new ArgumentOutOfRangeException(nameof(observation));

        string lineageA = $"{a.SpeciesId}:{a.ZoneId}";
        string lineageB = $"{b.SpeciesId}:{b.ZoneId}";

        var ecotypeA = _ecotypes.Observe(
            a.SpeciesId,
            a.ZoneId,
            lineageA,
            observation.Generation,
            observation.LocalAdaptationA,
            observation.AdaptiveTraitsA);

        var ecotypeB = _ecotypes.Observe(
            b.SpeciesId,
            b.ZoneId,
            lineageB,
            observation.Generation,
            observation.LocalAdaptationB,
            observation.AdaptiveTraitsB);

        var genetic = _genetic.Compare(a, b);
        var divergence = _divergence.Calculate(
            a.SpeciesId,
            a.ZoneId,
            b.ZoneId,
            genetic.MeanTotalVariationDistance,
            observation.TraitDivergence,
            observation.EcologicalSeparation,
            observation.GeographicIsolation);

        var isolation = _isolation.Evaluate(
            a.SpeciesId,
            a.ZoneId,
            b.ZoneId,
            observation.PrezygoticIsolation,
            observation.PostzygoticIsolation,
            observation.BehavioralIsolation,
            observation.TemporalIsolation,
            observation.GeographicIsolation,
            observation.Generation);

        var hybrid = _hybrids.Evaluate(
            observation.HybridZoneId,
            lineageA,
            lineageB,
            observation.HybridFrequency,
            observation.HybridViability,
            observation.HybridFertility,
            observation.IntrogressionRate,
            firstGeneration: observation.Generation,
            currentGeneration: observation.Generation);

        string candidateId = $"speciation:{a.SpeciesId}:{observation.ProposedLineageId}";
        double hybridInstability = hybrid.Disposition == HybridZoneDisposition.ReinforcingIsolation
            ? 1.0 - ((hybrid.HybridViability + hybrid.HybridFertility) * 0.5)
            : 0.0;

        var candidate = _candidates.Observe(
            candidateId,
            a.SpeciesId,
            observation.ProposedLineageId,
            new[] { a.ZoneId, b.ZoneId },
            divergence,
            isolation,
            observation.EcologicalSeparation,
            hybridInstability,
            observation.Generation);

        if (candidate is not null)
        {
            _tree.AddOrUpdateCandidate(candidate);
            _ecotypes.MarkDivergent(ecotypeA.EcotypeId);
            _ecotypes.MarkDivergent(ecotypeB.EcotypeId);
        }

        _history.Add(new LineageDivergenceHistoryEntry(
            observation.Generation,
            a.SpeciesId,
            a.ZoneId,
            b.ZoneId,
            divergence.CompositeDivergence,
            isolation.CompositeIsolation,
            hybrid.Disposition,
            candidate?.CandidateId,
            candidate?.Status));

        return new LineageDivergenceResult(
            candidate is null ? ecotypeA : _ecotypes.Profiles.Single(x => x.EcotypeId == ecotypeA.EcotypeId),
            candidate is null ? ecotypeB : _ecotypes.Profiles.Single(x => x.EcotypeId == ecotypeB.EcotypeId),
            divergence,
            isolation,
            hybrid,
            candidate);
    }

    public SpeciationCandidate Review(SpeciationReviewRecord review) =>
        _governance.ApplyReview(review);

    public PhaseXVIISaveState Snapshot() =>
        new(
            _ecotypes.Profiles.ToArray(),
            _candidates.Candidates.ToArray(),
            _reviews.Reviews.ToArray(),
            _history
                .OrderBy(x => x.Generation)
                .ThenBy(x => x.SpeciesId, StringComparer.Ordinal)
                .ThenBy(x => x.PopulationA, StringComparer.Ordinal)
                .ThenBy(x => x.PopulationB, StringComparer.Ordinal)
                .ToArray(),
            _tree.Snapshot());
}
