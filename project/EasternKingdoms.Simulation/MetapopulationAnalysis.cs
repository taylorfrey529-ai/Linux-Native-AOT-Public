namespace EasternKingdoms.Simulation;

public sealed record HabitatFragmentationReport(
    string SpeciesId,
    int ConnectedComponents,
    IReadOnlyList<IReadOnlyList<string>> Components,
    IReadOnlyList<string> IsolatedZones,
    double MeanRoutePermeability);

public sealed class HabitatFragmentationAnalyzer
{
    public HabitatFragmentationReport Analyze(
        string speciesId,
        IEnumerable<EvolutionCohort> cohorts,
        PopulationRouteCatalog routes,
        double minimumRouteCondition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(speciesId);
        ArgumentNullException.ThrowIfNull(routes);
        minimumRouteCondition = Math.Clamp(minimumRouteCondition, 0.0, 1.0);

        var zones = cohorts
            .Where(x => x.Population > 0 && StringComparer.Ordinal.Equals(x.SpeciesId, speciesId))
            .Select(x => x.ZoneId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        var zoneSet = zones.ToHashSet(StringComparer.Ordinal);
        var adjacency = zones.ToDictionary(
            x => x,
            _ => new HashSet<string>(StringComparer.Ordinal),
            StringComparer.Ordinal);
        var permeabilities = new List<double>();

        foreach (var route in routes.Routes.OrderBy(x => x.RouteId, StringComparer.Ordinal))
        {
            if (!zoneSet.Contains(route.FromZoneId) || !zoneSet.Contains(route.ToZoneId))
                continue;

            double permeability = Math.Clamp(route.Condition * (1.0 - route.TravelRisk), 0.0, 1.0);
            permeabilities.Add(permeability);
            if (route.Condition < minimumRouteCondition || permeability <= 0.0)
                continue;

            adjacency[route.FromZoneId].Add(route.ToZoneId);
            if (route.Bidirectional)
                adjacency[route.ToZoneId].Add(route.FromZoneId);
        }

        var visited = new HashSet<string>(StringComparer.Ordinal);
        var components = new List<IReadOnlyList<string>>();
        foreach (string zone in zones)
        {
            if (!visited.Add(zone))
                continue;

            var stack = new Stack<string>();
            var component = new List<string>();
            stack.Push(zone);
            while (stack.Count > 0)
            {
                string current = stack.Pop();
                component.Add(current);
                foreach (string next in adjacency[current].OrderBy(x => x, StringComparer.Ordinal))
                    if (visited.Add(next))
                        stack.Push(next);
            }
            components.Add(component.OrderBy(x => x, StringComparer.Ordinal).ToArray());
        }

        var isolated = components
            .Where(x => x.Count == 1)
            .Select(x => x[0])
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        return new HabitatFragmentationReport(
            speciesId,
            components.Count,
            components,
            isolated,
            permeabilities.DefaultIfEmpty(0.0).Average());
    }
}
