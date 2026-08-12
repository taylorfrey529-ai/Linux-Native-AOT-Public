using Evermore.Genetics;

namespace EasternKingdoms.Simulation;

public static class AzerothEvolutionBootstrap
{
    public const string ElvenGenomeSchema = "evermore.elven.genome/3.0";

    public static AzerothLifeRegistry CreateLifeRegistry()
    {
        var life = new AzerothLifeRegistry();

        life.RegisterSpecies(new SpeciesProfile(
            SpeciesId: "azeroth.elf.high",
            Kind: BeingKind.Person,
            Status: SpeciesProfileStatus.Authored,
            GenomeSchemaId: ElvenGenomeSchema,
            MinimumReproductivePhaseAge: 20,
            GenerationIntervalPhases: 20,
            BaseFecundity: 0.18,
            MinimumPopulationHealth: 0.45,
            CompatibleSpeciesIds: new HashSet<string>(StringComparer.Ordinal)
            {
                "azeroth.elf.sindorei"
            }));

        life.RegisterSpecies(new SpeciesProfile(
            SpeciesId: "azeroth.elf.sindorei",
            Kind: BeingKind.Person,
            Status: SpeciesProfileStatus.Authored,
            GenomeSchemaId: ElvenGenomeSchema,
            MinimumReproductivePhaseAge: 20,
            GenerationIntervalPhases: 20,
            BaseFecundity: 0.18,
            MinimumPopulationHealth: 0.45,
            CompatibleSpeciesIds: new HashSet<string>(StringComparer.Ordinal)
            {
                "azeroth.elf.high"
            }));

        life.RegisterSpecies(new SpeciesProfile(
            SpeciesId: "azeroth.elf.convergent",
            Kind: BeingKind.Person,
            Status: SpeciesProfileStatus.Authored,
            GenomeSchemaId: ElvenGenomeSchema,
            MinimumReproductivePhaseAge: 20,
            GenerationIntervalPhases: 20,
            BaseFecundity: 0.17,
            MinimumPopulationHealth: 0.45,
            CompatibleSpeciesIds: new HashSet<string>(StringComparer.Ordinal)
            {
                "azeroth.elf.high",
                "azeroth.elf.sindorei",
                "azeroth.elf.convergent"
            }));

        return life;
    }

    public static SpeciesProfile CreateDeferredSpecies(
        string speciesId,
        BeingKind kind,
        string genomeSchemaId) =>
        new(
            SpeciesId: speciesId,
            Kind: kind,
            Status: SpeciesProfileStatus.Deferred,
            GenomeSchemaId: genomeSchemaId,
            MinimumReproductivePhaseAge: int.MaxValue,
            GenerationIntervalPhases: int.MaxValue,
            BaseFecundity: 0.0,
            MinimumPopulationHealth: 1.0,
            CompatibleSpeciesIds: new HashSet<string>(StringComparer.Ordinal));

    public static IReadOnlyList<ZoneEnvironment> CreateNorthernScarCorridor()
    {
        ZoneEnvironment[] zones =
        [
            Zone(AzerothRegionAuthority.EversongZoneId, CellSimulationState.Warm, 0.81, 0.73, 0.86, 0.78, 0.29),
            Zone(AzerothRegionAuthority.GhostlandsZoneId, CellSimulationState.Hot, 0.54, 0.43, 0.46, 0.41, 0.69),
            Zone(AzerothRegionAuthority.WesternPlaguelandsZoneId, CellSimulationState.Warm, 0.61, 0.52, 0.52, 0.53, 0.54),
            Zone(AzerothRegionAuthority.EasternPlaguelandsZoneId, CellSimulationState.Hot, 0.48, 0.39, 0.38, 0.37, 0.76)
        ];
        AzerothRegionAuthority.RequireExactSet(zones.Select(zone => zone.ZoneId));
        return zones;
    }

    private static ZoneEnvironment Zone(
        string id,
        CellSimulationState state,
        double populationHealth,
        double stability,
        double luminosity,
        double integrity,
        double resonanceDebt) =>
        new(
            id,
            state,
            populationHealth,
            stability,
            luminosity,
            integrity,
            resonanceDebt,
            new Dictionary<string, double>(StringComparer.Ordinal)
            {
                ["arcane_precision"] = id.Contains("eversong", StringComparison.Ordinal) ? 0.16 : 0.05,
                ["arcane_conservation"] = id.Contains("plaguelands", StringComparison.Ordinal) ? 0.14 : 0.04,
                ["resonance_amplification"] = id.Contains("ghostlands", StringComparison.Ordinal) ? -0.12 : 0.03
            });
}

