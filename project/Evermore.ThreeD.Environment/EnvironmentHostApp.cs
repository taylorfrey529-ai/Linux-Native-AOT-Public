using System.Globalization;
using System.Text.Json;
using Evermore.Azeroth.Simulation;

namespace Evermore.ThreeD.Environment;

public static class EnvironmentHostApp
{
    private static readonly string[] RenderOptionNames =
    [
        "--input",
        "--output",
        "--width",
        "--height",
        "--azimuth-micros",
        "--elevation-micros",
        "--zoom"
    ];

    public const int SuccessExitCode = 0;
    public const int InputFailureExitCode = 2;
    public const int IoFailureExitCode = 3;
    public const int ContainmentExitCode = 10;
    public const int SandboxContainmentExitCode = 11;
    public const int InternalFailureExitCode = 70;

    public static int Invoke(string[] args, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (args.Length == 1 && StringComparer.Ordinal.Equals(args[0], "status"))
        {
            WriteStatus(output);
            return SuccessExitCode;
        }
        if (args.Length > 0 && StringComparer.Ordinal.Equals(args[0], "sandbox-create"))
            return OmegaSandboxHostCommands.Create(args.AsSpan(1), output, error);
        if (args.Length > 0 && StringComparer.Ordinal.Equals(args[0], "sandbox-advance"))
            return OmegaSandboxHostCommands.Advance(args.AsSpan(1), output, error);
        if (args.Length > 0 && StringComparer.Ordinal.Equals(args[0], "sandbox-render"))
            return OmegaSandboxHostCommands.Render(args.AsSpan(1), output, error);
        if (args.Length == 0 || !StringComparer.Ordinal.Equals(args[0], "render"))
        {
            error.WriteLine("USAGE: evermore-3d status | render ... | sandbox-create ... | sandbox-advance ... | sandbox-render ...");
            return InputFailureExitCode;
        }

        try
        {
            Dictionary<string, string> options = ParseOptions(args.AsSpan(1));
            if (options.Count != RenderOptionNames.Length ||
                RenderOptionNames.Any(name => !options.ContainsKey(name)))
                throw new ArgumentException("Render accepts only the documented seven options.", nameof(args));
            string inputPath = Require(options, "--input");
            string outputPath = Require(options, "--output");
            int width = ParseInt32(options, "--width");
            int height = ParseInt32(options, "--height");
            int azimuth = ParseInt32(options, "--azimuth-micros");
            int elevation = ParseInt32(options, "--elevation-micros");
            int zoom = ParseInt32(options, "--zoom");
            AzerothSoftwareRenderer.ValidateDimensions(width, height);
            if (File.Exists(outputPath))
                throw new IOException($"Output already exists: {outputPath}");

            var inputFile = new FileInfo(inputPath);
            inputFile.Refresh();
            if (inputFile.Length > AzerothSimulationBounds.MaximumCliInputBytes)
                throw new ArgumentOutOfRangeException(nameof(args), "The simulation input exceeds the bounded file size.");
            AzerothEnvironmentalSimulationInput environment = JsonSerializer.Deserialize(
                File.ReadAllText(inputFile.FullName),
                AzerothSimulationJsonContext.Default.AzerothEnvironmentalSimulationInput)
                ?? throw new JsonException("The Azeroth environmental input was null.");
            var orbitalInput = new AzerothOrbitalSceneInput(
                environment,
                new AzerothOrbitalCameraInput(azimuth, elevation, zoom));
            AzerothOrbitalSceneSnapshot scene = new AzerothOrbitalSceneEngine().Process(orbitalInput);
            if (scene.Disposition == AzerothSimulationDisposition.Contained)
            {
                error.WriteLine($"AZEROTH_CONTAINMENT: {string.Join(" | ", scene.ContainmentReasons)}");
                return ContainmentExitCode;
            }

            byte[] framebuffer = new AzerothSoftwareRenderer().Render(scene, width, height);
            using (var stream = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                stream.Write(framebuffer);

            output.WriteLine("environment=evermore-3d");
            output.WriteLine($"classification={scene.Classification}");
            output.WriteLine($"authority={AzerothOrbitalSceneSerializer.Authority}");
            output.WriteLine($"generation={scene.Generation}");
            output.WriteLine($"renderer={AzerothSoftwareRenderer.Format}");
            output.WriteLine($"frame={width}x{height}");
            output.WriteLine($"output={Path.GetFullPath(outputPath)}");
            output.WriteLine($"normalized_ratio={scene.Scale.LinearRatio}");
            output.WriteLine($"physical_scale={scene.Scale.PhysicalScale}");
            output.WriteLine($"zone_placement={AzerothOrbitalSceneEngine.MissingGlobePlacement}");
            return SuccessExitCode;
        }
        catch (IOException ex)
        {
            error.WriteLine($"IO_FAILURE: {ex.Message}");
            return IoFailureExitCode;
        }
        catch (UnauthorizedAccessException ex)
        {
            error.WriteLine($"IO_FAILURE: {ex.Message}");
            return IoFailureExitCode;
        }
        catch (JsonException ex)
        {
            error.WriteLine($"INPUT_FAILURE: {ex.Message}");
            return InputFailureExitCode;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidDataException or InvalidOperationException)
        {
            error.WriteLine($"INPUT_FAILURE: {ex.Message}");
            return InputFailureExitCode;
        }
        catch (Exception ex)
        {
            error.WriteLine($"INTERNAL_FAILURE: {ex.Message}");
            return InternalFailureExitCode;
        }
    }

    private static void WriteStatus(TextWriter output)
    {
        output.WriteLine("product=€V€RMØRE 3D Environment");
        output.WriteLine("version=0.3.0");
        output.WriteLine("target=net10.0/linux-x64");
        output.WriteLine("host=self-contained-native-aot");
        output.WriteLine($"renderer={AzerothSoftwareRenderer.Format}");
        output.WriteLine($"visual_authority={AzerothSoftwareRenderer.VisualAuthority}");
        output.WriteLine("geometry=generic-globe-shell-no-authored-landmass");
        output.WriteLine("sandbox=omega-multiverse");
        output.WriteLine($"sandbox_authority={OmegaSandboxEngine.Authority}");
        output.WriteLine($"sandbox_dimensions={OmegaSandboxDimensions.Count}");
        output.WriteLine($"sandbox_max_systems={OmegaSandboxEngine.MaximumSystems}");
        output.WriteLine($"sandbox_max_hearthstars={OmegaSandboxEngine.MaximumSystems}");
        output.WriteLine($"sandbox_max_bodies={OmegaSandboxEngine.MaximumBodies}");
        output.WriteLine($"sandbox_max_tick={OmegaSandboxEngine.MaximumSimulationTick}");
        output.WriteLine($"sandbox_max_advance_ticks={OmegaSandboxEngine.MaximumAdvanceTicks}");
    }

    private static Dictionary<string, string> ParseOptions(ReadOnlySpan<string> args)
    {
        if (args.Length % 2 != 0)
            throw new ArgumentException("Every render option requires exactly one value.", nameof(args));
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int index = 0; index < args.Length; index += 2)
        {
            string name = args[index];
            if (!name.StartsWith("--", StringComparison.Ordinal) || !options.TryAdd(name, args[index + 1]))
                throw new ArgumentException($"Invalid or duplicate render option: {name}.", nameof(args));
        }
        return options;
    }

    private static string Require(IReadOnlyDictionary<string, string> options, string name)
    {
        if (!options.TryGetValue(name, out string? value) || string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"Missing required option: {name}.", nameof(options));
        return value;
    }

    private static int ParseInt32(IReadOnlyDictionary<string, string> options, string name)
    {
        string value = Require(options, name);
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            throw new ArgumentException($"Option {name} must be a 32-bit integer.", nameof(options));
        return parsed;
    }
}
