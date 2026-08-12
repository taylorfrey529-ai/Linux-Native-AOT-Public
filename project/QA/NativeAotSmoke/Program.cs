using EasternKingdoms.Simulation;

internal static class Program
{
    private const string ZoneId = "zone.a";

    private static int Main()
    {
        try
        {
            var zones = new HashSet<string>(StringComparer.Ordinal) { ZoneId };
            var reconstructor = BuildReconstructor();
            var playback = new DeepTimeWorldStatePlayback(reconstructor);
            var frames = playback.Play(5, 10, 5, zones);
            Require(frames.Count == 2, "Expected two historical frames.");

            var replaySerializer = new HistoricalReplaySerializer(new HistoricalTimelineIndexer());
            var replayA = replaySerializer.Serialize(frames);
            var replayB = replaySerializer.Serialize(frames);
            Require(StringComparer.Ordinal.Equals(replayA.Json, replayB.Json), "Replay serialization is not deterministic.");
            var parsedReplay = replaySerializer.DeserializeAndVerify(replayA.Json);
            Require(parsedReplay.Payload.Frames.Count == 2, "Replay round-trip lost frames.");

            var builder = new HistoricalAtlasSceneBuilder(zones);
            var scene = builder.Build(frames[1], HistoricalAtlasCameraState.DefaultGlobe(), HistoricalAtlasSceneBuilder.DefaultLayers());
            var sceneSerializer = new HistoricalAtlasSceneSerializer(builder);
            var sceneFile = sceneSerializer.Serialize(scene);
            var parsedScene = sceneSerializer.DeserializeAndVerify(sceneFile.Json);
            Require(StringComparer.Ordinal.Equals(scene.IntegrityHash, parsedScene.Scene.IntegrityHash), "Scene round-trip changed integrity hash.");

            var camera = new HistoricalAtlasCameraState(HistoricalAtlasProjection.Globe, 35.0, -12.0, 0.0, 1.75, ZoneId);
            var cameraSerializer = new HistoricalAtlasCameraSerializer(zones);
            var cameraFile = cameraSerializer.Serialize(camera);
            var parsedCamera = cameraSerializer.DeserializeAndVerify(cameraFile.Json);
            Require(camera == parsedCamera.Camera, "Camera round-trip changed state.");

            Require(HistoricalPlaybackVerifier.Verify(playback.Snapshot()), "Playback integrity failed after serialization.");
            Console.WriteLine($"NATIVE_AOT_SMOKE PASS frames={frames.Count} replay={replayA.PayloadSha256} scene={sceneFile.PayloadSha256} camera={cameraFile.PayloadSha256}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"NATIVE_AOT_SMOKE FAIL: {ex.Message}");
            return 1;
        }
    }

    private static HistoricalWorldStateReconstructor BuildReconstructor()
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
        return new HistoricalWorldStateReconstructor(new[] { ZoneId }, geo, bio, deep);
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

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
