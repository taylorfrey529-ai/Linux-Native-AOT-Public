using System.Text.Json;
using Evermore.Cli;
using Omega.Recursive;
using Xunit;

namespace EasternKingdoms.Simulation.Tests;

public sealed class OmegaRecursiveTests
{
    [Fact]
    public void EngineIsDeterministicForTheSameSeed()
    {
        var engine = new OmegaEngine();

        OmegaRunResult first = engine.Run(new OmegaSimulationOptions(529, 4));
        OmegaRunResult second = engine.Run(new OmegaSimulationOptions(529, 4));

        Assert.Equal(first.Disposition, second.Disposition);
        Assert.Equal(first.IntensityHistory, second.IntensityHistory);
        Assert.Equal(
            first.FinalPopulation.Select(entity => entity.Name).ToArray(),
            second.FinalPopulation.Select(entity => entity.Name).ToArray());
        Assert.Equal(
            first.FinalPopulation.SelectMany(entity => entity.Traits).Select(trait => trait.Strength).ToArray(),
            second.FinalPopulation.SelectMany(entity => entity.Traits).Select(trait => trait.Strength).ToArray());
    }

    [Fact]
    public void DefaultRunKeepsPopulationAndTraitStrengthBounds()
    {
        OmegaRunResult result = new OmegaEngine().Run(new OmegaSimulationOptions(7, 3));

        Assert.Equal(24, result.FinalPopulation.Count);
        Assert.All(result.FinalPopulation, entity =>
        {
            Assert.Equal(4, entity.Traits.Count);
            Assert.True(entity.Name.Length <= 63);
            Assert.All(entity.Traits, trait => Assert.InRange(trait.Strength, 0f, 1f));
        });
        Assert.Equal(3, result.Snapshots.Count);
        Assert.True(result.Snapshots[0].IsArchiveCheckpoint);
    }

    [Fact]
    public void CombinePreservesTheSuppliedFirstParentTraitOrder()
    {
        var first = new OmegaEntityProfile("first", CreateTraits("first"));
        var second = new OmegaEntityProfile("second", CreateTraits("second"));

        OmegaEntityProfile child = new SelectionEngine().Combine(first, second);

        Assert.Equal(first.Traits.Select(trait => trait.Name), child.Traits.Select(trait => trait.Name));
        Assert.Equal("first-second", child.Name);
    }

    [Fact]
    public void WeightedEscalationEngagesContainment()
    {
        var population = new[] { new OmegaEntityProfile("test", [new OmegaTrait("Dominion", 0.5f)]) };
        var totals = new Dictionary<string, float>(StringComparer.Ordinal) { ["Dominion"] = 52f };

        OmegaContainmentDecision decision = new OmegaContainmentPolicy().Evaluate(totals, population, 0);

        Assert.True(decision.IsContained);
        Assert.Equal("Trait convergence exceeded safe threshold.", decision.Reason);
    }

    [Fact]
    public void ForbiddenTraitSemanticsEngageContainment()
    {
        var population = new[] { new OmegaEntityProfile("test", [new OmegaTrait("Consume", 0.5f)]) };

        OmegaContainmentDecision decision = new OmegaContainmentPolicy().Evaluate(
            new Dictionary<string, float>(StringComparer.Ordinal),
            population,
            0);

        Assert.True(decision.IsContained);
        Assert.Equal("Forbidden escalation semantic: Consume", decision.Reason);
    }

    [Fact]
    public void GlyphEncodingPreservesTheSuppliedAlphabet()
    {
        Assert.Equal("ᛖᚾᛏᚱᛟᛈᛦ", OmegaGlyphs.EncodeTraitName("Entropy"));
        Assert.Equal("•—", OmegaGlyphs.EncodeTraitName("._"));
    }

    [Fact]
    public async Task CliRunsOmegaWithExplicitDeterministicBounds()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        int exitCode = await EvermoreCliApp.InvokeAsync(
            ["omega", "run", "--generations", "3", "--seed", "529", "--format", "json"],
            output,
            error);

        Assert.Equal(EvermoreCliApp.SuccessExitCode, exitCode);
        Assert.Equal(string.Empty, error.ToString());
        using var document = JsonDocument.Parse(output.ToString());
        Assert.Equal("omega-recursive", document.RootElement.GetProperty("kind").GetString());
        Assert.Equal("completed", document.RootElement.GetProperty("status").GetString());
        Assert.Equal(529UL, document.RootElement.GetProperty("seed").GetUInt64());
        Assert.Equal(24, document.RootElement.GetProperty("population").GetInt32());
    }

    [Fact]
    public async Task CliRejectsUnboundedOmegaGenerationInput()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        int exitCode = await EvermoreCliApp.InvokeAsync(
            ["omega", "run", "--generations", "0", "--seed", "1"],
            output,
            error);

        Assert.Equal(EvermoreCliApp.OmegaInputFailureExitCode, exitCode);
        Assert.Contains("OMEGA_INPUT_FAILURE", error.ToString());
    }

    private static OmegaTrait[] CreateTraits(string prefix) =>
    [
        new OmegaTrait(prefix + ".1", 0.1f),
        new OmegaTrait(prefix + ".2", 0.2f),
        new OmegaTrait(prefix + ".3", 0.3f),
        new OmegaTrait(prefix + ".4", 0.4f)
    ];
}
