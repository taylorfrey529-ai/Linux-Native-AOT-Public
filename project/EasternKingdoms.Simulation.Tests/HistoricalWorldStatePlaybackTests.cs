using EasternKingdoms.Simulation;
using Xunit;

namespace EasternKingdoms.Simulation.Tests;

public sealed class HistoricalWorldStatePlaybackTests
{
    [Fact]
    public void Complete_sealed_evidence_reconstructs_cell_state()
    {
        var fixture = Fixture.SingleGeneration(5, includeLineage: true);
        var world = fixture.Reconstructor.Reconstruct(Request(5));

        Assert.Equal(ReconstructionCompleteness.Complete, world.Completeness);
        Assert.Equal(HistoricalReconstructionAuthority.TestHistoryOnly, world.Authority);
        var cell = Assert.Single(world.Cells);
        Assert.NotNull(cell.Climate);
        Assert.NotNull(cell.Landscape);
        Assert.NotNull(cell.Biodiversity);
        Assert.Single(cell.Lineages);
        Assert.True(HistoricalPlaybackVerifier.Verify(world));
    }

    [Fact]
    public void Unauthorized_zone_is_rejected()
    {
        var fixture = Fixture.SingleGeneration(5, includeLineage: true);
        var request = new HistoricalReconstructionRequest(5, new HashSet<string>(StringComparer.Ordinal) { "zone.forbidden" });
        Assert.Throws<InvalidOperationException>(() => fixture.Reconstructor.Reconstruct(request));
    }

    [Fact]
    public void Future_evidence_is_not_used_to_fill_earlier_generation()
    {
        var fixture = Fixture.SingleGeneration(10, includeLineage: true);
        var world = fixture.Reconstructor.Reconstruct(Request(5));
        var cell = Assert.Single(world.Cells);

        Assert.Equal(ReconstructionCompleteness.None, world.Completeness);
        Assert.Null(cell.Climate);
        Assert.Null(cell.Landscape);
        Assert.Null(cell.Biodiversity);
        Assert.Empty(cell.Lineages);
    }

    [Fact]
    public void Strict_reconstruction_fails_when_evidence_is_incomplete()
    {
        var fixture = Fixture.SingleGeneration(5, includeLineage: false, commitDeepStratum: false);
        var request = new HistoricalReconstructionRequest(5, ZoneSet(), RequireCompleteEvidence: true);
        Assert.Throws<InvalidOperationException>(() => fixture.Reconstructor.Reconstruct(request));
    }

    [Fact]
    public void Zero_biodiversity_is_valid_evidence_not_missing_evidence()
    {
        var fixture = Fixture.SingleGeneration(5, includeLineage: false, commitDeepStratum: true, zeroBiodiversity: true);
        var world = fixture.Reconstructor.Reconstruct(Request(5));
        var cell = Assert.Single(world.Cells);

        Assert.Equal(ReconstructionCompleteness.Complete, cell.Completeness);
        Assert.Equal(0, cell.Biodiversity!.LineageRichness);
        Assert.Empty(cell.Lineages);
    }

    [Fact]
    public void Same_evidence_and_request_produce_same_snapshot_hash()
    {
        var fixture = Fixture.SingleGeneration(5, includeLineage: true);
        var left = fixture.Reconstructor.Reconstruct(Request(5));
        var right = fixture.Reconstructor.Reconstruct(Request(5));
        Assert.Equal(left.IntegrityHash, right.IntegrityHash);
    }

    [Fact]
    public void Tampered_world_snapshot_fails_integrity_verification()
    {
        var fixture = Fixture.SingleGeneration(5, includeLineage: true);
        var world = fixture.Reconstructor.Reconstruct(Request(5));
        var tampered = world with { IntegrityHash = "tampered" };
        Assert.False(HistoricalPlaybackVerifier.Verify(tampered));
    }

    [Fact]
    public void Playback_delta_reports_lineage_gain_and_environment_change()
    {
        var fixture = Fixture.TwoGenerations();
        var playback = new DeepTimeWorldStatePlayback(fixture.Reconstructor);
        var from = playback.Reconstruct(Request(5));
        var to = playback.Reconstruct(Request(10));
        var delta = playback.Compare(from, to);
        var cell = Assert.Single(delta.Cells);

        Assert.Contains("lineage.b", cell.GainedLineages);
        Assert.Empty(cell.LostLineages);
        Assert.NotNull(cell.TemperatureDelta);
        Assert.True(cell.TemperatureDelta!.Value > 0.0);
    }

    [Fact]
    public void Playback_frames_are_deterministic_and_runtime_snapshot_verifies()
    {
        var fixture = Fixture.TwoGenerations();
        var playback = new DeepTimeWorldStatePlayback(fixture.Reconstructor);
        var frames = playback.Play(5, 10, 5, ZoneSet());

        Assert.Equal(2, frames.Count);
        Assert.Equal(5, frames[0].World.Generation);
        Assert.Equal(10, frames[1].World.Generation);
        Assert.True(HistoricalPlaybackVerifier.Verify(playback.Snapshot()));
    }

