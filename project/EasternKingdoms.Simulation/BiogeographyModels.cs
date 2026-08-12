namespace EasternKingdoms.Simulation;

public sealed record BiogeographySettings(
    int MinimumRefugiumPersistenceGenerations = 4,
    int ExtinctionDebtGraceGenerations = 5,
    double ExtinctionDebtRealizationThreshold = 0.70,
    double HabitatCollapseSuitability = 0.30,
    double RefugiumSuitabilityThreshold = 0.62,
    double CorridorOpenThreshold = 0.45,
    int StratumSpanGenerations = 25)
{
    public void Validate()
    {
        if (MinimumRefugiumPersistenceGenerations < 1)
            throw new InvalidOperationException("Refugium persistence must be positive.");
        if (ExtinctionDebtGraceGenerations < 1)
            throw new InvalidOperationException("Extinction-debt grace must be positive.");
        if (ExtinctionDebtRealizationThreshold is < 0.0 or > 1.0)
            throw new InvalidOperationException("Extinction-debt realization threshold must remain inside [0, 1].");
        if (HabitatCollapseSuitability is < 0.0 or > 1.0)
            throw new InvalidOperationException("Habitat-collapse suitability must remain inside [0, 1].");
        if (RefugiumSuitabilityThreshold is < 0.0 or > 1.0)
            throw new InvalidOperationException("Refugium suitability must remain inside [0, 1].");
        if (CorridorOpenThreshold is < 0.0 or > 1.0)
            throw new InvalidOperationException("Corridor-open threshold must remain inside [0, 1].");
        if (StratumSpanGenerations < 1)
            throw new InvalidOperationException("Biogeographic stratum span must be positive.");
    }
}

public enum RangeShiftKind
{
    Expansion,
    Contraction,
    Fragmentation,
    Reconnection,
    LocalExtirpation,
    Recolonization
}

public sealed record HabitatZoneState(
    string ZoneId,
    int Generation,
    double Suitability,
    double Disturbance,
    bool Stable);

public sealed record HabitatCorridorState(
    string RouteId,
    string FromZoneId,
    string ToZoneId,
    int Generation,
    double Permeability,
    bool Open);

public sealed record LineageRangeState(
    string LineageId,
    string SpeciesFamilyId,
    int FirstObservedGeneration,
    int LastObservedGeneration,
    long Population,
    IReadOnlySet<string> OccupiedZones,
    DeepTimeLineageAuthority Authority,
    int ConnectedComponents);

public sealed record RangeShiftEvent(
    string EventId,
    string LineageId,
    RangeShiftKind Kind,
    int Generation,
    IReadOnlySet<string> AddedZones,
    IReadOnlySet<string> RemovedZones,
    int PreviousComponents,
    int CurrentComponents,
    DeepTimeLineageAuthority Authority);

public sealed record LineageRangeTransition(
    LineageRangeState? Previous,
    LineageRangeState Current,
    IReadOnlyList<RangeShiftEvent> Events);

public sealed record RefugiumRecord(
    string RefugiumId,
    string LineageId,
    string ZoneId,
    int FirstObservedGeneration,
    int LastObservedGeneration,
    int ConsecutiveGenerations,
    double MeanSuitability,
    bool Persistent,
    DeepTimeLineageAuthority Authority);

public sealed record RecolonizationWaveEvent(
    string EventId,
    string LineageId,
    string SourceZoneId,
    string DestinationZoneId,
    string RouteId,
    int Generation,
    long EstimatedMigrants,
    bool SourceWasRefugium,
    DeepTimeLineageAuthority Authority);

public sealed record ExtinctionDebtState(
    string LineageId,
    string ZoneId,
    int FirstDebtGeneration,
    int LastGeneration,
    int ConsecutiveDebtGenerations,
    double DebtScore,
    bool Realized,
    int? RealizedGeneration);

public sealed record RegionalBiodiversitySnapshot(
    string ZoneId,
    int Generation,
    int LineageRichness,
    int SpeciesFamilyRichness,
    double ShannonDiversity,
    long ApproximatePopulation,
    IReadOnlySet<string> LineageIds);

public sealed record CommunityTurnoverMetrics(
    string ZoneId,
    int FromGeneration,
    int ToGeneration,
    int Gains,
    int Losses,
    int Retained,
    double JaccardDissimilarity,
    IReadOnlySet<string> GainedLineages,
    IReadOnlySet<string> LostLineages);

public sealed record BiogeographicBatchObservation(
    int Generation,
    IReadOnlyList<DeepTimeLineageObservation> Lineages,
    IReadOnlyList<ZoneEnvironment> Zones);

public sealed record BiogeographicGenerationResult(
    int Generation,
    IReadOnlyList<LineageRangeState> Ranges,
    IReadOnlyList<HabitatZoneState> Habitats,
    IReadOnlyList<HabitatCorridorState> Corridors,
    IReadOnlyList<RangeShiftEvent> RangeEvents,
    IReadOnlyList<RefugiumRecord> Refugia,
    IReadOnlyList<RecolonizationWaveEvent> RecolonizationWaves,
    IReadOnlyList<ExtinctionDebtState> ExtinctionDebt,
    IReadOnlyList<RegionalBiodiversitySnapshot> Biodiversity,
    IReadOnlyList<CommunityTurnoverMetrics> Turnover);
