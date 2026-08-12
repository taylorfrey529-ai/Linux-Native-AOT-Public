namespace EasternKingdoms.Simulation;

public sealed record DeepTimePopulationDescriptor(
    string EvolutionaryNodeId,
    long Population,
    IReadOnlySet<string> OccupiedZones,
    IReadOnlyDictionary<string, double> NicheVector,
    double MeanDivergence);

public sealed class DeepTimeObservationAdapter
{
    public DeepTimeBatchObservation FromPhaseXVII(
        EvolutionaryTreeSnapshot tree,
        IEnumerable<DeepTimePopulationDescriptor> populations,
        int generation)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(populations);
        if (generation < 0)
            throw new ArgumentOutOfRangeException(nameof(generation));

        var nodes = tree.Nodes.ToDictionary(x => x.NodeId, StringComparer.Ordinal);
        var observations = new List<DeepTimeLineageObservation>();

        foreach (var population in populations.OrderBy(x => x.EvolutionaryNodeId, StringComparer.Ordinal))
        {
            if (!nodes.TryGetValue(population.EvolutionaryNodeId, out var node))
                throw new InvalidOperationException($"Deep-time population references unknown evolutionary node {population.EvolutionaryNodeId}.");
            if (population.Population < 0)
                throw new ArgumentOutOfRangeException(nameof(populations), "Deep-time populations cannot be negative.");
            if (generation < node.FirstGeneration)
                throw new InvalidOperationException($"Node {node.NodeId} cannot be observed before its first generation.");

            observations.Add(new DeepTimeLineageObservation(
                node.NodeId,
                node.ParentNodeId,
                ResolveSpeciesFamily(node, nodes),
                generation,
                population.Population,
                population.OccupiedZones,
                population.NicheVector,
                DeepTimeLineageLedger.FromTreeNode(node.Status),
                population.MeanDivergence));
        }

        return new DeepTimeBatchObservation(generation, observations);
    }

    private static string ResolveSpeciesFamily(
        EvolutionaryTreeNode node,
        IReadOnlyDictionary<string, EvolutionaryTreeNode> nodes)
    {
        var current = node;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (current.ParentNodeId is not null && nodes.TryGetValue(current.ParentNodeId, out var parent))
        {
            if (!seen.Add(current.NodeId))
                throw new InvalidOperationException("Cycle detected while resolving deep-time species family.");
            current = parent;
        }

        return current.SpeciesOrLineageId;
    }
}
