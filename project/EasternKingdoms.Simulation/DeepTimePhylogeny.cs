namespace EasternKingdoms.Simulation;

public sealed record PhylogenyNode(
    string LineageId,
    string? ParentLineageId,
    int FirstGeneration,
    int LastGeneration,
    DeepTimeLineageStatus Status,
    DeepTimeLineageAuthority Authority,
    IReadOnlyList<string> Children);

public sealed record PhylogenySnapshot(
    IReadOnlyList<PhylogenyNode> Nodes,
    IReadOnlyList<string> RootLineageIds);

public sealed class DeepTimePhylogenyReconstructor
{
    public PhylogenySnapshot Reconstruct(IEnumerable<DeepTimeLineageState> states)
    {
        var all = states.OrderBy(x => x.LineageId, StringComparer.Ordinal).ToArray();
        var ids = all.Select(x => x.LineageId).ToHashSet(StringComparer.Ordinal);

        var nodes = all.Select(state => new PhylogenyNode(
            state.LineageId,
            state.ParentLineageId,
            state.FirstObservedGeneration,
            state.LastObservedGeneration,
            state.Status,
            state.Authority,
            all.Where(x => StringComparer.Ordinal.Equals(x.ParentLineageId, state.LineageId))
                .Select(x => x.LineageId)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray()))
            .ToArray();

        var roots = all
            .Where(x => x.ParentLineageId is null || !ids.Contains(x.ParentLineageId))
            .Select(x => x.LineageId)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        return new PhylogenySnapshot(nodes, roots);
    }
}

public sealed record DeepTimeGovernanceAssessment(
    bool MayEnterExperimentalDeepTime,
    bool MayBeUsedForCanonAuthoring,
    string Reason);

public sealed class DeepTimeGovernanceGate
{
    public DeepTimeGovernanceAssessment Assess(DeepTimeLineageState lineage)
    {
        ArgumentNullException.ThrowIfNull(lineage);

        return lineage.Authority switch
        {
            DeepTimeLineageAuthority.ExistingAuthorizedSpecies =>
                new(true, true, "Existing authored species may participate according to its existing continuity status."),
            DeepTimeLineageAuthority.CanonAuthoringAuthorized =>
                new(true, true, "Phase XVII review authorized canon authoring, but deep-time results remain Test History until separately promoted."),
            DeepTimeLineageAuthority.ExperimentalSimulation =>
                new(true, false, "Experimental lineage may participate in simulation branches only."),
            DeepTimeLineageAuthority.CandidateTestHistory =>
                new(false, false, "Unreviewed candidates remain analytical Test History."),
            _ =>
                new(false, false, "Rejected lineage cannot enter authoritative or experimental deep-time simulation.")
        };
    }
}
