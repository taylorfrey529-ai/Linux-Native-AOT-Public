using System.Security.Cryptography;
using System.Text;
using Evermore.ThreeD.Environment;
using Xunit;

namespace OfflineHarness;

public sealed class OmegaSandboxTests
{
    [Fact]
    public void Engine_CreatesOrderedSystemsWithExact18DimensionalBodies()
    {
        OmegaSandboxSnapshot snapshot = CreateSnapshot();

        Assert.Equal(OmegaSandboxDisposition.Completed, snapshot.Disposition);
        Assert.Equal(0, snapshot.SimulationTick);
        Assert.Equal(OmegaSandboxDimensions.Count, snapshot.DimensionNames.Length);
        Assert.Equal(OmegaSandboxDimensions.Count, snapshot.DimensionNames.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(3, snapshot.SystemCount);
        Assert.Equal(12, snapshot.WorldCount);
        Assert.Equal(24, snapshot.MoonCount);
        Assert.Equal(39, snapshot.TotalBodyCount);
        Assert.Equal("omega.system.0000", snapshot.Systems[0].SystemId);

        foreach (OmegaSandboxSystem system in snapshot.Systems)
        {
            Assert.Equal(OmegaSandboxBodyKinds.Hearthstar, system.Hearthstar.BodyKind);
            Assert.Null(system.Hearthstar.ParentBodyId);
            Assert.Equal(OmegaSandboxDimensions.Count, system.Hearthstar.DimensionVector.Length);
            Assert.Equal(4, system.Worlds.Length);
            Assert.Equal(8, system.Moons.Length);
            Assert.All(system.Worlds, world =>
            {
                Assert.Equal(system.Hearthstar.BodyId, world.ParentBodyId);
                Assert.Equal(OmegaSandboxDimensions.Count, world.DimensionVector.Length);
                Assert.True(IsSpatiallyNear(world, system.Hearthstar, 85_000));
            });
            Assert.All(system.Moons, moon =>
            {
                Assert.Contains(moon.ParentBodyId!, system.Worlds.Select(world => world.BodyId));
                Assert.Equal(OmegaSandboxDimensions.Count, moon.DimensionVector.Length);
                OmegaSandboxBody parent = system.Worlds.Single(world => world.BodyId == moon.ParentBodyId);
                Assert.True(IsSpatiallyNear(moon, parent, 17_000));
            });
        }
    }

    [Fact]
    public void Engine_IsDeterministicAndSeedSensitive()
    {
        string first = OmegaSandboxSerializer.Serialize(CreateSnapshot());
        string second = OmegaSandboxSerializer.Serialize(CreateSnapshot());
        OmegaSandboxSnapshot alternate = new OmegaSandboxEngine().Process(
            new OmegaSandboxInput(530, 3, 3, 4, 2));

        Assert.Equal(first, second);
        Assert.NotEqual(first, OmegaSandboxSerializer.Serialize(alternate));
    }

    [Fact]
    public void Engine_AdvancesAll18AxesWithStableIdentityAndParentage()
    {
        OmegaSandboxSnapshot initial = CreateSnapshot();
        OmegaSandboxSnapshot advanced = new OmegaSandboxEngine().Advance(initial, 37);
        OmegaSandboxBody[] initialBodies = FlattenBodies(initial);
        OmegaSandboxBody[] advancedBodies = FlattenBodies(advanced);

        Assert.Equal(37, advanced.SimulationTick);
        Assert.Equal(initial.TotalBodyCount, advanced.TotalBodyCount);
        Assert.Equal(initialBodies.Select(body => body.BodyId).ToArray(), advancedBodies.Select(body => body.BodyId).ToArray());
        Assert.Equal(initialBodies.Select(body => body.ParentBodyId).ToArray(), advancedBodies.Select(body => body.ParentBodyId).ToArray());
        Assert.Equal(initialBodies.Select(body => body.ScaleMicros).ToArray(), advancedBodies.Select(body => body.ScaleMicros).ToArray());
        Assert.All(Enumerable.Range(0, OmegaSandboxDimensions.Count), axis =>
            Assert.True(initialBodies.Zip(advancedBodies).Any(pair =>
                pair.First.DimensionVector[axis] != pair.Second.DimensionVector[axis])));
        Assert.All(advancedBodies, body =>
        {
            Assert.All(body.DimensionVector.Take(OmegaSandboxDimensions.Mana), value =>
                Assert.True(value is >= OmegaSandboxDimensions.MinimumValue and <= OmegaSandboxDimensions.MaximumValue));
            Assert.All(body.DimensionVector.Skip(OmegaSandboxDimensions.Mana), value =>
                Assert.True(value is >= 0 and <= OmegaSandboxDimensions.MaximumValue));
        });
        Assert.True(IsSpatiallyNear(advanced.Systems[0].Worlds[0], advanced.Systems[0].Hearthstar, 85_000));
        Assert.True(IsSpatiallyNear(advanced.Systems[0].Moons[0], advanced.Systems[0].Worlds[0], 17_000));
    }

    [Fact]
    public void Engine_RejectsRequestsBeyondMassiveBodyBoundBeforeAllocation()
    {
        var input = new OmegaSandboxInput(
            529,
            3,
            OmegaSandboxEngine.MaximumSystems,
            OmegaSandboxEngine.MaximumWorldsPerSystem,
            OmegaSandboxEngine.MaximumMoonsPerWorld);

        ArgumentOutOfRangeException error = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new OmegaSandboxEngine().Process(input));

        Assert.Contains("cannot exceed 100000 bodies", error.Message);
    }

