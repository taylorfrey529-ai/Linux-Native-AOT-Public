# Phase XVIII Source Guide

## New source files

### `DeepTimeMacroevolution.cs`
Defines `DeepTimeSettings`, authority/status enums, lineage observations and states, clade/radiation/extinction/replacement records, `DeepTimeLineageLedger`, and `AdaptiveRadiationDetector`.

### `DeepTimeReplacement.cs`
Contains `EcologicalReplacementDetector` and `CladeBuilder`.

### `DeepTimeArchive.cs`
Implements append-only generation strata with deterministic SHA-256 hash chaining and verification.

### `DeepTimeRuntime.cs`
Provides `DeepTimeMacroevolutionRuntime`, the generation-level orchestrator that updates lineage state, detects macroevolutionary events, builds clades, and seals strata.

### `DeepTimePhylogeny.cs`
Provides deterministic phylogeny reconstruction and `DeepTimeGovernanceGate`.

### `DeepTimeIntegration.cs`
Converts Phase XVII `EvolutionaryTreeSnapshot` nodes plus explicit population/niche descriptors into Phase XVIII observations.

### `AzerothDeepTimeBootstrap.cs`
Composes the existing Phase XVII runtime with Phase XVIII services.

### `DeepTimeMacroevolutionTests.cs`
Eight Phase XVIII xUnit tests covering sustained extinction, rejection persistence, adaptive radiation, ecological replacement, governance, deterministic phylogeny, archive hash chaining, and deterministic open-stratum commit.

## Minimal usage

```csharp
var runtime = new DeepTimeMacroevolutionRuntime(
    new DeepTimeSettings(
        ExtinctionPersistenceGenerations: 3,
        MinimumRadiationBranches: 3,
        StratumSpanGenerations: 25));

runtime.Advance(new DeepTimeBatchObservation(
    Generation: 1,
    Lineages: new[]
    {
        new DeepTimeLineageObservation(
            "species:azeroth.elf.high",
            ParentLineageId: null,
            SpeciesFamilyId: "azeroth.elf.high",
            Generation: 1,
            Population: 1000,
            OccupiedZones: new HashSet<string> { "ek.north.eversong" },
            NicheVector: new Dictionary<string, double>
            {
                ["arcane_stability"] = 0.9,
                ["forest_dependence"] = 0.8
            },
            Authority: DeepTimeLineageAuthority.ExistingAuthorizedSpecies,
            MeanDivergence: 0.0)
    }));

var stratum = runtime.CommitOpenStratum();
if (!runtime.Archive.Verify())
    throw new InvalidOperationException("Deep-time archive integrity failed.");
```

## Phase XVII adapter

```csharp
var adapter = new DeepTimeObservationAdapter();
var batch = adapter.FromPhaseXVII(
    phaseXVII.EvolutionaryTree,
    populationDescriptors,
    generation);

deepTime.Advance(batch);
```

Population descriptors are explicit because Phase XVIII must never infer missing population counts from tree membership.

## Build note
This package targets the inherited .NET 8 project structure. No compiler is available in the current execution environment, so the implementation has undergone structural verification rather than compiler certification.
