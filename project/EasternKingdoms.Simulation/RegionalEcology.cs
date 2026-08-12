namespace EasternKingdoms.Simulation;

public enum TrophicRelationKind
{
    ResourceConsumption,
    Grazing,
    Predation,
    Scavenging,
    Decomposition,
    Parasitism,
    Mutualism
}

public sealed record TrophicLink(
    string ConsumerSpeciesId,
    string SourceId,
    FoodWebNodeKind SourceKind,
    TrophicRelationKind Relation,
    double Strength,
    double AssimilationEfficiency,
    double SourceMortalityPressure);

public sealed record TrophicNetworkDefinition(
    string NetworkId,
    IReadOnlyList<TrophicLink> Links);

public sealed record TrophicSpeciesOutcome(
    string SpeciesId,
    double IntakeSupport,
    double PredationLoad,
    double CompetitionLoad,
    double NetSupportModifier);

public sealed class TrophicNetworkValidator
{
    public void Validate(
        TrophicNetworkDefinition network,
        SpeciesEcologyCatalog ecology)
    {
        ArgumentNullException.ThrowIfNull(network);
        ArgumentNullException.ThrowIfNull(ecology);

        if (string.IsNullOrWhiteSpace(network.NetworkId))
            throw new InvalidOperationException("Trophic network requires an ID.");

        foreach (var link in network.Links)
        {
            if (string.IsNullOrWhiteSpace(link.ConsumerSpeciesId))
                throw new InvalidOperationException("Trophic link requires a consumer species ID.");
            if (string.IsNullOrWhiteSpace(link.SourceId))
                throw new InvalidOperationException("Trophic link requires a source ID.");
            if (link.Strength is <= 0.0 or > 1.0)
                throw new InvalidOperationException("Trophic link strength must be in (0, 1].");
            if (link.AssimilationEfficiency is < 0.0 or > 1.0)
                throw new InvalidOperationException("Assimilation efficiency must be in [0, 1].");
            if (link.SourceMortalityPressure is < 0.0 or > 1.0)
                throw new InvalidOperationException("Source mortality pressure must be in [0, 1].");

            var consumer = ecology.Get(link.ConsumerSpeciesId);
            if (consumer.Status != SpeciesEcologyStatus.Active)
                throw new InvalidOperationException($"Consumer species {link.ConsumerSpeciesId} is not ecologically active.");

            if (link.SourceKind == FoodWebNodeKind.Species)
            {
                var source = ecology.Get(link.SourceId);
                if (source.Status != SpeciesEcologyStatus.Active)
                    throw new InvalidOperationException($"Trophic source species {link.SourceId} is not ecologically active.");
            }
        }
    }
}

public sealed class TrophicNetworkResolver
{
    private readonly TrophicNetworkValidator _validator = new();

