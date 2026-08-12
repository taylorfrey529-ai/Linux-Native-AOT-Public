namespace EasternKingdoms.Simulation;

public sealed class HabitatSuitabilityResolver
{
    public HabitatZoneState Resolve(ZoneEnvironment zone, int generation)
    {
        ArgumentNullException.ThrowIfNull(zone);
        if (generation < 0)
            throw new ArgumentOutOfRangeException(nameof(generation));

        double disturbance = Math.Clamp(
            (1.0 - zone.PopulationHealth) * 0.15 +
            (1.0 - zone.Stability) * 0.25 +
            (1.0 - zone.Integrity) * 0.30 +
            zone.ResonanceDebt * 0.30,
            0.0,
            1.0);

        double suitability = Math.Clamp(
            zone.PopulationHealth * 0.20 +
            zone.Stability * 0.25 +
            zone.Integrity * 0.25 +
            zone.Luminosity * 0.15 +
            (1.0 - zone.ResonanceDebt) * 0.15,
            0.0,
            1.0);

        return new HabitatZoneState(
            zone.ZoneId,
            generation,
            suitability,
            disturbance,
            zone.SimulationState != CellSimulationState.Locked && suitability >= 0.50);
    }
}

public sealed class HabitatCorridorResolver
{
    private readonly BiogeographySettings _settings;

    public HabitatCorridorResolver(BiogeographySettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _settings.Validate();
    }

    public IReadOnlyList<HabitatCorridorState> Resolve(
        PopulationRouteCatalog routes,
        IReadOnlyDictionary<string, HabitatZoneState> habitats,
        int generation)
    {
        ArgumentNullException.ThrowIfNull(routes);
        ArgumentNullException.ThrowIfNull(habitats);

        return routes.Routes
            .OrderBy(x => x.RouteId, StringComparer.Ordinal)
            .Select(route =>
            {
                if (!habitats.TryGetValue(route.FromZoneId, out var from) ||
                    !habitats.TryGetValue(route.ToZoneId, out var to))
                    throw new InvalidOperationException($"Route {route.RouteId} references a zone without habitat state.");

                double permeability = Math.Clamp(
                    route.Condition * 0.45 +
                    Math.Min(from.Suitability, to.Suitability) * 0.40 +
                    (1.0 - route.TravelRisk) * 0.15,
                    0.0,
                    1.0);

                return new HabitatCorridorState(
                    route.RouteId,
                    route.FromZoneId,
                    route.ToZoneId,
                    generation,
                    permeability,
                    permeability >= _settings.CorridorOpenThreshold);
            })
            .ToArray();
    }
}

public sealed class BiogeographicRangeLedger
{
    private readonly HashSet<string> _authorizedZones;
    private readonly Dictionary<string, LineageRangeState> _states = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> _everOccupied = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _lastGeneration = new(StringComparer.Ordinal);

    public BiogeographicRangeLedger(IEnumerable<string> authorizedZones)
    {
        _authorizedZones = authorizedZones
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.Ordinal);

