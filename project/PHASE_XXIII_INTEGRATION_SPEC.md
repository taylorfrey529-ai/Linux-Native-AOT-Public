# Phase XXIII Integration Specification

## Interactive Historical Atlas and Globe Playback

Status: integrated prototype layer. All atlas and globe outputs remain `TestHistoryOnly` and are strictly downstream from verified Phase XXI/XXII history.

## Boundary

Phase XXIII consumes verified `HistoricalPlaybackFrame`, `HistoricalWorldStateSnapshot`, timeline, overlay, and globe-state data. It may change display state only: selected generation, camera, enabled layers, selection, and comparison mode. It cannot advance evolution, mutate archives, edit authoritative geography, create missing evidence, open a sealed continent, or promote Test History into continuity.

## Interactive state

`HistoricalAtlasSession` owns only renderer-facing mutable state:
- current recorded frame
- scrub policy
- camera state
- layer visibility and opacity
- selected zone or lineage
- optional comparison target and comparison mode

The source playback frames remain immutable inputs.

## Temporal scrubbing

Supported policies:
- `ExactRecorded`: require an exact recorded generation
- `PreviousOrExact`: resolve the latest recorded generation at or before the request
- `NextOrExact`: resolve the earliest recorded generation at or after the request
- `NearestRecorded`: choose the closest recorded generation, with ties resolved toward the earlier generation

No scrub policy interpolates an unrecorded world state.

## Layers

Renderer-neutral layer toggles cover:
- climate
- landscape
- biodiversity
- approximate population
- lineage ranges
- refugia
- extinction debt
- evidence provenance

Disabled layers are removed from the scene payload rather than merely marked hidden while leaking their values downstream.

## Comparison modes

`SideBySide` emits two independently verified historical scenes.

`Delta` additionally emits verified Phase XXII change overlays. Default delta metrics are temperature, moisture, forest cover, wetland extent, river connectivity, terrain integrity, and lineage richness. A comparison target may not precede the current generation.

## Evidence provenance panels

When the provenance layer is enabled, each zone panel exposes only recorded Phase XXI evidence references and missing-evidence markers. Evidence end generations must not exceed the reconstructed generation because Phase XXI already rejects future-evidence leakage.

## Lineage range visualization

Lineage-range payloads aggregate only living lineage evidence from reconstructed cells. They include lineage ID, species-family ID, occupied authorized zone IDs, deep-time authority, status, mean divergence, and approximate global population. Phase XXIII does not infer unrecorded range geometry between cells.

## Globe and atlas layout

Scene cells contain `ZoneId` and `RenderAnchorKey`, with the render anchor key equal to the authorized zone ID. There are no latitude, longitude, polygon, coastline, or inferred map-coordinate fields. A globe or flat atlas renderer must supply separately authored anchors and geometry.

## Camera serialization

Schema: `evermore.eastern-kingdoms.camera/23.0`.

Camera state includes projection, yaw, pitch, roll, zoom, and optional authorized focus zone. Serialization uses canonical JSON and SHA-256 payload integrity. Unsupported authority, invalid focus zones, invalid camera ranges, and payload tampering are rejected.

## Scene serialization

Schema: `evermore.eastern-kingdoms.atlas-scene/23.0`.

Only a scene that passes semantic verification may be serialized. Scene JSON is canonicalized and hashed with SHA-256. Deserialization verifies both the canonical payload hash and scene semantics.

## Integrity

Scene integrity covers camera state, layer states, visible cell data, lineage ranges, refugia, extinction debt, provenance panels, and the source Phase XXI world hash. Session integrity covers frame, generation, comparison state, camera, layers, and selections.

## Acceptance

Phase XXIII is acceptable when scrubbing never interpolates unrecorded worlds, disabled layers do not leak hidden data, camera and scene serialization are deterministic and tamper-detecting, unauthorized focus zones are rejected, comparison overlays verify, session-state tampering is detectable, source playback state is unchanged by atlas operations, and all outputs remain `TestHistoryOnly`.
