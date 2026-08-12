using Evermore.Genetics;

namespace EasternKingdoms.Simulation;

public enum CellSimulationState { Cold, Warm, Hot, Locked }
public enum BeingKind { Person, Animal, Plant, Fungus, Other }
public enum WorldCycleStage { Ingest, Normalize, Store, Decide, Execute, Observe, Learn, Reconstruct }
public enum SpeciesProfileStatus { Authored, Deferred }

public sealed record SpeciesProfile(
    string SpeciesId,
    BeingKind Kind,
    SpeciesProfileStatus Status,
    string GenomeSchemaId,
    int MinimumReproductivePhaseAge,
    int GenerationIntervalPhases,
    double BaseFecundity,
    double MinimumPopulationHealth,
    IReadOnlySet<string> CompatibleSpeciesIds);

public sealed record BeingGenome(
    string BeingId,
    string SpeciesId,
    string LineageId,
    GenomeDefinition Genome,
    int BirthPhase,
    string HomeZoneId,
    bool Alive = true);

public sealed record ZoneEnvironment(
    string ZoneId,
    CellSimulationState SimulationState,
    double PopulationHealth,
    double Stability,
    double Luminosity,
    double Integrity,
    double ResonanceDebt,
    IReadOnlyDictionary<string, double> TraitPressures);

public sealed record EvolutionEvent(
    long Sequence,
    int Phase,
    WorldCycleStage Stage,
    string EventType,
    string ZoneId,
    string SubjectId,
    string Detail);

public sealed record AlleleFrequencyRecord(
    string ZoneId,
    string SpeciesId,
    string AlleleId,
    int Count,
    double Frequency);

public sealed record EvolutionSnapshot(
    int Phase,
    IReadOnlyList<BeingGenome> Beings,
    IReadOnlyList<EvolutionEvent> Events,
    IReadOnlyList<AlleleFrequencyRecord> AlleleFrequencies);

public sealed class AzerothLifeRegistry
{
    private readonly Dictionary<string, SpeciesProfile> _species = new(StringComparer.Ordinal);
    private readonly Dictionary<string, BeingGenome> _beings = new(StringComparer.Ordinal);

    public IReadOnlyCollection<SpeciesProfile> Species => _species.Values;
    public IReadOnlyCollection<BeingGenome> Beings => _beings.Values;

    public void RegisterSpecies(SpeciesProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (string.IsNullOrWhiteSpace(profile.SpeciesId))
            throw new InvalidOperationException("Species ID is required.");
        if (string.IsNullOrWhiteSpace(profile.GenomeSchemaId))
            throw new InvalidOperationException($"Species '{profile.SpeciesId}' requires a genome schema ID.");
        if (profile.BaseFecundity is < 0.0 or > 1.0)
            throw new InvalidOperationException($"Species '{profile.SpeciesId}' has invalid fecundity.");
        if (profile.MinimumPopulationHealth is < 0.0 or > 1.0)
            throw new InvalidOperationException($"Species '{profile.SpeciesId}' has invalid health threshold.");
        if (!_species.TryAdd(profile.SpeciesId, profile))
            throw new InvalidOperationException($"Species '{profile.SpeciesId}' already exists.");
    }

    public SpeciesProfile GetSpecies(string speciesId) =>
        _species.TryGetValue(speciesId, out var profile)
            ? profile
            : throw new KeyNotFoundException($"Unknown species '{speciesId}'.");

    public void AddBeing(BeingGenome being)
    {
        ArgumentNullException.ThrowIfNull(being);
        var species = GetSpecies(being.SpeciesId);
        if (species.Status != SpeciesProfileStatus.Authored)
            throw new InvalidOperationException($"Species '{being.SpeciesId}' is Deferred and cannot enter authoritative biological simulation.");
        if (!_beings.TryAdd(being.BeingId, being))
            throw new InvalidOperationException($"Being '{being.BeingId}' already exists.");
    }

    public IReadOnlyList<BeingGenome> InZone(string zoneId) =>
        _beings.Values
            .Where(x => x.Alive && StringComparer.Ordinal.Equals(x.HomeZoneId, zoneId))
            .OrderBy(x => x.BeingId, StringComparer.Ordinal)
            .ToArray();
}

public sealed class SpeciesCompatibilityGate
{
    private readonly AzerothLifeRegistry _life;

    public SpeciesCompatibilityGate(AzerothLifeRegistry life) => _life = life;

