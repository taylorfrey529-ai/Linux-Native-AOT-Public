using System.Text.Json.Serialization;

namespace Evermore.Mmo.Core;

public enum MmoEconomyActionKind
{
    GatherItem,
    CraftRecipe,
    VendorBuy,
    VendorSell,
    DamageDurability,
    RepairItem
}

public enum MmoEconomyEventKind
{
    ItemGathered,
    RecipeCrafted,
    ItemPurchased,
    ItemSold,
    DurabilityDamaged,
    ItemRepaired
}

public sealed record MmoEconomyItemDefinition(
    [property: JsonPropertyOrder(0), JsonRequired] string ItemId,
    [property: JsonPropertyOrder(1), JsonRequired] string DisplayName,
    [property: JsonPropertyOrder(2), JsonRequired] MmoItemCategory Category,
    [property: JsonPropertyOrder(3), JsonRequired] int MaximumStack,
    [property: JsonPropertyOrder(4), JsonRequired] int VendorBuyPrice,
    [property: JsonPropertyOrder(5), JsonRequired] int VendorSellPrice,
    [property: JsonPropertyOrder(6), JsonRequired] int MaximumDurability,
    [property: JsonPropertyOrder(7), JsonRequired] int RepairCostPerPoint);

public sealed record MmoRecipeIngredient(
    [property: JsonPropertyOrder(0), JsonRequired] string ItemId,
    [property: JsonPropertyOrder(1), JsonRequired] int Quantity);

public sealed record MmoRecipeDefinition(
    [property: JsonPropertyOrder(0), JsonRequired] string RecipeId,
    [property: JsonPropertyOrder(1), JsonRequired] string DisplayName,
    [property: JsonPropertyOrder(2), JsonRequired] string OutputItemId,
    [property: JsonPropertyOrder(3), JsonRequired] int OutputQuantity,
    [property: JsonPropertyOrder(4), JsonRequired] int CurrencyCost,
    [property: JsonPropertyOrder(5), JsonRequired] IReadOnlyList<MmoRecipeIngredient> Ingredients);

public sealed record MmoEconomyInventoryEntry(
    [property: JsonPropertyOrder(0), JsonRequired] string ItemId,
    [property: JsonPropertyOrder(1), JsonRequired] int Quantity,
    [property: JsonPropertyOrder(2), JsonRequired] int Durability);

public sealed record MmoEconomyActionInput(
    [property: JsonPropertyOrder(0), JsonRequired] int Sequence,
    [property: JsonPropertyOrder(1), JsonRequired] long Tick,
    [property: JsonPropertyOrder(2), JsonRequired] MmoEconomyActionKind Kind,
    [property: JsonPropertyOrder(3), JsonRequired] string ReferenceId,
    [property: JsonPropertyOrder(4), JsonRequired] int Quantity);

public sealed record MmoEconomyInput(
    [property: JsonPropertyOrder(0), JsonRequired] string TransactionId,
    [property: JsonPropertyOrder(1), JsonRequired] ulong Seed,
    [property: JsonPropertyOrder(2), JsonRequired] long StartTick,
    [property: JsonPropertyOrder(3), JsonRequired] string RegionZoneId,
    [property: JsonPropertyOrder(4), JsonRequired] string CharacterId,
    [property: JsonPropertyOrder(5), JsonRequired] int StartingCurrency,
    [property: JsonPropertyOrder(6), JsonRequired] IReadOnlyList<MmoEconomyItemDefinition> Items,
    [property: JsonPropertyOrder(7), JsonRequired] IReadOnlyList<MmoRecipeDefinition> Recipes,
    [property: JsonPropertyOrder(8), JsonRequired] IReadOnlyList<MmoEconomyInventoryEntry> StartingInventory,
    [property: JsonPropertyOrder(9), JsonRequired] IReadOnlyList<MmoEconomyActionInput> Actions);

public sealed record MmoEconomyEventRecord(
    [property: JsonPropertyOrder(0), JsonRequired] int Sequence,
    [property: JsonPropertyOrder(1), JsonRequired] long Tick,
    [property: JsonPropertyOrder(2), JsonRequired] MmoEconomyEventKind Kind,
    [property: JsonPropertyOrder(3), JsonRequired] string SubjectId,
    [property: JsonPropertyOrder(4), JsonRequired] int Quantity,
    [property: JsonPropertyOrder(5), JsonRequired] int CurrencyDelta);

public sealed record MmoEconomySnapshot(
    [property: JsonPropertyOrder(0), JsonRequired] string Authority,
    [property: JsonPropertyOrder(1), JsonRequired] string Model,
    [property: JsonPropertyOrder(2), JsonRequired] string RegionAuthority,
    [property: JsonPropertyOrder(3), JsonRequired] string SpatialAuthority,
    [property: JsonPropertyOrder(4), JsonRequired] MmoEconomyInput Input,
    [property: JsonPropertyOrder(5), JsonRequired] long FinalTick,
    [property: JsonPropertyOrder(6), JsonRequired] string CharacterId,
    [property: JsonPropertyOrder(7), JsonRequired] int Currency,
    [property: JsonPropertyOrder(8), JsonRequired] IReadOnlyList<MmoEconomyInventoryEntry> Inventory,
    [property: JsonPropertyOrder(9), JsonRequired] IReadOnlyList<MmoEconomyEventRecord> Events);

public sealed record MmoEconomyEnvelope(
    [property: JsonPropertyOrder(0), JsonRequired] string SchemaVersion,
    [property: JsonPropertyOrder(1), JsonRequired] string Authority,
    [property: JsonPropertyOrder(2), JsonRequired] string PayloadSha256,
    [property: JsonPropertyOrder(3), JsonRequired] MmoEconomySnapshot Payload);
