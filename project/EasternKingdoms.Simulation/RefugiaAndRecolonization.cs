namespace EasternKingdoms.Simulation;

public sealed class RefugiumTracker
{
    private readonly BiogeographySettings _settings;
    private readonly Dictionary<string, RefugiumRecord> _records = new(StringComparer.Ordinal);

    public RefugiumTracker(BiogeographySettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _settings.Validate();
    }

    public IReadOnlyCollection<RefugiumRecord> Records =>
        _records.Values.OrderBy(x => x.RefugiumId, StringComparer.Ordinal).ToArray();

    public IReadOnlyList<RefugiumRecord> Observe(
        LineageRangeState range,
        IReadOnlyDictionary<string, HabitatZoneState> habitats)
    {
        ArgumentNullException.ThrowIfNull(range);
        ArgumentNullException.ThrowIfNull(habitats);
        if (habitats.Count == 0)
            return Array.Empty<RefugiumRecord>();

        double regionalMean = habitats.Values.Average(x => x.Suitability);
        bool regionalStress = regionalMean < _settings.RefugiumSuitabilityThreshold;
        var changed = new List<RefugiumRecord>();

        foreach (string zoneId in range.OccupiedZones.OrderBy(x => x, StringComparer.Ordinal))
        {
            if (!habitats.TryGetValue(zoneId, out var habitat))
                continue;

            string key = $"{range.LineageId}|{zoneId}";
            bool qualifies = regionalStress && habitat.Suitability >= _settings.RefugiumSuitabilityThreshold;
            if (!qualifies)
                continue;

            if (_records.TryGetValue(key, out var previous) && previous.LastObservedGeneration == range.LastObservedGeneration - 1)
            {
                int count = previous.ConsecutiveGenerations + 1;
                double mean = ((previous.MeanSuitability * previous.ConsecutiveGenerations) + habitat.Suitability) / count;
                var next = previous with
                {
                    LastObservedGeneration = range.LastObservedGeneration,
                    ConsecutiveGenerations = count,
                    MeanSuitability = mean,
                    Persistent = count >= _settings.MinimumRefugiumPersistenceGenerations,
                    Authority = previous.Authority == DeepTimeLineageAuthority.Rejected
                        ? DeepTimeLineageAuthority.Rejected
                        : DeepTimeLineageLedger.MaxAuthority(previous.Authority, range.Authority)
                };
                _records[key] = next;
                changed.Add(next);
            }
            else
            {
                var created = new RefugiumRecord(
                    $"refugium:{range.LineageId}:{zoneId}",
                    range.LineageId,
                    zoneId,
                    range.LastObservedGeneration,
                    range.LastObservedGeneration,
                    1,
                    habitat.Suitability,
                    _settings.MinimumRefugiumPersistenceGenerations <= 1,
                    range.Authority);
                _records[key] = created;
                changed.Add(created);
            }
        }

        return changed;
    }

    public bool IsPersistent(string lineageId, string zoneId) =>
        _records.TryGetValue($"{lineageId}|{zoneId}", out var value) && value.Persistent;
}

public sealed class RecolonizationWaveDetector
{
    public IReadOnlyList<RecolonizationWaveEvent> Detect(
        LineageRangeTransition transition,
        IEnumerable<HabitatCorridorState> corridors,
        PopulationRouteCatalog routes,
        RefugiumTracker refugia)
    {
        ArgumentNullException.ThrowIfNull(transition);
        ArgumentNullException.ThrowIfNull(corridors);
        ArgumentNullException.ThrowIfNull(routes);
        ArgumentNullException.ThrowIfNull(refugia);

        if (transition.Previous is null)
            return Array.Empty<RecolonizationWaveEvent>();

        var recolonizations = transition.Events
            .Where(x => x.Kind == RangeShiftKind.Recolonization)
            .SelectMany(x => x.AddedZones)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();
        if (recolonizations.Length == 0)
            return Array.Empty<RecolonizationWaveEvent>();

        var open = corridors.Where(x => x.Open).ToDictionary(x => x.RouteId, StringComparer.Ordinal);
        var output = new List<RecolonizationWaveEvent>();

        foreach (string destination in recolonizations)
        {
            var candidates = routes.Routes
                .Where(route => open.ContainsKey(route.RouteId))
                .SelectMany(route => transition.Previous.OccupiedZones
                    .Where(source => routes.Connects(route, source, destination))
                    .Select(source => (Route: route, Source: source)))
                .OrderByDescending(x => refugia.IsPersistent(transition.Current.LineageId, x.Source))
                .ThenByDescending(x => x.Route.Condition)
                .ThenBy(x => x.Route.RouteId, StringComparer.Ordinal)
                .ToArray();

            if (candidates.Length == 0)
                continue;

            var selected = candidates[0];
            long estimated = Math.Min(
                selected.Route.PopulationCapacityPerPhase,
                Math.Max(1L, transition.Current.Population / Math.Max(2, transition.Current.OccupiedZones.Count * 5)));

            output.Add(new RecolonizationWaveEvent(
                $"recolonization:{transition.Current.LineageId}:{selected.Source}->{destination}:g{transition.Current.LastObservedGeneration:D8}",
                transition.Current.LineageId,
                selected.Source,
                destination,
                selected.Route.RouteId,
                transition.Current.LastObservedGeneration,
                estimated,
                refugia.IsPersistent(transition.Current.LineageId, selected.Source),
                transition.Current.Authority));
        }

        return output;
    }
}
