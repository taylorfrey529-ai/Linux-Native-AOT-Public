namespace Evermore.Lunara.Core;

public sealed class DeterministicMoonTideProcessor
{
    private readonly MoonTideContainmentPolicy _containment;

    public DeterministicMoonTideProcessor(MoonTideContainmentPolicy? containment = null)
    {
        _containment = containment ?? new MoonTideContainmentPolicy();
    }

    public MoonTideSnapshot Process(
        MoonTideInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ValidateInput(input);
        cancellationToken.ThrowIfCancellationRequested();

        LunaraRelicState[] relics = input.Relics
            .OrderBy(relic => relic.RelicId, StringComparer.Ordinal)
            .ToArray();
        LunaraRitualAttempt[] rituals = input.RitualAttempts
            .OrderBy(ritual => ritual.RelicId, StringComparer.Ordinal)
            .ThenBy(ritual => ritual.CultId, StringComparer.Ordinal)
            .ThenBy(ritual => ritual.RitualId, StringComparer.Ordinal)
            .ToArray();
        LunaraApiResponse[] responses = input.ApiResponses
            .OrderBy(response => response.ResponseId, StringComparer.Ordinal)
            .ToArray();

        var relicById = relics.ToDictionary(relic => relic.RelicId, StringComparer.Ordinal);
        var ritualById = rituals.ToDictionary(ritual => ritual.RitualId, StringComparer.Ordinal);
        LunarCycleState lunarCycle = ComputeLunarCycle(input.LunarPhaseStep);
        TideState tide = ComputeTide(
            input.TidePhaseStep,
            input.BaselineTideMicros,
            input.LocalTideResponseMicros,
            lunarCycle.SpringAlignmentMicros);
        int resonanceMicros = ComputeResonance(lunarCycle, tide);

        var acceptedRitualIds = new HashSet<string>(StringComparer.Ordinal);
        var rewardTotals = new Dictionary<string, long>(StringComparer.Ordinal);
        var unknownAcceptedRitualIds = new SortedSet<string>(StringComparer.Ordinal);
        var unknownRewardRelicIds = new SortedSet<string>(StringComparer.Ordinal);
        var rewardRelicMismatchResponseIds = new SortedSet<string>(StringComparer.Ordinal);
        bool hasRemoteManifestationDirective = false;

        foreach (LunaraApiResponse response in responses)
        {
            cancellationToken.ThrowIfCancellationRequested();
            hasRemoteManifestationDirective |= response.ManifestationRequested;
            if (!response.Accepted)
                continue;

            if (!ritualById.TryGetValue(response.RitualId, out LunaraRitualAttempt? ritual))
            {
                unknownAcceptedRitualIds.Add(response.RitualId);
                continue;
            }

            acceptedRitualIds.Add(ritual.RitualId);
            var perResponse = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (LunaraApiReward reward in response.Rewards)
            {
                if (!relicById.ContainsKey(reward.RelicId))
                {
                    unknownRewardRelicIds.Add(reward.RelicId);
                    continue;
                }

                if (!StringComparer.Ordinal.Equals(reward.RelicId, ritual.RelicId))
                {
                    rewardRelicMismatchResponseIds.Add(response.ResponseId);
                    continue;
                }

                int boundedReward = Math.Clamp(
                    reward.RewardChargeMicros,
                    0,
                    LunaraBounds.MaximumRewardPerRelicPerResponseMicros);
                perResponse[reward.RelicId] = perResponse.GetValueOrDefault(reward.RelicId) + boundedReward;
            }

            foreach ((string relicId, long rawReward) in perResponse.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                int boundedReward = (int)Math.Clamp(
                    rawReward,
                    0L,
                    LunaraBounds.MaximumRewardPerRelicPerResponseMicros);
                rewardTotals[relicId] = rewardTotals.GetValueOrDefault(relicId) + boundedReward;
            }
        }

        LunaraRitualAttempt[] locallyValidatedRituals = rituals
            .Where(ritual =>
                ritual.LocalCultAuthorized &&
                ritual.LocalRitualValid &&
                acceptedRitualIds.Contains(ritual.RitualId))
            .ToArray();
        Dictionary<string, int> validatedRitualCounts = locallyValidatedRituals
            .GroupBy(ritual => ritual.RelicId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        var tentativeRelics = new Dictionary<string, LunaraRelicState>(StringComparer.Ordinal);
        var stateLimitExceededRelicIds = new SortedSet<string>(StringComparer.Ordinal);
        foreach (LunaraRelicState relic in relics)
        {
            long reward = rewardTotals.GetValueOrDefault(relic.RelicId);
            long tentativeRitualCount = (long)relic.LocalRitualCount + validatedRitualCounts.GetValueOrDefault(relic.RelicId);
            if (tentativeRitualCount > LunaraBounds.MaximumLocalRitualCount)
                stateLimitExceededRelicIds.Add(relic.RelicId);

            tentativeRelics.Add(
                relic.RelicId,
                relic with
                {
                    ChargeMicros = LunaraBounds.ClampUnit((long)relic.ChargeMicros + reward),
                    LocalRitualCount = (int)Math.Min(tentativeRitualCount, LunaraBounds.MaximumLocalRitualCount)
                });
        }

        LunaraManifestationCandidate[] candidates = locallyValidatedRituals
            .GroupBy(ritual => ritual.RelicId, StringComparer.Ordinal)
            .Select(group => group.OrderBy(ritual => ritual.RitualId, StringComparer.Ordinal).First())
            .Select(ritual => new LunaraManifestationCandidate(
                ritual.RitualId,
                ritual.RelicId,
                tentativeRelics[ritual.RelicId].ChargeMicros,
                tentativeRelics[ritual.RelicId].LocalRitualCount))
            .Where(candidate =>
                candidate.TentativeChargeMicros >= LunaraBounds.MinimumManifestationChargeMicros &&
                candidate.TentativeRitualCount >= LunaraBounds.MinimumManifestationRitualCount &&
                resonanceMicros >= LunaraBounds.MinimumManifestationResonanceMicros &&
                tide.LevelMicros >= LunaraBounds.MinimumManifestationTideMicros)
            .OrderBy(candidate => candidate.RelicId, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.RitualId, StringComparer.Ordinal)
            .ToArray();

        (LunaraIdentityDrift[] identityDrifts, LunaraStateRegression[] stateRegressions) =
            CompareWithHistory(input.History, relics);
        var context = new MoonTideContainmentContext(
            hasRemoteManifestationDirective,
            unknownAcceptedRitualIds.ToArray(),
            unknownRewardRelicIds.ToArray(),
            rewardRelicMismatchResponseIds.ToArray(),
            identityDrifts,
            stateRegressions,
            rewardTotals
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new LunaraRewardPreview(pair.Key, checked((int)Math.Min(pair.Value, int.MaxValue))))
                .ToArray(),
            stateLimitExceededRelicIds.ToArray(),
            candidates);
        MoonTideContainmentDecision containment = _containment.Evaluate(context);

        if (containment.IsContained)
        {
            return new MoonTideSnapshot(
                MoonTideDisposition.Contained,
                containment.Code,
                containment.Reason,
                input.Seed,
                input.Generation,
                lunarCycle,
                tide,
                resonanceMicros,
                relics,
                [],
                [new LunaraEvent("CONTAINED", "engine", 0, containment.Reason ?? "Containment engaged.")]);
        }

        LunaraRelicState[] updatedRelics = tentativeRelics.Values
            .OrderBy(relic => relic.RelicId, StringComparer.Ordinal)
            .ToArray();
        var events = new List<LunaraEvent>();
        foreach (LunaraRelicState relic in updatedRelics)
        {
            long reward = rewardTotals.GetValueOrDefault(relic.RelicId);
            if (reward > 0)
            {
                AddEvent(events, new LunaraEvent(
                    "REMOTE_REWARD_APPLIED",
                    relic.RelicId,
                    checked((int)reward),
                    "Bounded remote charge was applied after containment clearance."));
            }

            int ritualCount = validatedRitualCounts.GetValueOrDefault(relic.RelicId);
            if (ritualCount > 0)
            {
                AddEvent(events, new LunaraEvent(
                    "LOCAL_RITUALS_VALIDATED",
                    relic.RelicId,
                    ritualCount,
                    "Accepted responses were paired with locally authorized cult and ritual validation."));
            }
        }

        var random = new LunaraRandom(input.Seed, input.Generation);
        var directives = new List<LunaraManifestationDirective>();
        foreach (LunaraManifestationCandidate candidate in candidates)
        {
            LunaraRelicState relic = tentativeRelics[candidate.RelicId];
            int probability = ComputeManifestationProbability(relic.ChargeMicros, resonanceMicros, tide.LevelMicros);
            int roll = random.NextUnitMicros();
            if (roll >= probability)
                continue;

            directives.Add(new LunaraManifestationDirective(
                candidate.RitualId,
                candidate.RelicId,
                relic.SoulSignature,
                probability,
                roll,
                "local-cult+local-ritual+containment-cleared"));
            AddEvent(events, new LunaraEvent(
                "MANIFESTATION_AUTHORIZED",
                candidate.RelicId,
                probability,
                "Pure authorization directive only; the core performs no spawn or external write."));
        }

        return new MoonTideSnapshot(
            MoonTideDisposition.Completed,
            null,
            null,
            input.Seed,
            input.Generation,
            lunarCycle,
            tide,
            resonanceMicros,
            updatedRelics,
            directives.ToArray(),
            events.ToArray());
    }

