# Phase XXIII C# Source Guide

Phase XXIII is engine-agnostic C# targeting the existing .NET 8 solution.

## New runtime files

- `HistoricalAtlasModels.cs` - immutable atlas/session/scene DTOs and enums.
- `HistoricalAtlasHashing.cs` - deterministic scene, comparison, and session hashes.
- `HistoricalTimelineScrubber.cs` - exact/previous/next/nearest recorded-frame resolution with no interpolation.
- `HistoricalAtlasSceneBuilder.cs` - verified Phase XXI frame to renderer-neutral atlas payload.
- `HistoricalAtlasComparison.cs` - side-by-side and delta comparison payloads.
- `HistoricalAtlasCameraSerialization.cs` - canonical JSON camera-state persistence.
- `HistoricalAtlasSceneSerialization.cs` - canonical JSON renderer-neutral scene persistence.
- `HistoricalAtlasSession.cs` - mutable display session isolated from simulation state.
- `AzerothInteractiveAtlasBootstrap.cs` - composition root over Phase XXII services.
- `HistoricalAtlasUsage.cs` - minimal default session helper.

## New tests

`EasternKingdoms.Simulation.Tests/HistoricalInteractiveAtlasTests.cs` covers:
- deterministic nearest-frame tie handling
- exact scrub refusal for missing generations
- disabled-layer data masking
- evidence provenance export
- lineage-range export
- camera serialization determinism and tamper detection
- renderer-neutral scene serialization
- delta comparison verification
- unauthorized camera focus rejection
- session-state integrity
- non-mutation of source playback state during comparison

## Composition

```csharp
var services = AzerothInteractiveAtlasBootstrap.Create(genomeCatalog);
var frames = services.Visualization.Historical.Playback.Play(
    startGeneration: 0,
    endGeneration: 100,
    step: 10,
    services.AuthorizedZoneIds);

var session = services.CreateSession(frames);
session.Seek(40, HistoricalAtlasScrubPolicy.ExactRecorded);
session.SetLayer(HistoricalAtlasLayer.Refugia, enabled: true);
session.SetCamera(new HistoricalAtlasCameraState(
    HistoricalAtlasProjection.Globe,
    YawDegrees: 20,
    PitchDegrees: -10,
    RollDegrees: 0,
    Zoom: 1.5,
    FocusZoneId: "ek.north.ghostlands"));

HistoricalAtlasScenePayload scene = session.BuildScene();
HistoricalAtlasSceneSerializationResult serialized = services.SceneSerializer.Serialize(scene);
```

## Constraints

Phase XXIII does not add renderer dependencies, Unity types, web calls, or coordinate generation. The renderer owns authored visual anchors. The simulation owns historical evidence. The atlas owns only presentation state.
