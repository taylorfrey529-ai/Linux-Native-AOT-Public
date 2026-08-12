using System.CommandLine;
using System.Globalization;
using System.Text.Json;
using EasternKingdoms.Simulation;
using Evermore.Lunara.Core;
using Evermore.Solune.Core;
using Omega.Recursive;

namespace Evermore.Cli;

public static partial class EvermoreCliApp
{
    public const int SuccessExitCode = 0;
    public const int SimulationInputFailureExitCode = 2;
    public const int OmegaInputFailureExitCode = SimulationInputFailureExitCode;
    public const int LunaraInputFailureExitCode = SimulationInputFailureExitCode;
    public const int SoluneInputFailureExitCode = SimulationInputFailureExitCode;
    public const int InputReadFailureExitCode = 3;
    public const int VerificationFailureExitCode = 4;
    public const int OmegaContainmentExitCode = 7;
    public const int LunaraContainmentExitCode = 8;
    public const int SoluneContainmentExitCode = 9;
    public const int InternalFailureExitCode = 70;

    public static async Task<int> InvokeAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        var configuration = new InvocationConfiguration
        {
            Output = output,
            Error = error,
            EnableDefaultExceptionHandler = false
        };

        return await BuildRootCommand(output, error)
            .Parse(args)
            .InvokeAsync(configuration, cancellationToken)
            .ConfigureAwait(false);
    }

    private static RootCommand BuildRootCommand(TextWriter output, TextWriter error)
    {
        var root = new RootCommand("Native AOT host for verified €V€RMØRE history, Azeroth simulation, and the deterministic MMORPG core.");
        root.Subcommands.Add(BuildStatusCommand(output));

        var verify = new Command("verify", "Verify existing TestHistoryOnly payloads without mutating simulation state.");
        verify.Subcommands.Add(BuildReplayVerificationCommand(output, error));
        verify.Subcommands.Add(BuildCameraVerificationCommand(output, error));
        verify.Subcommands.Add(BuildSceneVerificationCommand(output, error));
        root.Subcommands.Add(verify);
        root.Subcommands.Add(BuildLunaraCommand(output, error));
        root.Subcommands.Add(BuildSoluneCommand(output, error));
        root.Subcommands.Add(BuildOmegaCommand(output, error));
        root.Subcommands.Add(BuildAzerothCommand(output, error));
        root.Subcommands.Add(BuildMmoCommand(output));
        return root;
    }

    private static Command BuildStatusCommand(TextWriter output)
    {
        var format = CreateFormatOption();
        var command = new Command("status", "Report the CLI contract and its certification boundary.");
        command.Options.Add(format);
        command.SetAction(parseResult =>
        {
            var result = new CliStatusOutput(
                "€V€RMØRE / Eastern Kingdoms Simulation Evolution",
                "XXIII",
                "0.10.0",
                "net10.0",
                "linux-x64",
                "command-scoped",
                "historical-verification+isolated-components+negotiated-four-region-azeroth+mmorpg-session-economy-and-group-kernels",
                "separate-evidence-required");

            WriteStatus(output, parseResult.GetValue(format) ?? "text", result);
            return SuccessExitCode;
        });
        return command;
    }

    private static Command BuildLunaraCommand(TextWriter output, TextWriter error)
    {
        var input = CreateInputOption();
        var format = CreateFormatOption();
        var run = new Command("run", "Run a bounded, deterministic, non-canon Lunara moon/tide input.");
        run.Options.Add(input);
        run.Options.Add(format);
        run.SetAction(parseResult => RunLunara(
            output,
            error,
            parseResult.GetRequiredValue(input),
            parseResult.GetValue(format) ?? "text"));

        var lunara = new Command("lunara", "Operate the isolated Lunara engine without accessing Eastern Kingdoms state.");
        lunara.Subcommands.Add(run);
        return lunara;
    }

    private static Command BuildSoluneCommand(TextWriter output, TextWriter error)
    {
        var input = CreateInputOption();
        var format = CreateFormatOption();
        var run = new Command("run", "Evaluate a bounded, deterministic, non-canon Solune local-sun model.");
        run.Options.Add(input);
        run.Options.Add(format);
        run.SetAction(parseResult => RunSolune(
            output,
            error,
            parseResult.GetRequiredValue(input),
            parseResult.GetValue(format) ?? "text"));

        var solune = new Command("solune", "Operate the isolated Solune feasibility engine without accessing Eastern Kingdoms state.");
        solune.Subcommands.Add(run);
        return solune;
    }

    private static Command BuildOmegaCommand(TextWriter output, TextWriter error)
    {
        var generations = new Option<int>("--generations")
        {
            Description = "Finite number of generations to process (1..10000).",
            Required = true
        };
        var seed = new Option<ulong>("--seed")
        {
            Description = "Deterministic unsigned 64-bit simulation seed.",
            Required = true
        };
        var format = CreateFormatOption();
        var run = new Command("run", "Run the isolated, non-canon Omega Recursive simulation.");
        run.Options.Add(generations);
        run.Options.Add(seed);
        run.Options.Add(format);
        run.SetAction(parseResult => RunOmega(
            output,
            error,
            parseResult.GetRequiredValue(generations),
            parseResult.GetRequiredValue(seed),
            parseResult.GetValue(format) ?? "text"));

        var omega = new Command("omega", "Operate the isolated Omega Recursive engine without accessing Eastern Kingdoms state.");
        omega.Subcommands.Add(run);
        return omega;
    }

    private static Command BuildReplayVerificationCommand(TextWriter output, TextWriter error)
    {
        var input = CreateInputOption();
        var format = CreateFormatOption();
        var command = new Command("replay", "Verify a Phase XXII historical replay file.");
        command.Options.Add(input);
        command.Options.Add(format);
        command.SetAction(parseResult => RunVerification(error, () =>
        {
            FileInfo file = parseResult.GetRequiredValue(input);
            var verified = new HistoricalReplaySerializer(new HistoricalTimelineIndexer())
                .DeserializeAndVerify(File.ReadAllText(file.FullName));
            int[] generations = verified.Payload.Frames.Select(frame => frame.Generation).ToArray();
            var result = new ReplayVerificationOutput(
                "replay",
                file.FullName,
                verified.SchemaVersion,
                verified.Authority.ToString(),
                verified.PayloadSha256,
                verified.Payload.Frames.Count,
                generations.Min(),
                generations.Max(),
                verified.Payload.Frames.SelectMany(frame => frame.Cells).Select(cell => cell.ZoneId).Distinct(StringComparer.Ordinal).Count());
            WriteReplay(output, parseResult.GetValue(format) ?? "text", result);
            return SuccessExitCode;
        }));
        return command;
    }

    private static Command BuildCameraVerificationCommand(TextWriter output, TextWriter error)
    {
        var input = CreateInputOption();
        var zones = CreateZonesOption();
        var format = CreateFormatOption();
        var command = new Command("camera", "Verify a Phase XXIII camera file against an explicit authorized-zone set.");
        command.Options.Add(input);
        command.Options.Add(zones);
        command.Options.Add(format);
        command.SetAction(parseResult => RunVerification(error, () =>
        {
            FileInfo file = parseResult.GetRequiredValue(input);
            string[] authorizedZones = NormalizeZones(parseResult.GetRequiredValue(zones));
            var verified = new HistoricalAtlasCameraSerializer(authorizedZones)
                .DeserializeAndVerify(File.ReadAllText(file.FullName));
            var result = new CameraVerificationOutput(
                "camera",
                file.FullName,
                verified.SchemaVersion,
                verified.Authority.ToString(),
                verified.PayloadSha256,
                verified.Camera.Projection.ToString(),
                verified.Camera.Zoom,
                verified.Camera.FocusZoneId);
            WriteCamera(output, parseResult.GetValue(format) ?? "text", result);
            return SuccessExitCode;
        }));
        return command;
    }

    private static Command BuildSceneVerificationCommand(TextWriter output, TextWriter error)
    {
        var input = CreateInputOption();
        var zones = CreateZonesOption();
        var format = CreateFormatOption();
        var command = new Command("scene", "Verify a renderer-neutral Phase XXIII atlas scene against explicit authorized zones.");
        command.Options.Add(input);
        command.Options.Add(zones);
        command.Options.Add(format);
        command.SetAction(parseResult => RunVerification(error, () =>
        {
            FileInfo file = parseResult.GetRequiredValue(input);
            string[] authorizedZones = NormalizeZones(parseResult.GetRequiredValue(zones));
            var scenes = new HistoricalAtlasSceneBuilder(authorizedZones);
            var verified = new HistoricalAtlasSceneSerializer(scenes)
                .DeserializeAndVerify(File.ReadAllText(file.FullName));
            var result = new SceneVerificationOutput(
                "scene",
                file.FullName,
                verified.SchemaVersion,
                verified.Authority.ToString(),
                verified.PayloadSha256,
                verified.Scene.FrameIndex,
                verified.Scene.Generation,
                verified.Scene.Cells.Count,
                verified.Scene.LineageRanges.Count);
            WriteScene(output, parseResult.GetValue(format) ?? "text", result);
            return SuccessExitCode;
        }));
        return command;
    }

    private static Option<FileInfo> CreateInputOption()
    {
        var option = new Option<FileInfo>("--input")
        {
            Description = "Path to the JSON payload file.",
            Required = true
        };
        option.AcceptExistingOnly();
        return option;
    }

    private static Option<string[]> CreateZonesOption() => new("--zone")
    {
        Description = "Authorized Eastern Kingdoms zone ID. Repeat the option for multiple zones.",
        Required = true,
        Arity = ArgumentArity.OneOrMore,
        AllowMultipleArgumentsPerToken = true
    };

    private static Option<string> CreateFormatOption()
    {
        var option = new Option<string>("--format")
        {
            Description = "Output format: text or json.",
            DefaultValueFactory = _ => "text"
        };
        option.AcceptOnlyFromAmong("text", "json");
        return option;
    }

    private static string[] NormalizeZones(IEnumerable<string> zoneIds)
    {
        string[] zones = zoneIds.ToArray();
        if (zones.Length == 0 || zones.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("At least one nonblank authorized zone ID is required.", nameof(zoneIds));
        return zones.Distinct(StringComparer.Ordinal).OrderBy(zone => zone, StringComparer.Ordinal).ToArray();
    }

    private static int RunVerification(TextWriter error, Func<int> action)
    {
        try
        {
            return action();
        }
        catch (IOException ex)
        {
            error.WriteLine($"INPUT_READ_FAILURE: {ex.Message}");
            return InputReadFailureExitCode;
        }
        catch (UnauthorizedAccessException ex)
        {
            error.WriteLine($"INPUT_READ_FAILURE: {ex.Message}");
            return InputReadFailureExitCode;
        }
        catch (JsonException ex)
        {
            error.WriteLine($"VERIFICATION_FAILURE: {ex.Message}");
            return VerificationFailureExitCode;
        }
        catch (ArgumentException ex)
        {
            error.WriteLine($"VERIFICATION_FAILURE: {ex.Message}");
            return VerificationFailureExitCode;
        }
        catch (InvalidOperationException ex)
        {
            error.WriteLine($"VERIFICATION_FAILURE: {ex.Message}");
            return VerificationFailureExitCode;
        }
        catch (Exception ex)
        {
            error.WriteLine($"INTERNAL_FAILURE: {ex.Message}");
            return InternalFailureExitCode;
        }
    }

    private static int RunOmega(TextWriter output, TextWriter error, int generations, ulong seed, string format)
    {
        try
        {
            OmegaRunResult result = new OmegaEngine().Run(new OmegaSimulationOptions(seed, generations));
            OmegaGenerationSnapshot final = result.Snapshots[^1];
            var cliOutput = new OmegaCliOutput(
                "omega-recursive",
                OmegaEngine.Definition,
                result.Disposition.ToString().ToLowerInvariant(),
                result.Seed,
                result.RequestedGenerations,
                result.ProcessedGenerations,
                result.FinalPopulation.Count,
                final.AverageFitness,
                final.DominantTrait,
                result.Snapshots.Count(snapshot => snapshot.IsArchiveCheckpoint),
                result.ResonanceLog.Count,
                result.ContainmentReason);
            WriteOmega(output, format, cliOutput);

            if (result.Disposition == OmegaRunDisposition.Contained)
            {
                error.WriteLine($"OMEGA_CONTAINMENT: {result.ContainmentReason}");
                return OmegaContainmentExitCode;
            }

            return SuccessExitCode;
        }
        catch (ArgumentException ex)
        {
            error.WriteLine($"OMEGA_INPUT_FAILURE: {ex.Message}");
            return OmegaInputFailureExitCode;
        }
        catch (Exception ex)
        {
            error.WriteLine($"INTERNAL_FAILURE: {ex.Message}");
            return InternalFailureExitCode;
        }
    }

    private static int RunLunara(TextWriter output, TextWriter error, FileInfo input, string format)
    {
        try
        {
            input.Refresh();
            if (input.Length > LunaraBounds.MaximumCliInputBytes)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(input),
                    $"Lunara input cannot exceed {LunaraBounds.MaximumCliInputBytes} bytes.");
            }
            MoonTideInput value = JsonSerializer.Deserialize(
                File.ReadAllText(input.FullName),
                LunaraJsonContext.Default.MoonTideInput)
                ?? throw new JsonException("The Lunara input was null.");
            MoonTideSnapshot snapshot = new LunaraEngine().Process(value);
            WriteLunara(output, format, snapshot);

            if (snapshot.Disposition == MoonTideDisposition.Contained)
            {
                error.WriteLine($"LUNARA_CONTAINMENT: {snapshot.ContainmentCode}: {snapshot.ContainmentReason}");
                return LunaraContainmentExitCode;
            }

            return SuccessExitCode;
        }
        catch (IOException ex)
        {
            error.WriteLine($"INPUT_READ_FAILURE: {ex.Message}");
            return InputReadFailureExitCode;
        }
        catch (UnauthorizedAccessException ex)
        {
            error.WriteLine($"INPUT_READ_FAILURE: {ex.Message}");
            return InputReadFailureExitCode;
        }
        catch (JsonException ex)
        {
            error.WriteLine($"LUNARA_INPUT_FAILURE: {ex.Message}");
            return LunaraInputFailureExitCode;
        }
        catch (ArgumentException ex)
        {
            error.WriteLine($"LUNARA_INPUT_FAILURE: {ex.Message}");
            return LunaraInputFailureExitCode;
        }
        catch (InvalidDataException ex)
        {
            error.WriteLine($"LUNARA_INPUT_FAILURE: {ex.Message}");
            return LunaraInputFailureExitCode;
        }
        catch (Exception ex)
        {
            error.WriteLine($"INTERNAL_FAILURE: {ex.Message}");
            return InternalFailureExitCode;
        }
    }

    private static int RunSolune(TextWriter output, TextWriter error, FileInfo input, string format)
    {
        try
        {
            input.Refresh();
            if (input.Length > SoluneBounds.MaximumCliInputBytes)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(input),
                    $"Solune input cannot exceed {SoluneBounds.MaximumCliInputBytes} bytes.");
            }
            SoluneInput value = JsonSerializer.Deserialize(
                File.ReadAllText(input.FullName),
                SoluneJsonContext.Default.SoluneInput)
                ?? throw new JsonException("The Solune input was null.");
            SoluneSnapshot snapshot = new SoluneEngine().Process(value);
            WriteSolune(output, format, snapshot);

            if (snapshot.Disposition == SoluneDisposition.Contained)
            {
                error.WriteLine($"SOLUNE_CONTAINMENT: {snapshot.ContainmentCode}: {snapshot.ContainmentReason}");
                return SoluneContainmentExitCode;
            }

            return SuccessExitCode;
        }
        catch (IOException ex)
        {
            error.WriteLine($"INPUT_READ_FAILURE: {ex.Message}");
            return InputReadFailureExitCode;
        }
        catch (UnauthorizedAccessException ex)
        {
            error.WriteLine($"INPUT_READ_FAILURE: {ex.Message}");
            return InputReadFailureExitCode;
        }
        catch (JsonException ex)
        {
            error.WriteLine($"SOLUNE_INPUT_FAILURE: {ex.Message}");
            return SoluneInputFailureExitCode;
        }
        catch (ArgumentException ex)
        {
            error.WriteLine($"SOLUNE_INPUT_FAILURE: {ex.Message}");
            return SoluneInputFailureExitCode;
        }
        catch (InvalidDataException ex)
        {
            error.WriteLine($"SOLUNE_INPUT_FAILURE: {ex.Message}");
            return SoluneInputFailureExitCode;
        }
        catch (Exception ex)
        {
            error.WriteLine($"INTERNAL_FAILURE: {ex.Message}");
            return InternalFailureExitCode;
        }
    }

    private static void WriteStatus(TextWriter output, string format, CliStatusOutput result)
    {
        if (StringComparer.Ordinal.Equals(format, "json"))
        {
            output.WriteLine(JsonSerializer.Serialize(result, EvermoreCliJsonContext.Default.CliStatusOutput));
            return;
        }

        output.WriteLine($"product={result.Product}");
        output.WriteLine($"phase={result.Phase}");
        output.WriteLine($"cli_version={result.CliVersion}");
        output.WriteLine($"target={result.TargetFramework}/{result.ValidationRid}");
        output.WriteLine($"authority={result.Authority}");
        output.WriteLine($"mode={result.Mode}");
        output.WriteLine($"native_aot_certification={result.NativeAotCertification}");
    }

    private static void WriteReplay(TextWriter output, string format, ReplayVerificationOutput result)
    {
        if (StringComparer.Ordinal.Equals(format, "json"))
        {
            output.WriteLine(JsonSerializer.Serialize(result, EvermoreCliJsonContext.Default.ReplayVerificationOutput));
            return;
        }

        WriteVerificationHeader(output, result.Kind, result.Path, result.SchemaVersion, result.Authority, result.PayloadSha256);
        output.WriteLine($"frames={result.FrameCount}");
        output.WriteLine($"generations={result.FirstGeneration}..{result.LastGeneration}");
        output.WriteLine($"zones={result.ZoneCount}");
    }

    private static void WriteCamera(TextWriter output, string format, CameraVerificationOutput result)
    {
        if (StringComparer.Ordinal.Equals(format, "json"))
        {
            output.WriteLine(JsonSerializer.Serialize(result, EvermoreCliJsonContext.Default.CameraVerificationOutput));
            return;
        }

        WriteVerificationHeader(output, result.Kind, result.Path, result.SchemaVersion, result.Authority, result.PayloadSha256);
        output.WriteLine($"projection={result.Projection}");
        output.WriteLine($"zoom={result.Zoom.ToString("R", CultureInfo.InvariantCulture)}");
        output.WriteLine($"focus_zone={result.FocusZoneId ?? "none"}");
    }

    private static void WriteScene(TextWriter output, string format, SceneVerificationOutput result)
    {
        if (StringComparer.Ordinal.Equals(format, "json"))
        {
            output.WriteLine(JsonSerializer.Serialize(result, EvermoreCliJsonContext.Default.SceneVerificationOutput));
            return;
        }

        WriteVerificationHeader(output, result.Kind, result.Path, result.SchemaVersion, result.Authority, result.PayloadSha256);
        output.WriteLine($"frame={result.FrameIndex}");
        output.WriteLine($"generation={result.Generation}");
        output.WriteLine($"zones={result.ZoneCount}");
        output.WriteLine($"lineage_ranges={result.LineageRangeCount}");
    }

    private static void WriteOmega(TextWriter output, string format, OmegaCliOutput result)
    {
        if (StringComparer.Ordinal.Equals(format, "json"))
        {
            output.WriteLine(JsonSerializer.Serialize(result, EvermoreCliJsonContext.Default.OmegaCliOutput));
            return;
        }

        output.WriteLine($"omega={result.Kind}");
        output.WriteLine($"definition={result.Definition}");
        output.WriteLine($"status={result.Status}");
        output.WriteLine($"seed={result.Seed}");
        output.WriteLine($"generations={result.ProcessedGenerations}/{result.RequestedGenerations}");
        output.WriteLine($"population={result.Population}");
        output.WriteLine($"average_fitness={result.AverageFitness.ToString("R", CultureInfo.InvariantCulture)}");
        output.WriteLine($"dominant_trait={result.DominantTrait}");
        output.WriteLine($"archive_checkpoints={result.ArchiveCheckpoints}");
        output.WriteLine($"resonance_events={result.ResonanceEvents}");
        output.WriteLine($"containment_reason={result.ContainmentReason ?? "none"}");
    }

    private static void WriteLunara(TextWriter output, string format, MoonTideSnapshot result)
    {
        if (StringComparer.Ordinal.Equals(format, "json"))
        {
            output.WriteLine(MoonTideSnapshotSerializer.Serialize(result));
            return;
        }

        output.WriteLine("lunara=moon-tide");
        output.WriteLine($"status={result.Disposition.ToString().ToLowerInvariant()}");
        output.WriteLine($"generation={result.Generation}");
        output.WriteLine($"seed={result.Seed}");
        output.WriteLine($"lunar_phase={result.LunarCycle.Phase}");
        output.WriteLine($"illumination={result.LunarCycle.IlluminationMicros}");
        output.WriteLine($"tide={result.Tide.LevelMicros}");
        output.WriteLine($"resonance={result.ResonanceMicros}");
        output.WriteLine($"relics={result.Relics.Count}");
        output.WriteLine($"manifestation_directives={result.ManifestationDirectives.Count}");
        output.WriteLine($"containment_code={result.ContainmentCode ?? "none"}");
    }

    private static void WriteSolune(TextWriter output, string format, SoluneSnapshot result)
    {
        if (StringComparer.Ordinal.Equals(format, "json"))
        {
            output.WriteLine(SoluneSnapshotSerializer.Serialize(result));
            return;
        }

        output.WriteLine("solune=local-sun-feasibility");
        output.WriteLine($"status={result.Disposition.ToString().ToLowerInvariant()}");
        output.WriteLine($"generation={result.Generation}");
        output.WriteLine($"construct={result.Model.ConstructId}");
        output.WriteLine($"evercycle_phase={result.Calendar.Phase}");
        output.WriteLine($"orbit_phase={result.Orbit.PhaseMicros}");
        output.WriteLine($"irradiance_mw_m2={result.Radiance.IrradianceMilliwattsPerSquareMeter}");
        output.WriteLine($"apparent_diameter_microdegrees={result.Radiance.CalculatedApparentDiameterMicrodegrees}");
        output.WriteLine($"operation_directives={result.OperationDirectives.Count}");
        output.WriteLine($"containment_code={result.ContainmentCode ?? "none"}");
    }

    private static void WriteVerificationHeader(
        TextWriter output,
        string kind,
        string path,
        string schemaVersion,
        string authority,
        string payloadSha256)
    {
        output.WriteLine("verification=PASS");
        output.WriteLine($"kind={kind}");
        output.WriteLine($"path={path}");
        output.WriteLine($"schema={schemaVersion}");
        output.WriteLine($"authority={authority}");
        output.WriteLine($"payload_sha256={payloadSha256}");
    }
}