    [Fact]
    public void Serializer_RejectsPayloadTamperingAndSemanticDrift()
    {
        string json = OmegaSandboxSerializer.Serialize(CreateSnapshot());
        string tampered = json.Replace(
            "\"bodyKind\":\"hearthstar\"",
            "\"bodyKind\":\"canon-world\"",
            StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() =>
            OmegaSandboxSerializer.DeserializeAndVerify(tampered));
        OmegaSandboxSnapshot verified = OmegaSandboxSerializer.DeserializeAndVerify(json);
        Assert.Equal(39, verified.TotalBodyCount);

        string legacy = json.Replace(
            OmegaSandboxSerializer.SchemaVersion,
            OmegaSandboxSerializer.LegacySchemaVersion,
            StringComparison.Ordinal);
        Assert.Equal(0, OmegaSandboxSerializer.DeserializeAndVerify(legacy).SimulationTick);
        string invalidLegacyAdvance = OmegaSandboxSerializer.Serialize(
            new OmegaSandboxEngine().Advance(verified, 1)).Replace(
                OmegaSandboxSerializer.SchemaVersion,
                OmegaSandboxSerializer.LegacySchemaVersion,
                StringComparison.Ordinal);
        Assert.Throws<InvalidDataException>(() =>
            OmegaSandboxSerializer.DeserializeAndVerify(invalidLegacyAdvance));
    }

    [Fact]
    public void Renderer_ProjectsSelectedAxesDeterministically()
    {
        OmegaSandboxSnapshot snapshot = CreateSnapshot();
        var renderer = new OmegaSandboxSoftwareRenderer();
        byte[] first = renderer.Render(snapshot, 160, 96, 0, 1, 2);
        byte[] second = renderer.Render(snapshot, 160, 96, 0, 1, 2);
        byte[] alternate = renderer.Render(snapshot, 160, 96, 4, 5, 6);
        byte[] header = Encoding.ASCII.GetBytes("P6\n160 96\n255\n");

        Assert.Equal(header, first.Take(header.Length).ToArray());
        Assert.Equal(header.Length + (160 * 96 * 3), first.Length);
        Assert.Equal(first, second);
        Assert.NotEqual(Hash(first), Hash(alternate));
        Assert.Throws<ArgumentException>(() => renderer.Render(snapshot, 160, 96, 0, 0, 2));
    }

