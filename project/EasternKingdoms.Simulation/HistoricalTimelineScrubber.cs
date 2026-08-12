namespace EasternKingdoms.Simulation;

public sealed class HistoricalTimelineScrubber
{
    private readonly HistoricalPlaybackFrame[] _frames;
    private readonly Dictionary<int, HistoricalPlaybackFrame> _byFrameIndex;
    private readonly Dictionary<int, HistoricalPlaybackFrame> _byGeneration;

    public HistoricalTimelineScrubber(
        IEnumerable<HistoricalPlaybackFrame> frames,
        HistoricalTimelineIndexer? indexer = null)
    {
        ArgumentNullException.ThrowIfNull(frames);
        _frames = frames.OrderBy(x => x.World.Generation).ThenBy(x => x.FrameIndex).ToArray();
        if (_frames.Length == 0)
            throw new InvalidOperationException("Historical atlas scrubbing requires at least one frame.");
        if (_frames.Any(x => !HistoricalPlaybackVerifier.Verify(x.World)))
            throw new InvalidOperationException("Historical atlas scrubbing requires verified historical frames.");
        if (_frames.Select(x => x.FrameIndex).Distinct().Count() != _frames.Length)
            throw new InvalidOperationException("Historical atlas frame indices must be unique.");
        if (_frames.Select(x => x.World.Generation).Distinct().Count() != _frames.Length)
            throw new InvalidOperationException("Historical atlas generations must be unique.");

        _byFrameIndex = _frames.ToDictionary(x => x.FrameIndex);
        _byGeneration = _frames.ToDictionary(x => x.World.Generation);
        var timelineIndexer = indexer ?? new HistoricalTimelineIndexer();
        Timeline = timelineIndexer.Build(_frames.OrderBy(x => x.FrameIndex));
        if (!timelineIndexer.Verify(Timeline))
            throw new InvalidOperationException("Historical atlas timeline integrity verification failed.");
    }

    public HistoricalTimelineIndex Timeline { get; }

    public IReadOnlyList<HistoricalPlaybackFrame> Frames => Array.AsReadOnly(_frames);

    public HistoricalPlaybackFrame ResolveFrameIndex(int frameIndex)
    {
        if (!_byFrameIndex.TryGetValue(frameIndex, out var frame))
            throw new KeyNotFoundException($"Historical atlas frame index {frameIndex} is not recorded.");
        return frame;
    }

    public HistoricalPlaybackFrame ResolveGeneration(int generation, HistoricalAtlasScrubPolicy policy)
    {
        if (generation < 0) throw new ArgumentOutOfRangeException(nameof(generation));
        if (_byGeneration.TryGetValue(generation, out var exact)) return exact;

        return policy switch
        {
            HistoricalAtlasScrubPolicy.ExactRecorded => throw new KeyNotFoundException($"Generation {generation} is not a recorded historical frame."),
            HistoricalAtlasScrubPolicy.PreviousOrExact => _frames.Where(x => x.World.Generation < generation)
                .OrderByDescending(x => x.World.Generation).FirstOrDefault()
                ?? throw new KeyNotFoundException($"No recorded generation exists at or before {generation}."),
            HistoricalAtlasScrubPolicy.NextOrExact => _frames.Where(x => x.World.Generation > generation)
                .OrderBy(x => x.World.Generation).FirstOrDefault()
                ?? throw new KeyNotFoundException($"No recorded generation exists at or after {generation}."),
            HistoricalAtlasScrubPolicy.NearestRecorded => _frames
                .OrderBy(x => Math.Abs((long)x.World.Generation - generation))
                .ThenBy(x => x.World.Generation)
                .First(),
            _ => throw new ArgumentOutOfRangeException(nameof(policy))
        };
    }
}
