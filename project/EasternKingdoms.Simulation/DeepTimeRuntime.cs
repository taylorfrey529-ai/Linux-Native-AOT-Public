namespace EasternKingdoms.Simulation;

public sealed record DeepTimeBatchObservation(
    int Generation,
    IReadOnlyList<DeepTimeLineageObservation> Lineages);

public sealed record DeepTimeRuntimeSnapshot(
    int LastGeneration,
    IReadOnlyList<DeepTimeLineageState> Lineages,
    IReadOnlyList<AdaptiveRadiationEvent> RadiationEvents,
    IReadOnlyList<LineageExtinctionEvent> ExtinctionEvents,
    IReadOnlyList<EcologicalReplacementEvent> ReplacementEvents,
    IReadOnlyList<CladeRecord> Clades,
    DeepTimeArchiveSnapshot Archive);

public sealed class DeepTimeMacroevolutionRuntime
{
    private readonly DeepTimeSettings _settings;
    private readonly DeepTimeLineageLedger _lineages;
    private readonly AdaptiveRadiationDetector _radiation;
    private readonly EcologicalReplacementDetector _replacement = new();
    private readonly CladeBuilder _clades = new();
    private readonly DeepTimeArchive _archive = new();
    private readonly List<AdaptiveRadiationEvent> _radiations = new();
    private readonly List<LineageExtinctionEvent> _extinctions = new();
    private readonly List<EcologicalReplacementEvent> _replacements = new();
    private readonly HashSet<string> _recordedExtinctions = new(StringComparer.Ordinal);
    private readonly HashSet<string> _recordedRadiations = new(StringComparer.Ordinal);
    private readonly HashSet<string> _recordedReplacements = new(StringComparer.Ordinal);
    private int _lastGeneration = -1;
    private int _nextStratumStart;

    public DeepTimeMacroevolutionRuntime(DeepTimeSettings? settings = null)
    {
        _settings = settings ?? new DeepTimeSettings();
        _settings.Validate();
        _lineages = new DeepTimeLineageLedger(_settings);
        _radiation = new AdaptiveRadiationDetector(_settings);
        _nextStratumStart = 0;
    }

    public DeepTimeArchive Archive => _archive;
    public IReadOnlyCollection<DeepTimeLineageState> Lineages => _lineages.States;

    public DeepTimeGenerationResult Advance(DeepTimeBatchObservation batch)
    {
        ArgumentNullException.ThrowIfNull(batch);
        if (batch.Generation <= _lastGeneration)
            throw new InvalidOperationException("Deep-time generation batches must advance monotonically.");
        if (batch.Lineages.Any(x => x.Generation != batch.Generation))
            throw new InvalidOperationException("Every lineage observation in a deep-time batch must use the batch generation.");

        foreach (var observation in batch.Lineages.OrderBy(x => x.LineageId, StringComparer.Ordinal))
            _lineages.Observe(observation);

        var states = _lineages.States.ToArray();
        DetectExtinctions(states, batch.Generation);
        DetectRadiations(states, batch.Generation);
        DetectReplacements(states, batch.Generation);

        var clades = _clades.Build(states);
        _lastGeneration = batch.Generation;

        if (batch.Generation - _nextStratumStart + 1 >= _settings.StratumSpanGenerations)
        {
            CommitStratum(_nextStratumStart, batch.Generation, states, clades);
            _nextStratumStart = batch.Generation + 1;
        }

        return new DeepTimeGenerationResult(
            batch.Generation,
            states,
            _radiations.Where(x => x.DetectionGeneration == batch.Generation).OrderBy(x => x.EventId, StringComparer.Ordinal).ToArray(),
            _extinctions.Where(x => x.Generation == batch.Generation).OrderBy(x => x.EventId, StringComparer.Ordinal).ToArray(),
            _replacements.Where(x => x.Generation == batch.Generation).OrderBy(x => x.EventId, StringComparer.Ordinal).ToArray(),
            clades);
    }

    public DeepTimeArchiveStratum CommitOpenStratum()
    {
        if (_lastGeneration < _nextStratumStart)
            throw new InvalidOperationException("No uncommitted deep-time generations exist.");

        var states = _lineages.States.ToArray();
        var clades = _clades.Build(states);
        var stratum = CommitStratum(_nextStratumStart, _lastGeneration, states, clades);
        _nextStratumStart = _lastGeneration + 1;
        return stratum;
    }

    public DeepTimeRuntimeSnapshot Snapshot() =>
        new(
            _lastGeneration,
            _lineages.States.ToArray(),
            _radiations.OrderBy(x => x.DetectionGeneration).ThenBy(x => x.EventId, StringComparer.Ordinal).ToArray(),
            _extinctions.OrderBy(x => x.Generation).ThenBy(x => x.EventId, StringComparer.Ordinal).ToArray(),
            _replacements.OrderBy(x => x.Generation).ThenBy(x => x.EventId, StringComparer.Ordinal).ToArray(),
            _clades.Build(_lineages.States),
            _archive.Snapshot());

    private void DetectExtinctions(IReadOnlyList<DeepTimeLineageState> states, int generation)
    {
        foreach (var state in states.Where(x => x.Status == DeepTimeLineageStatus.Extinct))
        {
            string id = $"extinction:{state.LineageId}:g{generation}";
            if (!_recordedExtinctions.Add(state.LineageId))
                continue;

            _extinctions.Add(new LineageExtinctionEvent(
                id,
                state.LineageId,
                generation,
                state.ConsecutiveZeroPopulationGenerations,
                state.Authority,
                state.OccupiedZones));
        }
    }

    private void DetectRadiations(IReadOnlyList<DeepTimeLineageState> states, int generation)
    {
        foreach (var root in states.OrderBy(x => x.LineageId, StringComparer.Ordinal))
        {
            var detected = _radiation.Detect(root.LineageId, states, generation);
            if (detected is null)
                continue;

            string stableKey = $"{detected.RootLineageId}|{string.Join(',', detected.DescendantLineageIds)}";
            if (_recordedRadiations.Add(stableKey))
                _radiations.Add(detected);
        }
    }

    private void DetectReplacements(IReadOnlyList<DeepTimeLineageState> states, int generation)
    {
        foreach (var extinct in states.Where(x => x.Status == DeepTimeLineageStatus.Extinct).OrderBy(x => x.LineageId, StringComparer.Ordinal))
        {
            var detected = _replacement.Detect(extinct, states, generation);
            if (detected is null)
                continue;

            string stableKey = $"{detected.ExtinctLineageId}|{detected.ReplacementLineageId}";
            if (_recordedReplacements.Add(stableKey))
                _replacements.Add(detected);
        }
    }

    private DeepTimeArchiveStratum CommitStratum(
        int start,
        int end,
        IReadOnlyList<DeepTimeLineageState> states,
        IReadOnlyList<CladeRecord> clades) =>
        _archive.Commit(
            start,
            end,
            states,
            _radiations.Where(x => x.DetectionGeneration >= start && x.DetectionGeneration <= end),
            _extinctions.Where(x => x.Generation >= start && x.Generation <= end),
            _replacements.Where(x => x.Generation >= start && x.Generation <= end),
            clades);
}
