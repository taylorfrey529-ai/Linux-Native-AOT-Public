using Evermore.Genetics;

namespace EasternKingdoms.Simulation;

public sealed record AzerothBiogeographicEvolutionRuntime(
    AzerothDeepTimeEvolutionRuntime DeepTimeEvolution,
    DeepTimeBiogeographyRuntime Biogeography);

public static class AzerothBiogeographicEvolutionBootstrap
{
    public static AzerothBiogeographicEvolutionRuntime Create(
        GenomeCatalog genomeCatalog,
        LineageDivergenceSettings? lineageSettings = null,
        DeepTimeSettings? deepTimeSettings = null,
        BiogeographySettings? biogeographySettings = null)
    {
        ArgumentNullException.ThrowIfNull(genomeCatalog);

        var zones = AzerothEvolutionBootstrap.CreateNorthernScarCorridor();
        var routes = NorthernScarPopulationRoutes.Create();

        return new AzerothBiogeographicEvolutionRuntime(
            AzerothDeepTimeEvolutionBootstrap.Create(genomeCatalog, lineageSettings, deepTimeSettings),
            new DeepTimeBiogeographyRuntime(
                zones.Select(x => x.ZoneId),
                routes,
                biogeographySettings));
    }
}
