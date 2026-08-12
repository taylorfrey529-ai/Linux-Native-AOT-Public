using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace EasternKingdoms.Simulation;

public sealed record DeepTimeArchiveStratum(
    string StratumId,
    int StartGeneration,
    int EndGeneration,
    IReadOnlyList<DeepTimeLineageState> Lineages,
    IReadOnlyList<AdaptiveRadiationEvent> RadiationEvents,
    IReadOnlyList<LineageExtinctionEvent> ExtinctionEvents,
    IReadOnlyList<EcologicalReplacementEvent> ReplacementEvents,
    IReadOnlyList<CladeRecord> Clades,
    string PreviousStratumHash,
    string IntegrityHash);

public sealed record DeepTimeArchiveSnapshot(
    IReadOnlyList<DeepTimeArchiveStratum> Strata,
    string HeadHash);

public sealed class DeepTimeArchive
{
    private readonly List<DeepTimeArchiveStratum> _strata = new();

    public IReadOnlyList<DeepTimeArchiveStratum> Strata => _strata.ToArray();

    public DeepTimeArchiveStratum Commit(
        int startGeneration,
        int endGeneration,
        IEnumerable<DeepTimeLineageState> lineages,
        IEnumerable<AdaptiveRadiationEvent> radiations,
        IEnumerable<LineageExtinctionEvent> extinctions,
        IEnumerable<EcologicalReplacementEvent> replacements,
        IEnumerable<CladeRecord> clades)
    {
        if (startGeneration < 0 || endGeneration < startGeneration)
            throw new ArgumentOutOfRangeException(nameof(startGeneration));
        if (_strata.Count > 0 && startGeneration <= _strata[^1].EndGeneration)
            throw new InvalidOperationException("Deep-time strata must be append-only and non-overlapping.");

        var lineageArray = lineages.Select(CloneLineage).OrderBy(x => x.LineageId, StringComparer.Ordinal).ToArray();
        var radiationArray = radiations.Select(x => x with
            {
                DescendantLineageIds = x.DescendantLineageIds.OrderBy(v => v, StringComparer.Ordinal).ToArray()
            }).OrderBy(x => x.EventId, StringComparer.Ordinal).ToArray();
        var extinctionArray = extinctions.Select(x => x with
            {
                FormerZones = x.FormerZones.ToHashSet(StringComparer.Ordinal)
            }).OrderBy(x => x.EventId, StringComparer.Ordinal).ToArray();
        var replacementArray = replacements.Select(x => x with
            {
                SharedNiches = x.SharedNiches.OrderBy(v => v, StringComparer.Ordinal).ToArray()
            }).OrderBy(x => x.EventId, StringComparer.Ordinal).ToArray();
        var cladeArray = clades.Select(x => x with
            {
                MemberLineageIds = x.MemberLineageIds.OrderBy(v => v, StringComparer.Ordinal).ToArray()
            }).OrderBy(x => x.CladeId, StringComparer.Ordinal).ToArray();
        string previous = _strata.Count == 0 ? string.Empty : _strata[^1].IntegrityHash;
        string stratumId = $"stratum:{startGeneration:D8}-{endGeneration:D8}";
        string hash = ComputeHash(stratumId, startGeneration, endGeneration, previous, lineageArray, radiationArray, extinctionArray, replacementArray, cladeArray);

        var stratum = new DeepTimeArchiveStratum(
            stratumId,
            startGeneration,
            endGeneration,
            lineageArray,
            radiationArray,
            extinctionArray,
            replacementArray,
            cladeArray,
            previous,
            hash);

        _strata.Add(stratum);
        return stratum;
    }

    public bool Verify()
    {
        string previous = string.Empty;
        foreach (var stratum in _strata)
        {
            if (!StringComparer.Ordinal.Equals(previous, stratum.PreviousStratumHash))
                return false;

            string expected = ComputeHash(
                stratum.StratumId,
                stratum.StartGeneration,
                stratum.EndGeneration,
                stratum.PreviousStratumHash,
                stratum.Lineages,
                stratum.RadiationEvents,
                stratum.ExtinctionEvents,
                stratum.ReplacementEvents,
                stratum.Clades);

            if (!StringComparer.Ordinal.Equals(expected, stratum.IntegrityHash))
                return false;
            previous = stratum.IntegrityHash;
        }

        return true;
    }

