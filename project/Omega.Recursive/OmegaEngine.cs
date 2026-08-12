using System.Globalization;

namespace Omega.Recursive;

public sealed class OmegaEngine
{
    public const string Definition = "Omega is the great dark beyond our world.";

    private static readonly string[] Names = ["Nyra", "Xal", "Vorun", "Kelth", "Aeth", "Morv", "Thane", "Veyra"];
    private static readonly string[] TraitNames =
    [
        "Entropy",
        "Dominion",
        "Whispers",
        "VoidPulse",
        "Echo",
        "Ascension",
        "Decay",
        "Flux"
    ];

    private readonly SelectionEngine _selection;

    public OmegaEngine(SelectionEngine? selection = null)
    {
        _selection = selection ?? new SelectionEngine();
    }

    public OmegaRunResult Run(OmegaSimulationOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        Validate(options);

        var random = new OmegaRandom(options.Seed);
        var containment = new OmegaContainmentPolicy();
        var intensityHistory = new List<float>();
        var resonanceLog = new List<string>();
        var snapshots = new List<OmegaGenerationSnapshot>(options.Generations);
        var previousTraitTotals = new Dictionary<string, float>(StringComparer.Ordinal);
        List<OmegaEntityProfile> population = Enumerable
            .Range(0, options.PopulationSize)
            .Select(_ => CreateRandomEntity(random))
            .ToList();

        for (int generation = 0; generation < options.Generations; generation++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Dictionary<string, float> traitTotals = ComputeTraitTotals(population);
            float averageFitness = population.Average(entity => entity.FitnessScore());
            AddBounded(intensityHistory, averageFitness, 64);
            DetectSurgingTraits(traitTotals, previousTraitTotals, generation, resonanceLog);

            OmegaGenerationSnapshot snapshot = CreateSnapshot(
                generation,
                averageFitness,
                traitTotals,
                population);
            snapshots.Add(snapshot);

            OmegaContainmentDecision decision = containment.Evaluate(traitTotals, population, generation);
            if (decision.IsContained)
            {
                return new OmegaRunResult(
                    OmegaRunDisposition.Contained,
                    decision.Reason,
                    options.Seed,
                    options.Generations,
                    generation + 1,
                    population.ToArray(),
                    intensityHistory.ToArray(),
                    resonanceLog.ToArray(),
                    snapshots.ToArray());
            }

            previousTraitTotals = new Dictionary<string, float>(traitTotals, StringComparer.Ordinal);
            population = BreedNextGeneration(population, options, random);
        }

        return new OmegaRunResult(
            OmegaRunDisposition.Completed,
            null,
            options.Seed,
            options.Generations,
            options.Generations,
            population.ToArray(),
            intensityHistory.ToArray(),
            resonanceLog.ToArray(),
            snapshots.ToArray());
    }

    private List<OmegaEntityProfile> BreedNextGeneration(
        IReadOnlyList<OmegaEntityProfile> population,
        OmegaSimulationOptions options,
        OmegaRandom random)
    {
        IReadOnlyList<OmegaEntityProfile> selected = _selection.SelectTop(population, options.SelectionCount);
        var next = new List<OmegaEntityProfile>(options.PopulationSize);

        while (next.Count < options.PopulationSize)
        {
            OmegaEntityProfile first = selected[random.NextInt32(selected.Count)];
            OmegaEntityProfile second = selected[random.NextInt32(selected.Count)];
            OmegaEntityProfile child = _selection.Combine(first, second);
            next.Add(_selection.Mutate(child, options.MutationRate, random));
        }

        return next;
    }

    private static OmegaEntityProfile CreateRandomEntity(OmegaRandom random)
    {
        var traits = new OmegaTrait[4];
        for (int index = 0; index < traits.Length; index++)
        {
            traits[index] = new OmegaTrait(
                TraitNames[random.NextInt32(TraitNames.Length)],
                random.NextSingle());
        }

        return new OmegaEntityProfile(Names[random.NextInt32(Names.Length)], traits);
    }

    private static Dictionary<string, float> ComputeTraitTotals(IEnumerable<OmegaEntityProfile> population)
    {
        var totals = new Dictionary<string, float>(StringComparer.Ordinal);
        foreach (OmegaTrait trait in population.SelectMany(entity => entity.Traits))
            totals[trait.Name] = totals.GetValueOrDefault(trait.Name) + trait.Strength;
        return totals;
    }

    private static void DetectSurgingTraits(
        IReadOnlyDictionary<string, float> current,
        IReadOnlyDictionary<string, float> previous,
        int generation,
        List<string> resonanceLog)
    {
        foreach ((string name, float strength) in current.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (!previous.TryGetValue(name, out float oldStrength))
                continue;

            float delta = strength - oldStrength;
            if (delta <= 4f)
                continue;

            AddBounded(
                resonanceLog,
                $"[GEN {generation}] SURGE DETECTED :: {name} +{delta.ToString("F1", CultureInfo.InvariantCulture)}",
                100);
        }
    }

    private static OmegaGenerationSnapshot CreateSnapshot(
        int generation,
        float averageFitness,
        IReadOnlyDictionary<string, float> traitTotals,
        IReadOnlyList<OmegaEntityProfile> population)
    {
        OmegaTraitTotal[] orderedTotals = traitTotals
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new OmegaTraitTotal(pair.Key, pair.Value))
            .ToArray();
        OmegaEntitySummary[] topEntities = population
            .OrderByDescending(entity => entity.FitnessScore())
            .ThenBy(entity => entity.Name, StringComparer.Ordinal)
            .Take(5)
            .Select(entity => new OmegaEntitySummary(
                entity.Name,
                entity.FitnessScore(),
                string.Join(' ', entity.Traits.Take(3).Select(trait => OmegaGlyphs.EncodeTraitName(trait.Name)))))
            .ToArray();

        return new OmegaGenerationSnapshot(
            generation,
            averageFitness,
            orderedTotals[0].Name,
            orderedTotals,
            topEntities,
            generation % 5 == 0);
    }

    private static void AddBounded<T>(List<T> values, T value, int capacity)
    {
        values.Add(value);
        if (values.Count > capacity)
            values.RemoveAt(0);
    }

    private static void Validate(OmegaSimulationOptions options)
    {
        if (options.Generations is < 1 or > 10_000)
            throw new ArgumentOutOfRangeException(nameof(options), "Generations must be between 1 and 10,000.");
        if (options.PopulationSize is < 2 or > 1_024)
            throw new ArgumentOutOfRangeException(nameof(options), "Population size must be between 2 and 1,024.");
        if (options.SelectionCount < 1 || options.SelectionCount > options.PopulationSize)
            throw new ArgumentOutOfRangeException(nameof(options), "Selection count must fit the population.");
        if (options.MutationRate is < 0f or > 1f)
            throw new ArgumentOutOfRangeException(nameof(options), "Mutation rate must be between zero and one.");
    }
}
