namespace EasternKingdoms.Simulation;

public sealed class HistoricalAtlasComparisonBuilder
{
    private readonly HistoricalAtlasSceneBuilder _scenes;
    private readonly HistoricalOverlayBuilder _overlays;

    public HistoricalAtlasComparisonBuilder(
        HistoricalAtlasSceneBuilder scenes,
        HistoricalOverlayBuilder? overlays = null)
    {
        _scenes = scenes ?? throw new ArgumentNullException(nameof(scenes));
        _overlays = overlays ?? new HistoricalOverlayBuilder();
    }

    public HistoricalAtlasComparisonPayload Build(
        HistoricalPlaybackFrame from,
        HistoricalPlaybackFrame to,
        HistoricalAtlasComparisonMode mode,
        HistoricalAtlasCameraState camera,
        IEnumerable<HistoricalAtlasLayerState> layers,
        IEnumerable<HistoricalOverlayMetric>? metrics = null)
    {
        if (mode == HistoricalAtlasComparisonMode.Single)
            throw new InvalidOperationException("Single mode does not produce a comparison payload.");
        if (to.World.Generation < from.World.Generation)
            throw new InvalidOperationException("Historical atlas comparisons must move forward in generation order.");

        var left = _scenes.Build(from, camera, layers);
        var right = _scenes.Build(to, camera, layers);
        HistoricalChangeOverlay[] overlays = mode == HistoricalAtlasComparisonMode.Delta
            ? BuildOverlays(from.World, to.World, metrics)
            : Array.Empty<HistoricalChangeOverlay>();

        return new HistoricalAtlasComparisonPayload(
            mode,
            left,
            right,
            Array.AsReadOnly(overlays),
            HistoricalAtlasAuthority.TestHistoryOnly,
            HistoricalAtlasHasher.HashComparison(mode, left, right, overlays));
    }

    public bool Verify(HistoricalAtlasComparisonPayload comparison)
    {
        ArgumentNullException.ThrowIfNull(comparison);
        if (comparison.Authority != HistoricalAtlasAuthority.TestHistoryOnly) return false;
        if (comparison.Mode == HistoricalAtlasComparisonMode.Single) return false;
        if (!_scenes.Verify(comparison.From) || !_scenes.Verify(comparison.To)) return false;
        if (comparison.To.Generation < comparison.From.Generation) return false;
        if (comparison.Mode == HistoricalAtlasComparisonMode.SideBySide && comparison.Overlays.Count != 0) return false;
        if (comparison.Mode == HistoricalAtlasComparisonMode.Delta && comparison.Overlays.Any(x => !_overlays.Verify(x))) return false;
        if (comparison.Overlays.Any(x => x.FromGeneration != comparison.From.Generation || x.ToGeneration != comparison.To.Generation)) return false;
        if (comparison.Overlays.Select(x => x.Metric).Distinct().Count() != comparison.Overlays.Count) return false;
        return StringComparer.Ordinal.Equals(
            comparison.IntegrityHash,
            HistoricalAtlasHasher.HashComparison(comparison.Mode, comparison.From, comparison.To, comparison.Overlays));
    }

    private HistoricalChangeOverlay[] BuildOverlays(
        HistoricalWorldStateSnapshot from,
        HistoricalWorldStateSnapshot to,
        IEnumerable<HistoricalOverlayMetric>? metrics)
    {
        var requested = (metrics ?? DefaultDeltaMetrics())
            .Distinct()
            .OrderBy(x => x)
            .ToArray();
        if (requested.Length == 0)
            throw new InvalidOperationException("Delta comparison requires at least one overlay metric.");
        return requested.Select(metric => _overlays.Build(from, to, metric)).ToArray();
    }

    public static IReadOnlyList<HistoricalOverlayMetric> DefaultDeltaMetrics() => Array.AsReadOnly(new[]
    {
        HistoricalOverlayMetric.Temperature,
        HistoricalOverlayMetric.Moisture,
        HistoricalOverlayMetric.ForestCover,
        HistoricalOverlayMetric.WetlandExtent,
        HistoricalOverlayMetric.RiverConnectivity,
        HistoricalOverlayMetric.TerrainIntegrity,
        HistoricalOverlayMetric.LineageRichness
    });
}