    public DeepTimeArchiveSnapshot Snapshot() =>
        new(_strata.ToArray(), _strata.Count == 0 ? string.Empty : _strata[^1].IntegrityHash);

    private static DeepTimeLineageState CloneLineage(DeepTimeLineageState x) =>
        x with
        {
            OccupiedZones = x.OccupiedZones.ToHashSet(StringComparer.Ordinal),
            NicheVector = x.NicheVector.OrderBy(v => v.Key, StringComparer.Ordinal)
                .ToDictionary(v => v.Key, v => v.Value, StringComparer.Ordinal)
        };

    private static string ComputeHash(
        string stratumId,
        int startGeneration,
        int endGeneration,
        string previous,
        IReadOnlyList<DeepTimeLineageState> lineages,
        IReadOnlyList<AdaptiveRadiationEvent> radiations,
        IReadOnlyList<LineageExtinctionEvent> extinctions,
        IReadOnlyList<EcologicalReplacementEvent> replacements,
        IReadOnlyList<CladeRecord> clades)
    {
        var b = new StringBuilder();
        b.Append(stratumId).Append('|').Append(startGeneration).Append('|').Append(endGeneration).Append('|').Append(previous).AppendLine();
        foreach (var x in lineages)
        {
            b.Append("L|").Append(x.LineageId).Append('|').Append(x.ParentLineageId).Append('|').Append(x.SpeciesFamilyId).Append('|')
                .Append(x.FirstObservedGeneration).Append('|').Append(x.LastObservedGeneration).Append('|').Append(x.Population).Append('|')
                .Append(x.Authority).Append('|').Append(x.Status).Append('|').Append(x.ConsecutiveZeroPopulationGenerations).Append('|')
                .Append(x.MeanDivergence.ToString("R", CultureInfo.InvariantCulture)).AppendLine();
            foreach (var zone in x.OccupiedZones.OrderBy(v => v, StringComparer.Ordinal))
                b.Append("Z|").Append(zone).AppendLine();
            foreach (var niche in x.NicheVector.OrderBy(v => v.Key, StringComparer.Ordinal))
                b.Append("N|").Append(niche.Key).Append('|').Append(niche.Value.ToString("R", CultureInfo.InvariantCulture)).AppendLine();
        }
        foreach (var x in radiations)
        {
            b.Append("R|").Append(x.EventId).Append('|').Append(x.RootLineageId).Append('|').Append(x.FirstBranchGeneration).Append('|')
                .Append(x.DetectionGeneration).Append('|').Append(x.MeanNicheDivergence.ToString("R", CultureInfo.InvariantCulture)).Append('|').Append(x.ReviewOnly).AppendLine();
            foreach (var child in x.DescendantLineageIds.OrderBy(v => v, StringComparer.Ordinal))
                b.Append("RC|").Append(child).AppendLine();
        }
        foreach (var x in extinctions)
        {
            b.Append("E|").Append(x.EventId).Append('|').Append(x.LineageId).Append('|').Append(x.Generation).Append('|').Append(x.PersistenceGenerations).Append('|').Append(x.Authority).AppendLine();
            foreach (var zone in x.FormerZones.OrderBy(v => v, StringComparer.Ordinal))
                b.Append("EZ|").Append(zone).AppendLine();
        }
        foreach (var x in replacements)
        {
            b.Append("X|").Append(x.EventId).Append('|').Append(x.ExtinctLineageId).Append('|').Append(x.ReplacementLineageId).Append('|').Append(x.Generation).Append('|')
                .Append(x.NicheSimilarity.ToString("R", CultureInfo.InvariantCulture)).Append('|').Append(x.ReviewOnly).AppendLine();
            foreach (var niche in x.SharedNiches.OrderBy(v => v, StringComparer.Ordinal))
                b.Append("XN|").Append(niche).AppendLine();
        }
        foreach (var x in clades)
        {
            b.Append("C|").Append(x.CladeId).Append('|').Append(x.RootLineageId).Append('|').Append(x.FirstGeneration).Append('|').Append(x.LastGeneration).Append('|').Append(x.LivingBranches).Append('|').Append(x.ExtinctBranches).Append('|').Append(x.MaximumAuthority).AppendLine();
            foreach (var member in x.MemberLineageIds.OrderBy(v => v, StringComparer.Ordinal))
                b.Append("CM|").Append(member).AppendLine();
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(b.ToString()))).ToLowerInvariant();
    }
}
