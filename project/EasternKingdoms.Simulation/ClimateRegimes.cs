namespace EasternKingdoms.Simulation;

public sealed class GeoclimateScenarioCatalog
{
    private readonly Dictionary<string, ClimateEpochDefinition> _climate = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DisturbanceEpochDefinition> _disturbances = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HabitatEnvelopeDefinition> _envelopes = new(StringComparer.Ordinal);

    public IReadOnlyCollection<ClimateEpochDefinition> ClimateEpochs => _climate.Values;
    public IReadOnlyCollection<DisturbanceEpochDefinition> DisturbanceEpochs => _disturbances.Values;
    public IReadOnlyCollection<HabitatEnvelopeDefinition> HabitatEnvelopes => _envelopes.Values;

    public void RegisterClimateEpoch(ClimateEpochDefinition definition)
    {
        Validate(definition);
        if (!_climate.TryAdd(definition.EpochId, definition))
            throw new InvalidOperationException($"Climate epoch {definition.EpochId} already exists.");
    }

    public void RegisterDisturbance(DisturbanceEpochDefinition definition)
    {
        Validate(definition);
        if (!_disturbances.TryAdd(definition.DisturbanceId, definition))
            throw new InvalidOperationException($"Disturbance epoch {definition.DisturbanceId} already exists.");
    }

    public void RegisterHabitatEnvelope(HabitatEnvelopeDefinition definition)
    {
        Validate(definition);
        if (!_envelopes.TryAdd(definition.EnvelopeId, definition))
            throw new InvalidOperationException($"Habitat envelope {definition.EnvelopeId} already exists.");
    }

    public void ValidateBoundary(IReadOnlySet<string> authorizedZoneIds)
    {
        ArgumentNullException.ThrowIfNull(authorizedZoneIds);
        foreach (var zoneId in _climate.Values.SelectMany(x => x.TargetZoneIds)
                     .Concat(_disturbances.Values.SelectMany(x => x.TargetZoneIds))
                     .Concat(_envelopes.Values.SelectMany(x => x.AllowedZoneIds))
                     .Distinct(StringComparer.Ordinal))
        {
            if (!authorizedZoneIds.Contains(zoneId))
                throw new InvalidOperationException($"Geoclimate scenario references unauthorized zone {zoneId}.");
        }
    }

    private static void Validate(ClimateEpochDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (string.IsNullOrWhiteSpace(definition.EpochId))
            throw new InvalidOperationException("Climate epoch requires an ID.");
        if (definition.StartGeneration < 0 || definition.EndGeneration < definition.StartGeneration)
            throw new InvalidOperationException($"Climate epoch {definition.EpochId} has an invalid generation range.");
        ValidateSigned(definition.TemperatureDelta, definition.EpochId, "temperature");
        ValidateSigned(definition.MoistureDelta, definition.EpochId, "moisture");
        ValidateSigned(definition.StorminessDelta, definition.EpochId, "storminess");
        ValidateSigned(definition.SeasonalityDelta, definition.EpochId, "seasonality");
        ValidateSigned(definition.ResonanceDelta, definition.EpochId, "resonance");
    }

    private static void Validate(DisturbanceEpochDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (string.IsNullOrWhiteSpace(definition.DisturbanceId))
            throw new InvalidOperationException("Disturbance epoch requires an ID.");
        if (definition.StartGeneration < 0 || definition.EndGeneration < definition.StartGeneration)
            throw new InvalidOperationException($"Disturbance {definition.DisturbanceId} has an invalid generation range.");
        if (definition.Severity is < 0.0 or > 1.0)
            throw new InvalidOperationException($"Disturbance {definition.DisturbanceId} has invalid severity.");
    }