    public IReadOnlyList<TrophicSpeciesOutcome> Resolve(
        TrophicNetworkDefinition network,
        SpeciesEcologyCatalog ecology,
        ZoneFoodWebState state)
    {
        _validator.Validate(network, ecology);
        ArgumentNullException.ThrowIfNull(state);

        var intake = new Dictionary<string, double>(StringComparer.Ordinal);
        var predation = new Dictionary<string, double>(StringComparer.Ordinal);
        var competition = new Dictionary<string, double>(StringComparer.Ordinal);

        foreach (var group in network.Links
                     .GroupBy(x => x.SourceId, StringComparer.Ordinal))
        {
            double totalDemand = group.Sum(x => x.Strength);
            double competitionPenalty = Math.Clamp(totalDemand - 1.0, 0.0, 1.0);
            foreach (var link in group)
                competition[link.ConsumerSpeciesId] = Math.Max(
                    competition.GetValueOrDefault(link.ConsumerSpeciesId),
                    competitionPenalty * link.Strength);
        }

        foreach (var link in network.Links.OrderBy(x => x.ConsumerSpeciesId, StringComparer.Ordinal)
                     .ThenBy(x => x.SourceId, StringComparer.Ordinal))
        {
            double availability = link.SourceKind == FoodWebNodeKind.ResourcePool
                ? state.ResourcePools.GetValueOrDefault(link.SourceId)
                : state.SpeciesAbundance.GetValueOrDefault(link.SourceId);

            availability = Math.Clamp(availability, 0.0, 1.0);
            double gained = availability * link.Strength * link.AssimilationEfficiency;
            intake[link.ConsumerSpeciesId] = Math.Clamp(
                intake.GetValueOrDefault(link.ConsumerSpeciesId) + gained,
                0.0,
                1.0);

            if (link.SourceKind == FoodWebNodeKind.Species &&
                link.Relation is TrophicRelationKind.Predation or TrophicRelationKind.Parasitism)
            {
                double pressure = gained * link.SourceMortalityPressure;
                predation[link.SourceId] = Math.Clamp(
                    predation.GetValueOrDefault(link.SourceId) + pressure,
                    0.0,
                    1.0);
            }
        }

        var speciesIds = network.Links
            .SelectMany(link => link.SourceKind == FoodWebNodeKind.Species
                ? new[] { link.ConsumerSpeciesId, link.SourceId }
                : new[] { link.ConsumerSpeciesId })
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        return speciesIds
            .Select(speciesId =>
            {
                double intakeSupport = intake.GetValueOrDefault(speciesId);
                double predationLoad = predation.GetValueOrDefault(speciesId);
                double competitionLoad = Math.Clamp(competition.GetValueOrDefault(speciesId), 0.0, 1.0);
                double net = Math.Clamp(
                    0.50 + intakeSupport * 0.60 - predationLoad * 0.50 - competitionLoad * 0.25,
                    0.0,
                    1.25);

                return new TrophicSpeciesOutcome(
                    speciesId,
                    intakeSupport,
                    predationLoad,
                    competitionLoad,
                    net);
            })
            .ToArray();
    }
}

public sealed record GuildTrophicRule(
    EcologicalGuild ConsumerGuild,
    EcologicalGuild? SourceGuild,
    string? ResourcePoolId,
    TrophicRelationKind Relation,
    double Strength,
    double AssimilationEfficiency,
    double SourceMortalityPressure);

public sealed record TrophicGuildScaffold(
    string ScaffoldId,
    IReadOnlyList<GuildTrophicRule> Rules);

public static class DefaultTrophicGuildScaffold
{
    public static TrophicGuildScaffold Create() =>
        new(
            "azeroth.eastern-kingdoms.trophic-scaffold/1.0",
            new[]
            {
                new GuildTrophicRule(
                    EcologicalGuild.Producer,
                    SourceGuild: null,
                    ResourcePoolId: "soil_nutrients",
                    TrophicRelationKind.ResourceConsumption,
                    Strength: 0.55,
                    AssimilationEfficiency: 0.80,
                    SourceMortalityPressure: 0.0),
                new GuildTrophicRule(
                    EcologicalGuild.Grazer,
                    EcologicalGuild.Producer,
                    ResourcePoolId: null,
                    TrophicRelationKind.Grazing,
                    Strength: 0.65,
                    AssimilationEfficiency: 0.65,
                    SourceMortalityPressure: 0.18),
                new GuildTrophicRule(
                    EcologicalGuild.Predator,
                    EcologicalGuild.Grazer,
                    ResourcePoolId: null,
                    TrophicRelationKind.Predation,
                    Strength: 0.55,
                    AssimilationEfficiency: 0.70,
                    SourceMortalityPressure: 0.35),
                new GuildTrophicRule(
                    EcologicalGuild.Scavenger,
                    SourceGuild: null,
                    ResourcePoolId: "detritus",
                    TrophicRelationKind.Scavenging,
                    Strength: 0.50,
                    AssimilationEfficiency: 0.60,
                    SourceMortalityPressure: 0.0),
                new GuildTrophicRule(
                    EcologicalGuild.Decomposer,
                    SourceGuild: null,
                    ResourcePoolId: "detritus",
                    TrophicRelationKind.Decomposition,
                    Strength: 0.75,
                    AssimilationEfficiency: 0.80,
                    SourceMortalityPressure: 0.0),
                new GuildTrophicRule(
                    EcologicalGuild.SapientGeneralist,
                    SourceGuild: null,
                    ResourcePoolId: "food_support",
                    TrophicRelationKind.ResourceConsumption,
                    Strength: 0.50,
                    AssimilationEfficiency: 0.65,
                    SourceMortalityPressure: 0.0)
            });
}

