using EasternKingdoms.Simulation;
using Evermore.Lunara.Core;
using Evermore.Solune.Core;
using Omega.Recursive;

namespace Evermore.Azeroth.Simulation;

public static class AzerothSimulationBounds
{
    public const long MaximumCliInputBytes = 16_777_216;
}

public sealed class AzerothSimulationEngine
{
    public const string OmegaAuthority = "isolated-noncanon";

    private readonly OmegaEngine _omega;
    private readonly LunaraEngine _lunara;
    private readonly SoluneEngine _solune;

    public AzerothSimulationEngine(
        OmegaEngine? omega = null,
        LunaraEngine? lunara = null,
        SoluneEngine? solune = null)
    {
        _omega = omega ?? new OmegaEngine();
        _lunara = lunara ?? new LunaraEngine();
        _solune = solune ?? new SoluneEngine();
    }

    public AzerothSimulationSnapshot Process(
        AzerothSimulationInput input,
        CancellationToken cancellationToken = default)
    {
        ValidateInput(input);
        cancellationToken.ThrowIfCancellationRequested();

        AzerothZoneObservation[] zones = AzerothEvolutionBootstrap.CreateNorthernScarCorridor()
            .Select(zone => new AzerothZoneObservation(zone.ZoneId, AzerothRegionAuthority.ObservationSource))
            .OrderBy(zone => zone.ZoneId, StringComparer.Ordinal)
            .ToArray();
        AzerothRegionAuthority.RequireExactSet(zones.Select(zone => zone.ZoneId));

        OmegaRunResult omega = _omega.Run(input.Omega, cancellationToken);
        MoonTideSnapshot lunara = _lunara.Process(input.Lunara, cancellationToken);
        SoluneSnapshot solune = _solune.Process(input.Solune, cancellationToken);
        if (omega.Snapshots.Count == 0)
            throw new InvalidDataException("Omega returned no generation snapshot.");
        OmegaGenerationSnapshot finalOmega = omega.Snapshots[^1];

        string[] containmentReasons = CreateContainmentReasons(omega, lunara, solune);
        var disposition = containmentReasons.Length == 0
            ? AzerothSimulationDisposition.Completed
            : AzerothSimulationDisposition.Contained;

        return new AzerothSimulationSnapshot(
            input.Lunara.Generation,
            new AzerothScopeObservation(
                "Azeroth",
                "Eastern Kingdoms",
                AzerothRegionAuthority.Coverage,
                "deferred-authority-required",
                "deferred-authority-required",
                "lunara-resonance-observation-only",
                zones),
            new AzerothOmegaObservation(
                OmegaAuthority,
                omega.Disposition.ToString().ToLowerInvariant(),
                omega.Seed,
                omega.RequestedGenerations,
                omega.ProcessedGenerations,
                omega.FinalPopulation.Count,
                finalOmega.AverageFitness,
                finalOmega.DominantTrait,
                omega.Snapshots.Count(snapshot => snapshot.IsArchiveCheckpoint),
                omega.ResonanceLog.Count,
                omega.ContainmentReason),
            new AzerothLunaraObservation(
                MoonTideSnapshotSerializer.Authority,
                lunara.Disposition.ToString().ToLowerInvariant(),
                lunara.Seed,
                lunara.Generation,
                lunara.LunarCycle.Phase.ToString(),
                lunara.LunarCycle.IlluminationMicros,
                lunara.Tide.LevelMicros,
                lunara.ResonanceMicros,
                lunara.Relics.Count,
                lunara.ManifestationDirectives.Count,
                lunara.ContainmentCode,
                lunara.ContainmentReason),
            new AzerothSoluneObservation(
                SoluneSnapshotSerializer.Authority,
                solune.Disposition.ToString().ToLowerInvariant(),
                solune.Generation,
                solune.Model.ConstructId,
                solune.Calendar.Phase.ToString(),
                solune.Orbit.PhaseMicros,
                solune.Radiance.IrradianceMilliwattsPerSquareMeter,
                solune.Radiance.CalculatedApparentDiameterMicrodegrees,
                solune.OperationDirectives.Count,
                solune.ContainmentCode,
                solune.ContainmentReason),
            disposition,
            containmentReasons,
            input);
    }

    private static void ValidateInput(AzerothSimulationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Omega);
        ArgumentNullException.ThrowIfNull(input.Lunara);
        ArgumentNullException.ThrowIfNull(input.Solune);
        if (input.Omega.Seed != input.Lunara.Seed)
            throw new ArgumentException("Omega and Lunara must share one deterministic seed.", nameof(input));
        if (input.Lunara.Generation != input.Solune.Generation)
            throw new ArgumentException("Lunara and Solune must observe the same Azeroth generation.", nameof(input));
    }

    private static string[] CreateContainmentReasons(
        OmegaRunResult omega,
        MoonTideSnapshot lunara,
        SoluneSnapshot solune)
    {
        var reasons = new List<string>(3);
        if (omega.Disposition == OmegaRunDisposition.Contained)
            reasons.Add($"Omega:{omega.ContainmentReason ?? "unspecified"}");
        if (lunara.Disposition == MoonTideDisposition.Contained)
            reasons.Add($"Lunara:{lunara.ContainmentCode ?? "unspecified"}:{lunara.ContainmentReason ?? "unspecified"}");
        if (solune.Disposition == SoluneDisposition.Contained)
            reasons.Add($"Solune:{solune.ContainmentCode ?? "unspecified"}:{solune.ContainmentReason ?? "unspecified"}");
        return reasons.ToArray();
    }
}

