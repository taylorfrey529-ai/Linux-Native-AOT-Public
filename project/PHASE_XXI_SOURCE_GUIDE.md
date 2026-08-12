# Phase XXI C# Source Guide

## New runtime files
- `EasternKingdoms.Simulation/HistoricalReconstructionModels.cs` - immutable request, evidence, cell, world, frame, delta, and runtime snapshot contracts.
- `EasternKingdoms.Simulation/HistoricalPlaybackHashing.cs` - deterministic SHA-256 canonical hashing for cells, worlds, deltas, and playback caches.
- `EasternKingdoms.Simulation/HistoricalWorldStateReconstructor.cs` - evidence-bounded paleogeographic reconstruction over sealed archives.
- `EasternKingdoms.Simulation/DeepTimeWorldStatePlayback.cs` - playback sequencing, snapshot comparison, and integrity verification.
- `EasternKingdoms.Simulation/AzerothHistoricalPlaybackBootstrap.cs` - Eastern Kingdoms composition root over the Phase XX stack.
- `EasternKingdoms.Simulation.Tests/HistoricalWorldStatePlaybackTests.cs` - Phase XXI boundary tests.

## Minimal construction
```csharp
var runtime = AzerothHistoricalPlaybackBootstrap.Create(genomeCatalog);
var zones = AzerothHistoricalPlaybackBootstrap.AuthorizedPlaybackZones();

// Advance and seal the underlying evolution archives first.
var snapshot = runtime.Playback.Reconstruct(
    new HistoricalReconstructionRequest(
        Generation: 100,
        ZoneIds: zones,
        RequireCompleteEvidence: false));
```

## Playback
```csharp
var frames = runtime.Playback.Play(
    startGeneration: 50,
    endGeneration: 100,
    step: 10,
    zoneIds: zones);

var delta = runtime.Playback.Compare(frames[0].World, frames[^1].World);
```

## Important behavior
- Playback reads sealed archives only.
- No interpolation is performed.
- Latest admissible evidence means record generation `<= requested generation`.
- Empty-but-recorded biodiversity is evidence.
- Missing evidence remains missing.
- `HistoricalReconstructionAuthority.TestHistoryOnly` is fixed for all Phase XXI snapshots.
- Snapshot comparison requires successful integrity verification.

## Compiler status
This package was statically inspected in an environment without `dotnet`, `csc`, `mcs`, or Mono. No compilation claim is made.
