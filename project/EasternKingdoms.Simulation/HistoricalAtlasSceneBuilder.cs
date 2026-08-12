namespace EasternKingdoms.Simulation;

public sealed class HistoricalAtlasSceneBuilder
{
    private readonly IReadOnlySet<string> _authorizedZones;
    private readonly HistoricalAtlasSettings _settings;

    public HistoricalAtlasSceneBuilder(
        IEnumerable<string> authorizedZoneIds,
        HistoricalAtlasSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(authorizedZoneIds);
        _authorizedZones = authorizedZoneIds.ToHashSet(StringComparer.Ordinal);
        if (_authorizedZones.Count == 0)
            throw new InvalidOperationException("Historical atlas requires at least one authorized Eastern Kingdoms zone.");
        _settings = settings ?? new HistoricalAtlasSettings();
        _settings.Validate();
    }

    public HistoricalAtlasScenePayload Build(
        HistoricalPlaybackFrame frame,
        HistoricalAtlasCameraState camera,
        IEnumerable<HistoricalAtlasLayerState> layers)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(layers);
        if (frame.FrameIndex < 0)
            throw new InvalidOperationException("Historical atlas frame index cannot be negative.");
        if (!HistoricalPlaybackVerifier.Verify(frame.World))
            throw new InvalidOperationException("Historical atlas scenes require verified historical snapshots.");
        if (frame.World.Cells.Any(x => !_authorizedZones.Contains(x.ZoneId)))
            throw new InvalidOperationException("Historical atlas scene contains a zone outside the authorized Eastern Kingdoms set.");
        camera.Validate(_settings, _authorizedZones);

        var normalizedLayers = NormalizeLayers(layers);
        var enabled = normalizedLayers.Where(x => x.Enabled).Select(x => x.Layer).ToHashSet();
        var cells = frame.World.Cells.OrderBy(x => x.ZoneId, StringComparer.Ordinal)
            .Select(cell => MapCell(cell, frame.World.Generation, enabled))
            .ToArray();
        var ranges = enabled.Contains(HistoricalAtlasLayer.LineageRanges)
            ? BuildRanges(frame.World.Cells)
            : Array.Empty<HistoricalAtlasLineageRange>();
        var provenance = enabled.Contains(HistoricalAtlasLayer.Provenance)
            ? frame.World.Cells.OrderBy(x => x.ZoneId, StringComparer.Ordinal).Select(MapProvenance).ToArray()
            : Array.Empty<HistoricalAtlasProvenancePanel>();

        string hash = HistoricalAtlasHasher.HashScene(
            frame.FrameIndex,
            frame.World.Generation,
            camera,
            normalizedLayers,
            cells,
            ranges,
            provenance,
            frame.World.IntegrityHash);

