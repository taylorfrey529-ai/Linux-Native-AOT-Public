using Evermore.Genetics;

namespace EasternKingdoms.Simulation;

public sealed record CommunityTraitPressure(
    string ZoneId,
    string SpeciesId,
    IReadOnlyDictionary<string, double> TraitPressures);

public sealed record CommunityDiseaseDefinition(
    DiseaseProfile AuthoredProfile,
    DiseaseEvolutionState InitialEvolution,
    IReadOnlySet<string> HostResistanceTraitIds,
    HostPathogenCoevolutionSettings CoevolutionSettings,
    double CrossHostTransmission = 0.08);

public sealed record CommunityDiseaseCoevolutionResult(
    DiseaseEvolutionState Previous,
    DiseaseEvolutionState Next,
    double MeanHostResistance,
    double MeanEnvironmentalStress,
    double MeanPrevalence,
    IReadOnlyList<CommunityTraitPressure> HostPressures,
    ulong Seed);

public sealed record CommunityDiseaseGenerationResult(
    IReadOnlyList<DiseaseState> DiseaseStates,
    IReadOnlyList<DiseaseOutcome> TransmissionOutcomes,
    IReadOnlyList<DiseaseEvolutionState> EvolutionStates,
    IReadOnlyList<CommunityDiseaseCoevolutionResult> Coevolution,
    IReadOnlyList<CommunityTraitPressure> PendingHostPressures,
    IReadOnlyList<EvolutionCohort> CohortsAfterMortality);

public sealed class CommunityHostPathogenCoevolutionEngine
{
    private readonly CohortTraitPotentialResolver _traits;

    public CommunityHostPathogenCoevolutionEngine(GenomeCatalog catalog)
    {
        _traits = new CohortTraitPotentialResolver(
            catalog ?? throw new ArgumentNullException(nameof(catalog)));
    }

    public CommunityDiseaseCoevolutionResult Advance(
        CommunityDiseaseDefinition definition,
        DiseaseEvolutionState disease,
        IReadOnlyList<EvolutionCohort> cohorts,
        IReadOnlyList<DiseaseState> diseaseStates,
        IReadOnlyDictionary<string, ZoneEnvironment> zones,
        ulong simulationSeed)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(disease);
        ArgumentNullException.ThrowIfNull(cohorts);
        ArgumentNullException.ThrowIfNull(diseaseStates);
        ArgumentNullException.ThrowIfNull(zones);
        ValidateDefinition(definition);

        var hosts = cohorts
            .Where(x => x.Population > 0)
            .Where(x => definition.AuthoredProfile.HostSpeciesIds.Contains(x.SpeciesId))
            .Where(x => zones.TryGetValue(x.ZoneId, out var zone) && zone.SimulationState != CellSimulationState.Locked)
            .OrderBy(x => x.ZoneId, StringComparer.Ordinal)
            .ThenBy(x => x.SpeciesId, StringComparer.Ordinal)
            .ThenBy(x => x.CohortId, StringComparer.Ordinal)
            .ToArray();

        if (hosts.Length == 0)
        {
            ulong noHostSeed = StableSeed.FromText(
                $"{simulationSeed}|community-disease|{disease.DiseaseId}|g{disease.Generation + 1}|no-host");
            return new CommunityDiseaseCoevolutionResult(
                disease,
                disease with { Generation = disease.Generation + 1 },
                0.0,
                0.0,
                0.0,
                Array.Empty<CommunityTraitPressure>(),
                noHostSeed);
        }

        double totalPopulation = hosts.Sum(x => (double)x.Population);
        double resistance = 0.0;
        double environment = 0.0;
        double prevalence = 0.0;
        var localPrevalence = diseaseStates
            .Where(x => StringComparer.Ordinal.Equals(x.DiseaseId, disease.DiseaseId))
            .ToDictionary(
                x => (x.ZoneId, x.SpeciesId),
                x => Math.Clamp(x.Prevalence, 0.0, 1.0));

        foreach (var host in hosts)
        {
            double weight = host.Population / totalPopulation;
            var profile = _traits.Resolve(host);
            double hostResistance = definition.HostResistanceTraitIds
                .Select(x => profile.TraitPotentials.GetValueOrDefault(x))
                .DefaultIfEmpty(0.0)
                .Average();
            resistance += Math.Clamp(hostResistance, 0.0, 1.0) * weight;

            var zone = zones[host.ZoneId];
            double zoneStress = Math.Clamp(
                (1.0 - zone.PopulationHealth) * 0.45
                + (1.0 - zone.Stability) * 0.20
                + (1.0 - zone.Integrity) * 0.20
                + zone.ResonanceDebt * 0.15,
                0.0,
                1.0);
            environment += zoneStress * weight;
            prevalence += localPrevalence.GetValueOrDefault((host.ZoneId, host.SpeciesId)) * weight;
        }

