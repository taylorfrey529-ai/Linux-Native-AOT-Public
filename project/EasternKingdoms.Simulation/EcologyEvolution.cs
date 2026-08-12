using Evermore.Genetics;

namespace EasternKingdoms.Simulation;

public enum SpeciesEcologyStatus { Deferred, Active }
public enum EcologicalGuild { SapientGeneralist, Producer, Grazer, Omnivore, Predator, Scavenger, Decomposer, Parasite, Symbiont, Other }
public enum FoodWebNodeKind { ResourcePool, Species }

public sealed record FoodWebDependency(
    string TargetId,
    FoodWebNodeKind TargetKind,
    double Weight,
    bool Consumptive);

public sealed record SpeciesEcologyProfile(
    string SpeciesId,
    SpeciesEcologyStatus Status,
    EcologicalGuild Guild,
    IReadOnlyList<FoodWebDependency> Dependencies,
    IReadOnlyDictionary<string, double> HazardSensitivities,
    double DensitySensitivity,
    double MigrationSensitivity,
    string ContinuityStatus);

public sealed record ZoneFoodWebState(
    string ZoneId,
    IReadOnlyDictionary<string, double> ResourcePools,
    IReadOnlyDictionary<string, double> Hazards,
    IReadOnlyDictionary<string, double> SpeciesAbundance);

public sealed record SpeciesEcologyOutcome(
    string ZoneId,
    string SpeciesId,
    double ResourceSupport,
    double HazardLoad,
    double CarryingCapacityModifier,
    double ReproductiveModifier,
    double ViabilityModifier,
    double MigrationPressure,
    bool FoodWebComplete);

public sealed class SpeciesEcologyCatalog
{
    private readonly Dictionary<string, SpeciesEcologyProfile> _profiles = new(StringComparer.Ordinal);

    public IReadOnlyCollection<SpeciesEcologyProfile> Profiles => _profiles.Values;

    public void Register(SpeciesEcologyProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (string.IsNullOrWhiteSpace(profile.SpeciesId))
            throw new InvalidOperationException("Ecology profile requires a species ID.");
        if (profile.DensitySensitivity is < 0.0 or > 1.0)
            throw new InvalidOperationException($"Species {profile.SpeciesId} has invalid density sensitivity.");
        if (profile.MigrationSensitivity is < 0.0 or > 1.0)
            throw new InvalidOperationException($"Species {profile.SpeciesId} has invalid migration sensitivity.");
        foreach (var dependency in profile.Dependencies)
        {
            if (string.IsNullOrWhiteSpace(dependency.TargetId))
                throw new InvalidOperationException($"Species {profile.SpeciesId} has an unnamed food-web dependency.");
            if (dependency.Weight is <= 0.0 or > 1.0)
                throw new InvalidOperationException($"Species {profile.SpeciesId} has an invalid dependency weight.");
        }
        foreach (var hazard in profile.HazardSensitivities)
        {
            if (hazard.Value is < 0.0 or > 1.0)
                throw new InvalidOperationException($"Species {profile.SpeciesId} has invalid hazard sensitivity {hazard.Key}.");
        }
        if (!_profiles.TryAdd(profile.SpeciesId, profile))
            throw new InvalidOperationException($"Ecology profile for {profile.SpeciesId} already exists.");
    }

    public SpeciesEcologyProfile Get(string speciesId) =>
        _profiles.TryGetValue(speciesId, out var value)
            ? value
            : throw new KeyNotFoundException($"Unknown ecology profile {speciesId}.");

    public bool TryGet(string speciesId, out SpeciesEcologyProfile profile) =>
        _profiles.TryGetValue(speciesId, out profile!);
}

