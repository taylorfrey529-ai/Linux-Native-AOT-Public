using System.Text;
using Evermore.Genetics;

namespace EasternKingdoms.Simulation;

public sealed record CommunityEvolutionDefinition(
    IReadOnlyList<ZoneEnvironment> BaseZones,
    IReadOnlyList<EnvironmentalPulseDefinition> EnvironmentalPulses,
    TrophicNetworkDefinition TrophicNetwork,
    IReadOnlyList<ReciprocalSelectionRule> ReciprocalSelectionRules,
    IReadOnlyList<CommunityDiseaseDefinition> Diseases,
    MetapopulationSettings MetapopulationSettings,
    CommunityResilienceSettings ResilienceSettings);

public sealed record CommunityEvolutionState(
    int Generation,
    IReadOnlyList<EvolutionCohort> Cohorts,
    IReadOnlyList<DiseaseState> DiseaseStates,
    IReadOnlyList<DiseaseEvolutionState> DiseaseEvolutionStates,
    IReadOnlyList<CommunityTraitPressure> PendingTraitPressures,
    CommunityResilienceState Resilience)
{
    public static CommunityEvolutionState CreateInitial(
        IEnumerable<EvolutionCohort> cohorts,
        IEnumerable<CommunityDiseaseDefinition>? diseases = null,
        IEnumerable<DiseaseState>? diseaseStates = null)
    {
        ArgumentNullException.ThrowIfNull(cohorts);
        var definitions = diseases?.ToArray() ?? Array.Empty<CommunityDiseaseDefinition>();
        foreach (var definition in definitions)
            CommunityHostPathogenCoevolutionEngine.ValidateDefinition(definition);

        return new CommunityEvolutionState(
            Generation: 0,
            Cohorts: cohorts.OrderBy(x => x.SpeciesId, StringComparer.Ordinal)
                .ThenBy(x => x.ZoneId, StringComparer.Ordinal)
                .ToArray(),
            DiseaseStates: (diseaseStates ?? Array.Empty<DiseaseState>())
                .OrderBy(x => x.DiseaseId, StringComparer.Ordinal)
                .ThenBy(x => x.ZoneId, StringComparer.Ordinal)
                .ThenBy(x => x.SpeciesId, StringComparer.Ordinal)
                .ToArray(),
            DiseaseEvolutionStates: definitions
                .Select(x => x.InitialEvolution)
                .OrderBy(x => x.DiseaseId, StringComparer.Ordinal)
                .ToArray(),
            PendingTraitPressures: Array.Empty<CommunityTraitPressure>(),
            Resilience: CommunityResilienceState.Initial);
    }
}

public sealed record CommunityPopulationSnapshot(
    string SpeciesId,
    string ZoneId,
    int Generation,
    long Population);

public sealed record CommunityHistoryEntry(
    int Generation,
    ulong Seed,
    CommunityCondition Condition,
    int StressDimensions,
    long TotalPopulation,
    IReadOnlyList<string> AppliedPulseIds,
    IReadOnlyList<CommunityPopulationSnapshot> Populations,
    IReadOnlyList<DiseaseState> DiseaseStates);

public sealed record CommunityGenerationResult(
    CommunityEvolutionState Previous,
    CommunityEvolutionState Next,
    SeasonalEnvironmentResult Environment,
    IReadOnlyList<ZoneTrophicOutcome> TrophicOutcomes,
    IReadOnlyList<MetapopulationGenerationResult> Metapopulations,
    CommunityDiseaseGenerationResult Disease,
    CommunityHealthAssessment Health,
    CommunityHistoryEntry History);

public sealed class CommunityHistoryLedger
{
    private readonly List<CommunityHistoryEntry> _entries = new();

    public IReadOnlyList<CommunityHistoryEntry> Entries => _entries;

    public void Append(CommunityHistoryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (_entries.Count > 0 && entry.Generation <= _entries[^1].Generation)
            throw new InvalidOperationException("Community history generations must be appended in increasing order.");
        _entries.Add(entry);
    }

