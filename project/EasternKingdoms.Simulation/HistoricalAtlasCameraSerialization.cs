using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EasternKingdoms.Simulation;

public sealed class HistoricalAtlasCameraSerializer
{
    public const string SchemaVersion = "evermore.eastern-kingdoms.camera/23.0";

    private readonly HistoricalAtlasSettings _settings;
    private readonly IReadOnlySet<string> _authorizedZones;

    public HistoricalAtlasCameraSerializer(
        IEnumerable<string> authorizedZoneIds,
        HistoricalAtlasSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(authorizedZoneIds);
        _authorizedZones = authorizedZoneIds.ToHashSet(StringComparer.Ordinal);
        if (_authorizedZones.Count == 0)
            throw new InvalidOperationException("Camera serialization requires authorized Eastern Kingdoms zones.");
        _settings = settings ?? new HistoricalAtlasSettings();
        _settings.Validate();
    }

    public HistoricalAtlasCameraSerializationResult Serialize(HistoricalAtlasCameraState camera)
    {
        ArgumentNullException.ThrowIfNull(camera);
        camera.Validate(_settings, _authorizedZones);
        JsonElement payloadElement = JsonSerializer.SerializeToElement(camera, HistoricalJsonContext.Default.HistoricalAtlasCameraState);
        string payloadCanonical = CanonicalJson.Canonicalize(payloadElement);
        string payloadHash = Hash(payloadCanonical);
        var file = new HistoricalAtlasCameraFile(
            SchemaVersion,
            HistoricalAtlasAuthority.TestHistoryOnly,
            payloadHash,
            camera);
        string json = CanonicalJson.Canonicalize(JsonSerializer.SerializeToElement(file, HistoricalJsonContext.Default.HistoricalAtlasCameraFile));
        return new HistoricalAtlasCameraSerializationResult(json, payloadHash, file);
    }

    public HistoricalAtlasCameraFile DeserializeAndVerify(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("Camera JSON is required.", nameof(json));
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("Camera state root must be an object.");
        if (!root.TryGetProperty("schemaVersion", out var schema) || !StringComparer.Ordinal.Equals(schema.GetString(), SchemaVersion))
            throw new InvalidOperationException("Camera state schema version is unsupported.");
        if (!root.TryGetProperty("authority", out var authority) || !StringComparer.Ordinal.Equals(authority.GetString(), nameof(HistoricalAtlasAuthority.TestHistoryOnly)))
            throw new InvalidOperationException("Camera state authority must remain TestHistoryOnly.");
        if (!root.TryGetProperty("payloadSha256", out var hashElement) || hashElement.ValueKind != JsonValueKind.String)
            throw new InvalidOperationException("Camera payload hash is missing.");
        if (!root.TryGetProperty("camera", out var cameraElement))
            throw new InvalidOperationException("Camera payload is missing.");

        string expected = hashElement.GetString() ?? string.Empty;
        string actual = Hash(CanonicalJson.Canonicalize(cameraElement));
        if (!StringComparer.Ordinal.Equals(expected, actual))
            throw new InvalidOperationException("Camera payload integrity verification failed.");

        var file = JsonSerializer.Deserialize(json, HistoricalJsonContext.Default.HistoricalAtlasCameraFile)
            ?? throw new InvalidOperationException("Camera state could not be deserialized.");
        file.Camera.Validate(_settings, _authorizedZones);
        return file;
    }

    private static string Hash(string canonical) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
}
