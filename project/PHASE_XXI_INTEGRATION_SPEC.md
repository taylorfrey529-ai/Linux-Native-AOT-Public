# Phase XXI Integration Specification

## Title
Paleogeographic Reconstruction and Deep-Time World-State Playback

## Authority rule
Phase XXI reconstructs Test History from sealed Phase XVIII-XIX-XX archives. A reconstruction is evidence, not automatic Working Continuity and not an authoritative map edit.

## Inputs
- `GeoclimateArchive`: climate, landscape, geomorphic-change, envelope, and recovery strata.
- `BiogeographicArchive`: range, refugium, recolonization, extinction-debt, biodiversity, and turnover strata.
- `DeepTimeArchive`: lineage, radiation, extinction, replacement, and clade strata.
- Explicit authorized Eastern Kingdoms zone IDs.
- A requested generation and explicit requested zone set.

## Evidence rule
Only sealed archive records at or before the requested generation may contribute state. A later record is never backfilled into an earlier reconstruction. A sealed stratum may be cited when it contains the selected earlier record, but only records whose own generation is not later than the request are read.

## Cell reconstruction
For each requested cell, Phase XXI resolves the latest climate, landscape, biodiversity, and deep-time lineage evidence whose record generation is at or before the requested generation. Refugia, extinction debt, and recent geomorphic events are attached when their recorded generation is also at or before the request.

Lineage population is explicitly labeled `ApproximateGlobalPopulation`. Phase XXI does not fabricate per-zone lineage population allocation from a global lineage count.

## Completeness
Four primary evidence dimensions determine cell completeness: climate, landscape, biodiversity, and deep-time lineage evidence.

- `None`: no primary dimension available.
- `Partial`: one or two primary dimensions available.
- `Substantial`: three primary dimensions available.
- `Complete`: all four primary dimensions available.

A world snapshot uses the minimum completeness of its requested cells. Strict requests reject anything below `Complete`.

## Integrity
Every reconstructed cell is SHA-256 hashed from its complete normalized content and contributing stratum references. World hashes include cell hashes plus world evidence. Runtime playback snapshots hash every reconstructed world. `HistoricalPlaybackVerifier` recalculates these hashes and rejects altered content.

## Playback
`DeepTimeWorldStatePlayback` can reconstruct one generation, play an inclusive generation interval at a fixed positive step, compare two verified snapshots, and emit deterministic environmental plus lineage deltas.

Playback is read-only with respect to evolution. Viewing generation 100 does not advance generation 101, modify an archive, or promote Test History.

## Geographic boundary
Every requested zone must already belong to the authorized Eastern Kingdoms playback boundary. Phase XXI does not open sealed continents, invent external routes, or infer geography outside the supplied zone set.

## Non-authoritative geography
Forest cover, wetlands, river connectivity, coastal exposure, terrain integrity, temperature, and moisture remain normalized historical simulation fields. Reconstructing them does not rewrite the actual map, coastline, river network, or canon.

## Acceptance criteria
1. Same archives plus same request produce the same snapshot hash.
2. Unauthorized zones are rejected.
3. Future records never fill earlier missing evidence.
4. Strict reconstruction rejects incomplete evidence.
5. Zero biodiversity is treated as valid evidence when explicitly recorded.
6. Tampered snapshots fail integrity verification.
7. Playback deltas report lineage gains/losses and environmental change deterministically.
8. Runtime playback snapshots verify deterministically.
9. All outputs remain `TestHistoryOnly`.

## Next boundary
Phase XXII may add historical visualization products, timeline indexing, snapshot serialization, and map/globe presentation adapters. Those adapters must consume Phase XXI snapshots rather than reaching backward into mutable simulation state.
