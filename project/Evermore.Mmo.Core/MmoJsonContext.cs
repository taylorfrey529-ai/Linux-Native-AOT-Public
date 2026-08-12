using System.Text.Json.Serialization;

namespace Evermore.Mmo.Core;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    GenerationMode = JsonSourceGenerationMode.Default,
    UseStringEnumConverter = true,
    WriteIndented = false)]
[JsonSerializable(typeof(MmoCapabilityCatalogSnapshot))]
[JsonSerializable(typeof(MmoCapabilityEnvelope))]
[JsonSerializable(typeof(MmoSimulationInput))]
[JsonSerializable(typeof(MmoSimulationSnapshot))]
[JsonSerializable(typeof(MmoSimulationEnvelope))]
[JsonSerializable(typeof(MmoEconomyInput))]
[JsonSerializable(typeof(MmoEconomySnapshot))]
[JsonSerializable(typeof(MmoEconomyEnvelope))]
[JsonSerializable(typeof(MmoGroupInput))]
[JsonSerializable(typeof(MmoGroupSnapshot))]
[JsonSerializable(typeof(MmoGroupEnvelope))]
public sealed partial class MmoJsonContext : JsonSerializerContext;
