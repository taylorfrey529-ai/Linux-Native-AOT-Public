using System.CommandLine;
using System.Text.Json;
using EasternKingdoms.Simulation;
using Evermore.Azeroth.Simulation;

namespace Evermore.Cli;

public static partial class EvermoreCliApp
{
    public const int AzerothInputFailureExitCode = SimulationInputFailureExitCode;
    public const int AzerothContainmentExitCode = 10;

    private static Command BuildAzerothCommand(TextWriter output, TextWriter error)
    {
        var regionsFormat = CreateFormatOption();
        var regions = new Command("regions", "Report the exact owner-negotiated four-region Azeroth authority.");
        regions.Options.Add(regionsFormat);
        regions.SetAction(parseResult =>
        {
            WriteAzerothRegions(
                output,
                parseResult.GetValue(regionsFormat) ?? "text",
                AzerothRegionAuthority.CreateSnapshot());
            return SuccessExitCode;
        });

        var runInput = CreateInputOption();
        var runFormat = CreateFormatOption();
        var run = new Command("run", "Compose bounded Omega, Lunara, Solune, and authoritative Azeroth observations.");
        run.Options.Add(runInput);
        run.Options.Add(runFormat);
        run.SetAction(parseResult => RunAzeroth(
            output,
            error,
            parseResult.GetRequiredValue(runInput),
            parseResult.GetValue(runFormat) ?? "text"));

        var fieldInput = CreateInputOption();
        var fieldFormat = CreateFormatOption();
        var field = new Command("field", "Compose authored normalized atmosphere and mana signals over authorized Azeroth zones.");
        field.Options.Add(fieldInput);
        field.Options.Add(fieldFormat);
        field.SetAction(parseResult => RunAzerothField(
            output,
            error,
            parseResult.GetRequiredValue(fieldInput),
            parseResult.GetValue(fieldFormat) ?? "text"));

        var orbitInput = CreateInputOption();
        var orbitAzimuth = new Option<int>("--azimuth-micros")
        {
            Description = "Normalized renderer orbit azimuth (0..999999); not longitude.",
            Required = true
        };
        var orbitElevation = new Option<int>("--elevation-micros")
        {
            Description = "Normalized renderer orbit elevation (-250000..250000); not latitude.",
            Required = true
        };
        var orbitZoom = new Option<int>("--zoom")
        {
            Description = "Normalized orbital view zoom level (0..8).",
            Required = true
        };
        var orbitFormat = CreateFormatOption();
        var orbit = new Command("orbit", "Build a constrained renderer-neutral orbital scene from verified environmental signals.");
        orbit.Options.Add(orbitInput);
        orbit.Options.Add(orbitAzimuth);
        orbit.Options.Add(orbitElevation);
        orbit.Options.Add(orbitZoom);
        orbit.Options.Add(orbitFormat);
        orbit.SetAction(parseResult => RunAzerothOrbit(
            output,
            error,
            parseResult.GetRequiredValue(orbitInput),
            parseResult.GetRequiredValue(orbitAzimuth),
            parseResult.GetRequiredValue(orbitElevation),
            parseResult.GetRequiredValue(orbitZoom),
            parseResult.GetValue(orbitFormat) ?? "text"));

        var azeroth = new Command("azeroth", "Operate the TestHistoryOnly composed Azeroth simulation.");
        azeroth.Subcommands.Add(regions);
        azeroth.Subcommands.Add(run);
        azeroth.Subcommands.Add(field);
        azeroth.Subcommands.Add(orbit);
        return azeroth;
    }

