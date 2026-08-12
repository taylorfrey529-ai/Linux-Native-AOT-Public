namespace EasternKingdoms.Simulation;

public sealed class EcologicalReplacementDetector
{
    public EcologicalReplacementEvent? Detect(
        DeepTimeLineageState extinct,
        IEnumerable<DeepTimeLineageState> candidates,
        int generation,
        double minimumNicheSimilarity = 0.65)
    {
        ArgumentNullException.ThrowIfNull(extinct);
        if (extinct.Status != DeepTimeLineageStatus.Extinct)
            return null;
        if (minimumNicheSimilarity is < 0.0 or > 1.0)
            throw new ArgumentOutOfRangeException(nameof(minimumNicheSimilarity));

        var best = candidates
            .Where(x => x.Status == DeepTimeLineageStatus.Active)
            .Where(x => !StringComparer.Ordinal.Equals(x.LineageId, extinct.LineageId))
            .Select(x => new
            {
                State = x,
                Similarity = 1.0 - AdaptiveRadiationDetector.NicheDistance(extinct.NicheVector, x.NicheVector),
                Shared = extinct.NicheVector.Keys.Intersect(x.NicheVector.Keys, StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal).ToArray()
            })
            .Where(x => x.Similarity >= minimumNicheSimilarity && x.Shared.Length > 0)
            .OrderByDescending(x => x.Similarity)
            .ThenBy(x => x.State.LineageId, StringComparer.Ordinal)
            .FirstOrDefault();

        if (best is null)
            return null;

        return new EcologicalReplacementEvent(
            $"replacement:{extinct.LineageId}:{best.State.LineageId}:g{generation}",
            extinct.LineageId,
            best.State.LineageId,
            generation,
            best.Shared,
            best.Similarity,
            ReviewOnly: best.State.Authority is DeepTimeLineageAuthority.CandidateTestHistory or DeepTimeLineageAuthority.ExperimentalSimulation);
    }
}

public sealed class CladeBuilder
{
    public IReadOnlyList<CladeRecord> Build(IEnumerable<DeepTimeLineageState> states)
    {
        var all = states.ToDictionary(x => x.LineageId, StringComparer.Ordinal);
        var roots = all.Values
            .Where(x => x.ParentLineageId is null || !all.ContainsKey(x.ParentLineageId))
            .OrderBy(x => x.LineageId, StringComparer.Ordinal)
            .ToArray();

        var result = new List<CladeRecord>();
        foreach (var root in roots)
        {
            var members = Descendants(root.LineageId, all).Prepend(root).DistinctBy(x => x.LineageId).OrderBy(x => x.LineageId, StringComparer.Ordinal).ToArray();
            result.Add(new CladeRecord(
                $"clade:{root.LineageId}",
                root.LineageId,
                members.Select(x => x.LineageId).ToArray(),
                members.Min(x => x.FirstObservedGeneration),
                members.Max(x => x.LastObservedGeneration),
                members.Count(x => x.Status != DeepTimeLineageStatus.Extinct),
                members.Count(x => x.Status == DeepTimeLineageStatus.Extinct),
                members.Select(x => x.Authority).Aggregate(DeepTimeLineageLedger.MaxAuthority)));
        }

        return result;
    }

    private static IEnumerable<DeepTimeLineageState> Descendants(
        string parentId,
        IReadOnlyDictionary<string, DeepTimeLineageState> all)
    {
        foreach (var child in all.Values.Where(x => StringComparer.Ordinal.Equals(x.ParentLineageId, parentId)).OrderBy(x => x.LineageId, StringComparer.Ordinal))
        {
            yield return child;
            foreach (var descendant in Descendants(child.LineageId, all))
                yield return descendant;
        }
    }
}
