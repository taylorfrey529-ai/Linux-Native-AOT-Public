using System.CommandLine;
using Evermore.Mmo.Core;

namespace Evermore.Cli;

public static partial class EvermoreCliApp
{
    private static Command BuildMmoCommand(TextWriter output)
    {
        var capabilitiesFormat = CreateFormatOption();
        var capabilities = new Command("capabilities", "Report the complete MMORPG production-domain implementation ledger.");
        capabilities.Options.Add(capabilitiesFormat);
        capabilities.SetAction(parseResult =>
        {
            WriteMmoCapabilities(
                output,
                parseResult.GetValue(capabilitiesFormat) ?? "text",
                MmoCapabilityCatalog.Create());
            return SuccessExitCode;
        });

        var demoFormat = CreateFormatOption();
        var demo = new Command("demo", "Run the deterministic four-region-authorized MMORPG vertical slice.");
        demo.Options.Add(demoFormat);
        demo.SetAction(parseResult =>
        {
            MmoSimulationSnapshot snapshot = new MmoSessionEngine().Process(MmoDemoScenario.Create());
            WriteMmoDemo(output, parseResult.GetValue(demoFormat) ?? "text", snapshot);
            return SuccessExitCode;
        });

        var economyFormat = CreateFormatOption();
        var economyDemo = new Command("economy-demo", "Run the deterministic four-region-authorized economy and crafting slice.");
        economyDemo.Options.Add(economyFormat);
        economyDemo.SetAction(parseResult =>
        {
            MmoEconomySnapshot snapshot = new MmoEconomyEngine().Process(MmoEconomyDemoScenario.Create());
            WriteMmoEconomy(output, parseResult.GetValue(economyFormat) ?? "text", snapshot);
            return SuccessExitCode;
        });

        var groupFormat = CreateFormatOption();
        var groupDemo = new Command("group-demo", "Run the deterministic four-region-authorized party and raid slice.");
        groupDemo.Options.Add(groupFormat);
        groupDemo.SetAction(parseResult =>
        {
            MmoGroupSnapshot snapshot = new MmoGroupEngine().Process(MmoGroupDemoScenario.Create());
            WriteMmoGroup(output, parseResult.GetValue(groupFormat) ?? "text", snapshot);
            return SuccessExitCode;
        });

        var mmo = new Command("mmo", "Operate the engine-neutral €V€RMØRE MMORPG core.");
        mmo.Subcommands.Add(capabilities);
        mmo.Subcommands.Add(demo);
        mmo.Subcommands.Add(economyDemo);
        mmo.Subcommands.Add(groupDemo);
        return mmo;
    }

    private static void WriteMmoCapabilities(
        TextWriter output,
        string format,
        MmoCapabilityCatalogSnapshot snapshot)
    {
        if (StringComparer.Ordinal.Equals(format, "json"))
        {
            output.WriteLine(MmoCapabilitySerializer.Serialize(snapshot));
            return;
        }

        output.WriteLine("mmo=capability-ledger");
        output.WriteLine($"version={snapshot.Version}");
        output.WriteLine($"capabilities={snapshot.Capabilities.Count}");
        output.WriteLine($"implemented_core={snapshot.Capabilities.Count(capability => capability.Status == MmoCapabilityStatus.ImplementedCore)}");
        output.WriteLine($"adapter_required={snapshot.Capabilities.Count(capability => capability.Status == MmoCapabilityStatus.AdapterRequired)}");
        output.WriteLine($"planned={snapshot.Capabilities.Count(capability => capability.Status == MmoCapabilityStatus.Planned)}");
        output.WriteLine($"source_disposition={snapshot.SourceDisposition}");
    }

    private static void WriteMmoDemo(
        TextWriter output,
        string format,
        MmoSimulationSnapshot snapshot)
    {
        if (StringComparer.Ordinal.Equals(format, "json"))
        {
            output.WriteLine(MmoSimulationSerializer.Serialize(snapshot));
            return;
        }

        output.WriteLine("mmo=session-demo");
        output.WriteLine($"authority={snapshot.Authority}");
        output.WriteLine($"session={snapshot.Input.SessionId}");
        output.WriteLine($"region={snapshot.Input.RegionZoneId}");
        output.WriteLine($"tick={snapshot.FinalTick}");
        output.WriteLine($"character={snapshot.Character.CharacterId}");
        output.WriteLine($"resource={snapshot.Character.Resource}/{snapshot.Character.MaximumResource}");
        output.WriteLine($"experience={snapshot.Character.Experience}");
        output.WriteLine($"currency={snapshot.Character.Currency}");
        output.WriteLine($"target_defeated={snapshot.Encounter.Defeated}");
        output.WriteLine($"events={snapshot.Events.Count}");
    }

    private static void WriteMmoEconomy(
        TextWriter output,
        string format,
        MmoEconomySnapshot snapshot)
    {
        if (StringComparer.Ordinal.Equals(format, "json"))
        {
            output.WriteLine(MmoEconomySerializer.Serialize(snapshot));
            return;
        }

        output.WriteLine("mmo=economy-demo");
        output.WriteLine($"authority={snapshot.Authority}");
        output.WriteLine($"transaction={snapshot.Input.TransactionId}");
        output.WriteLine($"region={snapshot.Input.RegionZoneId}");
        output.WriteLine($"tick={snapshot.FinalTick}");
        output.WriteLine($"character={snapshot.CharacterId}");
        output.WriteLine($"currency={snapshot.Currency}");
        output.WriteLine($"inventory_entries={snapshot.Inventory.Count}");
        output.WriteLine($"events={snapshot.Events.Count}");
    }

    private static void WriteMmoGroup(
        TextWriter output,
        string format,
        MmoGroupSnapshot snapshot)
    {
        if (StringComparer.Ordinal.Equals(format, "json"))
        {
            output.WriteLine(MmoGroupSerializer.Serialize(snapshot));
            return;
        }

        output.WriteLine("mmo=group-demo");
        output.WriteLine($"authority={snapshot.Authority}");
        output.WriteLine($"group={snapshot.Input.GroupId}");
        output.WriteLine($"region={snapshot.Input.RegionZoneId}");
        output.WriteLine($"tick={snapshot.FinalTick}");
        output.WriteLine($"leader={snapshot.LeaderCharacterId}");
        output.WriteLine($"members={snapshot.Members.Count}");
        output.WriteLine($"ready={snapshot.ReadyCheck?.AllReady ?? false}");
        output.WriteLine($"events={snapshot.Events.Count}");
    }
}
