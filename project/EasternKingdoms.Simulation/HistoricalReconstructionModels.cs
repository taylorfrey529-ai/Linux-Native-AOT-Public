namespace EasternKingdoms.Simulation;

public enum HistoricalReconstructionAuthority
{
    TestHistoryOnly
}

public enum ReconstructionCompleteness
{
    None,
    Partial,
    Substantial,
    Complete
}

public enum HistoricalEvidenceKind
{
    GeoclimateStratum,
    BiogeographicStratum,
    DeepTimeStratum
}

public sealed record HistoricalPlaybackSettings(
    int RecentLandscapeEventLimit = 8,
    bool RequireVerifiedSourceChains = true)
{
    public void Validate()
    {
        if (RecentLandscapeEventLimit < 0)
            throw new InvalidOperationException("Recent landscape event limit cannot be negative.");
    }
}

public sealed record HistoricalReconstructionRequest(
    int Generation,
    IReadOnlySet<string> ZoneIds,
    bool RequireCompleteEvidence = false);

public sealed record HistoricalEvidenceReference(
    HistoricalEvidenceKind Kind,
    string StratumId,
    int StartGeneration,
    int EndGeneration,
    string IntegrityHash);

public sealed record HistoricalLineagePresence(
    string LineageId,
    string SpeciesFamilyId,
    string? ParentLineageId,
    int EvidenceGeneration,
    long ApproximateGlobalPopulation,
    DeepTimeLineageAuthority Authority,
    DeepTimeLineageStatus Status,
    double MeanDivergence);

public sealed record PaleogeographicCellState(
    string ZoneId,
    int RequestedGeneration,
    ClimateZoneState? Climate,
    LandscapeZoneState? Landscape,
    RegionalBiodiversitySnapshot? Biodiversity,
    IReadOnlyList<HistoricalLineagePresence> Lineages,
    IReadOnlyList<RefugiumRecord> Refugia,
    IReadOnlyList<ExtinctionDebtState> ExtinctionDebt,
    IReadOnlyList<LandscapeChangeEvent> RecentLandscapeEvents,
    ReconstructionCompleteness Completeness,
    IReadOnlyList<string> MissingEvidence,
    IReadOnlyList<HistoricalEvidenceReference> Evidence,
    HistoricalReconstructionAuthority Authority,
    string IntegrityHash);

public sealed record HistoricalWorldStateSnapshot(
    int Generation,
    IReadOnlyList<PaleogeographicCellState> Cells,
    ReconstructionCompleteness Completeness,
    IReadOnlyList<string> MissingEvidence,
    IReadOnlyList<HistoricalEvidenceReference> Evidence,
    HistoricalReconstructionAuthority Authority,
    string IntegrityHash);

public sealed record HistoricalPlaybackFrame(
    int FrameIndex,
    HistoricalWorldStateSnapshot World);

public sealed record HistoricalCellDelta(
    string ZoneId,
    int FromGeneration,
    int ToGeneration,
    double? TemperatureDelta,
    double? MoistureDelta,
    double? ForestCoverDelta,
    double? WetlandExtentDelta,
    double? RiverConnectivityDelta,
    double? CoastalExposureDelta,
    double? TerrainIntegrityDelta,
    IReadOnlyList<string> GainedLineages,
    IReadOnlyList<string> LostLineages);

public sealed record HistoricalWorldStateDelta(
    int FromGeneration,
    int ToGeneration,
    IReadOnlyList<HistoricalCellDelta> Cells,
    string IntegrityHash);

public sealed record HistoricalPlaybackRuntimeSnapshot(
    IReadOnlyList<HistoricalWorldStateSnapshot> ReconstructedWorlds,
    string IntegrityHash);
