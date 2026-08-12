namespace EasternKingdoms.Simulation;

public sealed record LineageDivergenceSettings(
    double AllelicWeight = 0.35,
    double TraitWeight = 0.25,
    double EcologicalWeight = 0.20,
    double GeographicWeight = 0.20,
    double DivergenceThreshold = 0.65,
    double IsolationThreshold = 0.60,
    double EcologicalDistinctnessThreshold = 0.50,
    int EcotypePersistenceGenerations = 2,
    int CandidatePersistenceGenerations = 3,
    double HybridStableFrequencyMinimum = 0.05,
    double HybridViabilityMinimum = 0.50,
    double HybridFertilityMinimum = 0.40)
{
    public void Validate()
    {
        foreach (double weight in new[] { AllelicWeight, TraitWeight, EcologicalWeight, GeographicWeight })
        {
            if (weight < 0.0)
                throw new InvalidOperationException("Divergence weights cannot be negative.");
        }

        double weightTotal = AllelicWeight + TraitWeight + EcologicalWeight + GeographicWeight;
        if (weightTotal <= 0.0)
            throw new InvalidOperationException("At least one divergence weight must be positive.");
        if (EcotypePersistenceGenerations < 1)
            throw new InvalidOperationException("Ecotype persistence must be at least one generation.");
        if (CandidatePersistenceGenerations < 1)
            throw new InvalidOperationException("Candidate persistence must be at least one generation.");

        foreach (double value in new[]
                 {
                     DivergenceThreshold,
                     IsolationThreshold,
                     EcologicalDistinctnessThreshold,
                     HybridStableFrequencyMinimum,
                     HybridViabilityMinimum,
                     HybridFertilityMinimum
                 })
        {
            if (value is < 0.0 or > 1.0)
                throw new InvalidOperationException("Phase XVII thresholds must remain inside [0, 1].");
        }
    }
}

public enum EcotypeStatus
{
    Observed,
    Persistent,
    Divergent
}

public sealed record EcotypeProfile(
    string EcotypeId,
    string SpeciesId,
    string ZoneId,
    string LineageId,
    IReadOnlyDictionary<string, double> AdaptiveTraitWeights,
    double LocalAdaptationScore,
    int FirstObservedGeneration,
    int LastObservedGeneration,
    int ConsecutiveObservations,
    EcotypeStatus Status);

public sealed class EcotypeTracker
{
    private readonly LineageDivergenceSettings _settings;
    private readonly Dictionary<string, EcotypeProfile> _profiles = new(StringComparer.Ordinal);

    public EcotypeTracker(LineageDivergenceSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _settings.Validate();
    }

    public IReadOnlyCollection<EcotypeProfile> Profiles =>
        _profiles.Values
            .OrderBy(x => x.EcotypeId, StringComparer.Ordinal)
            .ToArray();

    public EcotypeProfile Observe(
        string speciesId,
        string zoneId,
        string lineageId,
        int generation,
        double localAdaptationScore,
        IReadOnlyDictionary<string, double>? adaptiveTraitWeights = null)
    {
        RequireText(speciesId, nameof(speciesId));
        RequireText(zoneId, nameof(zoneId));
        RequireText(lineageId, nameof(lineageId));
        if (generation < 0)
            throw new ArgumentOutOfRangeException(nameof(generation));

        string id = $"ecotype:{speciesId}:{lineageId}:{zoneId}";
        int consecutive = 1;
        int first = generation;

        if (_profiles.TryGetValue(id, out var previous))
        {
            first = previous.FirstObservedGeneration;
            consecutive = previous.LastObservedGeneration == generation - 1
                ? previous.ConsecutiveObservations + 1
                : 1;
        }

        EcotypeStatus status = previous?.Status == EcotypeStatus.Divergent
            ? EcotypeStatus.Divergent
            : consecutive >= _settings.EcotypePersistenceGenerations
                ? EcotypeStatus.Persistent
                : EcotypeStatus.Observed;

        var profile = new EcotypeProfile(
            id,
            speciesId,
            zoneId,
            lineageId,
            NormalizeWeights(adaptiveTraitWeights),
            Clamp01(localAdaptationScore),
            first,
            generation,
            consecutive,
            status);

        _profiles[id] = profile;
        return profile;
    }

    public EcotypeProfile MarkDivergent(string ecotypeId)
    {
        if (!_profiles.TryGetValue(ecotypeId, out var current))
            throw new KeyNotFoundException($"Unknown ecotype {ecotypeId}.");

        var updated = current with { Status = EcotypeStatus.Divergent };
        _profiles[ecotypeId] = updated;
        return updated;
    }

