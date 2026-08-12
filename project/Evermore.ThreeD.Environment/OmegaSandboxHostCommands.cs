using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Evermore.ThreeD.Environment;

internal static class OmegaSandboxHostCommands
{
    private static readonly string[] CreateOptionNames =
    [
        "--seed",
        "--generations",
        "--systems",
        "--worlds",
        "--moons",
        "--output"
    ];

    private static readonly string[] RenderOptionNames =
    [
        "--input",
        "--output",
        "--width",
        "--height",
        "--axis-x",
        "--axis-y",
        "--axis-depth"
    ];

    private static readonly string[] AdvanceOptionNames =
    [
        "--input",
        "--ticks",
        "--output"
    ];

    public static int Create(ReadOnlySpan<string> args, TextWriter output, TextWriter error)
    {
        try
        {
            Dictionary<string, string> options = ParseExactOptions(args, CreateOptionNames);
            string outputPath = Require(options, "--output");
            if (File.Exists(outputPath))
                throw new IOException($"Output already exists: {outputPath}");
            var input = new OmegaSandboxInput(
                ParseUInt64(options, "--seed"),
                ParseInt32(options, "--generations"),
                ParseInt32(options, "--systems"),
                ParseInt32(options, "--worlds"),
                ParseInt32(options, "--moons"));
            OmegaSandboxSnapshot snapshot = new OmegaSandboxEngine().Process(input);
            if (snapshot.Disposition == OmegaSandboxDisposition.Contained)
            {
                error.WriteLine($"OMEGA_SANDBOX_CONTAINMENT: {snapshot.ContainmentReason}");
                return EnvironmentHostApp.SandboxContainmentExitCode;
            }

            byte[] json = Encoding.UTF8.GetBytes(OmegaSandboxSerializer.Serialize(snapshot));
            if (json.Length > OmegaSandboxSerializer.MaximumSnapshotBytes)
                throw new ArgumentOutOfRangeException(nameof(args), "The serialized Omega Sandbox exceeds the bounded snapshot size.");
            WriteNewFile(outputPath, json);
            output.WriteLine("environment=evermore-3d");
            output.WriteLine("sandbox=omega-multiverse");
            output.WriteLine($"authority={snapshot.Authority}");
            output.WriteLine($"classification={snapshot.Classification}");
            output.WriteLine($"dimensions={snapshot.DimensionNames.Length}");
            output.WriteLine($"simulation_tick={snapshot.SimulationTick}");
            output.WriteLine($"systems={snapshot.SystemCount}");
            output.WriteLine($"hearthstars={snapshot.SystemCount}");
            output.WriteLine($"worlds={snapshot.WorldCount}");
            output.WriteLine($"moons={snapshot.MoonCount}");
            output.WriteLine($"bodies={snapshot.TotalBodyCount}");
            output.WriteLine($"output={Path.GetFullPath(outputPath)}");
            output.WriteLine($"coordinate_meaning={snapshot.CoordinateMeaning}");
            output.WriteLine($"physical_scale={snapshot.PhysicalScale}");
            return EnvironmentHostApp.SuccessExitCode;
        }
        catch (IOException ex)
        {
            error.WriteLine($"IO_FAILURE: {ex.Message}");
            return EnvironmentHostApp.IoFailureExitCode;
        }
        catch (UnauthorizedAccessException ex)
        {
            error.WriteLine($"IO_FAILURE: {ex.Message}");
            return EnvironmentHostApp.IoFailureExitCode;
        }
        catch (JsonException ex)
        {
            error.WriteLine($"OMEGA_SANDBOX_INPUT_FAILURE: {ex.Message}");
            return EnvironmentHostApp.InputFailureExitCode;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidDataException or InvalidOperationException)
        {
            error.WriteLine($"OMEGA_SANDBOX_INPUT_FAILURE: {ex.Message}");
            return EnvironmentHostApp.InputFailureExitCode;
        }
        catch (Exception ex)
        {
            error.WriteLine($"INTERNAL_FAILURE: {ex.Message}");
            return EnvironmentHostApp.InternalFailureExitCode;
        }
    }

