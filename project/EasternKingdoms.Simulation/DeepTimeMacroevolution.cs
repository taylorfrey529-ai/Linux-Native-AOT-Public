using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace EasternKingdoms.Simulation;

public sealed record DeepTimeSettings(
    int ExtinctionPersistenceGenerations = 3,
    int RadiationWindowGenerations = 12,
    int MinimumRadiationBranches = 3,
    double MinimumRadiationNicheDivergence = 0.35,
    int StratumSpanGenerations = 25)
{
    public void Validate()
    {
        if (ExtinctionPersistenceGenerations < 1)
            throw new InvalidOperationException("Extinction persistence must be positive.");
        if (RadiationWindowGenerations < 1)
            throw new InvalidOperationException("Radiation window must be positive.");
        if (MinimumRadiationBranches < 2)
            throw new InvalidOperationException("Adaptive radiation requires at least two descendant branches.");
        if (MinimumRadiationNicheDivergence is < 0.0 or > 1.0)
            throw new InvalidOperationException("Niche divergence threshold must remain inside [0, 1].");
        if (StratumSpanGenerations < 1)
            throw new InvalidOperationException("Stratum span must be positive.");
    }
}

public enum DeepTimeLineageAuthority
{
    ExistingAuthorizedSpecies,
    CandidateTestHistory,
    ExperimentalSimulation,
    CanonAuthoringAuthorized,
    Rejected
}

public enum DeepTimeLineageStatus
{
    Active,
    Declining,
    Extinct
}

public sealed record DeepTimeLineageObservation(
    string LineageId,
    string? ParentLineageId,
    string SpeciesFamilyId,
    int Generation,
    long Population,
    IReadOnlySet<string> OccupiedZones,
    IReadOnlyDictionary<string, double> NicheVector,
    DeepTimeLineageAuthority Authority,
    double MeanDivergence = 0.0);

public sealed record DeepTimeLineageState(
    string LineageId,
    string? ParentLineageId,
    string SpeciesFamilyId,
    int FirstObservedGeneration,
    int LastObservedGeneration,
    long Population,
    IReadOnlySet<string> OccupiedZones,
    IReadOnlyDictionary<string, double> NicheVector,
    DeepTimeLineageAuthority Authority,
    DeepTimeLineageStatus Status,
    int ConsecutiveZeroPopulationGenerations,
    double MeanDivergence);

public sealed record CladeRecord(
    string CladeId,
    string RootLineageId,
    IReadOnlyList<string> MemberLineageIds,
    int FirstGeneration,
    int LastGeneration,
    int LivingBranches,
    int ExtinctBranches,
    DeepTimeLineageAuthority MaximumAuthority);

public sealed record AdaptiveRadiationEvent(
    string EventId,
    string RootLineageId,
    int FirstBranchGeneration,
    int DetectionGeneration,
    IReadOnlyList<string> DescendantLineageIds,
    double MeanNicheDivergence,
    bool ReviewOnly);

public sealed record LineageExtinctionEvent(
    string EventId,
    string LineageId,
    int Generation,
    int PersistenceGenerations,
    DeepTimeLineageAuthority Authority,
    IReadOnlySet<string> FormerZones);

public sealed record EcologicalReplacementEvent(
    string EventId,
    string ExtinctLineageId,
    string ReplacementLineageId,
    int Generation,
    IReadOnlyList<string> SharedNiches,
    double NicheSimilarity,
    bool ReviewOnly);

public sealed record DeepTimeGenerationResult(
    int Generation,
    IReadOnlyList<DeepTimeLineageState> Lineages,
    IReadOnlyList<AdaptiveRadiationEvent> RadiationEvents,
    IReadOnlyList<LineageExtinctionEvent> ExtinctionEvents,
    IReadOnlyList<EcologicalReplacementEvent> ReplacementEvents,
    IReadOnlyList<CladeRecord> Clades);

public sealed class DeepTimeLineageLedger
{
    private readonly DeepTimeSettings _settings;
    private readonly Dictionary<string, DeepTimeLineageState> _states = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _lastObservedGeneration = new(StringComparer.Ordinal);

    public DeepTimeLineageLedger(DeepTimeSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _settings.Validate();
    }

