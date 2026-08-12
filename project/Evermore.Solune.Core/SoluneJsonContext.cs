using System.Text.Json.Serialization;

namespace Evermore.Solune.Core;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    GenerationMode = JsonSourceGenerationMode.Default,
    WriteIndented = false)]
[JsonSerializable(typeof(SoluneInput))]
[JsonSerializable(typeof(SoluneSnapshot))]
[JsonSerializable(typeof(SoluneSnapshotEnvelope))]
public sealed partial class SoluneJsonContext : JsonSerializerContext;