        return new HistoricalAtlasScenePayload(
            frame.FrameIndex,
            frame.World.Generation,
            camera,
            Array.AsReadOnly(normalizedLayers),
            Array.AsReadOnly(cells),
            Array.AsReadOnly(ranges),
            Array.AsReadOnly(provenance),
            HistoricalAtlasAuthority.TestHistoryOnly,
            frame.World.IntegrityHash,
            hash);
    }

    public bool Verify(HistoricalAtlasScenePayload scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        if (scene.Authority != HistoricalAtlasAuthority.TestHistoryOnly) return false;
        if (scene.FrameIndex < 0 || scene.Generation < 0 || string.IsNullOrWhiteSpace(scene.SourceWorldIntegrityHash)) return false;
        try { scene.Camera.Validate(_settings, _authorizedZones); }
        catch (InvalidOperationException) { return false; }
        if (scene.Cells.Any(x => !_authorizedZones.Contains(x.ZoneId) || x.Generation != scene.Generation)) return false;
        if (scene.Cells.Select(x => x.ZoneId).Distinct(StringComparer.Ordinal).Count() != scene.Cells.Count) return false;
        if (scene.Cells.Any(x => !StringComparer.Ordinal.Equals(x.RenderAnchorKey, x.ZoneId) || string.IsNullOrWhiteSpace(x.SourceIntegrityHash))) return false;
        if (scene.Cells.Any(x => x.LivingLineageIds.Distinct(StringComparer.Ordinal).Count() != x.LivingLineageIds.Count)) return false;
        if (scene.LineageRanges.Any(x => x.ZoneIds.Count == 0 || x.ZoneIds.Any(zone => !_authorizedZones.Contains(zone)))) return false;
        if (scene.LineageRanges.Select(x => x.LineageId).Distinct(StringComparer.Ordinal).Count() != scene.LineageRanges.Count) return false;
        if (scene.ProvenancePanels.Any(x => !_authorizedZones.Contains(x.ZoneId) || x.Generation != scene.Generation || string.IsNullOrWhiteSpace(x.SourceIntegrityHash))) return false;
        if (scene.ProvenancePanels.Select(x => x.ZoneId).Distinct(StringComparer.Ordinal).Count() != scene.ProvenancePanels.Count) return false;
        if (scene.Layers.Select(x => x.Layer).Distinct().Count() != scene.Layers.Count) return false;
        if (scene.Layers.Any(x => x.Opacity is < 0.0 or > 1.0 || double.IsNaN(x.Opacity) || double.IsInfinity(x.Opacity))) return false;

        var enabled = scene.Layers.Where(x => x.Enabled).Select(x => x.Layer).ToHashSet();
        if (!enabled.Contains(HistoricalAtlasLayer.Climate) && scene.Cells.Any(x => x.ClimateRegime is not null || x.Temperature.HasValue || x.Moisture.HasValue)) return false;
        if (!enabled.Contains(HistoricalAtlasLayer.Landscape) && scene.Cells.Any(x => x.ForestCover.HasValue || x.WetlandExtent.HasValue || x.RiverConnectivity.HasValue || x.CoastalExposure.HasValue || x.TerrainIntegrity.HasValue)) return false;
        if (!enabled.Contains(HistoricalAtlasLayer.Biodiversity) && scene.Cells.Any(x => x.LineageRichness.HasValue || x.SpeciesFamilyRichness.HasValue)) return false;
        if (!enabled.Contains(HistoricalAtlasLayer.Population) && scene.Cells.Any(x => x.ApproximatePopulation.HasValue)) return false;
        if (!enabled.Contains(HistoricalAtlasLayer.LineageRanges) && (scene.Cells.Any(x => x.LivingLineageIds.Count > 0) || scene.LineageRanges.Count > 0)) return false;
        if (!enabled.Contains(HistoricalAtlasLayer.Refugia) && scene.Cells.Any(x => x.Refugia.Count > 0)) return false;
        if (!enabled.Contains(HistoricalAtlasLayer.ExtinctionDebt) && scene.Cells.Any(x => x.ExtinctionDebt.Count > 0)) return false;
        if (!enabled.Contains(HistoricalAtlasLayer.Provenance) && scene.ProvenancePanels.Count > 0) return false;

        string expected = HistoricalAtlasHasher.HashScene(
            scene.FrameIndex,
            scene.Generation,
            scene.Camera,
            scene.Layers,
            scene.Cells,
            scene.LineageRanges,
            scene.ProvenancePanels,
            scene.SourceWorldIntegrityHash);
        return StringComparer.Ordinal.Equals(expected, scene.IntegrityHash);
    }

    public static IReadOnlyList<HistoricalAtlasLayerState> DefaultLayers() => Array.AsReadOnly(new[]
    {
        new HistoricalAtlasLayerState(HistoricalAtlasLayer.Climate, true, 1.0),
        new HistoricalAtlasLayerState(HistoricalAtlasLayer.Landscape, true, 0.85),
        new HistoricalAtlasLayerState(HistoricalAtlasLayer.Biodiversity, true, 0.80),
        new HistoricalAtlasLayerState(HistoricalAtlasLayer.Population, false, 0.75),
        new HistoricalAtlasLayerState(HistoricalAtlasLayer.LineageRanges, true, 0.90),
        new HistoricalAtlasLayerState(HistoricalAtlasLayer.Refugia, false, 0.90),
        new HistoricalAtlasLayerState(HistoricalAtlasLayer.ExtinctionDebt, false, 0.90),
        new HistoricalAtlasLayerState(HistoricalAtlasLayer.Provenance, true, 1.0)
    });

    private static HistoricalAtlasLayerState[] NormalizeLayers(IEnumerable<HistoricalAtlasLayerState> layers)
    {
        var array = layers.OrderBy(x => x.Layer).ToArray();
        if (array.Length == 0)
            throw new InvalidOperationException("Historical atlas requires at least one layer state.");
        if (array.Select(x => x.Layer).Distinct().Count() != array.Length)
            throw new InvalidOperationException("Historical atlas layer states cannot contain duplicates.");
        foreach (var layer in array) layer.Validate();
        return array;
    }

    private static HistoricalAtlasCellPayload MapCell(
        PaleogeographicCellState cell,
        int generation,
        IReadOnlySet<HistoricalAtlasLayer> enabled)
    {
        bool climate = enabled.Contains(HistoricalAtlasLayer.Climate);
        bool landscape = enabled.Contains(HistoricalAtlasLayer.Landscape);
        bool biodiversity = enabled.Contains(HistoricalAtlasLayer.Biodiversity);
        bool population = enabled.Contains(HistoricalAtlasLayer.Population);
        bool lineages = enabled.Contains(HistoricalAtlasLayer.LineageRanges);
        bool refugia = enabled.Contains(HistoricalAtlasLayer.Refugia);
        bool debt = enabled.Contains(HistoricalAtlasLayer.ExtinctionDebt);

        var refugiumMarkers = refugia
            ? cell.Refugia.OrderBy(x => x.RefugiumId, StringComparer.Ordinal).Select(x => new HistoricalAtlasRefugiumMarker(
                x.RefugiumId, x.LineageId, x.Persistent, x.MeanSuitability, x.FirstObservedGeneration, x.LastObservedGeneration)).ToArray()
            : Array.Empty<HistoricalAtlasRefugiumMarker>();
        var debtMarkers = debt
            ? cell.ExtinctionDebt.OrderBy(x => x.LineageId, StringComparer.Ordinal).Select(x => new HistoricalAtlasExtinctionDebtMarker(
                x.LineageId, x.DebtScore, x.ConsecutiveDebtGenerations, x.Realized, x.RealizedGeneration)).ToArray()
            : Array.Empty<HistoricalAtlasExtinctionDebtMarker>();

        return new HistoricalAtlasCellPayload(
            cell.ZoneId,
            cell.ZoneId,
            generation,
            climate ? cell.Climate?.Regime : null,
            climate ? cell.Climate?.Temperature : null,
            climate ? cell.Climate?.Moisture : null,
            landscape ? cell.Landscape?.ForestCover : null,
            landscape ? cell.Landscape?.WetlandExtent : null,
            landscape ? cell.Landscape?.RiverConnectivity : null,
            landscape ? cell.Landscape?.CoastalExposure : null,
            landscape ? cell.Landscape?.TerrainIntegrity : null,
            biodiversity ? cell.Biodiversity?.LineageRichness : null,
            biodiversity ? cell.Biodiversity?.SpeciesFamilyRichness : null,
            population ? cell.Biodiversity?.ApproximatePopulation : null,
            lineages ? Array.AsReadOnly(cell.Lineages.Select(x => x.LineageId).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray()) : Array.Empty<string>(),
            Array.AsReadOnly(refugiumMarkers),
            Array.AsReadOnly(debtMarkers),
            cell.Completeness,
            cell.IntegrityHash);
    }

    private static HistoricalAtlasLineageRange[] BuildRanges(IEnumerable<PaleogeographicCellState> cells)
    {
        var observations = cells
            .SelectMany(cell => cell.Lineages.Select(lineage => (Cell: cell, Lineage: lineage)))
            .GroupBy(x => x.Lineage.LineageId, StringComparer.Ordinal)
            .OrderBy(x => x.Key, StringComparer.Ordinal);
        var output = new List<HistoricalAtlasLineageRange>();
        foreach (var group in observations)
        {
            var ordered = group.OrderBy(x => x.Cell.ZoneId, StringComparer.Ordinal).ToArray();
            var first = ordered[0].Lineage;
            if (ordered.Any(x => !StringComparer.Ordinal.Equals(x.Lineage.SpeciesFamilyId, first.SpeciesFamilyId)))
                throw new InvalidOperationException("Historical lineage range evidence disagrees on species-family identity.");
            if (ordered.Any(x => x.Lineage.Authority != first.Authority || x.Lineage.Status != first.Status))
                throw new InvalidOperationException("Historical lineage range evidence disagrees on authority or lineage status.");
            if (ordered.Any(x => x.Lineage.ApproximateGlobalPopulation != first.ApproximateGlobalPopulation))
                throw new InvalidOperationException("Historical lineage range evidence disagrees on global population.");
            output.Add(new HistoricalAtlasLineageRange(
                group.Key,
                first.SpeciesFamilyId,
                Array.AsReadOnly(ordered.Select(x => x.Cell.ZoneId).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray()),
                first.Authority,
                first.Status,
                ordered.Average(x => x.Lineage.MeanDivergence),
                first.ApproximateGlobalPopulation));
        }
        return output.ToArray();
    }

    private static HistoricalAtlasProvenancePanel MapProvenance(PaleogeographicCellState cell) =>
        new(
            cell.ZoneId,
            cell.RequestedGeneration,
            cell.Completeness,
            Array.AsReadOnly(cell.MissingEvidence.OrderBy(x => x, StringComparer.Ordinal).ToArray()),
            Array.AsReadOnly(cell.Evidence.OrderBy(x => x.Kind).ThenBy(x => x.StratumId, StringComparer.Ordinal).ToArray()),
            cell.IntegrityHash);
}
