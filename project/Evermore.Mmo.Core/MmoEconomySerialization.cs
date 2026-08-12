using System.Security.Cryptography;
using System.Text.Json;

namespace Evermore.Mmo.Core;

public static class MmoEconomySerializer
{
    public const string SchemaVersion = "evermore.mmo.economy/1.0";
    public const string Authority = MmoEconomyEngine.Authority;

    public static string Serialize(MmoEconomySnapshot snapshot)
    {
        Validate(snapshot);
        byte[] payload = SerializePayload(snapshot);
        return JsonSerializer.Serialize(
            new MmoEconomyEnvelope(
                SchemaVersion,
                Authority,
                MmoCapabilitySerializer.ComputeSha256(payload),
                snapshot),
            MmoJsonContext.Default.MmoEconomyEnvelope);
    }

    public static MmoEconomyEnvelope DeserializeAndVerify(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        MmoEconomyEnvelope envelope = JsonSerializer.Deserialize(
            json,
            MmoJsonContext.Default.MmoEconomyEnvelope)
            ?? throw new JsonException("The MMORPG economy envelope was null.");
        MmoCapabilitySerializer.VerifyHeader(
            envelope.SchemaVersion,
            envelope.Authority,
            envelope.PayloadSha256,
            SchemaVersion,
            Authority);
        MmoCapabilitySerializer.VerifyHash(
            envelope.PayloadSha256,
            SerializePayload(envelope.Payload));
        Validate(envelope.Payload);
        return envelope;
    }

    private static void Validate(MmoEconomySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        MmoEconomySnapshot replay = new MmoEconomyEngine().Process(snapshot.Input);
        byte[] expected = SerializePayload(replay);
        byte[] actual = SerializePayload(snapshot);
        if (expected.Length != actual.Length ||
            !CryptographicOperations.FixedTimeEquals(expected, actual))
        {
            throw new InvalidDataException("MMORPG economy semantic replay verification failed.");
        }
    }

    private static byte[] SerializePayload(MmoEconomySnapshot snapshot) =>
        JsonSerializer.SerializeToUtf8Bytes(
            snapshot,
            MmoJsonContext.Default.MmoEconomySnapshot);
}
