namespace EasternKingdoms.Simulation;

public static class HistoricalAtlasUsage
{
    public static HistoricalAtlasSession CreateDefaultSession(
        HistoricalAtlasServices services,
        IEnumerable<HistoricalPlaybackFrame> frames)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(frames);
        return services.CreateSession(
            frames,
            HistoricalAtlasCameraState.DefaultGlobe(),
            HistoricalAtlasSceneBuilder.DefaultLayers());
    }
}
