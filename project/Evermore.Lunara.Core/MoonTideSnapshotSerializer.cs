using System.Security.Cryptography;
using System.Text.Json;

namespace Evermore.Lunara.Core;

public static class MoonTideSnapshotSerializer
{
    public const string SchemaVersion = "evermore.lunara.moon-tide/1.0";
    public const string Authority = "isolated-noncanon-local-validation";

    public static string Serialize(MoonTideSnapshot snapshot)
    {
        ValidateSnapshot(snapshot);
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(
            snapshot,
            LunaraJsonContext.Default.MoonTideSnapshot);
        var envelope = new MoonTideSnapshotEnvelope(
            SchemaVersion,
            Authority,
            ComputeSha256(payload),
            snapshot);
        return JsonSerializer.Serialize(
            envelope,
            LunaraJsonContext.Default.MoonTideSnapshotEnvelope);
    }

    public static MoonTideSnapshotEnvelope DeserializeAndVerify(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        MoonTideSnapshotEnvelope envelope = JsonSerializer.Deserialize(
            json,
            LunaraJsonContext.Default.MoonTideSnapshotEnvelope)
            ?? throw new JsonException("The Lunara snapshot envelope was null.");

        if (!StringComparer.Ordinal.Equals(envelope.SchemaVersion, SchemaVersion))
            throw new InvalidDataException($"Unsupported Lunara snapshot schema: {envelope.SchemaVersion}.");
        if (!StringComparer.Ordinal.Equals(envelope.Authority, Authority))
            throw new InvalidDataException($"Unsupported Lunara snapshot authority: {envelope.Authority}.");
        if (string.IsNullOrEmpty(envelope.PayloadSha256) || envelope.PayloadSha256.Length != 64)
            throw new InvalidDataException("The Lunara snapshot payload hash is malformed.");

        ValidateSnapshot(envelope.Payload);
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(
            envelope.Payload,
            LunaraJsonContext.Default.MoonTideSnapshot);
        string actualHash = ComputeSha256(payload);
        if (!FixedTimeHexEquals(envelope.PayloadSha256, actualHash))
            throw new InvalidDataException("Lunara snapshot payload integrity verification failed.");
        return envelope;
    }

