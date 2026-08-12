using Evermore.Genetics;

namespace EasternKingdoms.Simulation;

public sealed record CohortTraitProfile(
    string SpeciesId,
    IReadOnlyDictionary<string, double> TraitPotentials);

public sealed class CohortTraitPotentialResolver
{
    private readonly GenomeCatalog _catalog;

    public CohortTraitPotentialResolver(GenomeCatalog catalog)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    public CohortTraitProfile Resolve(EvolutionCohort cohort)
    {
        ArgumentNullException.ThrowIfNull(cohort);
        var totals = new Dictionary<string, double>(StringComparer.Ordinal);
        var contributingLoci = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        foreach (var frequency in cohort.AlleleFrequencies)
        {
            var allele = _catalog.GetAllele(frequency.AlleleId);
            foreach (var effect in allele.Effects)
            {
                totals[effect.Key] = totals.GetValueOrDefault(effect.Key)
                    + Math.Max(0.0, frequency.Frequency) * effect.Value;

                if (!contributingLoci.TryGetValue(effect.Key, out var loci))
                {
                    loci = new HashSet<string>(StringComparer.Ordinal);
                    contributingLoci.Add(effect.Key, loci);
                }
                loci.Add(frequency.LocusId);
            }
        }

        var resolved = totals.ToDictionary(
            x => x.Key,
            x => Math.Clamp(
                x.Value / Math.Max(1, contributingLoci[x.Key].Count),
                -1.0,
                1.0),
            StringComparer.Ordinal);
        return new CohortTraitProfile(cohort.SpeciesId, resolved);
    }

    public CohortTraitProfile ResolvePopulation(IEnumerable<EvolutionCohort> cohorts, string speciesId)
    {
        var relevant = cohorts
            .Where(x => x.Population > 0 && StringComparer.Ordinal.Equals(x.SpeciesId, speciesId))
            .ToArray();
        if (relevant.Length == 0)
            return new CohortTraitProfile(
                speciesId,
                new Dictionary<string, double>(StringComparer.Ordinal));

        double totalPopulation = relevant.Sum(x => (double)x.Population);
        var aggregate = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var cohort in relevant)
        {
            var profile = Resolve(cohort);
            double weight = cohort.Population / totalPopulation;
            foreach (var trait in profile.TraitPotentials)
                aggregate[trait.Key] = aggregate.GetValueOrDefault(trait.Key) + trait.Value * weight;
        }
        return new CohortTraitProfile(speciesId, aggregate);
    }
}

public sealed record ReciprocalSelectionRule(
    string SpeciesAId,
    string TraitAId,
    string SpeciesBId,
    string TraitBId,
    double CouplingStrength,
    double MaximumPressure = 0.50);

public sealed record ReciprocalSelectionOutcome(
    IReadOnlyDictionary<string, CohortTraitProfile> TraitProfiles,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>> TraitPressures);

public sealed class ReciprocalCoevolutionEngine
{
    private readonly CohortTraitPotentialResolver _traits;

    public ReciprocalCoevolutionEngine(GenomeCatalog catalog)
    {
        _traits = new CohortTraitPotentialResolver(catalog);
    }

    public ReciprocalSelectionOutcome Resolve(
        IEnumerable<EvolutionCohort> cohorts,
        IEnumerable<ReciprocalSelectionRule> rules)
    {
        var cohortArray = cohorts.ToArray();
        var ruleArray = rules.ToArray();
        var speciesIds = ruleArray
            .SelectMany(x => new[] { x.SpeciesAId, x.SpeciesBId })
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        var presentSpecies = cohortArray
            .Where(x => x.Population > 0)
            .Select(x => x.SpeciesId)
            .ToHashSet(StringComparer.Ordinal);

        var profiles = speciesIds.ToDictionary(
            x => x,
            x => _traits.ResolvePopulation(cohortArray, x),
            StringComparer.Ordinal);
        var pressures = speciesIds.ToDictionary(
            x => x,
            _ => new Dictionary<string, double>(StringComparer.Ordinal),
            StringComparer.Ordinal);

        foreach (var rule in ruleArray
                     .OrderBy(x => x.SpeciesAId, StringComparer.Ordinal)
                     .ThenBy(x => x.SpeciesBId, StringComparer.Ordinal)
                     .ThenBy(x => x.TraitAId, StringComparer.Ordinal))
        {
            if (!presentSpecies.Contains(rule.SpeciesAId) || !presentSpecies.Contains(rule.SpeciesBId))
                continue;

            double coupling = Math.Clamp(rule.CouplingStrength, 0.0, 1.0);
            double max = Math.Clamp(rule.MaximumPressure, 0.0, 1.0);
            double potentialA = profiles[rule.SpeciesAId].TraitPotentials.GetValueOrDefault(rule.TraitAId);
            double potentialB = profiles[rule.SpeciesBId].TraitPotentials.GetValueOrDefault(rule.TraitBId);

            AddPressure(
                pressures[rule.SpeciesAId],
                rule.TraitAId,
                Math.Clamp((potentialB - 0.50) * coupling, -max, max));
            AddPressure(
                pressures[rule.SpeciesBId],
                rule.TraitBId,
                Math.Clamp((potentialA - 0.50) * coupling, -max, max));
        }

        return new ReciprocalSelectionOutcome(
            profiles,
            pressures.ToDictionary(
                x => x.Key,
                x => (IReadOnlyDictionary<string, double>)x.Value,
                StringComparer.Ordinal));
    }