    internal static LunarCycleState ComputeLunarCycle(int phaseStep)
    {
        int halfCycle = LunaraBounds.SynodicCycleSteps / 2;
        int distanceFromNew = Math.Min(phaseStep, LunaraBounds.SynodicCycleSteps - phaseStep);
        int illumination = (int)((long)distanceFromNew * LunaraBounds.Unit / halfCycle);
        int springAlignment = Math.Abs((illumination * 2) - LunaraBounds.Unit);
        var phase = (LunarPhaseName)((long)phaseStep * 8 / LunaraBounds.SynodicCycleSteps);
        bool isWaxing = phaseStep > 0 && phaseStep < halfCycle;
        return new LunarCycleState(phaseStep, phase, isWaxing, illumination, springAlignment);
    }

    internal static TideState ComputeTide(
        int tidePhaseStep,
        int baselineTideMicros,
        int localTideResponseMicros,
        int springAlignmentMicros)
    {
        int halfCycle = LunaraBounds.TideCycleSteps / 2;
        int distanceFromHigh = Math.Min(tidePhaseStep, LunaraBounds.TideCycleSteps - tidePhaseStep);
        int normalizedDistance = (int)((long)distanceFromHigh * LunaraBounds.Unit / halfCycle);
        int signedWave = LunaraBounds.Unit - (normalizedDistance * 2);
        int amplitude = 250_000 + (springAlignmentMicros / 4);
        long waveContribution = (long)signedWave * amplitude / LunaraBounds.Unit;
        int level = LunaraBounds.ClampUnit(baselineTideMicros + localTideResponseMicros + waveContribution);
        TideBand band = ComputeTideBand(level);
        return new TideState(tidePhaseStep, signedWave, amplitude, level, band);
    }