public sealed class TrophicNetworkCompiler
{
    public TrophicNetworkDefinition Compile(
        TrophicGuildScaffold scaffold,
        SpeciesEcologyCatalog ecology,
        string networkId)
    {
        ArgumentNullException.ThrowIfNull(scaffold);
        ArgumentNullException.ThrowIfNull(ecology);
        if (string.IsNullOrWhiteSpace(networkId))
            throw new InvalidOperationException("Compiled trophic network requires an ID.");

        var active = ecology.Profiles
            .Where(x => x.Status == SpeciesEcologyStatus.Active)
            .OrderBy(x => x.SpeciesId, StringComparer.Ordinal)
            .ToArray();

        var links = new List<TrophicLink>();
        foreach (var rule in scaffold.Rules)
        {
            foreach (var consumer in active.Where(x => x.Guild == rule.ConsumerGuild))
            {
                if (!string.IsNullOrWhiteSpace(rule.ResourcePoolId))
                {
                    links.Add(new TrophicLink(
                        consumer.SpeciesId,
                        rule.ResourcePoolId,
                        FoodWebNodeKind.ResourcePool,
                        rule.Relation,
                        rule.Strength,
                        rule.AssimilationEfficiency,
                        rule.SourceMortalityPressure));
                    continue;
                }

                if (rule.SourceGuild is null)
                    continue;

                foreach (var source in active.Where(x => x.Guild == rule.SourceGuild))
                {
                    if (StringComparer.Ordinal.Equals(source.SpeciesId, consumer.SpeciesId))
                        continue;
                    links.Add(new TrophicLink(
                        consumer.SpeciesId,
                        source.SpeciesId,
                        FoodWebNodeKind.Species,
                        rule.Relation,
                        rule.Strength,
                        rule.AssimilationEfficiency,
                        rule.SourceMortalityPressure));
                }
            }
        }

        return new TrophicNetworkDefinition(networkId, links);
    }
}

public sealed class PopulationBoundaryGate
{
    private readonly HashSet<string> _authorizedZones;

    public PopulationBoundaryGate(IEnumerable<string> authorizedZoneIds)
    {
        _authorizedZones = authorizedZoneIds.ToHashSet(StringComparer.Ordinal);
        if (_authorizedZones.Count == 0)
            throw new InvalidOperationException("Population boundary requires at least one authorized zone.");
    }

    public void Validate(PopulationRoute route)
    {
        ArgumentNullException.ThrowIfNull(route);
        if (!_authorizedZones.Contains(route.FromZoneId) || !_authorizedZones.Contains(route.ToZoneId))
            throw new InvalidOperationException(
                $"Population route {route.RouteId} crosses the authorized Eastern Kingdoms simulation boundary.");
    }

    public static PopulationBoundaryGate NorthernScarCorridor() =>
        new(AzerothEvolutionBootstrap.CreateNorthernScarCorridor().Select(x => x.ZoneId));
}

public sealed record PopulationRoute(
    string RouteId,
    string FromZoneId,
    string ToZoneId,
    bool Bidirectional,
    long PopulationCapacityPerPhase,
    double Condition,
    double TravelRisk,
    double DiseaseTransmissionModifier);

public sealed class PopulationRouteCatalog
{
    private readonly Dictionary<string, PopulationRoute> _routes = new(StringComparer.Ordinal);

    public IReadOnlyCollection<PopulationRoute> Routes => _routes.Values;

