namespace EasternKingdoms.Simulation;

public sealed class EcologicalRecoveryTracker
{
    private readonly GeoclimateSettings _settings;
    private readonly Dictionary<string, EcologicalRecoveryRecord> _records = new(StringComparer.Ordinal);

    public EcologicalRecoveryTracker(GeoclimateSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _settings.Validate();
    }

    public IReadOnlyCollection<EcologicalRecoveryRecord> Records =>
        _records.Values.OrderBy(x => x.ZoneId, StringComparer.Ordinal).ToArray();

    public EcologicalRecoveryRecord Observe(
        ClimateZoneState climate,
        LandscapeZoneState landscape)
    {
        ArgumentNullException.ThrowIfNull(climate);
        ArgumentNullException.ThrowIfNull(landscape);
        if (!StringComparer.Ordinal.Equals(climate.ZoneId, landscape.ZoneId))
            throw new InvalidOperationException("Recovery observation requires matching climate and landscape zone IDs.");

        bool impactedNow = landscape.DisturbanceLoad >= 0.50;
        bool recoveryEligible =
            climate.ClimateStability >= _settings.RecoveryClimateStabilityThreshold &&
            landscape.TerrainIntegrity >= _settings.RecoveryLandscapeIntegrityThreshold &&
            landscape.DisturbanceLoad < 0.50;

        if (!_records.TryGetValue(climate.ZoneId, out var previous))
        {
            var first = new EcologicalRecoveryRecord(
                climate.ZoneId,
                impactedNow ? climate.Generation : -1,
                climate.Generation,
                impactedNow ? RecoveryStatus.Impacted : RecoveryStatus.Stable,
                0,
                climate.ClimateStability,
                landscape.TerrainIntegrity,
                landscape.DisturbanceLoad);
            _records[climate.ZoneId] = first;
            return first;
        }

        int firstImpact = previous.FirstImpactGeneration;
        RecoveryStatus status = previous.Status;
        int consecutive = previous.ConsecutiveRecoveryGenerations;

        if (impactedNow)
        {
            firstImpact = firstImpact < 0 ? climate.Generation : firstImpact;
            status = RecoveryStatus.Impacted;
            consecutive = 0;
        }
        else if (firstImpact < 0)
        {
            status = RecoveryStatus.Stable;
            consecutive = 0;
        }
        else if (recoveryEligible)
        {
            consecutive = previous.LastGeneration == climate.Generation - 1 ? consecutive + 1 : 1;
            status = consecutive >= _settings.RecoveryPersistenceGenerations
                ? RecoveryStatus.Recovered
                : RecoveryStatus.Recovering;
        }
        else
        {
            status = RecoveryStatus.Stabilizing;
            consecutive = 0;
        }

        var next = new EcologicalRecoveryRecord(
            climate.ZoneId,
            firstImpact,
            climate.Generation,
            status,
            consecutive,
            climate.ClimateStability,
            landscape.TerrainIntegrity,
            landscape.DisturbanceLoad);
        _records[climate.ZoneId] = next;
        return next;
    }
}
