using System.Text.Json.Serialization;

namespace Evermore.Lunara.Core;

public sealed record LunaraApiPostRequest(
    [property: JsonPropertyOrder(0), JsonRequired] string RequestId,
    [property: JsonPropertyOrder(1), JsonRequired] string RitualId,
    [property: JsonPropertyOrder(2), JsonRequired] string RelicId,
    [property: JsonPropertyOrder(3), JsonRequired] string CultId,
    [property: JsonPropertyOrder(4), JsonRequired] bool LocalCultAuthorized,
    [property: JsonPropertyOrder(5), JsonRequired] bool LocalRitualValid);

public sealed record LunaraApiRawReward(
    [property: JsonPropertyOrder(0), JsonRequired] string RelicId,
    [property: JsonPropertyOrder(1), JsonRequired] decimal RewardCharge);

public sealed record LunaraApiRawResponse(
    [property: JsonPropertyOrder(0), JsonRequired] string ResponseId,
    [property: JsonPropertyOrder(1), JsonRequired] string RitualId,
    [property: JsonPropertyOrder(2), JsonRequired] bool Accepted,
    [property: JsonPropertyOrder(3), JsonRequired] bool ManifestationRequested,
    [property: JsonPropertyOrder(4), JsonRequired] IReadOnlyList<LunaraApiRawReward> Rewards);

public interface ILunaraApiTransport
{
    ValueTask<LunaraApiRawResponse> PostRitualAttemptAsync(
        LunaraApiPostRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class LunaraApiClient
{
    private const decimal MaximumRewardCharge = 0.25m;
    private readonly ILunaraApiTransport _transport;

    public LunaraApiClient(ILunaraApiTransport transport)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
    }

    public async ValueTask<LunaraApiResponse> PostRitualAttemptAsync(
        LunaraApiPostRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        LunaraApiRawResponse raw = await _transport
            .PostRitualAttemptAsync(request, cancellationToken)
            .ConfigureAwait(false);
        ArgumentNullException.ThrowIfNull(raw);

        LunaraBounds.RequireIdentifier(raw.ResponseId, nameof(raw.ResponseId));
        LunaraBounds.RequireIdentifier(raw.RitualId, nameof(raw.RitualId));
        if (!StringComparer.Ordinal.Equals(raw.RitualId, request.RitualId))
            throw new InvalidOperationException("The Lunara API response ritual correlation does not match the posted ritual.");
        ArgumentNullException.ThrowIfNull(raw.Rewards);
        if (raw.Rewards.Count > LunaraBounds.MaximumRewardsPerResponse)
            throw new InvalidOperationException($"A Lunara API response cannot contain more than {LunaraBounds.MaximumRewardsPerResponse} rewards.");

        LunaraApiReward[] boundedRewards = raw.Rewards
            .Select(reward =>
            {
                ArgumentNullException.ThrowIfNull(reward);
                LunaraBounds.RequireIdentifier(reward.RelicId, nameof(reward.RelicId));
                return reward;
            })
            .GroupBy(reward => reward.RelicId, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new LunaraApiReward(
                group.Key,
                ToRewardMicros(group.Select(reward => reward.RewardCharge))))
            .ToArray();

        return new LunaraApiResponse(
            raw.ResponseId,
            raw.RitualId,
            raw.Accepted,
            raw.ManifestationRequested,
            boundedRewards);
    }

    private static int ToRewardMicros(IEnumerable<decimal> rewardCharges)
    {
        decimal boundedTotal = 0m;
        foreach (decimal rewardCharge in rewardCharges)
        {
            boundedTotal += Math.Clamp(rewardCharge, 0m, MaximumRewardCharge);
            if (boundedTotal >= MaximumRewardCharge)
            {
                boundedTotal = MaximumRewardCharge;
                break;
            }
        }

        return checked((int)decimal.Floor(boundedTotal * LunaraBounds.Unit));
    }

    private static void ValidateRequest(LunaraApiPostRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        LunaraBounds.RequireIdentifier(request.RequestId, nameof(request.RequestId));
        LunaraBounds.RequireIdentifier(request.RitualId, nameof(request.RitualId));
        LunaraBounds.RequireIdentifier(request.RelicId, nameof(request.RelicId));
        LunaraBounds.RequireIdentifier(request.CultId, nameof(request.CultId));
    }
}
