namespace EasternKingdoms.Simulation;

public sealed class HistoricalAtlasSession
{
    private readonly HistoricalTimelineScrubber _scrubber;
    private readonly HistoricalAtlasSceneBuilder _scenes;
    private readonly HistoricalAtlasComparisonBuilder _comparisons;
    private readonly IReadOnlySet<string> _authorizedZones;
    private readonly HistoricalAtlasSettings _settings;
    private readonly Dictionary<HistoricalAtlasLayer, HistoricalAtlasLayerState> _layers;
    private HistoricalPlaybackFrame _current;
    private HistoricalAtlasCameraState _camera;
    private HistoricalAtlasComparisonMode _comparisonMode;
    private int? _comparisonGeneration;
    private string? _selectedZoneId;
    private string? _selectedLineageId;

    public HistoricalAtlasSession(
        IEnumerable<HistoricalPlaybackFrame> frames,
        IEnumerable<string> authorizedZoneIds,
        HistoricalAtlasSceneBuilder scenes,
        HistoricalAtlasComparisonBuilder comparisons,
        HistoricalAtlasSettings? settings = null,
        HistoricalAtlasCameraState? camera = null,
        IEnumerable<HistoricalAtlasLayerState>? layers = null)
    {
        _scrubber = new HistoricalTimelineScrubber(frames);
        _scenes = scenes ?? throw new ArgumentNullException(nameof(scenes));
        _comparisons = comparisons ?? throw new ArgumentNullException(nameof(comparisons));
        ArgumentNullException.ThrowIfNull(authorizedZoneIds);
        _authorizedZones = authorizedZoneIds.ToHashSet(StringComparer.Ordinal);
        if (_authorizedZones.Count == 0)
            throw new InvalidOperationException("Historical atlas session requires authorized Eastern Kingdoms zones.");
        _settings = settings ?? new HistoricalAtlasSettings();
        _settings.Validate();
        _camera = camera ?? HistoricalAtlasCameraState.DefaultGlobe();
        _camera.Validate(_settings, _authorizedZones);
        var initialLayers = (layers ?? HistoricalAtlasSceneBuilder.DefaultLayers()).ToArray();
        if (initialLayers.Select(x => x.Layer).Distinct().Count() != initialLayers.Length)
            throw new InvalidOperationException("Historical atlas session layer states cannot contain duplicates.");
        foreach (var layer in initialLayers) layer.Validate();
        _layers = initialLayers.ToDictionary(x => x.Layer);
        _current = _scrubber.Frames.OrderBy(x => x.World.Generation).First();
        _comparisonMode = HistoricalAtlasComparisonMode.Single;
    }

    public HistoricalTimelineIndex Timeline => _scrubber.Timeline;

    public HistoricalAtlasSessionState State => SnapshotState();

    public HistoricalAtlasSessionState Seek(int generation, HistoricalAtlasScrubPolicy? policy = null)
    {
        _current = _scrubber.ResolveGeneration(generation, policy ?? _settings.DefaultScrubPolicy);
        if (_comparisonGeneration.HasValue && _comparisonGeneration.Value < _current.World.Generation)
        {
            _comparisonGeneration = null;
            _comparisonMode = HistoricalAtlasComparisonMode.Single;
        }
        return SnapshotState();
    }

    public HistoricalAtlasSessionState SeekFrame(int frameIndex)
    {
        _current = _scrubber.ResolveFrameIndex(frameIndex);
        if (_comparisonGeneration.HasValue && _comparisonGeneration.Value < _current.World.Generation)
        {
            _comparisonGeneration = null;
            _comparisonMode = HistoricalAtlasComparisonMode.Single;
        }
        return SnapshotState();
    }

    public HistoricalAtlasSessionState SetLayer(HistoricalAtlasLayer layer, bool enabled, double? opacity = null)
    {
        var current = _layers.TryGetValue(layer, out var existing)
            ? existing
            : new HistoricalAtlasLayerState(layer, false, 1.0);
        var next = current with { Enabled = enabled, Opacity = opacity ?? current.Opacity };
        next.Validate();
        _layers[layer] = next;
        return SnapshotState();
    }

    public HistoricalAtlasSessionState SetCamera(HistoricalAtlasCameraState camera)
    {
        ArgumentNullException.ThrowIfNull(camera);
        camera.Validate(_settings, _authorizedZones);
        _camera = camera;
        return SnapshotState();
    }

