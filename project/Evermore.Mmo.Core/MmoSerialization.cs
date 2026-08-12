using System.Security.Cryptography;
using System.Text.Json;

namespace Evermore.Mmo.Core;

public static class MmoCapabilitySerializer
{
    public const string SchemaVersion = "evermore.mmo.capabilities/1.0";
    public const string Authority = "implementation-status-only";

    public static string Serialize(MmoCapabilityCatalogSnapshot snapshot)
    {
        Validate(snapshot);
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(
            snapshot,
            MmoJsonContext.Default.MmoCapabilityCatalogSnapshot);
        return JsonSerializer.Serialize(
            new MmoCapabilityEnvelope(SchemaVersion, Authority, ComputeSha256(payload), snapshot),
            MmoJsonContext.Default.MmoCapabilityEnvelope);
    }

    public static MmoCapabilityEnvelope DeserializeAndVerify(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        MmoCapabilityEnvelope envelope = JsonSerializer.Deserialize(
            json,
            MmoJsonContext.Default.MmoCapabilityEnvelope)
            ?? throw new JsonException("The MMORPG capability envelope was null.");
        VerifyHeader(envelope.SchemaVersion, envelope.Authority, envelope.PayloadSha256, SchemaVersion, Authority);
        VerifyHash(
            envelope.PayloadSha256,
            JsonSerializer.SerializeToUtf8Bytes(
                envelope.Payload,
                MmoJsonContext.Default.MmoCapabilityCatalogSnapshot));
        Validate(envelope.Payload);
        return envelope;
    }

    private static void Validate(MmoCapabilityCatalogSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        byte[] expected = JsonSerializer.SerializeToUtf8Bytes(
            MmoCapabilityCatalog.Create(),
            MmoJsonContext.Default.MmoCapabilityCatalogSnapshot);
        byte[] actual = JsonSerializer.SerializeToUtf8Bytes(
            snapshot,
            MmoJsonContext.Default.MmoCapabilityCatalogSnapshot);
        if (expected.Length != actual.Length || !CryptographicOperations.FixedTimeEquals(expected, actual))
            throw new InvalidDataException("The MMORPG capability ledger does not match the active contract.");
    }

    internal static void VerifyHeader(
        string schema,
        string authority,
        string payloadSha256,
        string expectedSchema,
        string expectedAuthority)
    {
        if (!StringComparer.Ordinal.Equals(schema, expectedSchema) ||
            !StringComparer.Ordinal.Equals(authority, expectedAuthority) ||
            string.IsNullOrWhiteSpace(payloadSha256) ||
            payloadSha256.Length != 64)
        {
            throw new InvalidDataException("MMORPG schema, authority, or hash metadata verification failed.");
        }
    }

    internal static void VerifyHash(string expectedHex, byte[] payload)
    {
        byte[] expected;
        try
        {
            expected = Convert.FromHexString(expectedHex);
        }
        catch (FormatException ex)
        {
            throw new InvalidDataException("The MMORPG payload hash was malformed.", ex);
        }
        byte[] actual = SHA256.HashData(payload);
        if (expected.Length != actual.Length || !CryptographicOperations.FixedTimeEquals(expected, actual))
            throw new InvalidDataException("MMORPG payload integrity verification failed.");
    }

    internal static string ComputeSha256(byte[] payload) =>
        Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
}

public static class MmoSimulationSerializer
{
    public const string SchemaVersion = "evermore.mmo.session/1.0";
    public const string Authority = MmoSessionEngine.Authority;

    public static string Serialize(MmoSimulationSnapshot snapshot)
    {
        Validate(snapshot);
        byte[] payload = SerializePayload(snapshot);
        return JsonSerializer.Serialize(
            new MmoSimulationEnvelope(
                SchemaVersion,
                Authority,
                MmoCapabilitySerializer.ComputeSha256(payload),
                snapshot),
            MmoJsonContext.Default.MmoSimulationEnvelope);
    }

    public static MmoSimulationEnvelope DeserializeAndVerify(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        MmoSimulationEnvelope envelope = JsonSerializer.Deserialize(
            json,
            MmoJsonContext.Default.MmoSimulationEnvelope)
            ?? throw new JsonException("The MMORPG session envelope was null.");
        MmoCapabilitySerializer.VerifyHeader(
            envelope.SchemaVersion,
            envelope.Authority,
            envelope.PayloadSha256,
            SchemaVersion,
            Authority);
        MmoCapabilitySerializer.VerifyHash(envelope.PayloadSha256, SerializePayload(envelope.Payload));
        Validate(envelope.Payload);
        return envelope;
    }

    private static void Validate(MmoSimulationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        MmoSimulationSnapshot replay = new MmoSessionEngine().Process(snapshot.Input);
        byte[] expected = SerializePayload(replay);
        byte[] actual = SerializePayload(snapshot);
        if (expected.Length != actual.Length || !CryptographicOperations.FixedTimeEquals(expected, actual))
            throw new InvalidDataException("MMORPG session semantic replay verification failed.");
    }

    private static byte[] SerializePayload(MmoSimulationSnapshot snapshot) =>
        JsonSerializer.SerializeToUtf8Bytes(
            snapshot,
            MmoJsonContext.Default.MmoSimulationSnapshot);
}