    [Fact]
    public void Host_CreatesAndRendersVerifiedSandboxWithoutOverwrite()
    {
        string snapshotPath = TemporaryPath("json");
        string framePath = TemporaryPath("ppm");
        try
        {
            var createOutput = new StringWriter();
            var createError = new StringWriter();
            int createExit = EnvironmentHostApp.Invoke(
                CreateArguments(snapshotPath),
                createOutput,
                createError);

            Assert.Equal(EnvironmentHostApp.SuccessExitCode, createExit);
            Assert.Equal(string.Empty, createError.ToString());
            Assert.True(File.Exists(snapshotPath));
            Assert.Contains("dimensions=18", createOutput.ToString());
            Assert.Contains("bodies=39", createOutput.ToString());

            var renderOutput = new StringWriter();
            var renderError = new StringWriter();
            int renderExit = EnvironmentHostApp.Invoke(
                RenderArguments(snapshotPath, framePath),
                renderOutput,
                renderError);

            Assert.Equal(EnvironmentHostApp.SuccessExitCode, renderExit);
            Assert.Equal(string.Empty, renderError.ToString());
            Assert.True(File.Exists(framePath));
            Assert.Contains("projection_axes=0,4,6", renderOutput.ToString());

            byte[] original = File.ReadAllBytes(framePath);
            int overwriteExit = EnvironmentHostApp.Invoke(
                RenderArguments(snapshotPath, framePath),
                TextWriter.Null,
                renderError);
            Assert.Equal(EnvironmentHostApp.IoFailureExitCode, overwriteExit);
            Assert.Equal(original, File.ReadAllBytes(framePath));
        }
        finally
        {
            if (File.Exists(snapshotPath))
                File.Delete(snapshotPath);
            if (File.Exists(framePath))
                File.Delete(framePath);
        }
    }

    [Fact]
    public void Host_AdvancesVerifiedSandboxAndRendersChangedObservation()
    {
        string initialPath = TemporaryPath("json");
        string advancedPath = TemporaryPath("json");
        string initialFramePath = TemporaryPath("ppm");
        string advancedFramePath = TemporaryPath("ppm");
        try
        {
            Assert.Equal(
                EnvironmentHostApp.SuccessExitCode,
                EnvironmentHostApp.Invoke(CreateArguments(initialPath), TextWriter.Null, TextWriter.Null));
            byte[] initialBytes = File.ReadAllBytes(initialPath);
            var advanceOutput = new StringWriter();
            var advanceError = new StringWriter();

            int advanceExit = EnvironmentHostApp.Invoke(
                AdvanceArguments(initialPath, advancedPath, 37),
                advanceOutput,
                advanceError);

            Assert.Equal(EnvironmentHostApp.SuccessExitCode, advanceExit);
            Assert.Equal(string.Empty, advanceError.ToString());
            Assert.Equal(initialBytes, File.ReadAllBytes(initialPath));
            Assert.Contains("previous_tick=0", advanceOutput.ToString());
            Assert.Contains("simulation_tick=37", advanceOutput.ToString());
            Assert.Equal(
                37,
                OmegaSandboxSerializer.DeserializeAndVerify(
                    File.ReadAllText(advancedPath)).SimulationTick);

            Assert.Equal(
                EnvironmentHostApp.SuccessExitCode,
                EnvironmentHostApp.Invoke(
                    RenderArguments(initialPath, initialFramePath),
                    TextWriter.Null,
                    TextWriter.Null));
            Assert.Equal(
                EnvironmentHostApp.SuccessExitCode,
                EnvironmentHostApp.Invoke(
                    RenderArguments(advancedPath, advancedFramePath),
                    TextWriter.Null,
                    TextWriter.Null));
            Assert.NotEqual(Hash(File.ReadAllBytes(initialFramePath)), Hash(File.ReadAllBytes(advancedFramePath)));

            string invalidPath = TemporaryPath("json");
            try
            {
                var invalidError = new StringWriter();
                int invalidExit = EnvironmentHostApp.Invoke(
                    AdvanceArguments(advancedPath, invalidPath, 0),
                    TextWriter.Null,
                    invalidError);
                Assert.Equal(EnvironmentHostApp.InputFailureExitCode, invalidExit);
                Assert.Contains("Advance ticks must be between", invalidError.ToString());
                Assert.False(File.Exists(invalidPath));
            }
            finally
            {
                if (File.Exists(invalidPath))
                    File.Delete(invalidPath);
            }
        }
        finally
        {
            foreach (string path in new[] { initialPath, advancedPath, initialFramePath, advancedFramePath })
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }
    }

