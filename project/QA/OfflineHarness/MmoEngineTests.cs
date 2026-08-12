using System.Security.Cryptography;
using System.Text.Json;
using Evermore.Cli;
using Evermore.Mmo.Core;
using Xunit;

namespace OfflineHarness;

public sealed class MmoEngineTests
{
    [Fact]
    public void CapabilityCatalog_MapsEveryProductionDomainAndPdfRequirement()
    {
        MmoCapabilityCatalogSnapshot catalog = MmoCapabilityCatalog.Create();

        Assert.Equal(18, catalog.Capabilities.Count);
        Assert.Equal(18, catalog.Capabilities.Select(capability => capability.Key).Distinct(StringComparer.Ordinal).Count());
        MmoCapability assets = Assert.Single(
            catalog.Capabilities,
            capability => StringComparer.Ordinal.Equals(capability.Key, "content.asset-pipeline"));
        MmoCapability audio = Assert.Single(
            catalog.Capabilities,
            capability => StringComparer.Ordinal.Equals(capability.Key, "audio.adaptive-runtime"));
        MmoCapability economy = Assert.Single(
            catalog.Capabilities,
            capability => StringComparer.Ordinal.Equals(capability.Key, "economy.crafting"));
        MmoCapability social = Assert.Single(
            catalog.Capabilities,
            capability => StringComparer.Ordinal.Equals(capability.Key, "social.groups"));
        Assert.Equal("0.3.0", catalog.Version);
        Assert.Equal(MmoCapabilityStatus.ImplementedCore, economy.Status);
        Assert.Equal(MmoCapabilityStatus.ImplementedCore, social.Status);
        Assert.Equal(MmoCapabilityStatus.AdapterRequired, assets.Status);
        Assert.Equal(MmoCapabilityStatus.AdapterRequired, audio.Status);
        Assert.Contains("Unity Editor adapter", assets.OwnershipBoundary);
        Assert.Contains("audio adapter", audio.OwnershipBoundary);
    }

    [Fact]
    public void DemoSession_IsDeterministic()
    {
        var engine = new MmoSessionEngine();
        MmoSimulationSnapshot first = engine.Process(MmoDemoScenario.Create());
        MmoSimulationSnapshot second = engine.Process(MmoDemoScenario.Create());

        Assert.Equal(
            MmoSimulationSerializer.Serialize(first),
            MmoSimulationSerializer.Serialize(second));
    }

    [Fact]
    public void DemoSession_CompletesInventoryCombatAndQuestVerticalSlice()
    {
        MmoSimulationSnapshot snapshot = new MmoSessionEngine().Process(MmoDemoScenario.Create());

        Assert.Equal(7L, snapshot.FinalTick);
        Assert.Equal(340, snapshot.Character.Resource);
        Assert.Equal(500, snapshot.Character.Experience);
        Assert.Equal(200, snapshot.Character.Currency);
        Assert.Empty(snapshot.Character.Inventory);
        MmoEquipmentEntry equipment = Assert.Single(snapshot.Character.Equipment);
        Assert.Equal(MmoEquipmentSlot.MainHand, equipment.Slot);
        Assert.Equal("item.training-blade", equipment.ItemId);
        MmoQuestProgress quest = Assert.Single(snapshot.Character.QuestJournal);
        Assert.True(quest.Completed);
        Assert.Equal(1, quest.CurrentDefeats);
        Assert.True(snapshot.Encounter.Defeated);
        Assert.Equal(0, snapshot.Encounter.RemainingHealth);
        Assert.Equal(10, snapshot.Events.Count);
    }

    [Fact]
    public void Session_RejectsRegionOutsideNegotiatedFour()
    {
        MmoSimulationInput input = MmoDemoScenario.Create() with
        {
            RegionZoneId = "ek.north.stratholme"
        };

        ArgumentException error = Assert.Throws<ArgumentException>(() =>
            new MmoSessionEngine().Process(input));
        Assert.Contains("four owner-negotiated", error.Message);
    }

