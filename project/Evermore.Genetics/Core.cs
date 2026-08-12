using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;

namespace Evermore.Genetics;

public enum ElvenLineage { Ancestral, HighElf, SinDorei, Convergent }
public enum AlleleInteraction { Dominant, Recessive, Codominant, Blended, Contextual, Latent, Suppressed, Amplified, Novel }
public enum DevelopmentStage { Infant, Child, Adolescent, YoungAdult, Adult, Mature }

public sealed record AlleleDefinition(string Id, string LocusId, AlleleInteraction Interaction, double Penetrance, IReadOnlyDictionary<string,double> Effects);
public sealed record LocusDefinition(string Id, string GeneId, long Position, IReadOnlyList<string> ValidAlleles);
public sealed record ChromosomeDefinition(string Id, string Name, IReadOnlyList<LocusDefinition> Loci);
public sealed record AllelePair(string ParentA, string ParentB);
public sealed record SequencedLocus(string LocusId, AllelePair Alleles);
public sealed record SequencedChromosome(string ChromosomeId, IReadOnlyList<SequencedLocus> Loci);
public sealed record GenomeDefinition(string GenomeId, ElvenLineage Lineage, int Generation, IReadOnlyList<SequencedChromosome> Chromosomes);
public sealed record RecombinationSettings(ulong Seed, double MutationRate = 0.002, double MaximumMutationEffect = 0.15);

public sealed class GenomeCatalog
{
    private readonly Dictionary<string,ChromosomeDefinition> _chromosomes;
    private readonly Dictionary<string,LocusDefinition> _loci;
    private readonly Dictionary<string,AlleleDefinition> _alleles;
    public GenomeCatalog(IEnumerable<ChromosomeDefinition> chromosomes, IEnumerable<AlleleDefinition> alleles)
    {
        _chromosomes = chromosomes.ToDictionary(x => x.Id, StringComparer.Ordinal);
        _loci = _chromosomes.Values.SelectMany(x => x.Loci).ToDictionary(x => x.Id, StringComparer.Ordinal);
        _alleles = alleles.ToDictionary(x => x.Id, StringComparer.Ordinal);
    }
    public ChromosomeDefinition GetChromosome(string id) => _chromosomes.TryGetValue(id, out var v) ? v : throw new KeyNotFoundException($"Unknown chromosome {id}.");
    public LocusDefinition GetLocus(string id) => _loci.TryGetValue(id, out var v) ? v : throw new KeyNotFoundException($"Unknown locus {id}.");
    public AlleleDefinition GetAllele(string id) => _alleles.TryGetValue(id, out var v) ? v : throw new KeyNotFoundException($"Unknown allele {id}.");
}

