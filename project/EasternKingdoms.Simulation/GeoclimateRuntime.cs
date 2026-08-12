namespace EasternKingdoms.Simulation;

public sealed record GeoclimateRuntimeSnapshot(
    int LastGeneration,
    IReadOnlyList<LandscapeZoneState> Landscapes,
    IReadOnlyList<EcologicalRecoveryRecord> Recovery,
    GeoclimateArchiveSnapshot Archive);

public sealed class DeepTimeGeoclimateRuntime
{
    private readonly GeoclimateSettings _settings;
    private readonly HashSet<string> _authorizedZones;
    private readonly PopulationRouteCatalog _routes;
    private readonly GeoclimateScenarioCatalog _scenario;
    private readonly Dictionary<string, ClimateZoneBaseline> _climateBaselines;
    private readonly Dictionary<string, LandscapeBaseline> _landscapeBaselines;
    private readonly PaleoclimateResolver _climate = new();
    private readonly LandscapeDynamicsLedger _landscapes = new();
    private readonly LandscapeChangeTracker _landscapeChanges;
    private readonly HabitatEnvelopeTracker _envelopes;
    private readonly EcologicalRecoveryTracker _recovery;
    private readonly ClimateEvolutionPressureResolver _pressure = new();
    private readonly HabitatSuitabilityResolver _biogeographicHabitats = new();
    private readonly HabitatCorridorResolver _biogeographicCorridors;
    private readonly GeoclimateArchive _archive = new();
    private readonly List<ClimateZoneState> _climateHistory = new();
    private readonly List<LandscapeZoneState> _landscapeHistory = new();
    private readonly List<LandscapeChangeEvent> _landscapeEventHistory = new();
    private readonly List<HabitatEnvelopeMigrationEvent> _envelopeHistory = new();
    private readonly List<EcologicalRecoveryRecord> _recoveryHistory = new();
    private int _lastGeneration = -1;
    private int _nextStratumStart;

    public DeepTimeGeoclimateRuntime(
        IEnumerable<string> authorizedZoneIds,
        PopulationRouteCatalog routes,
        IEnumerable<ClimateZoneBaseline> climateBaselines,
        IEnumerable<LandscapeBaseline> landscapeBaselines,
        GeoclimateScenarioCatalog scenario,
        GeoclimateSettings? settings = null)
    {
        _settings = settings ?? new GeoclimateSettings();
        _settings.Validate();
        _authorizedZones = authorizedZoneIds.Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet(StringComparer.Ordinal);
        if (_authorizedZones.Count == 0)
            throw new InvalidOperationException("Geoclimate runtime requires authorized Eastern Kingdoms zones.");

        _routes = routes ?? throw new ArgumentNullException(nameof(routes));
        _scenario = scenario ?? throw new ArgumentNullException(nameof(scenario));
        _scenario.ValidateBoundary(_authorizedZones);
        var boundary = new PopulationBoundaryGate(_authorizedZones);
        foreach (var route in _routes.Routes)
            boundary.Validate(route);

        _climateBaselines = climateBaselines.ToDictionary(x => x.ZoneId, x => x, StringComparer.Ordinal);
        _landscapeBaselines = landscapeBaselines.ToDictionary(x => x.ZoneId, x => x, StringComparer.Ordinal);
        ValidateBaselineCoverage(_climateBaselines.Keys, "climate");
        ValidateBaselineCoverage(_landscapeBaselines.Keys, "landscape");

        _landscapeChanges = new LandscapeChangeTracker(_settings.LandscapeChangeEventThreshold);
        _envelopes = new HabitatEnvelopeTracker(_settings);
        _recovery = new EcologicalRecoveryTracker(_settings);
        _biogeographicCorridors = new HabitatCorridorResolver(new BiogeographySettings());
        _nextStratumStart = 0;
    }

    public GeoclimateArchive Archive => _archive;