    public IReadOnlyCollection<DeepTimeLineageState> States =>
        _states.Values.OrderBy(x => x.LineageId, StringComparer.Ordinal).ToArray();

    public DeepTimeLineageState Observe(DeepTimeLineageObservation observation)
    {
        ValidateObservation(observation);
        if (_lastObservedGeneration.TryGetValue(observation.LineageId, out int last) && observation.Generation <= last)
            throw new InvalidOperationException($"Lineage {observation.LineageId} observations must advance generation monotonically.");

        _lastObservedGeneration[observation.LineageId] = observation.Generation;

        int first = observation.Generation;
        int zeroCount = observation.Population == 0 ? 1 : 0;
        DeepTimeLineageStatus status = observation.Population == 0 ? DeepTimeLineageStatus.Declining : DeepTimeLineageStatus.Active;
        DeepTimeLineageAuthority authority = observation.Authority;

        if (_states.TryGetValue(observation.LineageId, out var previous))
        {
            if (!StringComparer.Ordinal.Equals(previous.ParentLineageId, observation.ParentLineageId))
                throw new InvalidOperationException($"Lineage {observation.LineageId} cannot change parent identity across deep time.");
            if (!StringComparer.Ordinal.Equals(previous.SpeciesFamilyId, observation.SpeciesFamilyId))
                throw new InvalidOperationException($"Lineage {observation.LineageId} cannot change species-family identity across deep time.");

            first = previous.FirstObservedGeneration;
            authority = previous.Authority == DeepTimeLineageAuthority.Rejected
                ? DeepTimeLineageAuthority.Rejected
                : MaxAuthority(previous.Authority, observation.Authority);
            zeroCount = observation.Population == 0
                ? previous.LastObservedGeneration == observation.Generation - 1
                    ? previous.ConsecutiveZeroPopulationGenerations + 1
                    : 1
                : 0;

            if (previous.Status == DeepTimeLineageStatus.Extinct)
            {
                if (observation.Population > 0)
                    throw new InvalidOperationException($"Extinct lineage {observation.LineageId} cannot be revived in place; recolonization must use a new lineage or explicit restoration record.");
                status = DeepTimeLineageStatus.Extinct;
            }
            else if (zeroCount >= _settings.ExtinctionPersistenceGenerations)
            {
                status = DeepTimeLineageStatus.Extinct;
            }
            else
            {
                status = observation.Population == 0 ? DeepTimeLineageStatus.Declining : DeepTimeLineageStatus.Active;
            }
        }

        var state = new DeepTimeLineageState(
            observation.LineageId,
            observation.ParentLineageId,
            observation.SpeciesFamilyId,
            first,
            observation.Generation,
            observation.Population,
            NormalizeSet(observation.OccupiedZones),
            NormalizeVector(observation.NicheVector),
            authority,
            status,
            zeroCount,
            Math.Clamp(observation.MeanDivergence, 0.0, 1.0));

        _states[observation.LineageId] = state;
        return state;
    }

    public static DeepTimeLineageAuthority FromTreeNode(EvolutionaryTreeNodeStatus status) => status switch
    {
        EvolutionaryTreeNodeStatus.ActiveSpecies => DeepTimeLineageAuthority.ExistingAuthorizedSpecies,
        EvolutionaryTreeNodeStatus.ExperimentalLineage => DeepTimeLineageAuthority.ExperimentalSimulation,
        EvolutionaryTreeNodeStatus.CanonAuthoringAuthorized => DeepTimeLineageAuthority.CanonAuthoringAuthorized,
        EvolutionaryTreeNodeStatus.Rejected => DeepTimeLineageAuthority.Rejected,
        _ => DeepTimeLineageAuthority.CandidateTestHistory
    };

    public static DeepTimeLineageAuthority MaxAuthority(DeepTimeLineageAuthority a, DeepTimeLineageAuthority b)
    {
        static int Rank(DeepTimeLineageAuthority value) => value switch
        {
            DeepTimeLineageAuthority.Rejected => -1,
            DeepTimeLineageAuthority.CandidateTestHistory => 0,
            DeepTimeLineageAuthority.ExperimentalSimulation => 1,
            DeepTimeLineageAuthority.CanonAuthoringAuthorized => 2,
            DeepTimeLineageAuthority.ExistingAuthorizedSpecies => 3,
            _ => 0
        };

        return Rank(a) >= Rank(b) ? a : b;
    }