        var settings = definition.CoevolutionSettings;
        ulong seed = StableSeed.FromText(
            $"{simulationSeed}|community-disease|{disease.DiseaseId}|g{disease.Generation + 1}");
        var random = new GenomeRandom(seed);
        double jitter = (random.NextDouble() - 0.5) * 2.0 * Math.Clamp(settings.MutationJitter, 0.0, 0.25);
        double adaptation = Math.Clamp(settings.AdaptationRate, 0.0, 1.0);

        double nextEscape = Math.Clamp(
            disease.HostEscape + (resistance - disease.HostEscape) * adaptation + jitter,
            0.0,
            1.0);
        double nextTolerance = Math.Clamp(
            disease.EnvironmentalTolerance
            + (environment - disease.EnvironmentalTolerance) * adaptation
            + jitter * 0.50,
            0.0,
            1.0);
        double transmissionTarget = Math.Clamp(
            0.35 + prevalence * 0.35 + nextTolerance * 0.20 + nextEscape * 0.10 - resistance * 0.15,
            0.0,
            1.0);
        double nextTransmission = Math.Clamp(
            disease.TransmissionPotential
            + (transmissionTarget - disease.TransmissionPotential) * adaptation
            + jitter * 0.25,
            0.0,
            1.0);
        double virulenceTarget = Math.Clamp(
            0.40 + prevalence * 0.20 + nextEscape * 0.15
            - nextTransmission * Math.Clamp(settings.VirulenceTradeoff, 0.0, 1.0),
            0.0,
            1.0);
        double nextVirulence = Math.Clamp(
            disease.Virulence + (virulenceTarget - disease.Virulence) * adaptation,
            0.0,
            1.0);

        var next = disease with
        {
            Generation = disease.Generation + 1,
            TransmissionPotential = nextTransmission,
            Virulence = nextVirulence,
            EnvironmentalTolerance = nextTolerance,
            HostEscape = nextEscape
        };

        var pressures = hosts
            .Select(host =>
            {
                double local = localPrevalence.GetValueOrDefault((host.ZoneId, host.SpeciesId));
                double value = Math.Clamp(
                    local * nextEscape * Math.Clamp(settings.HostSelectionStrength, 0.0, 1.0),
                    0.0,
                    1.0);
                var traits = definition.HostResistanceTraitIds
                    .OrderBy(x => x, StringComparer.Ordinal)
                    .ToDictionary(x => x, _ => value, StringComparer.Ordinal);
                return new CommunityTraitPressure(host.ZoneId, host.SpeciesId, traits);
            })
            .GroupBy(x => (x.ZoneId, x.SpeciesId))
            .Select(group => new CommunityTraitPressure(
                group.Key.ZoneId,
                group.Key.SpeciesId,
                MergePressureMaps(group.Select(x => x.TraitPressures))))
            .OrderBy(x => x.ZoneId, StringComparer.Ordinal)
            .ThenBy(x => x.SpeciesId, StringComparer.Ordinal)
            .ToArray();

        return new CommunityDiseaseCoevolutionResult(
            disease,
            next,
            Math.Clamp(resistance, 0.0, 1.0),
            Math.Clamp(environment, 0.0, 1.0),
            Math.Clamp(prevalence, 0.0, 1.0),
            pressures,
            seed);
    }

    private static IReadOnlyDictionary<string, double> MergePressureMaps(
        IEnumerable<IReadOnlyDictionary<string, double>> maps)
    {
        var merged = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var map in maps)
        foreach (var item in map.OrderBy(x => x.Key, StringComparer.Ordinal))
            merged[item.Key] = Math.Clamp(
                Math.Max(merged.GetValueOrDefault(item.Key), item.Value),
                -1.0,
                1.0);
        return merged;
    }

    internal static void ValidateDefinition(CommunityDiseaseDefinition definition)
    {
        if (!StringComparer.Ordinal.Equals(
                definition.AuthoredProfile.DiseaseId,
                definition.InitialEvolution.DiseaseId))
            throw new InvalidOperationException("Disease authoring and initial evolution IDs must match.");
        if (definition.CrossHostTransmission is < 0.0 or > 1.0)
            throw new InvalidOperationException("Cross-host disease transmission must be in [0, 1].");
        if (definition.HostResistanceTraitIds.Count == 0)
            throw new InvalidOperationException($"Disease {definition.AuthoredProfile.DiseaseId} requires at least one authored host resistance trait.");
    }
}

