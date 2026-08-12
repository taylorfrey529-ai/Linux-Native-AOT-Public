namespace EasternKingdoms.Simulation;

public sealed record BiogeographicRuntimeSnapshot(
    int LastGeneration,
    IReadOnlyList<LineageRangeState> Ranges,
    IReadOnlyList<RefugiumRecord> Refugia,
    IReadOnlyList<RecolonizationWaveEvent> RecolonizationWaves,
    IReadOnlyList<ExtinctionDebtState> ExtinctionDebt,
    IReadOnlyList<RegionalBiodiversitySnapshot> Biodiversity,
    IReadOnlyList<CommunityTurnoverMetrics> Turnover,
    BiogeographicArchiveSnapshot Archive);

public sealed class DeepTimeBiogeographyRuntime
{
    private readonly BiogeographySettings _settings;
    private readonly HashSet<string> _authorizedZones;
    private readonly PopulationRouteCatalog _routes;
    private readonly PopulationBoundaryGate _boundary;
    private readonly HabitatSuitabilityResolver _habitats = new();
    private readonly HabitatCorridorResolver _corridors;
    private readonly RangeConnectivityAnalyzer _connectivity = new();
    private readonly BiogeographicRangeLedger _ranges;
    private readonly RefugiumTracker _refugia;
    private readonly RecolonizationWaveDetector _recolonization = new();
    private readonly ExtinctionDebtLedger _debt;
    private readonly RegionalBiodiversityAnalyzer _biodiversity = new();
    private readonly CommunityTurnoverAnalyzer _turnover = new();
    private readonly BiogeographicArchive _archive = new();
    private readonly List<RangeShiftEvent> _rangeEvents = new();
    private readonly List<RecolonizationWaveEvent> _recolonizations = new();
    private readonly List<RegionalBiodiversitySnapshot> _biodiversityHistory = new();
    private readonly List<CommunityTurnoverMetrics> _turnoverHistory = new();
    private readonly Dictionary<string, RegionalBiodiversitySnapshot> _lastBiodiversity = new(StringComparer.Ordinal);
    private int _lastGeneration = -1;
    private int _nextStratumStart;

    public DeepTimeBiogeographyRuntime(
        IEnumerable<string> authorizedZoneIds,
        PopulationRouteCatalog routes,
        BiogeographySettings? settings = null)
    {
        _settings = settings ?? new BiogeographySettings();
        _settings.Validate();
        _authorizedZones = authorizedZoneIds.Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet(StringComparer.Ordinal);
        if (_authorizedZones.Count == 0)
            throw new InvalidOperationException("Biogeography runtime requires authorized Eastern Kingdoms zones.");

        _routes = routes ?? throw new ArgumentNullException(nameof(routes));
        _boundary = new PopulationBoundaryGate(_authorizedZones);
        foreach (var route in _routes.Routes)
            _boundary.Validate(route);

        _corridors = new HabitatCorridorResolver(_settings);
        _ranges = new BiogeographicRangeLedger(_authorizedZones);
        _refugia = new RefugiumTracker(_settings);
        _debt = new ExtinctionDebtLedger(_settings);
        _nextStratumStart = 0;
    }

    public BiogeographicArchive Archive => _archive;