    internal static TideBand ComputeTideBand(int levelMicros) =>
        levelMicros switch
        {
            < 200_000 => TideBand.Low,
            < 400_000 => TideBand.Ebb,
            <= 600_000 => TideBand.Mean,
            <= 800_000 => TideBand.Flood,
            _ => TideBand.High
        };

    internal static int ComputeResonance(LunarCycleState cycle, TideState tide) =>
        (int)(((long)cycle.IlluminationMicros * 3 + cycle.SpringAlignmentMicros + tide.LevelMicros) / 5);

    internal static int ComputeManifestationProbability(int chargeMicros, int resonanceMicros, int tideMicros)
    {
        long probability = 50_000L;
        probability += Math.Max(0, chargeMicros - LunaraBounds.MinimumManifestationChargeMicros) / 2;
        probability += Math.Max(0, resonanceMicros - LunaraBounds.MinimumManifestationResonanceMicros) / 2;
        probability += Math.Max(0, tideMicros - LunaraBounds.MinimumManifestationTideMicros) / 4;
        return (int)Math.Clamp(probability, 0L, LunaraBounds.MaximumManifestationProbabilityMicros);
    }

    private static (LunaraIdentityDrift[] IdentityDrifts, LunaraStateRegression[] StateRegressions)
        CompareWithHistory(IReadOnlyList<MoonTideHistoryPoint> history, IReadOnlyList<LunaraRelicState> relics)
    {
        if (history.Count == 0)
            return ([], []);

        var previous = new Dictionary<string, LunaraRelicHistoryState>(StringComparer.Ordinal);
        var identityDrifts = new List<LunaraIdentityDrift>();
        var stateRegressions = new List<LunaraStateRegression>();

        foreach (MoonTideHistoryPoint point in history)
        {
            foreach (LunaraRelicHistoryState relic in point.Relics.OrderBy(value => value.RelicId, StringComparer.Ordinal))
            {
                CompareHistoricalRelic(previous.GetValueOrDefault(relic.RelicId), relic, identityDrifts, stateRegressions);
                previous[relic.RelicId] = relic;
            }
        }

        foreach (LunaraRelicState relic in relics)
        {
            if (!previous.TryGetValue(relic.RelicId, out LunaraRelicHistoryState? prior))
                continue;

            CompareHistoricalRelic(
                prior,
                new LunaraRelicHistoryState(
                    relic.RelicId,
                    relic.SoulSignature,
                    relic.ChargeMicros,
                    relic.LocalRitualCount),
                identityDrifts,
                stateRegressions);
        }

        return (
            identityDrifts.OrderBy(drift => drift.RelicId, StringComparer.Ordinal).ToArray(),
            stateRegressions
                .OrderBy(regression => regression.RelicId, StringComparer.Ordinal)
                .ThenBy(regression => regression.Field, StringComparer.Ordinal)
                .ToArray());
    }