    public HistoricalAtlasSessionState SelectZone(string? zoneId)
    {
        if (zoneId is not null && !_authorizedZones.Contains(zoneId))
            throw new InvalidOperationException("Historical atlas selection is outside the authorized Eastern Kingdoms set.");
        _selectedZoneId = zoneId;
        return SnapshotState();
    }

    public HistoricalAtlasSessionState SelectLineage(string? lineageId)
    {
        if (lineageId is not null && string.IsNullOrWhiteSpace(lineageId))
            throw new InvalidOperationException("Historical atlas lineage selection cannot be blank.");
        _selectedLineageId = lineageId;
        return SnapshotState();
    }

    public HistoricalAtlasSessionState SetComparison(int? generation, HistoricalAtlasComparisonMode mode)
    {
        if (mode == HistoricalAtlasComparisonMode.Single)
        {
            _comparisonMode = HistoricalAtlasComparisonMode.Single;
            _comparisonGeneration = null;
            return SnapshotState();
        }
        if (!generation.HasValue)
            throw new InvalidOperationException("Historical atlas comparison mode requires a comparison generation.");
        var target = _scrubber.ResolveGeneration(generation.Value, _settings.DefaultScrubPolicy);
        if (target.World.Generation < _current.World.Generation)
            throw new InvalidOperationException("Historical atlas comparison generation cannot precede the current generation.");
        _comparisonMode = mode;
        _comparisonGeneration = target.World.Generation;
        return SnapshotState();
    }

    public HistoricalAtlasScenePayload BuildScene() =>
        _scenes.Build(_current, _camera, LayerSnapshot());

    public HistoricalAtlasComparisonPayload BuildComparison(IEnumerable<HistoricalOverlayMetric>? metrics = null)
    {
        if (_comparisonMode == HistoricalAtlasComparisonMode.Single || !_comparisonGeneration.HasValue)
            throw new InvalidOperationException("Historical atlas session is not in comparison mode.");
        var target = _scrubber.ResolveGeneration(_comparisonGeneration.Value, HistoricalAtlasScrubPolicy.ExactRecorded);
        return _comparisons.Build(_current, target, _comparisonMode, _camera, LayerSnapshot(), metrics);
    }

    public bool Verify(HistoricalAtlasSessionState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Authority != HistoricalAtlasAuthority.TestHistoryOnly) return false;
        if (state.FrameIndex < 0 || state.Generation < 0) return false;
        try { state.Camera.Validate(_settings, _authorizedZones); }
        catch (InvalidOperationException) { return false; }
        if (state.SelectedZoneId is not null && !_authorizedZones.Contains(state.SelectedZoneId)) return false;
        if (state.SelectedLineageId is not null && string.IsNullOrWhiteSpace(state.SelectedLineageId)) return false;
        if (state.ComparisonMode == HistoricalAtlasComparisonMode.Single && state.ComparisonGeneration.HasValue) return false;
        if (state.ComparisonMode != HistoricalAtlasComparisonMode.Single && !state.ComparisonGeneration.HasValue) return false;
        if (state.ComparisonGeneration.HasValue && state.ComparisonGeneration.Value < state.Generation) return false;
        if (state.Layers.Select(x => x.Layer).Distinct().Count() != state.Layers.Count) return false;
        if (state.Layers.Any(x => x.Opacity is < 0.0 or > 1.0 || double.IsNaN(x.Opacity) || double.IsInfinity(x.Opacity))) return false;
        string expected = HistoricalAtlasHasher.HashSession(
            state.FrameIndex,
            state.Generation,
            state.ComparisonMode,
            state.ComparisonGeneration,
            state.Camera,
            state.Layers,
            state.SelectedZoneId,
            state.SelectedLineageId);
        return StringComparer.Ordinal.Equals(expected, state.IntegrityHash);
    }

    private HistoricalAtlasSessionState SnapshotState()
    {
        var layers = LayerSnapshot();
        string hash = HistoricalAtlasHasher.HashSession(
            _current.FrameIndex,
            _current.World.Generation,
            _comparisonMode,
            _comparisonGeneration,
            _camera,
            layers,
            _selectedZoneId,
            _selectedLineageId);
        return new HistoricalAtlasSessionState(
            _current.FrameIndex,
            _current.World.Generation,
            _comparisonMode,
            _comparisonGeneration,
            _camera,
            Array.AsReadOnly(layers),
            _selectedZoneId,
            _selectedLineageId,
            HistoricalAtlasAuthority.TestHistoryOnly,
            hash);
    }

    private HistoricalAtlasLayerState[] LayerSnapshot() =>
        _layers.Values.OrderBy(x => x.Layer).ToArray();
}
