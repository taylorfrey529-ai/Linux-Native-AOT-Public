namespace EasternKingdoms.Simulation;

public enum SpeciationReviewDecision
{
    Hold,
    Reject,
    ApproveSimulation,
    AuthorizeCanonAuthoring
}

public enum SpeciationActivationDisposition
{
    Held,
    Rejected,
    SimulationAuthorized,
    CanonAuthoringAuthorized
}

public sealed record SpeciationReviewRecord(
    string ReviewId,
    string CandidateId,
    string Reviewer,
    SpeciationReviewDecision Decision,
    string Notes,
    int ReviewGeneration,
    string? ProvenanceId = null);

public sealed class SpeciationReviewGate
{
    private readonly Dictionary<string, SpeciationReviewRecord> _reviews = new(StringComparer.Ordinal);

    public IReadOnlyCollection<SpeciationReviewRecord> Reviews =>
        _reviews.Values
            .OrderBy(x => x.CandidateId, StringComparer.Ordinal)
            .ThenBy(x => x.ReviewGeneration)
            .ThenBy(x => x.ReviewId, StringComparer.Ordinal)
            .ToArray();

    public void Submit(SpeciationReviewRecord review)
    {
        ArgumentNullException.ThrowIfNull(review);
        if (string.IsNullOrWhiteSpace(review.ReviewId))
            throw new ArgumentException("Review ID cannot be blank.", nameof(review));
        if (string.IsNullOrWhiteSpace(review.CandidateId))
            throw new ArgumentException("Candidate ID cannot be blank.", nameof(review));
        if (string.IsNullOrWhiteSpace(review.Reviewer))
            throw new ArgumentException("Reviewer cannot be blank.", nameof(review));
        if (review.ReviewGeneration < 0)
            throw new ArgumentOutOfRangeException(nameof(review));

        _reviews[review.ReviewId] = review;
    }

    public SpeciationActivationDisposition Resolve(string candidateId)
    {
        var latest = _reviews.Values
            .Where(x => StringComparer.Ordinal.Equals(x.CandidateId, candidateId))
            .OrderByDescending(x => x.ReviewGeneration)
            .ThenByDescending(x => x.ReviewId, StringComparer.Ordinal)
            .FirstOrDefault();

        if (latest is null)
            return SpeciationActivationDisposition.Held;

        return latest.Decision switch
        {
            SpeciationReviewDecision.Reject => SpeciationActivationDisposition.Rejected,
            SpeciationReviewDecision.ApproveSimulation => SpeciationActivationDisposition.SimulationAuthorized,
            SpeciationReviewDecision.AuthorizeCanonAuthoring => SpeciationActivationDisposition.CanonAuthoringAuthorized,
            _ => SpeciationActivationDisposition.Held
        };
    }

    public bool MayEnterExperimentalSimulation(string candidateId) =>
        Resolve(candidateId) is SpeciationActivationDisposition.SimulationAuthorized or
            SpeciationActivationDisposition.CanonAuthoringAuthorized;

    public bool MayBeAuthoredIntoCanon(string candidateId) =>
        Resolve(candidateId) == SpeciationActivationDisposition.CanonAuthoringAuthorized;
}

public sealed class SpeciationGovernanceService
{
    private readonly SpeciationCandidateRegistry _candidates;
    private readonly SpeciationReviewGate _reviews;
    private readonly EvolutionaryTree _tree;

    public SpeciationGovernanceService(
        SpeciationCandidateRegistry candidates,
        SpeciationReviewGate reviews,
        EvolutionaryTree tree)
    {
        _candidates = candidates ?? throw new ArgumentNullException(nameof(candidates));
        _reviews = reviews ?? throw new ArgumentNullException(nameof(reviews));
        _tree = tree ?? throw new ArgumentNullException(nameof(tree));
    }

    public SpeciationCandidate ApplyReview(SpeciationReviewRecord review)
    {
        ArgumentNullException.ThrowIfNull(review);
        if (!_candidates.TryGet(review.CandidateId, out var candidate))
            throw new KeyNotFoundException($"Unknown speciation candidate {review.CandidateId}.");

        if ((review.Decision is SpeciationReviewDecision.ApproveSimulation or
             SpeciationReviewDecision.AuthorizeCanonAuthoring) &&
            candidate.Status != SpeciationCandidateStatus.ReviewReady &&
            candidate.Status != SpeciationCandidateStatus.SimulationAuthorized)
        {
            throw new InvalidOperationException(
                $"Candidate {candidate.CandidateId} must be review-ready before activation authorization.");
        }

        _reviews.Submit(review);
        SpeciationCandidateStatus status = _reviews.Resolve(candidate.CandidateId) switch
        {
            SpeciationActivationDisposition.Rejected => SpeciationCandidateStatus.Rejected,
            SpeciationActivationDisposition.SimulationAuthorized => SpeciationCandidateStatus.SimulationAuthorized,
            SpeciationActivationDisposition.CanonAuthoringAuthorized => SpeciationCandidateStatus.CanonAuthoringAuthorized,
            _ => candidate.Status
        };

        var updated = _candidates.SetStatus(candidate.CandidateId, status);
        _tree.ApplyCandidateDisposition(updated.CandidateId, _reviews.Resolve(updated.CandidateId), review.ReviewGeneration);
        return updated;
    }
}
