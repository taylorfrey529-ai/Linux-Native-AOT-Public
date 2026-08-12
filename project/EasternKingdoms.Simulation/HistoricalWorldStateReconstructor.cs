using System.Collections.Frozen;

namespace EasternKingdoms.Simulation;

public sealed class HistoricalWorldStateReconstructor
{
    private readonly FrozenSet<string> _authorizedZones;
    private readonly GeoclimateArchive _geoclimate;
    private readonly BiogeographicArchive _biogeography;
    private readonly DeepTimeArchive _deepTime;
    private readonly HistoricalPlaybackSettings _settings;

    public HistoricalWorldStateReconstructor(
        IEnumerable<string> authorizedZoneIds,
        GeoclimateArchive geoclimate,
        BiogeographicArchive biogeography,
        DeepTimeArchive deepTime,
        HistoricalPlaybackSettings? settings = null)
    {
        _authorizedZones = authorizedZoneIds.Where(x => !string.IsNullOrWhiteSpace(x)).ToFrozenSet(StringComparer.Ordinal);
        if (_authorizedZones.Count == 0)
            throw new InvalidOperationException("Historical reconstruction requires authorized Eastern Kingdoms zones.");

        _geoclimate = geoclimate ?? throw new ArgumentNullException(nameof(geoclimate));
        _biogeography = biogeography ?? throw new ArgumentNullException(nameof(biogeography));
        _deepTime = deepTime ?? throw new ArgumentNullException(nameof(deepTime));
        _settings = settings ?? new HistoricalPlaybackSettings();
        _settings.Validate();
    }

    public HistoricalWorldStateSnapshot Reconstruct(HistoricalReconstructionRequest request)
    {
        ValidateRequest(request);
        VerifySources();

        var requestedZones = request.ZoneIds.ToFrozenSet(StringComparer.Ordinal);
        var cells = requestedZones.OrderBy(x => x, StringComparer.Ordinal)
            .Select(zoneId => ReconstructCell(zoneId, request.Generation))
            .ToArray();

        var evidence = DistinctEvidence(cells.SelectMany(x => x.Evidence));
        var missing = cells.SelectMany(x => x.MissingEvidence.Select(m => $"{x.ZoneId}:{m}"))
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();
        ReconstructionCompleteness completeness = cells.Length == 0
            ? ReconstructionCompleteness.None
            : (ReconstructionCompleteness)cells.Min(x => (int)x.Completeness);

        string hash = HistoricalPlaybackHasher.HashWorld(request.Generation, cells, completeness, missing, evidence);
        var snapshot = new HistoricalWorldStateSnapshot(
            request.Generation,
            Array.AsReadOnly(cells),
            completeness,
            Array.AsReadOnly(missing),
            Array.AsReadOnly(evidence),
            HistoricalReconstructionAuthority.TestHistoryOnly,
            hash);

        if (request.RequireCompleteEvidence && completeness != ReconstructionCompleteness.Complete)
            throw new InvalidOperationException($"Generation {request.Generation} cannot be reconstructed completely from sealed historical evidence.");

        return snapshot;
    }