        if (_authorizedZones.Count == 0)
            throw new InvalidOperationException("Biogeographic range ledger requires at least one authorized zone.");
    }

    public IReadOnlyCollection<LineageRangeState> States =>
        _states.Values.OrderBy(x => x.LineageId, StringComparer.Ordinal).ToArray();

    public LineageRangeTransition Observe(DeepTimeLineageObservation observation, int connectedComponents)
    {
        ArgumentNullException.ThrowIfNull(observation);
        if (connectedComponents < 0)
            throw new ArgumentOutOfRangeException(nameof(connectedComponents));

        foreach (string zoneId in observation.OccupiedZones)
        {
            if (!_authorizedZones.Contains(zoneId))
                throw new InvalidOperationException($"Lineage {observation.LineageId} occupies unauthorized zone {zoneId}.");
        }

        if (_lastGeneration.TryGetValue(observation.LineageId, out int last) && observation.Generation <= last)
            throw new InvalidOperationException($"Range observations for {observation.LineageId} must advance monotonically.");
        _lastGeneration[observation.LineageId] = observation.Generation;

        _states.TryGetValue(observation.LineageId, out var previous);
        if (previous is not null && !StringComparer.Ordinal.Equals(previous.SpeciesFamilyId, observation.SpeciesFamilyId))
            throw new InvalidOperationException($"Lineage {observation.LineageId} cannot change species-family identity across biogeographic history.");

        var occupied = observation.OccupiedZones.ToHashSet(StringComparer.Ordinal);
        var ever = _everOccupied.GetValueOrDefault(observation.LineageId);
        if (ever is null)
        {
            ever = new HashSet<string>(StringComparer.Ordinal);
            _everOccupied[observation.LineageId] = ever;
        }

        int first = previous?.FirstObservedGeneration ?? observation.Generation;
        var current = new LineageRangeState(
            observation.LineageId,
            observation.SpeciesFamilyId,
            first,
            observation.Generation,
            observation.Population,
            occupied,
            previous?.Authority == DeepTimeLineageAuthority.Rejected
                ? DeepTimeLineageAuthority.Rejected
                : DeepTimeLineageLedger.MaxAuthority(previous?.Authority ?? observation.Authority, observation.Authority),
            connectedComponents);

        var events = new List<RangeShiftEvent>();
        if (previous is not null)
        {
            var added = occupied.Except(previous.OccupiedZones, StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
            var removed = previous.OccupiedZones.Except(occupied, StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);

            if (added.Count > 0)
            {
                bool recolonized = added.Any(ever.Contains);
                events.Add(CreateEvent(current, recolonized ? RangeShiftKind.Recolonization : RangeShiftKind.Expansion, added, EmptySet(), previous.ConnectedComponents));
            }
            if (removed.Count > 0)
            {
                var kind = occupied.Count == 0 ? RangeShiftKind.LocalExtirpation : RangeShiftKind.Contraction;
                events.Add(CreateEvent(current, kind, EmptySet(), removed, previous.ConnectedComponents));
            }
            if (previous.ConnectedComponents <= 1 && connectedComponents > 1)
                events.Add(CreateEvent(current, RangeShiftKind.Fragmentation, EmptySet(), EmptySet(), previous.ConnectedComponents));
            if (previous.ConnectedComponents > 1 && connectedComponents <= 1 && occupied.Count > 1)
                events.Add(CreateEvent(current, RangeShiftKind.Reconnection, EmptySet(), EmptySet(), previous.ConnectedComponents));
        }

        ever.UnionWith(occupied);
        _states[observation.LineageId] = current;
        return new LineageRangeTransition(previous, current, events);
    }

    private static RangeShiftEvent CreateEvent(
        LineageRangeState current,
        RangeShiftKind kind,
        IReadOnlySet<string> added,
        IReadOnlySet<string> removed,
        int previousComponents) =>
        new(
            $"range:{current.LineageId}:{kind.ToString().ToLowerInvariant()}:g{current.LastObservedGeneration:D8}",
            current.LineageId,
            kind,
            current.LastObservedGeneration,
            added,
            removed,
            previousComponents,
            current.ConnectedComponents,
            current.Authority);

    private static IReadOnlySet<string> EmptySet() => new HashSet<string>(StringComparer.Ordinal);
}

public sealed class RangeConnectivityAnalyzer
{
    public int CountComponents(
        IReadOnlySet<string> occupiedZones,
        IEnumerable<HabitatCorridorState> corridors)
    {
        if (occupiedZones.Count == 0)
            return 0;

        var adjacency = occupiedZones.ToDictionary(
            x => x,
            _ => new HashSet<string>(StringComparer.Ordinal),
            StringComparer.Ordinal);

        foreach (var corridor in corridors.Where(x => x.Open))
        {
            if (!occupiedZones.Contains(corridor.FromZoneId) || !occupiedZones.Contains(corridor.ToZoneId))
                continue;
            adjacency[corridor.FromZoneId].Add(corridor.ToZoneId);
            adjacency[corridor.ToZoneId].Add(corridor.FromZoneId);
        }

        int components = 0;
        var visited = new HashSet<string>(StringComparer.Ordinal);
        foreach (string start in occupiedZones.OrderBy(x => x, StringComparer.Ordinal))
        {
            if (!visited.Add(start))
                continue;
            components++;
            var queue = new Queue<string>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                string current = queue.Dequeue();
                foreach (string next in adjacency[current])
                {
                    if (visited.Add(next))
                        queue.Enqueue(next);
                }
            }
        }
        return components;
    }
}