    public GeoclimateGenerationResult Advance(GeoclimateBatchObservation batch)
    {
        ArgumentNullException.ThrowIfNull(batch);
        if (batch.Generation <= _lastGeneration)
            throw new InvalidOperationException("Geoclimate generations must advance monotonically.");

        var zones = batch.Zones.ToDictionary(x => x.ZoneId, x => x, StringComparer.Ordinal);
        if (zones.Count != batch.Zones.Count)
            throw new InvalidOperationException("Geoclimate batch contains duplicate zone IDs.");
        ValidateBaselineCoverage(zones.Keys, "batch");

        var climateStates = _authorizedZones.OrderBy(x => x, StringComparer.Ordinal)
            .Select(zoneId => _climate.Resolve(_climateBaselines[zoneId], batch.Generation, _scenario.ClimateEpochs))
            .ToArray();
        var climateByZone = climateStates.ToDictionary(x => x.ZoneId, x => x, StringComparer.Ordinal);

        var landscapeStates = _authorizedZones.OrderBy(x => x, StringComparer.Ordinal)
            .Select(zoneId => _landscapes.Advance(
                _landscapeBaselines[zoneId],
                climateByZone[zoneId],
                zones[zoneId],
                _scenario.DisturbanceEpochs,
                batch.Generation))
            .ToArray();
        var landscapeByZone = landscapeStates.ToDictionary(x => x.ZoneId, x => x, StringComparer.Ordinal);
        var landscapeEvents = landscapeStates.SelectMany(x => _landscapeChanges.Observe(x))
            .OrderBy(x => x.EventId, StringComparer.Ordinal)
            .ToArray();

        var habitatStates = zones.Values
            .Select(x => _biogeographicHabitats.Resolve(x, batch.Generation))
            .ToDictionary(x => x.ZoneId, x => x, StringComparer.Ordinal);
        var corridorStates = _biogeographicCorridors.Resolve(_routes, habitatStates, batch.Generation);

        var envelopeStates = new List<HabitatEnvelopeZoneState>();
        var envelopeEvents = new List<HabitatEnvelopeMigrationEvent>();
        foreach (var envelope in _scenario.HabitatEnvelopes.OrderBy(x => x.EnvelopeId, StringComparer.Ordinal))
        {
            var result = _envelopes.Evaluate(
                envelope,
                batch.Generation,
                climateByZone,
                landscapeByZone,
                corridorStates);
            envelopeStates.AddRange(result.States);
            envelopeEvents.AddRange(result.Events);
        }

        var pressures = climateStates
            .Select(x => _pressure.Resolve(x, landscapeByZone[x.ZoneId]))
            .OrderBy(x => x.ZoneId, StringComparer.Ordinal)
            .ToArray();
        var integrated = pressures
            .Select(x => ClimatePressureIntegrator.Apply(zones[x.ZoneId], x))
            .OrderBy(x => x.ZoneId, StringComparer.Ordinal)
            .ToArray();

        var recovery = climateStates
            .Select(x => _recovery.Observe(x, landscapeByZone[x.ZoneId]))
            .OrderBy(x => x.ZoneId, StringComparer.Ordinal)
            .ToArray();

        _climateHistory.AddRange(climateStates);
        _landscapeHistory.AddRange(landscapeStates);
        _landscapeEventHistory.AddRange(landscapeEvents);
        _envelopeHistory.AddRange(envelopeEvents);
        _recoveryHistory.AddRange(recovery);
        _lastGeneration = batch.Generation;

        if (batch.Generation - _nextStratumStart + 1 >= _settings.StratumSpanGenerations)
        {
            CommitStratum(_nextStratumStart, batch.Generation);
            _nextStratumStart = batch.Generation + 1;
        }

        return new GeoclimateGenerationResult(
            batch.Generation,
            climateStates,
            landscapeStates,
            landscapeEvents,
            envelopeStates.OrderBy(x => x.EnvelopeId, StringComparer.Ordinal).ThenBy(x => x.ZoneId, StringComparer.Ordinal).ToArray(),
            envelopeEvents.OrderBy(x => x.EventId, StringComparer.Ordinal).ToArray(),
            pressures,
            recovery,
            integrated);
    }

    public GeoclimateArchiveStratum CommitOpenStratum()
    {
        if (_lastGeneration < _nextStratumStart)
            throw new InvalidOperationException("No uncommitted geoclimate generations exist.");
        var stratum = CommitStratum(_nextStratumStart, _lastGeneration);
        _nextStratumStart = _lastGeneration + 1;
        return stratum;
    }

    public GeoclimateRuntimeSnapshot Snapshot() =>
        new(
            _lastGeneration,
            _landscapes.States.ToArray(),
            _recovery.Records.ToArray(),
            _archive.Snapshot());

    private GeoclimateArchiveStratum CommitStratum(int start, int end) =>
        _archive.Commit(
            start,
            end,
            _climateHistory.Where(x => x.Generation >= start && x.Generation <= end),
            _landscapeHistory.Where(x => x.Generation >= start && x.Generation <= end),
            _landscapeEventHistory.Where(x => x.Generation >= start && x.Generation <= end),
            _envelopeHistory.Where(x => x.Generation >= start && x.Generation <= end),
            _recoveryHistory.Where(x => x.LastGeneration >= start && x.LastGeneration <= end));

    private void ValidateBaselineCoverage(IEnumerable<string> zoneIds, string label)
    {
        var ids = zoneIds.ToHashSet(StringComparer.Ordinal);
        foreach (string required in _authorizedZones)
        {
            if (!ids.Contains(required))
                throw new InvalidOperationException($"Geoclimate {label} is missing authorized zone {required}.");
        }
        foreach (string supplied in ids)
        {
            if (!_authorizedZones.Contains(supplied))
                throw new InvalidOperationException($"Geoclimate {label} contains unauthorized zone {supplied}.");
        }
    }
}
