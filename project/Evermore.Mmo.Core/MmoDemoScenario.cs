using EasternKingdoms.Simulation;

namespace Evermore.Mmo.Core;

public static class MmoDemoScenario
{
    public static MmoSimulationInput Create() =>
        new(
            "session.revision-19",
            529,
            0,
            AzerothRegionAuthority.EversongZoneId,
            new MmoCharacterInput(
                "character.lunara-warden",
                "Lunara Warden",
                "archetype.arcane-warden",
                10,
                1_000,
                MmoResourceType.Mana,
                500),
            [
                new MmoItemDefinition(
                    "item.training-blade",
                    "Training Blade",
                    MmoItemCategory.Weapon,
                    MmoEquipmentSlot.MainHand,
                    1)
            ],
            [
                new MmoAbilityDefinition(
                    "ability.arcane-bolt",
                    "Arcane Bolt",
                    40,
                    150,
                    2)
            ],
            [
                new MmoQuestDefinition(
                    "quest.frontier-watch",
                    "Frontier Watch",
                    "creature.training-construct",
                    1,
                    500,
                    200)
            ],
            new MmoEncounterInput(
                "target.training-construct.01",
                "creature.training-construct",
                "Training Construct",
                600),
            [
                new MmoActionInput(0, 0, MmoActionKind.AcceptQuest, "quest.frontier-watch", 1),
                new MmoActionInput(1, 0, MmoActionKind.LootItem, "item.training-blade", 1),
                new MmoActionInput(2, 0, MmoActionKind.EquipItem, "item.training-blade", 1),
                new MmoActionInput(3, 1, MmoActionKind.UseAbility, "ability.arcane-bolt", 1),
                new MmoActionInput(4, 3, MmoActionKind.UseAbility, "ability.arcane-bolt", 1),
                new MmoActionInput(5, 5, MmoActionKind.UseAbility, "ability.arcane-bolt", 1),
                new MmoActionInput(6, 7, MmoActionKind.UseAbility, "ability.arcane-bolt", 1)
            ]);
}
