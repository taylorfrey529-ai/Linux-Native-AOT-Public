using System.Security.Cryptography;
using System.Text.Json;
using EasternKingdoms.Simulation;

namespace Evermore.Azeroth.Simulation;

public static class AzerothRegionAuthoritySerializer
{
    public const string SchemaVersion = "evermore.azeroth.region-authority/1.0";
    public const string Authority = AzerothRegionAuthority.Authority;

    public static string Serialize(AzerothRegionAuthoritySnapshot snapshot)
    {
        ValidateSnapshot(snapshot);
        byte[] payload = SerializePayload(snapshot);
        var envelope = new AzerothRegionAuthorityEnvelope(
            SchemaVersion,
            Authority,
            ComputeSha256(payload),
            snapshot);
        return JsonSerializer.Serialize(
            envelope,
            AzerothSimulationJsonContext.Default.AzerothRegionAuthorityEnvelope);
    }

    public static AzerothRegionAuthorityEnvelope DeserializeAndVerify(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        AzerothRegionAuthorityEnvelope envelope = JsonSerializer.Deserialize(
            json,
            AzerothSimulationJsonContext.Default.AzerothRegionAuthorityEnvelope)
            ?? throw new JsonException("The Azeroth region authority envelope was null.");
        if (!StringComparer.Ordinal.Equals(envelope.SchemaVersion, SchemaVersion) ||
            !StringComparer.Ordinal.Equals(envelope.Authority, Authority))
        {
            throw new InvalidDataException("Azeroth region schema or authority verification failed.");
        }

        if (string.IsNullOrWhiteSpace(envelope.PayloadSha256) ||
            envelope.PayloadSha256.Length != 64)
        {
            throw new InvalidDataException("The Azeroth region authority hash was malformed.");
        }

        byte[] expectedHash;
        try
        {
            expectedHash = Convert.FromHexString(envelope.PayloadSha256);
        }
        catch (FormatException ex)
        {
            throw new InvalidDataException("The Azeroth region authority hash was malformed.", ex);
        }

        byte[] actualHash = SHA256.HashData(SerializePayload(envelope.Payload));
        if (expectedHash.Length != actualHash.Length ||
            !CryptographicOperations.FixedTimeEquals(expectedHash, actualHash))
        {
            throw new InvalidDataException("Azeroth region authority integrity verification failed.");
        }

        ValidateSnapshot(envelope.Payload);
        return envelope;
    }

    private static void ValidateSnapshot(AzerothRegionAuthoritySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        AzerothRegionAuthoritySnapshot expected = AzerothRegionAuthority.CreateSnapshot();
        byte[] expectedBytes = SerializePayload(expected);
        byte[] suppliedBytes = SerializePayload(snapshot);
        if (expectedBytes.Length != suppliedBytes.Length ||
            !CryptographicOperations.FixedTimeEquals(expectedBytes, suppliedBytes))
        {
            throw new InvalidDataException(
                "Azeroth region authority must remain exactly Eversong, Ghostlands, Eastern Plaguelands, and Western Plaguelands.");
        }
    }

    private static byte[] SerializePayload(AzerothRegionAuthoritySnapshot snapshot) =>
        JsonSerializer.SerializeToUtf8Bytes(
            snapshot,
            AzerothSimulationJsonContext.Default.AzerothRegionAuthoritySnapshot);

    private static string ComputeSha256(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
