using System.Security.Cryptography;
using System.Text;

namespace EasternKingdoms.Simulation;

public enum EvolutionaryTreeNodeStatus
{
    ActiveSpecies,
    Candidate,
    ExperimentalLineage,
    CanonAuthoringAuthorized,
    Rejected,
    Extinct
}

public sealed record EvolutionaryTreeNode(
    string NodeId,
    string SpeciesOrLineageId,
    string? ParentNodeId,
    int FirstGeneration,
    int LastGeneration,
    EvolutionaryTreeNodeStatus Status,
    bool CandidateOnly);

public sealed record EvolutionaryTreeSnapshot(
    IReadOnlyList<EvolutionaryTreeNode> Nodes,
    string IntegrityHash);

public sealed class EvolutionaryTree
{
    private readonly Dictionary<string, EvolutionaryTreeNode> _nodes = new(StringComparer.Ordinal);

    public IReadOnlyCollection<EvolutionaryTreeNode> Nodes =>
        _nodes.Values.OrderBy(x => x.NodeId, StringComparer.Ordinal).ToArray();

    public EvolutionaryTreeNode EnsureSpeciesRoot(string speciesId, int generation = 0)
    {
        string nodeId = $"species:{speciesId}";
        if (_nodes.TryGetValue(nodeId, out var existing))
            return existing;

        var node = new EvolutionaryTreeNode(
            nodeId,
            speciesId,
            ParentNodeId: null,
            generation,
            generation,
            EvolutionaryTreeNodeStatus.ActiveSpecies,
            CandidateOnly: false);

        _nodes.Add(nodeId, node);
        return node;
    }

    public EvolutionaryTreeNode AddOrUpdateCandidate(SpeciationCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        var parent = EnsureSpeciesRoot(candidate.ParentSpeciesId, 0);
        string nodeId = $"candidate:{candidate.CandidateId}";

        EvolutionaryTreeNodeStatus status = EvolutionaryTreeNodeStatus.Candidate;
        bool candidateOnly = true;
        if (_nodes.TryGetValue(nodeId, out var existing) &&
            (existing.Status is EvolutionaryTreeNodeStatus.ExperimentalLineage or
                EvolutionaryTreeNodeStatus.CanonAuthoringAuthorized or
                EvolutionaryTreeNodeStatus.Rejected))
        {
            status = existing.Status;
            candidateOnly = existing.CandidateOnly;
        }

        var node = new EvolutionaryTreeNode(
            nodeId,
            candidate.ProposedLineageId,
            parent.NodeId,
            candidate.FirstObservedGeneration,
            candidate.LastObservedGeneration,
            status,
            candidateOnly);

        _nodes[nodeId] = node;
        return node;
    }

    public void ApplyCandidateDisposition(
        string candidateId,
        SpeciationActivationDisposition disposition,
        int generation)
    {
        string nodeId = $"candidate:{candidateId}";
        if (!_nodes.TryGetValue(nodeId, out var current))
            throw new KeyNotFoundException($"Candidate node {candidateId} does not exist in the evolutionary tree.");

        EvolutionaryTreeNodeStatus status = disposition switch
        {
            SpeciationActivationDisposition.Rejected => EvolutionaryTreeNodeStatus.Rejected,
            SpeciationActivationDisposition.SimulationAuthorized => EvolutionaryTreeNodeStatus.ExperimentalLineage,
            SpeciationActivationDisposition.CanonAuthoringAuthorized => EvolutionaryTreeNodeStatus.CanonAuthoringAuthorized,
            _ => current.Status
        };

        _nodes[nodeId] = current with
        {
            LastGeneration = Math.Max(current.LastGeneration, generation),
            Status = status,
            CandidateOnly = status is not EvolutionaryTreeNodeStatus.CanonAuthoringAuthorized
        };
    }

    public void MarkExtinct(string nodeId, int generation)
    {
        if (!_nodes.TryGetValue(nodeId, out var current))
            throw new KeyNotFoundException($"Unknown evolutionary tree node {nodeId}.");

        _nodes[nodeId] = current with
        {
            LastGeneration = Math.Max(current.LastGeneration, generation),
            Status = EvolutionaryTreeNodeStatus.Extinct
        };
    }

    public EvolutionaryTreeSnapshot Snapshot()
    {
        var nodes = Nodes.ToArray();
        string canonical = string.Join(
            "\n",
            nodes.Select(x => string.Join(
                "|",
                x.NodeId,
                x.SpeciesOrLineageId,
                x.ParentNodeId ?? string.Empty,
                x.FirstGeneration,
                x.LastGeneration,
                x.Status,
                x.CandidateOnly)));

        string hash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))
            .ToLowerInvariant();

        return new EvolutionaryTreeSnapshot(nodes, hash);
    }
}
