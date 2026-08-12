using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Evermore.Azeroth.Simulation;
using Evermore.ThreeD.Environment;
using Xunit;

namespace OfflineHarness;

public sealed class AzerothSoftwareRendererTests
{
    [Fact]
    public void Render_ProducesDeterministicBoundedPpmFramebuffer()
    {
        AzerothOrbitalSceneSnapshot scene = CreateScene(125_000, 50_000, 2);
        var renderer = new AzerothSoftwareRenderer();
        byte[] first = renderer.Render(scene, 160, 96);
        byte[] second = renderer.Render(scene, 160, 96);
        byte[] header = Encoding.ASCII.GetBytes("P6\n160 96\n255\n");

        Assert.Equal(header, first.Take(header.Length).ToArray());
        Assert.Equal(header.Length + (160 * 96 * 3), first.Length);
        Assert.Equal(first, second);
        Assert.True(first.Skip(header.Length).Distinct().Count() > 8);
    }

    [Fact]
    public void Render_CameraOrbitChangesFramebufferWithoutChangingAuthority()
    {
        var renderer = new AzerothSoftwareRenderer();
        byte[] first = renderer.Render(CreateScene(0, 0, 2), 128, 96);
        byte[] second = renderer.Render(CreateScene(250_000, 100_000, 2), 128, 96);

        Assert.NotEqual(
            Convert.ToHexString(SHA256.HashData(first)),
            Convert.ToHexString(SHA256.HashData(second)));
        Assert.Equal("ConstrainedSimulationConceptOnly", AzerothSoftwareRenderer.VisualAuthority);
    }

    [Fact]
    public void Render_RejectsContainedSceneAndOutOfRangeDimensions()
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
        AzerothOrbitalSceneSnapshot scene = new AzerothOrbitalSceneEngine().Process(
            new AzerothOrbitalSceneInput(contained, new AzerothOrbitalCameraInput(0, 0, 0)));
        var renderer = new AzerothSoftwareRenderer();

        Assert.Throws<InvalidOperationException>(() => renderer.Render(scene, 64, 64));
        Assert.Throws<ArgumentOutOfRangeException>(() => renderer.Render(CreateScene(0, 0, 0), 63, 64));
    }

    [Fact]
    public void Host_StatusDeclaresSelfContainedNativeAotBoundary()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        int exitCode = EnvironmentHostApp.Invoke(["status"], output, error);

        Assert.Equal(EnvironmentHostApp.SuccessExitCode, exitCode);
        Assert.Equal(string.Empty, error.ToString());
        Assert.Contains("product=€V€RMØRE 3D Environment", output.ToString());
        Assert.Contains("host=self-contained-native-aot", output.ToString());
        Assert.Contains("geometry=generic-globe-shell-no-authored-landmass", output.ToString());
    }

    [Fact]
    public void Host_RendersNewFrameAndRefusesOverwrite()
    {
        string outputPath = Path.Combine(Path.GetTempPath(), $"evermore-3d-{Guid.NewGuid():N}.ppm");
        try
        {
            var firstOutput = new StringWriter();
            var firstError = new StringWriter();
            int firstExit = EnvironmentHostApp.Invoke(RenderArguments(outputPath), firstOutput, firstError);

            Assert.Equal(EnvironmentHostApp.SuccessExitCode, firstExit);
            Assert.Equal(string.Empty, firstError.ToString());
            Assert.True(File.Exists(outputPath));
            byte[] expectedHeader = Encoding.ASCII.GetBytes("P6\n128 96\n255\n");
            Assert.Equal(
                expectedHeader,
                File.ReadAllBytes(outputPath).Take(expectedHeader.Length).ToArray());

            byte[] original = File.ReadAllBytes(outputPath);
            var secondError = new StringWriter();
            int secondExit = EnvironmentHostApp.Invoke(RenderArguments(outputPath), TextWriter.Null, secondError);

            Assert.Equal(EnvironmentHostApp.IoFailureExitCode, secondExit);
            Assert.Contains("Output already exists", secondError.ToString());
            Assert.Equal(original, File.ReadAllBytes(outputPath));
        }
        finally
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }
    }

    [Fact]
    public void Host_RejectsUndocumentedOptions()
    {
        string outputPath = Path.Combine(Path.GetTempPath(), $"evermore-3d-{Guid.NewGuid():N}.ppm");
        string[] arguments = [.. RenderArguments(outputPath), "--unbounded", "true"];
        var error = new StringWriter();

        int exitCode = EnvironmentHostApp.Invoke(arguments, TextWriter.Null, error);

        Assert.Equal(EnvironmentHostApp.InputFailureExitCode, exitCode);
        Assert.Contains("documented seven options", error.ToString());
        Assert.False(File.Exists(outputPath));
    }

    private static AzerothOrbitalSceneSnapshot CreateScene(int azimuth, int elevation, int zoom) =>
        new AzerothOrbitalSceneEngine().Process(
            new AzerothOrbitalSceneInput(
                LoadEnvironmentalInput(),
                new AzerothOrbitalCameraInput(azimuth, elevation, zoom)));

    private static AzerothEnvironmentalSimulationInput LoadEnvironmentalInput() =>
        JsonSerializer.Deserialize(
            File.ReadAllText(EnvironmentalFixturePath),
            AzerothSimulationJsonContext.Default.AzerothEnvironmentalSimulationInput)
        ?? throw new JsonException("The Azeroth environmental field fixture was null.");

    private static string[] RenderArguments(string outputPath) =>
    [
        "render",
        "--input", EnvironmentalFixturePath,
        "--output", outputPath,
        "--width", "128",
        "--height", "96",
        "--azimuth-micros", "125000",
        "--elevation-micros", "50000",
        "--zoom", "2"
    ];

    private static string EnvironmentalFixturePath =>
        Path.Combine(AppContext.BaseDirectory, "CliFixtures", "valid-azeroth-environmental-field.json");
}
