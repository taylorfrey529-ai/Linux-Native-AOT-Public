using EasternKingdoms.Simulation;
using System.Text.Json.Serialization;

namespace Evermore.Azeroth.Simulation;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    GenerationMode = JsonSourceGenerationMode.Default,
    WriteIndented = false)]
[JsonSerializable(typeof(AzerothRegionAuthoritySnapshot))]
[JsonSerializable(typeof(AzerothRegionAuthorityEnvelope))]
[JsonSerializable(typeof(AzerothSimulationInput))]
[JsonSerializable(typeof(AzerothSimulationSnapshot))]
[JsonSerializable(typeof(AzerothSimulationEnvelope))]
[JsonSerializable(typeof(AzerothEnvironmentalSimulationInput))]
[JsonSerializable(typeof(AzerothEnvironmentalSimulationSnapshot))]
[JsonSerializable(typeof(AzerothEnvironmentalSimulationEnvelope))]
[JsonSerializable(typeof(AzerothOrbitalSceneInput))]
[JsonSerializable(typeof(AzerothOrbitalSceneSnapshot))]
[JsonSerializable(typeof(AzerothOrbitalSceneEnvelope))]
public sealed partial class AzerothSimulationJsonContext : JsonSerializerContext;

