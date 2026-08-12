using EasternKingdoms.Simulation;
using Xunit;

namespace EasternKingdoms.Simulation.Tests;

public sealed class BiogeographicHistoryTests
{
    [Fact]
    public void RangeExpansionIsRecordedInsideAuthorizedBoundary()
    {
        var runtime = Runtime();
        runtime.Advance(Batch(1, Lineage("l1", 1, 100, "zone.a")));
        var second = runtime.Advance(Batch(2, Lineage("l1", 2, 110, "zone.a", "zone.b")));

        Assert.Contains(second.RangeEvents, x => x.Kind == RangeShiftKind.Expansion && x.AddedZones.Contains("zone.b"));
    }

    [Fact]
    public void UnauthorizedZoneIsRejected()
    {
        var runtime = Runtime();
        var batch = new BiogeographicBatchObservation(
            1,
            new[] { Lineage("l1", 1, 100, "zone.external") },
            new[] { Zone("zone.a", true), Zone("zone.b", true) });

        Assert.Throws<InvalidOperationException>(() => runtime.Advance(batch));
    }

    [Fact]
    public void CorridorLossCanFragmentOccupiedRange()
    {
        var runtime = Runtime(new BiogeographySettings(CorridorOpenThreshold: 0.70));
        runtime.Advance(Batch(1, Lineage("l1", 1, 100, "zone.a", "zone.b")));

        var second = new BiogeographicBatchObservation(
            2,
            new[] { Lineage("l1", 2, 100, "zone.a", "zone.b") },
            new[] { Zone("zone.a", false), Zone("zone.b", false) });
        var result = runtime.Advance(second);

        Assert.Contains(result.RangeEvents, x => x.Kind == RangeShiftKind.Fragmentation);
    }

    [Fact]
    public void RefugiumRequiresPersistentSuitablePocketDuringRegionalStress()
    {
        var settings = new BiogeographySettings(
            MinimumRefugiumPersistenceGenerations: 3,
            RefugiumSuitabilityThreshold: 0.60);
        var runtime = Runtime(settings);

        for (int generation = 1; generation <= 3; generation++)
        {
            runtime.Advance(new BiogeographicBatchObservation(
                generation,
                new[] { Lineage("l1", generation, 100, "zone.a") },
                new[] { Zone("zone.a", true), Zone("zone.b", false) }));
        }

        Assert.Contains(runtime.Snapshot().Refugia, x => x.LineageId == "l1" && x.ZoneId == "zone.a" && x.Persistent);
    }

    [Fact]
    public void ExtinctionDebtCanRealizeAfterDelayedLocalLoss()
    {
        var settings = new BiogeographySettings(
            ExtinctionDebtGraceGenerations: 2,
            HabitatCollapseSuitability: 0.35,
            ExtinctionDebtRealizationThreshold: 0.95);
        var runtime = Runtime(settings);

        runtime.Advance(new BiogeographicBatchObservation(
            1,
            new[] { Lineage("l1", 1, 100, "zone.a") },
            new[] { Zone("zone.a", false), Zone("zone.b", true) }));
        runtime.Advance(new BiogeographicBatchObservation(
            2,
            new[] { Lineage("l1", 2, 90, "zone.a") },
            new[] { Zone("zone.a", false), Zone("zone.b", true) }));
        runtime.Advance(new BiogeographicBatchObservation(
            3,
            new[] { Lineage("l1", 3, 80, "zone.b") },
            new[] { Zone("zone.a", false), Zone("zone.b", true) }));

        Assert.Contains(runtime.Snapshot().ExtinctionDebt, x => x.LineageId == "l1" && x.ZoneId == "zone.a" && x.Realized);
    }

    [Fact]
    public void RecolonizationRequiresPreviouslyOccupiedZoneAndAuthorizedRoute()
    {
        var runtime = Runtime();
        runtime.Advance(Batch(1, Lineage("l1", 1, 100, "zone.a", "zone.b")));
        runtime.Advance(Batch(2, Lineage("l1", 2, 90, "zone.a")));
        var result = runtime.Advance(Batch(3, Lineage("l1", 3, 120, "zone.a", "zone.b")));

        Assert.Contains(result.RangeEvents, x => x.Kind == RangeShiftKind.Recolonization);
        Assert.Contains(result.RecolonizationWaves, x => x.SourceZoneId == "zone.a" && x.DestinationZoneId == "zone.b");
    }