    public void Register(PopulationRoute route)
    {
        ArgumentNullException.ThrowIfNull(route);
        if (string.IsNullOrWhiteSpace(route.RouteId))
            throw new InvalidOperationException("Population route requires an ID.");
        if (string.IsNullOrWhiteSpace(route.FromZoneId) || string.IsNullOrWhiteSpace(route.ToZoneId))
            throw new InvalidOperationException($"Route {route.RouteId} requires both endpoints.");
        if (StringComparer.Ordinal.Equals(route.FromZoneId, route.ToZoneId))
            throw new InvalidOperationException($"Route {route.RouteId} cannot connect a zone to itself.");
        if (route.PopulationCapacityPerPhase < 0)
            throw new InvalidOperationException($"Route {route.RouteId} has negative capacity.");
        if (route.Condition is < 0.0 or > 1.0)
            throw new InvalidOperationException($"Route {route.RouteId} has invalid condition.");
        if (route.TravelRisk is < 0.0 or > 1.0)
            throw new InvalidOperationException($"Route {route.RouteId} has invalid travel risk.");
        if (route.DiseaseTransmissionModifier is < 0.0 or > 1.0)
            throw new InvalidOperationException($"Route {route.RouteId} has invalid disease transmission modifier.");
        if (!_routes.TryAdd(route.RouteId, route))
            throw new InvalidOperationException($"Population route {route.RouteId} already exists.");
    }

    public PopulationRoute Get(string routeId) =>
        _routes.TryGetValue(routeId, out var route)
            ? route
            : throw new KeyNotFoundException($"Unknown population route {routeId}.");

    public bool Connects(PopulationRoute route, string fromZoneId, string toZoneId) =>
        (StringComparer.Ordinal.Equals(route.FromZoneId, fromZoneId) &&
         StringComparer.Ordinal.Equals(route.ToZoneId, toZoneId)) ||
        (route.Bidirectional &&
         StringComparer.Ordinal.Equals(route.FromZoneId, toZoneId) &&
         StringComparer.Ordinal.Equals(route.ToZoneId, fromZoneId));
}

public sealed record CohortMigrationResult(
    string RouteId,
    EvolutionCohort SourceBefore,
    EvolutionCohort SourceAfter,
    EvolutionCohort? DestinationBefore,
    EvolutionCohort DestinationAfter,
    long Departed,
    long Arrived,
    long TransitLosses,
    bool Recolonized,
    ulong Seed);

public sealed class CohortMigrationEngine
{
    public CohortMigrationResult Move(
        EvolutionCohort source,
        EvolutionCohort? destination,
        PopulationRoute route,
        ZoneEnvironment fromZone,
        ZoneEnvironment toZone,
        long requestedPopulation,
        ulong simulationSeed)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(fromZone);
        ArgumentNullException.ThrowIfNull(toZone);

        if (requestedPopulation < 0)
            throw new ArgumentOutOfRangeException(nameof(requestedPopulation));
        if (fromZone.SimulationState == CellSimulationState.Locked ||
            toZone.SimulationState == CellSimulationState.Locked)
            throw new InvalidOperationException("Locked cells cannot participate in cohort migration.");
        if (!StringComparer.Ordinal.Equals(source.ZoneId, fromZone.ZoneId))
            throw new InvalidOperationException("Source cohort and source zone do not match.");

        bool direct = StringComparer.Ordinal.Equals(route.FromZoneId, fromZone.ZoneId) &&
                      StringComparer.Ordinal.Equals(route.ToZoneId, toZone.ZoneId);
        bool reverse = route.Bidirectional &&
                       StringComparer.Ordinal.Equals(route.ToZoneId, fromZone.ZoneId) &&
                       StringComparer.Ordinal.Equals(route.FromZoneId, toZone.ZoneId);
        if (!direct && !reverse)
            throw new InvalidOperationException($"Route {route.RouteId} does not connect {fromZone.ZoneId} to {toZone.ZoneId}.");

        if (destination is not null &&
            !StringComparer.Ordinal.Equals(destination.SpeciesId, source.SpeciesId))
            throw new InvalidOperationException("Migration destination cohort must use the same species profile.");
        if (destination is not null &&
            !StringComparer.Ordinal.Equals(destination.ZoneId, toZone.ZoneId))
            throw new InvalidOperationException("Destination cohort and destination zone do not match.");

        long capacity = (long)Math.Floor(route.PopulationCapacityPerPhase * route.Condition);
        long departed = Math.Min(Math.Min(requestedPopulation, source.Population), Math.Max(0L, capacity));

