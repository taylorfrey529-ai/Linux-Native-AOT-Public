using System.Text.Json;
using EasternKingdoms.Simulation;
using Evermore.Azeroth.Simulation;
using Evermore.Cli;
using Xunit;

namespace OfflineHarness;

public sealed class AzerothSimulationTests
{
    [Fact]
    public void Process_ComposesAuthorizedScopeDeterministically()
    {
        AzerothSimulationInput input = LoadInput();
        var engine = new AzerothSimulationEngine();

        AzerothSimulationSnapshot first = engine.Process(input);
        AzerothSimulationSnapshot second = engine.Process(input);

        Assert.Equal(AzerothSimulationDisposition.Completed, first.Disposition);
        Assert.Equal(42, first.Generation);
        Assert.Equal(
            AuthorizedZoneIds,
            first.Azeroth.Zones.Select(zone => zone.ZoneId).ToArray());
        Assert.Equal("completed", first.Omega.Disposition);
        Assert.Equal("completed", first.Lunara.Disposition);
        Assert.Equal("completed", first.Solune.Disposition);
        Assert.Equal(
            AzerothSimulationSnapshotSerializer.Serialize(first),
            AzerothSimulationSnapshotSerializer.Serialize(second));
    }

    [Fact]
    public void Process_RejectsCrossComponentClockDrift()
    {
        AzerothSimulationInput input = LoadInput();
        AzerothSimulationInput drifted = input with
        {
            Solune = input.Solune with { Generation = input.Solune.Generation + 1 }
        };

        ArgumentException error = Assert.Throws<ArgumentException>(() =>
            new AzerothSimulationEngine().Process(drifted));
        Assert.Contains("same Azeroth generation", error.Message);
    }

    [Fact]
    public void Serializer_RejectsTamperedComposite()
    {
        string json = AzerothSimulationSnapshotSerializer.Serialize(
            new AzerothSimulationEngine().Process(LoadInput()));
        string tampered = json.Replace(
            "authorized-northern-scar-corridor",
            "unauthorized-continent",
            StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() =>
            AzerothSimulationSnapshotSerializer.DeserializeAndVerify(tampered));
    }

    [Fact]
    public async Task Cli_EmitsVerifiedTestHistoryOnlyEnvelope()
    {
        var output = new StringWriter();
        var error = new StringWriter();
        int exitCode = await EvermoreCliApp.InvokeAsync(
            new[]
            {
                "azeroth",
                "run",
                "--input",
                FixturePath,
                "--format",
                "json"
            },
            output,
            error);

        Assert.Equal(EvermoreCliApp.SuccessExitCode, exitCode);
        Assert.Equal(string.Empty, error.ToString());
        using JsonDocument document = JsonDocument.Parse(output.ToString());
        Assert.Equal(
            AzerothSimulationSnapshotSerializer.SchemaVersion,
            document.RootElement.GetProperty("schemaVersion").GetString());
        Assert.Equal(
            AzerothSimulationSnapshotSerializer.Authority,
            document.RootElement.GetProperty("authority").GetString());
        Assert.Equal(
            4,
            document.RootElement.GetProperty("payload").GetProperty("azeroth").GetProperty("zones").GetArrayLength());
    }

