# Phase XVII Changelog

## Added

- persistent ecotype tracking
- configurable divergence weighting and thresholds
- reproductive-isolation evaluation independent of general divergence
- hybrid-zone dispositions
- persistent speciation-candidate registry
- explicit Hold / Reject / ApproveSimulation / AuthorizeCanonAuthoring review decisions
- candidate-state invariants preventing authorized/rejected candidates from silently reverting
- deterministic evolutionary-tree snapshots with SHA-256 integrity hash
- `PhaseXVIISaveState`
- `AzerothLineageEvolutionBootstrap`
- eight Phase XVII lineage/governance tests
- formal Phase XVII integration specification and source guide

## Preserved

- Phase XVI whole-community orchestration
- named-actor continuity boundary
- species/ecology activation gates
- deterministic cohort history
- Test History vs Working Continuity separation

## Build limitation

No .NET SDK or C# compiler is available in the current execution environment. Compilation and xUnit execution remain pending on a .NET 8 host.
