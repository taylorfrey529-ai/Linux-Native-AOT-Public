using EasternKingdoms.Simulation;
using Xunit;

namespace EasternKingdoms.Simulation.Tests;

public sealed class HistoricalInteractiveAtlasTests
{
    [Fact]
    public void Nearest_scrub_tie_prefers_earlier_recorded_generation()
    {
        var fixture = Fixture.TwoGenerations();
        var playback = new DeepTimeWorldStatePlayback(fixture.Reconstructor);
        var frames = playback.Play(5, 9, 4, ZoneSet());
        var scrubber = new HistoricalTimelineScrubber(frames);

        var resolved = scrubber.ResolveGeneration(7, HistoricalAtlasScrubPolicy.NearestRecorded);

        Assert.Equal(5, resolved.World.Generation);
    }

    [Fact]
    public void Exact_scrub_never_interpolates_missing_generation()
    {
        var fixture = Fixture.TwoGenerations();
        var playback = new DeepTimeWorldStatePlayback(fixture.Reconstructor);
        var frames = playback.Play(5, 10, 5, ZoneSet());
        var scrubber = new HistoricalTimelineScrubber(frames);

        Assert.Throws<KeyNotFoundException>(() => scrubber.ResolveGeneration(7, HistoricalAtlasScrubPolicy.ExactRecorded));
    }

    [Fact]
    public void Disabled_layers_remove_their_data_from_scene_payload()
    {
        var fixture = Fixture.TwoGenerations();
        var playback = new DeepTimeWorldStatePlayback(fixture.Reconstructor);
        var frame = new HistoricalPlaybackFrame(0, playback.Reconstruct(Request(10)));
        var builder = new HistoricalAtlasSceneBuilder(ZoneSet());
        var layers = new[]
        {
            new HistoricalAtlasLayerState(HistoricalAtlasLayer.Climate, false, 1.0),
            new HistoricalAtlasLayerState(HistoricalAtlasLayer.Landscape, false, 1.0),
            new HistoricalAtlasLayerState(HistoricalAtlasLayer.LineageRanges, true, 1.0)
        };

        var scene = builder.Build(frame, HistoricalAtlasCameraState.DefaultGlobe(), layers);
        var cell = Assert.Single(scene.Cells);

        Assert.Null(cell.ClimateRegime);
        Assert.Null(cell.Temperature);
        Assert.Null(cell.ForestCover);
        Assert.NotEmpty(cell.LivingLineageIds);
        Assert.True(builder.Verify(scene));
    }

    [Fact]
    public void Provenance_layer_exposes_only_recorded_evidence()
    {
        var fixture = Fixture.TwoGenerations();
        var playback = new DeepTimeWorldStatePlayback(fixture.Reconstructor);
        var frame = new HistoricalPlaybackFrame(0, playback.Reconstruct(Request(10)));
        var builder = new HistoricalAtlasSceneBuilder(ZoneSet());
        var layers = new[] { new HistoricalAtlasLayerState(HistoricalAtlasLayer.Provenance, true, 1.0) };

        var scene = builder.Build(frame, HistoricalAtlasCameraState.DefaultGlobe(), layers);
        var panel = Assert.Single(scene.ProvenancePanels);

        Assert.Equal(Fixture.ZoneId, panel.ZoneId);
        Assert.NotEmpty(panel.Evidence);
        Assert.All(panel.Evidence, evidence => Assert.True(evidence.EndGeneration <= 10));
        Assert.True(builder.Verify(scene));
    }

    [Fact]
    public void Lineage_range_layer_uses_living_lineage_evidence()
    {
        var fixture = Fixture.TwoGenerations();
        var playback = new DeepTimeWorldStatePlayback(fixture.Reconstructor);
        var frame = new HistoricalPlaybackFrame(0, playback.Reconstruct(Request(10)));
        var builder = new HistoricalAtlasSceneBuilder(ZoneSet());
        var layers = new[] { new HistoricalAtlasLayerState(HistoricalAtlasLayer.LineageRanges, true, 1.0) };

        var scene = builder.Build(frame, HistoricalAtlasCameraState.DefaultGlobe(), layers);

        Assert.Equal(2, scene.LineageRanges.Count);
        Assert.All(scene.LineageRanges, range => Assert.Contains(Fixture.ZoneId, range.ZoneIds));
        Assert.True(builder.Verify(scene));
    }