    private static void CompareHistoricalRelic(
        LunaraRelicHistoryState? prior,
        LunaraRelicHistoryState current,
        List<LunaraIdentityDrift> identityDrifts,
        List<LunaraStateRegression> stateRegressions)
    {
        if (prior is null)
            return;

        if (!StringComparer.Ordinal.Equals(current.SoulSignature, prior.SoulSignature))
        {
            identityDrifts.Add(new LunaraIdentityDrift(
                current.RelicId,
                prior.SoulSignature,
                current.SoulSignature));
        }

        if (current.LocalRitualCount < prior.LocalRitualCount)
        {
            stateRegressions.Add(new LunaraStateRegression(
                current.RelicId,
                "localRitualCount",
                prior.LocalRitualCount,
                current.LocalRitualCount));
        }

        if (current.ChargeMicros - prior.ChargeMicros > LunaraBounds.MaximumChargeIncreasePerGenerationMicros)
        {
            stateRegressions.Add(new LunaraStateRegression(
                current.RelicId,
                "unauthorizedChargeIncrease",
                prior.ChargeMicros,
                current.ChargeMicros));
        }
    }

    private static void AddEvent(List<LunaraEvent> events, LunaraEvent value)
    {
        if (events.Count < LunaraBounds.MaximumSnapshotEvents)
            events.Add(value);
    }

