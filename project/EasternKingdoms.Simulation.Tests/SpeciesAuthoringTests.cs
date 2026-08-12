using EasternKingdoms.Simulation;
using Xunit;

namespace EasternKingdoms.Simulation.Tests;

public sealed class SpeciesAuthoringTests
{
    [Fact]
    public void DeferredTemplateCannotBeActivated()
    {
        var catalog = AzerothSpeciesAuthoringBootstrap.Create();
        var gate = new SpeciesActivationGate(catalog);
        Assert.Throws<InvalidOperationException>(() => gate.Activate("azeroth.deferred.animal"));
    }

    [Fact]
    public void AuthoredHighElfProfileCanBeActivated()
    {
        var catalog = AzerothSpeciesAuthoringBootstrap.Create();
        var gate = new SpeciesActivationGate(catalog);
        var profile = gate.Activate("azeroth.elf.high");
        Assert.Equal(SpeciesProfileStatus.Authored, profile.Status);
        Assert.Contains("azeroth.elf.sindorei", profile.CompatibleSpeciesIds);
    }

    [Fact]
    public void ZonePressureResolverPreservesAuthoredAndEnvironmentalInfluence()
    {
        var catalog = AzerothSpeciesAuthoringBootstrap.Create();
        var record = catalog.Get("azeroth.elf.high");
        var zone = AzerothEvolutionBootstrap.CreateNorthernScarCorridor().First();
        var pressure = new EvolutionaryPressureResolver().Resolve(zone, record);
        Assert.True(pressure.TraitPressures.ContainsKey("arcane_precision"));
        Assert.InRange(pressure.ReproductiveModifier, 0.0, 1.0);
        Assert.InRange(pressure.ViabilityModifier, 0.0, 1.0);
    }
}