        ulong seed = StableSeed.FromText(
            $"{simulationSeed}|migration|{route.RouteId}|{source.CohortId}|{fromZone.ZoneId}|{toZone.ZoneId}|{source.Generation}");
        var random = new Evermore.Genetics.GenomeRandom(seed);

        double environmentalRisk = Math.Clamp(
            route.TravelRisk * (0.50 + toZone.ResonanceDebt * 0.25 + (1.0 - toZone.Integrity) * 0.25),
            0.0,
            0.95);
        double expectedArrivals = departed * (1.0 - environmentalRisk);
        long arrived = StochasticRound(expectedArrivals, random);
        arrived = Math.Clamp(arrived, 0L, departed);

        var sourceAfter = source with
        {
            Population = source.Population - departed
        };

        EvolutionCohort destinationAfter;
        bool recolonized = destination is null || destination.Population <= 0;
        if (destination is null || destination.Population <= 0)
        {
            var founderFrequencies = SampleFounderFrequencies(
                source.AlleleFrequencies,
                arrived,
                random);

            destinationAfter = new EvolutionCohort(
                $"{source.CohortId}@{toZone.ZoneId}",
                source.SpeciesId,
                toZone.ZoneId,
                source.Generation,
                arrived,
                founderFrequencies);
        }
        else
        {
            destinationAfter = Merge(destination, source.AlleleFrequencies, arrived);
        }

        return new CohortMigrationResult(
            route.RouteId,
            source,
            sourceAfter,
            destination,
            destinationAfter,
            departed,
            arrived,
            departed - arrived,
            recolonized && arrived > 0,
            seed);
    }

    private static IReadOnlyList<CohortAlleleFrequency> SampleFounderFrequencies(
        IReadOnlyList<CohortAlleleFrequency> source,
        long founderPopulation,
        Evermore.Genetics.GenomeRandom random)
    {
        if (founderPopulation <= 0)
            return source.ToArray();

        long requestedCopies = founderPopulation > 2048L ? 4096L : founderPopulation * 2L;
        int geneCopies = (int)Math.Min(4096L, Math.Max(2L, requestedCopies));
        var output = new List<CohortAlleleFrequency>();

        foreach (var locusGroup in source
                     .GroupBy(x => x.LocusId, StringComparer.Ordinal)
                     .OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            var alleles = locusGroup
                .OrderBy(x => x.AlleleId, StringComparer.Ordinal)
                .ToArray();
            if (alleles.Length == 0)
                continue;

            double total = alleles.Sum(x => Math.Max(0.0, x.Frequency));
            if (total <= 0.0)
            {
                double equal = 1.0 / alleles.Length;
                output.AddRange(alleles.Select(x =>
                    new CohortAlleleFrequency(x.LocusId, x.AlleleId, equal)));
                continue;
            }

            var counts = alleles.ToDictionary(x => x.AlleleId, _ => 0, StringComparer.Ordinal);
            var cumulative = new double[alleles.Length];
            double running = 0.0;
            for (int i = 0; i < alleles.Length; i++)
            {
                running += Math.Max(0.0, alleles[i].Frequency) / total;
                cumulative[i] = running;
            }
            cumulative[^1] = 1.0;

            for (int copy = 0; copy < geneCopies; copy++)
            {
                double roll = random.NextDouble();
                int index = 0;
                while (index < cumulative.Length - 1 && roll > cumulative[index])
                    index++;
                counts[alleles[index].AlleleId]++;
            }

            foreach (var allele in alleles)
            {
                output.Add(new CohortAlleleFrequency(
                    allele.LocusId,
                    allele.AlleleId,
                    counts[allele.AlleleId] / (double)geneCopies));
            }
        }

        return output;
    }

    private static EvolutionCohort Merge(
        EvolutionCohort destination,
        IReadOnlyList<CohortAlleleFrequency> migrantFrequencies,
        long migrants)
    {
        if (migrants <= 0)
            return destination;

        long totalPopulation = destination.Population + migrants;
        var destinationMap = destination.AlleleFrequencies
            .ToDictionary(x => (x.LocusId, x.AlleleId), x => x.Frequency);
        var migrantMap = migrantFrequencies
            .ToDictionary(x => (x.LocusId, x.AlleleId), x => x.Frequency);

        var keys = destinationMap.Keys
            .Concat(migrantMap.Keys)
            .Distinct()
            .OrderBy(x => x.LocusId, StringComparer.Ordinal)
            .ThenBy(x => x.AlleleId, StringComparer.Ordinal)
            .ToArray();

        var frequencies = keys
            .Select(key =>
            {
                double residentCount = destinationMap.GetValueOrDefault(key) * destination.Population;
                double migrantCount = migrantMap.GetValueOrDefault(key) * migrants;
                return new CohortAlleleFrequency(
                    key.LocusId,
                    key.AlleleId,
                    (residentCount + migrantCount) / totalPopulation);
            })
            .ToArray();

        return destination with
        {
            Population = totalPopulation,
            AlleleFrequencies = frequencies
        };
    }

    private static long StochasticRound(double value, Evermore.Genetics.GenomeRandom random)
    {
        if (value <= 0.0)
            return 0;
        long floor = (long)Math.Floor(value);
        double remainder = value - floor;
        return floor + (random.Chance(remainder) ? 1L : 0L);
    }
}

