using Evermore.Genetics;

namespace EasternKingdoms.Simulation;

public sealed record CohortAlleleFrequency(
    string LocusId,
    string AlleleId,
    double Frequency);

public sealed record EvolutionCohort(
    string CohortId,
    string SpeciesId,
    string ZoneId,
    int Generation,
    long Population,
    IReadOnlyList<CohortAlleleFrequency> AlleleFrequencies);

public sealed record CohortEvolutionSettings(
    double SelectionStrength = 0.35,
    double MutationRate = 0.001,
    double DriftStrength = 0.02,
    double MigrationLossRate = 0.12,
    double HazardLossRate = 0.10);

public sealed record CohortEvolutionResult(
    EvolutionCohort Previous,
    EvolutionCohort Next,
    SpeciesEcologyOutcome Ecology,
    EvolutionaryPressureProfile Selection,
    double GrowthFactor,
    ulong Seed);

public sealed class CohortFactory
{
    public EvolutionCohort FromBeings(
        string cohortId,
        string speciesId,
        string zoneId,
        int generation,
        IEnumerable<BeingGenome> beings)
    {
        var members = beings
            .Where(x => x.Alive)
            .Where(x => StringComparer.Ordinal.Equals(x.SpeciesId, speciesId))
            .Where(x => StringComparer.Ordinal.Equals(x.HomeZoneId, zoneId))
            .OrderBy(x => x.BeingId, StringComparer.Ordinal)
            .ToArray();

        if (members.Length == 0)
            throw new InvalidOperationException($"Cannot seed cohort {cohortId} from an empty population.");

        var counts = new Dictionary<(string Locus, string Allele), int>();
        var totals = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var member in members)
        foreach (var chromosome in member.Genome.Chromosomes)
        foreach (var locus in chromosome.Loci)
        {
            Increment(locus.LocusId, locus.Alleles.ParentA);
            Increment(locus.LocusId, locus.Alleles.ParentB);
        }

        var frequencies = counts
            .OrderBy(x => x.Key.Locus, StringComparer.Ordinal)
            .ThenBy(x => x.Key.Allele, StringComparer.Ordinal)
            .Select(x => new CohortAlleleFrequency(
                x.Key.Locus,
                x.Key.Allele,
                x.Value / (double)totals[x.Key.Locus]))
            .ToArray();

        return new EvolutionCohort(
            cohortId,
            speciesId,
            zoneId,
            generation,
            members.LongLength,
            frequencies);

        void Increment(string locus, string allele)
        {
            var key = (locus, allele);
            counts[key] = counts.GetValueOrDefault(key) + 1;
            totals[locus] = totals.GetValueOrDefault(locus) + 1;
        }
    }
}

public sealed class CohortEvolutionEngine
{
    private readonly GenomeCatalog _genomeCatalog;
    private readonly SpeciesAuthoringCatalog _species;
    private readonly SpeciesEcologyCatalog _ecology;
    private readonly EvolutionaryPressureResolver _selection = new();
    private readonly ZoneFoodWebBuilder _foodWebBuilder = new();
    private readonly FoodWebResolver _foodWeb = new();

    public CohortEvolutionEngine(
        GenomeCatalog genomeCatalog,
        SpeciesAuthoringCatalog species,
        SpeciesEcologyCatalog ecology)
    {
        _genomeCatalog = genomeCatalog;
        _species = species;
        _ecology = ecology;
    }

    public CohortEvolutionResult AdvanceGeneration(
        EvolutionCohort cohort,
        ZoneEnvironment zone,
        ulong simulationSeed,
        CohortEvolutionSettings? settings = null,
        IReadOnlyDictionary<string, double>? speciesAbundance = null,
        IReadOnlyDictionary<string, double>? externalTraitPressures = null,
        double externalGrowthModifier = 1.0)
    {
        ArgumentNullException.ThrowIfNull(cohort);
        ArgumentNullException.ThrowIfNull(zone);
        settings ??= new CohortEvolutionSettings();

        if (!StringComparer.Ordinal.Equals(cohort.ZoneId, zone.ZoneId))
            throw new InvalidOperationException("Cohort and zone IDs must match.");
        if (cohort.Population <= 0)
            throw new InvalidOperationException("Cohort population must be positive.");
        if (zone.SimulationState == CellSimulationState.Locked)
            throw new InvalidOperationException("Locked cells cannot commit cohort evolution.");

        var species = _species.Get(cohort.SpeciesId);
        if (species.ActivationState != SpeciesActivationState.Active)
            throw new InvalidOperationException($"Species {cohort.SpeciesId} is not active for cohort evolution.");

        var ecologyProfile = _ecology.Get(cohort.SpeciesId);
        if (ecologyProfile.Status != SpeciesEcologyStatus.Active)
            throw new InvalidOperationException($"Species {cohort.SpeciesId} has no active ecology profile.");

        var abundance = speciesAbundance is null
            ? new Dictionary<string, double>(StringComparer.Ordinal) { [cohort.SpeciesId] = 0.25 }
            : speciesAbundance.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);

        var foodState = _foodWebBuilder.Build(zone, abundance);
        var ecologyOutcome = _foodWeb.Resolve(foodState, ecologyProfile);
        if (!ecologyOutcome.FoodWebComplete)
            throw new InvalidOperationException($"Food web for {cohort.SpeciesId} is incomplete in zone {zone.ZoneId}.");

        var selection = _selection.Resolve(zone, species);
        if (externalTraitPressures is not null)
            selection = ApplyExternalTraitPressures(selection, externalTraitPressures);

