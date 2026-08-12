# Azeroth Environmental Field Contract

## Authority

This Revision 14 layer emits `TestHistoryOnly` normalized signals for explicitly authorized Azeroth zone identifiers. It is a deterministic simulation interface, not a claim of canonical weather, climate, atmospheric chemistry, physical mana, geography, or globe geometry.

The parent `AzerothSimulationSnapshot` remains immutable and read-only. Omega, Lunara, and Solune keep their existing isolated authorities. A contained parent simulation produces a contained field with zero drivers and no evaluated zones.

## Authored inputs

Each field input must:

- use the same recorded generation as the composed simulation;
- provide a positive bounded Solune reference irradiance;
- contain every authorized zone exactly once in ordinal zone-ID order;
- provide only normalized integer values in the closed interval `0..1,000,000`.

Missing, duplicate, reordered, unknown, or extra zones fail closed. The engine never derives coordinates, adjacency, terrain, route geometry, climate, or local scenes from a zone identifier.

## Read-only drivers

The engine observes:

- Solune irradiance, normalized against the authored reference and clamped at unity;
- Lunara tide level;
- Lunara resonance;
- Omega final average fitness, bounded to unity and converted to micro-units.

These are simulation drivers only. They do not transfer component authority or mutate a component.

## Signal equations

For unit `U = 1,000,000`:

`contribution(driver, response) = floor(driver * response / U)`

`atmosphere = min(U, authored atmosphere baseline + Solune contribution + Lunara tide contribution)`

`mana = min(U, authored mana baseline + Lunara resonance contribution + Omega contribution)`

The output is named `authored-zone-signal-compositor/1.0`. “Atmosphere” and “mana” are normalized field-channel names. No physical unit, thermodynamic behavior, meteorology, magical ontology, or visual styling is inferred.

## Serialization and verification

The deployed schema is `evermore.azeroth.environmental-field/1.0`. JSON uses source-generated metadata, a deterministic payload SHA-256, fixed-time integrity comparison, and semantic reprocessing from the embedded authored input.

CLI entry:

`evermore azeroth field --input FILE --format json`