    private static IReadOnlyDictionary<string, double> NormalizeWeights(
        IReadOnlyDictionary<string, double>? values)
    {
        if (values is null || values.Count == 0)
            return new Dictionary<string, double>(StringComparer.Ordinal);

        return values
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .ToDictionary(
                x => x.Key,
                x => Math.Clamp(x.Value, -1.0, 1.0),
                StringComparer.Ordinal);
    }

    private static void RequireText(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value cannot be blank.", name);
    }

    private static double Clamp01(double value) => Math.Clamp(value, 0.0, 1.0);
}

public sealed record DivergenceMetrics(
    string SpeciesId,
    string PopulationA,
    string PopulationB,
    double AllelicDivergence,
    double TraitDivergence,
    double EcologicalSeparation,
    double GeographicIsolation,
    double CompositeDivergence);

public sealed class DivergenceAnalyzer
{
    private readonly LineageDivergenceSettings _settings;

    public DivergenceAnalyzer(LineageDivergenceSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _settings.Validate();
    }

    public DivergenceMetrics Calculate(
        string speciesId,
        string populationA,
        string populationB,
        double allelicDivergence,
        double traitDivergence,
        double ecologicalSeparation,
        double geographicIsolation)
    {
        double denominator =
            _settings.AllelicWeight +
            _settings.TraitWeight +
            _settings.EcologicalWeight +
            _settings.GeographicWeight;

        double composite =
            (Clamp01(allelicDivergence) * _settings.AllelicWeight +
             Clamp01(traitDivergence) * _settings.TraitWeight +
             Clamp01(ecologicalSeparation) * _settings.EcologicalWeight +
             Clamp01(geographicIsolation) * _settings.GeographicWeight) /
            denominator;

        return new DivergenceMetrics(
            speciesId,
            populationA,
            populationB,
            Clamp01(allelicDivergence),
            Clamp01(traitDivergence),
            Clamp01(ecologicalSeparation),
            Clamp01(geographicIsolation),
            Clamp01(composite));
    }

    private static double Clamp01(double value) => Math.Clamp(value, 0.0, 1.0);
}

public sealed record ReproductiveIsolationState(
    string SpeciesId,
    string PopulationA,
    string PopulationB,
    double PrezygoticIsolation,
    double PostzygoticIsolation,
    double BehavioralIsolation,
    double TemporalIsolation,
    double GeographicIsolation,
    double CompositeIsolation,
    int GenerationObserved);

public sealed class ReproductiveIsolationEvaluator
{
    public ReproductiveIsolationState Evaluate(
        string speciesId,
        string populationA,
        string populationB,
        double prezygotic,
        double postzygotic,
        double behavioral,
        double temporal,
        double geographic,
        int generation)
    {
        if (generation < 0)
            throw new ArgumentOutOfRangeException(nameof(generation));

        double composite = Math.Clamp(
            Clamp01(prezygotic) * 0.25 +
            Clamp01(postzygotic) * 0.25 +
            Clamp01(behavioral) * 0.20 +
            Clamp01(temporal) * 0.10 +
            Clamp01(geographic) * 0.20,
            0.0,
            1.0);

        return new ReproductiveIsolationState(
            speciesId,
            populationA,
            populationB,
            Clamp01(prezygotic),
            Clamp01(postzygotic),
            Clamp01(behavioral),
            Clamp01(temporal),
            Clamp01(geographic),
            composite,
            generation);
    }

    private static double Clamp01(double value) => Math.Clamp(value, 0.0, 1.0);
}

public enum HybridZoneDisposition
{
    Absent,
    Stable,
    Introgressing,
    ReinforcingIsolation
}

public sealed record HybridZoneProfile(
    string ZoneId,
    string ParentLineageA,
    string ParentLineageB,
    double HybridFrequency,
    double HybridViability,
    double HybridFertility,
    double IntrogressionRate,
    HybridZoneDisposition Disposition,
    int FirstObservedGeneration,
    int LastObservedGeneration);

public sealed class HybridZoneEvaluator
{
    private readonly LineageDivergenceSettings _settings;

    public HybridZoneEvaluator(LineageDivergenceSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _settings.Validate();
    }