    public BiogeographicGenerationResult Advance(BiogeographicBatchObservation batch)
    {
        ArgumentNullException.ThrowIfNull(batch);
        if (batch.Generation <= _lastGeneration)
            throw new InvalidOperationException("Biogeographic generations must advance monotonically.");
        if (batch.Lineages.Any(x => x.Generation != batch.Generation))
            throw new InvalidOperationException("Every lineage observation must use the batch generation.");

        var zones = batch.Zones.ToDictionary(x => x.ZoneId, StringComparer.Ordinal);
        if (zones.Count != batch.Zones.Count)
            throw new InvalidOperationException("Biogeographic batch contains duplicate zone IDs.");
        foreach (string zoneId in zones.Keys)
        {
            if (!_authorizedZones.Contains(zoneId))
                throw new InvalidOperationException($"Zone {zoneId} is outside the authorized Eastern Kingdoms biogeographic boundary.");
        }
        foreach (string required in _authorizedZones)
        {
            if (!zones.ContainsKey(required))
                throw new InvalidOperationException($"Biogeographic batch is missing authorized zone {required}.");
        }

        var habitatStates = zones.Values
            .Select(x => _habitats.Resolve(x, batch.Generation))
            .OrderBy(x => x.ZoneId, StringComparer.Ordinal)
            .ToArray();
        var habitatByZone = habitatStates.ToDictionary(x => x.ZoneId, StringComparer.Ordinal);
        var corridorStates = _corridors.Resolve(_routes, habitatByZone, batch.Generation);

        var generationRangeEvents = new List<RangeShiftEvent>();
        var generationRefugia = new List<RefugiumRecord>();
        var generationRecolonizations = new List<RecolonizationWaveEvent>();
        var generationDebt = new List<ExtinctionDebtState>();

        foreach (var lineage in batch.Lineages.OrderBy(x => x.LineageId, StringComparer.Ordinal))
        {
            foreach (string zoneId in lineage.OccupiedZones)
            {
                if (!_authorizedZones.Contains(zoneId))
                    throw new InvalidOperationException($"Lineage {lineage.LineageId} crossed into unauthorized zone {zoneId}.");
            }

            int components = _connectivity.CountComponents(lineage.OccupiedZones, corridorStates);
            var transition = _ranges.Observe(lineage, components);
            generationRangeEvents.AddRange(transition.Events);
            generationRefugia.AddRange(_refugia.Observe(transition.Current, habitatByZone));

            var waves = _recolonization.Detect(transition, corridorStates, _routes, _refugia);
            generationRecolonizations.AddRange(waves);

            foreach (string zoneId in _authorizedZones.OrderBy(x => x, StringComparer.Ordinal))
            {
                bool occupiedNow = transition.Current.OccupiedZones.Contains(zoneId);
                bool occupiedBefore = transition.Previous?.OccupiedZones.Contains(zoneId) == true;
                if (!occupiedNow && !occupiedBefore)
                    continue;

                var debt = _debt.Observe(
                    lineage.LineageId,
                    zoneId,
                    batch.Generation,
                    occupiedNow,
                    habitatByZone[zoneId].Suitability);
                if (debt is not null)
                    generationDebt.Add(debt);
            }
        }

        var biodiversity = new List<RegionalBiodiversitySnapshot>();
        var turnover = new List<CommunityTurnoverMetrics>();
        foreach (string zoneId in _authorizedZones.OrderBy(x => x, StringComparer.Ordinal))
        {
            var snapshot = _biodiversity.Analyze(zoneId, batch.Generation, batch.Lineages);
            biodiversity.Add(snapshot);
            if (_lastBiodiversity.TryGetValue(zoneId, out var previous))
                turnover.Add(_turnover.Compare(previous, snapshot));
            _lastBiodiversity[zoneId] = snapshot;
        }

        _rangeEvents.AddRange(generationRangeEvents);
        _recolonizations.AddRange(generationRecolonizations);
        _biodiversityHistory.AddRange(biodiversity);
        _turnoverHistory.AddRange(turnover);
        _lastGeneration = batch.Generation;

        if (batch.Generation - _nextStratumStart + 1 >= _settings.StratumSpanGenerations)
        {
            CommitStratum(_nextStratumStart, batch.Generation);
            _nextStratumStart = batch.Generation + 1;
        }

        return new BiogeographicGenerationResult(
            batch.Generation,
            _ranges.States.ToArray(),
            habitatStates,
            corridorStates,
            generationRangeEvents.OrderBy(x => x.EventId, StringComparer.Ordinal).ToArray(),
            generationRefugia.OrderBy(x => x.RefugiumId, StringComparer.Ordinal).ToArray(),
            generationRecolonizations.OrderBy(x => x.EventId, StringComparer.Ordinal).ToArray(),
            generationDebt.OrderBy(x => x.LineageId, StringComparer.Ordinal).ThenBy(x => x.ZoneId, StringComparer.Ordinal).ToArray(),
            biodiversity,
            turnover);
    }

    public BiogeographicArchiveStratum CommitOpenStratum()
    {
        if (_lastGeneration < _nextStratumStart)
            throw new InvalidOperationException("No uncommitted biogeographic generations exist.");
        var stratum = CommitStratum(_nextStratumStart, _lastGeneration);
        _nextStratumStart = _lastGeneration + 1;
        return stratum;
    }

    public BiogeographicRuntimeSnapshot Snapshot() =>
        new(
            _lastGeneration,
            _ranges.States.ToArray(),
            _refugia.Records.ToArray(),
            _recolonizations.OrderBy(x => x.Generation).ThenBy(x => x.EventId, StringComparer.Ordinal).ToArray(),
            _debt.States.ToArray(),
            _biodiversityHistory.OrderBy(x => x.Generation).ThenBy(x => x.ZoneId, StringComparer.Ordinal).ToArray(),
            _turnoverHistory.OrderBy(x => x.ToGeneration).ThenBy(x => x.ZoneId, StringComparer.Ordinal).ToArray(),
            _archive.Snapshot());

    private BiogeographicArchiveStratum CommitStratum(int start, int end) =>
        _archive.Commit(
            start,
            end,
            _rangeEvents.Where(x => x.Generation >= start && x.Generation <= end),
            _refugia.Records.Where(x => x.LastObservedGeneration >= start && x.LastObservedGeneration <= end),
            _recolonizations.Where(x => x.Generation >= start && x.Generation <= end),
            _debt.States.Where(x => x.LastGeneration >= start && x.LastGeneration <= end),
            _biodiversityHistory.Where(x => x.Generation >= start && x.Generation <= end),
            _turnoverHistory.Where(x => x.ToGeneration >= start && x.ToGeneration <= end));
}

public static class DeepTimeBiogeographyAdapter
{
    public static BiogeographicBatchObservation CreateBatch(
        int generation,
        IEnumerable<DeepTimeLineageState> lineages,
        IEnumerable<ZoneEnvironment> zones)
    {
        var observations = lineages
            .OrderBy(x => x.LineageId, StringComparer.Ordinal)
            .Select(x => new DeepTimeLineageObservation(
                x.LineageId,
                x.ParentLineageId,
                x.SpeciesFamilyId,
                generation,
                x.Population,
                x.OccupiedZones.ToHashSet(StringComparer.Ordinal),
                x.NicheVector.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal),
                x.Authority,
                x.MeanDivergence))
            .ToArray();

        return new BiogeographicBatchObservation(
            generation,
            observations,
            zones.OrderBy(x => x.ZoneId, StringComparer.Ordinal).ToArray());
    }
}
