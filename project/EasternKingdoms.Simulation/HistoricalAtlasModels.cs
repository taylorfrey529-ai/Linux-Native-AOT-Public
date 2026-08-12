namespace EasternKingdoms.Simulation;

public enum HistoricalAtlasAuthority
{
    TestHistoryOnly
}

public enum HistoricalAtlasLayer
{
    Climate,
    Landscape,
    Biodiversity,
    Population,
    LineageRanges,
    Refugia,
    ExtinctionDebt,
    Provenance
}

public enum HistoricalAtlasScrubPolicy
{
    ExactRecorded,
    PreviousOrExact,
    NextOrExact,
    NearestRecorded
}

public enum HistoricalAtlasComparisonMode
{
    Single,
    SideBySide,
    Delta
}

public enum HistoricalAtlasProjection
{
    Globe,
    Atlas
}

public sealed record HistoricalAtlasSettings(
    HistoricalAtlasScrubPolicy DefaultScrubPolicy = HistoricalAtlasScrubPolicy.PreviousOrExact,
    double MinimumZoom = 0.10,
    double MaximumZoom = 12.0)
{
    public void Validate()
    {
        if (MinimumZoom <= 0.0 || double.IsNaN(MinimumZoom) || double.IsInfinity(MinimumZoom))
            throw new InvalidOperationException("Minimum atlas zoom must be finite and positive.");
        if (MaximumZoom < MinimumZoom || double.IsNaN(MaximumZoom) || double.IsInfinity(MaximumZoom))
            throw new InvalidOperationException("Maximum atlas zoom must be finite and at least the minimum zoom.");
    }
}

public sealed record HistoricalAtlasLayerState(
    HistoricalAtlasLayer Layer,
    bool Enabled,
    double Opacity)
{
    public void Validate()
    {
        if (Opacity is < 0.0 or > 1.0 || double.IsNaN(Opacity) || double.IsInfinity(Opacity))
            throw new InvalidOperationException("Atlas layer opacity must be between zero and one.");
    }
}

public sealed record HistoricalAtlasCameraState(
    HistoricalAtlasProjection Projection,
    double YawDegrees,
    double PitchDegrees,
    double RollDegrees,
    double Zoom,
    string? FocusZoneId)
{
    public void Validate(HistoricalAtlasSettings settings, IReadOnlySet<string>? authorizedZoneIds = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();
        if (YawDegrees is < -180.0 or > 180.0 || double.IsNaN(YawDegrees) || double.IsInfinity(YawDegrees))
            throw new InvalidOperationException("Atlas camera yaw must be finite and between -180 and 180 degrees.");
        if (PitchDegrees is < -90.0 or > 90.0 || double.IsNaN(PitchDegrees) || double.IsInfinity(PitchDegrees))
            throw new InvalidOperationException("Atlas camera pitch must be finite and between -90 and 90 degrees.");
        if (RollDegrees is < -180.0 or > 180.0 || double.IsNaN(RollDegrees) || double.IsInfinity(RollDegrees))
            throw new InvalidOperationException("Atlas camera roll must be finite and between -180 and 180 degrees.");
        if (Zoom < settings.MinimumZoom || Zoom > settings.MaximumZoom || double.IsNaN(Zoom) || double.IsInfinity(Zoom))
            throw new InvalidOperationException("Atlas camera zoom is outside the configured range.");
        if (FocusZoneId is not null && string.IsNullOrWhiteSpace(FocusZoneId))
            throw new InvalidOperationException("Atlas camera focus zone cannot be blank.");
        if (FocusZoneId is not null && authorizedZoneIds is not null && !authorizedZoneIds.Contains(FocusZoneId))
            throw new InvalidOperationException("Atlas camera focus zone is outside the authorized Eastern Kingdoms set.");
    }

    public static HistoricalAtlasCameraState DefaultGlobe() =>
        new(HistoricalAtlasProjection.Globe, 0.0, 0.0, 0.0, 1.0, null);
}

public sealed record HistoricalAtlasRefugiumMarker(
    string RefugiumId,
    string LineageId,
    bool Persistent,
    double MeanSuitability,
    int FirstObservedGeneration,
    int LastObservedGeneration);

