namespace Evermore.Lunara.Core;

public static class LunaraBounds
{
    public const int Unit = 1_000_000;
    public const int SynodicCycleSteps = 29_530;
    public const int TideCycleSteps = 12_420;
    public const int MaximumGeneration = 1_000_000;
    public const int MaximumRelicPopulation = 256;
    public const int MaximumRitualAttempts = 1_024;
    public const int MaximumApiResponses = 64;
    public const int MaximumRewardsPerResponse = 256;
    public const int MaximumHistoryPoints = 128;
    public const int MaximumSnapshotEvents = 512;
    public const int MaximumManifestationDirectives = 4;
    public const int MaximumIdentifierLength = 96;
    public const int MaximumSoulSignatureLength = 128;
    public const int MaximumDetailLength = 512;
    public const int MaximumCliInputBytes = 16 * 1_024 * 1_024;
    public const int MaximumLocalRitualCount = 1_000_000;
    public const int MaximumRewardPerRelicPerResponseMicros = 250_000;
    public const int MaximumRewardPerRelicPerGenerationMicros = 500_000;
    public const int MaximumChargeIncreasePerGenerationMicros = 500_000;
    public const int MinimumManifestationChargeMicros = 850_000;
    public const int MinimumManifestationResonanceMicros = 600_000;
    public const int MinimumManifestationTideMicros = 500_000;
    public const int MinimumManifestationRitualCount = 3;
    public const int MaximumManifestationProbabilityMicros = 250_000;
    public const int MaximumLocalTideResponseMicros = 250_000;

    internal static void RequireIdentifier(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaximumIdentifierLength)
            throw new ArgumentException($"{parameterName} must contain 1..{MaximumIdentifierLength} nonblank characters.", parameterName);
    }

    internal static void RequireSoulSignature(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaximumSoulSignatureLength)
            throw new ArgumentException($"{parameterName} must contain 1..{MaximumSoulSignatureLength} nonblank characters.", parameterName);
    }

    internal static void RequireDetail(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaximumDetailLength)
            throw new ArgumentException($"{parameterName} must contain 1..{MaximumDetailLength} nonblank characters.", parameterName);
    }

    internal static int ClampUnit(long value) => (int)Math.Clamp(value, 0L, Unit);
}
