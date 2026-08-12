namespace EasternKingdoms.Simulation;

public sealed class DeepTimeWorldStatePlayback
{
    private readonly HistoricalWorldStateReconstructor _reconstructor;
    private readonly Dictionary<string, HistoricalWorldStateSnapshot> _history = new(StringComparer.Ordinal);

    public DeepTimeWorldStatePlayback(HistoricalWorldStateReconstructor reconstructor)
    {
        _reconstructor = reconstructor ?? throw new ArgumentNullException(nameof(reconstructor));
    }

    public HistoricalWorldStateSnapshot Reconstruct(HistoricalReconstructionRequest request)
    {
        var snapshot = _reconstructor.Reconstruct(request);
        _history.TryAdd(snapshot.IntegrityHash, snapshot);
        return snapshot;
    }

    public IReadOnlyList<HistoricalPlaybackFrame> Play(
        int startGeneration,
        int endGeneration,
        int step,
        IReadOnlySet<string> zoneIds,
        bool requireCompleteEvidence = false)
    {
        if (startGeneration < 0) throw new ArgumentOutOfRangeException(nameof(startGeneration));
        if (endGeneration < startGeneration) throw new ArgumentOutOfRangeException(nameof(endGeneration));
        if (step < 1) throw new ArgumentOutOfRangeException(nameof(step));
        ArgumentNullException.ThrowIfNull(zoneIds);

        var frames = new List<HistoricalPlaybackFrame>();
        int index = 0;
        for (int generation = startGeneration; generation <= endGeneration; generation += step)
        {
            var world = Reconstruct(new HistoricalReconstructionRequest(generation, zoneIds, requireCompleteEvidence));
            frames.Add(new HistoricalPlaybackFrame(index++, world));
            if (generation > int.MaxValue - step) break;
        }
        return Array.AsReadOnly(frames.ToArray());
    }

    public HistoricalWorldStateDelta Compare(HistoricalWorldStateSnapshot from, HistoricalWorldStateSnapshot to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        if (to.Generation < from.Generation)
            throw new InvalidOperationException("Historical comparison must move forward in generation order.");
        if (!HistoricalPlaybackVerifier.Verify(from) || !HistoricalPlaybackVerifier.Verify(to))
            throw new InvalidOperationException("Historical comparison requires internally valid snapshots.");

        var fromCells = from.Cells.ToDictionary(x => x.ZoneId, StringComparer.Ordinal);
        var toCells = to.Cells.ToDictionary(x => x.ZoneId, StringComparer.Ordinal);
        var zoneIds = fromCells.Keys.Concat(toCells.Keys).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal);
        var deltas = new List<HistoricalCellDelta>();

