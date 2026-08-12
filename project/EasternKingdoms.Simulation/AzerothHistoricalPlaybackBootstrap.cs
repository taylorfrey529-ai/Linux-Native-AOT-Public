using Evermore.Genetics;

namespace EasternKingdoms.Simulation;

public sealed record AzerothHistoricalPlaybackRuntime(
    AzerothGeoclimateEvolutionRuntime Evolution,
    DeepTimeWorldStatePlayback Playback);

public static class AzerothHistoricalPlaybackBootstrap
{
    public static AzerothHistoricalPlaybackRuntime Create(
        GenomeCatalog genomeCatalog,
        LineageDivergenceSettings? lineageSettings = null,
        DeepTimeSettings? deepTimeSettings = null,
        BiogeographySettings? biogeographySettings = null,
        GeoclimateSettings? geoclimateSettings = null,
        GeoclimateScenarioCatalog? scenario = null,
        HistoricalPlaybackSettings? playbackSettings = null)
    {
        ArgumentNullException.ThrowIfNull(genomeCatalog);
        var evolution = AzerothGeoclimateEvolutionBootstrap.Create(
            genomeCatalog,
            lineageSettings,
            deepTimeSettings,
            biogeographySettings,
            geoclimateSettings,
            scenario);

        var zones = AzerothEvolutionBootstrap.CreateNorthernScarCorridor();
        var reconstructor = new HistoricalWorldStateReconstructor(
            zones.Select(x => x.ZoneId),
            evolution.Geoclimate.Archive,
            evolution.BiogeographicEvolution.Biogeography.Archive,
            evolution.BiogeographicEvolution.DeepTimeEvolution.DeepTime.Archive,
            playbackSettings);

        return new AzerothHistoricalPlaybackRuntime(
            evolution,
            new DeepTimeWorldStatePlayback(reconstructor));
    }

    public static IReadOnlySet<string> AuthorizedPlaybackZones() =>
        AzerothEvolutionBootstrap.CreateNorthernScarCorridor()
            .Select(x => x.ZoneId)
            .ToHashSet(StringComparer.Ordinal);
}