    public static int Advance(ReadOnlySpan<string> args, TextWriter output, TextWriter error)
    {
        try
        {
            Dictionary<string, string> options = ParseExactOptions(args, AdvanceOptionNames);
            string inputPath = Require(options, "--input");
            string outputPath = Require(options, "--output");
            int ticks = ParseInt32(options, "--ticks");
            if (File.Exists(outputPath))
                throw new IOException($"Output already exists: {outputPath}");

            OmegaSandboxSnapshot snapshot = ReadVerifiedSnapshot(inputPath, args);
            if (snapshot.Disposition == OmegaSandboxDisposition.Contained)
            {
                error.WriteLine($"OMEGA_SANDBOX_CONTAINMENT: {snapshot.ContainmentReason}");
                return EnvironmentHostApp.SandboxContainmentExitCode;
            }

            OmegaSandboxSnapshot advanced = new OmegaSandboxEngine().Advance(snapshot, ticks);
            if (advanced.Disposition == OmegaSandboxDisposition.Contained)
            {
                error.WriteLine($"OMEGA_SANDBOX_CONTAINMENT: {advanced.ContainmentReason}");
                return EnvironmentHostApp.SandboxContainmentExitCode;
            }

            byte[] json = Encoding.UTF8.GetBytes(OmegaSandboxSerializer.Serialize(advanced));
            if (json.Length > OmegaSandboxSerializer.MaximumSnapshotBytes)
                throw new ArgumentOutOfRangeException(nameof(args), "The advanced Omega Sandbox exceeds the bounded snapshot size.");
            WriteNewFile(outputPath, json);
            output.WriteLine("environment=evermore-3d");
            output.WriteLine("sandbox=omega-multiverse");
            output.WriteLine($"authority={advanced.Authority}");
            output.WriteLine($"previous_tick={snapshot.SimulationTick}");
            output.WriteLine($"advanced_ticks={ticks}");
            output.WriteLine($"simulation_tick={advanced.SimulationTick}");
            output.WriteLine($"dimensions={advanced.DimensionNames.Length}");
            output.WriteLine($"bodies={advanced.TotalBodyCount}");
            output.WriteLine($"output={Path.GetFullPath(outputPath)}");
            return EnvironmentHostApp.SuccessExitCode;
        }
        catch (IOException ex)
        {
            error.WriteLine($"IO_FAILURE: {ex.Message}");
            return EnvironmentHostApp.IoFailureExitCode;
        }
        catch (UnauthorizedAccessException ex)
        {
            error.WriteLine($"IO_FAILURE: {ex.Message}");
            return EnvironmentHostApp.IoFailureExitCode;
        }
        catch (JsonException ex)
        {
            error.WriteLine($"OMEGA_SANDBOX_INPUT_FAILURE: {ex.Message}");
            return EnvironmentHostApp.InputFailureExitCode;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidDataException or InvalidOperationException)
        {
            error.WriteLine($"OMEGA_SANDBOX_INPUT_FAILURE: {ex.Message}");
            return EnvironmentHostApp.InputFailureExitCode;
        }
        catch (Exception ex)
        {
            error.WriteLine($"INTERNAL_FAILURE: {ex.Message}");
            return EnvironmentHostApp.InternalFailureExitCode;
        }
    }

