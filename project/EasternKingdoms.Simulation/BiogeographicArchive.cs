using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace EasternKingdoms.Simulation;

public sealed record BiogeographicArchiveStratum(
    string StratumId,
    int StartGeneration,
    int EndGeneration,
    IReadOnlyList<RangeShiftEvent> RangeEvents,
    IReadOnlyList<RefugiumRecord> Refugia,
    IReadOnlyList<RecolonizationWaveEvent> RecolonizationWaves,
    IReadOnlyList<ExtinctionDebtState> ExtinctionDebt,
    IReadOnlyList<RegionalBiodiversitySnapshot> Biodiversity,
    IReadOnlyList<CommunityTurnoverMetrics> Turnover,
    string PreviousStratumHash,
    string IntegrityHash);

public sealed record BiogeographicArchiveSnapshot(
    IReadOnlyList<BiogeographicArchiveStratum> Strata,
    string HeadHash);

public sealed class BiogeographicArchive
{
    private readonly List<BiogeographicArchiveStratum> _strata = new();

    public IReadOnlyList<BiogeographicArchiveStratum> Strata => _strata.ToArray();

    public BiogeographicArchiveStratum Commit(
        int startGeneration,
        int endGeneration,
        IEnumerable<RangeShiftEvent> rangeEvents,
        IEnumerable<RefugiumRecord> refugia,
        IEnumerable<RecolonizationWaveEvent> recolonizations,
        IEnumerable<ExtinctionDebtState> extinctionDebt,
        IEnumerable<RegionalBiodiversitySnapshot> biodiversity,
        IEnumerable<CommunityTurnoverMetrics> turnover)
    {
        if (startGeneration < 0 || endGeneration < startGeneration)
            throw new ArgumentOutOfRangeException(nameof(startGeneration));
        if (_strata.Count > 0 && startGeneration <= _strata[^1].EndGeneration)
            throw new InvalidOperationException("Biogeographic strata must be append-only and non-overlapping.");

        var rangeArray = rangeEvents.Select(CloneRangeEvent)
            .OrderBy(x => x.Generation)
            .ThenBy(x => x.EventId, StringComparer.Ordinal)
            .ToArray();
        var refugiumArray = refugia.Select(x => x with { })
            .OrderBy(x => x.RefugiumId, StringComparer.Ordinal)
            .ToArray();
        var recolonizationArray = recolonizations.Select(x => x with { })
            .OrderBy(x => x.Generation)
            .ThenBy(x => x.EventId, StringComparer.Ordinal)
            .ToArray();
        var debtArray = extinctionDebt.Select(x => x with { })
            .OrderBy(x => x.LineageId, StringComparer.Ordinal)
            .ThenBy(x => x.ZoneId, StringComparer.Ordinal)
            .ToArray();
        var biodiversityArray = biodiversity.Select(CloneBiodiversity)
            .OrderBy(x => x.Generation)
            .ThenBy(x => x.ZoneId, StringComparer.Ordinal)
            .ToArray();
        var turnoverArray = turnover.Select(CloneTurnover)
            .OrderBy(x => x.ToGeneration)
            .ThenBy(x => x.ZoneId, StringComparer.Ordinal)
            .ToArray();

        string previous = _strata.Count == 0 ? string.Empty : _strata[^1].IntegrityHash;
        string stratumId = $"biostrata:{startGeneration:D8}-{endGeneration:D8}";
        string hash = ComputeHash(
            stratumId,
            startGeneration,
            endGeneration,
            rangeArray,
            refugiumArray,
            recolonizationArray,
            debtArray,
            biodiversityArray,
            turnoverArray,
            previous);

        var stratum = new BiogeographicArchiveStratum(
            stratumId,
            startGeneration,
            endGeneration,
            rangeArray,
            refugiumArray,
            recolonizationArray,
            debtArray,
            biodiversityArray,
            turnoverArray,
            previous,
            hash);
        _strata.Add(stratum);
        return stratum;
    }

    public bool Verify()
    {
        string previous = string.Empty;
        for (int i = 0; i < _strata.Count; i++)
        {
            var stratum = _strata[i];
            if (!StringComparer.Ordinal.Equals(previous, stratum.PreviousStratumHash))
                return false;
            if (i > 0 && stratum.StartGeneration <= _strata[i - 1].EndGeneration)
                return false;

            string expected = ComputeHash(
                stratum.StratumId,
                stratum.StartGeneration,
                stratum.EndGeneration,
                stratum.RangeEvents,
                stratum.Refugia,
                stratum.RecolonizationWaves,
                stratum.ExtinctionDebt,
                stratum.Biodiversity,
                stratum.Turnover,
                stratum.PreviousStratumHash);
            if (!StringComparer.Ordinal.Equals(expected, stratum.IntegrityHash))
                return false;
            previous = stratum.IntegrityHash;
        }
        return true;
    }

    public BiogeographicArchiveSnapshot Snapshot() =>
        new(_strata.ToArray(), _strata.Count == 0 ? string.Empty : _strata[^1].IntegrityHash);

    private static RangeShiftEvent CloneRangeEvent(RangeShiftEvent value) =>
        value with
        {
            AddedZones = value.AddedZones.ToHashSet(StringComparer.Ordinal),
            RemovedZones = value.RemovedZones.ToHashSet(StringComparer.Ordinal)
        };

