namespace EasternKingdoms.Simulation;

public sealed record MetapopulationSettings(
    double BaseDispersalRate = 0.08,
    double MaximumDispersalFraction = 0.25,
    double MigrationPressureDifference = 0.05,
    double MinimumRouteCondition = 0.15,
    long MinimumViablePopulation = 10,
    long MinimumFounderPopulation = 8,
    double AbundanceScale = 10000.0);

public sealed record GeneFlowRecord(
    string RouteId,
    string FromZoneId,
    string ToZoneId,
    long Departed,
    long Arrived,
    bool Recolonized,
    double FounderDiversityRetention,
    bool FounderBelowPreferredMinimum,
    ulong Seed);

public sealed record MetapopulationGenerationResult(
    string SpeciesId,
    int Generation,
    IReadOnlyList<EvolutionCohort> Cohorts,
    IReadOnlyList<CohortEvolutionResult> LocalEvolution,
    IReadOnlyList<CohortMigrationResult> Migrations,
    IReadOnlyList<GeneFlowRecord> GeneFlow,
    IReadOnlyList<FounderEffectReport> FounderEffects,
    IReadOnlyList<PairwiseGeneticDivergence> Divergence,
    HabitatFragmentationReport Fragmentation);

public sealed class MetapopulationOrchestrator
{
    private readonly CohortEvolutionEngine _cohorts;
    private readonly PopulationRouteCatalog _routes;
    private readonly PopulationBoundaryGate _boundary;
    private readonly CohortMigrationEngine _migration = new();
    private readonly LocalPopulationContinuityEngine _continuity = new();
    private readonly FounderEffectAnalyzer _founders = new();
    private readonly GeneticDivergenceAnalyzer _divergence = new();
    private readonly HabitatFragmentationAnalyzer _fragmentation = new();

    public MetapopulationOrchestrator(
        CohortEvolutionEngine cohorts,
        PopulationRouteCatalog routes,
        PopulationBoundaryGate boundary)
    {
        _cohorts = cohorts ?? throw new ArgumentNullException(nameof(cohorts));
        _routes = routes ?? throw new ArgumentNullException(nameof(routes));
        _boundary = boundary ?? throw new ArgumentNullException(nameof(boundary));
    }

