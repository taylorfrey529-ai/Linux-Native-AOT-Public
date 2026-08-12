using EasternKingdoms.Simulation;
using Xunit;

namespace EasternKingdoms.Simulation.Tests;

public sealed class HistoricalVisualizationSerializationTests
{
    [Fact]
    public void Same_frames_serialize_to_same_json_and_hash()
    {
        var fixture = Fixture.TwoGenerations();
        var playback = new DeepTimeWorldStatePlayback(fixture.Reconstructor);
        var frames = playback.Play(5, 10, 5, ZoneSet());
        var timeline = new HistoricalTimelineIndexer();
        var serializer = new HistoricalReplaySerializer(timeline);

        var left = serializer.Serialize(frames);
        var right = serializer.Serialize(frames);

        Assert.Equal(left.PayloadSha256, right.PayloadSha256);
        Assert.Equal(left.Json, right.Json);
        Assert.True(timeline.Verify(left.File.Payload.Timeline));
    }

    [Fact]
    public void Tampered_replay_payload_is_rejected()
    {
        var fixture = Fixture.TwoGenerations();
        var playback = new DeepTimeWorldStatePlayback(fixture.Reconstructor);
        var frames = playback.Play(5, 10, 5, ZoneSet());
        var serializer = new HistoricalReplaySerializer(new HistoricalTimelineIndexer());
        var result = serializer.Serialize(frames);
        string tampered = result.Json.Replace("\"generation\":10", "\"generation\":11", StringComparison.Ordinal);

        Assert.NotEqual(result.Json, tampered);
        Assert.Throws<InvalidOperationException>(() => serializer.DeserializeAndVerify(tampered));
    }

    [Fact]
    public void Serialized_replay_round_trips_through_integrity_verifier()
    {
        var fixture = Fixture.TwoGenerations();
        var playback = new DeepTimeWorldStatePlayback(fixture.Reconstructor);
        var frames = playback.Play(5, 10, 5, ZoneSet());
        var serializer = new HistoricalReplaySerializer(new HistoricalTimelineIndexer());
        var result = serializer.Serialize(frames);

        var file = serializer.DeserializeAndVerify(result.Json);

        Assert.Equal("evermore.eastern-kingdoms.history/22.0", file.SchemaVersion);
        Assert.Equal(HistoricalVisualizationAuthority.TestHistoryOnly, file.Authority);
        Assert.Equal(2, file.Payload.Frames.Count);
    }

    [Fact]
    public void Timeline_tamper_fails_verification()
    {
        var fixture = Fixture.TwoGenerations();
        var playback = new DeepTimeWorldStatePlayback(fixture.Reconstructor);
        var frames = playback.Play(5, 10, 5, ZoneSet());
        var indexer = new HistoricalTimelineIndexer();
        var timeline = indexer.Build(frames);
        var tampered = timeline with { IntegrityHash = "tampered" };

        Assert.False(indexer.Verify(tampered));
    }

    [Fact]
    public void Temperature_overlay_reports_increase_and_verifies()
    {
        var fixture = Fixture.TwoGenerations();
        var playback = new DeepTimeWorldStatePlayback(fixture.Reconstructor);
        var from = playback.Reconstruct(Request(5));
        var to = playback.Reconstruct(Request(10));
        var builder = new HistoricalOverlayBuilder();

        var overlay = builder.Build(from, to, HistoricalOverlayMetric.Temperature);
        var cell = Assert.Single(overlay.Cells);

        Assert.Equal(HistoricalOverlayDirection.Increase, cell.Direction);
        Assert.True(cell.Delta > 0.0);
        Assert.True(builder.Verify(overlay));
    }

    [Fact]
    public void Tampered_overlay_direction_fails_verification()
    {
        var fixture = Fixture.TwoGenerations();
        var playback = new DeepTimeWorldStatePlayback(fixture.Reconstructor);
        var builder = new HistoricalOverlayBuilder();
        var overlay = builder.Build(playback.Reconstruct(Request(5)), playback.Reconstruct(Request(10)), HistoricalOverlayMetric.Temperature);
        var cell = Assert.Single(overlay.Cells);
        var tampered = overlay with { Cells = new[] { cell with { Direction = HistoricalOverlayDirection.Decrease } } };

        Assert.False(builder.Verify(tampered));
    }

    [Fact]
    public void Globe_adapter_requires_authored_anchor_layout_and_preserves_zone_keys()
    {
        var fixture = Fixture.TwoGenerations();
        var playback = new DeepTimeWorldStatePlayback(fixture.Reconstructor);
        var world = playback.Reconstruct(Request(10));
        var adapter = new EasternKingdomsGlobeStateAdapter(ZoneSet());

        var frame = adapter.Adapt(world);
        var cell = Assert.Single(frame.Cells);

        Assert.Equal(GlobeLayoutPolicy.AuthoredAnchorsRequired, frame.LayoutPolicy);
        Assert.Equal(Fixture.ZoneId, cell.RenderAnchorKey);
        Assert.True(adapter.Verify(frame));
    }

    [Fact]
    public void Globe_adapter_rejects_verified_world_outside_its_authorized_zone_set()
    {
        var fixture = Fixture.TwoGenerations();
        var playback = new DeepTimeWorldStatePlayback(fixture.Reconstructor);
        var world = playback.Reconstruct(Request(10));
        var adapter = new EasternKingdomsGlobeStateAdapter(new[] { "zone.other" });

        Assert.Throws<InvalidOperationException>(() => adapter.Adapt(world));
    }

    [Fact]
    public void Serialization_does_not_mutate_historical_runtime_state()
    {
        var fixture = Fixture.TwoGenerations();
        var playback = new DeepTimeWorldStatePlayback(fixture.Reconstructor);
        var frames = playback.Play(5, 10, 5, ZoneSet());
        var before = playback.Snapshot();
        var serializer = new HistoricalReplaySerializer(new HistoricalTimelineIndexer());

        _ = serializer.Serialize(frames);
        var after = playback.Snapshot();

        Assert.Equal(before.IntegrityHash, after.IntegrityHash);
        Assert.True(HistoricalPlaybackVerifier.Verify(after));
    }

    private static HistoricalReconstructionRequest Request(int generation) => new(generation, ZoneSet());
    private static IReadOnlySet<string> ZoneSet() => new HashSet<string>(StringComparer.Ordinal) { Fixture.ZoneId };

    private sealed record Fixture(HistoricalWorldStateReconstructor Reconstructor)
    {
        public const string ZoneId = "zone.a";

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
            return new Fixture(new HistoricalWorldStateReconstructor(new[] { ZoneId }, geo, bio, deep));
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