    public HybridZoneProfile Evaluate(
        string zoneId,
        string lineageA,
        string lineageB,
        double hybridFrequency,
        double hybridViability,
        double hybridFertility,
        double introgressionRate,
        int firstGeneration,
        int currentGeneration)
    {
        double frequency = Clamp01(hybridFrequency);
        double viability = Clamp01(hybridViability);
        double fertility = Clamp01(hybridFertility);
        double introgression = Clamp01(introgressionRate);

        HybridZoneDisposition disposition;
        if (frequency < _settings.HybridStableFrequencyMinimum)
            disposition = HybridZoneDisposition.Absent;
        else if (viability >= _settings.HybridViabilityMinimum &&
                 fertility >= _settings.HybridFertilityMinimum)
            disposition = introgression >= 0.25
                ? HybridZoneDisposition.Introgressing
                : HybridZoneDisposition.Stable;
        else
            disposition = HybridZoneDisposition.ReinforcingIsolation;

        return new HybridZoneProfile(
            zoneId,
            lineageA,
            lineageB,
            frequency,
            viability,
            fertility,
            introgression,
            disposition,
            firstGeneration,
            currentGeneration);
    }

    private static double Clamp01(double value) => Math.Clamp(value, 0.0, 1.0);
}

public enum SpeciationCandidateStatus
{
    Observed,
    Persistent,
    ReviewReady,
    Rejected,
    SimulationAuthorized,
    CanonAuthoringAuthorized
}

public sealed record SpeciationCandidate(
    string CandidateId,
    string ParentSpeciesId,
    string ProposedLineageId,
    IReadOnlyList<string> OriginZones,
    double DivergenceScore,
    double IsolationScore,
    double EcologicalDistinctness,
    double HybridInstability,
    int FirstObservedGeneration,
    int LastObservedGeneration,
    int ConsecutiveQualifyingGenerations,
    SpeciationCandidateStatus Status);

public sealed class SpeciationCandidateRegistry
{
    private readonly LineageDivergenceSettings _settings;
    private readonly Dictionary<string, SpeciationCandidate> _candidates = new(StringComparer.Ordinal);

    public SpeciationCandidateRegistry(LineageDivergenceSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _settings.Validate();
    }

    public IReadOnlyCollection<SpeciationCandidate> Candidates =>
        _candidates.Values.OrderBy(x => x.CandidateId, StringComparer.Ordinal).ToArray();

    public bool TryGet(string candidateId, out SpeciationCandidate candidate)
    {
        if (_candidates.TryGetValue(candidateId, out var found))
        {
            candidate = found;
            return true;
        }

        candidate = null!;
        return false;
    }

    public SpeciationCandidate? Observe(
        string candidateId,
        string parentSpeciesId,
        string proposedLineageId,
        IReadOnlyList<string> originZones,
        DivergenceMetrics divergence,
        ReproductiveIsolationState isolation,
        double ecologicalDistinctness,
        double hybridInstability,
        int generation)
    {
        bool qualifies =
            divergence.CompositeDivergence >= _settings.DivergenceThreshold &&
            isolation.CompositeIsolation >= _settings.IsolationThreshold &&
            ecologicalDistinctness >= _settings.EcologicalDistinctnessThreshold;

        if (!qualifies)
            return null;

        int first = generation;
        int consecutive = 1;
        SpeciationCandidate? previous = null;
        if (_candidates.TryGetValue(candidateId, out previous))
        {
            if (previous.Status == SpeciationCandidateStatus.Rejected)
                return previous;

            first = previous.FirstObservedGeneration;
            consecutive = previous.LastObservedGeneration == generation - 1
                ? previous.ConsecutiveQualifyingGenerations + 1
                : 1;
        }

        SpeciationCandidateStatus status = previous?.Status switch
        {
            SpeciationCandidateStatus.SimulationAuthorized => SpeciationCandidateStatus.SimulationAuthorized,
            SpeciationCandidateStatus.CanonAuthoringAuthorized => SpeciationCandidateStatus.CanonAuthoringAuthorized,
            _ => consecutive switch
            {
                var x when x >= _settings.CandidatePersistenceGenerations => SpeciationCandidateStatus.ReviewReady,
                > 1 => SpeciationCandidateStatus.Persistent,
                _ => SpeciationCandidateStatus.Observed
            }
        };

        var candidate = new SpeciationCandidate(
            candidateId,
            parentSpeciesId,
            proposedLineageId,
            originZones
                .Distinct(StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray(),
            divergence.CompositeDivergence,
            isolation.CompositeIsolation,
            Clamp01(ecologicalDistinctness),
            Clamp01(hybridInstability),
            first,
            generation,
            consecutive,
            status);

        _candidates[candidateId] = candidate;
        return candidate;
    }

    public SpeciationCandidate SetStatus(string candidateId, SpeciationCandidateStatus status)
    {
        if (!_candidates.TryGetValue(candidateId, out var candidate))
            throw new KeyNotFoundException($"Unknown speciation candidate {candidateId}.");

        var updated = candidate with { Status = status };
        _candidates[candidateId] = updated;
        return updated;
    }

    private static double Clamp01(double value) => Math.Clamp(value, 0.0, 1.0);
}