    private static int RunAzeroth(TextWriter output, TextWriter error, FileInfo input, string format)
    {
        try
        {
            CheckAzerothInputSize(input);
            AzerothSimulationInput value = JsonSerializer.Deserialize(
                File.ReadAllText(input.FullName),
                AzerothSimulationJsonContext.Default.AzerothSimulationInput)
                ?? throw new JsonException("The Azeroth simulation input was null.");
            AzerothSimulationSnapshot snapshot = new AzerothSimulationEngine().Process(value);
            WriteAzeroth(output, format, snapshot);

            if (snapshot.Disposition == AzerothSimulationDisposition.Contained)
            {
                error.WriteLine($"AZEROTH_CONTAINMENT: {string.Join(" | ", snapshot.ContainmentReasons)}");
                return AzerothContainmentExitCode;
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
            error.WriteLine($"AZEROTH_INPUT_FAILURE: {ex.Message}");
            return AzerothInputFailureExitCode;
        }
        catch (ArgumentException ex)
        {
            error.WriteLine($"AZEROTH_INPUT_FAILURE: {ex.Message}");
            return AzerothInputFailureExitCode;
        }
        catch (InvalidDataException ex)
        {
            error.WriteLine($"AZEROTH_INPUT_FAILURE: {ex.Message}");
            return AzerothInputFailureExitCode;
        }
        catch (Exception ex)
        {
            error.WriteLine($"INTERNAL_FAILURE: {ex.Message}");
            return InternalFailureExitCode;
        }
    }

    private static int RunAzerothField(TextWriter output, TextWriter error, FileInfo input, string format)
    {
        try
        {
            CheckAzerothInputSize(input);
            AzerothEnvironmentalSimulationInput value = JsonSerializer.Deserialize(
                File.ReadAllText(input.FullName),
                AzerothSimulationJsonContext.Default.AzerothEnvironmentalSimulationInput)
                ?? throw new JsonException("The Azeroth environmental field input was null.");
            AzerothEnvironmentalSimulationSnapshot snapshot = new AzerothEnvironmentalFieldEngine().Process(value);
            WriteAzerothField(output, format, snapshot);

            if (snapshot.Environment.Disposition == AzerothSimulationDisposition.Contained)
            {
                error.WriteLine($"AZEROTH_CONTAINMENT: {string.Join(" | ", snapshot.Environment.ContainmentReasons)}");
                return AzerothContainmentExitCode;
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
            error.WriteLine($"AZEROTH_INPUT_FAILURE: {ex.Message}");
            return AzerothInputFailureExitCode;
        }
        catch (ArgumentException ex)
        {
            error.WriteLine($"AZEROTH_INPUT_FAILURE: {ex.Message}");
            return AzerothInputFailureExitCode;
        }
        catch (InvalidDataException ex)
        {
            error.WriteLine($"AZEROTH_INPUT_FAILURE: {ex.Message}");
            return AzerothInputFailureExitCode;
        }
        catch (Exception ex)
        {
            error.WriteLine($"INTERNAL_FAILURE: {ex.Message}");
            return InternalFailureExitCode;
        }
    }

    private static int RunAzerothOrbit(
        TextWriter output,
        TextWriter error,
        FileInfo input,
        int azimuthMicros,
        int elevationMicros,
        int zoom,
        string format)
    {
        try
        {
            CheckAzerothInputSize(input);
            AzerothEnvironmentalSimulationInput environment = JsonSerializer.Deserialize(
                File.ReadAllText(input.FullName),
                AzerothSimulationJsonContext.Default.AzerothEnvironmentalSimulationInput)
                ?? throw new JsonException("The Azeroth environmental field input was null.");
            var value = new AzerothOrbitalSceneInput(
                environment,
                new AzerothOrbitalCameraInput(azimuthMicros, elevationMicros, zoom));
            AzerothOrbitalSceneSnapshot snapshot = new AzerothOrbitalSceneEngine().Process(value);
            WriteAzerothOrbit(output, format, snapshot);

            if (snapshot.Disposition == AzerothSimulationDisposition.Contained)
            {
                error.WriteLine($"AZEROTH_CONTAINMENT: {string.Join(" | ", snapshot.ContainmentReasons)}");
                return AzerothContainmentExitCode;
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
            error.WriteLine($"AZEROTH_INPUT_FAILURE: {ex.Message}");
            return AzerothInputFailureExitCode;
        }
        catch (ArgumentException ex)
        {
            error.WriteLine($"AZEROTH_INPUT_FAILURE: {ex.Message}");
            return AzerothInputFailureExitCode;
        }
        catch (InvalidDataException ex)
        {
            error.WriteLine($"AZEROTH_INPUT_FAILURE: {ex.Message}");
            return AzerothInputFailureExitCode;
        }
        catch (Exception ex)
        {
            error.WriteLine($"INTERNAL_FAILURE: {ex.Message}");
            return InternalFailureExitCode;
        }
    }

    private static void CheckAzerothInputSize(FileInfo input)
    {
        input.Refresh();
        if (input.Length > AzerothSimulationBounds.MaximumCliInputBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(input),
                $"Azeroth simulation input cannot exceed {AzerothSimulationBounds.MaximumCliInputBytes} bytes.");
        }
    }

    private static void WriteAzerothRegions(
        TextWriter output,
        string format,
        AzerothRegionAuthoritySnapshot result)
    {
        if (StringComparer.Ordinal.Equals(format, "json"))
        {
            output.WriteLine(AzerothRegionAuthoritySerializer.Serialize(result));
            return;
        }

        output.WriteLine("azeroth=region-authority");
        output.WriteLine($"authority={result.Authority}");
        output.WriteLine($"scope={result.Scope}");
        output.WriteLine($"regions={result.Regions.Count}");
        output.WriteLine($"region_names={string.Join(" | ", result.Regions.Select(region => region.RegionName))}");
        output.WriteLine($"zone_ids={string.Join(" | ", result.Regions.Select(region => region.ZoneId))}");
        output.WriteLine($"spatial_authority={result.SpatialAuthority}");
        output.WriteLine($"expansion_policy={result.ExpansionPolicy}");
    }

    private static void WriteAzeroth(TextWriter output, string format, AzerothSimulationSnapshot result)
    {
        if (StringComparer.Ordinal.Equals(format, "json"))
        {
            output.WriteLine(AzerothSimulationSnapshotSerializer.Serialize(result));
            return;
        }

        output.WriteLine("azeroth=composed-simulation");
        output.WriteLine($"authority={AzerothSimulationSnapshotSerializer.Authority}");
        output.WriteLine($"status={result.Disposition.ToString().ToLowerInvariant()}");
        output.WriteLine($"generation={result.Generation}");
        output.WriteLine($"coverage={result.Azeroth.Coverage}");
        output.WriteLine($"zones={result.Azeroth.Zones.Count}");
        output.WriteLine($"omega={result.Omega.Disposition}");
        output.WriteLine($"lunara={result.Lunara.Disposition}");
        output.WriteLine($"solune={result.Solune.Disposition}");
        output.WriteLine($"globe_model={result.Azeroth.GlobeModel}");
        output.WriteLine($"atmosphere_model={result.Azeroth.AtmosphereModel}");
        output.WriteLine($"mana_model={result.Azeroth.ManaModel}");
        output.WriteLine($"containment_reasons={string.Join(" | ", result.ContainmentReasons)}");
    }

    private static void WriteAzerothField(
        TextWriter output,
        string format,
        AzerothEnvironmentalSimulationSnapshot result)
    {
        if (StringComparer.Ordinal.Equals(format, "json"))
        {
            output.WriteLine(AzerothEnvironmentalFieldSnapshotSerializer.Serialize(result));
            return;
        }

        output.WriteLine("azeroth=environmental-field");
        output.WriteLine($"authority={AzerothEnvironmentalFieldSnapshotSerializer.Authority}");
        output.WriteLine($"status={result.Environment.Disposition.ToString().ToLowerInvariant()}");
        output.WriteLine($"generation={result.Environment.Generation}");
        output.WriteLine($"model={result.Environment.Model}");
        output.WriteLine($"units={result.Environment.Units}");
        output.WriteLine($"spatial_authority={result.Environment.SpatialAuthority}");
        output.WriteLine($"zones={result.Environment.Zones.Count}");
        output.WriteLine($"solune_driver={result.Environment.Drivers.SoluneIrradianceMicros}");
        output.WriteLine($"lunara_tide_driver={result.Environment.Drivers.LunaraTideMicros}");
        output.WriteLine($"lunara_mana_driver={result.Environment.Drivers.LunaraResonanceMicros}");
        output.WriteLine($"omega_mana_driver={result.Environment.Drivers.OmegaFitnessMicros}");
        output.WriteLine($"containment_reasons={string.Join(" | ", result.Environment.ContainmentReasons)}");
    }

    private static void WriteAzerothOrbit(
        TextWriter output,
        string format,
        AzerothOrbitalSceneSnapshot result)
    {
        if (StringComparer.Ordinal.Equals(format, "json"))
        {
            output.WriteLine(AzerothOrbitalSceneSerializer.Serialize(result));
            return;
        }

        output.WriteLine("azeroth=orbital-scene");
        output.WriteLine($"authority={AzerothOrbitalSceneSerializer.Authority}");
        output.WriteLine($"classification={result.Classification}");
        output.WriteLine($"status={result.Disposition.ToString().ToLowerInvariant()}");
        output.WriteLine($"generation={result.Generation}");
        output.WriteLine($"normalized_ratio={result.Scale.LinearRatio}");
        output.WriteLine($"physical_scale={result.Scale.PhysicalScale}");
        output.WriteLine($"geometry_authority={result.GeometryAuthority}");
        output.WriteLine($"orbit_azimuth={result.Camera.OrbitAzimuthMicros}");
        output.WriteLine($"orbit_elevation={result.Camera.OrbitElevationMicros}");
        output.WriteLine($"zoom={result.Camera.ZoomLevel}");
        output.WriteLine($"viewport_width={result.Camera.ViewportReferenceWidthMicros}");
        output.WriteLine($"atmosphere_shell={result.Lighting.AtmosphereShellSignalMicros}");
        output.WriteLine($"mana_visibility={result.Lighting.ManaVisibilitySignalMicros}");
        output.WriteLine($"zones={result.Zones.Count}");
        output.WriteLine($"containment_reasons={string.Join(" | ", result.ContainmentReasons)}");
    }
}