    public MetapopulationGenerationResult AdvanceGeneration(
        IReadOnlyList<EvolutionCohort> source,
        IReadOnlyList<ZoneEnvironment> zones,
        ulong simulationSeed,
        MetapopulationSettings? settings = null,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>>? abundanceByZone = null,
        IReadOnlyDictionary<string, double>? externalTraitPressures = null,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>>? externalTraitPressuresByZone = null,
        IReadOnlyDictionary<string, double>? externalGrowthModifiersByZone = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(zones);
        settings ??= new MetapopulationSettings();
        if (source.Count == 0)
            throw new InvalidOperationException("Metapopulation simulation requires at least one cohort.");

        string speciesId = source[0].SpeciesId;
        if (source.Any(x => !StringComparer.Ordinal.Equals(x.SpeciesId, speciesId)))
            throw new InvalidOperationException("A metapopulation run may contain only one species.");

        var zoneMap = zones.ToDictionary(x => x.ZoneId, StringComparer.Ordinal);
        var patches = source.ToDictionary(x => x.ZoneId, x => x, StringComparer.Ordinal);
        var localResults = new List<CohortEvolutionResult>();
        var ecologyByZone = new Dictionary<string, SpeciesEcologyOutcome>(StringComparer.Ordinal);

        foreach (var cohort in source
                     .Where(x => x.Population > 0)
                     .OrderBy(x => x.ZoneId, StringComparer.Ordinal))
        {
            if (!zoneMap.TryGetValue(cohort.ZoneId, out var zone))
                throw new InvalidOperationException($"No authoritative zone state exists for {cohort.ZoneId}.");

            IReadOnlyDictionary<string, double> abundance = abundanceByZone is not null
                && abundanceByZone.TryGetValue(cohort.ZoneId, out var authoredAbundance)
                    ? authoredAbundance
                    : new Dictionary<string, double>(StringComparer.Ordinal)
                    {
                        [speciesId] = Math.Clamp(
                            cohort.Population / Math.Max(1.0, settings.AbundanceScale),
                            0.0,
                            1.0)
                    };

            var zoneTraitPressures = MergePressures(
                externalTraitPressures,
                externalTraitPressuresByZone is not null && externalTraitPressuresByZone.TryGetValue(cohort.ZoneId, out var localPressures)
                    ? localPressures
                    : null);
            double growthModifier = externalGrowthModifiersByZone is not null
                && externalGrowthModifiersByZone.TryGetValue(cohort.ZoneId, out var authoredGrowthModifier)
                    ? authoredGrowthModifier
                    : 1.0;

            var result = _cohorts.AdvanceGeneration(
                cohort,
                zone,
                simulationSeed,
                settings: null,
                speciesAbundance: abundance,
                externalTraitPressures: zoneTraitPressures,
                externalGrowthModifier: growthModifier);

            var transition = _continuity.Apply(
                result.Next,
                result.Ecology,
                settings.MinimumViablePopulation);

            patches[cohort.ZoneId] = transition.Cohort;
            ecologyByZone[cohort.ZoneId] = result.Ecology;
            localResults.Add(result with { Next = transition.Cohort });
        }

        var migrationSourceZones = patches.Values
            .Where(x => x.Population > 0)
            .Select(x => x.ZoneId)
            .ToHashSet(StringComparer.Ordinal);
        var migrationBudgets = patches.Values
            .Where(x => x.Population > 0)
            .ToDictionary(
                x => x.ZoneId,
                x => (long)Math.Floor(x.Population * Math.Clamp(settings.MaximumDispersalFraction, 0.0, 1.0)),
                StringComparer.Ordinal);

        var migrations = new List<CohortMigrationResult>();
        var geneFlow = new List<GeneFlowRecord>();
        var founderEffects = new List<FounderEffectReport>();

        foreach (var route in _routes.Routes.OrderBy(x => x.RouteId, StringComparer.Ordinal))
        {
            _boundary.Validate(route);
            if (route.Condition < settings.MinimumRouteCondition)
                continue;
            if (!zoneMap.ContainsKey(route.FromZoneId) || !zoneMap.ContainsKey(route.ToZoneId))
                continue;
            if (zoneMap[route.FromZoneId].SimulationState == CellSimulationState.Locked ||
                zoneMap[route.ToZoneId].SimulationState == CellSimulationState.Locked)
                continue;

            var direction = SelectDirection(
                route,
                patches,
                ecologyByZone,
                settings.MigrationPressureDifference);
            if (direction is null)
                continue;

            var (fromZoneId, toZoneId) = direction.Value;
            if (!migrationSourceZones.Contains(fromZoneId))
                continue;
            if (!patches.TryGetValue(fromZoneId, out var sourceCohort) || sourceCohort.Population <= 0)
                continue;

            patches.TryGetValue(toZoneId, out var destinationCohort);
            double pressure = ecologyByZone.TryGetValue(fromZoneId, out var ecology)
                ? ecology.MigrationPressure
                : 0.25;
            long requested = CalculateRequestedMigration(sourceCohort.Population, pressure, settings);
            long budget = migrationBudgets.GetValueOrDefault(fromZoneId);
            requested = Math.Min(requested, budget);
            if (requested <= 0)
                continue;

            var movement = _migration.Move(
                sourceCohort,
                destinationCohort,
                route,
                zoneMap[fromZoneId],
                zoneMap[toZoneId],
                requested,
                simulationSeed);

            patches[fromZoneId] = movement.SourceAfter;
            patches[toZoneId] = movement.DestinationAfter;
            migrationBudgets[fromZoneId] = Math.Max(0L, budget - movement.Departed);
            migrations.Add(movement);

            double retention = 1.0;
            if (movement.Recolonized)
            {
                var founder = _founders.Analyze(movement);
                founderEffects.Add(founder);
                retention = founder.DiversityRetention;
            }

            geneFlow.Add(new GeneFlowRecord(
                movement.RouteId,
                fromZoneId,
                toZoneId,
                movement.Departed,
                movement.Arrived,
                movement.Recolonized,
                retention,
                movement.Recolonized && movement.Arrived < settings.MinimumFounderPopulation,
                movement.Seed));
        }

        var finalCohorts = patches.Values
            .OrderBy(x => x.ZoneId, StringComparer.Ordinal)
            .ToArray();
        var divergence = _divergence.Analyze(finalCohorts);
        var fragmentation = _fragmentation.Analyze(
            speciesId,
            finalCohorts,
            _routes,
            settings.MinimumRouteCondition);
        int generation = finalCohorts.Select(x => x.Generation).DefaultIfEmpty(0).Max();

        return new MetapopulationGenerationResult(
            speciesId,
            generation,
            finalCohorts,
            localResults,
            migrations,
            geneFlow,
            founderEffects,
            divergence,
            fragmentation);
    }

