# Phase XXI Changelog

## Added
- Evidence-addressed historical world reconstruction.
- Per-cell climate, landscape, biodiversity, lineage, refugium, extinction-debt, and geomorphic history snapshots.
- Reconstruction completeness and explicit missing-evidence reporting.
- SHA-256 cell/world/delta/runtime integrity hashes.
- Read-only generation playback.
- Verified snapshot comparison with environmental and lineage deltas.
- Eastern Kingdoms playback bootstrap.
- Nine Phase XXI tests.

## Preserved boundaries
- Eastern Kingdoms-only authorization.
- Test History is not Working Continuity.
- Reconstructed geography does not mutate the authoritative map.
- Named actors are not synthesized from cohort evidence.
- No future-state backfill.
- No hidden interpolation.

## Known environment limitation
No C# compiler or .NET SDK is available in the build container, so compilation and xUnit execution are not certified.