    private static void AddPressure(IDictionary<string, double> target, string traitId, double delta)
    {
        var current = target.TryGetValue(traitId, out var value) ? value : 0.0;
        target[traitId] = Math.Clamp(current + delta, -1.0, 1.0);
    }
}



public sealed record TrophicCoevolutionBinding(
    string PredatorSpeciesId,
    string PredatorTraitId,
    string PreySpeciesId,
    string PreyTraitId,
    double CouplingStrength,
    double MaximumPressure = 0.50);

public sealed class TrophicCoevolutionRuleCompiler
{
    public IReadOnlyList<ReciprocalSelectionRule> Compile(
        TrophicNetworkDefinition network,
        IEnumerable<TrophicCoevolutionBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(network);
        ArgumentNullException.ThrowIfNull(bindings);
        var output = new List<ReciprocalSelectionRule>();

        foreach (var binding in bindings
                     .OrderBy(x => x.PredatorSpeciesId, StringComparer.Ordinal)
                     .ThenBy(x => x.PreySpeciesId, StringComparer.Ordinal))
        {
            bool linked = network.Links.Any(x =>
                StringComparer.Ordinal.Equals(x.ConsumerSpeciesId, binding.PredatorSpeciesId)
                && x.SourceKind == FoodWebNodeKind.Species
                && StringComparer.Ordinal.Equals(x.SourceId, binding.PreySpeciesId)
                && x.Relation == TrophicRelationKind.Predation);

            if (!linked)
                throw new InvalidOperationException(
                    $"No authored predation link exists from {binding.PredatorSpeciesId} to {binding.PreySpeciesId}.");

            output.Add(new ReciprocalSelectionRule(
                binding.PredatorSpeciesId,
                binding.PredatorTraitId,
                binding.PreySpeciesId,
                binding.PreyTraitId,
                binding.CouplingStrength,
                binding.MaximumPressure));
        }

        return output;
    }
}

public sealed class CoevolutionPressureCombiner
{
    public IReadOnlyDictionary<string, double> Merge(
        params IReadOnlyDictionary<string, double>[] pressureSets)
    {
        var merged = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var pressureSet in pressureSets)
        foreach (var pressure in pressureSet.OrderBy(x => x.Key, StringComparer.Ordinal))
            merged[pressure.Key] = Math.Clamp(
                merged.GetValueOrDefault(pressure.Key) + pressure.Value,
                -1.0,
                1.0);
        return merged;
    }
}

public sealed record DiseaseEvolutionState(
    string DiseaseId,
    int Generation,
    double TransmissionPotential,
    double Virulence,
    double EnvironmentalTolerance,
    double HostEscape);

public sealed record HostPathogenCoevolutionSettings(
    double AdaptationRate = 0.08,
    double MutationJitter = 0.015,
    double VirulenceTradeoff = 0.12,
    double HostSelectionStrength = 0.30);

public sealed record DiseaseCoevolutionResult(
    DiseaseEvolutionState Previous,
    DiseaseEvolutionState Next,
    double HostResistance,
    double EnvironmentalStress,
    IReadOnlyDictionary<string, double> HostTraitPressures,
    ulong Seed);

public sealed class HostPathogenCoevolutionEngine
{
    private readonly CohortTraitPotentialResolver _traits;

    public HostPathogenCoevolutionEngine(GenomeCatalog catalog)
    {
        _traits = new CohortTraitPotentialResolver(catalog);
    }