    [Fact]
    public void Session_RejectsAbilityBeforeCooldownExpires()
    {
        MmoSimulationInput original = MmoDemoScenario.Create();
        MmoSimulationInput input = original with
        {
            Actions = original.Actions
                .Select(action => action.Sequence == 4 ? action with { Tick = 2 } : action)
                .ToArray()
        };

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            new MmoSessionEngine().Process(input));
        Assert.Contains("cooldown until tick 3", error.Message);
    }

    [Fact]
    public void SessionSerializer_RejectsTamperedAuthority()
    {
        string json = MmoSimulationSerializer.Serialize(
            new MmoSessionEngine().Process(MmoDemoScenario.Create()));
        string tampered = json.Replace(
            MmoSessionEngine.Authority,
            "WorkingContinuity",
            StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() =>
            MmoSimulationSerializer.DeserializeAndVerify(tampered));
    }

    [Fact]
    public async Task CapabilitiesCli_EmitsVerifiedImplementationLedger()
    {
        var output = new StringWriter();
        var error = new StringWriter();
        int exitCode = await EvermoreCliApp.InvokeAsync(
            new[] { "mmo", "capabilities", "--format", "json" },
            output,
            error);

        Assert.Equal(EvermoreCliApp.SuccessExitCode, exitCode);
        Assert.Equal(string.Empty, error.ToString());
        MmoCapabilityEnvelope verified =
            MmoCapabilitySerializer.DeserializeAndVerify(output.ToString());
        Assert.Equal(18, verified.Payload.Capabilities.Count);
    }

    [Fact]
    public async Task DemoCli_EmitsVerifiedSessionEnvelope()
    {
        var output = new StringWriter();
        var error = new StringWriter();
        int exitCode = await EvermoreCliApp.InvokeAsync(
            new[] { "mmo", "demo", "--format", "json" },
            output,
            error);

        Assert.Equal(EvermoreCliApp.SuccessExitCode, exitCode);
        Assert.Equal(string.Empty, error.ToString());
        using JsonDocument document = JsonDocument.Parse(output.ToString());
        JsonElement payload = document.RootElement.GetProperty("payload");
        Assert.Equal(MmoSimulationSerializer.SchemaVersion, document.RootElement.GetProperty("schemaVersion").GetString());
        Assert.Equal(0, payload.GetProperty("encounter").GetProperty("remainingHealth").GetInt32());
        Assert.Equal("ek.north.eversong", payload.GetProperty("input").GetProperty("regionZoneId").GetString());
        Assert.Equal(1, payload.GetProperty("character").GetProperty("equipment").GetArrayLength());
    }

    [Fact]
    public void EconomyDemo_IsDeterministic()
    {
        var engine = new MmoEconomyEngine();
        MmoEconomySnapshot first = engine.Process(MmoEconomyDemoScenario.Create());
        MmoEconomySnapshot second = engine.Process(MmoEconomyDemoScenario.Create());

        Assert.Equal(
            MmoEconomySerializer.Serialize(first),
            MmoEconomySerializer.Serialize(second));
    }

    [Fact]
    public void EconomyDemo_CompletesGatherCraftVendorDurabilityAndRepairSlice()
    {
        MmoEconomySnapshot snapshot = new MmoEconomyEngine().Process(
            MmoEconomyDemoScenario.Create());

        Assert.Equal(5L, snapshot.FinalTick);
        Assert.Equal(810, snapshot.Currency);
        Assert.Equal(2, snapshot.Inventory.Count);
        MmoEconomyInventoryEntry blade = Assert.Single(
            snapshot.Inventory,
            entry => StringComparer.Ordinal.Equals(entry.ItemId, "item.runed-blade"));
        Assert.Equal(1, blade.Quantity);
        Assert.Equal(95, blade.Durability);
        MmoEconomyInventoryEntry potion = Assert.Single(
            snapshot.Inventory,
            entry => StringComparer.Ordinal.Equals(entry.ItemId, "item.restoration-potion"));
        Assert.Equal(1, potion.Quantity);
        Assert.Equal(0, potion.Durability);
        Assert.Equal(7, snapshot.Events.Count);
        Assert.Equal(
            [
                MmoEconomyEventKind.ItemGathered,
                MmoEconomyEventKind.ItemGathered,
                MmoEconomyEventKind.RecipeCrafted,
                MmoEconomyEventKind.DurabilityDamaged,
                MmoEconomyEventKind.ItemRepaired,
                MmoEconomyEventKind.ItemPurchased,
                MmoEconomyEventKind.ItemSold
            ],
            snapshot.Events.Select(item => item.Kind).ToArray());
    }

    [Fact]
    public void Economy_RejectsRegionOutsideNegotiatedFour()
    {
        MmoEconomyInput input = MmoEconomyDemoScenario.Create() with
        {
            RegionZoneId = "ek.north.stratholme"
        };

        ArgumentException error = Assert.Throws<ArgumentException>(() =>
            new MmoEconomyEngine().Process(input));
        Assert.Contains("four owner-negotiated", error.Message);
    }

    [Fact]
    public void Economy_RejectsCraftWhenIngredientsAreMissing()
    {
        MmoEconomyInput original = MmoEconomyDemoScenario.Create();
        MmoEconomyInput input = original with
        {
            Actions =
            [
                new MmoEconomyActionInput(
                    0,
                    0,
                    MmoEconomyActionKind.CraftRecipe,
                    "recipe.runed-blade",
                    1)
            ]
        };

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            new MmoEconomyEngine().Process(input));
        Assert.Contains("lacks its required quantity", error.Message);
    }

    [Fact]
    public void Economy_RejectsUnaffordablePurchase()
    {
        MmoEconomyInput original = MmoEconomyDemoScenario.Create();
        MmoEconomyInput input = original with
        {
            StartingCurrency = 49,
            Actions =
            [
                new MmoEconomyActionInput(
                    0,
                    0,
                    MmoEconomyActionKind.VendorBuy,
                    "item.restoration-potion",
                    1)
            ]
        };

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            new MmoEconomyEngine().Process(input));
        Assert.Contains("lacks its required currency", error.Message);
    }

    [Fact]
    public void Economy_RepairChargesOnlyForRestoredDurability()
    {
        MmoEconomyInput original = MmoEconomyDemoScenario.Create();
        MmoEconomyInput input = original with
        {
            StartingInventory =
            [
                new MmoEconomyInventoryEntry("item.runed-blade", 1, 75)
            ],
            Actions =
            [
                new MmoEconomyActionInput(
                    0,
                    0,
                    MmoEconomyActionKind.RepairItem,
                    "item.runed-blade",
                    20)
            ]
        };

        MmoEconomySnapshot snapshot = new MmoEconomyEngine().Process(input);
        Assert.Equal(940, snapshot.Currency);
        MmoEconomyInventoryEntry blade = Assert.Single(snapshot.Inventory);
        Assert.Equal(95, blade.Durability);
        MmoEconomyEventRecord repair = Assert.Single(snapshot.Events);
        Assert.Equal(20, repair.Quantity);
        Assert.Equal(-60, repair.CurrencyDelta);
    }

    [Fact]
    public void EconomySerializer_RejectsSemanticallyTamperedRehashedPayload()
    {
        MmoEconomySnapshot snapshot = new MmoEconomyEngine().Process(
            MmoEconomyDemoScenario.Create());
        MmoEconomySnapshot tampered = snapshot with { Currency = snapshot.Currency + 1 };
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(
            tampered,
            MmoJsonContext.Default.MmoEconomySnapshot);
        string hash = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
        string json = JsonSerializer.Serialize(
            new MmoEconomyEnvelope(
                MmoEconomySerializer.SchemaVersion,
                MmoEconomySerializer.Authority,
                hash,
                tampered),
            MmoJsonContext.Default.MmoEconomyEnvelope);

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            MmoEconomySerializer.DeserializeAndVerify(json));
        Assert.Contains("semantic replay", error.Message);
    }

    [Fact]
    public async Task EconomyCli_EmitsVerifiedEconomyEnvelope()
    {
        var output = new StringWriter();
        var error = new StringWriter();
        int exitCode = await EvermoreCliApp.InvokeAsync(
            new[] { "mmo", "economy-demo", "--format", "json" },
            output,
            error);

        Assert.Equal(EvermoreCliApp.SuccessExitCode, exitCode);
        Assert.Equal(string.Empty, error.ToString());
        MmoEconomyEnvelope verified =
            MmoEconomySerializer.DeserializeAndVerify(output.ToString());
        Assert.Equal(810, verified.Payload.Currency);
        Assert.Equal(7, verified.Payload.Events.Count);
    }

    [Fact]
    public void GroupDemo_IsDeterministic()
    {
        var engine = new MmoGroupEngine();
        MmoGroupSnapshot first = engine.Process(MmoGroupDemoScenario.Create());
        MmoGroupSnapshot second = engine.Process(MmoGroupDemoScenario.Create());

        Assert.Equal(
            MmoGroupSerializer.Serialize(first),
            MmoGroupSerializer.Serialize(second));
    }

    [Fact]
    public void GroupDemo_CompletesMembershipLeadershipRoleAndReadyCheckSlice()
    {
        MmoGroupSnapshot snapshot = new MmoGroupEngine().Process(
            MmoGroupDemoScenario.Create());

        Assert.Equal(6L, snapshot.FinalTick);
        Assert.Equal("character.lunara-healer", snapshot.LeaderCharacterId);
        Assert.Equal(2, snapshot.Members.Count);
        Assert.Empty(snapshot.PendingInvitations);
        MmoGroupMemberSnapshot tank = Assert.Single(
            snapshot.Members,
            member => StringComparer.Ordinal.Equals(member.CharacterId, "character.azeroth-warden"));
        Assert.Equal(MmoGroupRole.Tank, tank.Role);
        MmoGroupMemberSnapshot healer = Assert.Single(
            snapshot.Members,
            member => StringComparer.Ordinal.Equals(member.CharacterId, "character.lunara-healer"));
        Assert.Equal(MmoGroupRole.Healer, healer.Role);
        Assert.True(snapshot.ReadyCheck is not null);
        MmoReadyCheckSnapshot readyCheck = snapshot.ReadyCheck!;
        Assert.True(readyCheck.Completed);
        Assert.True(readyCheck.AllReady);
        Assert.Equal(3, readyCheck.Responses.Count);
        Assert.Equal(14, snapshot.Events.Count);
        Assert.Equal(MmoGroupEventKind.MemberLeft, snapshot.Events[^1].Kind);
    }

    [Fact]
    public void Group_RejectsRegionOutsideNegotiatedFour()
    {
        MmoGroupInput input = MmoGroupDemoScenario.Create() with
        {
            RegionZoneId = "ek.north.stratholme"
        };

        ArgumentException error = Assert.Throws<ArgumentException>(() =>
            new MmoGroupEngine().Process(input));
        Assert.Contains("four owner-negotiated", error.Message);
    }

    [Fact]
    public void Group_RejectsInvitationFromNonLeader()
    {
        MmoGroupInput original = MmoGroupDemoScenario.Create();
        MmoGroupInput input = original with
        {
            Actions =
            [
                new MmoGroupActionInput(
                    0,
                    0,
                    MmoGroupActionKind.InviteMember,
                    "character.lunara-healer",
                    "character.solune-ranger",
                    MmoGroupRole.Unassigned,
                    MmoReadyResponse.Pending)
            ]
        };

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            new MmoGroupEngine().Process(input));
        Assert.Contains("not the group leader", error.Message);
    }

    [Fact]
    public void Group_RejectsPartyCapacityOverflow()
    {
        MmoGroupInput original = MmoGroupDemoScenario.Create();
        MmoGroupInput input = original with
        {
            Participants =
            [
                new MmoGroupParticipantDefinition("character.azeroth-warden", "Azeroth Warden"),
                new MmoGroupParticipantDefinition("character.member-one", "Member One"),
                new MmoGroupParticipantDefinition("character.member-two", "Member Two"),
                new MmoGroupParticipantDefinition("character.member-three", "Member Three"),
                new MmoGroupParticipantDefinition("character.member-four", "Member Four"),
                new MmoGroupParticipantDefinition("character.member-five", "Member Five")
            ],
            Actions =
            [
                new MmoGroupActionInput(0, 0, MmoGroupActionKind.InviteMember, "character.azeroth-warden", "character.member-one", MmoGroupRole.Unassigned, MmoReadyResponse.Pending),
                new MmoGroupActionInput(1, 1, MmoGroupActionKind.InviteMember, "character.azeroth-warden", "character.member-two", MmoGroupRole.Unassigned, MmoReadyResponse.Pending),
                new MmoGroupActionInput(2, 2, MmoGroupActionKind.InviteMember, "character.azeroth-warden", "character.member-three", MmoGroupRole.Unassigned, MmoReadyResponse.Pending),
                new MmoGroupActionInput(3, 3, MmoGroupActionKind.InviteMember, "character.azeroth-warden", "character.member-four", MmoGroupRole.Unassigned, MmoReadyResponse.Pending),
                new MmoGroupActionInput(4, 4, MmoGroupActionKind.InviteMember, "character.azeroth-warden", "character.member-five", MmoGroupRole.Unassigned, MmoReadyResponse.Pending)
            ]
        };

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            new MmoGroupEngine().Process(input));
        Assert.Contains("capacity", error.Message);
    }

    [Fact]
    public void Group_RejectsMembershipChangeDuringActiveReadyCheck()
    {
        MmoGroupInput original = MmoGroupDemoScenario.Create();
        MmoGroupInput input = original with
        {
            Actions = original.Actions
                .Take(8)
                .Append(new MmoGroupActionInput(
                    8,
                    4,
                    MmoGroupActionKind.TransferLeadership,
                    "character.azeroth-warden",
                    "character.lunara-healer",
                    MmoGroupRole.Unassigned,
                    MmoReadyResponse.Pending))
                .ToArray()
        };

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            new MmoGroupEngine().Process(input));
        Assert.Contains("active ready check", error.Message);
    }

    [Fact]
    public void Group_RejectsDuplicateReadyResponse()
    {
        MmoGroupInput original = MmoGroupDemoScenario.Create();
        MmoGroupInput input = original with
        {
            Actions = original.Actions
                .Take(9)
                .Append(new MmoGroupActionInput(
                    9,
                    4,
                    MmoGroupActionKind.RespondReadyCheck,
                    "character.azeroth-warden",
                    "character.azeroth-warden",
                    MmoGroupRole.Unassigned,
                    MmoReadyResponse.Ready))
                .ToArray()
        };

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            new MmoGroupEngine().Process(input));
        Assert.Contains("already answered", error.Message);
    }

    [Fact]
    public void GroupSerializer_RejectsSemanticallyTamperedRehashedPayload()
    {
        MmoGroupSnapshot snapshot = new MmoGroupEngine().Process(
            MmoGroupDemoScenario.Create());
        MmoGroupSnapshot tampered = snapshot with
        {
            LeaderCharacterId = "character.azeroth-warden"
        };
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(
            tampered,
            MmoJsonContext.Default.MmoGroupSnapshot);
        string hash = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
        string json = JsonSerializer.Serialize(
            new MmoGroupEnvelope(
                MmoGroupSerializer.SchemaVersion,
                MmoGroupSerializer.Authority,
                hash,
                tampered),
            MmoJsonContext.Default.MmoGroupEnvelope);

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            MmoGroupSerializer.DeserializeAndVerify(json));
        Assert.Contains("semantic replay", error.Message);
    }

    [Fact]
    public async Task GroupCli_EmitsVerifiedGroupEnvelope()
    {
        var output = new StringWriter();
        var error = new StringWriter();
        int exitCode = await EvermoreCliApp.InvokeAsync(
            new[] { "mmo", "group-demo", "--format", "json" },
            output,
            error);

        Assert.Equal(EvermoreCliApp.SuccessExitCode, exitCode);
        Assert.Equal(string.Empty, error.ToString());
        MmoGroupEnvelope verified =
            MmoGroupSerializer.DeserializeAndVerify(output.ToString());
        Assert.Equal("character.lunara-healer", verified.Payload.LeaderCharacterId);
        Assert.Equal(2, verified.Payload.Members.Count);
        Assert.True(verified.Payload.ReadyCheck?.AllReady ?? false);
        Assert.Equal(14, verified.Payload.Events.Count);
    }
}