    private static void ValidateSnapshot(MoonTideSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(snapshot.LunarCycle);
        ArgumentNullException.ThrowIfNull(snapshot.Tide);
        ArgumentNullException.ThrowIfNull(snapshot.Relics);
        ArgumentNullException.ThrowIfNull(snapshot.ManifestationDirectives);
        ArgumentNullException.ThrowIfNull(snapshot.Events);
        foreach (LunaraEvent value in snapshot.Events)
            ArgumentNullException.ThrowIfNull(value);

        if (snapshot.Generation is < 0 or > LunaraBounds.MaximumGeneration)
            throw new InvalidDataException("Snapshot generation is outside finite bounds.");
        if (snapshot.LunarCycle.PhaseStep is < 0 or >= LunaraBounds.SynodicCycleSteps)
            throw new InvalidDataException("Snapshot lunar phase is outside finite bounds.");
        if (snapshot.Tide.TidePhaseStep is < 0 or >= LunaraBounds.TideCycleSteps)
            throw new InvalidDataException("Snapshot tide phase is outside finite bounds.");
        RequireUnit(snapshot.LunarCycle.IlluminationMicros, "illumination");
        RequireUnit(snapshot.LunarCycle.SpringAlignmentMicros, "spring alignment");
        if (snapshot.Tide.SignedWaveMicros is < -LunaraBounds.Unit or > LunaraBounds.Unit)
            throw new InvalidDataException("Snapshot signed tide wave is outside finite bounds.");
        RequireUnit(snapshot.Tide.AmplitudeMicros, "tide amplitude");
        RequireUnit(snapshot.Tide.LevelMicros, "tide level");
        RequireUnit(snapshot.ResonanceMicros, "resonance");

        LunarCycleState expectedLunar = DeterministicMoonTideProcessor.ComputeLunarCycle(
            snapshot.LunarCycle.PhaseStep);
        if (snapshot.LunarCycle != expectedLunar)
            throw new InvalidDataException("Snapshot lunar-cycle state does not match the deterministic equation.");
        TideState expectedTideShape = DeterministicMoonTideProcessor.ComputeTide(
            snapshot.Tide.TidePhaseStep,
            0,
            0,
            snapshot.LunarCycle.SpringAlignmentMicros);
        if (snapshot.Tide.SignedWaveMicros != expectedTideShape.SignedWaveMicros ||
            snapshot.Tide.AmplitudeMicros != expectedTideShape.AmplitudeMicros)
        {
            throw new InvalidDataException("Snapshot tide shape does not match the deterministic equation.");
        }
        if (snapshot.Tide.Band != DeterministicMoonTideProcessor.ComputeTideBand(snapshot.Tide.LevelMicros))
            throw new InvalidDataException("Snapshot tide band does not match its level.");
        if (snapshot.ResonanceMicros != DeterministicMoonTideProcessor.ComputeResonance(snapshot.LunarCycle, snapshot.Tide))
            throw new InvalidDataException("Snapshot resonance does not match the deterministic equation.");

        if (snapshot.Relics.Count is < 1 or > LunaraBounds.MaximumRelicPopulation)
            throw new InvalidDataException("Snapshot relic population is outside finite bounds.");
        if (snapshot.ManifestationDirectives.Count > LunaraBounds.MaximumManifestationDirectives)
            throw new InvalidDataException("Snapshot manifestation directives exceed the finite bound.");
        if (snapshot.Events.Count > LunaraBounds.MaximumSnapshotEvents)
            throw new InvalidDataException("Snapshot events exceed the finite bound.");
        if (snapshot.Disposition is not MoonTideDisposition.Completed and not MoonTideDisposition.Contained)
            throw new InvalidDataException("Snapshot disposition is unsupported.");
        if (snapshot.Disposition == MoonTideDisposition.Contained && snapshot.ManifestationDirectives.Count != 0)
            throw new InvalidDataException("A contained snapshot cannot authorize manifestation.");
        if (snapshot.Disposition == MoonTideDisposition.Contained)
        {
            if (string.IsNullOrWhiteSpace(snapshot.ContainmentCode))
                throw new InvalidDataException("A contained snapshot requires a containment code.");
            LunaraBounds.RequireIdentifier(snapshot.ContainmentCode, nameof(snapshot.ContainmentCode));
            LunaraBounds.RequireDetail(snapshot.ContainmentReason, nameof(snapshot.ContainmentReason));
            if (!IsKnownContainmentCode(snapshot.ContainmentCode))
                throw new InvalidDataException("A contained snapshot carries an unsupported containment code.");
            if (snapshot.Events.Count != 1 ||
                !StringComparer.Ordinal.Equals(snapshot.Events[0].Code, "CONTAINED") ||
                !StringComparer.Ordinal.Equals(snapshot.Events[0].SubjectId, "engine") ||
                snapshot.Events[0].ValueMicros != 0 ||
                !StringComparer.Ordinal.Equals(snapshot.Events[0].Detail, snapshot.ContainmentReason))
            {
                throw new InvalidDataException("A contained snapshot must carry exactly its canonical containment event.");
            }
        }
        if (snapshot.Disposition == MoonTideDisposition.Completed &&
            (snapshot.ContainmentCode is not null || snapshot.ContainmentReason is not null))
        {
            throw new InvalidDataException("A completed snapshot cannot carry containment state.");
        }

        foreach (LunaraRelicState relic in snapshot.Relics)
            ArgumentNullException.ThrowIfNull(relic);
        string[] relicIds = snapshot.Relics.Select(relic => relic.RelicId).ToArray();
        if (!relicIds.SequenceEqual(relicIds.OrderBy(id => id, StringComparer.Ordinal), StringComparer.Ordinal))
            throw new InvalidDataException("Snapshot relics are not in stable ordinal order.");
        if (relicIds.Distinct(StringComparer.Ordinal).Count() != relicIds.Length)
            throw new InvalidDataException("Snapshot contains duplicate relic IDs.");
        foreach (LunaraRelicState relic in snapshot.Relics)
        {
            LunaraBounds.RequireIdentifier(relic.RelicId, nameof(relic.RelicId));
            LunaraBounds.RequireSoulSignature(relic.SoulSignature, nameof(relic.SoulSignature));
            RequireUnit(relic.ChargeMicros, $"relic {relic.RelicId} charge");
            if (relic.LocalRitualCount is < 0 or > LunaraBounds.MaximumLocalRitualCount)
                throw new InvalidDataException($"Relic {relic.RelicId} ritual count is outside finite bounds.");
        }

        foreach (LunaraManifestationDirective directive in snapshot.ManifestationDirectives)
            ArgumentNullException.ThrowIfNull(directive);
        string[] directiveKeys = snapshot.ManifestationDirectives
            .Select(directive => $"{directive.RelicId}\u001f{directive.RitualId}")
            .ToArray();
        if (!directiveKeys.SequenceEqual(directiveKeys.OrderBy(key => key, StringComparer.Ordinal), StringComparer.Ordinal))
            throw new InvalidDataException("Snapshot manifestation directives are not in stable ordinal order.");
        foreach (LunaraManifestationDirective directive in snapshot.ManifestationDirectives)
        {
            LunaraBounds.RequireIdentifier(directive.RitualId, nameof(directive.RitualId));
            LunaraBounds.RequireIdentifier(directive.RelicId, nameof(directive.RelicId));
            LunaraBounds.RequireSoulSignature(directive.SoulSignature, nameof(directive.SoulSignature));
            if (!StringComparer.Ordinal.Equals(directive.Authority, "local-cult+local-ritual+containment-cleared"))
                throw new InvalidDataException("A manifestation directive carries unsupported authority.");
            if (!relicIds.Contains(directive.RelicId, StringComparer.Ordinal))
                throw new InvalidDataException("A manifestation directive references an unknown snapshot relic.");
            if (!StringComparer.Ordinal.Equals(
                    snapshot.Relics.Single(relic => StringComparer.Ordinal.Equals(relic.RelicId, directive.RelicId)).SoulSignature,
                    directive.SoulSignature))
            {
                throw new InvalidDataException("A manifestation directive does not preserve its relic soul signature.");
            }
            if (directive.ProbabilityMicros is < 0 or > LunaraBounds.MaximumManifestationProbabilityMicros)
                throw new InvalidDataException("A manifestation probability is outside finite bounds.");
            if (directive.RollMicros is < 0 or >= LunaraBounds.Unit)
                throw new InvalidDataException("A manifestation roll is outside finite bounds.");
            if (directive.RollMicros >= directive.ProbabilityMicros)
                throw new InvalidDataException("A manifestation directive does not satisfy its recorded deterministic roll.");
            LunaraRelicState relic = snapshot.Relics.Single(value =>
                StringComparer.Ordinal.Equals(value.RelicId, directive.RelicId));
            if (relic.ChargeMicros < LunaraBounds.MinimumManifestationChargeMicros ||
                relic.LocalRitualCount < LunaraBounds.MinimumManifestationRitualCount ||
                snapshot.ResonanceMicros < LunaraBounds.MinimumManifestationResonanceMicros ||
                snapshot.Tide.LevelMicros < LunaraBounds.MinimumManifestationTideMicros)
            {
                throw new InvalidDataException("A manifestation directive does not meet local threshold state.");
            }
            if (directive.ProbabilityMicros != DeterministicMoonTideProcessor.ComputeManifestationProbability(
                    relic.ChargeMicros,
                    snapshot.ResonanceMicros,
                    snapshot.Tide.LevelMicros))
            {
                throw new InvalidDataException("A manifestation directive probability does not match the deterministic equation.");
            }
        }

        if (directiveKeys.Distinct(StringComparer.Ordinal).Count() != directiveKeys.Length)
            throw new InvalidDataException("Snapshot contains duplicate manifestation directives.");

        foreach (LunaraEvent value in snapshot.Events)
        {
            LunaraBounds.RequireIdentifier(value.Code, nameof(value.Code));
            LunaraBounds.RequireIdentifier(value.SubjectId, nameof(value.SubjectId));
            LunaraBounds.RequireDetail(value.Detail, nameof(value.Detail));
            RequireUnit(value.ValueMicros, $"event {value.Code} value");
            ValidateEvent(snapshot, relicIds, value);
        }

        string[] eventKeys = snapshot.Events
            .Select(value => $"{value.SubjectId}\u001f{value.Code}")
            .ToArray();
        if (eventKeys.Distinct(StringComparer.Ordinal).Count() != eventKeys.Length)
            throw new InvalidDataException("Snapshot contains duplicate subject event codes.");
        LunaraEvent[] expectedEventOrder = snapshot.Events
            .OrderBy(EventGroup)
            .ThenBy(value => value.SubjectId, StringComparer.Ordinal)
            .ThenBy(EventOrder)
            .ToArray();
        if (!snapshot.Events.SequenceEqual(expectedEventOrder))
            throw new InvalidDataException("Snapshot events are not in stable canonical order.");
    }

