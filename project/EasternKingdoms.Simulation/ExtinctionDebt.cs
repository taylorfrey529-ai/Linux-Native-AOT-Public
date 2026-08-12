namespace EasternKingdoms.Simulation;

public sealed class ExtinctionDebtLedger
{
    private readonly BiogeographySettings _settings;
    private readonly Dictionary<string, ExtinctionDebtState> _states = new(StringComparer.Ordinal);

    public ExtinctionDebtLedger(BiogeographySettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _settings.Validate();
    }

    public IReadOnlyCollection<ExtinctionDebtState> States =>
        _states.Values.OrderBy(x => x.LineageId, StringComparer.Ordinal)
            .ThenBy(x => x.ZoneId, StringComparer.Ordinal)
            .ToArray();

    public ExtinctionDebtState? Observe(
        string lineageId,
        string zoneId,
        int generation,
        bool occupied,
        double habitatSuitability)
    {
        if (string.IsNullOrWhiteSpace(lineageId) || string.IsNullOrWhiteSpace(zoneId))
            throw new ArgumentException("Lineage and zone IDs are required for extinction debt.");
        if (generation < 0)
            throw new ArgumentOutOfRangeException(nameof(generation));
        if (habitatSuitability is < 0.0 or > 1.0)
            throw new ArgumentOutOfRangeException(nameof(habitatSuitability));

        string key = $"{lineageId}|{zoneId}";
        _states.TryGetValue(key, out var previous);

        if (!occupied && previous is null)
            return null;

        if (previous?.Realized == true)
            return previous;

        if (occupied && habitatSuitability < _settings.HabitatCollapseSuitability)
        {
            int consecutive = previous is not null && previous.LastGeneration == generation - 1
                ? previous.ConsecutiveDebtGenerations + 1
                : 1;
            double deficit = (_settings.HabitatCollapseSuitability - habitatSuitability) /
                             Math.Max(0.0001, _settings.HabitatCollapseSuitability);
            double increment = 0.10 + deficit * 0.20 + 0.20 / _settings.ExtinctionDebtGraceGenerations;
            double score = Math.Clamp((previous?.DebtScore ?? 0.0) + increment, 0.0, 1.0);
            var next = new ExtinctionDebtState(
                lineageId,
                zoneId,
                previous?.FirstDebtGeneration ?? generation,
                generation,
                consecutive,
                score,
                Realized: false,
                RealizedGeneration: null);
            _states[key] = next;
            return next;
        }

        if (!occupied && previous is not null && previous.DebtScore > 0.0)
        {
            bool realized = previous.DebtScore >= _settings.ExtinctionDebtRealizationThreshold ||
                            previous.ConsecutiveDebtGenerations >= _settings.ExtinctionDebtGraceGenerations;
            var next = previous with
            {
                LastGeneration = generation,
                Realized = realized,
                RealizedGeneration = realized ? generation : previous.RealizedGeneration
            };
            _states[key] = next;
            return next;
        }

        if (occupied && previous is not null)
        {
            double score = Math.Max(0.0, previous.DebtScore - 0.15);
            var next = previous with
            {
                LastGeneration = generation,
                ConsecutiveDebtGenerations = 0,
                DebtScore = score
            };
            _states[key] = next;
            return next;
        }

        return previous;
    }
}