public sealed class ZoneFoodWebBuilder
{
    public ZoneFoodWebState Build(
        ZoneEnvironment zone,
        IReadOnlyDictionary<string, double>? speciesAbundance = null)
    {
        ArgumentNullException.ThrowIfNull(zone);

        var resources = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["food_support"] = Clamp01((zone.PopulationHealth + zone.Stability + zone.Luminosity) / 3.0),
            ["clean_water"] = Clamp01((zone.PopulationHealth + zone.Integrity) / 2.0),
            ["shelter"] = Clamp01((zone.Integrity + zone.Stability) / 2.0),
            ["medical_support"] = Clamp01((zone.PopulationHealth + zone.Stability) / 2.0),
            ["arcane_stability"] = Clamp01(zone.Luminosity * (1.0 - zone.ResonanceDebt * 0.50)),
            ["primary_biomass"] = Clamp01((zone.Stability + zone.Integrity + zone.Luminosity) / 3.0),
            ["soil_nutrients"] = Clamp01((zone.Integrity + zone.Stability + (1.0 - zone.ResonanceDebt)) / 3.0),
            ["detritus"] = Clamp01(((1.0 - zone.Integrity) + zone.ResonanceDebt + (1.0 - zone.PopulationHealth)) / 3.0),
            ["decomposer_capacity"] = Clamp01((zone.Integrity + (1.0 - zone.ResonanceDebt)) / 2.0)
        };

        var hazards = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["contamination"] = Clamp01((1.0 - zone.Integrity) * 0.50 + zone.ResonanceDebt * 0.50),
            ["disease"] = Clamp01((1.0 - zone.PopulationHealth) * 0.70 + (1.0 - zone.Stability) * 0.30),
            ["displacement"] = Clamp01(((1.0 - zone.Stability) + (1.0 - zone.Integrity)) / 2.0),
            ["resonance_stress"] = Clamp01(zone.ResonanceDebt)
        };

        return new ZoneFoodWebState(
            zone.ZoneId,
            resources,
            hazards,
            speciesAbundance ?? new Dictionary<string, double>(StringComparer.Ordinal));
    }

    private static double Clamp01(double value) => Math.Clamp(value, 0.0, 1.0);
}

public sealed class FoodWebResolver
{
    public SpeciesEcologyOutcome Resolve(
        ZoneFoodWebState state,
        SpeciesEcologyProfile profile)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(profile);

        if (profile.Status != SpeciesEcologyStatus.Active)
            throw new InvalidOperationException($"Ecology profile {profile.SpeciesId} is Deferred.");

        double weightedSupport = 0.0;
        double totalWeight = 0.0;
        bool complete = true;

        foreach (var dependency in profile.Dependencies)
        {
            double availability;
            if (dependency.TargetKind == FoodWebNodeKind.ResourcePool)
            {
                if (!state.ResourcePools.TryGetValue(dependency.TargetId, out availability))
                {
                    complete = false;
                    availability = 0.0;
                }
            }
            else
            {
                if (!state.SpeciesAbundance.TryGetValue(dependency.TargetId, out availability))
                {
                    complete = false;
                    availability = 0.0;
                }
            }

            weightedSupport += Math.Clamp(availability, 0.0, 1.0) * dependency.Weight;
            totalWeight += dependency.Weight;
        }

        double resourceSupport = totalWeight <= 0.0 ? 0.0 : weightedSupport / totalWeight;

        double hazardWeighted = 0.0;
        double hazardWeight = 0.0;
        foreach (var sensitivity in profile.HazardSensitivities)
        {
            double hazard = state.Hazards.GetValueOrDefault(sensitivity.Key);
            hazardWeighted += Math.Clamp(hazard, 0.0, 1.0) * sensitivity.Value;
            hazardWeight += sensitivity.Value;
        }
        double hazardLoad = hazardWeight <= 0.0 ? 0.0 : hazardWeighted / hazardWeight;

        double density = Math.Clamp(state.SpeciesAbundance.GetValueOrDefault(profile.SpeciesId), 0.0, 1.0);
        double densityPenalty = density * profile.DensitySensitivity * 0.35;
        double carrying = Math.Clamp(resourceSupport * (1.0 - hazardLoad * 0.55) - densityPenalty, 0.0, 1.0);
        double reproductive = Math.Clamp(0.35 + carrying * 0.80 - hazardLoad * 0.20, 0.0, 1.25);
        double viability = Math.Clamp(0.40 + carrying * 0.70 - hazardLoad * 0.25, 0.0, 1.0);
        double migration = Math.Clamp((1.0 - carrying) * profile.MigrationSensitivity + state.Hazards.GetValueOrDefault("displacement") * 0.25, 0.0, 1.0);

