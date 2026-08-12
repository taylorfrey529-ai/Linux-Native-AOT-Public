namespace Omega.Recursive;

public sealed class OmegaContainmentPolicy
{
    private static readonly string[] ForbiddenTerms =
    [
        "consume",
        "subjugate",
        "eradicate",
        "ascend beyond control",
        "perfect dominion",
        "eternal rule"
    ];

    private float _previousIntensity;

    public OmegaContainmentDecision Evaluate(
        IReadOnlyDictionary<string, float> traitTotals,
        IReadOnlyList<OmegaEntityProfile> population,
        int generation)
    {
        ArgumentNullException.ThrowIfNull(traitTotals);
        ArgumentNullException.ThrowIfNull(population);
        if (population.Count == 0)
            throw new ArgumentException("Population cannot be empty.", nameof(population));
        if (generation < 0)
            throw new ArgumentOutOfRangeException(nameof(generation));

        float averageFitness = population.Average(entity => entity.FitnessScore());
        float escalationIndex =
            (ValueOrZero(traitTotals, "Dominion") * 0.35f) +
            (ValueOrZero(traitTotals, "Entropy") * 0.20f) +
            (ValueOrZero(traitTotals, "Ascension") * 0.25f) +
            (ValueOrZero(traitTotals, "VoidPulse") * 0.20f);

        if (escalationIndex > 18f)
            return OmegaContainmentDecision.Contain("Trait convergence exceeded safe threshold.");

        int uniqueTraitCount = population
            .SelectMany(entity => entity.Traits)
            .Select(trait => trait.Name)
            .Distinct(StringComparer.Ordinal)
            .Count();

        if (uniqueTraitCount <= 2 && generation > 10)
            return OmegaContainmentDecision.Contain("Adaptive diversity collapse detected.");

        if (averageFitness > 0.95f && generation > 25)
            return OmegaContainmentDecision.Contain("Recursive optimization runaway detected.");

        foreach (OmegaTrait trait in population.SelectMany(entity => entity.Traits))
        {
            if (ForbiddenTerms.Any(term => trait.Name.Contains(term, StringComparison.OrdinalIgnoreCase)))
                return OmegaContainmentDecision.Contain($"Forbidden escalation semantic: {trait.Name}");
        }

        if (_previousIntensity > 0.8f && averageFitness - _previousIntensity > 0.25f)
            return OmegaContainmentDecision.Contain("Mutation acceleration spike detected.");

        _previousIntensity = averageFitness;
        return OmegaContainmentDecision.Continue;
    }

    private static float ValueOrZero(IReadOnlyDictionary<string, float> values, string name) =>
        values.TryGetValue(name, out float value) ? value : 0f;
}
