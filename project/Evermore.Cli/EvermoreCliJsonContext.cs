using System.Text.Json.Serialization;

namespace Evermore.Cli;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    GenerationMode = JsonSourceGenerationMode.Serialization,
    WriteIndented = true)]
[JsonSerializable(typeof(CliStatusOutput))]
[JsonSerializable(typeof(ReplayVerificationOutput))]
[JsonSerializable(typeof(CameraVerificationOutput))]
[JsonSerializable(typeof(SceneVerificationOutput))]
[JsonSerializable(typeof(OmegaCliOutput))]
internal sealed partial class EvermoreCliJsonContext : JsonSerializerContext;

internal sealed record CliStatusOutput(
    string Product,
    string Phase,
    string CliVersion,
    string TargetFramework,
    string ValidationRid,
    string Authority,
    string Mode,
    string NativeAotCertification);

internal sealed record ReplayVerificationOutput(
    string Kind,
    string Path,
    string SchemaVersion,
    string Authority,
    string PayloadSha256,
    int FrameCount,
    int FirstGeneration,
    int LastGeneration,
    int ZoneCount);

internal sealed record CameraVerificationOutput(
    string Kind,
    string Path,
    string SchemaVersion,
    string Authority,
    string PayloadSha256,
    string Projection,
    double Zoom,
    string? FocusZoneId);

internal sealed record SceneVerificationOutput(
    string Kind,
    string Path,
    string SchemaVersion,
    string Authority,
    string PayloadSha256,
    int FrameIndex,
    int Generation,
    int ZoneCount,
    int LineageRangeCount);

internal sealed record OmegaCliOutput(
    string Kind,
    string Definition,
    string Status,
    ulong Seed,
    int RequestedGenerations,
    int ProcessedGenerations,
    int Population,
    float AverageFitness,
    string DominantTrait,
    int ArchiveCheckpoints,
    int ResonanceEvents,
    string? ContainmentReason);
