using System.Security.Cryptography;
using System.Text.Json;

namespace Evermore.Azeroth.Simulation;

public static class AzerothEnvironmentalFieldSnapshotSerializer
{
    public const string SchemaVersion = "evermore.azeroth.environmental-field/1.0";
    public const string Authority = "TestHistoryOnly";

    public static string Serialize(AzerothEnvironmentalSimulationSnapshot snapshot)
    {
        ValidateSnapshot(snapshot);
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(
            snapshot,
            AzerothSimulationJsonContext.Default.AzerothEnvironmentalSimulationSnapshot);
        var envelope = new AzerothEnvironmentalSimulationEnvelope(
            SchemaVersion,
            Authority,
            ComputeSha256(payload),
            snapshot);
        return JsonSerializer.Serialize(
            envelope,
            AzerothSimulationJsonContext.Default.AzerothEnvironmentalSimulationEnvelope);
    }

    public static AzerothEnvironmentalSimulationEnvelope DeserializeAndVerify(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        AzerothEnvironmentalSimulationEnvelope envelope = JsonSerializer.Deserialize(
            json,
            AzerothSimulationJsonContext.Default.AzerothEnvironmentalSimulationEnvelope)
            ?? throw new JsonException("The Azeroth environmental field envelope was null.");

        if (!StringComparer.Ordinal.Equals(envelope.SchemaVersion, SchemaVersion))
            throw new InvalidDataException($"Unsupported Azeroth environmental field schema: {envelope.SchemaVersion}.");
        if (!StringComparer.Ordinal.Equals(envelope.Authority, Authority))
            throw new InvalidDataException($"Unsupported Azeroth environmental field authority: {envelope.Authority}.");
        if (string.IsNullOrEmpty(envelope.PayloadSha256) || envelope.PayloadSha256.Length != 64)
            throw new InvalidDataException("The Azeroth environmental field payload hash is malformed.");

        ValidateSnapshot(envelope.Payload);
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(
            envelope.Payload,
            AzerothSimulationJsonContext.Default.AzerothEnvironmentalSimulationSnapshot);
        string actualHash = ComputeSha256(payload);
        if (!FixedTimeHexEquals(envelope.PayloadSha256, actualHash))
            throw new InvalidDataException("Azeroth environmental field payload integrity verification failed.");
        return envelope;
    }

    private static void ValidateSnapshot(AzerothEnvironmentalSimulationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(snapshot.Simulation);
        ArgumentNullException.ThrowIfNull(snapshot.Environment);
        ArgumentNullException.ThrowIfNull(snapshot.Input);

        AzerothEnvironmentalSimulationSnapshot expected;
        try
        {
            expected = new AzerothEnvironmentalFieldEngine().Process(snapshot.Input);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidDataException)
        {
            throw new InvalidDataException("The Azeroth environmental field snapshot carries invalid bounded input.", ex);
        }

        byte[] expectedBytes = JsonSerializer.SerializeToUtf8Bytes(
            expected,
            AzerothSimulationJsonContext.Default.AzerothEnvironmentalSimulationSnapshot);
        byte[] actualBytes = JsonSerializer.SerializeToUtf8Bytes(
            snapshot,
            AzerothSimulationJsonContext.Default.AzerothEnvironmentalSimulationSnapshot);
        if (!CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes))
        {
            throw new InvalidDataException(
                "Azeroth environmental field semantic verification failed against its deterministic authored inputs.");
        }
    }

    private static string ComputeSha256(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static bool FixedTimeHexEquals(string expected, string actual)
    {
        try
        {
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(expected),
                Convert.FromHexString(actual));
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