    private static void ValidateObservation(DeepTimeLineageObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        if (string.IsNullOrWhiteSpace(observation.LineageId))
            throw new ArgumentException("Lineage ID is required.", nameof(observation));
        if (string.IsNullOrWhiteSpace(observation.SpeciesFamilyId))
            throw new ArgumentException("Species family ID is required.", nameof(observation));
        if (StringComparer.Ordinal.Equals(observation.LineageId, observation.ParentLineageId))
            throw new InvalidOperationException("A lineage cannot be its own parent.");
        if (observation.Generation < 0)
            throw new ArgumentOutOfRangeException(nameof(observation));
        if (observation.Population < 0)
            throw new ArgumentOutOfRangeException(nameof(observation));
        foreach (var item in observation.NicheVector)
        {
            if (string.IsNullOrWhiteSpace(item.Key) || item.Value is < 0.0 or > 1.0)
                throw new InvalidOperationException("Niche vectors require non-blank keys and values inside [0, 1].");
        }
    }

    private static IReadOnlySet<string> NormalizeSet(IEnumerable<string> values) =>
        values.Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet(StringComparer.Ordinal);

    private static IReadOnlyDictionary<string, double> NormalizeVector(IReadOnlyDictionary<string, double> values) =>
        values.OrderBy(x => x.Key, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => Math.Clamp(x.Value, 0.0, 1.0), StringComparer.Ordinal);
}

public sealed class AdaptiveRadiationDetector
{
    private readonly DeepTimeSettings _settings;

    public AdaptiveRadiationDetector(DeepTimeSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _settings.Validate();
    }

    public AdaptiveRadiationEvent? Detect(string rootLineageId, IEnumerable<DeepTimeLineageState> lineages, int generation)
    {
        var descendants = lineages
            .Where(x => StringComparer.Ordinal.Equals(x.ParentLineageId, rootLineageId))
            .Where(x => x.FirstObservedGeneration >= generation - _settings.RadiationWindowGenerations)
            .Where(x => x.FirstObservedGeneration <= generation)
            .Where(x => x.Authority != DeepTimeLineageAuthority.Rejected)
            .OrderBy(x => x.LineageId, StringComparer.Ordinal)
            .ToArray();

        if (descendants.Length < _settings.MinimumRadiationBranches)
            return null;

        double niche = MeanPairwiseNicheDistance(descendants);
        if (niche < _settings.MinimumRadiationNicheDivergence)
            return null;

        return new AdaptiveRadiationEvent(
            $"radiation:{rootLineageId}:g{generation}",
            rootLineageId,
            descendants.Min(x => x.FirstObservedGeneration),
            generation,
            descendants.Select(x => x.LineageId).ToArray(),
            niche,
            ReviewOnly: descendants.Any(x => x.Authority is DeepTimeLineageAuthority.CandidateTestHistory or DeepTimeLineageAuthority.ExperimentalSimulation));
    }

    internal static double MeanPairwiseNicheDistance(IReadOnlyList<DeepTimeLineageState> lineages)
    {
        if (lineages.Count < 2)
            return 0.0;

        double total = 0.0;
        int pairs = 0;
        for (int i = 0; i < lineages.Count; i++)
        for (int j = i + 1; j < lineages.Count; j++)
        {
            total += NicheDistance(lineages[i].NicheVector, lineages[j].NicheVector);
            pairs++;
        }

        return pairs == 0 ? 0.0 : total / pairs;
    }

    internal static double NicheDistance(IReadOnlyDictionary<string, double> a, IReadOnlyDictionary<string, double> b)
    {
        var keys = a.Keys.Concat(b.Keys).Distinct(StringComparer.Ordinal).ToArray();
        if (keys.Length == 0)
            return 0.0;

        return keys.Average(key => Math.Abs(a.GetValueOrDefault(key) - b.GetValueOrDefault(key)));
    }
}
