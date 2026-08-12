using Evermore.Genetics;

namespace EasternKingdoms.Simulation;

public sealed record PopulationDiversitySnapshot(
    string CohortId,
    double MeanExpectedHeterozygosity,
    int AlleleRichness,
    int PolymorphicLoci);

public sealed record FounderEffectReport(
    string SpeciesId,
    string SourceZoneId,
    string DestinationZoneId,
    long FounderPopulation,
    double SourceDiversity,
    double FounderDiversity,
    double DiversityRetention,
    int SourceAlleleRichness,
    int FounderAlleleRichness);

public sealed class CohortDiversityAnalyzer
{
    public PopulationDiversitySnapshot Analyze(EvolutionCohort cohort)
    {
        ArgumentNullException.ThrowIfNull(cohort);
        var heterozygosity = new List<double>();
        int richness = 0;
        int polymorphic = 0;

        foreach (var locus in cohort.AlleleFrequencies
                     .GroupBy(x => x.LocusId, StringComparer.Ordinal)
                     .OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            var frequencies = locus.Select(x => Math.Max(0.0, x.Frequency)).ToArray();
            double total = frequencies.Sum();
            if (total <= 0.0)
                continue;

            var normalized = frequencies.Select(x => x / total).ToArray();
            heterozygosity.Add(1.0 - normalized.Sum(x => x * x));
            int localRichness = normalized.Count(x => x > 0.0000001);
            richness += localRichness;
            if (localRichness > 1)
                polymorphic++;
        }

        return new PopulationDiversitySnapshot(
            cohort.CohortId,
            heterozygosity.DefaultIfEmpty(0.0).Average(),
            richness,
            polymorphic);
    }
}

public sealed class FounderEffectAnalyzer
{
    private readonly CohortDiversityAnalyzer _diversity = new();

    public FounderEffectReport Analyze(CohortMigrationResult migration)
    {
        ArgumentNullException.ThrowIfNull(migration);
        if (!migration.Recolonized)
            throw new InvalidOperationException("Founder-effect analysis requires a recolonization event.");

        var source = _diversity.Analyze(migration.SourceBefore);
        var founder = _diversity.Analyze(migration.DestinationAfter);
        double retention = source.MeanExpectedHeterozygosity <= 0.0
            ? 1.0
            : Math.Clamp(
                founder.MeanExpectedHeterozygosity / source.MeanExpectedHeterozygosity,
                0.0,
                1.0);

        return new FounderEffectReport(
            migration.SourceBefore.SpeciesId,
            migration.SourceBefore.ZoneId,
            migration.DestinationAfter.ZoneId,
            migration.Arrived,
            source.MeanExpectedHeterozygosity,
            founder.MeanExpectedHeterozygosity,
            retention,
            source.AlleleRichness,
            founder.AlleleRichness);
    }
}

public sealed record PairwiseGeneticDivergence(
    string SpeciesId,
    string ZoneA,
    string ZoneB,
    double MeanTotalVariationDistance,
    double SimulationFst);

public sealed class GeneticDivergenceAnalyzer
{
    public IReadOnlyList<PairwiseGeneticDivergence> Analyze(IEnumerable<EvolutionCohort> cohorts)
    {
        var active = cohorts
            .Where(x => x.Population > 0)
            .OrderBy(x => x.ZoneId, StringComparer.Ordinal)
            .ToArray();

        if (active.Select(x => x.SpeciesId).Distinct(StringComparer.Ordinal).Skip(1).Any())
            throw new InvalidOperationException("Pairwise divergence analysis requires one species at a time.");

        var output = new List<PairwiseGeneticDivergence>();
        for (int i = 0; i < active.Length; i++)
        for (int j = i + 1; j < active.Length; j++)
            output.Add(Compare(active[i], active[j]));
        return output;
    }

    public PairwiseGeneticDivergence Compare(EvolutionCohort a, EvolutionCohort b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        if (!StringComparer.Ordinal.Equals(a.SpeciesId, b.SpeciesId))
            throw new InvalidOperationException("Genetic divergence can only compare cohorts of the same species.");

        var loci = a.AlleleFrequencies.Select(x => x.LocusId)
            .Concat(b.AlleleFrequencies.Select(x => x.LocusId))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        var tvd = new List<double>();
        var fst = new List<double>();
        foreach (string locusId in loci)
        {
            var mapA = NormalizeLocus(a, locusId);
            var mapB = NormalizeLocus(b, locusId);
            var alleles = mapA.Keys.Concat(mapB.Keys)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray();

            double distance = 0.5 * alleles.Sum(allele =>
                Math.Abs(mapA.GetValueOrDefault(allele) - mapB.GetValueOrDefault(allele)));
            tvd.Add(Math.Clamp(distance, 0.0, 1.0));

            double hA = 1.0 - alleles.Sum(allele => Math.Pow(mapA.GetValueOrDefault(allele), 2));
            double hB = 1.0 - alleles.Sum(allele => Math.Pow(mapB.GetValueOrDefault(allele), 2));
            double hS = (hA + hB) * 0.5;
            double hT = 1.0 - alleles.Sum(allele =>
            {
                double mean = (mapA.GetValueOrDefault(allele) + mapB.GetValueOrDefault(allele)) * 0.5;
                return mean * mean;
            });
            fst.Add(hT <= 0.0000001 ? 0.0 : Math.Clamp((hT - hS) / hT, 0.0, 1.0));
        }

        return new PairwiseGeneticDivergence(
            a.SpeciesId,
            a.ZoneId,
            b.ZoneId,
            tvd.DefaultIfEmpty(0.0).Average(),
            fst.DefaultIfEmpty(0.0).Average());
    }

    private static Dictionary<string, double> NormalizeLocus(EvolutionCohort cohort, string locusId)
    {
        var result = cohort.AlleleFrequencies
            .Where(x => StringComparer.Ordinal.Equals(x.LocusId, locusId))
            .GroupBy(x => x.AlleleId, StringComparer.Ordinal)
            .ToDictionary(
                x => x.Key,
                x => x.Sum(v => Math.Max(0.0, v.Frequency)),
                StringComparer.Ordinal);

        double total = result.Values.Sum();
        if (total <= 0.0)
            return result;
        foreach (string key in result.Keys.ToArray())
            result[key] /= total;
        return result;
    }
}
