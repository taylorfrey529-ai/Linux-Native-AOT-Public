namespace EasternKingdoms.Simulation;

public sealed class RegionalBiodiversityAnalyzer
{
    public RegionalBiodiversitySnapshot Analyze(
        string zoneId,
        int generation,
        IEnumerable<DeepTimeLineageObservation> lineages)
    {
        if (string.IsNullOrWhiteSpace(zoneId))
            throw new ArgumentException("Zone ID is required.", nameof(zoneId));

        var present = lineages
            .Where(x => x.Population > 0 && x.OccupiedZones.Contains(zoneId))
            .OrderBy(x => x.LineageId, StringComparer.Ordinal)
            .ToArray();

        var lineageIds = present.Select(x => x.LineageId).ToHashSet(StringComparer.Ordinal);
        int familyRichness = present.Select(x => x.SpeciesFamilyId).Distinct(StringComparer.Ordinal).Count();
        var weightedPopulations = present
            .Select(x => x.Population / Math.Max(1, x.OccupiedZones.Count))
            .ToArray();
        long approximatePopulation = weightedPopulations.Sum();

        double shannon = 0.0;
        if (approximatePopulation > 0)
        {
            foreach (long population in weightedPopulations.Where(x => x > 0))
            {
                double p = population / (double)approximatePopulation;
                shannon -= p * Math.Log(p);
            }
        }

        return new RegionalBiodiversitySnapshot(
            zoneId,
            generation,
            lineageIds.Count,
            familyRichness,
            shannon,
            approximatePopulation,
            lineageIds);
    }
}

public sealed class CommunityTurnoverAnalyzer
{
    public CommunityTurnoverMetrics Compare(
        RegionalBiodiversitySnapshot previous,
        RegionalBiodiversitySnapshot current)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);
        if (!StringComparer.Ordinal.Equals(previous.ZoneId, current.ZoneId))
            throw new InvalidOperationException("Community turnover requires snapshots from the same zone.");
        if (current.Generation <= previous.Generation)
            throw new InvalidOperationException("Community turnover requires a later current generation.");

        var gained = current.LineageIds.Except(previous.LineageIds, StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
        var lost = previous.LineageIds.Except(current.LineageIds, StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
        int retained = previous.LineageIds.Intersect(current.LineageIds, StringComparer.Ordinal).Count();
        int union = previous.LineageIds.Union(current.LineageIds, StringComparer.Ordinal).Count();
        double jaccard = union == 0 ? 0.0 : 1.0 - retained / (double)union;

        return new CommunityTurnoverMetrics(
            current.ZoneId,
            previous.Generation,
            current.Generation,
            gained.Count,
            lost.Count,
            retained,
            Math.Clamp(jaccard, 0.0, 1.0),
            gained,
            lost);
    }
}
