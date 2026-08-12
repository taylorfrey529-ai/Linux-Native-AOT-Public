using System.Globalization;
using Omega.Recursive;

namespace Evermore.ThreeD.Environment;

public sealed class OmegaSandboxEngine
{
    public const int MinimumSystems = 1;
    public const int MaximumSystems = 512;
    public const int MinimumWorldsPerSystem = 1;
    public const int MaximumWorldsPerSystem = 32;
    public const int MinimumMoonsPerWorld = 0;
    public const int MaximumMoonsPerWorld = 16;
    public const int MaximumBodies = 100_000;
    public const int MaximumGenerations = 1_024;
    public const int MaximumSimulationTick = 1_000_000;
    public const int MaximumAdvanceTicks = 10_000;
    public const string Authority = "isolated-noncanon-omega-sandbox";
    public const string Classification = "ConstrainedSimulationConceptOnly";
    public const string CoordinateMeaning = "sandbox-normalized-axes-not-geography-or-physical-spacetime";
    public const string PhysicalScale = "unavailable-no-authority";

    private const int UnitMicros = 1_000_000;
    private const ulong SandboxSeedSalt = 0xD1B54A32D192ED03UL;

    public OmegaSandboxSnapshot Process(OmegaSandboxInput input) => Process(input, 0);

    public OmegaSandboxSnapshot Process(OmegaSandboxInput input, int simulationTick)
    {
        ArgumentNullException.ThrowIfNull(input);
        int requestedBodyCount = Validate(input);
        ValidateSimulationTick(simulationTick);
        OmegaRunResult omega = new OmegaEngine().Run(
            new OmegaSimulationOptions(input.Seed, input.Generations));
        OmegaGenerationSnapshot? finalGeneration = omega.Snapshots.Count == 0
            ? null
            : omega.Snapshots[^1];
        var observation = new OmegaSandboxOmegaObservation(
            "isolated-noncanon",
            omega.Disposition == OmegaRunDisposition.Completed ? "completed" : "contained",
            omega.Seed,
            omega.RequestedGenerations,
            omega.ProcessedGenerations,
            ToMicros(finalGeneration?.AverageFitness ?? 0f),
            finalGeneration?.DominantTrait ?? "unavailable-contained-before-observation",
            omega.ResonanceLog.Count,
            omega.ContainmentReason);

        if (omega.Disposition == OmegaRunDisposition.Contained)
        {
            return new OmegaSandboxSnapshot(
                input,
                OmegaSandboxDisposition.Contained,
                Authority,
                Classification,
                CoordinateMeaning,
                PhysicalScale,
                OmegaSandboxDimensions.CreateNames(),
                observation,
                0,
                0,
                0,
                0,
                omega.ContainmentReason,
                [],
                simulationTick);
        }

        IReadOnlyDictionary<string, int> traitMicros = CreateTraitMicros(finalGeneration);
        ulong derivedSeed = unchecked(
            input.Seed ^ SandboxSeedSalt ^ ((ulong)(uint)input.Generations << 32));
        var random = new OmegaRandom(derivedSeed);
        var systems = new OmegaSandboxSystem[input.SystemCount];

        for (int systemIndex = 0; systemIndex < systems.Length; systemIndex++)
        {
            string systemId = "omega.system." + systemIndex.ToString("D4", CultureInfo.InvariantCulture);
            OmegaSandboxBody hearthstar = CreateBody(
                random,
                traitMicros,
                systemId + ".hearthstar",
                OmegaSandboxBodyKinds.Hearthstar,
                null,
                systemIndex,
                null,
                0,
                simulationTick);
            var worlds = new OmegaSandboxBody[input.WorldsPerSystem];
            var moons = new OmegaSandboxBody[checked(input.WorldsPerSystem * input.MoonsPerWorld)];
            int moonOffset = 0;

            for (int worldIndex = 0; worldIndex < worlds.Length; worldIndex++)
            {
                string worldId = systemId + ".world." + worldIndex.ToString("D2", CultureInfo.InvariantCulture);
                worlds[worldIndex] = CreateBody(
                    random,
                    traitMicros,
                    worldId,
                    OmegaSandboxBodyKinds.World,
                    hearthstar.BodyId,
                    worldIndex,
                    hearthstar.DimensionVector,
                    checked(45_000 + ((worldIndex + 1) * 8_000)),
                    simulationTick);

                for (int moonIndex = 0; moonIndex < input.MoonsPerWorld; moonIndex++)
                {
                    string moonId = worldId + ".moon." + moonIndex.ToString("D2", CultureInfo.InvariantCulture);
                    moons[moonOffset++] = CreateBody(
                        random,
                        traitMicros,
                        moonId,
                        OmegaSandboxBodyKinds.Moon,
                        worldId,
                        moonIndex,
                        worlds[worldIndex].DimensionVector,
                        checked(5_000 + ((moonIndex + 1) * 4_000)),
                        simulationTick);
                }
            }

            systems[systemIndex] = new OmegaSandboxSystem(systemId, hearthstar, worlds, moons);
        }

        int worldCount = checked(input.SystemCount * input.WorldsPerSystem);
        int moonCount = checked(worldCount * input.MoonsPerWorld);
        return new OmegaSandboxSnapshot(
            input,
            OmegaSandboxDisposition.Completed,
            Authority,
            Classification,
            CoordinateMeaning,
            PhysicalScale,
            OmegaSandboxDimensions.CreateNames(),
            observation,
            systems.Length,
            worldCount,
            moonCount,
            requestedBodyCount,
            null,
            systems,
            simulationTick);
    }