    [Fact]
    public void EnvironmentalField_ComposesAuthoredSignalsDeterministically()
    {
        AzerothEnvironmentalSimulationInput input = LoadEnvironmentalInput();
        var engine = new AzerothEnvironmentalFieldEngine();

        AzerothEnvironmentalSimulationSnapshot first = engine.Process(input);
        AzerothEnvironmentalSimulationSnapshot second = engine.Process(input);

        Assert.Equal(AzerothSimulationDisposition.Completed, first.Environment.Disposition);
        Assert.Equal(AzerothEnvironmentalFieldEngine.UnitMicros, first.Environment.Drivers.SoluneIrradianceMicros);
        Assert.Equal(AzerothEnvironmentalFieldEngine.Units, first.Environment.Units);
        Assert.Equal(AzerothEnvironmentalFieldEngine.SpatialAuthority, first.Environment.SpatialAuthority);
        Assert.Equal(AuthorizedZoneIds, first.Environment.Zones.Select(zone => zone.ZoneId).ToArray());
        foreach (AzerothZoneFieldObservation zone in first.Environment.Zones)
        {
            AzerothZoneFieldInput authored = Assert.Single(
                input.Environment.Zones,
                candidate => StringComparer.Ordinal.Equals(candidate.ZoneId, zone.ZoneId));
            Assert.Equal(
                Math.Min(
                    AzerothEnvironmentalFieldEngine.UnitMicros,
                    authored.AtmosphereBaselineMicros +
                        zone.SoluneAtmosphereContributionMicros +
                        zone.LunaraAtmosphereContributionMicros),
                zone.AtmosphereSignalMicros);
            Assert.Equal(
                Math.Min(
                    AzerothEnvironmentalFieldEngine.UnitMicros,
                    authored.ManaBaselineMicros +
                        zone.LunaraManaContributionMicros +
                        zone.OmegaManaContributionMicros),
                zone.ManaSignalMicros);
        }
        Assert.Equal(
            AzerothEnvironmentalFieldSnapshotSerializer.Serialize(first),
            AzerothEnvironmentalFieldSnapshotSerializer.Serialize(second));
    }

    [Fact]
    public void EnvironmentalField_RejectsUnauthorizedZoneSet()
    {
        AzerothEnvironmentalSimulationInput input = LoadEnvironmentalInput();
        AzerothEnvironmentalSimulationInput incomplete = input with
        {
            Environment = input.Environment with
            {
                Zones = input.Environment.Zones.Take(input.Environment.Zones.Count - 1).ToArray()
            }
        };

        ArgumentException error = Assert.Throws<ArgumentException>(() =>
            new AzerothEnvironmentalFieldEngine().Process(incomplete));
        Assert.Contains("exactly match", error.Message);
    }

    [Fact]
    public void EnvironmentalField_RejectsGenerationDrift()
    {
        AzerothEnvironmentalSimulationInput input = LoadEnvironmentalInput();
        AzerothEnvironmentalSimulationInput drifted = input with
        {
            Environment = input.Environment with { Generation = input.Environment.Generation + 1 }
        };

        ArgumentException error = Assert.Throws<ArgumentException>(() =>
            new AzerothEnvironmentalFieldEngine().Process(drifted));
        Assert.Contains("same Azeroth generation", error.Message);
    }

    [Fact]
    public void EnvironmentalField_PropagatesContainmentWithoutEvaluatingZones()
    {
        AzerothEnvironmentalSimulationInput input = LoadEnvironmentalInput();
        AzerothEnvironmentalSimulationInput contained = input with
        {
            Simulation = input.Simulation with
            {
                Solune = input.Simulation.Solune with
                {
                    Model = input.Simulation.Solune.Model with { ContainmentIntegrityMicros = 900_000 }
                }
            }
        };

        AzerothEnvironmentalSimulationSnapshot snapshot =
            new AzerothEnvironmentalFieldEngine().Process(contained);

        Assert.Equal(AzerothSimulationDisposition.Contained, snapshot.Environment.Disposition);
        Assert.Empty(snapshot.Environment.Zones);
        Assert.All(
            new[]
            {
                snapshot.Environment.Drivers.SoluneIrradianceMicros,
                snapshot.Environment.Drivers.LunaraTideMicros,
                snapshot.Environment.Drivers.LunaraResonanceMicros,
                snapshot.Environment.Drivers.OmegaFitnessMicros
            },
            value => Assert.Equal(0, value));
    }

    [Fact]
    public void EnvironmentalFieldSerializer_RejectsTamperedSignal()
    {
        string json = AzerothEnvironmentalFieldSnapshotSerializer.Serialize(
            new AzerothEnvironmentalFieldEngine().Process(LoadEnvironmentalInput()));
        string tampered = json.Replace(
            AzerothEnvironmentalFieldEngine.SpatialAuthority,
            "inferred-geography",
            StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() =>
            AzerothEnvironmentalFieldSnapshotSerializer.DeserializeAndVerify(tampered));
    }