    private static void ValidateEvent(MoonTideSnapshot snapshot, IReadOnlyList<string> relicIds, LunaraEvent value)
    {
        switch (value.Code)
        {
            case "CONTAINED":
                if (snapshot.Disposition != MoonTideDisposition.Contained)
                    throw new InvalidDataException("A completed snapshot cannot carry a containment event.");
                break;
            case "REMOTE_REWARD_APPLIED":
                RequireRelicEvent(
                    relicIds,
                    value,
                    LunaraBounds.MaximumRewardPerRelicPerGenerationMicros,
                    "Bounded remote charge was applied after containment clearance.");
                break;
            case "LOCAL_RITUALS_VALIDATED":
                RequireRelicEvent(
                    relicIds,
                    value,
                    LunaraBounds.MaximumRitualAttempts,
                    "Accepted responses were paired with locally authorized cult and ritual validation.");
                break;
            case "MANIFESTATION_AUTHORIZED":
                RequireRelicEvent(
                    relicIds,
                    value,
                    LunaraBounds.MaximumManifestationProbabilityMicros,
                    "Pure authorization directive only; the core performs no spawn or external write.");
                if (!snapshot.ManifestationDirectives.Any(directive =>
                        StringComparer.Ordinal.Equals(directive.RelicId, value.SubjectId) &&
                        directive.ProbabilityMicros == value.ValueMicros))
                {
                    throw new InvalidDataException("A manifestation event has no matching pure directive.");
                }
                break;
            default:
                throw new InvalidDataException($"Snapshot event code is unsupported: {value.Code}.");
        }
    }