public sealed class CommunityDiseaseEngine
{
    private readonly DiseaseTransmissionEngine _transmission = new();
    private readonly EvolvedDiseaseProfileAdapter _adapter = new();
    private readonly CommunityHostPathogenCoevolutionEngine _coevolution;

    public CommunityDiseaseEngine(GenomeCatalog catalog)
    {
        _coevolution = new CommunityHostPathogenCoevolutionEngine(catalog);
    }

    public CommunityDiseaseGenerationResult Advance(
        IReadOnlyList<CommunityDiseaseDefinition> definitions,
        IReadOnlyList<DiseaseState> previousStates,
        IReadOnlyList<DiseaseEvolutionState> previousEvolution,
        IReadOnlyList<EvolutionCohort> cohorts,
        IReadOnlyList<CohortMigrationResult> migrations,
        IReadOnlyList<PopulationRoute> routes,
        IReadOnlyList<ZoneEnvironment> zones,
        ulong simulationSeed)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(previousStates);
        ArgumentNullException.ThrowIfNull(previousEvolution);
        ArgumentNullException.ThrowIfNull(cohorts);
        ArgumentNullException.ThrowIfNull(migrations);
        ArgumentNullException.ThrowIfNull(routes);
        ArgumentNullException.ThrowIfNull(zones);

        var duplicateDisease = definitions
            .GroupBy(x => x.AuthoredProfile.DiseaseId, StringComparer.Ordinal)
            .FirstOrDefault(x => x.Count() > 1);
        if (duplicateDisease is not null)
            throw new InvalidOperationException($"Duplicate community disease definition {duplicateDisease.Key}.");

        var zoneMap = zones.ToDictionary(x => x.ZoneId, StringComparer.Ordinal);
        var states = EnsureHostStates(definitions, previousStates, cohorts);
        var evolutionMap = previousEvolution.ToDictionary(x => x.DiseaseId, x => x, StringComparer.Ordinal);
        var allOutcomes = new List<DiseaseOutcome>();
        var nextEvolution = new List<DiseaseEvolutionState>();
        var coevolution = new List<CommunityDiseaseCoevolutionResult>();
        var pendingPressures = new List<CommunityTraitPressure>();

        foreach (var definition in definitions.OrderBy(x => x.AuthoredProfile.DiseaseId, StringComparer.Ordinal))
        {
            CommunityHostPathogenCoevolutionEngine.ValidateDefinition(definition);
            var evolution = evolutionMap.GetValueOrDefault(
                definition.AuthoredProfile.DiseaseId,
                definition.InitialEvolution);
            var profile = _adapter.Apply(definition.AuthoredProfile, evolution);

            var outcomes = _transmission.Advance(
                    profile,
                    states,
                    migrations,
                    routes,
                    zones)
                .Select(x => ApplyCrossHostSpillover(x, definition, profile, states))
                .ToArray();

            ReplaceStates(states, outcomes);
            allOutcomes.AddRange(outcomes);

            var coevolved = _coevolution.Advance(
                definition,
                evolution,
                cohorts,
                states,
                zoneMap,
                simulationSeed);
            coevolution.Add(coevolved);
            nextEvolution.Add(coevolved.Next);
            pendingPressures.AddRange(coevolved.HostPressures);
        }

        var cohortsAfterMortality = ApplyMortality(cohorts, allOutcomes);