    private PaleogeographicCellState ReconstructCell(string zoneId, int generation)
    {
        var evidence = new List<HistoricalEvidenceReference>();
        var missing = new List<string>();

        var climateMatch = FindClimate(zoneId, generation);
        var landscapeMatch = FindLandscape(zoneId, generation);
        var biodiversityMatch = FindBiodiversity(zoneId, generation);
        var lineages = FindLineages(zoneId, generation, evidence, out bool hasDeepTimeEvidence);
        var refugia = FindRefugia(zoneId, generation, evidence);
        var debt = FindExtinctionDebt(zoneId, generation, evidence);
        var landscapeEvents = FindLandscapeEvents(zoneId, generation, evidence);

        if (climateMatch.Value is null) missing.Add("climate"); else evidence.Add(GeoEvidence(climateMatch.Stratum!));
        if (landscapeMatch.Value is null) missing.Add("landscape"); else evidence.Add(GeoEvidence(landscapeMatch.Stratum!));
        if (biodiversityMatch.Value is null) missing.Add("biodiversity"); else evidence.Add(BioEvidence(biodiversityMatch.Stratum!));
        if (!hasDeepTimeEvidence) missing.Add("deep-time-lineage-evidence");

        int evidenceCount = 4 - missing.Count(x => x is "climate" or "landscape" or "biodiversity" or "deep-time-lineage-evidence");
        ReconstructionCompleteness completeness = evidenceCount switch
        {
            4 => ReconstructionCompleteness.Complete,
            3 => ReconstructionCompleteness.Substantial,
            > 0 => ReconstructionCompleteness.Partial,
            _ => ReconstructionCompleteness.None
        };

        var distinctEvidence = DistinctEvidence(evidence);
        var orderedMissing = missing.OrderBy(x => x, StringComparer.Ordinal).ToArray();
        ClimateZoneState? climate = Clone(climateMatch.Value);
        LandscapeZoneState? landscape = Clone(landscapeMatch.Value);
        RegionalBiodiversitySnapshot? biodiversity = Clone(biodiversityMatch.Value);

        string hash = HistoricalPlaybackHasher.HashCell(
            zoneId,
            generation,
            climate,
            landscape,
            biodiversity,
            lineages,
            refugia,
            debt,
            landscapeEvents,
            completeness,
            orderedMissing,
            distinctEvidence);

        return new PaleogeographicCellState(
            zoneId,
            generation,
            climate,
            landscape,
            biodiversity,
            Array.AsReadOnly(lineages),
            Array.AsReadOnly(refugia),
            Array.AsReadOnly(debt),
            Array.AsReadOnly(landscapeEvents),
            completeness,
            Array.AsReadOnly(orderedMissing),
            Array.AsReadOnly(distinctEvidence),
            HistoricalReconstructionAuthority.TestHistoryOnly,
            hash);
    }

    private (ClimateZoneState? Value, GeoclimateArchiveStratum? Stratum) FindClimate(string zoneId, int generation)
    {
        ClimateZoneState? best = null;
        GeoclimateArchiveStratum? source = null;
        foreach (var stratum in _geoclimate.Strata.Where(x => x.StartGeneration <= generation))
        foreach (var value in stratum.Climate.Where(x => StringComparer.Ordinal.Equals(x.ZoneId, zoneId) && x.Generation <= generation))
        {
            if (best is null || value.Generation > best.Generation)
            {
                best = value;
                source = stratum;
            }
        }
        return (best, source);
    }

    private (LandscapeZoneState? Value, GeoclimateArchiveStratum? Stratum) FindLandscape(string zoneId, int generation)
    {
        LandscapeZoneState? best = null;
        GeoclimateArchiveStratum? source = null;
        foreach (var stratum in _geoclimate.Strata.Where(x => x.StartGeneration <= generation))
        foreach (var value in stratum.Landscapes.Where(x => StringComparer.Ordinal.Equals(x.ZoneId, zoneId) && x.Generation <= generation))
        {
            if (best is null || value.Generation > best.Generation)
            {
                best = value;
                source = stratum;
            }
        }
        return (best, source);
    }

    private (RegionalBiodiversitySnapshot? Value, BiogeographicArchiveStratum? Stratum) FindBiodiversity(string zoneId, int generation)
    {
        RegionalBiodiversitySnapshot? best = null;
        BiogeographicArchiveStratum? source = null;
        foreach (var stratum in _biogeography.Strata.Where(x => x.StartGeneration <= generation))
        foreach (var value in stratum.Biodiversity.Where(x => StringComparer.Ordinal.Equals(x.ZoneId, zoneId) && x.Generation <= generation))
        {
            if (best is null || value.Generation > best.Generation)
            {
                best = value;
                source = stratum;
            }
        }
        return (best, source);
    }

