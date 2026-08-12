namespace Omega.Recursive;

public sealed record OmegaTrait(string Name, float Strength);

public sealed record OmegaEntityProfile(string Name, IReadOnlyList<OmegaTrait> Traits)
{
    public float FitnessScore()
    {
        if (Traits.Count == 0)
            throw new InvalidOperationException("An Omega entity must have at least one trait.");

        return Traits.Average(trait => trait.Strength);
    }
}

public sealed record OmegaSimulationOptions(
    ulong Seed,
    int Generations,
    int PopulationSize = 24,
    int SelectionCount = 8,
    float MutationRate = 0.25f);

public enum OmegaRunDisposition
{
    Completed,
    Contained
}

public sealed record OmegaContainmentDecision(bool IsContained, string? Reason)
{
    public static OmegaContainmentDecision Continue { get; } = new(false, null);

    public static OmegaContainmentDecision Contain(string reason) => new(true, reason);
}

public sealed record OmegaTraitTotal(string Name, float Strength);

public sealed record OmegaEntitySummary(string Name, float Fitness, string GlyphSignature);

public sealed record OmegaGenerationSnapshot(
    int Generation,
    float AverageFitness,
    string DominantTrait,
    IReadOnlyList<OmegaTraitTotal> TraitTotals,
    IReadOnlyList<OmegaEntitySummary> TopEntities,
    bool IsArchiveCheckpoint);

public sealed record OmegaRunResult(
    OmegaRunDisposition Disposition,
    string? ContainmentReason,
    ulong Seed,
    int RequestedGenerations,
    int ProcessedGenerations,
    IReadOnlyList<OmegaEntityProfile> FinalPopulation,
    IReadOnlyList<float> IntensityHistory,
    IReadOnlyList<string> ResonanceLog,
    IReadOnlyList<OmegaGenerationSnapshot> Snapshots);