    [Fact]
    public async Task EnvironmentalFieldCli_EmitsVerifiedTestHistoryOnlyEnvelope()
    {
        var output = new StringWriter();
        var error = new StringWriter();
        int exitCode = await EvermoreCliApp.InvokeAsync(
            new[]
            {
                "azeroth",
                "field",
                "--input",
                EnvironmentalFixturePath,
                "--format",
                "json"
            },
            output,
            error);

        Assert.Equal(EvermoreCliApp.SuccessExitCode, exitCode);
        Assert.Equal(string.Empty, error.ToString());
        using JsonDocument document = JsonDocument.Parse(output.ToString());
        Assert.Equal(
            AzerothEnvironmentalFieldSnapshotSerializer.SchemaVersion,
            document.RootElement.GetProperty("schemaVersion").GetString());
        JsonElement environment = document.RootElement.GetProperty("payload").GetProperty("environment");
        Assert.Equal(AzerothEnvironmentalFieldEngine.Units, environment.GetProperty("units").GetString());
        Assert.Equal(4, environment.GetProperty("zones").GetArrayLength());
    }

    [Fact]
    public void OrbitalScene_ComposesNormalizedViewDeterministically()
    {
        AzerothEnvironmentalSimulationInput environment = LoadEnvironmentalInput();
        var input = new AzerothOrbitalSceneInput(
            environment,
            new AzerothOrbitalCameraInput(125_000, 50_000, 2));
        var engine = new AzerothOrbitalSceneEngine();

        AzerothOrbitalSceneSnapshot first = engine.Process(input);
        AzerothOrbitalSceneSnapshot second = engine.Process(input);

        Assert.Equal(AzerothSimulationDisposition.Completed, first.Disposition);
        Assert.Equal(AzerothOrbitalSceneEngine.Classification, first.Classification);
        Assert.Equal(AzerothOrbitalSceneEngine.GeometryAuthority, first.GeometryAuthority);
        Assert.Equal(AzerothOrbitalSceneEngine.UnitMicros, first.Scale.EarthReferenceMicros);
        Assert.Equal(AzerothOrbitalSceneEngine.UnitMicros, first.Scale.AzerothReferenceMicros);
        Assert.Equal(AzerothOrbitalSceneEngine.LinearRatio, first.Scale.LinearRatio);
        Assert.Equal(AzerothOrbitalSceneEngine.PhysicalScale, first.Scale.PhysicalScale);
        Assert.Equal(250_000, first.Camera.ViewportReferenceWidthMicros);
        Assert.Equal(AuthorizedZoneIds, first.Zones.Select(zone => zone.ZoneId).ToArray());
        Assert.All(
            first.Zones,
            zone => Assert.Equal(AzerothOrbitalSceneEngine.MissingGlobePlacement, zone.GlobePlacement));
        Assert.Equal(
            Average(first.Zones.Select(zone => zone.AtmosphereSignalMicros)),
            first.Lighting.AtmosphereShellSignalMicros);
        Assert.Equal(
            Average(first.Zones.Select(zone => zone.ManaSignalMicros)),
            first.Lighting.ManaVisibilitySignalMicros);
        Assert.Equal(
            AzerothOrbitalSceneSerializer.Serialize(first),
            AzerothOrbitalSceneSerializer.Serialize(second));
    }

    [Fact]
    public void OrbitalScene_ZoomUsesNormalizedReferenceExtent()
    {
        AzerothEnvironmentalSimulationInput environment = LoadEnvironmentalInput();
        var engine = new AzerothOrbitalSceneEngine();

        int[] widths = Enumerable.Range(0, AzerothOrbitalSceneEngine.MaximumZoomLevel + 1)
            .Select(zoom => engine.Process(new AzerothOrbitalSceneInput(
                environment,
                new AzerothOrbitalCameraInput(0, 0, zoom))).Camera.ViewportReferenceWidthMicros)
            .ToArray();

        Assert.Equal(
            new[] { 1_000_000, 500_000, 250_000, 125_000, 62_500, 31_250, 15_625, 7_812, 3_906 },
            widths);
    }

