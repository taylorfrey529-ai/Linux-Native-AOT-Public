# Phase XX C# Source Guide

## New files

`GeoclimateModels.cs`
: Immutable definitions and result records for climate, disturbance, landscape, habitat envelopes, climate pressure, recovery, and generation output.

`ClimateRegimes.cs`
: `GeoclimateScenarioCatalog` plus deterministic `PaleoclimateResolver`.

`LandscapeDynamics.cs`
: Persistent normalized landscape dynamics and directional `LandscapeChangeEvent` tracking.

`HabitatEnvelopeDynamics.cs`
: Climate/landscape suitability and route-gated envelope-shift opportunities.

`ClimateRecovery.cs`
: Multi-generation ecological recovery state machine.

`ClimateEvolutionPressure.cs`
: Climate-to-selection pressure conversion and `ZoneEnvironment` integration adapter.

`GeoclimateArchive.cs`
: Defensive-copy, append-only SHA-256 history strata.

`GeoclimateRuntime.cs`
: Phase XX orchestration and archive scheduling.

`AzerothGeoclimateBootstrap.cs`
: Phase XIX + Phase XX composition with neutral, explicitly non-canonical prototype baselines.

`EasternKingdoms.Simulation.Tests/GeoclimateDeepTimeTests.cs`
: Nine boundary tests for determinism, authorization, Locked-state preservation, corridor gating, geomorphic events, recovery, pressure integration, archive immutability, and stratum overlap.

## Minimal usage

```csharp
using EasternKingdoms.Simulation;
using Evermore.Genetics;

var genomeCatalog = new GenomeCatalog();
var scenario = new GeoclimateScenarioCatalog();

scenario.RegisterClimateEpoch(new ClimateEpochDefinition(
    "test-history.cool-wet",
    StartGeneration: 100,
    EndGeneration: 124,
    TargetZoneIds: new HashSet<string>(StringComparer.Ordinal),
    TemperatureDelta: -0.15,
    MoistureDelta: 0.20,
    StorminessDelta: 0.10,
    SeasonalityDelta: 0.05,
    ResonanceDelta: 0.0));

var runtime = AzerothGeoclimateEvolutionBootstrap.Create(
    genomeCatalog,
    scenario: scenario);

var zones = AzerothEvolutionBootstrap.CreateNorthernScarCorridor();
var result = runtime.Geoclimate.Advance(
    new GeoclimateBatchObservation(100, zones));

// Feed these pressure-integrated zones to downstream community/cohort simulation.
IReadOnlyList<ZoneEnvironment> nextZones = result.PressureIntegratedZones;
```

The example uses an empty target set, meaning all already-authorized Phase 0 zones. It does not authorize a new zone.

## Configuration

Use `GeoclimateSettings` for archive span, envelope suitability threshold, recovery persistence, recovery climate/landscape thresholds, and directional landscape-event threshold.

All numeric balance and timescale values are prototype configuration, not Warcraft canon.

## Compilation status

The current container has no `dotnet`, `csc`, `mcs`, or Mono executable. Static structural verification was run, but compilation and xUnit execution were not available and are not claimed.
