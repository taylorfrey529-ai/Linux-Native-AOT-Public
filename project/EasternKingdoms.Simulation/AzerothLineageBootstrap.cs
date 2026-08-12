using Evermore.Genetics;

namespace EasternKingdoms.Simulation;

public sealed record AzerothLineageEvolutionRuntime(
    AzerothCommunityEvolutionRuntime Community,
    LineageDivergenceOrchestrator Lineages);

public static class AzerothLineageEvolutionBootstrap
{
    public static AzerothLineageEvolutionRuntime Create(
        GenomeCatalog genomeCatalog,
        LineageDivergenceSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(genomeCatalog);

        return new AzerothLineageEvolutionRuntime(
            AzerothCommunityEvolutionBootstrap.Create(genomeCatalog),
            new LineageDivergenceOrchestrator(settings));
    }
}
