namespace EasternKingdoms.Simulation;

public enum SpeciesActivationState
{
    Deferred,
    ReviewReady,
    Active
}

public sealed record LifecycleProfile(
    int MinimumReproductivePhaseAge,
    int GenerationIntervalPhases,
    double BaseFecundity,
    double MinimumPopulationHealth,
    int MaximumExpectedPhaseAge);

public sealed record GenomeFamilyProfile(
    string GenomeFamilyId,
    string GenomeSchemaId,
    IReadOnlySet<string> RequiredChromosomeIds,
    IReadOnlySet<string> RequiredTraitIds);

public sealed record CompatibilityContract(
    string SpeciesA,
    string SpeciesB,
    bool Permitted,
    string OffspringSpeciesId,
    string Rationale);

public sealed record SpeciesAuthoringRecord(
    string SpeciesId,
    BeingKind Kind,
    SpeciesActivationState ActivationState,
    GenomeFamilyProfile GenomeFamily,
    LifecycleProfile Lifecycle,
    IReadOnlyList<CompatibilityContract> Compatibility,
    IReadOnlyDictionary<string, double> SelectionWeights,
    string ContinuityStatus,
    string ReviewAuthority);

public sealed class SpeciesAuthoringCatalog
{
    private readonly Dictionary<string, SpeciesAuthoringRecord> _records =
        new(StringComparer.Ordinal);

    public IReadOnlyCollection<SpeciesAuthoringRecord> Records => _records.Values;

    public void Register(SpeciesAuthoringRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        ValidateRecord(record);
        if (!_records.TryAdd(record.SpeciesId, record))
            throw new InvalidOperationException($"Species authoring record '{record.SpeciesId}' already exists.");
    }

    public SpeciesAuthoringRecord Get(string speciesId) =>
        _records.TryGetValue(speciesId, out var value)
            ? value
            : throw new KeyNotFoundException($"Unknown species authoring record '{speciesId}'.");

    public bool IsActive(string speciesId) =>
        Get(speciesId).ActivationState == SpeciesActivationState.Active;

    private static void ValidateRecord(SpeciesAuthoringRecord record)
    {
        if (string.IsNullOrWhiteSpace(record.SpeciesId))
            throw new InvalidOperationException("Species ID is required.");
        if (string.IsNullOrWhiteSpace(record.GenomeFamily.GenomeFamilyId))
            throw new InvalidOperationException($"Species '{record.SpeciesId}' requires a genome family ID.");
        if (string.IsNullOrWhiteSpace(record.GenomeFamily.GenomeSchemaId))
            throw new InvalidOperationException($"Species '{record.SpeciesId}' requires a genome schema ID.");
        if (record.Lifecycle.MinimumReproductivePhaseAge < 0)
            throw new InvalidOperationException($"Species '{record.SpeciesId}' has a negative reproductive age.");
        if (record.Lifecycle.GenerationIntervalPhases <= 0)
            throw new InvalidOperationException($"Species '{record.SpeciesId}' requires a positive generation interval.");
        if (record.Lifecycle.BaseFecundity is < 0.0 or > 1.0)
            throw new InvalidOperationException($"Species '{record.SpeciesId}' has invalid fecundity.");
        if (record.Lifecycle.MinimumPopulationHealth is < 0.0 or > 1.0)
            throw new InvalidOperationException($"Species '{record.SpeciesId}' has invalid health threshold.");
        if (record.Lifecycle.MaximumExpectedPhaseAge < record.Lifecycle.MinimumReproductivePhaseAge)
            throw new InvalidOperationException($"Species '{record.SpeciesId}' has an invalid lifecycle age range.");
        foreach (var weight in record.SelectionWeights)
        {
            if (weight.Value is < -1.0 or > 1.0)
                throw new InvalidOperationException($"Selection weight '{weight.Key}' for '{record.SpeciesId}' must be between -1 and 1.");
        }
    }
}

public sealed class SpeciesActivationGate
{
    private readonly SpeciesAuthoringCatalog _catalog;

    public SpeciesActivationGate(SpeciesAuthoringCatalog catalog)
    {
        _catalog = catalog;
    }

    public SpeciesProfile Activate(string speciesId)
    {
        var record = _catalog.Get(speciesId);
        if (record.ActivationState != SpeciesActivationState.Active)
            throw new InvalidOperationException($"Species '{speciesId}' is not approved for authoritative simulation.");

        var compatible = record.Compatibility
            .Where(x => x.Permitted)
            .Select(x => StringComparer.Ordinal.Equals(x.SpeciesA, speciesId) ? x.SpeciesB : x.SpeciesA)
            .Where(x => !StringComparer.Ordinal.Equals(x, speciesId))
            .ToHashSet(StringComparer.Ordinal);

        return new SpeciesProfile(
            record.SpeciesId,
            record.Kind,
            SpeciesProfileStatus.Authored,
            record.GenomeFamily.GenomeSchemaId,
            record.Lifecycle.MinimumReproductivePhaseAge,
            record.Lifecycle.GenerationIntervalPhases,
            record.Lifecycle.BaseFecundity,
            record.Lifecycle.MinimumPopulationHealth,
            compatible);
    }
}

public sealed record EvolutionaryPressureProfile(
    string ZoneId,
    string SpeciesId,
    IReadOnlyDictionary<string, double> TraitPressures,
    double ReproductiveModifier,
    double ViabilityModifier);

public sealed class EvolutionaryPressureResolver
{
    public EvolutionaryPressureProfile Resolve(
        ZoneEnvironment zone,
        SpeciesAuthoringRecord species)
    {
        var pressures = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var trait in species.SelectionWeights)
        {
            double environmental = zone.TraitPressures.GetValueOrDefault(trait.Key);
            pressures[trait.Key] = Math.Clamp(trait.Value + environmental, -1.0, 1.0);
        }

