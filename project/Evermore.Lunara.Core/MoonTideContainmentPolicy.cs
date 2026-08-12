namespace Evermore.Lunara.Core;

public sealed record LunaraIdentityDrift(
    string RelicId,
    string PreviousSoulSignature,
    string CurrentSoulSignature);

public sealed record LunaraStateRegression(
    string RelicId,
    string Field,
    int PreviousValue,
    int CurrentValue);

public sealed record LunaraRewardPreview(string RelicId, int RewardChargeMicros);

public sealed record LunaraManifestationCandidate(
    string RitualId,
    string RelicId,
    int TentativeChargeMicros,
    int TentativeRitualCount);

public sealed record MoonTideContainmentContext(
    bool HasRemoteManifestationDirective,
    IReadOnlyList<string> UnknownAcceptedRitualIds,
    IReadOnlyList<string> UnknownRewardRelicIds,
    IReadOnlyList<string> RewardRelicMismatchResponseIds,
    IReadOnlyList<LunaraIdentityDrift> IdentityDrifts,
    IReadOnlyList<LunaraStateRegression> StateRegressions,
    IReadOnlyList<LunaraRewardPreview> RewardPreviews,
    IReadOnlyList<string> StateLimitExceededRelicIds,
    IReadOnlyList<LunaraManifestationCandidate> ManifestationCandidates);

public sealed record MoonTideContainmentDecision(
    bool IsContained,
    string? Code,
    string? Reason)
{
    public static MoonTideContainmentDecision Continue { get; } = new(false, null, null);

    public static MoonTideContainmentDecision Contain(string code, string reason) =>
        new(true, code, reason);
}

public sealed class MoonTideContainmentPolicy
{
    public MoonTideContainmentDecision Evaluate(MoonTideContainmentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.HasRemoteManifestationDirective)
        {
            return MoonTideContainmentDecision.Contain(
                "REMOTE_MANIFESTATION_AUTHORITY_REJECTED",
                "A remote Lunara response attempted to direct manifestation; only the local bounded engine may authorize a directive.");
        }

        if (context.UnknownAcceptedRitualIds.Count > 0)
        {
            return MoonTideContainmentDecision.Contain(
                "UNKNOWN_ACCEPTED_RITUAL",
                $"An accepted response referenced unknown local ritual {context.UnknownAcceptedRitualIds[0]} ({context.UnknownAcceptedRitualIds.Count} total).");
        }

        if (context.UnknownRewardRelicIds.Count > 0)
        {
            return MoonTideContainmentDecision.Contain(
                "UNKNOWN_REWARD_RELIC",
                $"An accepted reward referenced unknown local relic {context.UnknownRewardRelicIds[0]} ({context.UnknownRewardRelicIds.Count} total).");
        }

        if (context.RewardRelicMismatchResponseIds.Count > 0)
        {
            return MoonTideContainmentDecision.Contain(
                "REWARD_RELIC_MISMATCH",
                $"Accepted response {context.RewardRelicMismatchResponseIds[0]} rewarded a relic other than its local ritual relic ({context.RewardRelicMismatchResponseIds.Count} total).");
        }

        if (context.IdentityDrifts.Count > 0)
        {
            return MoonTideContainmentDecision.Contain(
                "SOUL_IDENTITY_DRIFT",
                $"Relic soul identity changed without an authored canon revision: {context.IdentityDrifts[0].RelicId}.");
        }

        if (context.StateRegressions.Count > 0)
        {
            return MoonTideContainmentDecision.Contain(
                "HISTORY_STATE_REGRESSION",
                $"Relic history violated a protected continuity bound: {context.StateRegressions[0].RelicId}/{context.StateRegressions[0].Field}.");
        }

        LunaraRewardPreview? excessiveReward = context.RewardPreviews
            .FirstOrDefault(preview => preview.RewardChargeMicros > LunaraBounds.MaximumRewardPerRelicPerGenerationMicros);
        if (excessiveReward is not null)
        {
            return MoonTideContainmentDecision.Contain(
                "GENERATION_REWARD_LIMIT",
                $"Relic {excessiveReward.RelicId} exceeded the bounded per-generation remote reward limit.");
        }

        if (context.StateLimitExceededRelicIds.Count > 0)
        {
            return MoonTideContainmentDecision.Contain(
                "STATE_LIMIT_EXCEEDED",
                $"A tentative relic state exceeded the finite ritual-count bound: {context.StateLimitExceededRelicIds[0]}.");
        }

        if (context.ManifestationCandidates.Count > LunaraBounds.MaximumManifestationDirectives)
        {
            return MoonTideContainmentDecision.Contain(
                "MANIFESTATION_FANOUT_LIMIT",
                $"The generation proposed {context.ManifestationCandidates.Count} manifestation branches; the maximum is {LunaraBounds.MaximumManifestationDirectives}.");
        }

        return MoonTideContainmentDecision.Continue;
    }
}
