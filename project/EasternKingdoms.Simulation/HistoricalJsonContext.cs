using System.Text.Json.Serialization;

namespace EasternKingdoms.Simulation;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    GenerationMode = JsonSourceGenerationMode.Metadata,
    UseStringEnumConverter = true,
    WriteIndented = false)]
[JsonSerializable(typeof(HistoricalAtlasScenePayload))]
[JsonSerializable(typeof(HistoricalAtlasSceneFile))]
[JsonSerializable(typeof(HistoricalAtlasCameraState))]
[JsonSerializable(typeof(HistoricalAtlasCameraFile))]
[JsonSerializable(typeof(HistoricalReplayPayload))]
[JsonSerializable(typeof(HistoricalReplayFile))]
internal sealed partial class HistoricalJsonContext : JsonSerializerContext;