    public OmegaSandboxSnapshot Advance(OmegaSandboxSnapshot snapshot, int ticks)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Disposition != OmegaSandboxDisposition.Completed)
            throw new InvalidOperationException("A contained Omega Sandbox cannot advance.");
        if (ticks is < 1 or > MaximumAdvanceTicks)
            throw new ArgumentOutOfRangeException(nameof(ticks), $"Advance ticks must be between 1 and {MaximumAdvanceTicks}.");
        int targetTick = checked(snapshot.SimulationTick + ticks);
        ValidateSimulationTick(targetTick);
        return Process(snapshot.Input, targetTick);
    }

    public static int Validate(OmegaSandboxInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Generations is < 1 or > MaximumGenerations)
            throw new ArgumentOutOfRangeException(nameof(input), $"Sandbox generations must be between 1 and {MaximumGenerations}.");
        if (input.SystemCount is < MinimumSystems or > MaximumSystems)
            throw new ArgumentOutOfRangeException(nameof(input), $"System count must be between {MinimumSystems} and {MaximumSystems}.");
        if (input.WorldsPerSystem is < MinimumWorldsPerSystem or > MaximumWorldsPerSystem)
            throw new ArgumentOutOfRangeException(nameof(input), $"Worlds per system must be between {MinimumWorldsPerSystem} and {MaximumWorldsPerSystem}.");
        if (input.MoonsPerWorld is < MinimumMoonsPerWorld or > MaximumMoonsPerWorld)
            throw new ArgumentOutOfRangeException(nameof(input), $"Moons per world must be between {MinimumMoonsPerWorld} and {MaximumMoonsPerWorld}.");

        long worlds = checked((long)input.SystemCount * input.WorldsPerSystem);
        long moons = checked(worlds * input.MoonsPerWorld);
        long bodies = checked(input.SystemCount + worlds + moons);
        if (bodies > MaximumBodies)
            throw new ArgumentOutOfRangeException(nameof(input), $"The Omega Sandbox cannot exceed {MaximumBodies} bodies.");
        return checked((int)bodies);
    }

    public static void ValidateSimulationTick(int simulationTick)
    {
        if (simulationTick is < 0 or > MaximumSimulationTick)
        {
            throw new ArgumentOutOfRangeException(
                nameof(simulationTick),
                $"Simulation tick must be between 0 and {MaximumSimulationTick}.");
        }
    }

    private static OmegaSandboxBody CreateBody(
        OmegaRandom random,
        IReadOnlyDictionary<string, int> traitMicros,
        string bodyId,
        string bodyKind,
        string? parentBodyId,
        int ordinal,
        int[]? spatialParent,
        int spatialSpan,
        int simulationTick)
    {
        var vector = new int[OmegaSandboxDimensions.Count];
        PopulateSpatialAxes(vector, random, bodyId, spatialParent, spatialSpan, simulationTick);
        vector[OmegaSandboxDimensions.TemporalPhase] = NextSignedMicros(random);
        vector[OmegaSandboxDimensions.Mana] = BodyChannel(random, bodyKind, OmegaSandboxBodyKinds.World, 250_000, UnitMicros);
        vector[OmegaSandboxDimensions.Atmosphere] = BodyChannel(random, bodyKind, OmegaSandboxBodyKinds.World, 250_000, UnitMicros);
        vector[OmegaSandboxDimensions.Radiance] = BodyChannel(random, bodyKind, OmegaSandboxBodyKinds.Hearthstar, 650_000, UnitMicros);
        vector[OmegaSandboxDimensions.Tidal] = BodyChannel(random, bodyKind, OmegaSandboxBodyKinds.Moon, 400_000, UnitMicros);
        vector[OmegaSandboxDimensions.Resonance] = NextUnitMicros(random);
        vector[OmegaSandboxDimensions.Entropy] = BlendTrait(random, traitMicros, "Entropy");
        vector[OmegaSandboxDimensions.Dominion] = BlendTrait(random, traitMicros, "Dominion");
        vector[OmegaSandboxDimensions.Whispers] = BlendTrait(random, traitMicros, "Whispers");
        vector[OmegaSandboxDimensions.VoidPulse] = BlendTrait(random, traitMicros, "VoidPulse");
        vector[OmegaSandboxDimensions.Echo] = BlendTrait(random, traitMicros, "Echo");
        vector[OmegaSandboxDimensions.Ascension] = BlendTrait(random, traitMicros, "Ascension");
        vector[OmegaSandboxDimensions.Decay] = BlendTrait(random, traitMicros, "Decay");
        vector[OmegaSandboxDimensions.Flux] = BlendTrait(random, traitMicros, "Flux");
        vector[OmegaSandboxDimensions.ContainmentIntegrity] = UnitMicros;
        EvolveChannels(vector, bodyId, simulationTick);

        int scaleMicros = bodyKind switch
        {
            OmegaSandboxBodyKinds.Hearthstar => 600_000 + random.NextInt32(400_001),
            OmegaSandboxBodyKinds.World => 120_000 + random.NextInt32(280_001),
            OmegaSandboxBodyKinds.Moon => 35_000 + random.NextInt32(115_001),
            _ => throw new InvalidOperationException("Unknown sandbox body kind.")
        };
        return new OmegaSandboxBody(bodyId, bodyKind, parentBodyId, ordinal, scaleMicros, vector);
    }

    private static void PopulateSpatialAxes(
        int[] vector,
        OmegaRandom random,
        string bodyId,
        int[]? parent,
        int span,
        int simulationTick)
    {
        for (int axis = OmegaSandboxDimensions.X; axis <= OmegaSandboxDimensions.Z; axis++)
        {
            if (parent is null)
            {
                int initial = NextSignedMicros(random);
                vector[axis] = simulationTick == 0
                    ? initial
                    : EvolveBounded(
                        initial,
                        OmegaSandboxDimensions.MinimumValue,
                        OmegaSandboxDimensions.MaximumValue,
                        TemporalOffset(bodyId, axis, simulationTick, 50_000));
                continue;
            }

            int initialOffset = NextSignedOffset(random, span);
            int evolvedOffset = simulationTick == 0
                ? initialOffset
                : EvolveBounded(
                    initialOffset,
                    -span,
                    span,
                    TemporalOffset(bodyId, axis, simulationTick, Math.Max(1, span / 4)));
            vector[axis] = Math.Clamp(
                checked(parent[axis] + evolvedOffset),
                OmegaSandboxDimensions.MinimumValue,
                OmegaSandboxDimensions.MaximumValue);
        }
    }

    private static void EvolveChannels(int[] vector, string bodyId, int simulationTick)
    {
        if (simulationTick == 0)
            return;

        vector[OmegaSandboxDimensions.TemporalPhase] = EvolveBounded(
            vector[OmegaSandboxDimensions.TemporalPhase],
            OmegaSandboxDimensions.MinimumValue,
            OmegaSandboxDimensions.MaximumValue,
            TemporalOffset(bodyId, OmegaSandboxDimensions.TemporalPhase, simulationTick, 250_000));
        for (int axis = OmegaSandboxDimensions.Mana; axis <= OmegaSandboxDimensions.Flux; axis++)
        {
            vector[axis] = EvolveBounded(
                vector[axis],
                0,
                UnitMicros,
                TemporalOffset(bodyId, axis, simulationTick, 125_000));
        }

        int integrityWave = Math.Min(
            24_999,
            Math.Abs(TemporalOffset(
                bodyId,
                OmegaSandboxDimensions.ContainmentIntegrity,
                simulationTick,
                25_000)));
        vector[OmegaSandboxDimensions.ContainmentIntegrity] = 975_000 + integrityWave;
    }

    private static int TemporalOffset(
        string bodyId,
        int axis,
        int simulationTick,
        int amplitude)
    {
        long cycle = checked((amplitude * 2L) + 1L);
        long motion = checked((long)simulationTick * (axis + 17) * 104_729L);
        long position = (StableHash(bodyId, axis) + motion) % cycle;
        return checked((int)(position - amplitude));
    }

    private static uint StableHash(string bodyId, int axis)
    {
        unchecked
        {
            uint hash = 2_166_136_261U;
            foreach (char value in bodyId)
            {
                hash ^= value;
                hash *= 16_777_619U;
            }
            hash ^= (uint)axis;
            hash *= 16_777_619U;
            return hash;
        }
    }

    private static int EvolveBounded(int initial, int minimum, int maximum, int offset)
    {
        int evolved = checked((int)Math.Clamp((long)initial + offset, minimum, maximum));
        if (evolved != initial)
            return evolved;
        return initial == maximum ? initial - 1 : initial + 1;
    }

    private static int NextSignedOffset(OmegaRandom random, int span) =>
        random.NextInt32(checked((span * 2) + 1)) - span;

    private static int BodyChannel(
        OmegaRandom random,
        string bodyKind,
        string enabledKind,
        int enabledMinimum,
        int enabledMaximum) =>
        StringComparer.Ordinal.Equals(bodyKind, enabledKind)
            ? enabledMinimum + random.NextInt32(checked(enabledMaximum - enabledMinimum + 1))
            : random.NextInt32(250_001);

    private static int BlendTrait(
        OmegaRandom random,
        IReadOnlyDictionary<string, int> traitMicros,
        string traitName)
    {
        int observed = traitMicros.GetValueOrDefault(traitName);
        return checked(((observed * 3) + NextUnitMicros(random)) / 4);
    }

    private static IReadOnlyDictionary<string, int> CreateTraitMicros(OmegaGenerationSnapshot? snapshot)
    {
        if (snapshot is null || snapshot.TraitTotals.Count == 0)
            return new Dictionary<string, int>(StringComparer.Ordinal);
        float maximum = snapshot.TraitTotals.Max(value => value.Strength);
        return snapshot.TraitTotals.ToDictionary(
            value => value.Name,
            value => ToMicros(value.Strength / maximum),
            StringComparer.Ordinal);
    }

    private static int NextSignedMicros(OmegaRandom random) =>
        random.NextInt32((OmegaSandboxDimensions.MaximumValue * 2) + 1) + OmegaSandboxDimensions.MinimumValue;

    private static int NextUnitMicros(OmegaRandom random) => random.NextInt32(UnitMicros + 1);

    private static int ToMicros(float value) =>
        checked((int)Math.Round(Math.Clamp(value, 0f, 1f) * UnitMicros, MidpointRounding.ToEven));
}
