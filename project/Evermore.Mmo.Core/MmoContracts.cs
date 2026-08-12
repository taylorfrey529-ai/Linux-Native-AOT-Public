using System.Text.Json.Serialization;

namespace Evermore.Mmo.Core;

public enum MmoCapabilityStatus
{
    ImplementedCore,
    AuthorityBound,
    AdapterRequired,
    Planned
}

public enum MmoItemCategory
{
    Armor,
    Weapon,
    Consumable,
    Quest,
    Material
}

public enum MmoEquipmentSlot
{
    None,
    Head,
    Shoulders,
    Chest,
    Hands,
    Waist,
    Legs,
    Feet,
    MainHand,
    OffHand
}

public enum MmoResourceType
{
    Mana,
    Energy,
    Rage
}

public enum MmoActionKind
{
    LootItem,
    EquipItem,
    AcceptQuest,
    UseAbility,
    RestoreResource
}

public enum MmoEventKind
{
    ItemLooted,
    ItemEquipped,
    QuestAccepted,
    AbilityUsed,
    TargetDefeated,
    QuestProgressed,
    QuestCompleted,
    ResourceRestored
}

public sealed record MmoCapability(
    [property: JsonPropertyOrder(0), JsonRequired] string Key,
    [property: JsonPropertyOrder(1), JsonRequired] string Name,
    [property: JsonPropertyOrder(2), JsonRequired] MmoCapabilityStatus Status,
    [property: JsonPropertyOrder(3), JsonRequired] string OwnershipBoundary);

public sealed record MmoCapabilityCatalogSnapshot(
    [property: JsonPropertyOrder(0), JsonRequired] string Product,
    [property: JsonPropertyOrder(1), JsonRequired] string Version,
    [property: JsonPropertyOrder(2), JsonRequired] string Implementation,
    [property: JsonPropertyOrder(3), JsonRequired] string SourceDisposition,
    [property: JsonPropertyOrder(4), JsonRequired] IReadOnlyList<MmoCapability> Capabilities);

public sealed record MmoCharacterInput(
    [property: JsonPropertyOrder(0), JsonRequired] string CharacterId,
    [property: JsonPropertyOrder(1), JsonRequired] string DisplayName,
    [property: JsonPropertyOrder(2), JsonRequired] string ArchetypeId,
    [property: JsonPropertyOrder(3), JsonRequired] int Level,
    [property: JsonPropertyOrder(4), JsonRequired] int MaximumHealth,
    [property: JsonPropertyOrder(5), JsonRequired] MmoResourceType ResourceType,
    [property: JsonPropertyOrder(6), JsonRequired] int MaximumResource);

public sealed record MmoItemDefinition(
    [property: JsonPropertyOrder(0), JsonRequired] string ItemId,
    [property: JsonPropertyOrder(1), JsonRequired] string DisplayName,
    [property: JsonPropertyOrder(2), JsonRequired] MmoItemCategory Category,
    [property: JsonPropertyOrder(3), JsonRequired] MmoEquipmentSlot EquipmentSlot,
    [property: JsonPropertyOrder(4), JsonRequired] int MaximumStack);

public sealed record MmoAbilityDefinition(
    [property: JsonPropertyOrder(0), JsonRequired] string AbilityId,
    [property: JsonPropertyOrder(1), JsonRequired] string DisplayName,
    [property: JsonPropertyOrder(2), JsonRequired] int ResourceCost,
    [property: JsonPropertyOrder(3), JsonRequired] int Damage,
    [property: JsonPropertyOrder(4), JsonRequired] int CooldownTicks);

public sealed record MmoQuestDefinition(
    [property: JsonPropertyOrder(0), JsonRequired] string QuestId,
    [property: JsonPropertyOrder(1), JsonRequired] string DisplayName,
    [property: JsonPropertyOrder(2), JsonRequired] string TargetArchetypeId,
    [property: JsonPropertyOrder(3), JsonRequired] int RequiredDefeats,
    [property: JsonPropertyOrder(4), JsonRequired] int RewardExperience,
    [property: JsonPropertyOrder(5), JsonRequired] int RewardCurrency);

public sealed record MmoEncounterInput(
    [property: JsonPropertyOrder(0), JsonRequired] string TargetId,
    [property: JsonPropertyOrder(1), JsonRequired] string TargetArchetypeId,
    [property: JsonPropertyOrder(2), JsonRequired] string DisplayName,
    [property: JsonPropertyOrder(3), JsonRequired] int MaximumHealth);

public sealed record MmoActionInput(
    [property: JsonPropertyOrder(0), JsonRequired] int Sequence,
    [property: JsonPropertyOrder(1), JsonRequired] long Tick,
    [property: JsonPropertyOrder(2), JsonRequired] MmoActionKind Kind,
    [property: JsonPropertyOrder(3), JsonRequired] string ReferenceId,
    [property: JsonPropertyOrder(4), JsonRequired] int Quantity);

