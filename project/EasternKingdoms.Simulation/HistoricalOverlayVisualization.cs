namespace EasternKingdoms.Simulation;

public sealed class HistoricalOverlayBuilder
{
    private readonly HistoricalVisualizationSettings _settings;

    public HistoricalOverlayBuilder(HistoricalVisualizationSettings? settings = null)
    {
        _settings = settings ?? new HistoricalVisualizationSettings();
        _settings.Validate();
    }

    public HistoricalChangeOverlay Build(
        HistoricalWorldStateSnapshot from,
        HistoricalWorldStateSnapshot to,
        HistoricalOverlayMetric metric)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        if (to.Generation < from.Generation)
            throw new InvalidOperationException("Historical overlays must move forward in generation order.");
        if (!HistoricalPlaybackVerifier.Verify(from) || !HistoricalPlaybackVerifier.Verify(to))
            throw new InvalidOperationException("Historical overlays require verified source snapshots.");

        var left = from.Cells.ToDictionary(x => x.ZoneId, StringComparer.Ordinal);
        var right = to.Cells.ToDictionary(x => x.ZoneId, StringComparer.Ordinal);
        var zoneIds = left.Keys.Concat(right.Keys).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal);
        var cells = new List<HistoricalOverlayCell>();

        foreach (string zoneId in zoneIds)
        {
            left.TryGetValue(zoneId, out var a);
            right.TryGetValue(zoneId, out var b);
            double? before = Read(a, metric);
            double? after = Read(b, metric);
            double? delta = before.HasValue && after.HasValue ? after.Value - before.Value : null;
            var direction = !delta.HasValue
                ? HistoricalOverlayDirection.Missing
                : Math.Abs(delta.Value) <= _settings.StableDeltaEpsilon
                    ? HistoricalOverlayDirection.Stable
                    : delta.Value > 0.0
                        ? HistoricalOverlayDirection.Increase
                        : HistoricalOverlayDirection.Decrease;
            cells.Add(new HistoricalOverlayCell(zoneId, before, after, delta, direction));
        }

        var array = cells.ToArray();
        return new HistoricalChangeOverlay(
            metric,
            from.Generation,
            to.Generation,
            Array.AsReadOnly(array),
            HistoricalVisualizationAuthority.TestHistoryOnly,
            HistoricalVisualizationHasher.HashOverlay(metric, from.Generation, to.Generation, array));
    }

    public bool Verify(HistoricalChangeOverlay overlay)
    {
        ArgumentNullException.ThrowIfNull(overlay);
        if (overlay.Authority != HistoricalVisualizationAuthority.TestHistoryOnly) return false;
        if (overlay.ToGeneration < overlay.FromGeneration) return false;
        foreach (var x in overlay.Cells)
        {
            double? semanticDelta = x.FromValue.HasValue && x.ToValue.HasValue
                ? x.ToValue.Value - x.FromValue.Value
                : null;
            if (semanticDelta.HasValue != x.Delta.HasValue) return false;
            if (semanticDelta.HasValue && Math.Abs(semanticDelta.Value - x.Delta!.Value) > _settings.StableDeltaEpsilon) return false;
            var expected = !semanticDelta.HasValue
                ? HistoricalOverlayDirection.Missing
                : Math.Abs(semanticDelta.Value) <= _settings.StableDeltaEpsilon
                    ? HistoricalOverlayDirection.Stable
                    : semanticDelta.Value > 0.0
                        ? HistoricalOverlayDirection.Increase
                        : HistoricalOverlayDirection.Decrease;
            if (x.Direction != expected) return false;
        }
        return StringComparer.Ordinal.Equals(
            overlay.IntegrityHash,
            HistoricalVisualizationHasher.HashOverlay(overlay.Metric, overlay.FromGeneration, overlay.ToGeneration, overlay.Cells));
    }

    private static double? Read(PaleogeographicCellState? cell, HistoricalOverlayMetric metric) => metric switch
    {
        HistoricalOverlayMetric.Temperature => cell?.Climate?.Temperature,
        HistoricalOverlayMetric.Moisture => cell?.Climate?.Moisture,
        HistoricalOverlayMetric.ForestCover => cell?.Landscape?.ForestCover,
        HistoricalOverlayMetric.WetlandExtent => cell?.Landscape?.WetlandExtent,
        HistoricalOverlayMetric.RiverConnectivity => cell?.Landscape?.RiverConnectivity,
        HistoricalOverlayMetric.CoastalExposure => cell?.Landscape?.CoastalExposure,
        HistoricalOverlayMetric.TerrainIntegrity => cell?.Landscape?.TerrainIntegrity,
        HistoricalOverlayMetric.LineageRichness => cell?.Biodiversity?.LineageRichness,
        HistoricalOverlayMetric.SpeciesFamilyRichness => cell?.Biodiversity?.SpeciesFamilyRichness,
        HistoricalOverlayMetric.ApproximatePopulation => cell?.Biodiversity?.ApproximatePopulation,
        _ => throw new ArgumentOutOfRangeException(nameof(metric))
    };
}