    public DiseaseCoevolutionResult Advance(
        DiseaseEvolutionState disease,
        EvolutionCohort host,
        IReadOnlySet<string> hostResistanceTraitIds,
        ZoneEnvironment zone,
        double prevalence,
        ulong simulationSeed,
        HostPathogenCoevolutionSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(disease);
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(hostResistanceTraitIds);
        ArgumentNullException.ThrowIfNull(zone);
        settings ??= new HostPathogenCoevolutionSettings();

        if (zone.SimulationState == CellSimulationState.Locked)
            throw new InvalidOperationException("Locked cells cannot commit host-pathogen coevolution.");
        if (!StringComparer.Ordinal.Equals(host.ZoneId, zone.ZoneId))
            throw new InvalidOperationException("Host cohort and zone must match.");

        var hostProfile = _traits.Resolve(host);
        double hostResistance = Math.Clamp(
            hostResistanceTraitIds
                .Select(x => hostProfile.TraitPotentials.GetValueOrDefault(x))
                .DefaultIfEmpty(0.0)
                .Average(),
            0.0,
            1.0);
        double environmentalStress = Math.Clamp(
            (1.0 - zone.PopulationHealth) * 0.45
            + (1.0 - zone.Stability) * 0.20
            + (1.0 - zone.Integrity) * 0.20
            + zone.ResonanceDebt * 0.15,
            0.0,
            1.0);

        ulong seed = StableSeed.FromText(
            $"{simulationSeed}|disease-evolution|{disease.DiseaseId}|{host.CohortId}|g{disease.Generation + 1}|{zone.ZoneId}");
        var random = new GenomeRandom(seed);
        double jitter = (random.NextDouble() - 0.5) * 2.0 * Math.Clamp(settings.MutationJitter, 0.0, 0.25);
        double adaptation = Math.Clamp(settings.AdaptationRate, 0.0, 1.0);
        double prevalence01 = Math.Clamp(prevalence, 0.0, 1.0);

        double nextEscape = Math.Clamp(
            disease.HostEscape + (hostResistance - disease.HostEscape) * adaptation + jitter,
            0.0,
            1.0);
        double nextTolerance = Math.Clamp(
            disease.EnvironmentalTolerance
            + (environmentalStress - disease.EnvironmentalTolerance) * adaptation
            + jitter * 0.50,
            0.0,
            1.0);
        double transmissionTarget = Math.Clamp(
            0.35 + prevalence01 * 0.35 + nextTolerance * 0.20 + nextEscape * 0.10 - hostResistance * 0.15,
            0.0,
            1.0);
        double nextTransmission = Math.Clamp(
            disease.TransmissionPotential
            + (transmissionTarget - disease.TransmissionPotential) * adaptation
            + jitter * 0.25,
            0.0,
            1.0);
        double virulenceTarget = Math.Clamp(
            0.40 + prevalence01 * 0.20 + nextEscape * 0.15
            - nextTransmission * Math.Clamp(settings.VirulenceTradeoff, 0.0, 1.0),
            0.0,
            1.0);
        double nextVirulence = Math.Clamp(
            disease.Virulence + (virulenceTarget - disease.Virulence) * adaptation,
            0.0,
            1.0);

        double hostPressureValue = Math.Clamp(
            prevalence01 * nextEscape * Math.Clamp(settings.HostSelectionStrength, 0.0, 1.0),
            0.0,
            1.0);
        var hostPressures = hostResistanceTraitIds
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToDictionary(x => x, _ => hostPressureValue, StringComparer.Ordinal);

        var next = disease with
        {
            Generation = disease.Generation + 1,
            TransmissionPotential = nextTransmission,
            Virulence = nextVirulence,
            EnvironmentalTolerance = nextTolerance,
            HostEscape = nextEscape
        };

        return new DiseaseCoevolutionResult(
            disease,
            next,
            hostResistance,
            environmentalStress,
            hostPressures,
            seed);
    }
}

public sealed class EvolvedDiseaseProfileAdapter
{
    public DiseaseProfile Apply(
        DiseaseProfile authored,
        DiseaseEvolutionState evolved)
    {
        ArgumentNullException.ThrowIfNull(authored);
        ArgumentNullException.ThrowIfNull(evolved);
        if (!StringComparer.Ordinal.Equals(authored.DiseaseId, evolved.DiseaseId))
            throw new InvalidOperationException("Authored and evolved disease IDs must match.");

        return authored with
        {
            BaseTransmission = Math.Clamp(
                authored.BaseTransmission * (0.50 + evolved.TransmissionPotential),
                0.0,
                1.0),
            MortalityRate = Math.Clamp(
                authored.MortalityRate * (0.50 + evolved.Virulence),
                0.0,
                1.0),
            EnvironmentalSensitivity = Math.Clamp(
                authored.EnvironmentalSensitivity * (0.50 + evolved.EnvironmentalTolerance),
                0.0,
                1.0)
        };
    }
}
