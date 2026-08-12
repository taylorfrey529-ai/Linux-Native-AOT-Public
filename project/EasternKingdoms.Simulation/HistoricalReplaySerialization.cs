using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EasternKingdoms.Simulation;

public sealed class HistoricalReplaySerializer
{
    private readonly HistoricalTimelineIndexer _timeline;
    private readonly HistoricalVisualizationSettings _settings;

    public HistoricalReplaySerializer(
        HistoricalTimelineIndexer timeline,
        HistoricalVisualizationSettings? settings = null)
    {
        _timeline = timeline ?? throw new ArgumentNullException(nameof(timeline));
        _settings = settings ?? new HistoricalVisualizationSettings();
        _settings.Validate();
    }

    public HistoricalReplaySerializationResult Serialize(IEnumerable<HistoricalPlaybackFrame> frames)
    {
        ArgumentNullException.ThrowIfNull(frames);
        var source = frames.OrderBy(x => x.FrameIndex).ThenBy(x => x.World.Generation).ToArray();
        if (source.Length == 0)
            throw new InvalidOperationException("Historical replay serialization requires at least one frame.");
        var timeline = _timeline.Build(source);
        var serializedFrames = source.Select(Map).ToArray();
        var payload = new HistoricalReplayPayload(timeline, Array.AsReadOnly(serializedFrames));
        JsonElement payloadElement = JsonSerializer.SerializeToElement(payload, HistoricalJsonContext.Default.HistoricalReplayPayload);
        string payloadCanonical = CanonicalJson.Canonicalize(payloadElement);
        string payloadHash = Hash(payloadCanonical);
        var file = new HistoricalReplayFile(
            _settings.ReplaySchemaVersion,
            HistoricalVisualizationAuthority.TestHistoryOnly,
            payloadHash,
            payload);
        JsonElement fileElement = JsonSerializer.SerializeToElement(file, HistoricalJsonContext.Default.HistoricalReplayFile);
        string json = CanonicalJson.Canonicalize(fileElement);
        return new HistoricalReplaySerializationResult(json, payloadHash, file);
    }

    public HistoricalReplayFile DeserializeAndVerify(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("Replay JSON is required.", nameof(json));

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("Replay root must be an object.");
        if (!root.TryGetProperty("schemaVersion", out var schema) || !StringComparer.Ordinal.Equals(schema.GetString(), _settings.ReplaySchemaVersion))
            throw new InvalidOperationException("Replay schema version is unsupported.");
        if (!root.TryGetProperty("authority", out var authority) || !StringComparer.Ordinal.Equals(authority.GetString(), nameof(HistoricalVisualizationAuthority.TestHistoryOnly)))
            throw new InvalidOperationException("Replay authority must remain TestHistoryOnly.");
        if (!root.TryGetProperty("payloadSha256", out var expectedHashElement) || expectedHashElement.ValueKind != JsonValueKind.String)
            throw new InvalidOperationException("Replay payload hash is missing.");
        if (!root.TryGetProperty("payload", out var payloadElement))
            throw new InvalidOperationException("Replay payload is missing.");

        string expectedHash = expectedHashElement.GetString() ?? string.Empty;
        string actualHash = Hash(CanonicalJson.Canonicalize(payloadElement));
        if (!StringComparer.Ordinal.Equals(expectedHash, actualHash))
            throw new InvalidOperationException("Replay payload integrity verification failed.");

        var file = JsonSerializer.Deserialize(json, HistoricalJsonContext.Default.HistoricalReplayFile)
            ?? throw new InvalidOperationException("Replay JSON could not be deserialized.");
        if (!_timeline.Verify(file.Payload.Timeline))
            throw new InvalidOperationException("Replay timeline integrity verification failed.");
        VerifyFrameTimelineConsistency(file);
        return file;
    }