    [Fact]
    public void BiodiversityTurnoverTracksGainLossAndRetention()
    {
        var analyzer = new RegionalBiodiversityAnalyzer();
        var turnover = new CommunityTurnoverAnalyzer();
        var previous = analyzer.Analyze("zone.a", 1, new[]
        {
            Lineage("a", 1, 100, "zone.a"),
            Lineage("b", 1, 100, "zone.a")
        });
        var current = analyzer.Analyze("zone.a", 2, new[]
        {
            Lineage("b", 2, 100, "zone.a"),
            Lineage("c", 2, 100, "zone.a")
        });

        var result = turnover.Compare(previous, current);
        Assert.Equal(1, result.Gains);
        Assert.Equal(1, result.Losses);
        Assert.Equal(1, result.Retained);
        Assert.Contains("c", result.GainedLineages);
        Assert.Contains("a", result.LostLineages);
    }

    [Fact]
    public void BiogeographicArchiveIsDeterministicAndDefensivelyCopiesSets()
    {
        var mutable = new HashSet<string>(StringComparer.Ordinal) { "zone.b" };
        var archive = new BiogeographicArchive();
        archive.Commit(
            0,
            4,
            new[]
            {
                new RangeShiftEvent(
                    "event.1",
                    "l1",
                    RangeShiftKind.Expansion,
                    2,
                    mutable,
                    new HashSet<string>(StringComparer.Ordinal),
                    1,
                    1,
                    DeepTimeLineageAuthority.ExperimentalSimulation)
            },
            Array.Empty<RefugiumRecord>(),
            Array.Empty<RecolonizationWaveEvent>(),
            Array.Empty<ExtinctionDebtState>(),
            Array.Empty<RegionalBiodiversitySnapshot>(),
            Array.Empty<CommunityTurnoverMetrics>());

        string before = archive.Snapshot().HeadHash;
        mutable.Add("zone.external");

        Assert.True(archive.Verify());
        Assert.Equal(before, archive.Snapshot().HeadHash);
        Assert.DoesNotContain("zone.external", archive.Strata[0].RangeEvents[0].AddedZones);
    }

    private static DeepTimeBiogeographyRuntime Runtime(BiogeographySettings? settings = null)
    {
        var routes = new PopulationRouteCatalog();
        routes.Register(new PopulationRoute(
            "route.a-b",
            "zone.a",
            "zone.b",
            Bidirectional: true,
            PopulationCapacityPerPhase: 1000,
            Condition: 0.90,
            TravelRisk: 0.05,
            DiseaseTransmissionModifier: 0.30));
        return new DeepTimeBiogeographyRuntime(new[] { "zone.a", "zone.b" }, routes, settings);
    }

    private static BiogeographicBatchObservation Batch(int generation, params DeepTimeLineageObservation[] lineages) =>
        new(generation, lineages, new[] { Zone("zone.a", true), Zone("zone.b", true) });

    private static DeepTimeLineageObservation Lineage(
        string id,
        int generation,
        long population,
        params string[] zones) =>
        new(
            id,
            ParentLineageId: null,
            SpeciesFamilyId: "family.a",
            generation,
            population,
            zones.ToHashSet(StringComparer.Ordinal),
            new Dictionary<string, double>(StringComparer.Ordinal) { ["forest"] = 0.8 },
            DeepTimeLineageAuthority.ExperimentalSimulation,
            MeanDivergence: 0.4);

    private static ZoneEnvironment Zone(string id, bool healthy) =>
        healthy
            ? new ZoneEnvironment(
                id,
                CellSimulationState.Warm,
                PopulationHealth: 0.90,
                Stability: 0.90,
                Luminosity: 0.80,
                Integrity: 0.90,
                ResonanceDebt: 0.10,
                TraitPressures: new Dictionary<string, double>(StringComparer.Ordinal))
            : new ZoneEnvironment(
                id,
                CellSimulationState.Warm,
                PopulationHealth: 0.10,
                Stability: 0.10,
                Luminosity: 0.10,
                Integrity: 0.10,
                ResonanceDebt: 0.90,
                TraitPressures: new Dictionary<string, double>(StringComparer.Ordinal));
}