public sealed record HistoricalAtlasExtinctionDebtMarker(
    string LineageId,
    double DebtScore,
    int ConsecutiveDebtGenerations,
    bool Realized,
    int? RealizedGeneration);

public sealed record HistoricalAtlasCellPayload(
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
    long? ApproximatePopulation,
    IReadOnlyList<string> LivingLineageIds,
    IReadOnlyList<HistoricalAtlasRefugiumMarker> Refugia,
    IReadOnlyList<HistoricalAtlasExtinctionDebtMarker> ExtinctionDebt,
    ReconstructionCompleteness Completeness,
    string SourceIntegrityHash);

public sealed record HistoricalAtlasLineageRange(
    string LineageId,
    string SpeciesFamilyId,
    IReadOnlyList<string> ZoneIds,
    DeepTimeLineageAuthority Authority,
    DeepTimeLineageStatus Status,
    double MeanDivergence,
    long ApproximateGlobalPopulation);

public sealed record HistoricalAtlasProvenancePanel(
    string ZoneId,
    int Generation,
    ReconstructionCompleteness Completeness,
    IReadOnlyList<string> MissingEvidence,
    IReadOnlyList<HistoricalEvidenceReference> Evidence,
    string SourceIntegrityHash);

public sealed record HistoricalAtlasScenePayload(
    int FrameIndex,
    int Generation,
    HistoricalAtlasCameraState Camera,
    IReadOnlyList<HistoricalAtlasLayerState> Layers,
    IReadOnlyList<HistoricalAtlasCellPayload> Cells,
    IReadOnlyList<HistoricalAtlasLineageRange> LineageRanges,
    IReadOnlyList<HistoricalAtlasProvenancePanel> ProvenancePanels,
    HistoricalAtlasAuthority Authority,
    string SourceWorldIntegrityHash,
    string IntegrityHash);

public sealed record HistoricalAtlasComparisonPayload(
    HistoricalAtlasComparisonMode Mode,
    HistoricalAtlasScenePayload From,
    HistoricalAtlasScenePayload To,
    IReadOnlyList<HistoricalChangeOverlay> Overlays,
    HistoricalAtlasAuthority Authority,
    string IntegrityHash);

public sealed record HistoricalAtlasCameraFile(
    string SchemaVersion,
    HistoricalAtlasAuthority Authority,
    string PayloadSha256,
    HistoricalAtlasCameraState Camera);

public sealed record HistoricalAtlasCameraSerializationResult(
    string Json,
    string PayloadSha256,
    HistoricalAtlasCameraFile File);

public sealed record HistoricalAtlasSceneFile(
    string SchemaVersion,
    HistoricalAtlasAuthority Authority,
    string PayloadSha256,
    HistoricalAtlasScenePayload Scene);

public sealed record HistoricalAtlasSceneSerializationResult(
    string Json,
    string PayloadSha256,
    HistoricalAtlasSceneFile File);

public sealed record HistoricalAtlasSessionState(
    int FrameIndex,
    int Generation,
    HistoricalAtlasComparisonMode ComparisonMode,
    int? ComparisonGeneration,
    HistoricalAtlasCameraState Camera,
    IReadOnlyList<HistoricalAtlasLayerState> Layers,
    string? SelectedZoneId,
    string? SelectedLineageId,
    HistoricalAtlasAuthority Authority,
    string IntegrityHash);

public sealed record HistoricalAtlasServices(
    HistoricalVisualizationRuntime Visualization,
    HistoricalAtlasSceneBuilder Scenes,
    HistoricalAtlasComparisonBuilder Comparisons,
    HistoricalAtlasCameraSerializer CameraSerializer,
    HistoricalAtlasSceneSerializer SceneSerializer,
    IReadOnlySet<string> AuthorizedZoneIds,
    HistoricalAtlasSettings Settings)
{
    public HistoricalAtlasSession CreateSession(
        IEnumerable<HistoricalPlaybackFrame> frames,
        HistoricalAtlasCameraState? camera = null,
        IEnumerable<HistoricalAtlasLayerState>? layers = null)
    {
        return new HistoricalAtlasSession(
            frames,
            AuthorizedZoneIds,
            Scenes,
            Comparisons,
            Settings,
            camera,
            layers);
    }
}
