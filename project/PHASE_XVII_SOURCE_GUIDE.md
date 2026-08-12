# Phase XVII Source Guide

## New runtime modules

### `EasternKingdoms.Simulation/LineageDivergence.cs`

Defines:

- `LineageDivergenceSettings`
- `EcotypeProfile`
- `EcotypeTracker`
- `DivergenceMetrics`
- `DivergenceAnalyzer`
- `ReproductiveIsolationState`
- `ReproductiveIsolationEvaluator`
- `HybridZoneProfile`
- `HybridZoneEvaluator`
- `SpeciationCandidate`
- `SpeciationCandidateRegistry`

### `EasternKingdoms.Simulation/SpeciationGovernance.cs`

Defines the review boundary:

- `SpeciationReviewDecision`
- `SpeciationActivationDisposition`
- `SpeciationReviewRecord`
- `SpeciationReviewGate`
- `SpeciationGovernanceService`

Simulation authorization and canon-authoring authorization are intentionally distinct.

### `EasternKingdoms.Simulation/EvolutionaryTree.cs`

Defines deterministic lineage-tree state and snapshot hashing.

### `EasternKingdoms.Simulation/LineageDivergenceRuntime.cs`

Composes the Phase XVII services into `LineageDivergenceOrchestrator` and exposes `PhaseXVIISaveState`.

### `EasternKingdoms.Simulation/AzerothLineageBootstrap.cs`

Adds:

`AzerothLineageEvolutionBootstrap.Create(GenomeCatalog)`

which composes the existing Phase XVI community runtime with the new lineage-divergence orchestrator.

## Minimal usage

```csharp
var lineageRuntime = AzerothLineageEvolutionBootstrap.Create(genomeCatalog);

var result = lineageRuntime.Lineages.Observe(
    new LineageDivergenceObservation(
        populationA,
        populationB,
        ProposedLineageId: "candidate.lineage.001",
        HybridZoneId: "ek.north.hybrid-border",
        LocalAdaptationA: 0.72,
        LocalAdaptationB: 0.81,
        AdaptiveTraitsA: adaptiveA,
        AdaptiveTraitsB: adaptiveB,
        TraitDivergence: 0.70,
        EcologicalSeparation: 0.75,
        GeographicIsolation: 0.65,
        PrezygoticIsolation: 0.60,
        PostzygoticIsolation: 0.62,
        BehavioralIsolation: 0.58,
        TemporalIsolation: 0.30,
        HybridFrequency: 0.10,
        HybridViability: 0.35,
        HybridFertility: 0.30,
        IntrogressionRate: 0.05,
        Generation: generation));
```

A returned `SpeciationCandidate` is analysis output only. It remains held until explicit review.

## Review example

```csharp
var reviewed = lineageRuntime.Lineages.Review(
    new SpeciationReviewRecord(
        ReviewId: "review-001",
        CandidateId: candidate.CandidateId,
        Reviewer: "continuity-review",
        Decision: SpeciationReviewDecision.ApproveSimulation,
        Notes: "Experimental branch only.",
        ReviewGeneration: generation));
```

`ApproveSimulation` does not grant canon-authoring authority.

## Build note

The package targets .NET 8. This environment has no `dotnet`, `csc`, `mcs`, or Mono executable, so source is structurally verified but not compiler-certified here.