    private static void Validate(HabitatEnvelopeDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (string.IsNullOrWhiteSpace(definition.EnvelopeId) || string.IsNullOrWhiteSpace(definition.SpeciesFamilyId))
            throw new InvalidOperationException("Habitat envelope requires stable envelope and species-family IDs.");
        if (definition.OptimalTemperature is < 0.0 or > 1.0 || definition.OptimalMoisture is < 0.0 or > 1.0)
            throw new InvalidOperationException($"Habitat envelope {definition.EnvelopeId} has an invalid optimum.");
        if (definition.TemperatureTolerance <= 0.0 || definition.MoistureTolerance <= 0.0)
            throw new InvalidOperationException($"Habitat envelope {definition.EnvelopeId} requires positive tolerances.");
        if (definition.MinimumForestCover is < 0.0 or > 1.0 || definition.MinimumWetlandExtent is < 0.0 or > 1.0 || definition.MaximumDisturbance is < 0.0 or > 1.0)
            throw new InvalidOperationException($"Habitat envelope {definition.EnvelopeId} contains an invalid bounded value.");
    }

    private static void ValidateSigned(double value, string id, string field)
    {
        if (value is < -1.0 or > 1.0)
            throw new InvalidOperationException($"Climate epoch {id} has invalid {field} delta.");
    }
}

public sealed class PaleoclimateResolver
{
    public ClimateZoneState Resolve(
        ClimateZoneBaseline baseline,
        int generation,
        IEnumerable<ClimateEpochDefinition> epochs)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(epochs);
        ValidateBaseline(baseline);
        if (generation < 0)
            throw new ArgumentOutOfRangeException(nameof(generation));

        var active = epochs
            .Where(x => generation >= x.StartGeneration && generation <= x.EndGeneration)
            .Where(x => x.TargetZoneIds.Count == 0 || x.TargetZoneIds.Contains(baseline.ZoneId))
            .OrderBy(x => x.EpochId, StringComparer.Ordinal)
            .ToArray();

        double temperature = Clamp01(baseline.Temperature + active.Sum(x => x.TemperatureDelta));
        double moisture = Clamp01(baseline.Moisture + active.Sum(x => x.MoistureDelta));
        double storminess = Clamp01(baseline.Storminess + active.Sum(x => x.StorminessDelta));
        double seasonality = Clamp01(baseline.Seasonality + active.Sum(x => x.SeasonalityDelta));
        double resonance = Clamp01(baseline.ResonanceBackground + active.Sum(x => x.ResonanceDelta));
        double climateStability = Clamp01(1.0 - storminess * 0.35 - Math.Abs(seasonality - baseline.Seasonality) * 0.25 - resonance * 0.20);

        return new ClimateZoneState(
            baseline.ZoneId,
            generation,
            Classify(temperature, moisture, storminess, seasonality, resonance, active.Length),
            temperature,
            moisture,
            storminess,
            seasonality,
            resonance,
            climateStability,
            active.Select(x => x.EpochId).ToArray());
    }

    private static ClimateRegimeKind Classify(
        double temperature,
        double moisture,
        double storminess,
        double seasonality,
        double resonance,
        int activeEpochs)
    {
        if (resonance >= 0.70)
            return ClimateRegimeKind.ArcanePerturbed;
        if (storminess >= 0.75 || seasonality >= 0.80)
            return ClimateRegimeKind.HighlyVariable;
        if (activeEpochs == 0)
            return ClimateRegimeKind.Baseline;
        if (temperature < 0.50)
            return moisture < 0.50 ? ClimateRegimeKind.CoolDry : ClimateRegimeKind.CoolWet;
        return moisture < 0.50 ? ClimateRegimeKind.WarmDry : ClimateRegimeKind.WarmWet;
    }

    private static void ValidateBaseline(ClimateZoneBaseline baseline)
    {
        if (string.IsNullOrWhiteSpace(baseline.ZoneId))
            throw new InvalidOperationException("Climate baseline requires a zone ID.");
        foreach (double value in new[] { baseline.Temperature, baseline.Moisture, baseline.Storminess, baseline.Seasonality, baseline.ResonanceBackground })
        {
            if (value is < 0.0 or > 1.0)
                throw new InvalidOperationException($"Climate baseline {baseline.ZoneId} contains a value outside [0, 1].");
        }
    }

    private static double Clamp01(double value) => Math.Clamp(value, 0.0, 1.0);
}
