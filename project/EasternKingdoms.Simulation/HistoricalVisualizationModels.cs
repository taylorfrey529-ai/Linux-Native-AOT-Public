namespace EasternKingdoms.Simulation;

public enum HistoricalVisualizationAuthority
{
    TestHistoryOnly
}

public enum HistoricalOverlayMetric
{
    Temperature,
    Moisture,
    ForestCover,
    WetlandExtent,
    RiverConnectivity,
    CoastalExposure,
    TerrainIntegrity,
    LineageRichness,
    SpeciesFamilyRichness,
    ApproximatePopulation
}

public enum HistoricalOverlayDirection
{
    Decrease,
    Stable,
    Increase,
    Missing
}

public enum GlobeLayoutPolicy
{
    AuthoredAnchorsRequired
}

public sealed record HistoricalVisualizationSettings(
    string ReplaySchemaVersion = "evermore.eastern-kingdoms.history/22.0",
    double StableDeltaEpsilon = 1e-9)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ReplaySchemaVersion))
            throw new InvalidOperationException("Replay schema version is required.");
        if (StableDeltaEpsilon < 0.0 || double.IsNaN(StableDeltaEpsilon) || double.IsInfinity(StableDeltaEpsilon))
            throw new InvalidOperationException("Stable delta epsilon must be finite and non-negative.");
    }
}

public sealed record HistoricalTimelineIndexEntry(
    int FrameIndex,
    int Generation,
    string WorldIntegrityHash,
    ReconstructionCompleteness Completeness,
    IReadOnlyList<string> ZoneIds,
    int EvidenceCount);

public sealed record HistoricalTimelineIndex(
    IReadOnlyList<HistoricalTimelineIndexEntry> Entries,
    string IntegrityHash);

public sealed record SerializedHistoricalCell(
    string ZoneId,
    int Generation,
    ClimateRegimeKind? ClimateRegime,
    double? Temperature,
    double? Moisture,
    double? ForestCover,
    double? WetlandExtent,
    double? RiverConnectivity,
    double? CoastalExposure,
    double? TerrainIntegrity,
    int? LineageRichness,
    int? SpeciesFamilyRichness,
    long? ApproximatePopulation,
    IReadOnlyList<string> LivingLineageIds,
    ReconstructionCompleteness Completeness,
    string SourceIntegrityHash);

public sealed record SerializedHistoricalFrame(
    int FrameIndex,
    int Generation,
    IReadOnlyList<SerializedHistoricalCell> Cells,
    ReconstructionCompleteness Completeness,
    string SourceWorldIntegrityHash);

public sealed record HistoricalReplayPayload(
    HistoricalTimelineIndex Timeline,
    IReadOnlyList<SerializedHistoricalFrame> Frames);

public sealed record HistoricalReplayFile(
    string SchemaVersion,
    HistoricalVisualizationAuthority Authority,
    string PayloadSha256,
    HistoricalReplayPayload Payload);

public sealed record HistoricalReplaySerializationResult(
    string Json,
    string PayloadSha256,
    HistoricalReplayFile File);

public sealed record HistoricalOverlayCell(
    string ZoneId,
    double? FromValue,
    double? ToValue,
    double? Delta,
    HistoricalOverlayDirection Direction);

public sealed record HistoricalChangeOverlay(
    HistoricalOverlayMetric Metric,
    int FromGeneration,
    int ToGeneration,
    IReadOnlyList<HistoricalOverlayCell> Cells,
    HistoricalVisualizationAuthority Authority,
    string IntegrityHash);

public sealed record HistoricalGlobeCellState(
    string ZoneId,
    string RenderAnchorKey,
    int Generation,
    ClimateRegimeKind? ClimateRegime,
    double? Temperature,
    double? Moisture,
    double? ForestCover,
    double? WetlandExtent,
    double? RiverConnectivity,
    double? CoastalExposure,
    double? TerrainIntegrity,
    int? LineageRichness,
    int? SpeciesFamilyRichness,
    ReconstructionCompleteness Completeness,
    string SourceIntegrityHash);

public sealed record HistoricalGlobeFrame(
    int Generation,
    GlobeLayoutPolicy LayoutPolicy,
    IReadOnlyList<HistoricalGlobeCellState> Cells,
    HistoricalVisualizationAuthority Authority,
    string IntegrityHash);

public sealed record HistoricalVisualizationRuntime(
    AzerothHistoricalPlaybackRuntime Historical,
    HistoricalTimelineIndexer Timeline,
    HistoricalReplaySerializer Serializer,
    HistoricalOverlayBuilder Overlays,
    EasternKingdomsGlobeStateAdapter Globe);
