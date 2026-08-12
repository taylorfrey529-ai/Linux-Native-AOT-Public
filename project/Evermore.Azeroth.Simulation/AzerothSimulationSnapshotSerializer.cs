using System.Security.Cryptography;
using System.Text.Json;

namespace Evermore.Azeroth.Simulation;

public static class AzerothSimulationSnapshotSerializer
{
    public const string SchemaVersion = "evermore.azeroth.composed-simulation/1.0";
    public const string Authority = "TestHistoryOnly";

    public static string Serialize(AzerothSimulationSnapshot snapshot)
    {
        ValidateSnapshot(snapshot);
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(
            snapshot,
            AzerothSimulationJsonContext.Default.AzerothSimulationSnapshot);
        var envelope = new AzerothSimulationEnvelope(
            SchemaVersion,
            Authority,
            ComputeSha256(payload),
            snapshot);
        return JsonSerializer.Serialize(
            envelope,
            AzerothSimulationJsonContext.Default.AzerothSimulationEnvelope);
    }

    public static AzerothSimulationEnvelope DeserializeAndVerify(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        AzerothSimulationEnvelope envelope = JsonSerializer.Deserialize(
            json,
            AzerothSimulationJsonContext.Default.AzerothSimulationEnvelope)
            ?? throw new JsonException("The Azeroth simulation envelope was null.");

        if (!StringComparer.Ordinal.Equals(envelope.SchemaVersion, SchemaVersion))
            throw new InvalidDataException($"Unsupported Azeroth simulation schema: {envelope.SchemaVersion}.");
        if (!StringComparer.Ordinal.Equals(envelope.Authority, Authority))
            throw new InvalidDataException($"Unsupported Azeroth simulation authority: {envelope.Authority}.");
        if (string.IsNullOrEmpty(envelope.PayloadSha256) || envelope.PayloadSha256.Length != 64)
            throw new InvalidDataException("The Azeroth simulation payload hash is malformed.");

        ValidateSnapshot(envelope.Payload);
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(
            envelope.Payload,
            AzerothSimulationJsonContext.Default.AzerothSimulationSnapshot);
        string actualHash = ComputeSha256(payload);
        if (!FixedTimeHexEquals(envelope.PayloadSha256, actualHash))
            throw new InvalidDataException("Azeroth simulation payload integrity verification failed.");
        return envelope;
    }

    private static void ValidateSnapshot(AzerothSimulationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(snapshot.Azeroth);
        ArgumentNullException.ThrowIfNull(snapshot.Omega);
        ArgumentNullException.ThrowIfNull(snapshot.Lunara);
        ArgumentNullException.ThrowIfNull(snapshot.Solune);
        ArgumentNullException.ThrowIfNull(snapshot.ContainmentReasons);
        ArgumentNullException.ThrowIfNull(snapshot.Input);

        AzerothSimulationSnapshot expected;
        try
        {
            expected = new AzerothSimulationEngine().Process(snapshot.Input);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidDataException("The Azeroth simulation snapshot carries invalid bounded input.", ex);
        }

        byte[] expectedBytes = JsonSerializer.SerializeToUtf8Bytes(
            expected,
            AzerothSimulationJsonContext.Default.AzerothSimulationSnapshot);
        byte[] actualBytes = JsonSerializer.SerializeToUtf8Bytes(
            snapshot,
            AzerothSimulationJsonContext.Default.AzerothSimulationSnapshot);
        if (!CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes))
            throw new InvalidDataException("Azeroth simulation semantic verification failed against its deterministic component inputs.");
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
