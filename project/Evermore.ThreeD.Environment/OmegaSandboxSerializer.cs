using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Evermore.ThreeD.Environment;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false,
    GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(OmegaSandboxEnvelope))]
[JsonSerializable(typeof(OmegaSandboxSnapshot))]
[JsonSerializable(typeof(OmegaSandboxInput))]
internal sealed partial class OmegaSandboxJsonContext : JsonSerializerContext;

public static class OmegaSandboxSerializer
{
    public const string SchemaVersion = "evermore.omega.sandbox/1.1";
    public const string LegacySchemaVersion = "evermore.omega.sandbox/1.0";
    public const string Authority = OmegaSandboxEngine.Authority;
    public const int MaximumSnapshotBytes = 64 * 1024 * 1024;

    public static string Serialize(OmegaSandboxSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        byte[] payload = SerializePayload(snapshot);
        string hash = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
        var envelope = new OmegaSandboxEnvelope(SchemaVersion, Authority, hash, snapshot);
        return JsonSerializer.Serialize(envelope, OmegaSandboxJsonContext.Default.OmegaSandboxEnvelope);
    }

    public static OmegaSandboxSnapshot DeserializeAndVerify(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        OmegaSandboxEnvelope envelope = JsonSerializer.Deserialize(
            json,
            OmegaSandboxJsonContext.Default.OmegaSandboxEnvelope)
            ?? throw new JsonException("The Omega Sandbox envelope was null.");
        bool isCurrent = StringComparer.Ordinal.Equals(envelope.SchemaVersion, SchemaVersion);
        bool isLegacy = StringComparer.Ordinal.Equals(envelope.SchemaVersion, LegacySchemaVersion);
        if ((!isCurrent && !isLegacy) || !StringComparer.Ordinal.Equals(envelope.Authority, Authority))
            throw new InvalidDataException("Omega Sandbox schema or authority verification failed.");
        if (isLegacy && envelope.Payload.SimulationTick != 0)
            throw new InvalidDataException("Legacy Omega Sandbox snapshots cannot declare a nonzero simulation tick.");

        byte[] expectedHash;
        try
        {
            expectedHash = Convert.FromHexString(envelope.PayloadSha256);
        }
        catch (FormatException ex)
        {
            throw new InvalidDataException("Omega Sandbox payload hash was malformed.", ex);
        }

        byte[] actualHash = SHA256.HashData(SerializePayload(envelope.Payload));
        if (expectedHash.Length != actualHash.Length ||
            !CryptographicOperations.FixedTimeEquals(expectedHash, actualHash))
            throw new InvalidDataException("Omega Sandbox payload integrity verification failed.");

        OmegaSandboxSnapshot expected = new OmegaSandboxEngine().Process(
            envelope.Payload.Input,
            envelope.Payload.SimulationTick);
        byte[] expectedPayload = SerializePayload(expected);
        byte[] suppliedPayload = SerializePayload(envelope.Payload);
        if (!suppliedPayload.AsSpan().SequenceEqual(expectedPayload))
            throw new InvalidDataException("Omega Sandbox semantic replay verification failed.");
        return envelope.Payload;
    }

    private static byte[] SerializePayload(OmegaSandboxSnapshot snapshot) =>
        JsonSerializer.SerializeToUtf8Bytes(
            snapshot,
            OmegaSandboxJsonContext.Default.OmegaSandboxSnapshot);
}
