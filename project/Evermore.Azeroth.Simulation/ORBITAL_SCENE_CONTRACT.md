# Azeroth Orbital Scene Contract

## Purpose and authority

Revision 15 exposes a renderer-neutral orbital scene manifest over the verified Revision 14 environmental field. Its authority is `TestHistoryOnly`, and its acceptance class is `ConstrainedSimulationConceptOnly`.

The generated orbital concept image is a style reference only. It does not author continent outlines, terrain, settlements, clouds, weather, coordinates, moon placement, geography, or simulation state.

## Normalized scale

The scene preserves the established Earth:Azeroth comparison:

- Earth reference extent: `1,000,000` micro-units;
- Azeroth reference extent: `1,000,000` micro-units;
- normalized linear ratio: `1.000000 : 1.000000`.

This is unitless display calibration. Physical radius, circumference, kilometers, miles, gravity, latitude, and longitude remain `unavailable-no-authority`.

At zoom level `z`, the renderer-facing viewport reference width is `floor(1,000,000 / 2^z)` micro-units. Supported zoom levels are `0..8`.

## Orbital camera

The camera accepts normalized renderer references only:

- azimuth: `0..999,999` micro-units;
- elevation: `-250,000..250,000` micro-units;
- zoom: `0..8`.

These values are not angles, latitude, longitude, altitude, or physical distance. They do not mutate or create geography.

## Environmental bindings

The scene carries read-only observations from the verified environmental snapshot:

- Solune orbit phase;
- Lunara illumination and phase;
- mean authored-zone atmosphere signal;
- mean authored-zone mana signal;
- per-zone atmosphere and mana signals.

Every current zone is emitted with `missing-authored-globe-vector`. A renderer may display the aggregate shell signals in a constrained concept, but it must not place a zone on the globe until a separately authored globe-vector contract exists.

If the environmental simulation is contained, the orbital scene is contained, aggregate signals are zero, and no zones are emitted.

## Verification

The deployed schema is `evermore.azeroth.orbital-scene/1.0`. JSON uses source-generated metadata, deterministic payload SHA-256, fixed-time integrity comparison, and semantic reprocessing from the embedded environmental and camera input.

CLI entry:

`evermore azeroth orbit --input FILE --azimuth-micros N --elevation-micros N --zoom N --format json`