public sealed record DiseaseProfile(
    string DiseaseId,
    IReadOnlySet<string> HostSpeciesIds,
    double BaseTransmission,
    double RecoveryRate,
    double MortalityRate,
    double EnvironmentalSensitivity);

public sealed record DiseaseState(
    string ZoneId,
    string SpeciesId,
    string DiseaseId,
    double Prevalence);

public sealed record DiseaseOutcome(
    DiseaseState Previous,
    DiseaseState Next,
    double ImportedPressure,
    double LocalTransmission,
    double MortalityPressure);

public sealed class DiseaseTransmissionEngine
{
    public IReadOnlyList<DiseaseOutcome> Advance(
        DiseaseProfile profile,
        IReadOnlyList<DiseaseState> states,
        IReadOnlyList<CohortMigrationResult> migrations,
        IReadOnlyList<PopulationRoute> routes,
        IReadOnlyList<ZoneEnvironment> zones)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (profile.BaseTransmission is < 0.0 or > 1.0 ||
            profile.RecoveryRate is < 0.0 or > 1.0 ||
            profile.MortalityRate is < 0.0 or > 1.0 ||
            profile.EnvironmentalSensitivity is < 0.0 or > 1.0)
            throw new InvalidOperationException($"Disease profile {profile.DiseaseId} has invalid bounded values.");

        var zoneMap = zones.ToDictionary(x => x.ZoneId, StringComparer.Ordinal);
        var routeMap = routes.ToDictionary(x => x.RouteId, StringComparer.Ordinal);
        var stateMap = states.ToDictionary(x => (x.ZoneId, x.SpeciesId, x.DiseaseId));
        var outcomes = new List<DiseaseOutcome>();

