using Evermore.Genetics;

namespace EasternKingdoms.Simulation;

public static class AzerothHistoricalVisualizationBootstrap
{
    public static HistoricalVisualizationRuntime Create(
        GenomeCatalog genomeCatalog,
        LineageDivergenceSettings? lineageSettings = null,
        DeepTimeSettings? deepTimeSettings = null,
        BiogeographySettings? biogeographySettings = null,
        GeoclimateSettings? geoclimateSettings = null,
        GeoclimateScenarioCatalog? scenario = null,
        HistoricalPlaybackSettings? playbackSettings = null,
        HistoricalVisualizationSettings? visualizationSettings = null)
    {
        ArgumentNullException.ThrowIfNull(genomeCatalog);
        var settings = visualizationSettings ?? new HistoricalVisualizationSettings();
        settings.Validate();

        var historical = AzerothHistoricalPlaybackBootstrap.Create(
            genomeCatalog,
            lineageSettings,
            deepTimeSettings,
            biogeographySettings,
            geoclimateSettings,
            scenario,
            playbackSettings);

        var timeline = new HistoricalTimelineIndexer();
        return new HistoricalVisualizationRuntime(
            historical,
            timeline,
            new HistoricalReplaySerializer(timeline, settings),
            new HistoricalOverlayBuilder(settings),
            new EasternKingdomsGlobeStateAdapter(AzerothHistoricalPlaybackBootstrap.AuthorizedPlaybackZones()));
    }
}