    private static IReadOnlyDictionary<string, double>? MergePressures(
        IReadOnlyDictionary<string, double>? global,
        IReadOnlyDictionary<string, double>? local)
    {
        if (global is null && local is null)
            return null;

        var merged = new Dictionary<string, double>(StringComparer.Ordinal);
        if (global is not null)
        {
            foreach (var pressure in global.OrderBy(x => x.Key, StringComparer.Ordinal))
                merged[pressure.Key] = Math.Clamp(pressure.Value, -1.0, 1.0);
        }
        if (local is not null)
        {
            foreach (var pressure in local.OrderBy(x => x.Key, StringComparer.Ordinal))
                merged[pressure.Key] = Math.Clamp(
                    merged.GetValueOrDefault(pressure.Key) + pressure.Value,
                    -1.0,
                    1.0);
        }
        return merged;
    }

    private static (string From, string To)? SelectDirection(
        PopulationRoute route,
        IReadOnlyDictionary<string, EvolutionCohort> patches,
        IReadOnlyDictionary<string, SpeciesEcologyOutcome> ecology,
        double threshold)
    {
        patches.TryGetValue(route.FromZoneId, out var left);
        patches.TryGetValue(route.ToZoneId, out var right);
        bool leftActive = left is not null && left.Population > 0;
        bool rightActive = right is not null && right.Population > 0;
        if (!leftActive && !rightActive)
            return null;
        if (leftActive && !rightActive)
            return (route.FromZoneId, route.ToZoneId);
        if (!leftActive && rightActive)
            return route.Bidirectional ? (route.ToZoneId, route.FromZoneId) : null;

        double leftPressure = ecology.GetValueOrDefault(route.FromZoneId)?.MigrationPressure ?? 0.0;
        double rightPressure = ecology.GetValueOrDefault(route.ToZoneId)?.MigrationPressure ?? 0.0;
        if (Math.Abs(leftPressure - rightPressure) < Math.Clamp(threshold, 0.0, 1.0))
            return null;
        if (leftPressure > rightPressure)
            return (route.FromZoneId, route.ToZoneId);
        return route.Bidirectional ? (route.ToZoneId, route.FromZoneId) : null;
    }

    private static long CalculateRequestedMigration(
        long population,
        double migrationPressure,
        MetapopulationSettings settings)
    {
        if (population <= 0)
            return 0;
        double baseRate = Math.Clamp(settings.BaseDispersalRate, 0.0, 1.0);
        double maxFraction = Math.Clamp(settings.MaximumDispersalFraction, 0.0, 1.0);
        double fraction = Math.Clamp(
            baseRate * Math.Max(0.10, Math.Clamp(migrationPressure, 0.0, 1.0)),
            0.0,
            maxFraction);
        long requested = (long)Math.Round(population * fraction, MidpointRounding.AwayFromZero);
        return requested <= 0 && fraction > 0.0 ? 1 : Math.Min(population, requested);
    }
}