    public string ComputeIntegrityHash()
    {
        var canonical = new StringBuilder();
        foreach (var entry in _entries.OrderBy(x => x.Generation))
        {
            canonical.Append(entry.Generation).Append('|')
                .Append(entry.Seed).Append('|')
                .Append(entry.Condition).Append('|')
                .Append(entry.StressDimensions).Append('|')
                .Append(entry.TotalPopulation).AppendLine();

            foreach (var pulse in entry.AppliedPulseIds.OrderBy(x => x, StringComparer.Ordinal))
                canonical.Append("P|").Append(pulse).AppendLine();
            foreach (var population in entry.Populations
                         .OrderBy(x => x.SpeciesId, StringComparer.Ordinal)
                         .ThenBy(x => x.ZoneId, StringComparer.Ordinal))
            {
                canonical.Append("C|").Append(population.SpeciesId).Append('|')
                    .Append(population.ZoneId).Append('|')
                    .Append(population.Generation).Append('|')
                    .Append(population.Population).AppendLine();
            }
            foreach (var disease in entry.DiseaseStates
                         .OrderBy(x => x.DiseaseId, StringComparer.Ordinal)
                         .ThenBy(x => x.ZoneId, StringComparer.Ordinal)
                         .ThenBy(x => x.SpeciesId, StringComparer.Ordinal))
            {
                canonical.Append("D|").Append(disease.DiseaseId).Append('|')
                    .Append(disease.ZoneId).Append('|')
                    .Append(disease.SpeciesId).Append('|')
                    .Append(disease.Prevalence.ToString("R", System.Globalization.CultureInfo.InvariantCulture))
                    .AppendLine();
            }
        }

        return new GeneticsIntegrityHasher().Compute(canonical.ToString());
    }
}

public sealed record LongHorizonCommunityResult(
    CommunityEvolutionState FinalState,
    IReadOnlyList<CommunityHistoryEntry> History,
    string IntegrityHash);

public sealed class WholeCommunityEvolutionOrchestrator
{
    private readonly SpeciesEcologyCatalog _ecology;
    private readonly PopulationRouteCatalog _routes;
    private readonly MetapopulationOrchestrator _metapopulations;
    private readonly ReciprocalCoevolutionEngine _reciprocal;
    private readonly SeasonalEnvironmentEngine _seasonal = new();
    private readonly ZoneFoodWebBuilder _foodBuilder = new();
    private readonly TrophicNetworkResolver _trophic = new();
    private readonly CommunityDiseaseEngine _diseases;
    private readonly CommunityResilienceTracker _resilience = new();

    public WholeCommunityEvolutionOrchestrator(
        GenomeCatalog genomeCatalog,
        SpeciesEcologyCatalog ecology,
        PopulationRouteCatalog routes,
        MetapopulationOrchestrator metapopulations,
        ReciprocalCoevolutionEngine reciprocal)
    {
        ArgumentNullException.ThrowIfNull(genomeCatalog);
        _ecology = ecology ?? throw new ArgumentNullException(nameof(ecology));
        _routes = routes ?? throw new ArgumentNullException(nameof(routes));
        _metapopulations = metapopulations ?? throw new ArgumentNullException(nameof(metapopulations));
        _reciprocal = reciprocal ?? throw new ArgumentNullException(nameof(reciprocal));
        _diseases = new CommunityDiseaseEngine(genomeCatalog);
    }

    public CommunityGenerationResult AdvanceGeneration(
        CommunityEvolutionDefinition definition,
        CommunityEvolutionState state,
        ulong simulationSeed)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(state);
        ValidateState(definition, state);

        int generation = state.Generation + 1;
        ulong generationSeed = StableSeed.FromText($"{simulationSeed}|community|g{generation}");
        var environment = _seasonal.Apply(
            definition.BaseZones,
            generation,
            definition.EnvironmentalPulses);

