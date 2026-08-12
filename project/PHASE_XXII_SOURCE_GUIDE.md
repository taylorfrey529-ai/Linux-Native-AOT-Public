# Phase XXII C# Source Guide

## New runtime files

### `HistoricalVisualizationModels.cs`
Immutable visualization, serialization, timeline, overlay, and globe DTOs plus `HistoricalVisualizationSettings`.

### `HistoricalTimelineIndex.cs`
Builds and verifies deterministic timeline indices from verified Phase XXI playback frames.

### `HistoricalReplaySerialization.cs`
Maps verified playback frames to a stable replay DTO, canonicalizes JSON recursively, computes SHA-256 payload integrity, and verifies replay files on read.

### `HistoricalVisualizationHashing.cs`
Canonical integrity functions for overlays and globe states.

### `HistoricalOverlayVisualization.cs`
Builds comparative map overlays for environmental and biodiversity metrics without mutating world state.

### `HistoricalGlobeAdapter.cs`
Converts verified Phase XXI worlds to zone-keyed globe render state. No coordinates are generated. Authored render anchors are required downstream.

### `AzerothHistoricalVisualizationBootstrap.cs`
Composes the Phase XXI historical playback runtime with Phase XXII timeline, serializer, overlay, and globe services.

### `HistoricalVisualizationUsage.cs`
Minimal usage helper for creating a serialized replay from a generation range.

## New tests

`HistoricalVisualizationSerializationTests.cs` covers deterministic serialization, tamper rejection, round-trip verification, timeline integrity, overlays, globe layout/authorization, and non-mutating behavior.

## Minimal usage

```csharp
var runtime = AzerothHistoricalVisualizationBootstrap.Create(genomeCatalog);
var replay = HistoricalVisualizationUsage.BuildReplay(runtime, 0, 30, 5);
File.WriteAllText("northern-scar.ekhistory.json", replay.Json);
```

The example writes a Test-History replay only. It does not promote reconstructed states into continuity.
