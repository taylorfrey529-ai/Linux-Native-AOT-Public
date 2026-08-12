namespace Omega.Recursive;

public sealed class SelectionEngine
{
    private const int TraitCapacity = 4;
    private const int ParentNameSegmentLength = 31;

    public IReadOnlyList<OmegaEntityProfile> SelectTop(
        IReadOnlyList<OmegaEntityProfile> population,
        int count)
    {
        ArgumentNullException.ThrowIfNull(population);
        if (count <= 0 || count > population.Count)
            throw new ArgumentOutOfRangeException(nameof(count), "Selection count must fit the population.");

        return population
            .OrderByDescending(entity => entity.FitnessScore())
            .ThenBy(entity => entity.Name, StringComparer.Ordinal)
            .Take(count)
            .ToArray();
    }

    public OmegaEntityProfile Combine(OmegaEntityProfile first, OmegaEntityProfile second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        OmegaTrait[] traits = first.Traits
            .Concat(second.Traits)
            .Take(TraitCapacity)
            .Select(trait => new OmegaTrait(trait.Name, trait.Strength))
            .ToArray();

        if (traits.Length == 0)
            throw new InvalidOperationException("At least one parent trait is required.");

        return new OmegaEntityProfile(CombineNames(first.Name, second.Name), traits);
    }

    public OmegaEntityProfile Mutate(OmegaEntityProfile entity, float rate, OmegaRandom random)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(random);
        if (rate is < 0f or > 1f)
            throw new ArgumentOutOfRangeException(nameof(rate), "Mutation rate must be between zero and one.");

        OmegaTrait[] traits = entity.Traits.Select(trait =>
        {
            if (!random.Chance(rate))
                return trait;

            float delta = (random.NextSingle() - 0.5f) * 0.3f;
            return trait with { Strength = Math.Clamp(trait.Strength + delta, 0f, 1f) };
        }).ToArray();

        return entity with { Traits = traits };
    }

    private static string CombineNames(string first, string second)
    {
        string left = first.Length <= ParentNameSegmentLength ? first : first[..ParentNameSegmentLength];
        string right = second.Length <= ParentNameSegmentLength ? second : second[..ParentNameSegmentLength];
        return $"{left}-{right}";
    }
}