        ulong seed = StableSeed.FromText($"{simulationSeed}|cohort|{cohort.CohortId}|g{cohort.Generation + 1}|{zone.ZoneId}");
        var random = new GenomeRandom(seed);

        var nextFrequencies = EvolveFrequencies(
            cohort,
            selection,
            settings,
            random);

        double growth = CalculateGrowth(
            species,
            ecologyOutcome,
            selection,
            settings);
        growth = Math.Clamp(
            growth * Math.Clamp(externalGrowthModifier, 0.25, 1.75),
            0.25,
            1.75);

        long nextPopulation = Math.Max(1L, (long)Math.Round(cohort.Population * growth, MidpointRounding.AwayFromZero));

        var next = new EvolutionCohort(
            cohort.CohortId,
            cohort.SpeciesId,
            cohort.ZoneId,
            cohort.Generation + 1,
            nextPopulation,
            nextFrequencies);

        return new CohortEvolutionResult(
            cohort,
            next,
            ecologyOutcome,
            selection,
            growth,
            seed);
    }

    private static EvolutionaryPressureProfile ApplyExternalTraitPressures(
        EvolutionaryPressureProfile selection,
        IReadOnlyDictionary<string, double> externalTraitPressures)
    {
        var merged = selection.TraitPressures
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);

        foreach (var pressure in externalTraitPressures.OrderBy(x => x.Key, StringComparer.Ordinal))
            merged[pressure.Key] = Math.Clamp(
                merged.GetValueOrDefault(pressure.Key) + pressure.Value,
                -1.0,
                1.0);

        return selection with { TraitPressures = merged };
    }

    private IReadOnlyList<CohortAlleleFrequency> EvolveFrequencies(
        EvolutionCohort cohort,
        EvolutionaryPressureProfile selection,
        CohortEvolutionSettings settings,
        GenomeRandom random)
    {
        var output = new List<CohortAlleleFrequency>();

        foreach (var locusGroup in cohort.AlleleFrequencies
                     .GroupBy(x => x.LocusId, StringComparer.Ordinal)
                     .OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            var locus = _genomeCatalog.GetLocus(locusGroup.Key);
            var source = locus.ValidAlleles
                .ToDictionary(
                    allele => allele,
                    allele => Math.Max(0.0, locusGroup.FirstOrDefault(x => StringComparer.Ordinal.Equals(x.AlleleId, allele))?.Frequency ?? 0.0),
                    StringComparer.Ordinal);

            Normalize(source);

            var selected = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var alleleId in locus.ValidAlleles.OrderBy(x => x, StringComparer.Ordinal))
            {
                var allele = _genomeCatalog.GetAllele(alleleId);
                double selectionScore = 0.0;
                foreach (var effect in allele.Effects)
                    selectionScore += effect.Value * selection.TraitPressures.GetValueOrDefault(effect.Key);

                double driftScale = settings.DriftStrength / Math.Sqrt(Math.Max(1.0, cohort.Population));
                double drift = (random.NextDouble() - 0.5) * 2.0 * driftScale;
                double weight = source[alleleId] * Math.Exp(selectionScore * settings.SelectionStrength + drift);
                selected[alleleId] = Math.Max(0.0, weight);
            }
            Normalize(selected);

            var mutated = new Dictionary<string, double>(selected, StringComparer.Ordinal);
            double mutationRate = Math.Clamp(settings.MutationRate, 0.0, 0.25);
            if (locus.ValidAlleles.Count > 1 && mutationRate > 0.0)
            {
                var additions = locus.ValidAlleles.ToDictionary(x => x, _ => 0.0, StringComparer.Ordinal);
                foreach (var alleleId in locus.ValidAlleles)
                {
                    double moved = selected[alleleId] * mutationRate;
                    mutated[alleleId] -= moved;
                    double share = moved / (locus.ValidAlleles.Count - 1);
                    foreach (var target in locus.ValidAlleles.Where(x => !StringComparer.Ordinal.Equals(x, alleleId)))
                        additions[target] += share;
                }
                foreach (var addition in additions)
                    mutated[addition.Key] += addition.Value;
            }
            Normalize(mutated);

            foreach (var allele in mutated.OrderBy(x => x.Key, StringComparer.Ordinal))
                output.Add(new CohortAlleleFrequency(locusGroup.Key, allele.Key, allele.Value));
        }

        return output;
    }

    private static double CalculateGrowth(
        SpeciesAuthoringRecord species,
        SpeciesEcologyOutcome ecology,
        EvolutionaryPressureProfile selection,
        CohortEvolutionSettings settings)
    {
        double births = species.Lifecycle.BaseFecundity
            * ecology.ReproductiveModifier
            * selection.ReproductiveModifier
            * ecology.ViabilityModifier;

        double migrationLoss = ecology.MigrationPressure * Math.Clamp(settings.MigrationLossRate, 0.0, 1.0);
        double hazardLoss = (1.0 - ecology.ViabilityModifier) * Math.Clamp(settings.HazardLossRate, 0.0, 1.0);
        return Math.Clamp(1.0 + births - migrationLoss - hazardLoss, 0.50, 1.50);
    }

    private static void Normalize(IDictionary<string, double> values)
    {
        double total = values.Values.Sum();
        if (total <= 0.0)
        {
            double equal = values.Count == 0 ? 0.0 : 1.0 / values.Count;
            foreach (var key in values.Keys.ToArray()) values[key] = equal;
            return;
        }
        foreach (var key in values.Keys.ToArray()) values[key] = values[key] / total;
    }
}