        var abundanceByZone = BuildAbundanceByZone(state.Cohorts, environment.Zones);
        var trophicOutcomes = ResolveTrophicOutcomes(
            definition.TrophicNetwork,
            environment.Zones,
            abundanceByZone);
        var growthModifiers = BuildGrowthModifiers(trophicOutcomes);
        var pressures = ResolveSelectionPressures(
            definition.ReciprocalSelectionRules,
            state.Cohorts,
            state.PendingTraitPressures,
            environment.Zones);

        var metapopulationResults = new List<MetapopulationGenerationResult>();
        foreach (string speciesId in state.Cohorts
                     .Select(x => x.SpeciesId)
                     .Distinct(StringComparer.Ordinal)
                     .OrderBy(x => x, StringComparer.Ordinal))
        {
            var source = state.Cohorts
                .Where(x => StringComparer.Ordinal.Equals(x.SpeciesId, speciesId))
                .OrderBy(x => x.ZoneId, StringComparer.Ordinal)
                .ToArray();
            if (source.Length == 0)
                continue;

            var speciesPressures = pressures
                .Where(x => StringComparer.Ordinal.Equals(x.SpeciesId, speciesId))
                .ToDictionary(
                    x => x.ZoneId,
                    x => x.TraitPressures,
                    StringComparer.Ordinal);
            var speciesGrowth = growthModifiers
                .Where(x => StringComparer.Ordinal.Equals(x.SpeciesId, speciesId))
                .ToDictionary(
                    x => x.ZoneId,
                    x => x.Modifier,
                    StringComparer.Ordinal);

            ulong speciesSeed = StableSeed.FromText(
                $"{generationSeed}|metapopulation|{speciesId}");
            metapopulationResults.Add(_metapopulations.AdvanceGeneration(
                source,
                environment.Zones,
                speciesSeed,
                definition.MetapopulationSettings,
                abundanceByZone,
                externalTraitPressures: null,
                externalTraitPressuresByZone: speciesPressures,
                externalGrowthModifiersByZone: speciesGrowth));
        }

        var postMigration = metapopulationResults
            .SelectMany(x => x.Cohorts)
            .OrderBy(x => x.SpeciesId, StringComparer.Ordinal)
            .ThenBy(x => x.ZoneId, StringComparer.Ordinal)
            .ToArray();
        var migrations = metapopulationResults
            .SelectMany(x => x.Migrations)
            .OrderBy(x => x.RouteId, StringComparer.Ordinal)
            .ThenBy(x => x.SourceBefore.SpeciesId, StringComparer.Ordinal)
            .ToArray();

        var disease = _diseases.Advance(
            definition.Diseases,
            state.DiseaseStates,
            state.DiseaseEvolutionStates,
            postMigration,
            migrations,
            _routes.Routes.ToArray(),
            environment.Zones,
            generationSeed);

        var metrics = _resilience.Measure(
            state.Cohorts,
            disease.CohortsAfterMortality,
            disease.DiseaseStates,
            trophicOutcomes,
            metapopulationResults);
        var health = _resilience.Assess(
            state.Resilience,
            metrics,
            definition.ResilienceSettings);

        var next = new CommunityEvolutionState(
            generation,
            disease.CohortsAfterMortality,
            disease.DiseaseStates,
            disease.EvolutionStates,
            disease.PendingHostPressures,
            health.Next);
        var history = BuildHistoryEntry(
            next,
            environment,
            health,
            generationSeed);