        double fitness = Math.Clamp(
            (zone.PopulationHealth + zone.Stability + zone.Integrity) / 3.0,
            0.0,
            1.0);

        double resonancePenalty = Math.Clamp(zone.ResonanceDebt * 0.35, 0.0, 0.5);
        double reproduction = Math.Clamp(fitness * (1.0 - resonancePenalty), 0.0, 1.0);
        double viability = Math.Clamp((zone.PopulationHealth + zone.Integrity) / 2.0, 0.0, 1.0);

        return new EvolutionaryPressureProfile(
            zone.ZoneId,
            species.SpeciesId,
            pressures,
            reproduction,
            viability);
    }
}

public static class AzerothSpeciesAuthoringBootstrap
{
    public static SpeciesAuthoringCatalog Create()
    {
        var catalog = new SpeciesAuthoringCatalog();

        var family = new GenomeFamilyProfile(
            "azeroth.elven.sequence-family",
            AzerothEvolutionBootstrap.ElvenGenomeSchema,
            new HashSet<string>(StringComparer.Ordinal) { "EA-05" },
            new HashSet<string>(StringComparer.Ordinal)
            {
                "arcane_precision",
                "arcane_conservation",
                "resonance_amplification"
            });

        var lifecycle = new LifecycleProfile(
            MinimumReproductivePhaseAge: 20,
            GenerationIntervalPhases: 20,
            BaseFecundity: 0.18,
            MinimumPopulationHealth: 0.45,
            MaximumExpectedPhaseAge: 1000);

        var compatibility = new[]
        {
            new CompatibilityContract(
                "azeroth.elf.high",
                "azeroth.elf.sindorei",
                true,
                "azeroth.elf.convergent",
                "Explicitly authored shared elven sequence-family convergence."),
            new CompatibilityContract(
                "azeroth.elf.high",
                "azeroth.elf.convergent",
                true,
                "azeroth.elf.convergent",
                "Explicitly authored continuation within the shared elven sequence family."),
            new CompatibilityContract(
                "azeroth.elf.sindorei",
                "azeroth.elf.convergent",
                true,
                "azeroth.elf.convergent",
                "Explicitly authored continuation within the shared elven sequence family."),
            new CompatibilityContract(
                "azeroth.elf.high",
                "azeroth.elf.high",
                true,
                "azeroth.elf.high",
                "Same authored species profile."),
            new CompatibilityContract(
                "azeroth.elf.sindorei",
                "azeroth.elf.sindorei",
                true,
                "azeroth.elf.sindorei",
                "Same authored species profile."),
            new CompatibilityContract(
                "azeroth.elf.convergent",
                "azeroth.elf.convergent",
                true,
                "azeroth.elf.convergent",
                "Same authored convergent profile.")
        };

        catalog.Register(new SpeciesAuthoringRecord(
            "azeroth.elf.high",
            BeingKind.Person,
            SpeciesActivationState.Active,
            family,
            lifecycle,
            compatibility,
            new Dictionary<string, double>(StringComparer.Ordinal)
            {
                ["arcane_conservation"] = 0.12,
                ["arcane_precision"] = 0.08
            },
            "License-Gated Working Continuity",
            "World Grimoire / Species Review Gate"));

        catalog.Register(new SpeciesAuthoringRecord(
            "azeroth.elf.sindorei",
            BeingKind.Person,
            SpeciesActivationState.Active,
            family,
            lifecycle,
            compatibility,
            new Dictionary<string, double>(StringComparer.Ordinal)
            {
                ["resonance_amplification"] = 0.12,
                ["arcane_precision"] = 0.08
            },
            "License-Gated Working Continuity",
            "World Grimoire / Species Review Gate"));

        catalog.Register(new SpeciesAuthoringRecord(
            "azeroth.elf.convergent",
            BeingKind.Person,
            SpeciesActivationState.Active,
            family,
            lifecycle with { BaseFecundity = 0.17 },
            compatibility,
            new Dictionary<string, double>(StringComparer.Ordinal)
            {
                ["arcane_conservation"] = 0.06,
                ["resonance_amplification"] = 0.06,
                ["arcane_precision"] = 0.08
            },
            "License-Gated Working Continuity",
            "World Grimoire / Species Review Gate"));

        RegisterDeferredTemplate(catalog, "azeroth.deferred.person", BeingKind.Person);
        RegisterDeferredTemplate(catalog, "azeroth.deferred.animal", BeingKind.Animal);
        RegisterDeferredTemplate(catalog, "azeroth.deferred.plant", BeingKind.Plant);
        RegisterDeferredTemplate(catalog, "azeroth.deferred.fungus", BeingKind.Fungus);
        RegisterDeferredTemplate(catalog, "azeroth.deferred.other", BeingKind.Other);

        return catalog;
    }

    private static void RegisterDeferredTemplate(
        SpeciesAuthoringCatalog catalog,
        string speciesId,
        BeingKind kind)
    {
        catalog.Register(new SpeciesAuthoringRecord(
            speciesId,
            kind,
            SpeciesActivationState.Deferred,
            new GenomeFamilyProfile(
                $"{speciesId}.unresolved-family",
                $"{speciesId}.unresolved-schema",
                new HashSet<string>(StringComparer.Ordinal),
                new HashSet<string>(StringComparer.Ordinal)),
            new LifecycleProfile(
                int.MaxValue - 1,
                int.MaxValue,
                0.0,
                1.0,
                int.MaxValue),
            Array.Empty<CompatibilityContract>(),
            new Dictionary<string, double>(StringComparer.Ordinal),
            "Deferred",
            "Species Review Gate"));
    }
}