    [Fact]
    public void Camera_serialization_is_deterministic_and_round_trips()
    {
        var serializer = new HistoricalAtlasCameraSerializer(ZoneSet());
        var camera = new HistoricalAtlasCameraState(HistoricalAtlasProjection.Globe, 35.0, -12.0, 0.0, 1.75, Fixture.ZoneId);

        var left = serializer.Serialize(camera);
        var right = serializer.Serialize(camera);
        var parsed = serializer.DeserializeAndVerify(left.Json);

        Assert.Equal(left.Json, right.Json);
        Assert.Equal(left.PayloadSha256, right.PayloadSha256);
        Assert.Equal(camera, parsed.Camera);
    }

    [Fact]
    public void Tampered_camera_payload_is_rejected()
    {
        var serializer = new HistoricalAtlasCameraSerializer(ZoneSet());
        var camera = new HistoricalAtlasCameraState(HistoricalAtlasProjection.Globe, 35.0, -12.0, 0.0, 1.75, Fixture.ZoneId);
        var file = serializer.Serialize(camera);
        string tampered = file.Json.Replace("1.75", "2.75", StringComparison.Ordinal);

        Assert.NotEqual(file.Json, tampered);
        Assert.Throws<InvalidOperationException>(() => serializer.DeserializeAndVerify(tampered));
    }

    [Fact]
    public void Delta_comparison_builds_verified_overlay_without_mutating_sources()
    {
        var fixture = Fixture.TwoGenerations();
        var playback = new DeepTimeWorldStatePlayback(fixture.Reconstructor);
        var frames = playback.Play(5, 10, 5, ZoneSet());
        var before = playback.Snapshot();
        var scenes = new HistoricalAtlasSceneBuilder(ZoneSet());
        var comparisons = new HistoricalAtlasComparisonBuilder(scenes);

        var result = comparisons.Build(
            frames[0],
            frames[1],
            HistoricalAtlasComparisonMode.Delta,
            HistoricalAtlasCameraState.DefaultGlobe(),
            HistoricalAtlasSceneBuilder.DefaultLayers(),
            new[] { HistoricalOverlayMetric.Temperature });
        var after = playback.Snapshot();

        Assert.Single(result.Overlays);
        Assert.True(comparisons.Verify(result));
        Assert.Equal(before.IntegrityHash, after.IntegrityHash);
    }

    [Fact]
    public void Renderer_neutral_scene_serialization_is_deterministic_and_verified()
    {
        var fixture = Fixture.TwoGenerations();
        var playback = new DeepTimeWorldStatePlayback(fixture.Reconstructor);
        var frame = new HistoricalPlaybackFrame(0, playback.Reconstruct(Request(10)));
        var builder = new HistoricalAtlasSceneBuilder(ZoneSet());
        var scene = builder.Build(frame, HistoricalAtlasCameraState.DefaultGlobe(), HistoricalAtlasSceneBuilder.DefaultLayers());
        var serializer = new HistoricalAtlasSceneSerializer(builder);

        var left = serializer.Serialize(scene);
        var right = serializer.Serialize(scene);
        var parsed = serializer.DeserializeAndVerify(left.Json);

        Assert.Equal(left.Json, right.Json);
        Assert.Equal(left.PayloadSha256, right.PayloadSha256);
        Assert.Equal(scene.IntegrityHash, parsed.Scene.IntegrityHash);
    }

    [Fact]
    public void Unauthorized_camera_focus_is_rejected()
    {
        var serializer = new HistoricalAtlasCameraSerializer(ZoneSet());
        var camera = HistoricalAtlasCameraState.DefaultGlobe() with { FocusZoneId = "zone.outside" };

        Assert.Throws<InvalidOperationException>(() => serializer.Serialize(camera));
    }

    [Fact]
    public void Session_state_integrity_detects_tampering()
    {
        var fixture = Fixture.TwoGenerations();
        var playback = new DeepTimeWorldStatePlayback(fixture.Reconstructor);
        var frames = playback.Play(5, 10, 5, ZoneSet());
        var scenes = new HistoricalAtlasSceneBuilder(ZoneSet());
        var comparisons = new HistoricalAtlasComparisonBuilder(scenes);
        var session = new HistoricalAtlasSession(frames, ZoneSet(), scenes, comparisons);
        var state = session.Seek(10, HistoricalAtlasScrubPolicy.ExactRecorded);
        var tampered = state with { SelectedZoneId = Fixture.ZoneId };

        Assert.True(session.Verify(state));
        Assert.False(session.Verify(tampered));
        Assert.Equal(10, session.BuildScene().Generation);
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