        foreach (var current in states
                     .Where(x => StringComparer.Ordinal.Equals(x.DiseaseId, profile.DiseaseId))
                     .OrderBy(x => x.ZoneId, StringComparer.Ordinal)
                     .ThenBy(x => x.SpeciesId, StringComparer.Ordinal))
        {
            if (!profile.HostSpeciesIds.Contains(current.SpeciesId))
                continue;
            var zone = zoneMap[current.ZoneId];
            if (zone.SimulationState == CellSimulationState.Locked)
                continue;

            double prevalence = Math.Clamp(current.Prevalence, 0.0, 1.0);
            double diseaseEnvironment = Math.Clamp(
                (1.0 - zone.PopulationHealth) * 0.60 +
                (1.0 - zone.Stability) * 0.20 +
                zone.ResonanceDebt * profile.EnvironmentalSensitivity * 0.20,
                0.0,
                1.0);

            double local = profile.BaseTransmission * prevalence * (1.0 - prevalence) * (0.50 + diseaseEnvironment * 0.50);
            double imported = 0.0;

            foreach (var movement in migrations.Where(x =>
                         StringComparer.Ordinal.Equals(x.DestinationAfter.ZoneId, current.ZoneId) &&
                         StringComparer.Ordinal.Equals(x.DestinationAfter.SpeciesId, current.SpeciesId) &&
                         x.Arrived > 0))
            {
                if (!routeMap.TryGetValue(movement.RouteId, out var route))
                    continue;
                if (!stateMap.TryGetValue((movement.SourceBefore.ZoneId, current.SpeciesId, profile.DiseaseId), out var sourceDisease))
                    continue;

                double destinationPopulation = Math.Max(1.0, movement.DestinationAfter.Population);
                double migrantFraction = movement.Arrived / destinationPopulation;
                imported += Math.Clamp(sourceDisease.Prevalence, 0.0, 1.0)
                    * migrantFraction
                    * route.DiseaseTransmissionModifier;
            }

            imported = Math.Clamp(imported, 0.0, 1.0);
            double recovered = prevalence * profile.RecoveryRate;
            double nextPrevalence = Math.Clamp(prevalence + local + imported - recovered, 0.0, 1.0);
            double mortality = Math.Clamp(nextPrevalence * profile.MortalityRate, 0.0, 1.0);

            outcomes.Add(new DiseaseOutcome(
                current,
                current with { Prevalence = nextPrevalence },
                imported,
                local,
                mortality));
        }

        return outcomes;
    }
}

public enum LocalPopulationStatus
{
    Extant,
    Recolonizing,
    Extinct
}

public sealed record LocalPopulationAssessment(
    string ZoneId,
    string SpeciesId,
    LocalPopulationStatus Status,
    long Population,
    long MinimumViablePopulation,
    string Reason);

public sealed record LocalPopulationTransition(
    LocalPopulationAssessment Assessment,
    EvolutionCohort Cohort);

public sealed class LocalPopulationContinuityEngine
{
    public LocalPopulationTransition Apply(
        EvolutionCohort cohort,
        SpeciesEcologyOutcome ecology,
        long minimumViablePopulation)
    {
        var assessment = Assess(cohort, ecology, minimumViablePopulation);
        var next = assessment.Status == LocalPopulationStatus.Extinct
            ? cohort with { Population = 0 }
            : cohort;
        return new LocalPopulationTransition(assessment, next);
    }

    public LocalPopulationAssessment Assess(
        EvolutionCohort cohort,
        SpeciesEcologyOutcome ecology,
        long minimumViablePopulation)
    {
        ArgumentNullException.ThrowIfNull(cohort);
        ArgumentNullException.ThrowIfNull(ecology);
        if (minimumViablePopulation < 1)
            throw new ArgumentOutOfRangeException(nameof(minimumViablePopulation));

        if (cohort.Population <= 0)
        {
            return new LocalPopulationAssessment(
                cohort.ZoneId,
                cohort.SpeciesId,
                LocalPopulationStatus.Extinct,
                cohort.Population,
                minimumViablePopulation,
                "No local cohort remains.");
        }

        if (cohort.Population < minimumViablePopulation && ecology.ViabilityModifier < 0.45)
        {
            return new LocalPopulationAssessment(
                cohort.ZoneId,
                cohort.SpeciesId,
                LocalPopulationStatus.Extinct,
                cohort.Population,
                minimumViablePopulation,
                "Population is below the authored minimum viable threshold under low viability.");
        }

        if (cohort.Population < minimumViablePopulation)
        {
            return new LocalPopulationAssessment(
                cohort.ZoneId,
                cohort.SpeciesId,
                LocalPopulationStatus.Recolonizing,
                cohort.Population,
                minimumViablePopulation,
                "Population is below the viable threshold but habitat conditions permit recolonization.");
        }

        return new LocalPopulationAssessment(
            cohort.ZoneId,
            cohort.SpeciesId,
            LocalPopulationStatus.Extant,
            cohort.Population,
            minimumViablePopulation,
            "Local population is above the minimum viable threshold.");
    }
}

public enum SuccessionStage
{
    Sterile,
    Pioneer,
    Establishing,
    Mature,
    Disturbed,
    Recovering
}