        return new CommunityGenerationResult(
            state,
            next,
            environment,
            trophicOutcomes,
            metapopulationResults,
            disease,
            health,
            history);
    }

    private IReadOnlyList<ZoneTrophicOutcome> ResolveTrophicOutcomes(
        TrophicNetworkDefinition network,
        IReadOnlyList<ZoneEnvironment> zones,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>> abundanceByZone)
    {
        var output = new List<ZoneTrophicOutcome>();
        foreach (var zone in zones.OrderBy(x => x.ZoneId, StringComparer.Ordinal))
        {
            if (zone.SimulationState == CellSimulationState.Locked)
                continue;
            var abundance = abundanceByZone.GetValueOrDefault(
                zone.ZoneId,
                new Dictionary<string, double>(StringComparer.Ordinal));
            var state = _foodBuilder.Build(zone, abundance);
            foreach (var outcome in _trophic.Resolve(network, _ecology, state))
                output.Add(new ZoneTrophicOutcome(zone.ZoneId, outcome));
        }
        return output
            .OrderBy(x => x.ZoneId, StringComparer.Ordinal)
            .ThenBy(x => x.Outcome.SpeciesId, StringComparer.Ordinal)
            .ToArray();
    }

    private IReadOnlyList<CommunityTraitPressure> ResolveSelectionPressures(
        IReadOnlyList<ReciprocalSelectionRule> rules,
        IReadOnlyList<EvolutionCohort> cohorts,
        IReadOnlyList<CommunityTraitPressure> pending,
        IReadOnlyList<ZoneEnvironment> zones)
    {
        var result = pending
            .GroupBy(x => (x.ZoneId, x.SpeciesId))
            .ToDictionary(
                x => x.Key,
                x => MergePressureMaps(x.Select(y => y.TraitPressures)));

        foreach (var zone in zones.OrderBy(x => x.ZoneId, StringComparer.Ordinal))
        {
            if (zone.SimulationState == CellSimulationState.Locked)
                continue;
            var local = cohorts
                .Where(x => x.Population > 0 && StringComparer.Ordinal.Equals(x.ZoneId, zone.ZoneId))
                .ToArray();
            if (local.Length == 0 || rules.Count == 0)
                continue;

            var coevolution = _reciprocal.Resolve(local, rules);
            foreach (var species in coevolution.TraitPressures.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                if (species.Value.Count == 0)
                    continue;
                var key = (zone.ZoneId, species.Key);
                result[key] = MergePressureMaps(new[]
                {
                    result.GetValueOrDefault(key, new Dictionary<string, double>(StringComparer.Ordinal)),
                    species.Value
                });
            }
        }

        return result
            .Select(x => new CommunityTraitPressure(x.Key.ZoneId, x.Key.SpeciesId, x.Value))
            .OrderBy(x => x.ZoneId, StringComparer.Ordinal)
            .ThenBy(x => x.SpeciesId, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>> BuildAbundanceByZone(
        IReadOnlyList<EvolutionCohort> cohorts,
        IReadOnlyList<ZoneEnvironment> zones)
    {
        var output = new Dictionary<string, IReadOnlyDictionary<string, double>>(StringComparer.Ordinal);
        foreach (var zone in zones.OrderBy(x => x.ZoneId, StringComparer.Ordinal))
        {
            var local = cohorts.Where(x => x.Population > 0 && StringComparer.Ordinal.Equals(x.ZoneId, zone.ZoneId)).ToArray();
            double total = local.Sum(x => (double)x.Population);
            output[zone.ZoneId] = local
                .GroupBy(x => x.SpeciesId, StringComparer.Ordinal)
                .ToDictionary(
                    x => x.Key,
                    x => total <= 0.0 ? 0.0 : Math.Clamp(x.Sum(y => (double)y.Population) / total, 0.0, 1.0),
                    StringComparer.Ordinal);
        }
        return output;
    }

    private static IReadOnlyList<ZoneGrowthModifier> BuildGrowthModifiers(
        IReadOnlyList<ZoneTrophicOutcome> trophic)
    {
        return trophic.Select(x => new ZoneGrowthModifier(
                x.ZoneId,
                x.Outcome.SpeciesId,
                Math.Clamp(0.65 + x.Outcome.NetSupportModifier * 0.50, 0.50, 1.35)))
            .OrderBy(x => x.SpeciesId, StringComparer.Ordinal)
            .ThenBy(x => x.ZoneId, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyDictionary<string, double> MergePressureMaps(
        IEnumerable<IReadOnlyDictionary<string, double>> maps)
    {
        var merged = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var map in maps)
        foreach (var pressure in map.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            merged[pressure.Key] = Math.Clamp(
                merged.GetValueOrDefault(pressure.Key) + pressure.Value,
                -1.0,
                1.0);
        }
        return merged;
    }

    private static CommunityHistoryEntry BuildHistoryEntry(
        CommunityEvolutionState state,
        SeasonalEnvironmentResult environment,
        CommunityHealthAssessment health,
        ulong seed)
    {
        var populations = state.Cohorts
            .Select(x => new CommunityPopulationSnapshot(
                x.SpeciesId,
                x.ZoneId,
                x.Generation,
                x.Population))
            .OrderBy(x => x.SpeciesId, StringComparer.Ordinal)
            .ThenBy(x => x.ZoneId, StringComparer.Ordinal)
            .ToArray();

        return new CommunityHistoryEntry(
            state.Generation,
            seed,
            state.Resilience.Condition,
            health.StressDimensions,
            populations.Sum(x => x.Population),
            environment.AppliedPulses.Select(x => x.PulseId)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray(),
            populations,
            state.DiseaseStates);
    }

    private void ValidateState(CommunityEvolutionDefinition definition, CommunityEvolutionState state)
    {
        if (state.Generation < 0)
            throw new InvalidOperationException("Community generation cannot be negative.");
        if (state.Cohorts.Count == 0)
            throw new InvalidOperationException("Whole-community evolution requires at least one cohort.");

        var zoneIds = definition.BaseZones.Select(x => x.ZoneId).ToHashSet(StringComparer.Ordinal);
        var duplicatePatch = state.Cohorts
            .GroupBy(x => (x.SpeciesId, x.ZoneId))
            .FirstOrDefault(x => x.Count() > 1);
        if (duplicatePatch is not null)
            throw new InvalidOperationException(
                $"Community state contains duplicate cohort patches for {duplicatePatch.Key.SpeciesId} in {duplicatePatch.Key.ZoneId}.");

        foreach (var cohort in state.Cohorts)
        {
            if (!zoneIds.Contains(cohort.ZoneId))
                throw new InvalidOperationException($"Cohort {cohort.CohortId} is outside the authoritative community zone set.");
            var profile = _ecology.Get(cohort.SpeciesId);
            if (profile.Status != SpeciesEcologyStatus.Active)
                throw new InvalidOperationException($"Species {cohort.SpeciesId} is not active for community evolution.");
        }
    }

    private sealed record ZoneGrowthModifier(
        string ZoneId,
        string SpeciesId,
        double Modifier);
}

public sealed class CommunityEvolutionRuntime
{
    private readonly WholeCommunityEvolutionOrchestrator _orchestrator;
    private readonly CommunityEvolutionDefinition _definition;
    private readonly CommunityHistoryLedger _history = new();

    public CommunityEvolutionState State { get; private set; }
    public CommunityHistoryLedger History => _history;

    public CommunityEvolutionRuntime(
        WholeCommunityEvolutionOrchestrator orchestrator,
        CommunityEvolutionDefinition definition,
        CommunityEvolutionState initialState)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        State = initialState ?? throw new ArgumentNullException(nameof(initialState));
    }

    public CommunityGenerationResult Advance(ulong simulationSeed)
    {
        var result = _orchestrator.AdvanceGeneration(_definition, State, simulationSeed);
        State = result.Next;
        _history.Append(result.History);
        return result;
    }

    public LongHorizonCommunityResult Run(int generations, ulong simulationSeed)
    {
        if (generations < 0)
            throw new ArgumentOutOfRangeException(nameof(generations));
        for (int i = 0; i < generations; i++)
            Advance(simulationSeed);

        return new LongHorizonCommunityResult(
            State,
            _history.Entries.ToArray(),
            _history.ComputeIntegrityHash());
    }
}
