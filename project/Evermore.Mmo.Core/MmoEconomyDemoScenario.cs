using EasternKingdoms.Simulation;

namespace Evermore.Mmo.Core;

public static class MmoEconomyDemoScenario
{
    public static MmoEconomyInput Create() =>
        new(
            "economy.revision-20",
            529,
            0,
            AzerothRegionAuthority.EversongZoneId,
            "character.lunara-warden",
            1_000,
            [
                new MmoEconomyItemDefinition(
                    "item.arcane-ore",
                    "Arcane Ore",
                    MmoItemCategory.Material,
                    20,
                    20,
                    5,
                    0,
                    0),
                new MmoEconomyItemDefinition(
                    "item.silverwood",
                    "Silverwood",
                    MmoItemCategory.Material,
                    20,
                    10,
                    2,
                    0,
                    0),
                new MmoEconomyItemDefinition(
                    "item.runed-blade",
                    "Runed Blade",
                    MmoItemCategory.Weapon,
                    1,
                    500,
                    100,
                    100,
                    3),
                new MmoEconomyItemDefinition(
                    "item.restoration-potion",
                    "Restoration Potion",
                    MmoItemCategory.Consumable,
                    5,
                    50,
                    10,
                    0,
                    0)
            ],
            [
                new MmoRecipeDefinition(
                    "recipe.runed-blade",
                    "Forge Runed Blade",
                    "item.runed-blade",
                    1,
                    40,
                    [
                        new MmoRecipeIngredient("item.arcane-ore", 3),
                        new MmoRecipeIngredient("item.silverwood", 2)
                    ])
            ],
            [],
            [
                new MmoEconomyActionInput(0, 0, MmoEconomyActionKind.GatherItem, "item.arcane-ore", 3),
                new MmoEconomyActionInput(1, 0, MmoEconomyActionKind.GatherItem, "item.silverwood", 2),
                new MmoEconomyActionInput(2, 1, MmoEconomyActionKind.CraftRecipe, "recipe.runed-blade", 1),
                new MmoEconomyActionInput(3, 2, MmoEconomyActionKind.DamageDurability, "item.runed-blade", 25),
                new MmoEconomyActionInput(4, 3, MmoEconomyActionKind.RepairItem, "item.runed-blade", 20),
                new MmoEconomyActionInput(5, 4, MmoEconomyActionKind.VendorBuy, "item.restoration-potion", 2),
                new MmoEconomyActionInput(6, 5, MmoEconomyActionKind.VendorSell, "item.restoration-potion", 1)
            ]);
}