    public static int Render(ReadOnlySpan<string> args, TextWriter output, TextWriter error)
    {
        try
        {
            Dictionary<string, string> options = ParseExactOptions(args, RenderOptionNames);
            string inputPath = Require(options, "--input");
            string outputPath = Require(options, "--output");
            int width = ParseInt32(options, "--width");
            int height = ParseInt32(options, "--height");
            int axisX = ParseInt32(options, "--axis-x");
            int axisY = ParseInt32(options, "--axis-y");
            int axisDepth = ParseInt32(options, "--axis-depth");
            AzerothSoftwareRenderer.ValidateDimensions(width, height);
            OmegaSandboxSoftwareRenderer.ValidateAxes(axisX, axisY, axisDepth);
            if (File.Exists(outputPath))
                throw new IOException($"Output already exists: {outputPath}");

            OmegaSandboxSnapshot snapshot = ReadVerifiedSnapshot(inputPath, args);
            if (snapshot.Disposition == OmegaSandboxDisposition.Contained)
            {
                error.WriteLine($"OMEGA_SANDBOX_CONTAINMENT: {snapshot.ContainmentReason}");
                return EnvironmentHostApp.SandboxContainmentExitCode;
            }

            byte[] framebuffer = new OmegaSandboxSoftwareRenderer().Render(
                snapshot,
                width,
                height,
                axisX,
                axisY,
                axisDepth);
            WriteNewFile(outputPath, framebuffer);
            output.WriteLine("environment=evermore-3d");
            output.WriteLine("sandbox=omega-multiverse");
            output.WriteLine($"renderer={OmegaSandboxSoftwareRenderer.Format}");
            output.WriteLine($"projection_authority={OmegaSandboxSoftwareRenderer.ProjectionAuthority}");
            output.WriteLine($"frame={width}x{height}");
            output.WriteLine($"projection_axes={axisX},{axisY},{axisDepth}");
            output.WriteLine($"simulation_tick={snapshot.SimulationTick}");
            output.WriteLine($"bodies={snapshot.TotalBodyCount}");
            output.WriteLine($"output={Path.GetFullPath(outputPath)}");
            return EnvironmentHostApp.SuccessExitCode;
        }
        catch (IOException ex)
        {
            error.WriteLine($"IO_FAILURE: {ex.Message}");
            return EnvironmentHostApp.IoFailureExitCode;
        }
        catch (UnauthorizedAccessException ex)
        {
            error.WriteLine($"IO_FAILURE: {ex.Message}");
            return EnvironmentHostApp.IoFailureExitCode;
        }
        catch (JsonException ex)
        {
            error.WriteLine($"OMEGA_SANDBOX_INPUT_FAILURE: {ex.Message}");
            return EnvironmentHostApp.InputFailureExitCode;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidDataException or InvalidOperationException)
        {
            error.WriteLine($"OMEGA_SANDBOX_INPUT_FAILURE: {ex.Message}");
            return EnvironmentHostApp.InputFailureExitCode;
        }
        catch (Exception ex)
        {
            error.WriteLine($"INTERNAL_FAILURE: {ex.Message}");
            return EnvironmentHostApp.InternalFailureExitCode;
        }
    }

    private static Dictionary<string, string> ParseExactOptions(
        ReadOnlySpan<string> args,
        IReadOnlyList<string> expectedNames)
    {
        if (args.Length % 2 != 0)
            throw new ArgumentException("Every Omega Sandbox option requires exactly one value.", nameof(args));
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int index = 0; index < args.Length; index += 2)
        {
            string name = args[index];
            if (!name.StartsWith("--", StringComparison.Ordinal) || !options.TryAdd(name, args[index + 1]))
                throw new ArgumentException($"Invalid or duplicate Omega Sandbox option: {name}.", nameof(args));
        }
        if (options.Count != expectedNames.Count || expectedNames.Any(name => !options.ContainsKey(name)))
            throw new ArgumentException("Omega Sandbox commands accept only their documented options.", nameof(args));
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

    private static ulong ParseUInt64(IReadOnlyDictionary<string, string> options, string name)
    {
        string value = Require(options, name);
        if (!ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out ulong parsed))
            throw new ArgumentException($"Option {name} must be an unsigned 64-bit integer.", nameof(options));
        return parsed;
    }

    private static OmegaSandboxSnapshot ReadVerifiedSnapshot(
        string inputPath,
        ReadOnlySpan<string> args)
    {
        var inputFile = new FileInfo(inputPath);
        inputFile.Refresh();
        if (inputFile.Length > OmegaSandboxSerializer.MaximumSnapshotBytes)
            throw new ArgumentOutOfRangeException(nameof(args), "The Omega Sandbox snapshot exceeds the bounded input size.");
        return OmegaSandboxSerializer.DeserializeAndVerify(File.ReadAllText(inputFile.FullName));
    }

    private static void WriteNewFile(string path, ReadOnlySpan<byte> contents)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.Write(contents);
    }
}