public sealed record MmoSimulationInput(
    [property: JsonPropertyOrder(0), JsonRequired] string SessionId,
    [property: JsonPropertyOrder(1), JsonRequired] ulong Seed,
    [property: JsonPropertyOrder(2), JsonRequired] long StartTick,
    [property: JsonPropertyOrder(3), JsonRequired] string RegionZoneId,
    [property: JsonPropertyOrder(4), JsonRequired] MmoCharacterInput Character,
    [property: JsonPropertyOrder(5), JsonRequired] IReadOnlyList<MmoItemDefinition> Items,
    [property: JsonPropertyOrder(6), JsonRequired] IReadOnlyList<MmoAbilityDefinition> Abilities,
    [property: JsonPropertyOrder(7), JsonRequired] IReadOnlyList<MmoQuestDefinition> Quests,
    [property: JsonPropertyOrder(8), JsonRequired] MmoEncounterInput Encounter,
    [property: JsonPropertyOrder(9), JsonRequired] IReadOnlyList<MmoActionInput> Actions);

public sealed record MmoInventoryEntry(
    [property: JsonPropertyOrder(0), JsonRequired] string ItemId,
    [property: JsonPropertyOrder(1), JsonRequired] int Quantity);

public sealed record MmoEquipmentEntry(
    [property: JsonPropertyOrder(0), JsonRequired] MmoEquipmentSlot Slot,
    [property: JsonPropertyOrder(1), JsonRequired] string ItemId);

public sealed record MmoQuestProgress(
    [property: JsonPropertyOrder(0), JsonRequired] string QuestId,
    [property: JsonPropertyOrder(1), JsonRequired] int CurrentDefeats,
    [property: JsonPropertyOrder(2), JsonRequired] int RequiredDefeats,
    [property: JsonPropertyOrder(3), JsonRequired] bool Completed);

public sealed record MmoCharacterSnapshot(
    [property: JsonPropertyOrder(0), JsonRequired] string CharacterId,
    [property: JsonPropertyOrder(1), JsonRequired] string DisplayName,
    [property: JsonPropertyOrder(2), JsonRequired] string ArchetypeId,
    [property: JsonPropertyOrder(3), JsonRequired] int Level,
    [property: JsonPropertyOrder(4), JsonRequired] int Health,
    [property: JsonPropertyOrder(5), JsonRequired] int MaximumHealth,
    [property: JsonPropertyOrder(6), JsonRequired] MmoResourceType ResourceType,
    [property: JsonPropertyOrder(7), JsonRequired] int Resource,
    [property: JsonPropertyOrder(8), JsonRequired] int MaximumResource,
    [property: JsonPropertyOrder(9), JsonRequired] int Experience,
    [property: JsonPropertyOrder(10), JsonRequired] int Currency,
    [property: JsonPropertyOrder(11), JsonRequired] IReadOnlyList<MmoInventoryEntry> Inventory,
    [property: JsonPropertyOrder(12), JsonRequired] IReadOnlyList<MmoEquipmentEntry> Equipment,
    [property: JsonPropertyOrder(13), JsonRequired] IReadOnlyList<MmoQuestProgress> QuestJournal);

public sealed record MmoEncounterSnapshot(
    [property: JsonPropertyOrder(0), JsonRequired] string TargetId,
    [property: JsonPropertyOrder(1), JsonRequired] string TargetArchetypeId,
    [property: JsonPropertyOrder(2), JsonRequired] int RemainingHealth,
    [property: JsonPropertyOrder(3), JsonRequired] int MaximumHealth,
    [property: JsonPropertyOrder(4), JsonRequired] bool Defeated);

public sealed record MmoSessionEventRecord(
    [property: JsonPropertyOrder(0), JsonRequired] int Sequence,
    [property: JsonPropertyOrder(1), JsonRequired] long Tick,
    [property: JsonPropertyOrder(2), JsonRequired] MmoEventKind Kind,
    [property: JsonPropertyOrder(3), JsonRequired] string SubjectId,
    [property: JsonPropertyOrder(4), JsonRequired] int Amount);

public sealed record MmoSimulationSnapshot(
    [property: JsonPropertyOrder(0), JsonRequired] string Authority,
    [property: JsonPropertyOrder(1), JsonRequired] string Model,
    [property: JsonPropertyOrder(2), JsonRequired] string RegionAuthority,
    [property: JsonPropertyOrder(3), JsonRequired] string SpatialAuthority,
    [property: JsonPropertyOrder(4), JsonRequired] MmoSimulationInput Input,
    [property: JsonPropertyOrder(5), JsonRequired] long FinalTick,
    [property: JsonPropertyOrder(6), JsonRequired] MmoCharacterSnapshot Character,
    [property: JsonPropertyOrder(7), JsonRequired] MmoEncounterSnapshot Encounter,
    [property: JsonPropertyOrder(8), JsonRequired] IReadOnlyList<MmoSessionEventRecord> Events);

public sealed record MmoCapabilityEnvelope(
    [property: JsonPropertyOrder(0), JsonRequired] string SchemaVersion,
    [property: JsonPropertyOrder(1), JsonRequired] string Authority,
    [property: JsonPropertyOrder(2), JsonRequired] string PayloadSha256,
    [property: JsonPropertyOrder(3), JsonRequired] MmoCapabilityCatalogSnapshot Payload);

public sealed record MmoSimulationEnvelope(
    [property: JsonPropertyOrder(0), JsonRequired] string SchemaVersion,
    [property: JsonPropertyOrder(1), JsonRequired] string Authority,
    [property: JsonPropertyOrder(2), JsonRequired] string PayloadSha256,
    [property: JsonPropertyOrder(3), JsonRequired] MmoSimulationSnapshot Payload);
