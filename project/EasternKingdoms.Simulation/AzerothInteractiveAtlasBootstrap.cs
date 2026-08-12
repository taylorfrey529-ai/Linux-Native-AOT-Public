using Evermore.Genetics;

namespace EasternKingdoms.Simulation;

public static class AzerothInteractiveAtlasBootstrap
{
    public static HistoricalAtlasServices Create(
        GenomeCatalog genomeCatalog,
        LineageDivergenceSettings? lineageSettings = null,
        DeepTimeSettings? deepTimeSettings = null,
        BiogeographySettings? biogeographySettings = null,
        GeoclimateSettings? geoclimateSettings = null,
        GeoclimateScenarioCatalog? scenario = null,
        HistoricalPlaybackSettings? playbackSettings = null,
        HistoricalVisualizationSettings? visualizationSettings = null,
        HistoricalAtlasSettings? atlasSettings = null)
    {
        ArgumentNullException.ThrowIfNull(genomeCatalog);
        var settings = atlasSettings ?? new HistoricalAtlasSettings();
        settings.Validate();
        var visualization = AzerothHistoricalVisualizationBootstrap.Create(
            genomeCatalog,
            lineageSettings,
            deepTimeSettings,
            biogeographySettings,
            geoclimateSettings,
            scenario,
            playbackSettings,
            visualizationSettings);
        var zones = AzerothHistoricalPlaybackBootstrap.AuthorizedPlaybackZones();
        var scenes = new HistoricalAtlasSceneBuilder(zones, settings);
        var comparisons = new HistoricalAtlasComparisonBuilder(scenes, visualization.Overlays);
        return new HistoricalAtlasServices(
            visualization,
            scenes,
            comparisons,
            new HistoricalAtlasCameraSerializer(zones, settings),
            new HistoricalAtlasSceneSerializer(scenes),
            zones,
            settings);
    }
}
