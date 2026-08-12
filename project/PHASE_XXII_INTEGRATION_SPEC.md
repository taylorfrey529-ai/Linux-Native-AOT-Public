# Phase XXII Integration Specification

## Historical Visualization and Snapshot Serialization

Status: integrated prototype layer. All outputs are Test History visualization artifacts and are not authoritative Azeroth geography or continuity.

## Boundary

Phase XXII is strictly downstream from Phase XXI. It consumes only verified `HistoricalWorldStateSnapshot` and `HistoricalPlaybackFrame` values. It does not advance evolution, alter archives, rewrite geography, change named actors, or promote simulation discoveries into continuity.

## Pipeline

1. Reconstruct a verified Phase XXI historical world state.
2. Build deterministic timeline entries from playback frames.
3. Convert verified cells into serialization DTOs.
4. Canonicalize JSON property ordering and hash the payload with SHA-256.
5. Emit timeline, replay, overlay, and globe-state artifacts.
6. Verify hashes and semantic consistency before reuse.

## Replay schema

Schema identifier: `evermore.eastern-kingdoms.history/22.0`.

Replay files contain:
- schema version
- fixed `TestHistoryOnly` visualization authority
- SHA-256 of the canonical payload
- deterministic timeline index
- ordered historical frames
- ordered cell states
- source Phase XXI integrity hashes

The replay serializer rejects unsupported schema versions, non-Test-History authority, payload-hash mismatch, timeline-hash mismatch, unindexed frames, generation mismatches, or frame/timeline source-hash disagreement.

## Timeline index

Each entry records frame index, generation, source world integrity hash, reconstruction completeness, sorted zone IDs, and evidence count. Frame indices and generations must be unique.

## Comparative overlays

Supported overlay metrics:
- temperature
- moisture
- forest cover
- wetland extent
- river connectivity
- coastal exposure
- terrain integrity
- lineage richness
- species-family richness
- approximate population

Each overlay cell reports before value, after value, delta, and deterministic direction: Increase, Decrease, Stable, or Missing.

## Globe-state adapter

The globe adapter exports visualization state keyed by authorized zone ID. It intentionally contains no latitude, longitude, coastline geometry, or invented render coordinates. `GlobeLayoutPolicy.AuthoredAnchorsRequired` means a renderer must provide a separately authored visual layout. The adapter cannot create geography.

Only zones accepted by the configured Eastern Kingdoms authorization set may enter a globe frame.

## Authority rules

- Visualization authority is always `TestHistoryOnly`.
- Rendering never mutates simulation or archive state.
- A serialized replay is evidence-backed playback data, not canon.
- Source snapshot hashes are retained in every frame and globe cell.
- Missing evidence remains missing; visualization does not interpolate it.
- No other continent is introduced by Phase XXII.

## Acceptance

Phase XXII is acceptable when the same verified frames produce byte-identical canonical JSON and identical SHA-256 payload hashes, tampering is rejected, timeline indices verify, overlays verify, globe frames reject unauthorized zones, source runtime state is unchanged by serialization, and all visualization artifacts remain downstream Test History.
