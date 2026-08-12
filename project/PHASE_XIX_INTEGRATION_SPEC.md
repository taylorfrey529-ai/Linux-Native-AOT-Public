# Eastern Kingdoms Simulation Evolution Phase XIX
## Deep-Time World Ecology and Biogeographic History

### Status
Implementation-oriented prototype layer. All outputs remain simulation Test History unless separately reviewed and promoted through the existing continuity gates.

## Purpose
Phase XIX connects the Phase XVIII deep-time lineage archive to geography. It records where lineages persist, contract, fragment, survive in refugia, recolonize through authorized corridors, accumulate delayed local extinction risk, and alter regional biodiversity over long horizons.

The phase does not open another continent, invent a new travel route, or promote a simulated lineage into canon. Biogeographic history is downstream evidence, not authority.

## Authority chain

```text
Phase XVI community evolution
    -> Phase XVII lineage divergence
    -> Phase XVIII deep-time lineage history
    -> Phase XIX habitat and range history
       -> range shifts
       -> habitat corridor state
       -> refugia
       -> recolonization waves
       -> extinction debt
       -> biodiversity turnover
       -> hash-chained biogeographic strata
    -> Test History
    -> separate continuity review
```

## Boundary law
Every habitat corridor must resolve to an already authorized `PopulationRoute`. The Phase XIX bootstrap uses only the Northern Scar Corridor routes:

- `ek.route.eversong-ghostlands`
- `ek.route.ghostlands-western-plaguelands`
- `ek.route.western-eastern-plaguelands`

No range expansion, recolonization wave, or ecological history event may use an external continent, plane, timeline, or unregistered route.

## Habitat suitability
`HabitatSuitabilityResolver` derives a bounded proxy from the existing zone contract:

- population health
- stability
- integrity
- luminosity
- resonance debt

This is simulation state, not a moral score and not real ecological science.

## Range history
Each lineage keeps a persistent `LineageRangeState`. New observations may generate:

- Expansion
- Contraction
- Fragmentation
- Reconnection
- LocalExtirpation
- Recolonization

A zone is classified as recolonized only if that lineage occupied it earlier, later lost it, and subsequently occupies it again.

## Habitat corridors
A corridor is open only when the authorized route and both endpoint habitats produce permeability at or above the configured threshold. Corridor closure can fragment a range even when both endpoint cells remain occupied.

## Refugia
A refugium requires:

1. regional habitat stress,
2. continued lineage occupancy,
3. local suitability above the refugium threshold, and
4. persistence across the configured minimum generation count.

A pleasant zone during an otherwise pleasant era is not automatically a refugium. Words must continue meaning things, apparently.

## Recolonization waves
A recolonization wave requires:

- a previously occupied destination,
- an immediately available occupied source,
- an authorized route connecting source and destination,
- an open habitat corridor, and
- a deterministic estimated migrant count bounded by route capacity.

Persistent refugia are preferred as source populations when multiple valid sources exist.

## Extinction debt
Phase XIX models a fictional delayed local-extinction proxy. A lineage can remain locally present while habitat suitability remains below the configured collapse threshold. Debt accumulates during sustained low-suitability occupancy and may be realized when local occupancy is later lost.

Extinction debt never kills named persistent actors directly and does not replace Phase XVIII's sustained lineage-extinction rules.

## Biodiversity history
Each authorized zone receives a `RegionalBiodiversitySnapshot` containing:

- lineage richness
- species-family richness
- a bounded analytical Shannon diversity value
- approximate cohort population
- the exact lineage set used for turnover

`CommunityTurnoverAnalyzer` compares consecutive zone snapshots and records gains, losses, retained lineages, and Jaccard dissimilarity.

## Archive law
`BiogeographicArchive` is append-only and hash-chained. Committed strata deep-copy nested sets before hashing. The hash covers every range zone, refugium, recolonization route, debt record, biodiversity lineage set, and turnover gain/loss set.

Later mutation of caller-owned collections must not mutate committed history.

## Determinism
Given the same ordered authoritative observations, route catalog, zone states, and settings, Phase XIX must produce the same:

- habitat suitability states
- corridor states
- range-shift events
- refugia
- recolonization waves
- extinction-debt states
- biodiversity snapshots
- turnover metrics
- archive head hash

## Named actor boundary
Phase XIX operates on lineage/cohort observations. It does not alter a named persistent actor's genome, identity, memory, allegiance, survival state, or continuity status.

## Acceptance gates

1. Unauthorized zones are rejected.
2. Unauthorized routes are rejected at runtime construction.
3. Range expansion remains inside the Eastern Kingdoms boundary.
4. Corridor closure can create measurable fragmentation.
5. Refugia require persistence under regional stress.
6. Recolonization requires prior occupancy plus an open authorized route.
7. Extinction debt is delayed and local, not instant species extinction.
8. Biodiversity turnover records exact lineage gains and losses.
9. Hash-chained strata verify deterministically.
10. No result becomes Working Continuity automatically.
