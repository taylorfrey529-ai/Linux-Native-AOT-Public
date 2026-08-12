using Evermore.Genetics;

namespace EasternKingdoms.Simulation;

public sealed record AzerothBiologyRuntime(
    AzerothLifeRegistry Life,
    SpeciesAuthoringCatalog SpeciesAuthoring,
    SpeciesEcologyCatalog Ecology);

public static class AzerothBiologyBootstrap
{
    public static AzerothBiologyRuntime CreateAuthoritative()
    {
        var authoring = AzerothSpeciesAuthoringBootstrap.Create();
        var ecology = AzerothEcologyBootstrap.Create();
        var gate = new BiologicalActivationGate(
            new SpeciesActivationGate(authoring),
            ecology);

        var life = new AzerothLifeRegistry();
        foreach (var record in authoring.Records
                     .Where(x => x.ActivationState == SpeciesActivationState.Active)
                     .OrderBy(x => x.SpeciesId, StringComparer.Ordinal))
        {
            life.RegisterSpecies(gate.Activate(record.SpeciesId));
        }

        return new AzerothBiologyRuntime(
            life,
            authoring,
            ecology);
    }
}

public sealed record AzerothIntegratedEvolutionRuntime(
    AzerothBiologyRuntime Biology,
    EasternKingdomsEvolutionEngine Evolution,
    CohortEvolutionEngine Cohorts);

public static class AzerothIntegratedEvolutionBootstrap
{
    public static AzerothIntegratedEvolutionRuntime Create(GenomeCatalog genomeCatalog)
    {
        ArgumentNullException.ThrowIfNull(genomeCatalog);
        var biology = AzerothBiologyBootstrap.CreateAuthoritative();
        var evolution = new EasternKingdomsEvolutionEngine(
            biology.Life,
            genomeCatalog,
            new GenomeRecombiner(new LinkedGameteGenerator(), new MutationEngine()),
            new GenomeValidator(),
            new ExpressionEngine(),
            biology.Ecology);

        var cohorts = new CohortEvolutionEngine(
            genomeCatalog,
            biology.SpeciesAuthoring,
            biology.Ecology);

        return new AzerothIntegratedEvolutionRuntime(
            biology,
            evolution,
            cohorts);
    }
}

public sealed record AzerothMetapopulationRuntime(
    AzerothIntegratedEvolutionRuntime Integrated,
    PopulationRouteCatalog PopulationRoutes,
    MetapopulationOrchestrator Metapopulations,
    ReciprocalCoevolutionEngine ReciprocalCoevolution,
    HostPathogenCoevolutionEngine HostPathogenCoevolution);

public static class AzerothMetapopulationBootstrap
{
    public static AzerothMetapopulationRuntime Create(GenomeCatalog genomeCatalog)
    {
        ArgumentNullException.ThrowIfNull(genomeCatalog);
        var integrated = AzerothIntegratedEvolutionBootstrap.Create(genomeCatalog);
        var routes = NorthernScarPopulationRoutes.Create();
        var metapopulations = new MetapopulationOrchestrator(
            integrated.Cohorts,
            routes,
            PopulationBoundaryGate.NorthernScarCorridor());

        return new AzerothMetapopulationRuntime(
            integrated,
            routes,
            metapopulations,
            new ReciprocalCoevolutionEngine(genomeCatalog),
            new HostPathogenCoevolutionEngine(genomeCatalog));
    }
}

public sealed record AzerothCommunityEvolutionRuntime(
    AzerothMetapopulationRuntime Metapopulation,
    CommunityEvolutionDefinition Definition,
    WholeCommunityEvolutionOrchestrator Orchestrator);

public static class AzerothCommunityEvolutionBootstrap
{
    public static AzerothCommunityEvolutionRuntime Create(GenomeCatalog genomeCatalog)
    {
        ArgumentNullException.ThrowIfNull(genomeCatalog);
        var metapopulation = AzerothMetapopulationBootstrap.Create(genomeCatalog);
        var ecology = metapopulation.Integrated.Biology.Ecology;
        var network = new TrophicNetworkCompiler().Compile(
            DefaultTrophicGuildScaffold.Create(),
            ecology,
            "azeroth.eastern-kingdoms.community-trophic/1.0");

        var definition = new CommunityEvolutionDefinition(
            BaseZones: AzerothEvolutionBootstrap.CreateNorthernScarCorridor(),
            EnvironmentalPulses: Array.Empty<EnvironmentalPulseDefinition>(),
            TrophicNetwork: network,
            ReciprocalSelectionRules: Array.Empty<ReciprocalSelectionRule>(),
            Diseases: Array.Empty<CommunityDiseaseDefinition>(),
            MetapopulationSettings: new MetapopulationSettings(),
            ResilienceSettings: new CommunityResilienceSettings());

        var orchestrator = new WholeCommunityEvolutionOrchestrator(
            genomeCatalog,
            ecology,
            metapopulation.PopulationRoutes,
            metapopulation.Metapopulations,
            metapopulation.ReciprocalCoevolution);

        return new AzerothCommunityEvolutionRuntime(
            metapopulation,
            definition,
            orchestrator);
    }
}
