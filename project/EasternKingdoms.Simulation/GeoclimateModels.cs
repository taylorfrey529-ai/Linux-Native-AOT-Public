namespace EasternKingdoms.Simulation;

public sealed record GeoclimateSettings(
    int StratumSpanGenerations = 25,
    double EnvelopeSuitabilityThreshold = 0.55,
    int RecoveryPersistenceGenerations = 3,
    double RecoveryClimateStabilityThreshold = 0.55,
    double RecoveryLandscapeIntegrityThreshold = 0.55,
    double LandscapeChangeEventThreshold = 0.05)
{
    public void Validate()
    {
        if (StratumSpanGenerations < 1)
            throw new InvalidOperationException("Geoclimate stratum span must be positive.");
        if (EnvelopeSuitabilityThreshold is < 0.0 or > 1.0)
            throw new InvalidOperationException("Envelope suitability threshold must remain inside [0, 1].");
        if (RecoveryPersistenceGenerations < 1)
            throw new InvalidOperationException("Recovery persistence must be positive.");
        if (RecoveryClimateStabilityThreshold is < 0.0 or > 1.0)
            throw new InvalidOperationException("Recovery climate-stability threshold must remain inside [0, 1].");
        if (RecoveryLandscapeIntegrityThreshold is < 0.0 or > 1.0)
            throw new InvalidOperationException("Recovery landscape-integrity threshold must remain inside [0, 1].");
        if (LandscapeChangeEventThreshold is <= 0.0 or > 1.0)
            throw new InvalidOperationException("Landscape-change event threshold must remain inside (0, 1].");
    }
}

public enum ClimateRegimeKind
{
    Baseline,
    CoolDry,
    CoolWet,
    WarmDry,
    WarmWet,
    HighlyVariable,
    ArcanePerturbed
}

public enum DisturbanceEpochKind
{
    Drought,
    FloodPulse,
    ColdCycle,
    WarmCycle,
    Fire,
    ErosionPulse,
    DiseaseAftermath,
    ArcaneInstability,
    CompoundCatastrophe
}

public enum RecoveryStatus
{
    Stable,
    Impacted,
    Stabilizing,
    Recovering,
    Recovered
}


public enum LandscapeChangeKind
{
    ForestAdvance,
    ForestRetreat,
    WetlandExpansion,
    WetlandContraction,
    RiverConnectivityGain,
    RiverConnectivityLoss,
    CoastalExposureIncrease,
    CoastalExposureDecrease,
    TerrainDegradation,
    TerrainRecovery
}

public enum HabitatEnvelopeShiftKind
{
    ExpansionOpportunity,
    Contraction,
    RouteBlocked,
    Reconnected
}

public sealed record ClimateZoneBaseline(
    string ZoneId,
    double Temperature,
    double Moisture,
    double Storminess,
    double Seasonality,
    double ResonanceBackground);

public sealed record ClimateEpochDefinition(
    string EpochId,
    int StartGeneration,
    int EndGeneration,
    IReadOnlySet<string> TargetZoneIds,
    double TemperatureDelta,
    double MoistureDelta,
    double StorminessDelta,
    double SeasonalityDelta,
    double ResonanceDelta);

public sealed record DisturbanceEpochDefinition(
    string DisturbanceId,
    DisturbanceEpochKind Kind,
    int StartGeneration,
    int EndGeneration,
    IReadOnlySet<string> TargetZoneIds,
    double Severity);

public sealed record ClimateZoneState(
    string ZoneId,
    int Generation,
    ClimateRegimeKind Regime,
    double Temperature,
    double Moisture,
    double Storminess,
    double Seasonality,
    double ResonanceBackground,
    double ClimateStability,
    IReadOnlyList<string> ActiveEpochIds);

public sealed record LandscapeBaseline(
    string ZoneId,
    double ForestPotential,
    double WetlandPotential,
    double RiverConnectivity,
    double CoastalExposure,
    double TerrainIntegrity,
    double ErosionResistance);

public sealed record LandscapeZoneState(
    string ZoneId,
    int Generation,
    double ForestCover,
    double WetlandExtent,
    double RiverConnectivity,
    double CoastalExposure,
    double TerrainIntegrity,
    double DisturbanceLoad,
    IReadOnlyList<string> ActiveDisturbanceIds);


public sealed record LandscapeChangeEvent(
    string EventId,
    string ZoneId,
    LandscapeChangeKind Kind,
    int Generation,
    double PreviousValue,
    double CurrentValue,
    double Delta);

public sealed record HabitatEnvelopeDefinition(
    string EnvelopeId,
    string SpeciesFamilyId,
    string? LineageId,
    double OptimalTemperature,
    double TemperatureTolerance,
    double OptimalMoisture,
    double MoistureTolerance,
    double MinimumForestCover,
    double MinimumWetlandExtent,
    double MaximumDisturbance,
    IReadOnlySet<string> AllowedZoneIds);

public sealed record HabitatEnvelopeZoneState(
    string EnvelopeId,
    string ZoneId,
    int Generation,
    double Suitability,
    bool Suitable);

public sealed record HabitatEnvelopeMigrationEvent(
    string EventId,
    string EnvelopeId,
    string SpeciesFamilyId,
    string? LineageId,
    HabitatEnvelopeShiftKind Kind,
    string? SourceZoneId,
    string DestinationZoneId,
    string? RouteId,
    int Generation,
    bool PotentialOnly);

public sealed record ClimateEvolutionPressure(
    string ZoneId,
    int Generation,
    IReadOnlyDictionary<string, double> TraitPressures,
    double ReproductiveModifier,
    double ViabilityModifier);

public sealed record EcologicalRecoveryRecord(
    string ZoneId,
    int FirstImpactGeneration,
    int LastGeneration,
    RecoveryStatus Status,
    int ConsecutiveRecoveryGenerations,
    double ClimateStability,
    double LandscapeIntegrity,
    double DisturbanceLoad);

public sealed record GeoclimateBatchObservation(
    int Generation,
    IReadOnlyList<ZoneEnvironment> Zones);

public sealed record GeoclimateGenerationResult(
    int Generation,
    IReadOnlyList<ClimateZoneState> Climate,
    IReadOnlyList<LandscapeZoneState> Landscapes,
    IReadOnlyList<LandscapeChangeEvent> LandscapeEvents,
    IReadOnlyList<HabitatEnvelopeZoneState> HabitatEnvelopes,
    IReadOnlyList<HabitatEnvelopeMigrationEvent> EnvelopeEvents,
    IReadOnlyList<ClimateEvolutionPressure> EvolutionPressures,
    IReadOnlyList<EcologicalRecoveryRecord> Recovery,
    IReadOnlyList<ZoneEnvironment> PressureIntegratedZones);