public sealed class GenomeRandom
{
    private ulong _state;
    public GenomeRandom(ulong seed) => _state = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;
    public ulong NextUInt64()
    {
        unchecked
        {
            ulong z = (_state += 0x9E3779B97F4A7C15UL);
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
    public double NextDouble() => (NextUInt64() >> 11) * (1.0 / (1UL << 53));
    public bool Chance(double probability) => NextDouble() < Math.Clamp(probability, 0.0, 1.0);
    public T Choose<T>(IReadOnlyList<T> values) => values.Count == 0 ? throw new InvalidOperationException("Cannot choose from an empty set.") : values[(int)(NextUInt64() % (ulong)values.Count)];
}

public sealed record Gamete(IReadOnlyDictionary<string,string> Alleles);
public interface IGameteGenerator { Gamete Generate(GenomeDefinition parent, GenomeCatalog catalog, GenomeRandom random); }

public sealed class LinkedGameteGenerator : IGameteGenerator
{
    public Gamete Generate(GenomeDefinition parent, GenomeCatalog catalog, GenomeRandom random)
    {
        var result = new Dictionary<string,string>(StringComparer.Ordinal);
        foreach (var chromosome in parent.Chromosomes)
        {
            bool useA = random.Chance(0.5);
            foreach (var locus in chromosome.Loci.OrderBy(x => catalog.GetLocus(x.LocusId).Position))
            {
                if (random.Chance(0.10)) useA = !useA;
                result[locus.LocusId] = useA ? locus.Alleles.ParentA : locus.Alleles.ParentB;
            }
        }
        return new Gamete(new ReadOnlyDictionary<string,string>(result));
    }
}

public sealed class MutationEngine
{
    public Gamete Apply(Gamete source, GenomeCatalog catalog, RecombinationSettings settings, GenomeRandom random)
    {
        var result = source.Alleles.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
        foreach (var locusId in source.Alleles.Keys.OrderBy(x => x, StringComparer.Ordinal))
        {
            if (!random.Chance(settings.MutationRate)) continue;
            var locus = catalog.GetLocus(locusId);
            var candidates = locus.ValidAlleles.Where(x => !StringComparer.Ordinal.Equals(x, source.Alleles[locusId])).ToArray();
            if (candidates.Length > 0) result[locusId] = random.Choose(candidates);
        }
        return new Gamete(new ReadOnlyDictionary<string,string>(result));
    }
}

public sealed class GenomeRecombiner
{
    private readonly IGameteGenerator _gametes;
    private readonly MutationEngine _mutation;
    public GenomeRecombiner(IGameteGenerator gametes, MutationEngine mutation) { _gametes = gametes; _mutation = mutation; }
    public GenomeDefinition Recombine(string offspringId, GenomeDefinition a, GenomeDefinition b, GenomeCatalog catalog, RecombinationSettings settings)
    {
        var random = new GenomeRandom(settings.Seed);
        var ga = _mutation.Apply(_gametes.Generate(a, catalog, random), catalog, settings, random);
        var gb = _mutation.Apply(_gametes.Generate(b, catalog, random), catalog, settings, random);
        var chromosomes = new List<SequencedChromosome>();
        foreach (var c in a.Chromosomes)
        {
            var loci = c.Loci.Where(x => ga.Alleles.ContainsKey(x.LocusId) && gb.Alleles.ContainsKey(x.LocusId))
                .Select(x => new SequencedLocus(x.LocusId, new AllelePair(ga.Alleles[x.LocusId], gb.Alleles[x.LocusId]))).ToArray();
            chromosomes.Add(new SequencedChromosome(c.ChromosomeId, loci));
        }
        return new GenomeDefinition(offspringId, ResolveLineage(a.Lineage, b.Lineage), Math.Max(a.Generation,b.Generation)+1, chromosomes);
    }
    private static ElvenLineage ResolveLineage(ElvenLineage a, ElvenLineage b) => a == b ? a : a == ElvenLineage.Ancestral ? b : b == ElvenLineage.Ancestral ? a : ElvenLineage.Convergent;
}

public sealed class GenomeValidator
{
    public void Validate(GenomeDefinition genome, GenomeCatalog catalog)
    {
        if (string.IsNullOrWhiteSpace(genome.GenomeId)) throw new InvalidOperationException("Genome ID is required.");
        if (genome.Generation < 0) throw new InvalidOperationException("Generation cannot be negative.");
        var seenChromosomes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var chromosome in genome.Chromosomes)
        {
            if (!seenChromosomes.Add(chromosome.ChromosomeId)) throw new InvalidOperationException($"Duplicate chromosome {chromosome.ChromosomeId}.");
            catalog.GetChromosome(chromosome.ChromosomeId);
            var seenLoci = new HashSet<string>(StringComparer.Ordinal);
            foreach (var locus in chromosome.Loci)
            {
                if (!seenLoci.Add(locus.LocusId)) throw new InvalidOperationException($"Duplicate locus {locus.LocusId}.");
                var def = catalog.GetLocus(locus.LocusId);
                ValidateAllele(locus.Alleles.ParentA, def, catalog);
                ValidateAllele(locus.Alleles.ParentB, def, catalog);
            }
        }
    }
    private static void ValidateAllele(string alleleId, LocusDefinition locus, GenomeCatalog catalog)
    {
        var allele = catalog.GetAllele(alleleId);
        if (!StringComparer.Ordinal.Equals(allele.LocusId, locus.Id) || !locus.ValidAlleles.Contains(alleleId, StringComparer.Ordinal)) throw new InvalidOperationException($"Allele {alleleId} is invalid for locus {locus.Id}.");
        if (allele.Penetrance is < 0.0 or > 1.0) throw new InvalidOperationException($"Allele {alleleId} has invalid penetrance.");
    }
}

public sealed record ExpressionContext(DevelopmentStage Stage, double Development, double Environment, double Training, double ArcaneState);
public sealed record TraitExpression(string TraitId, double GeneticPotential, double FinalExpression);

public sealed class ExpressionEngine
{
    public IReadOnlyList<TraitExpression> Evaluate(GenomeDefinition genome, GenomeCatalog catalog, ExpressionContext context)
    {
        var traits = new Dictionary<string,double>(StringComparer.Ordinal);
        foreach (var chromosome in genome.Chromosomes)
        foreach (var locus in chromosome.Loci)
        {
            var a = catalog.GetAllele(locus.Alleles.ParentA);
            var b = catalog.GetAllele(locus.Alleles.ParentB);
            foreach (var id in a.Effects.Keys.Concat(b.Effects.Keys).Distinct(StringComparer.Ordinal))
            {
                double ea = a.Effects.GetValueOrDefault(id);
                double eb = b.Effects.GetValueOrDefault(id);
                double value = ((ea + eb) / 2.0) * ((a.Penetrance + b.Penetrance) / 2.0);
                traits[id] = traits.GetValueOrDefault(id) + value;
            }
        }
        return traits.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x =>
        {
            double genetic = Math.Clamp(x.Value, 0, 1);
            double final = genetic * Math.Clamp(context.Development,0,1) * Math.Clamp(context.Environment,0,1) * Math.Clamp(context.ArcaneState,0,1) + Math.Clamp(context.Training,0,1) * 0.20;
            return new TraitExpression(x.Key, genetic, Math.Clamp(final,0,1));
        }).ToArray();
    }
}

public static class SeedMixer
{
    public static ulong Mix(ulong root, ulong generation, ulong index)
    {
        unchecked
        {
            ulong x = root ^ generation * 0x9E3779B97F4A7C15UL ^ index * 0xBF58476D1CE4E5B9UL;
            x ^= x >> 30;
            x *= 0xBF58476D1CE4E5B9UL;
            x ^= x >> 27;
            x *= 0x94D049BB133111EBUL;
            x ^= x >> 31;
            return x;
        }
    }
}

public sealed class GeneticsIntegrityHasher
{
    public string Compute(string canonicalText) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalText)));
}