        return new SpeciesEcologyOutcome(
            state.ZoneId,
            profile.SpeciesId,
            resourceSupport,
            hazardLoad,
            carrying,
            reproductive,
            viability,
            migration,
            complete);
    }
}

public sealed class BiologicalActivationGate
{
    private readonly SpeciesActivationGate _species;
    private readonly SpeciesEcologyCatalog _ecology;

    public BiologicalActivationGate(
        SpeciesActivationGate species,
        SpeciesEcologyCatalog ecology)
    {
        _species = species;
        _ecology = ecology;
    }

    public SpeciesProfile Activate(string speciesId)
    {
        var species = _species.Activate(speciesId);
        var ecology = _ecology.Get(speciesId);
        if (ecology.Status != SpeciesEcologyStatus.Active)
            throw new InvalidOperationException($"Species {speciesId} has not passed ecological review.");

        foreach (var dependency in ecology.Dependencies.Where(x => x.TargetKind == FoodWebNodeKind.Species))
        {
            if (!_ecology.TryGet(dependency.TargetId, out var target) || target.Status != SpeciesEcologyStatus.Active)
                throw new InvalidOperationException($"Species {speciesId} depends on unresolved food-web species {dependency.TargetId}.");
        }

        return species;
    }
}

public static class AzerothEcologyBootstrap
{
    public static SpeciesEcologyCatalog Create()
    {
        var catalog = new SpeciesEcologyCatalog();
        RegisterElvenGeneralist(catalog, "azeroth.elf.high");
        RegisterElvenGeneralist(catalog, "azeroth.elf.sindorei");
        RegisterElvenGeneralist(catalog, "azeroth.elf.convergent");

        RegisterDeferred(catalog, "azeroth.deferred.animal", EcologicalGuild.Other);
        RegisterDeferred(catalog, "azeroth.deferred.plant", EcologicalGuild.Producer);
        RegisterDeferred(catalog, "azeroth.deferred.fungus", EcologicalGuild.Decomposer);
        RegisterDeferred(catalog, "azeroth.deferred.other", EcologicalGuild.Other);
        return catalog;
    }

    private static void RegisterElvenGeneralist(
        SpeciesEcologyCatalog catalog,
        string speciesId)
    {
        catalog.Register(new SpeciesEcologyProfile(
            speciesId,
            SpeciesEcologyStatus.Active,
            EcologicalGuild.SapientGeneralist,
            new[]
            {
                new FoodWebDependency("food_support", FoodWebNodeKind.ResourcePool, 0.35, true),
                new FoodWebDependency("clean_water", FoodWebNodeKind.ResourcePool, 0.25, true),
                new FoodWebDependency("shelter", FoodWebNodeKind.ResourcePool, 0.20, false),
                new FoodWebDependency("medical_support", FoodWebNodeKind.ResourcePool, 0.10, false),
                new FoodWebDependency("arcane_stability", FoodWebNodeKind.ResourcePool, 0.10, false)
            },
            new Dictionary<string, double>(StringComparer.Ordinal)
            {
                ["contamination"] = 0.35,
                ["disease"] = 0.25,
                ["displacement"] = 0.20,
                ["resonance_stress"] = 0.20
            },
            DensitySensitivity: 0.20,
            MigrationSensitivity: 0.45,
            ContinuityStatus: "Prototype operational ecology; no new biological canon asserted."));
    }

    private static void RegisterDeferred(
        SpeciesEcologyCatalog catalog,
        string speciesId,
        EcologicalGuild guild)
    {
        catalog.Register(new SpeciesEcologyProfile(
            speciesId,
            SpeciesEcologyStatus.Deferred,
            guild,
            Array.Empty<FoodWebDependency>(),
            new Dictionary<string, double>(StringComparer.Ordinal),
            DensitySensitivity: 0.0,
            MigrationSensitivity: 0.0,
            ContinuityStatus: "Deferred pending species-specific ecology authoring."));
    }
}
