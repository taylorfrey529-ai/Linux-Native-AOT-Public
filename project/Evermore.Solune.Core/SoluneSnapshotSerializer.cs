using System.Security.Cryptography;
using System.Text.Json;

namespace Evermore.Solune.Core;

public static class SoluneSnapshotSerializer
{
    public const string SchemaVersion = "evermore.solune.local-sun/1.0";
    public const string Authority = "isolated-noncanon-contained-stellar-construct";

    public static string Serialize(SoluneSnapshot snapshot)
    {
        ValidateSnapshot(snapshot);
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(
            snapshot,
            SoluneJsonContext.Default.SoluneSnapshot);
        var envelope = new SoluneSnapshotEnvelope(
            SchemaVersion,
            Authority,
            ComputeSha256(payload),
            snapshot);
        return JsonSerializer.Serialize(
            envelope,
            SoluneJsonContext.Default.SoluneSnapshotEnvelope);
    }

    public static SoluneSnapshotEnvelope DeserializeAndVerify(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        SoluneSnapshotEnvelope envelope = JsonSerializer.Deserialize(
            json,
            SoluneJsonContext.Default.SoluneSnapshotEnvelope)
            ?? throw new JsonException("The Solune snapshot envelope was null.");

        if (!StringComparer.Ordinal.Equals(envelope.SchemaVersion, SchemaVersion))
            throw new InvalidDataException($"Unsupported Solune snapshot schema: {envelope.SchemaVersion}.");
        if (!StringComparer.Ordinal.Equals(envelope.Authority, Authority))
            throw new InvalidDataException($"Unsupported Solune snapshot authority: {envelope.Authority}.");
        if (string.IsNullOrEmpty(envelope.PayloadSha256) || envelope.PayloadSha256.Length != 64)
            throw new InvalidDataException("The Solune snapshot payload hash is malformed.");

        ValidateSnapshot(envelope.Payload);
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(
            envelope.Payload,
            SoluneJsonContext.Default.SoluneSnapshot);
        string actualHash = ComputeSha256(payload);
        if (!FixedTimeHexEquals(envelope.PayloadSha256, actualHash))
            throw new InvalidDataException("Solune snapshot payload integrity verification failed.");
        return envelope;
    }

    private static void ValidateSnapshot(SoluneSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(snapshot.Model);
        ArgumentNullException.ThrowIfNull(snapshot.History);
        ArgumentNullException.ThrowIfNull(snapshot.Orbit);
        ArgumentNullException.ThrowIfNull(snapshot.Radiance);
        ArgumentNullException.ThrowIfNull(snapshot.Calendar);
        ArgumentNullException.ThrowIfNull(snapshot.OperationDirectives);
        ArgumentNullException.ThrowIfNull(snapshot.Events);

        SoluneSnapshot expected;
        try
        {
            expected = new DeterministicSoluneProcessor().Process(
                new SoluneInput(
                    snapshot.Generation,
                    snapshot.EvercycleStepMicrodays,
                    snapshot.Model,
                    snapshot.History));
        }
        catch (ArgumentException ex)
        {
            throw new InvalidDataException("The Solune snapshot carries invalid bounded model input.", ex);
        }

        byte[] expectedBytes = JsonSerializer.SerializeToUtf8Bytes(
            expected,
            SoluneJsonContext.Default.SoluneSnapshot);
        byte[] actualBytes = JsonSerializer.SerializeToUtf8Bytes(
            snapshot,
            SoluneJsonContext.Default.SoluneSnapshot);
        if (!CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes))
        {
            throw new InvalidDataException(
                "Solune snapshot semantic verification failed against its deterministic input, equations, continuity, containment, directives, or events.");
        }
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
