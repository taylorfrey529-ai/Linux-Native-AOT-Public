# Phase XIX C# Source Guide

## New runtime files

### `BiogeographyModels.cs`
Immutable Phase XIX contracts and configurable settings:
- habitat-zone and corridor states
- range states and events
- refugia
- recolonization waves
- extinction debt
- biodiversity snapshots
- turnover metrics
- generation batch/results

### `RangeDynamics.cs`
- `HabitatSuitabilityResolver`
- `HabitatCorridorResolver`
- `BiogeographicRangeLedger`
- `RangeConnectivityAnalyzer`

The range ledger preserves species-family identity and rejected authority across later observations.

### `RefugiaAndRecolonization.cs`
- `RefugiumTracker`
- `RecolonizationWaveDetector`

Recolonization is route-gated and cannot invent external transport.

### `ExtinctionDebt.cs`
`ExtinctionDebtLedger` models delayed local loss after sustained unsuitable occupancy. It does not alter individual actors or replace deep-time lineage extinction.

### `RegionalBiodiversity.cs`
- `RegionalBiodiversityAnalyzer`
- `CommunityTurnoverAnalyzer`

Approximate zone population divides each lineage population across its occupied zones because Phase XVIII does not yet carry authoritative per-zone abundance. This approximation is explicit and should be replaced when per-zone lineage abundance becomes authoritative.

### `BiogeographicArchive.cs`
Append-only SHA-256 hash chain with defensive copies of all nested sets.

### `BiogeographicRuntime.cs`
`DeepTimeBiogeographyRuntime` orchestrates one complete biogeographic generation in deterministic order:

```text
validate boundary
-> derive habitat state
-> derive corridor state
-> evaluate ranges
-> update refugia
-> detect recolonization
-> update extinction debt
-> measure biodiversity
-> measure turnover
-> commit stratum when due
```

`DeepTimeBiogeographyAdapter` converts committed Phase XVIII lineage state into Phase XIX batch observations.

### `AzerothBiogeographyBootstrap.cs`
Composes the Phase XVIII runtime with Phase XIX using the existing Northern Scar zones and routes.

## Tests
`BiogeographicHistoryTests.cs` covers:

1. range expansion
2. unauthorized-zone rejection
3. habitat-corridor fragmentation
4. refugium persistence
5. extinction-debt realization
6. route-gated recolonization
7. biodiversity turnover
8. defensive archive hashing

## Build status
The package targets .NET 8. This execution environment does not expose `dotnet`, `csc`, `mcs`, or `mono`, so compilation and xUnit execution cannot be truthfully certified here. Static structural verification is included separately.