    private static HistoricalReconstructionRequest Request(int generation) => new(generation, ZoneSet());
    private static IReadOnlySet<string> ZoneSet() => new HashSet<string>(StringComparer.Ordinal) { Fixture.ZoneId };

    private sealed record Fixture(
        GeoclimateArchive Geoclimate,
        BiogeographicArchive Biogeography,
        DeepTimeArchive DeepTime,
        HistoricalWorldStateReconstructor Reconstructor)
    {
        public const string ZoneId = "zone.a";

        public static Fixture SingleGeneration(
            int generation,
            bool includeLineage,
            bool commitDeepStratum = true,
            bool zeroBiodiversity = false)
        {
            var geo = new GeoclimateArchive();
            var bio = new BiogeographicArchive();
            var deep = new DeepTimeArchive();
            CommitGeo(geo, generation, temperature: 0.50);
            CommitBio(bio, generation, zeroBiodiversity ? Array.Empty<string>() : includeLineage ? new[] { "lineage.a" } : Array.Empty<string>());
            if (commitDeepStratum)
                CommitDeep(deep, generation, includeLineage ? new[] { "lineage.a" } : Array.Empty<string>());

            return new Fixture(
                geo,
                bio,
                deep,
                new HistoricalWorldStateReconstructor(new[] { ZoneId }, geo, bio, deep));
        }

        public static Fixture TwoGenerations()
        {
            var geo = new GeoclimateArchive();
            var bio = new BiogeographicArchive();
            var deep = new DeepTimeArchive();
            CommitGeo(geo, 5, 0.45);
            CommitBio(bio, 5, new[] { "lineage.a" });
            CommitDeep(deep, 5, new[] { "lineage.a" });
            CommitGeo(geo, 10, 0.65);
            CommitBio(bio, 10, new[] { "lineage.a", "lineage.b" });
            CommitDeep(deep, 10, new[] { "lineage.a", "lineage.b" });

            return new Fixture(
                geo,
                bio,
                deep,
                new HistoricalWorldStateReconstructor(new[] { ZoneId }, geo, bio, deep));
        }

        private static void CommitGeo(GeoclimateArchive archive, int generation, double temperature)
        {
            archive.Commit(
                generation,
                generation,
                new[] { new ClimateZoneState(ZoneId, generation, ClimateRegimeKind.Baseline, temperature, 0.50, 0.20, 0.40, 0.10, 0.80, Array.Empty<string>()) },
                new[] { new LandscapeZoneState(ZoneId, generation, 0.50, 0.20, 0.30, 0.0, 0.75, 0.10, Array.Empty<string>()) },
                new[] { new LandscapeChangeEvent($"landscape:{generation}", ZoneId, LandscapeChangeKind.ForestAdvance, generation, 0.45, 0.50, 0.05) },
                Array.Empty<HabitatEnvelopeMigrationEvent>(),
                Array.Empty<EcologicalRecoveryRecord>());
        }

        private static void CommitBio(BiogeographicArchive archive, int generation, IReadOnlyList<string> lineageIds)
        {
            archive.Commit(
                generation,
                generation,
                Array.Empty<RangeShiftEvent>(),
                Array.Empty<RefugiumRecord>(),
                Array.Empty<RecolonizationWaveEvent>(),
                Array.Empty<ExtinctionDebtState>(),
                new[]
                {
                    new RegionalBiodiversitySnapshot(
                        ZoneId,
                        generation,
                        lineageIds.Count,
                        lineageIds.Count == 0 ? 0 : 1,
                        lineageIds.Count <= 1 ? 0.0 : 0.69,
                        lineageIds.Count * 100,
                        lineageIds.ToHashSet(StringComparer.Ordinal))
                },
                Array.Empty<CommunityTurnoverMetrics>());
        }

        private static void CommitDeep(DeepTimeArchive archive, int generation, IReadOnlyList<string> lineageIds)
        {
            var lineages = lineageIds.Select(id => new DeepTimeLineageState(
                id,
                ParentLineageId: null,
                SpeciesFamilyId: "species.family.a",
                FirstObservedGeneration: 5,
                LastObservedGeneration: generation,
                Population: 100,
                OccupiedZones: new HashSet<string>(StringComparer.Ordinal) { ZoneId },
                NicheVector: new Dictionary<string, double>(StringComparer.Ordinal) { ["forest"] = 0.50 },
                Authority: DeepTimeLineageAuthority.CandidateTestHistory,
                Status: DeepTimeLineageStatus.Active,
                ConsecutiveZeroPopulationGenerations: 0,
                MeanDivergence: 0.20)).ToArray();

            archive.Commit(
                generation,
                generation,
                lineages,
                Array.Empty<AdaptiveRadiationEvent>(),
                Array.Empty<LineageExtinctionEvent>(),
                Array.Empty<EcologicalReplacementEvent>(),
                Array.Empty<CladeRecord>());
        }
    }
}