    public bool CanReproduce(BeingGenome a, BeingGenome b, ZoneEnvironment zone, int phase)
    {
        if (!a.Alive || !b.Alive) return false;
        if (zone.SimulationState is CellSimulationState.Cold or CellSimulationState.Locked) return false;
        if (!StringComparer.Ordinal.Equals(a.HomeZoneId, zone.ZoneId) || !StringComparer.Ordinal.Equals(b.HomeZoneId, zone.ZoneId)) return false;

        var sa = _life.GetSpecies(a.SpeciesId);
        var sb = _life.GetSpecies(b.SpeciesId);
        if (sa.Status != SpeciesProfileStatus.Authored || sb.Status != SpeciesProfileStatus.Authored) return false;
        if (!StringComparer.Ordinal.Equals(sa.GenomeSchemaId, sb.GenomeSchemaId)) return false;
        if (phase - a.BirthPhase < sa.MinimumReproductivePhaseAge || phase - b.BirthPhase < sb.MinimumReproductivePhaseAge) return false;
        if (zone.PopulationHealth < Math.Max(sa.MinimumPopulationHealth, sb.MinimumPopulationHealth)) return false;

        if (StringComparer.Ordinal.Equals(a.SpeciesId, b.SpeciesId)) return true;
        return sa.CompatibleSpeciesIds.Contains(b.SpeciesId) && sb.CompatibleSpeciesIds.Contains(a.SpeciesId);
    }
}

public sealed class EasternKingdomsEvolutionEngine
{
    private readonly AzerothLifeRegistry _life;
    private readonly GenomeCatalog _catalog;
    private readonly GenomeRecombiner _recombiner;
    private readonly GenomeValidator _validator;
    private readonly ExpressionEngine _expression;
    private readonly SpeciesCompatibilityGate _compatibility;
    private readonly SpeciesEcologyCatalog? _ecology;
    private readonly ZoneFoodWebBuilder _foodWebBuilder = new();
    private readonly FoodWebResolver _foodWebResolver = new();
    private readonly List<EvolutionEvent> _events = new();
    private long _eventSequence;

    public EasternKingdomsEvolutionEngine(
        AzerothLifeRegistry life,
        GenomeCatalog catalog,
        GenomeRecombiner recombiner,
        GenomeValidator validator,
        ExpressionEngine expression,
        SpeciesEcologyCatalog? ecology = null)
    {
        _life = life;
        _catalog = catalog;
        _recombiner = recombiner;
        _validator = validator;
        _expression = expression;
        _compatibility = new SpeciesCompatibilityGate(life);
        _ecology = ecology;
    }

    public EvolutionSnapshot AdvancePhase(
        int phase,
        IReadOnlyList<ZoneEnvironment> zones,
        ulong simulationSeed)
    {
        if (phase <= 0) throw new ArgumentOutOfRangeException(nameof(phase));

        foreach (var zone in zones.OrderBy(x => x.ZoneId, StringComparer.Ordinal))
            RunEightfold(zone, phase, simulationSeed);

        return new EvolutionSnapshot(
            phase,
            _life.Beings.OrderBy(x => x.BeingId, StringComparer.Ordinal).ToArray(),
            _events.Where(x => x.Phase == phase).OrderBy(x => x.Sequence).ToArray(),
            BuildAlleleFrequencies());
    }

