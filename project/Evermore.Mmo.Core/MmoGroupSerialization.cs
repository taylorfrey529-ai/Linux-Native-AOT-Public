using System.Security.Cryptography;
using System.Text.Json;

namespace Evermore.Mmo.Core;

public static class MmoGroupSerializer
{
    public const string SchemaVersion = "evermore.mmo.group/1.0";
    public const string Authority = MmoGroupEngine.Authority;

    public static string Serialize(MmoGroupSnapshot snapshot)
    {
        Validate(snapshot);
        byte[] payload = SerializePayload(snapshot);
        return JsonSerializer.Serialize(
            new MmoGroupEnvelope(
                SchemaVersion,
                Authority,
                MmoCapabilitySerializer.ComputeSha256(payload),
                snapshot),
            MmoJsonContext.Default.MmoGroupEnvelope);
    }

    public static MmoGroupEnvelope DeserializeAndVerify(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        MmoGroupEnvelope envelope = JsonSerializer.Deserialize(
            json,
            MmoJsonContext.Default.MmoGroupEnvelope)
            ?? throw new JsonException("The MMORPG group envelope was null.");
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

    private static void Validate(MmoGroupSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        MmoGroupSnapshot replay = new MmoGroupEngine().Process(snapshot.Input);
        byte[] expected = SerializePayload(replay);
        byte[] actual = SerializePayload(snapshot);
        if (expected.Length != actual.Length ||
            !CryptographicOperations.FixedTimeEquals(expected, actual))
        {
            throw new InvalidDataException("MMORPG group semantic replay verification failed.");
        }
    }

    private static byte[] SerializePayload(MmoGroupSnapshot snapshot) =>
        JsonSerializer.SerializeToUtf8Bytes(
            snapshot,
            MmoJsonContext.Default.MmoGroupSnapshot);
}