public sealed record ZoneSuccessionState(
    string ZoneId,
    SuccessionStage Stage,
    int StablePhases,
    double BiomassIndex,
    double DecomposerIndex,
    double DisturbanceIndex);

public sealed class EcologicalSuccessionEngine
{
    public ZoneSuccessionState Advance(
        ZoneSuccessionState previous,
        ZoneEnvironment zone,
        ZoneFoodWebState foodWeb)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(zone);
        ArgumentNullException.ThrowIfNull(foodWeb);
        if (!StringComparer.Ordinal.Equals(previous.ZoneId, zone.ZoneId) ||
            !StringComparer.Ordinal.Equals(previous.ZoneId, foodWeb.ZoneId))
            throw new InvalidOperationException("Succession state, zone, and food-web state must share a zone ID.");
        if (zone.SimulationState == CellSimulationState.Locked)
            return previous;

        double biomass = Math.Clamp(foodWeb.ResourcePools.GetValueOrDefault("primary_biomass"), 0.0, 1.0);
        double decomposers = Math.Clamp(foodWeb.ResourcePools.GetValueOrDefault("decomposer_capacity"), 0.0, 1.0);
        double disturbance = Math.Clamp(
            foodWeb.Hazards.GetValueOrDefault("contamination") * 0.40 +
            foodWeb.Hazards.GetValueOrDefault("disease") * 0.20 +
            foodWeb.Hazards.GetValueOrDefault("resonance_stress") * 0.40,
            0.0,
            1.0);

        bool improving = zone.Stability >= 0.50 && zone.Integrity >= 0.50 && disturbance < 0.55;
        int stablePhases = improving ? previous.StablePhases + 1 : 0;
        SuccessionStage next = ResolveStage(previous.Stage, stablePhases, biomass, decomposers, disturbance);

        return new ZoneSuccessionState(
            zone.ZoneId,
            next,
            stablePhases,
            biomass,
            decomposers,
            disturbance);
    }

    private static SuccessionStage ResolveStage(
        SuccessionStage previous,
        int stablePhases,
        double biomass,
        double decomposers,
        double disturbance)
    {
        if (disturbance >= 0.80)
            return SuccessionStage.Disturbed;
        if (biomass < 0.15 && decomposers < 0.20)
            return SuccessionStage.Sterile;
        if (previous == SuccessionStage.Disturbed && disturbance < 0.65)
            return SuccessionStage.Recovering;
        if (stablePhases >= 8 && biomass >= 0.70 && decomposers >= 0.55)
            return SuccessionStage.Mature;
        if (stablePhases >= 4 && biomass >= 0.45)
            return SuccessionStage.Establishing;
        if (stablePhases >= 1 && biomass >= 0.20)
            return previous == SuccessionStage.Mature ? SuccessionStage.Mature : SuccessionStage.Pioneer;
        return previous;
    }
}

public static class NorthernScarPopulationRoutes
{
    public static PopulationRouteCatalog Create()
    {
        var catalog = new PopulationRouteCatalog();
        catalog.Register(new PopulationRoute(
            "ek.route.eversong-ghostlands",
            "ek.north.eversong",
            "ek.north.ghostlands",
            Bidirectional: true,
            PopulationCapacityPerPhase: 1000,
            Condition: 0.62,
            TravelRisk: 0.07,
            DiseaseTransmissionModifier: 0.35));

        catalog.Register(new PopulationRoute(
            "ek.route.ghostlands-western-plaguelands",
            "ek.north.ghostlands",
            "ek.north.western-plaguelands",
            Bidirectional: true,
            PopulationCapacityPerPhase: 700,
            Condition: 0.45,
            TravelRisk: 0.14,
            DiseaseTransmissionModifier: 0.50));

        catalog.Register(new PopulationRoute(
            "ek.route.western-eastern-plaguelands",
            "ek.north.western-plaguelands",
            "ek.north.eastern-plaguelands",
            Bidirectional: true,
            PopulationCapacityPerPhase: 500,
            Condition: 0.39,
            TravelRisk: 0.18,
            DiseaseTransmissionModifier: 0.60));
        return catalog;
    }
}