    private void RunEightfold(ZoneEnvironment zone, int phase, ulong simulationSeed)
    {
        Emit(phase, WorldCycleStage.Ingest, "environment_ingested", zone.ZoneId, zone.ZoneId,
            $"state={zone.SimulationState};health={zone.PopulationHealth:F3};stability={zone.Stability:F3};integrity={zone.Integrity:F3};resonanceDebt={zone.ResonanceDebt:F3}");

        var residents = _life.InZone(zone.ZoneId);
        Emit(phase, WorldCycleStage.Normalize, "population_normalized", zone.ZoneId, zone.ZoneId, $"residentCount={residents.Count}");

        foreach (var being in residents)
            _validator.Validate(being.Genome, _catalog);
        Emit(phase, WorldCycleStage.Store, "genomes_validated", zone.ZoneId, zone.ZoneId, $"validated={residents.Count}");

        if (zone.SimulationState == CellSimulationState.Locked)
        {
            Emit(phase, WorldCycleStage.Decide, "heredity_locked", zone.ZoneId, zone.ZoneId, "Locked cells accept no hereditary writes.");
            Emit(phase, WorldCycleStage.Reconstruct, "population_reconstructed", zone.ZoneId, zone.ZoneId, "Identity and sequence preserved without mutation.");
            return;
        }

        EvaluateExpressions(zone, residents, phase);

        if (zone.SimulationState == CellSimulationState.Cold)
        {
            Emit(phase, WorldCycleStage.Decide, "coarse_evolution_only", zone.ZoneId, zone.ZoneId,
                "Cold cells retain aggregate allele state; no individual births are committed.");
        }
        else if (zone.SimulationState == CellSimulationState.Hot)
        {
            ExecuteReproduction(zone, residents, phase, simulationSeed);
        }
        else if (zone.SimulationState == CellSimulationState.Warm && phase % 5 == 0)
        {
            ExecuteReproduction(zone, residents, phase, simulationSeed);
        }

        Emit(phase, WorldCycleStage.Observe, "population_observed", zone.ZoneId, zone.ZoneId,
            $"postPhasePopulation={_life.InZone(zone.ZoneId).Count}");
        Emit(phase, WorldCycleStage.Learn, "selection_memory_updated", zone.ZoneId, zone.ZoneId,
            "Environment influences expression and reproductive probability; sequence changes only through offspring construction.");
        Emit(phase, WorldCycleStage.Reconstruct, "population_reconstructed", zone.ZoneId, zone.ZoneId,
            "Next authoritative population state committed deterministically.");
    }

    private void EvaluateExpressions(ZoneEnvironment zone, IReadOnlyList<BeingGenome> residents, int phase)
    {
        foreach (var being in residents)
        {
            var species = _life.GetSpecies(being.SpeciesId);
            int age = Math.Max(0, phase - being.BirthPhase);
            double development = Math.Clamp(age / (double)Math.Max(1, species.MinimumReproductivePhaseAge), 0.05, 1.0);
            double environment = Math.Clamp((zone.PopulationHealth + zone.Stability + zone.Integrity) / 3.0, 0.0, 1.0);
            double arcane = Math.Clamp(zone.Luminosity * (1.0 - zone.ResonanceDebt * 0.35), 0.0, 1.0);

            var expressions = _expression.Evaluate(
                being.Genome,
                _catalog,
                new ExpressionContext(DevelopmentStage.Adult, development, environment, Training: 0.5, ArcaneState: arcane));

            double pressureScore = expressions.Sum(x =>
                zone.TraitPressures.TryGetValue(x.TraitId, out var pressure) ? x.FinalExpression * pressure : 0.0);

            Emit(phase, WorldCycleStage.Decide, "expression_evaluated", zone.ZoneId, being.BeingId,
                $"traits={expressions.Count};ecologicalPressureScore={pressureScore:F4}");
        }
    }

    private void ExecuteReproduction(ZoneEnvironment zone, IReadOnlyList<BeingGenome> residents, int phase, ulong simulationSeed)
    {
        var eligible = residents
            .Where(x => phase - x.BirthPhase >= _life.GetSpecies(x.SpeciesId).MinimumReproductivePhaseAge)
            .OrderBy(x => x.SpeciesId, StringComparer.Ordinal)
            .ThenBy(x => x.BeingId, StringComparer.Ordinal)
            .ToArray();

        int birthIndex = 0;
        for (int i = 0; i + 1 < eligible.Length; i += 2)
        {
            var a = eligible[i];
            var b = eligible[i + 1];
            if (!_compatibility.CanReproduce(a, b, zone, phase)) continue;

            var speciesA = _life.GetSpecies(a.SpeciesId);
            var speciesB = _life.GetSpecies(b.SpeciesId);
            int interval = Math.Max(speciesA.GenerationIntervalPhases, speciesB.GenerationIntervalPhases);
            if (phase % Math.Max(1, interval) != 0) continue;

            ulong pairSeed = StableSeed.FromText($"{simulationSeed}|{phase}|{zone.ZoneId}|{a.BeingId}|{b.BeingId}");
            ulong seed = SeedMixer.Mix(pairSeed, (ulong)phase, (ulong)birthIndex);
            var random = new GenomeRandom(seed);

            double environmentalFitness = Math.Clamp((zone.PopulationHealth + zone.Stability + zone.Integrity) / 3.0, 0.0, 1.0);
            double resonancePenalty = Math.Clamp(zone.ResonanceDebt * 0.35, 0.0, 0.5);
            double baseFecundity = (speciesA.BaseFecundity + speciesB.BaseFecundity) / 2.0;
            double ecologyModifier = ResolveEcologyReproductiveModifier(zone, a.SpeciesId, b.SpeciesId);
            double birthProbability = Math.Clamp(baseFecundity * environmentalFitness * (1.0 - resonancePenalty) * ecologyModifier, 0.0, 1.0);
            if (!random.Chance(birthProbability)) continue;

            string childId = $"{zone.ZoneId}.born.p{phase:D4}.{birthIndex:D4}";
            var childGenome = _recombiner.Recombine(
                $"genome.{childId}",
                a.Genome,
                b.Genome,
                _catalog,
                new RecombinationSettings(seed));
            _validator.Validate(childGenome, _catalog);

            string speciesId = ResolveOffspringSpecies(a, b);
            _life.AddBeing(new BeingGenome(
                childId,
                speciesId,
                ResolveLineageId(a, b),
                childGenome,
                phase,
                zone.ZoneId));

            Emit(phase, WorldCycleStage.Execute, "birth_committed", zone.ZoneId, childId,
                $"parentA={a.BeingId};parentB={b.BeingId};species={speciesId};genome={childGenome.GenomeId};seed={seed}");
            birthIndex++;
        }
    }

