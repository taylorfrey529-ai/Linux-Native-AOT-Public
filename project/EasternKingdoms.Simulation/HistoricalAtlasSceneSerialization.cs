using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EasternKingdoms.Simulation;

public sealed class HistoricalAtlasSceneSerializer
{
    public const string SchemaVersion = "evermore.eastern-kingdoms.atlas-scene/23.0";

    private readonly HistoricalAtlasSceneBuilder _scenes;

    public HistoricalAtlasSceneSerializer(HistoricalAtlasSceneBuilder scenes)
    {
        _scenes = scenes ?? throw new ArgumentNullException(nameof(scenes));
    }

    public HistoricalAtlasSceneSerializationResult Serialize(HistoricalAtlasScenePayload scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        if (!_scenes.Verify(scene))
            throw new InvalidOperationException("Atlas scene serialization requires a verified renderer-neutral scene payload.");
        JsonElement sceneElement = JsonSerializer.SerializeToElement(scene, HistoricalJsonContext.Default.HistoricalAtlasScenePayload);
        string canonical = CanonicalJson.Canonicalize(sceneElement);
        string hash = Hash(canonical);
        var file = new HistoricalAtlasSceneFile(SchemaVersion, HistoricalAtlasAuthority.TestHistoryOnly, hash, scene);
        string json = CanonicalJson.Canonicalize(JsonSerializer.SerializeToElement(file, HistoricalJsonContext.Default.HistoricalAtlasSceneFile));
        return new HistoricalAtlasSceneSerializationResult(json, hash, file);
    }

    public HistoricalAtlasSceneFile DeserializeAndVerify(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("Atlas scene JSON is required.", nameof(json));
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("Atlas scene file root must be an object.");
        if (!root.TryGetProperty("schemaVersion", out var schema) || !StringComparer.Ordinal.Equals(schema.GetString(), SchemaVersion))
            throw new InvalidOperationException("Atlas scene schema version is unsupported.");
        if (!root.TryGetProperty("authority", out var authority) || !StringComparer.Ordinal.Equals(authority.GetString(), nameof(HistoricalAtlasAuthority.TestHistoryOnly)))
            throw new InvalidOperationException("Atlas scene authority must remain TestHistoryOnly.");
        if (!root.TryGetProperty("payloadSha256", out var hashElement) || hashElement.ValueKind != JsonValueKind.String)
            throw new InvalidOperationException("Atlas scene payload hash is missing.");
        if (!root.TryGetProperty("scene", out var sceneElement))
            throw new InvalidOperationException("Atlas scene payload is missing.");

        string expected = hashElement.GetString() ?? string.Empty;
        string actual = Hash(CanonicalJson.Canonicalize(sceneElement));
        if (!StringComparer.Ordinal.Equals(expected, actual))
            throw new InvalidOperationException("Atlas scene payload integrity verification failed.");

        var file = JsonSerializer.Deserialize(json, HistoricalJsonContext.Default.HistoricalAtlasSceneFile)
            ?? throw new InvalidOperationException("Atlas scene JSON could not be deserialized.");
        if (!_scenes.Verify(file.Scene))
            throw new InvalidOperationException("Atlas scene semantic verification failed.");
        return file;
    }

    private static string Hash(string canonical) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
}
