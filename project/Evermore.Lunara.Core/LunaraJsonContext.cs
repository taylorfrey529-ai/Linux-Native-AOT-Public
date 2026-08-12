using System.Text.Json.Serialization;

namespace Evermore.Lunara.Core;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    GenerationMode = JsonSourceGenerationMode.Default,
    WriteIndented = false)]
[JsonSerializable(typeof(MoonTideInput))]
[JsonSerializable(typeof(MoonTideSnapshot))]
[JsonSerializable(typeof(MoonTideSnapshotEnvelope))]
[JsonSerializable(typeof(LunaraApiPostRequest))]
[JsonSerializable(typeof(LunaraApiRawResponse))]
public sealed partial class LunaraJsonContext : JsonSerializerContext;
