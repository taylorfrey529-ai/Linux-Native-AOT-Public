namespace EasternKingdoms.Simulation;

public sealed class EasternKingdomsGlobeStateAdapter
{
    private readonly IReadOnlySet<string> _authorizedZones;

    public EasternKingdomsGlobeStateAdapter(IEnumerable<string> authorizedZoneIds)
    {
        ArgumentNullException.ThrowIfNull(authorizedZoneIds);
        _authorizedZones = authorizedZoneIds.ToHashSet(StringComparer.Ordinal);
        if (_authorizedZones.Count == 0)
            throw new InvalidOperationException("At least one authorized Eastern Kingdoms zone is required.");
    }

    public HistoricalGlobeFrame Adapt(HistoricalWorldStateSnapshot world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (!HistoricalPlaybackVerifier.Verify(world))
            throw new InvalidOperationException("Globe visualization requires a verified historical snapshot.");
        if (world.Cells.Any(x => !_authorizedZones.Contains(x.ZoneId)))
            throw new InvalidOperationException("Globe visualization contains a zone outside the authorized Eastern Kingdoms set.");

        var cells = world.Cells.OrderBy(x => x.ZoneId, StringComparer.Ordinal).Select(x => new HistoricalGlobeCellState(
            x.ZoneId,
            RenderAnchorKey: x.ZoneId,
            world.Generation,
            x.Climate?.Regime,
            x.Climate?.Temperature,
            x.Climate?.Moisture,
            x.Landscape?.ForestCover,
            x.Landscape?.WetlandExtent,
            x.Landscape?.RiverConnectivity,
            x.Landscape?.CoastalExposure,
            x.Landscape?.TerrainIntegrity,
            x.Biodiversity?.LineageRichness,
            x.Biodiversity?.SpeciesFamilyRichness,
            x.Completeness,
            x.IntegrityHash)).ToArray();

        return new HistoricalGlobeFrame(
            world.Generation,
            GlobeLayoutPolicy.AuthoredAnchorsRequired,
            Array.AsReadOnly(cells),
            HistoricalVisualizationAuthority.TestHistoryOnly,
            HistoricalVisualizationHasher.HashGlobe(world.Generation, GlobeLayoutPolicy.AuthoredAnchorsRequired, cells));
    }

    public bool Verify(HistoricalGlobeFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (frame.Authority != HistoricalVisualizationAuthority.TestHistoryOnly) return false;
        if (frame.LayoutPolicy != GlobeLayoutPolicy.AuthoredAnchorsRequired) return false;
        if (frame.Cells.Any(x => !_authorizedZones.Contains(x.ZoneId))) return false;
        if (frame.Cells.Any(x => x.Generation != frame.Generation)) return false;
        if (frame.Cells.Select(x => x.ZoneId).Distinct(StringComparer.Ordinal).Count() != frame.Cells.Count) return false;
        if (frame.Cells.Any(x => !StringComparer.Ordinal.Equals(x.RenderAnchorKey, x.ZoneId))) return false;
        if (frame.Cells.Any(x => string.IsNullOrWhiteSpace(x.SourceIntegrityHash))) return false;
        return StringComparer.Ordinal.Equals(
            frame.IntegrityHash,
            HistoricalVisualizationHasher.HashGlobe(frame.Generation, frame.LayoutPolicy, frame.Cells));
    }
}
