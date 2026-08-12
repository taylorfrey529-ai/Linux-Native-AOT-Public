namespace EasternKingdoms.Simulation;

public enum CommunityCondition
{
    Stable,
    Stressed,
    Collapsing,
    Recovering
}

public sealed record ZoneTrophicOutcome(
    string ZoneId,
    TrophicSpeciesOutcome Outcome);

public sealed record CommunityStressMetrics(
    double PopulationLoss,
    double DiseaseLoad,
    double TrophicStress,
    double FragmentationStress,
    double LocalExtinctionStress);

public sealed record CommunityResilienceSettings(
    int CollapseMinimumStressDimensions = 3,
    int CollapseConsecutiveGenerations = 3,
    int RecoveryMaximumStressDimensions = 1,
    int RecoveryConsecutiveGenerations = 2,
    double PopulationLossThreshold = 0.20,
    double DiseaseLoadThreshold = 0.45,
    double TrophicStressThreshold = 0.45,
    double FragmentationStressThreshold = 0.50,
    double LocalExtinctionStressThreshold = 0.30);

public sealed record CommunityResilienceState(
    CommunityCondition Condition,
    int ConsecutiveStressGenerations,
    int ConsecutiveRecoveryGenerations)
{
    public static CommunityResilienceState Initial =>
        new(CommunityCondition.Stable, 0, 0);
}

public sealed record CommunityHealthAssessment(
    CommunityStressMetrics Metrics,
    int StressDimensions,
    CommunityResilienceState Previous,
    CommunityResilienceState Next,
    string Reason);

public sealed class CommunityResilienceTracker
{
    public CommunityStressMetrics Measure(
        IReadOnlyList<EvolutionCohort> previous,
        IReadOnlyList<EvolutionCohort> current,
        IReadOnlyList<DiseaseState> diseases,
        IReadOnlyList<ZoneTrophicOutcome> trophic,
        IReadOnlyList<MetapopulationGenerationResult> metapopulations)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(diseases);
        ArgumentNullException.ThrowIfNull(trophic);
        ArgumentNullException.ThrowIfNull(metapopulations);

        double previousPopulation = previous.Sum(x => Math.Max(0.0, x.Population));
        double currentPopulation = current.Sum(x => Math.Max(0.0, x.Population));
        double populationLoss = previousPopulation <= 0.0
            ? 0.0
            : Math.Clamp(1.0 - currentPopulation / previousPopulation, 0.0, 1.0);

        double diseaseLoad = Math.Clamp(
            diseases.Select(x => Math.Clamp(x.Prevalence, 0.0, 1.0)).DefaultIfEmpty(0.0).Average(),
            0.0,
            1.0);

        double trophicStress = Math.Clamp(
            1.0 - trophic.Select(x => Math.Clamp(x.Outcome.NetSupportModifier, 0.0, 1.0))
                .DefaultIfEmpty(1.0)
                .Average(),
            0.0,
            1.0);

        var fragmentationValues = new List<double>();
        foreach (var result in metapopulations)
        {
            int extantZones = result.Cohorts.Count(x => x.Population > 0);
            if (extantZones <= 1)
            {
                fragmentationValues.Add(0.0);
                continue;
            }

            fragmentationValues.Add(Math.Clamp(
                (result.Fragmentation.ConnectedComponents - 1.0) / Math.Max(1.0, extantZones - 1.0),
                0.0,
                1.0));
        }

        double fragmentationStress = fragmentationValues.DefaultIfEmpty(0.0).Average();
        double localExtinctionStress = current.Count == 0
            ? 0.0
            : current.Count(x => x.Population <= 0) / (double)current.Count;

        return new CommunityStressMetrics(
            populationLoss,
            diseaseLoad,
            trophicStress,
            Math.Clamp(fragmentationStress, 0.0, 1.0),
            Math.Clamp(localExtinctionStress, 0.0, 1.0));
    }

    public CommunityHealthAssessment Assess(
        CommunityResilienceState previous,
        CommunityStressMetrics metrics,
        CommunityResilienceSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(metrics);
        settings ??= new CommunityResilienceSettings();
        Validate(settings);

        int stressDimensions = 0;
        if (metrics.PopulationLoss >= settings.PopulationLossThreshold) stressDimensions++;
        if (metrics.DiseaseLoad >= settings.DiseaseLoadThreshold) stressDimensions++;
        if (metrics.TrophicStress >= settings.TrophicStressThreshold) stressDimensions++;
        if (metrics.FragmentationStress >= settings.FragmentationStressThreshold) stressDimensions++;
        if (metrics.LocalExtinctionStress >= settings.LocalExtinctionStressThreshold) stressDimensions++;

        CommunityResilienceState next;
        string reason;

        if (stressDimensions >= settings.CollapseMinimumStressDimensions)
        {
            int stress = previous.ConsecutiveStressGenerations + 1;
            next = new CommunityResilienceState(
                stress >= settings.CollapseConsecutiveGenerations
                    ? CommunityCondition.Collapsing
                    : CommunityCondition.Stressed,
                stress,
                0);
            reason = $"{stressDimensions} interacting stress dimensions are above threshold for {stress} consecutive generations.";
        }
        else if (stressDimensions <= settings.RecoveryMaximumStressDimensions &&
                 previous.Condition is CommunityCondition.Stressed or CommunityCondition.Collapsing or CommunityCondition.Recovering)
        {
            int recovery = previous.ConsecutiveRecoveryGenerations + 1;
            next = new CommunityResilienceState(
                recovery >= settings.RecoveryConsecutiveGenerations
                    ? CommunityCondition.Stable
                    : CommunityCondition.Recovering,
                0,
                recovery);
            reason = $"Stress has fallen to {stressDimensions} dimensions for {recovery} consecutive generations.";
        }
        else
        {
            next = new CommunityResilienceState(
                stressDimensions == 0 ? CommunityCondition.Stable : CommunityCondition.Stressed,
                0,
                0);
            reason = stressDimensions == 0
                ? "No community stress dimension is above its threshold."
                : $"{stressDimensions} stress dimensions are elevated, below the collapse gate.";
        }

        return new CommunityHealthAssessment(
            metrics,
            stressDimensions,
            previous,
            next,
            reason);
    }

    private static void Validate(CommunityResilienceSettings settings)
    {
        if (settings.CollapseMinimumStressDimensions is < 2 or > 5)
            throw new InvalidOperationException("Community collapse requires between two and five interacting stress dimensions.");
        if (settings.CollapseConsecutiveGenerations <= 0 || settings.RecoveryConsecutiveGenerations <= 0)
            throw new InvalidOperationException("Community resilience generation gates must be positive.");
        if (settings.RecoveryMaximumStressDimensions < 0 ||
            settings.RecoveryMaximumStressDimensions >= settings.CollapseMinimumStressDimensions)
            throw new InvalidOperationException("Recovery stress gate must remain below the collapse gate.");
    }
}
