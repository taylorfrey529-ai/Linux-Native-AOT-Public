# Historical Atlas Scene Format 23.0

## Scene schema

Serialized atlas scenes use `evermore.eastern-kingdoms.atlas-scene/23.0`.

Top-level fields:
- `schemaVersion`
- `authority` (`TestHistoryOnly`)
- `payloadSha256`
- `scene`

The scene contains:
- frame index and generation
- deterministic camera state
- ordered layer-state list
- ordered renderer-neutral cell payloads
- optional lineage-range payloads
- optional evidence-provenance panels
- source Phase XXI world-integrity hash
- scene-integrity hash

## Renderer contract

A renderer receives stable keys, not geography:

```text
ZoneId -> RenderAnchorKey
```

`RenderAnchorKey` currently equals `ZoneId`. The rendering application must resolve that key against an authored layout. Scene files intentionally omit latitude, longitude, polygons, route geometry, coastline paths, and inferred world coordinates.

## Layer masking

When a layer is disabled, its corresponding values are absent/null/empty in the scene payload. This prevents a downstream renderer from accidentally displaying data the session marked hidden.

## Determinism

Canonical JSON property order is normalized before SHA-256 hashing. Two semantically identical verified scenes created from the same frame, camera, and layer state produce the same canonical scene file.
