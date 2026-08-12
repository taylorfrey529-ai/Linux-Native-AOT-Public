using Evermore.Genetics;

namespace EasternKingdoms.Simulation;

public sealed record AzerothDeepTimeEvolutionRuntime(
    AzerothLineageEvolutionRuntime LineageEvolution,
    DeepTimeMacroevolutionRuntime DeepTime,
    DeepTimePhylogenyReconstructor Phylogeny,
    DeepTimeGovernanceGate Governance);

public static class AzerothDeepTimeEvolutionBootstrap
{
    public static AzerothDeepTimeEvolutionRuntime Create(
        GenomeCatalog genomeCatalog,
        LineageDivergenceSettings? lineageSettings = null,
        DeepTimeSettings? deepTimeSettings = null)
    {
        ArgumentNullException.ThrowIfNull(genomeCatalog);

        return new AzerothDeepTimeEvolutionRuntime(
            AzerothLineageEvolutionBootstrap.Create(genomeCatalog, lineageSettings),
            new DeepTimeMacroevolutionRuntime(deepTimeSettings),
            new DeepTimePhylogenyReconstructor(),
            new DeepTimeGovernanceGate());
    }
}
