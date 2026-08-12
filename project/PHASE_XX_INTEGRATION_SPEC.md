# Phase XX Integration Specification

## Geological and Climatic Deep Time

Phase XX adds a review-safe geological and climatic deep-time layer downstream of the Phase XIX biogeographic runtime.

## Authority boundary

All climate epochs, disturbance epochs, landscape states, habitat-envelope shifts, and recovery states are Test History unless separately promoted through the existing continuity process. The runtime does not rewrite authoritative Eastern Kingdoms geography.

River connectivity, coastal exposure, forest cover, wetland extent, terrain integrity, temperature, moisture, storminess, and seasonality are normalized simulation fields in the range [0, 1]. They are not map coordinates, literal shoreline edits, or canon claims.

Locked cells preserve their previous physical landscape state and accept no landscape mutation for that generation.

## Causal order

```text
Authored Test-History climate epochs
        |
        v
Paleoclimate state per authorized zone
        |
        +--> active disturbance epochs
        |
        v
Landscape dynamics
        |
        +--> directional geomorphic events
        |
        v
Habitat-envelope suitability
        |
        +--> authorized-route expansion opportunities
        |
        v
Climate evolutionary pressure
        |
        +--> pressure-integrated ZoneEnvironment
        |
        v
Ecological recovery tracking
        |
        v
Hash-chained geoclimate archive strata
```

## Climate epochs

`ClimateEpochDefinition` is immutable authored Test-History input. Multiple active epochs combine deterministically in ordinal ID order. Each epoch can alter normalized temperature, moisture, storminess, seasonality, and resonance background for explicitly listed authorized zones. An empty target set means all authorized zones.

## Landscape dynamics

`LandscapeDynamicsLedger` evolves normalized landscape indices with inertia rather than instant map replacement. The state includes forest cover, wetland extent, river connectivity, coastal exposure, terrain integrity, and disturbance load.

A baseline with `CoastalExposure == 0` remains non-coastal in this model. Phase XX therefore cannot spontaneously invent a coast for an inland baseline.

`LandscapeChangeTracker` emits direction-bearing events after a configurable threshold is crossed:

- forest advance / retreat
- wetland expansion / contraction
- river-connectivity gain / loss
- coastal-exposure increase / decrease
- terrain degradation / recovery

## Habitat-envelope migration

`HabitatEnvelopeDefinition` is authored per species family or lineage. It describes climate and landscape suitability, not biological worth. The tracker computes suitability in authorized zones and may emit `ExpansionOpportunity` only when a newly suitable zone is connected to a previously suitable zone by an open authorized Phase XIX habitat corridor.

Envelope events are `PotentialOnly = true`. They do not move a cohort by themselves. Actual population movement remains under the Phase XIV-XIX migration and biogeography systems.

## Evolution pressure integration

`ClimateEvolutionPressureResolver` emits generic ecological trait pressures:

- `climate_temperature_tolerance`
- `climate_moisture_tolerance`
- `disturbance_resilience`
- `habitat_flexibility`
- `terrain_stability`

These pressures affect selection only when an authored allele has a matching effect key. They never modify the sequence of a living individual.

`ClimatePressureIntegrator` merges the pressures into `ZoneEnvironment` for downstream community/cohort simulation.

## Disturbance and recovery

Disturbance epochs are explicitly authored Test-History inputs. Supported classes are drought, flood pulse, cold cycle, warm cycle, fire, erosion pulse, disease aftermath, arcane instability, and compound catastrophe.

Recovery is multi-metric. A zone cannot become `Recovered` from one favorable number. It must sustain climate stability and landscape integrity below the disturbance threshold for the configured number of generations.

## Archive

`GeoclimateArchive` is append-only and hash-chained. Sealed strata defensively copy nested lists before hashing. Hash input covers climate state, landscape state, directional landscape events, envelope migration opportunities, and recovery records.

Overlapping or backward strata are rejected.

## Northern Scar bootstrap

`AzerothGeoclimateEvolutionBootstrap` composes the Phase XIX runtime and Phase XX geoclimate runtime.

The bootstrap uses deliberately neutral prototype baselines because no new geographic or climatic canon was supplied in the current design context. These baselines are Test-History scaffolding only and must be replaced by reviewed authored baselines before any continuity promotion.

No new continent, plane, world, or external route is opened.
