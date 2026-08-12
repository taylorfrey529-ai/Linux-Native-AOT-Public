using System.Security.Cryptography;
using System.Text.Json;

namespace Evermore.Azeroth.Simulation;

public static class AzerothOrbitalSceneSerializer
{
    public const string SchemaVersion = "evermore.azeroth.orbital-scene/1.0";
    public const string Authority = "TestHistoryOnly";

    public static string Serialize(AzerothOrbitalSceneSnapshot snapshot)
    {
        ValidateSnapshot(snapshot);
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(
            snapshot,
            AzerothSimulationJsonContext.Default.AzerothOrbitalSceneSnapshot);
        var envelope = new AzerothOrbitalSceneEnvelope(
            SchemaVersion,
            Authority,
            ComputeSha256(payload),
            snapshot);
        return JsonSerializer.Serialize(
            envelope,
            AzerothSimulationJsonContext.Default.AzerothOrbitalSceneEnvelope);
    }

    public static AzerothOrbitalSceneEnvelope DeserializeAndVerify(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        AzerothOrbitalSceneEnvelope envelope = JsonSerializer.Deserialize(
            json,
            AzerothSimulationJsonContext.Default.AzerothOrbitalSceneEnvelope)
            ?? throw new JsonException("The Azeroth orbital scene envelope was null.");

        if (!StringComparer.Ordinal.Equals(envelope.SchemaVersion, SchemaVersion))
            throw new InvalidDataException($"Unsupported Azeroth orbital scene schema: {envelope.SchemaVersion}.");
        if (!StringComparer.Ordinal.Equals(envelope.Authority, Authority))
            throw new InvalidDataException($"Unsupported Azeroth orbital scene authority: {envelope.Authority}.");
        if (string.IsNullOrEmpty(envelope.PayloadSha256) || envelope.PayloadSha256.Length != 64)
            throw new InvalidDataException("The Azeroth orbital scene payload hash is malformed.");

        ValidateSnapshot(envelope.Payload);
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(
            envelope.Payload,
            AzerothSimulationJsonContext.Default.AzerothOrbitalSceneSnapshot);
        string actualHash = ComputeSha256(payload);
        if (!FixedTimeHexEquals(envelope.PayloadSha256, actualHash))
            throw new InvalidDataException("Azeroth orbital scene payload integrity verification failed.");
        return envelope;
    }

    private static void ValidateSnapshot(AzerothOrbitalSceneSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(snapshot.Scale);
        ArgumentNullException.ThrowIfNull(snapshot.Camera);
        ArgumentNullException.ThrowIfNull(snapshot.Lighting);
        ArgumentNullException.ThrowIfNull(snapshot.Zones);
        ArgumentNullException.ThrowIfNull(snapshot.Environment);
        ArgumentNullException.ThrowIfNull(snapshot.Input);

        AzerothOrbitalSceneSnapshot expected;
        try
        {
            expected = new AzerothOrbitalSceneEngine().Process(snapshot.Input);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidDataException)
        {
            throw new InvalidDataException("The Azeroth orbital scene carries invalid bounded input.", ex);
        }

        byte[] expectedBytes = JsonSerializer.SerializeToUtf8Bytes(
            expected,
            AzerothSimulationJsonContext.Default.AzerothOrbitalSceneSnapshot);
        byte[] actualBytes = JsonSerializer.SerializeToUtf8Bytes(
            snapshot,
            AzerothSimulationJsonContext.Default.AzerothOrbitalSceneSnapshot);
        if (!CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes))
        {
            throw new InvalidDataException(
                "Azeroth orbital scene semantic verification failed against its deterministic environmental and view inputs.");
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