    private static void ValidateInput(MoonTideInput input)
    {
        if (input.Generation is < 0 or > LunaraBounds.MaximumGeneration)
            throw new ArgumentOutOfRangeException(nameof(input), $"Generation must be in 0..{LunaraBounds.MaximumGeneration}.");
        if (input.LunarPhaseStep is < 0 or >= LunaraBounds.SynodicCycleSteps)
            throw new ArgumentOutOfRangeException(nameof(input), $"Lunar phase step must be in 0..{LunaraBounds.SynodicCycleSteps - 1}.");
        if (input.TidePhaseStep is < 0 or >= LunaraBounds.TideCycleSteps)
            throw new ArgumentOutOfRangeException(nameof(input), $"Tide phase step must be in 0..{LunaraBounds.TideCycleSteps - 1}.");
        if (input.BaselineTideMicros is < 0 or > LunaraBounds.Unit)
            throw new ArgumentOutOfRangeException(nameof(input), "Baseline tide must be in 0..1,000,000 micro-units.");
        if (Math.Abs((long)input.LocalTideResponseMicros) > LunaraBounds.MaximumLocalTideResponseMicros)
            throw new ArgumentOutOfRangeException(nameof(input), $"Local tide response must be within +/-{LunaraBounds.MaximumLocalTideResponseMicros} micro-units.");

        ArgumentNullException.ThrowIfNull(input.Relics);
        ArgumentNullException.ThrowIfNull(input.RitualAttempts);
        ArgumentNullException.ThrowIfNull(input.ApiResponses);
        ArgumentNullException.ThrowIfNull(input.History);
        if (input.Relics.Count is < 1 or > LunaraBounds.MaximumRelicPopulation)
            throw new ArgumentOutOfRangeException(nameof(input), $"Relic population must be in 1..{LunaraBounds.MaximumRelicPopulation}.");
        if (input.RitualAttempts.Count > LunaraBounds.MaximumRitualAttempts)
            throw new ArgumentOutOfRangeException(nameof(input), $"Ritual attempts cannot exceed {LunaraBounds.MaximumRitualAttempts}.");
        if (input.ApiResponses.Count > LunaraBounds.MaximumApiResponses)
            throw new ArgumentOutOfRangeException(nameof(input), $"API responses cannot exceed {LunaraBounds.MaximumApiResponses}.");
        if (input.History.Count > LunaraBounds.MaximumHistoryPoints)
            throw new ArgumentOutOfRangeException(nameof(input), $"History cannot exceed {LunaraBounds.MaximumHistoryPoints} points.");

        var relicIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (LunaraRelicState relic in input.Relics)
        {
            ArgumentNullException.ThrowIfNull(relic);
            LunaraBounds.RequireIdentifier(relic.RelicId, nameof(relic.RelicId));
            LunaraBounds.RequireSoulSignature(relic.SoulSignature, nameof(relic.SoulSignature));
            if (!relicIds.Add(relic.RelicId))
                throw new ArgumentException($"Duplicate relic ID: {relic.RelicId}.", nameof(input));
            if (relic.ChargeMicros is < 0 or > LunaraBounds.Unit)
                throw new ArgumentOutOfRangeException(nameof(input), $"Relic {relic.RelicId} charge must be in 0..1,000,000.");
            if (relic.LocalRitualCount is < 0 or > LunaraBounds.MaximumLocalRitualCount)
                throw new ArgumentOutOfRangeException(nameof(input), $"Relic {relic.RelicId} ritual count is outside finite bounds.");
        }

        var ritualIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (LunaraRitualAttempt ritual in input.RitualAttempts)
        {
            ArgumentNullException.ThrowIfNull(ritual);
            LunaraBounds.RequireIdentifier(ritual.RitualId, nameof(ritual.RitualId));
            LunaraBounds.RequireIdentifier(ritual.RelicId, nameof(ritual.RelicId));
            LunaraBounds.RequireIdentifier(ritual.CultId, nameof(ritual.CultId));
            if (!ritualIds.Add(ritual.RitualId))
                throw new ArgumentException($"Duplicate ritual ID: {ritual.RitualId}.", nameof(input));
            if (!relicIds.Contains(ritual.RelicId))
                throw new ArgumentException($"Ritual {ritual.RitualId} references an unknown local relic.", nameof(input));
        }

        var responseIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (LunaraApiResponse response in input.ApiResponses)
        {
            ArgumentNullException.ThrowIfNull(response);
            LunaraBounds.RequireIdentifier(response.ResponseId, nameof(response.ResponseId));
            LunaraBounds.RequireIdentifier(response.RitualId, nameof(response.RitualId));
            if (!responseIds.Add(response.ResponseId))
                throw new ArgumentException($"Duplicate API response ID: {response.ResponseId}.", nameof(input));
            ArgumentNullException.ThrowIfNull(response.Rewards);
            if (response.Rewards.Count > LunaraBounds.MaximumRewardsPerResponse)
                throw new ArgumentOutOfRangeException(nameof(input), $"Response {response.ResponseId} has too many rewards.");
            foreach (LunaraApiReward reward in response.Rewards)
            {
                ArgumentNullException.ThrowIfNull(reward);
                LunaraBounds.RequireIdentifier(reward.RelicId, nameof(reward.RelicId));
            }
        }

        int previousGeneration = -1;
        foreach (MoonTideHistoryPoint point in input.History)
        {
            ArgumentNullException.ThrowIfNull(point);
            if (point.Generation <= previousGeneration || point.Generation >= input.Generation)
                throw new ArgumentException("History generations must be strictly increasing and earlier than the current generation.", nameof(input));
            previousGeneration = point.Generation;
            if (point.ManifestationDirectiveCount is < 0 or > LunaraBounds.MaximumManifestationDirectives)
                throw new ArgumentOutOfRangeException(nameof(input), "A history point exceeds the manifestation-directive bound.");
            ArgumentNullException.ThrowIfNull(point.Relics);
            if (point.Relics.Count > LunaraBounds.MaximumRelicPopulation)
                throw new ArgumentOutOfRangeException(nameof(input), "A history point exceeds the relic-population bound.");
            var historyRelicIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (LunaraRelicHistoryState relic in point.Relics)
            {
                ArgumentNullException.ThrowIfNull(relic);
                LunaraBounds.RequireIdentifier(relic.RelicId, nameof(relic.RelicId));
                LunaraBounds.RequireSoulSignature(relic.SoulSignature, nameof(relic.SoulSignature));
                if (!historyRelicIds.Add(relic.RelicId))
                    throw new ArgumentException($"Duplicate history relic ID: {relic.RelicId}.", nameof(input));
                if (relic.ChargeMicros is < 0 or > LunaraBounds.Unit)
                    throw new ArgumentOutOfRangeException(nameof(input), "History relic charge is outside finite bounds.");
                if (relic.LocalRitualCount is < 0 or > LunaraBounds.MaximumLocalRitualCount)
                    throw new ArgumentOutOfRangeException(nameof(input), "History ritual count is outside finite bounds.");
            }
        }
    }
}
