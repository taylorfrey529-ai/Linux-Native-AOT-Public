using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace EasternKingdoms.Simulation;

public sealed record GeoclimateArchiveStratum(
    int StartGeneration,
    int EndGeneration,
    IReadOnlyList<ClimateZoneState> Climate,
    IReadOnlyList<LandscapeZoneState> Landscapes,
    IReadOnlyList<LandscapeChangeEvent> LandscapeEvents,
    IReadOnlyList<HabitatEnvelopeMigrationEvent> EnvelopeEvents,
    IReadOnlyList<EcologicalRecoveryRecord> Recovery,
    string PreviousHash,
    string IntegrityHash);

public sealed record GeoclimateArchiveSnapshot(
    IReadOnlyList<GeoclimateArchiveStratum> Strata,
    string HeadHash);

public sealed class GeoclimateArchive
{
    public const string GenesisHash = "GENESIS";
    private readonly List<GeoclimateArchiveStratum> _strata = new();

    public IReadOnlyList<GeoclimateArchiveStratum> Strata => _strata.ToArray();

    public GeoclimateArchiveStratum Commit(
        int startGeneration,
        int endGeneration,
        IEnumerable<ClimateZoneState> climate,
        IEnumerable<LandscapeZoneState> landscapes,
        IEnumerable<LandscapeChangeEvent> landscapeEvents,
        IEnumerable<HabitatEnvelopeMigrationEvent> envelopeEvents,
        IEnumerable<EcologicalRecoveryRecord> recovery)
    {
        if (startGeneration < 0 || endGeneration < startGeneration)
            throw new InvalidOperationException("Geoclimate archive stratum range is invalid.");
        if (_strata.Count > 0 && startGeneration <= _strata[^1].EndGeneration)
            throw new InvalidOperationException("Geoclimate archive strata may not overlap or move backward.");

        var climateCopy = climate.Select(Clone).OrderBy(x => x.Generation).ThenBy(x => x.ZoneId, StringComparer.Ordinal).ToArray();
        var landscapeCopy = landscapes.Select(Clone).OrderBy(x => x.Generation).ThenBy(x => x.ZoneId, StringComparer.Ordinal).ToArray();
        var landscapeEventCopy = landscapeEvents.Select(x => x with { }).OrderBy(x => x.Generation).ThenBy(x => x.EventId, StringComparer.Ordinal).ToArray();
        var eventCopy = envelopeEvents.Select(x => x with { }).OrderBy(x => x.Generation).ThenBy(x => x.EventId, StringComparer.Ordinal).ToArray();
        var recoveryCopy = recovery.Select(x => x with { }).OrderBy(x => x.LastGeneration).ThenBy(x => x.ZoneId, StringComparer.Ordinal).ToArray();
        string previous = _strata.Count == 0 ? GenesisHash : _strata[^1].IntegrityHash;
        string hash = ComputeHash(startGeneration, endGeneration, climateCopy, landscapeCopy, landscapeEventCopy, eventCopy, recoveryCopy, previous);

        var stratum = new GeoclimateArchiveStratum(
            startGeneration,
            endGeneration,
            climateCopy,
            landscapeCopy,
            landscapeEventCopy,
            eventCopy,
            recoveryCopy,
            previous,
            hash);
        _strata.Add(stratum);
        return stratum;
    }

    public bool Verify()
    {
        string previous = GenesisHash;
        foreach (var stratum in _strata)
        {
            if (!StringComparer.Ordinal.Equals(stratum.PreviousHash, previous))
                return false;
            string expected = ComputeHash(
                stratum.StartGeneration,
                stratum.EndGeneration,
                stratum.Climate,
                stratum.Landscapes,
                stratum.LandscapeEvents,
                stratum.EnvelopeEvents,
                stratum.Recovery,
                previous);
            if (!StringComparer.Ordinal.Equals(expected, stratum.IntegrityHash))
                return false;
            previous = stratum.IntegrityHash;
        }
        return true;
    }

    public GeoclimateArchiveSnapshot Snapshot() =>
        new(_strata.ToArray(), _strata.Count == 0 ? GenesisHash : _strata[^1].IntegrityHash);