    [Fact]
    public void OrbitalScene_RejectsOutOfRangeCamera()
    {
        var input = new AzerothOrbitalSceneInput(
            LoadEnvironmentalInput(),
            new AzerothOrbitalCameraInput(AzerothOrbitalSceneEngine.UnitMicros, 0, 0));

        ArgumentOutOfRangeException error = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new AzerothOrbitalSceneEngine().Process(input));
        Assert.Contains("Orbit azimuth", error.Message);
    }

    [Fact]
    public void OrbitalScene_PropagatesContainmentWithoutPlacingZones()
    {
        AzerothEnvironmentalSimulationInput environment = LoadEnvironmentalInput();
        AzerothEnvironmentalSimulationInput contained = environment with
        {
            Simulation = environment.Simulation with
            {
                Solune = environment.Simulation.Solune with
                {
                    Model = environment.Simulation.Solune.Model with { ContainmentIntegrityMicros = 900_000 }
                }
            }
        };
        var input = new AzerothOrbitalSceneInput(
            contained,
            new AzerothOrbitalCameraInput(125_000, 50_000, 2));

        AzerothOrbitalSceneSnapshot snapshot = new AzerothOrbitalSceneEngine().Process(input);

        Assert.Equal(AzerothSimulationDisposition.Contained, snapshot.Disposition);
        Assert.Empty(snapshot.Zones);
        Assert.Equal(0, snapshot.Lighting.AtmosphereShellSignalMicros);
        Assert.Equal(0, snapshot.Lighting.ManaVisibilitySignalMicros);
    }

    [Fact]
    public void OrbitalSceneSerializer_RejectsTamperedGeometryAuthority()
    {
        var input = new AzerothOrbitalSceneInput(
            LoadEnvironmentalInput(),
            new AzerothOrbitalCameraInput(125_000, 50_000, 2));
        string json = AzerothOrbitalSceneSerializer.Serialize(
            new AzerothOrbitalSceneEngine().Process(input));
        string tampered = json.Replace(
            AzerothOrbitalSceneEngine.GeometryAuthority,
            "inferred-from-concept-art",
            StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() =>
            AzerothOrbitalSceneSerializer.DeserializeAndVerify(tampered));
    }

    [Fact]
    public async Task OrbitalSceneCli_EmitsConstrainedSimulationConceptEnvelope()
    {
        var output = new StringWriter();
        var error = new StringWriter();
        int exitCode = await EvermoreCliApp.InvokeAsync(
            new[]
            {
                "azeroth",
                "orbit",
                "--input",
                EnvironmentalFixturePath,
                "--azimuth-micros",
                "125000",
                "--elevation-micros",
                "50000",
                "--zoom",
                "2",
                "--format",
                "json"
            },
            output,
            error);

        Assert.Equal(EvermoreCliApp.SuccessExitCode, exitCode);
        Assert.Equal(string.Empty, error.ToString());
        using JsonDocument document = JsonDocument.Parse(output.ToString());
        Assert.Equal(
            AzerothOrbitalSceneSerializer.SchemaVersion,
            document.RootElement.GetProperty("schemaVersion").GetString());
        JsonElement payload = document.RootElement.GetProperty("payload");
        Assert.Equal(AzerothOrbitalSceneEngine.Classification, payload.GetProperty("classification").GetString());
        Assert.Equal(AzerothOrbitalSceneEngine.PhysicalScale, payload.GetProperty("scale").GetProperty("physicalScale").GetString());
        Assert.Equal(4, payload.GetProperty("zones").GetArrayLength());
    }

    [Fact]
    public void RegionAuthority_ContainsOnlyTheFourNegotiatedRegions()
    {
        AzerothRegionAuthoritySnapshot snapshot = AzerothRegionAuthority.CreateSnapshot();

        Assert.Equal(AzerothRegionAuthority.Authority, snapshot.Authority);
        Assert.Equal(AzerothRegionAuthority.ExpansionPolicy, snapshot.ExpansionPolicy);
        Assert.Equal(
            new[] { "Eversong", "Ghostlands", "Eastern Plaguelands", "Western Plaguelands" },
            snapshot.Regions.Select(region => region.RegionName).ToArray());
        Assert.Equal(AuthorizedZoneIds, AzerothRegionAuthority.CreateOrderedZoneIds());
        AzerothRegionAuthority.RequireExactSet(AuthorizedZoneIds);
        Assert.Throws<ArgumentException>(() =>
            AzerothRegionAuthority.RequireExactSet(AuthorizedZoneIds.Take(3)));
        Assert.Throws<ArgumentException>(() =>
            AzerothRegionAuthority.RequireExactSet(AuthorizedZoneIds.Append("ek.north.stratholme")));
        Assert.Throws<ArgumentException>(() =>
            AzerothRegionAuthority.RequireExactSet(
                new[]
                {
                    AzerothRegionAuthority.EversongZoneId,
                    AzerothRegionAuthority.EversongZoneId,
                    AzerothRegionAuthority.EasternPlaguelandsZoneId,
                    AzerothRegionAuthority.WesternPlaguelandsZoneId
                }));
    }

    [Fact]
    public void RegionAuthoritySerializer_RejectsAnyFifthRegion()
    {
        string json = AzerothRegionAuthoritySerializer.Serialize(
            AzerothRegionAuthority.CreateSnapshot());
        string tampered = json.Replace(
            AzerothRegionAuthority.EversongZoneId,
            "ek.north.stratholme",
            StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() =>
            AzerothRegionAuthoritySerializer.DeserializeAndVerify(tampered));
        AzerothRegionAuthorityEnvelope verified =
            AzerothRegionAuthoritySerializer.DeserializeAndVerify(json);
        Assert.Equal(4, verified.Payload.Regions.Count);
    }

    [Fact]
    public async Task RegionAuthorityCli_EmitsVerifiedFourRegionEnvelope()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        int exitCode = await EvermoreCliApp.InvokeAsync(
            new[] { "azeroth", "regions", "--format", "json" },
            output,
            error);

        Assert.Equal(EvermoreCliApp.SuccessExitCode, exitCode);
        Assert.Equal(string.Empty, error.ToString());
        using JsonDocument document = JsonDocument.Parse(output.ToString());
        Assert.Equal(
            AzerothRegionAuthoritySerializer.SchemaVersion,
            document.RootElement.GetProperty("schemaVersion").GetString());
        Assert.Equal(
            AzerothRegionAuthority.Authority,
            document.RootElement.GetProperty("authority").GetString());
        Assert.Equal(
            4,
            document.RootElement.GetProperty("payload").GetProperty("regions").GetArrayLength());
    }

    private static AzerothSimulationInput LoadInput() =>
        JsonSerializer.Deserialize(
            File.ReadAllText(FixturePath),
            AzerothSimulationJsonContext.Default.AzerothSimulationInput)
        ?? throw new JsonException("The Azeroth simulation fixture was null.");

    private static AzerothEnvironmentalSimulationInput LoadEnvironmentalInput() =>
        JsonSerializer.Deserialize(
            File.ReadAllText(EnvironmentalFixturePath),
            AzerothSimulationJsonContext.Default.AzerothEnvironmentalSimulationInput)
        ?? throw new JsonException("The Azeroth environmental field fixture was null.");

    private static string FixturePath =>
        Path.Combine(AppContext.BaseDirectory, "CliFixtures", "valid-azeroth-simulation.json");

    private static string EnvironmentalFixturePath =>
        Path.Combine(AppContext.BaseDirectory, "CliFixtures", "valid-azeroth-environmental-field.json");

    private static int Average(IEnumerable<int> values)
    {
        int[] materialized = values.ToArray();
        return checked((int)(materialized.Aggregate(0L, (sum, value) => sum + value) / materialized.Length));
    }

    private static string[] AuthorizedZoneIds =>
        AzerothRegionAuthority.CreateOrderedZoneIds();
}

