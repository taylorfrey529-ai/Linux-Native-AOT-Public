namespace EasternKingdoms.Simulation;

public static class HistoricalVisualizationUsage
{
    public static HistoricalReplaySerializationResult BuildReplay(
        HistoricalVisualizationRuntime runtime,
        int startGeneration,
        int endGeneration,
        int step)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        var frames = runtime.Historical.Playback.Play(
            startGeneration,
            endGeneration,
            step,
            AzerothHistoricalPlaybackBootstrap.AuthorizedPlaybackZones());
        return runtime.Serializer.Serialize(frames);
    }
}