    private static ClimateZoneState Clone(ClimateZoneState value) =>
        value with { ActiveEpochIds = value.ActiveEpochIds.ToArray() };

    private static LandscapeZoneState Clone(LandscapeZoneState value) =>
        value with { ActiveDisturbanceIds = value.ActiveDisturbanceIds.ToArray() };

    private static string ComputeHash(
        int start,
        int end,
        IEnumerable<ClimateZoneState> climate,
        IEnumerable<LandscapeZoneState> landscapes,
        IEnumerable<LandscapeChangeEvent> landscapeEvents,
        IEnumerable<HabitatEnvelopeMigrationEvent> events,
        IEnumerable<EcologicalRecoveryRecord> recovery,
        string previous)
    {
        var sb = new StringBuilder();
        sb.Append("geoclimate/20|").Append(start).Append('|').Append(end).Append('|').Append(previous).Append('\n');
        foreach (var x in climate.OrderBy(x => x.Generation).ThenBy(x => x.ZoneId, StringComparer.Ordinal))
        {
            sb.Append("C|").Append(x.Generation).Append('|').Append(x.ZoneId).Append('|').Append(x.Regime).Append('|')
                .Append(F(x.Temperature)).Append('|').Append(F(x.Moisture)).Append('|').Append(F(x.Storminess)).Append('|')
                .Append(F(x.Seasonality)).Append('|').Append(F(x.ResonanceBackground)).Append('|').Append(F(x.ClimateStability)).Append('|')
                .Append(string.Join(',', x.ActiveEpochIds.OrderBy(y => y, StringComparer.Ordinal))).Append('\n');
        }
        foreach (var x in landscapes.OrderBy(x => x.Generation).ThenBy(x => x.ZoneId, StringComparer.Ordinal))
        {
            sb.Append("L|").Append(x.Generation).Append('|').Append(x.ZoneId).Append('|')
                .Append(F(x.ForestCover)).Append('|').Append(F(x.WetlandExtent)).Append('|').Append(F(x.RiverConnectivity)).Append('|')
                .Append(F(x.CoastalExposure)).Append('|').Append(F(x.TerrainIntegrity)).Append('|').Append(F(x.DisturbanceLoad)).Append('|')
                .Append(string.Join(',', x.ActiveDisturbanceIds.OrderBy(y => y, StringComparer.Ordinal))).Append('\n');
        }
        foreach (var x in landscapeEvents.OrderBy(x => x.Generation).ThenBy(x => x.EventId, StringComparer.Ordinal))
            sb.Append("G|").Append(x.Generation).Append('|').Append(x.EventId).Append('|').Append(x.ZoneId).Append('|').Append(x.Kind).Append('|')
                .Append(F(x.PreviousValue)).Append('|').Append(F(x.CurrentValue)).Append('|').Append(F(x.Delta)).Append('\n');
        foreach (var x in events.OrderBy(x => x.Generation).ThenBy(x => x.EventId, StringComparer.Ordinal))
            sb.Append("E|").Append(x.Generation).Append('|').Append(x.EventId).Append('|').Append(x.EnvelopeId).Append('|').Append(x.Kind).Append('|')
                .Append(x.SourceZoneId ?? "").Append('|').Append(x.DestinationZoneId).Append('|').Append(x.RouteId ?? "").Append('|').Append(x.PotentialOnly).Append('\n');
        foreach (var x in recovery.OrderBy(x => x.LastGeneration).ThenBy(x => x.ZoneId, StringComparer.Ordinal))
            sb.Append("R|").Append(x.LastGeneration).Append('|').Append(x.ZoneId).Append('|').Append(x.FirstImpactGeneration).Append('|').Append(x.Status).Append('|')
                .Append(x.ConsecutiveRecoveryGenerations).Append('|').Append(F(x.ClimateStability)).Append('|').Append(F(x.LandscapeIntegrity)).Append('|').Append(F(x.DisturbanceLoad)).Append('\n');

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string F(double value) => value.ToString("R", CultureInfo.InvariantCulture);
}