    private HistoricalLineagePresence[] FindLineages(
        string zoneId,
        int generation,
        ICollection<HistoricalEvidenceReference> evidence,
        out bool hasDeepTimeEvidence)
    {
        var candidates = new Dictionary<string, (DeepTimeLineageState State, DeepTimeArchiveStratum Stratum)>(StringComparer.Ordinal);
        hasDeepTimeEvidence = false;

        foreach (var stratum in _deepTime.Strata.Where(x => x.StartGeneration <= generation))
        {
            if (stratum.EndGeneration <= generation)
                hasDeepTimeEvidence = true;

            foreach (var state in stratum.Lineages.Where(x => x.LastObservedGeneration <= generation))
            {
                hasDeepTimeEvidence = true;
                if (!candidates.TryGetValue(state.LineageId, out var current) || state.LastObservedGeneration > current.State.LastObservedGeneration)
                    candidates[state.LineageId] = (state, stratum);
            }
        }

        var result = new List<HistoricalLineagePresence>();
        foreach (var item in candidates.Values.OrderBy(x => x.State.LineageId, StringComparer.Ordinal))
        {
            if (!item.State.OccupiedZones.Contains(zoneId) || item.State.Population <= 0 || item.State.Status == DeepTimeLineageStatus.Extinct)
                continue;
            evidence.Add(DeepEvidence(item.Stratum));
            result.Add(new HistoricalLineagePresence(
                item.State.LineageId,
                item.State.SpeciesFamilyId,
                item.State.ParentLineageId,
                item.State.LastObservedGeneration,
                item.State.Population,
                item.State.Authority,
                item.State.Status,
                item.State.MeanDivergence));
        }

        if (result.Count == 0 && hasDeepTimeEvidence)
        {
            var latest = _deepTime.Strata.Where(x => x.EndGeneration <= generation).OrderByDescending(x => x.EndGeneration).FirstOrDefault();
            if (latest is not null) evidence.Add(DeepEvidence(latest));
        }

        return result.ToArray();
    }

    private RefugiumRecord[] FindRefugia(string zoneId, int generation, ICollection<HistoricalEvidenceReference> evidence)
    {
        var latest = new Dictionary<string, (RefugiumRecord Value, BiogeographicArchiveStratum Stratum)>(StringComparer.Ordinal);
        foreach (var stratum in _biogeography.Strata.Where(x => x.StartGeneration <= generation))
        foreach (var value in stratum.Refugia.Where(x => StringComparer.Ordinal.Equals(x.ZoneId, zoneId) && x.LastObservedGeneration <= generation))
        {
            if (!latest.TryGetValue(value.RefugiumId, out var current) || value.LastObservedGeneration > current.Value.LastObservedGeneration)
                latest[value.RefugiumId] = (value, stratum);
        }
        foreach (var x in latest.Values) evidence.Add(BioEvidence(x.Stratum));
        return latest.Values.Select(x => x.Value with { }).OrderBy(x => x.RefugiumId, StringComparer.Ordinal).ToArray();
    }

    private ExtinctionDebtState[] FindExtinctionDebt(string zoneId, int generation, ICollection<HistoricalEvidenceReference> evidence)
    {
        var latest = new Dictionary<string, (ExtinctionDebtState Value, BiogeographicArchiveStratum Stratum)>(StringComparer.Ordinal);
        foreach (var stratum in _biogeography.Strata.Where(x => x.StartGeneration <= generation))
        foreach (var value in stratum.ExtinctionDebt.Where(x => StringComparer.Ordinal.Equals(x.ZoneId, zoneId) && x.LastGeneration <= generation))
        {
            string key = value.LineageId + "|" + value.ZoneId;
            if (!latest.TryGetValue(key, out var current) || value.LastGeneration > current.Value.LastGeneration)
                latest[key] = (value, stratum);
        }
        foreach (var x in latest.Values) evidence.Add(BioEvidence(x.Stratum));
        return latest.Values.Select(x => x.Value with { }).OrderBy(x => x.LineageId, StringComparer.Ordinal).ToArray();
    }