    private double ResolveEcologyReproductiveModifier(ZoneEnvironment zone, string speciesA, string speciesB)
    {
        if (_ecology is null) return 1.0;
        var state = _foodWebBuilder.Build(zone);
        double total = 0.0;
        int count = 0;
        foreach (var speciesId in new[] { speciesA, speciesB })
        {
            if (!_ecology.TryGet(speciesId, out var profile) || profile.Status != SpeciesEcologyStatus.Active)
                continue;
            var outcome = _foodWebResolver.Resolve(state, profile);
            if (!outcome.FoodWebComplete)
                throw new InvalidOperationException($"Food web for {speciesId} is incomplete in zone {zone.ZoneId}.");
            total += outcome.ReproductiveModifier;
            count++;
        }
        return count == 0 ? 1.0 : Math.Clamp(total / count, 0.0, 1.25);
    }

    private string ResolveOffspringSpecies(BeingGenome a, BeingGenome b)
    {
        if (StringComparer.Ordinal.Equals(a.SpeciesId, b.SpeciesId)) return a.SpeciesId;
        var sa = _life.GetSpecies(a.SpeciesId);
        var sb = _life.GetSpecies(b.SpeciesId);
        if (!StringComparer.Ordinal.Equals(sa.GenomeSchemaId, sb.GenomeSchemaId))
            throw new InvalidOperationException("Cross-schema offspring cannot be created.");
        return "azeroth.elf.convergent";
    }

    private static string ResolveLineageId(BeingGenome a, BeingGenome b) =>
        StringComparer.Ordinal.Equals(a.LineageId, b.LineageId)
            ? a.LineageId
            : $"convergent:{a.LineageId}+{b.LineageId}";

    private IReadOnlyList<AlleleFrequencyRecord> BuildAlleleFrequencies()
    {
        var output = new List<AlleleFrequencyRecord>();
        foreach (var zoneGroup in _life.Beings.Where(x => x.Alive).GroupBy(x => x.HomeZoneId, StringComparer.Ordinal))
        foreach (var speciesGroup in zoneGroup.GroupBy(x => x.SpeciesId, StringComparer.Ordinal))
        {
            var alleles = speciesGroup
                .SelectMany(x => x.Genome.Chromosomes)
                .SelectMany(x => x.Loci)
                .SelectMany(x => new[] { x.Alleles.ParentA, x.Alleles.ParentB })
                .ToArray();
            foreach (var group in alleles.GroupBy(x => x, StringComparer.Ordinal).OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                output.Add(new AlleleFrequencyRecord(
                    zoneGroup.Key,
                    speciesGroup.Key,
                    group.Key,
                    group.Count(),
                    alleles.Length == 0 ? 0.0 : group.Count() / (double)alleles.Length));
            }
        }
        return output;
    }

    private void Emit(int phase, WorldCycleStage stage, string eventType, string zoneId, string subjectId, string detail)
    {
        _events.Add(new EvolutionEvent(_eventSequence++, phase, stage, eventType, zoneId, subjectId, detail));
    }
}

public static class StableSeed
{
    public static ulong FromText(string text)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text));
        return BitConverter.ToUInt64(bytes, 0);
    }
}