    private static SerializedHistoricalFrame Map(HistoricalPlaybackFrame frame)
    {
        if (!HistoricalPlaybackVerifier.Verify(frame.World))
            throw new InvalidOperationException("Replay serialization requires verified historical snapshots.");

        var cells = frame.World.Cells.OrderBy(x => x.ZoneId, StringComparer.Ordinal).Select(cell => new SerializedHistoricalCell(
            cell.ZoneId,
            frame.World.Generation,
            cell.Climate?.Regime,
            cell.Climate?.Temperature,
            cell.Climate?.Moisture,
            cell.Landscape?.ForestCover,
            cell.Landscape?.WetlandExtent,
            cell.Landscape?.RiverConnectivity,
            cell.Landscape?.CoastalExposure,
            cell.Landscape?.TerrainIntegrity,
            cell.Biodiversity?.LineageRichness,
            cell.Biodiversity?.SpeciesFamilyRichness,
            cell.Biodiversity?.ApproximatePopulation,
            Array.AsReadOnly(cell.Lineages.Select(x => x.LineageId).OrderBy(x => x, StringComparer.Ordinal).ToArray()),
            cell.Completeness,
            cell.IntegrityHash)).ToArray();

        return new SerializedHistoricalFrame(
            frame.FrameIndex,
            frame.World.Generation,
            Array.AsReadOnly(cells),
            frame.World.Completeness,
            frame.World.IntegrityHash);
    }

    private static void VerifyFrameTimelineConsistency(HistoricalReplayFile file)
    {
        var entries = file.Payload.Timeline.Entries.ToDictionary(x => x.FrameIndex);
        if (entries.Count != file.Payload.Frames.Count)
            throw new InvalidOperationException("Replay timeline and frame counts differ.");

        foreach (var frame in file.Payload.Frames)
        {
            if (!entries.TryGetValue(frame.FrameIndex, out var entry))
                throw new InvalidOperationException("Replay frame is not indexed.");
            if (entry.Generation != frame.Generation || !StringComparer.Ordinal.Equals(entry.WorldIntegrityHash, frame.SourceWorldIntegrityHash))
                throw new InvalidOperationException("Replay timeline does not match serialized frames.");
            if (entry.Completeness != frame.Completeness)
                throw new InvalidOperationException("Replay timeline completeness does not match its frame.");
            if (frame.Cells.Any(x => x.Generation != frame.Generation))
                throw new InvalidOperationException("Replay cell generation does not match its frame.");
            if (frame.Cells.Select(x => x.ZoneId).Distinct(StringComparer.Ordinal).Count() != frame.Cells.Count)
                throw new InvalidOperationException("Replay frames cannot contain duplicate zones.");
            var indexedZones = entry.ZoneIds.OrderBy(x => x, StringComparer.Ordinal).ToArray();
            var frameZones = frame.Cells.Select(x => x.ZoneId).OrderBy(x => x, StringComparer.Ordinal).ToArray();
            if (!indexedZones.SequenceEqual(frameZones, StringComparer.Ordinal))
                throw new InvalidOperationException("Replay timeline zones do not match serialized frame zones.");
            foreach (var cell in frame.Cells)
            {
                if (string.IsNullOrWhiteSpace(cell.SourceIntegrityHash))
                    throw new InvalidOperationException("Replay cells require source integrity hashes.");
                if (cell.LivingLineageIds.Distinct(StringComparer.Ordinal).Count() != cell.LivingLineageIds.Count)
                    throw new InvalidOperationException("Replay cells cannot contain duplicate living lineage IDs.");
            }
        }
    }

    private static string Hash(string canonical) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
}

internal static class CanonicalJson
{
    public static string Canonicalize(JsonElement element)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
            Write(writer, element);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void Write(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    Write(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray()) Write(writer, item);
                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;
            case JsonValueKind.Number:
                writer.WriteRawValue(element.GetRawText(), skipInputValidation: false);
                break;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                writer.WriteNullValue();
                break;
            default:
                throw new InvalidOperationException($"Unsupported JSON value kind: {element.ValueKind}.");
        }
    }
}