    [Fact]
    public void Host_RejectsOversizedSandboxAndRepeatedProjectionAxesWithoutOutputs()
    {
        string oversizedPath = TemporaryPath("json");
        string snapshotPath = TemporaryPath("json");
        string framePath = TemporaryPath("ppm");
        try
        {
            string[] oversized =
            [
                "sandbox-create",
                "--seed", "529",
                "--generations", "3",
                "--systems", "512",
                "--worlds", "32",
                "--moons", "16",
                "--output", oversizedPath
            ];
            var oversizedError = new StringWriter();
            int oversizedExit = EnvironmentHostApp.Invoke(oversized, TextWriter.Null, oversizedError);

            Assert.Equal(EnvironmentHostApp.InputFailureExitCode, oversizedExit);
            Assert.Contains("cannot exceed 100000 bodies", oversizedError.ToString());
            Assert.False(File.Exists(oversizedPath));

            Assert.Equal(
                EnvironmentHostApp.SuccessExitCode,
                EnvironmentHostApp.Invoke(CreateArguments(snapshotPath), TextWriter.Null, TextWriter.Null));
            string[] repeatedAxes = RenderArguments(snapshotPath, framePath);
            repeatedAxes[12] = "0";
            var axesError = new StringWriter();
            int axesExit = EnvironmentHostApp.Invoke(repeatedAxes, TextWriter.Null, axesError);

            Assert.Equal(EnvironmentHostApp.InputFailureExitCode, axesExit);
            Assert.Contains("Projection axes must be distinct", axesError.ToString());
            Assert.False(File.Exists(framePath));
        }
        finally
        {
            if (File.Exists(oversizedPath))
                File.Delete(oversizedPath);
            if (File.Exists(snapshotPath))
                File.Delete(snapshotPath);
            if (File.Exists(framePath))
                File.Delete(framePath);
        }
    }

    private static OmegaSandboxSnapshot CreateSnapshot() =>
        new OmegaSandboxEngine().Process(new OmegaSandboxInput(529, 3, 3, 4, 2));

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private static OmegaSandboxBody[] FlattenBodies(OmegaSandboxSnapshot snapshot) =>
        snapshot.Systems.SelectMany(system =>
            new[] { system.Hearthstar }.Concat(system.Worlds).Concat(system.Moons)).ToArray();

    private static bool IsSpatiallyNear(OmegaSandboxBody body, OmegaSandboxBody parent, int maximumSpan) =>
        Enumerable.Range(OmegaSandboxDimensions.X, 3).All(axis =>
            Math.Abs(body.DimensionVector[axis] - parent.DimensionVector[axis]) <= maximumSpan);

    private static string TemporaryPath(string extension) =>
        Path.Combine(Path.GetTempPath(), $"evermore-omega-{Guid.NewGuid():N}.{extension}");

    private static string[] CreateArguments(string outputPath) =>
    [
        "sandbox-create",
        "--seed", "529",
        "--generations", "3",
        "--systems", "3",
        "--worlds", "4",
        "--moons", "2",
        "--output", outputPath
    ];

    private static string[] RenderArguments(string snapshotPath, string framePath) =>
    [
        "sandbox-render",
        "--input", snapshotPath,
        "--output", framePath,
        "--width", "160",
        "--height", "96",
        "--axis-x", "0",
        "--axis-y", "4",
        "--axis-depth", "6"
    ];

    private static string[] AdvanceArguments(
        string snapshotPath,
        string outputPath,
        int ticks) =>
    [
        "sandbox-advance",
        "--input", snapshotPath,
        "--ticks", ticks.ToString(System.Globalization.CultureInfo.InvariantCulture),
        "--output", outputPath
    ];
}