    private static void RequireRelicEvent(
        IReadOnlyList<string> relicIds,
        LunaraEvent value,
        int maximumValue,
        string expectedDetail)
    {
        if (!relicIds.Contains(value.SubjectId, StringComparer.Ordinal))
            throw new InvalidDataException($"Snapshot event {value.Code} references an unknown relic.");
        if (value.ValueMicros is < 1 || value.ValueMicros > maximumValue)
            throw new InvalidDataException($"Snapshot event {value.Code} has an invalid value.");
        if (!StringComparer.Ordinal.Equals(value.Detail, expectedDetail))
            throw new InvalidDataException($"Snapshot event {value.Code} has noncanonical detail.");
    }

    private static bool IsKnownContainmentCode(string value) =>
        value is
            "REMOTE_MANIFESTATION_AUTHORITY_REJECTED" or
            "UNKNOWN_ACCEPTED_RITUAL" or
            "UNKNOWN_REWARD_RELIC" or
            "REWARD_RELIC_MISMATCH" or
            "SOUL_IDENTITY_DRIFT" or
            "HISTORY_STATE_REGRESSION" or
            "GENERATION_REWARD_LIMIT" or
            "STATE_LIMIT_EXCEEDED" or
            "MANIFESTATION_FANOUT_LIMIT";

    private static int EventGroup(LunaraEvent value) =>
        StringComparer.Ordinal.Equals(value.Code, "MANIFESTATION_AUTHORIZED") ? 1 : 0;

    private static int EventOrder(LunaraEvent value) =>
        value.Code switch
        {
            "REMOTE_REWARD_APPLIED" => 0,
            "LOCAL_RITUALS_VALIDATED" => 1,
            _ => 0
        };

    private static void RequireUnit(int value, string field)
    {
        if (value is < 0 or > LunaraBounds.Unit)
            throw new InvalidDataException($"Snapshot {field} is outside 0..1,000,000 micro-units.");
    }

    private static string ComputeSha256(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static bool FixedTimeHexEquals(string expected, string actual)
    {
        try
        {
            byte[] expectedBytes = Convert.FromHexString(expected);
            byte[] actualBytes = Convert.FromHexString(actual);
            return CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