    private static RegionalBiodiversitySnapshot CloneBiodiversity(RegionalBiodiversitySnapshot value) =>
        value with { LineageIds = value.LineageIds.ToHashSet(StringComparer.Ordinal) };

    private static CommunityTurnoverMetrics CloneTurnover(CommunityTurnoverMetrics value) =>
        value with
        {
            GainedLineages = value.GainedLineages.ToHashSet(StringComparer.Ordinal),
            LostLineages = value.LostLineages.ToHashSet(StringComparer.Ordinal)
        };

    private static string ComputeHash(
        string stratumId,
        int start,
        int end,
        IEnumerable<RangeShiftEvent> ranges,
        IEnumerable<RefugiumRecord> refugia,
        IEnumerable<RecolonizationWaveEvent> recolonizations,
        IEnumerable<ExtinctionDebtState> debt,
        IEnumerable<RegionalBiodiversitySnapshot> biodiversity,
        IEnumerable<CommunityTurnoverMetrics> turnover,
        string previous)
    {
        var builder = new StringBuilder();
        builder.Append(stratumId).Append('|').Append(start).Append('|').Append(end).Append('|').Append(previous).AppendLine();

        foreach (var item in ranges.OrderBy(x => x.Generation).ThenBy(x => x.EventId, StringComparer.Ordinal))
        {
            builder.Append("R|").Append(item.EventId).Append('|').Append(item.LineageId).Append('|').Append(item.Kind)
                .Append('|').Append(item.Generation).Append('|').Append(item.PreviousComponents).Append('|').Append(item.CurrentComponents)
                .Append('|').Append(item.Authority).Append('|')
                .Append(string.Join(',', item.AddedZones.OrderBy(x => x, StringComparer.Ordinal))).Append('|')
                .Append(string.Join(',', item.RemovedZones.OrderBy(x => x, StringComparer.Ordinal))).AppendLine();
        }
        foreach (var item in refugia.OrderBy(x => x.RefugiumId, StringComparer.Ordinal))
        {
            builder.Append("F|").Append(item.RefugiumId).Append('|').Append(item.LineageId).Append('|').Append(item.ZoneId)
                .Append('|').Append(item.FirstObservedGeneration).Append('|').Append(item.LastObservedGeneration)
                .Append('|').Append(item.ConsecutiveGenerations).Append('|')
                .Append(item.MeanSuitability.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                .Append(item.Persistent).Append('|').Append(item.Authority).AppendLine();
        }
        foreach (var item in recolonizations.OrderBy(x => x.Generation).ThenBy(x => x.EventId, StringComparer.Ordinal))
        {
            builder.Append("C|").Append(item.EventId).Append('|').Append(item.LineageId).Append('|').Append(item.SourceZoneId)
                .Append('|').Append(item.DestinationZoneId).Append('|').Append(item.RouteId).Append('|').Append(item.Generation)
                .Append('|').Append(item.EstimatedMigrants).Append('|').Append(item.SourceWasRefugium).Append('|').Append(item.Authority).AppendLine();
        }
        foreach (var item in debt.OrderBy(x => x.LineageId, StringComparer.Ordinal).ThenBy(x => x.ZoneId, StringComparer.Ordinal))
        {
            builder.Append("D|").Append(item.LineageId).Append('|').Append(item.ZoneId).Append('|').Append(item.FirstDebtGeneration)
                .Append('|').Append(item.LastGeneration).Append('|').Append(item.ConsecutiveDebtGenerations).Append('|')
                .Append(item.DebtScore.ToString("R", CultureInfo.InvariantCulture)).Append('|').Append(item.Realized).Append('|')
                .Append(item.RealizedGeneration?.ToString(CultureInfo.InvariantCulture) ?? string.Empty).AppendLine();
        }
        foreach (var item in biodiversity.OrderBy(x => x.Generation).ThenBy(x => x.ZoneId, StringComparer.Ordinal))
        {
            builder.Append("B|").Append(item.ZoneId).Append('|').Append(item.Generation).Append('|').Append(item.LineageRichness)
                .Append('|').Append(item.SpeciesFamilyRichness).Append('|')
                .Append(item.ShannonDiversity.ToString("R", CultureInfo.InvariantCulture)).Append('|').Append(item.ApproximatePopulation).Append('|')
                .Append(string.Join(',', item.LineageIds.OrderBy(x => x, StringComparer.Ordinal))).AppendLine();
        }
        foreach (var item in turnover.OrderBy(x => x.ToGeneration).ThenBy(x => x.ZoneId, StringComparer.Ordinal))
        {
            builder.Append("T|").Append(item.ZoneId).Append('|').Append(item.FromGeneration).Append('|').Append(item.ToGeneration)
                .Append('|').Append(item.Gains).Append('|').Append(item.Losses).Append('|').Append(item.Retained).Append('|')
                .Append(item.JaccardDissimilarity.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                .Append(string.Join(',', item.GainedLineages.OrderBy(x => x, StringComparer.Ordinal))).Append('|')
                .Append(string.Join(',', item.LostLineages.OrderBy(x => x, StringComparer.Ordinal))).AppendLine();
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()))).ToLowerInvariant();
    }
}