        return new CommunityDiseaseGenerationResult(
            states.OrderBy(x => x.DiseaseId, StringComparer.Ordinal)
                .ThenBy(x => x.ZoneId, StringComparer.Ordinal)
                .ThenBy(x => x.SpeciesId, StringComparer.Ordinal)
                .ToArray(),
            allOutcomes.OrderBy(x => x.Next.DiseaseId, StringComparer.Ordinal)
                .ThenBy(x => x.Next.ZoneId, StringComparer.Ordinal)
                .ThenBy(x => x.Next.SpeciesId, StringComparer.Ordinal)
                .ToArray(),
            nextEvolution.OrderBy(x => x.DiseaseId, StringComparer.Ordinal).ToArray(),
            coevolution.OrderBy(x => x.Next.DiseaseId, StringComparer.Ordinal).ToArray(),
            MergePressures(pendingPressures),
            cohortsAfterMortality);
    }

    private static List<DiseaseState> EnsureHostStates(
        IReadOnlyList<CommunityDiseaseDefinition> definitions,
        IReadOnlyList<DiseaseState> previous,
        IReadOnlyList<EvolutionCohort> cohorts)
    {
        var map = previous.ToDictionary(
            x => (x.ZoneId, x.SpeciesId, x.DiseaseId),
            x => x);

        foreach (var definition in definitions)
        foreach (var cohort in cohorts.Where(x => x.Population > 0))
        {
            if (!definition.AuthoredProfile.HostSpeciesIds.Contains(cohort.SpeciesId))
                continue;
            var key = (cohort.ZoneId, cohort.SpeciesId, definition.AuthoredProfile.DiseaseId);
            if (!map.ContainsKey(key))
                map[key] = new DiseaseState(cohort.ZoneId, cohort.SpeciesId, definition.AuthoredProfile.DiseaseId, 0.0);
        }

        return map.Values.ToList();
    }

    private static DiseaseOutcome ApplyCrossHostSpillover(
        DiseaseOutcome source,
        CommunityDiseaseDefinition definition,
        DiseaseProfile effectiveProfile,
        IReadOnlyList<DiseaseState> states)
    {
        double spillover = states
            .Where(x => StringComparer.Ordinal.Equals(x.ZoneId, source.Next.ZoneId))
            .Where(x => StringComparer.Ordinal.Equals(x.DiseaseId, source.Next.DiseaseId))
            .Where(x => !StringComparer.Ordinal.Equals(x.SpeciesId, source.Next.SpeciesId))
            .Where(x => definition.AuthoredProfile.HostSpeciesIds.Contains(x.SpeciesId))
            .Select(x => Math.Clamp(x.Prevalence, 0.0, 1.0))
            .DefaultIfEmpty(0.0)
            .Average()
            * Math.Clamp(definition.CrossHostTransmission, 0.0, 1.0);

        if (spillover <= 0.0)
            return source;

        double nextPrevalence = Math.Clamp(source.Next.Prevalence + spillover, 0.0, 1.0);
        double mortality = Math.Clamp(
            nextPrevalence * effectiveProfile.MortalityRate,
            0.0,
            1.0);
        return source with
        {
            Next = source.Next with { Prevalence = nextPrevalence },
            ImportedPressure = Math.Clamp(source.ImportedPressure + spillover, 0.0, 1.0),
            MortalityPressure = mortality
        };
    }

    private static void ReplaceStates(List<DiseaseState> states, IEnumerable<DiseaseOutcome> outcomes)
    {
        var replacements = outcomes.ToDictionary(
            x => (x.Next.ZoneId, x.Next.SpeciesId, x.Next.DiseaseId),
            x => x.Next);
        for (int i = 0; i < states.Count; i++)
        {
            var key = (states[i].ZoneId, states[i].SpeciesId, states[i].DiseaseId);
            if (replacements.TryGetValue(key, out var next))
                states[i] = next;
        }
    }

    private static IReadOnlyList<EvolutionCohort> ApplyMortality(
        IReadOnlyList<EvolutionCohort> cohorts,
        IReadOnlyList<DiseaseOutcome> outcomes)
    {
        var mortalityByHost = outcomes
            .GroupBy(x => (x.Next.ZoneId, x.Next.SpeciesId))
            .ToDictionary(
                x => x.Key,
                x => x.OrderBy(y => y.Next.DiseaseId, StringComparer.Ordinal)
                    .Select(y => Math.Clamp(y.MortalityPressure, 0.0, 1.0))
                    .ToArray());

        return cohorts
            .Select(cohort =>
            {
                if (!mortalityByHost.TryGetValue((cohort.ZoneId, cohort.SpeciesId), out var pressures))
                    return cohort;
                double survival = pressures.Aggregate(1.0, (current, pressure) => current * (1.0 - pressure));
                long population = Math.Max(0L, (long)Math.Floor(cohort.Population * survival));
                return cohort with { Population = population };
            })
            .OrderBy(x => x.SpeciesId, StringComparer.Ordinal)
            .ThenBy(x => x.ZoneId, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<CommunityTraitPressure> MergePressures(
        IEnumerable<CommunityTraitPressure> source)
    {
        return source
            .GroupBy(x => (x.ZoneId, x.SpeciesId))
            .Select(group =>
            {
                var merged = new Dictionary<string, double>(StringComparer.Ordinal);
                foreach (var pressure in group.SelectMany(x => x.TraitPressures)
                             .OrderBy(x => x.Key, StringComparer.Ordinal))
                {
                    merged[pressure.Key] = Math.Clamp(
                        merged.GetValueOrDefault(pressure.Key) + pressure.Value,
                        -1.0,
                        1.0);
                }
                return new CommunityTraitPressure(group.Key.ZoneId, group.Key.SpeciesId, merged);
            })
            .OrderBy(x => x.ZoneId, StringComparer.Ordinal)
            .ThenBy(x => x.SpeciesId, StringComparer.Ordinal)
            .ToArray();
    }
}