    private LandscapeChangeEvent[] FindLandscapeEvents(string zoneId, int generation, ICollection<HistoricalEvidenceReference> evidence)
    {
        if (_settings.RecentLandscapeEventLimit == 0) return Array.Empty<LandscapeChangeEvent>();
        var matches = new List<(LandscapeChangeEvent Value, GeoclimateArchiveStratum Stratum)>();
        foreach (var stratum in _geoclimate.Strata.Where(x => x.StartGeneration <= generation))
        foreach (var value in stratum.LandscapeEvents.Where(x => StringComparer.Ordinal.Equals(x.ZoneId, zoneId) && x.Generation <= generation))
            matches.Add((value, stratum));

        var selected = matches.OrderByDescending(x => x.Value.Generation).ThenBy(x => x.Value.EventId, StringComparer.Ordinal)
            .Take(_settings.RecentLandscapeEventLimit).ToArray();
        foreach (var x in selected) evidence.Add(GeoEvidence(x.Stratum));
        return selected.Select(x => x.Value with { }).OrderBy(x => x.Generation).ThenBy(x => x.EventId, StringComparer.Ordinal).ToArray();
    }

    private void ValidateRequest(HistoricalReconstructionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Generation < 0) throw new ArgumentOutOfRangeException(nameof(request));
        if (request.ZoneIds is null || request.ZoneIds.Count == 0)
            throw new InvalidOperationException("Historical reconstruction requires at least one requested zone.");
        foreach (string zoneId in request.ZoneIds)
        {
            if (!_authorizedZones.Contains(zoneId))
                throw new InvalidOperationException($"Zone {zoneId} is outside the authorized Eastern Kingdoms playback boundary.");
        }
    }

    private void VerifySources()
    {
        if (!_settings.RequireVerifiedSourceChains) return;
        if (!_geoclimate.Verify()) throw new InvalidOperationException("Geoclimate source archive failed integrity verification.");
        if (!_biogeography.Verify()) throw new InvalidOperationException("Biogeographic source archive failed integrity verification.");
        if (!_deepTime.Verify()) throw new InvalidOperationException("Deep-time source archive failed integrity verification.");
    }

    private static HistoricalEvidenceReference GeoEvidence(GeoclimateArchiveStratum x) =>
        new(HistoricalEvidenceKind.GeoclimateStratum, $"geostrata:{x.StartGeneration:D8}-{x.EndGeneration:D8}", x.StartGeneration, x.EndGeneration, x.IntegrityHash);

    private static HistoricalEvidenceReference BioEvidence(BiogeographicArchiveStratum x) =>
        new(HistoricalEvidenceKind.BiogeographicStratum, x.StratumId, x.StartGeneration, x.EndGeneration, x.IntegrityHash);

    private static HistoricalEvidenceReference DeepEvidence(DeepTimeArchiveStratum x) =>
        new(HistoricalEvidenceKind.DeepTimeStratum, x.StratumId, x.StartGeneration, x.EndGeneration, x.IntegrityHash);

    private static HistoricalEvidenceReference[] DistinctEvidence(IEnumerable<HistoricalEvidenceReference> values) =>
        values.GroupBy(x => $"{x.Kind}|{x.StratumId}", StringComparer.Ordinal)
            .Select(x => x.First())
            .OrderBy(x => x.Kind)
            .ThenBy(x => x.StartGeneration)
            .ThenBy(x => x.StratumId, StringComparer.Ordinal)
            .ToArray();

    private static ClimateZoneState? Clone(ClimateZoneState? value) =>
        value is null ? null : value with { ActiveEpochIds = Array.AsReadOnly(value.ActiveEpochIds.ToArray()) };

    private static LandscapeZoneState? Clone(LandscapeZoneState? value) =>
        value is null ? null : value with { ActiveDisturbanceIds = Array.AsReadOnly(value.ActiveDisturbanceIds.ToArray()) };

    private static RegionalBiodiversitySnapshot? Clone(RegionalBiodiversitySnapshot? value) =>
        value is null ? null : value with { LineageIds = value.LineageIds.ToFrozenSet(StringComparer.Ordinal) };
}