        foreach (string zoneId in zoneIds)
        {
            fromCells.TryGetValue(zoneId, out var a);
            toCells.TryGetValue(zoneId, out var b);
            var oldLineages = a?.Lineages.Select(x => x.LineageId).ToHashSet(StringComparer.Ordinal) ?? new HashSet<string>(StringComparer.Ordinal);
            var newLineages = b?.Lineages.Select(x => x.LineageId).ToHashSet(StringComparer.Ordinal) ?? new HashSet<string>(StringComparer.Ordinal);

            deltas.Add(new HistoricalCellDelta(
                zoneId,
                from.Generation,
                to.Generation,
                Difference(a?.Climate?.Temperature, b?.Climate?.Temperature),
                Difference(a?.Climate?.Moisture, b?.Climate?.Moisture),
                Difference(a?.Landscape?.ForestCover, b?.Landscape?.ForestCover),
                Difference(a?.Landscape?.WetlandExtent, b?.Landscape?.WetlandExtent),
                Difference(a?.Landscape?.RiverConnectivity, b?.Landscape?.RiverConnectivity),
                Difference(a?.Landscape?.CoastalExposure, b?.Landscape?.CoastalExposure),
                Difference(a?.Landscape?.TerrainIntegrity, b?.Landscape?.TerrainIntegrity),
                Array.AsReadOnly(newLineages.Except(oldLineages, StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray()),
                Array.AsReadOnly(oldLineages.Except(newLineages, StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray())));
        }

        var array = deltas.ToArray();
        return new HistoricalWorldStateDelta(
            from.Generation,
            to.Generation,
            Array.AsReadOnly(array),
            HistoricalPlaybackHasher.HashDelta(from.Generation, to.Generation, array));
    }

    public HistoricalPlaybackRuntimeSnapshot Snapshot()
    {
        var worlds = _history.Values.OrderBy(x => x.Generation).ThenBy(x => x.IntegrityHash, StringComparer.Ordinal).ToArray();
        return new HistoricalPlaybackRuntimeSnapshot(
            Array.AsReadOnly(worlds),
            HistoricalPlaybackHasher.HashRuntime(worlds));
    }

    private static double? Difference(double? before, double? after) =>
        before.HasValue && after.HasValue ? after.Value - before.Value : null;
}

public static class HistoricalPlaybackVerifier
{
    public static bool Verify(PaleogeographicCellState cell)
    {
        ArgumentNullException.ThrowIfNull(cell);
        var missing = cell.MissingEvidence.ToHashSet(StringComparer.Ordinal);
        bool hasDeepEvidence = cell.Evidence.Any(x => x.Kind == HistoricalEvidenceKind.DeepTimeStratum);
        if ((cell.Climate is null) != missing.Contains("climate")) return false;
        if ((cell.Landscape is null) != missing.Contains("landscape")) return false;
        if ((cell.Biodiversity is null) != missing.Contains("biodiversity")) return false;
        if ((!hasDeepEvidence) != missing.Contains("deep-time-lineage-evidence")) return false;
        int present = 4 - new[] { "climate", "landscape", "biodiversity", "deep-time-lineage-evidence" }.Count(x => missing.Contains(x));
        ReconstructionCompleteness expectedCompleteness = present switch
        {
            4 => ReconstructionCompleteness.Complete,
            3 => ReconstructionCompleteness.Substantial,
            > 0 => ReconstructionCompleteness.Partial,
            _ => ReconstructionCompleteness.None
        };
        if (cell.Completeness != expectedCompleteness) return false;
        string expected = HistoricalPlaybackHasher.HashCell(
            cell.ZoneId,
            cell.RequestedGeneration,
            cell.Climate,
            cell.Landscape,
            cell.Biodiversity,
            cell.Lineages,
            cell.Refugia,
            cell.ExtinctionDebt,
            cell.RecentLandscapeEvents,
            cell.Completeness,
            cell.MissingEvidence,
            cell.Evidence);
        return StringComparer.Ordinal.Equals(expected, cell.IntegrityHash);
    }

    public static bool Verify(HistoricalWorldStateSnapshot world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (world.Authority != HistoricalReconstructionAuthority.TestHistoryOnly)
            return false;
        if (world.Cells.Select(x => x.ZoneId).Distinct(StringComparer.Ordinal).Count() != world.Cells.Count)
            return false;
        if (world.Cells.Any(cell => cell.RequestedGeneration != world.Generation || cell.Authority != HistoricalReconstructionAuthority.TestHistoryOnly || !Verify(cell)))
            return false;
        ReconstructionCompleteness expectedCompleteness = world.Cells.Count == 0
            ? ReconstructionCompleteness.None
            : (ReconstructionCompleteness)world.Cells.Min(x => (int)x.Completeness);
        if (world.Completeness != expectedCompleteness) return false;
        var expectedMissing = world.Cells
            .SelectMany(cell => cell.MissingEvidence.Select(item => $"{cell.ZoneId}:{item}"))
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();
        if (!expectedMissing.SequenceEqual(world.MissingEvidence.OrderBy(x => x, StringComparer.Ordinal), StringComparer.Ordinal))
            return false;
        string expected = HistoricalPlaybackHasher.HashWorld(
            world.Generation,
            world.Cells,
            world.Completeness,
            world.MissingEvidence,
            world.Evidence);
        return StringComparer.Ordinal.Equals(expected, world.IntegrityHash);
    }

    public static bool Verify(HistoricalWorldStateDelta delta)
    {
        ArgumentNullException.ThrowIfNull(delta);
        string expected = HistoricalPlaybackHasher.HashDelta(delta.FromGeneration, delta.ToGeneration, delta.Cells);
        return StringComparer.Ordinal.Equals(expected, delta.IntegrityHash);
    }

    public static bool Verify(HistoricalPlaybackRuntimeSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.ReconstructedWorlds.Any(x => !Verify(x))) return false;
        return StringComparer.Ordinal.Equals(
            HistoricalPlaybackHasher.HashRuntime(snapshot.ReconstructedWorlds),
            snapshot.IntegrityHash);
    }
}
