using EasternKingdoms.Simulation;

namespace Evermore.Mmo.Core;

public sealed class MmoSessionEngine
{
    public const int MaximumInventorySlots = 64;
    public const int MaximumDefinitions = 4_096;
    public const int MaximumActions = 10_000;
    public const string Authority = "TestHistoryOnly";
    public const string Model = "evermore-deterministic-mmorpg-session-kernel/1.0";
    public const string SpatialAuthority = "authorized-region-identifier-only-no-coordinate-or-geometry-authority";

    public MmoSimulationSnapshot Process(
        MmoSimulationInput input,
        CancellationToken cancellationToken = default)
    {
        Validate(input);
        cancellationToken.ThrowIfCancellationRequested();

        var itemDefinitions = input.Items.ToDictionary(item => item.ItemId, StringComparer.Ordinal);
        var abilityDefinitions = input.Abilities.ToDictionary(ability => ability.AbilityId, StringComparer.Ordinal);
        var questDefinitions = input.Quests.ToDictionary(quest => quest.QuestId, StringComparer.Ordinal);
        var inventory = new Dictionary<string, int>(StringComparer.Ordinal);
        var equipment = new Dictionary<MmoEquipmentSlot, string>();
        var questProgress = new Dictionary<string, int>(StringComparer.Ordinal);
        var completedQuests = new HashSet<string>(StringComparer.Ordinal);
        var cooldowns = new Dictionary<string, long>(StringComparer.Ordinal);
        var events = new List<MmoSessionEventRecord>();
        int resource = input.Character.MaximumResource;
        int experience = 0;
        int currency = 0;
        int targetHealth = input.Encounter.MaximumHealth;
        long currentTick = input.StartTick;

        foreach (MmoActionInput action in input.Actions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            currentTick = action.Tick;
            switch (action.Kind)
            {
                case MmoActionKind.LootItem:
                    Loot(action, itemDefinitions, inventory, events);
                    break;
                case MmoActionKind.EquipItem:
                    Equip(action, itemDefinitions, inventory, equipment, events);
                    break;
                case MmoActionKind.AcceptQuest:
                    AcceptQuest(action, questDefinitions, questProgress, events);
                    break;
                case MmoActionKind.UseAbility:
                    UseAbility(
                        action,
                        input,
                        abilityDefinitions,
                        questDefinitions,
                        cooldowns,
                        questProgress,
                        completedQuests,
                        events,
                        ref resource,
                        ref targetHealth,
                        ref experience,
                        ref currency);
                    break;
                case MmoActionKind.RestoreResource:
                    int restored = Math.Min(action.Quantity, input.Character.MaximumResource - resource);
                    resource = checked(resource + restored);
                    events.Add(new MmoSessionEventRecord(action.Sequence, action.Tick, MmoEventKind.ResourceRestored, input.Character.CharacterId, restored));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(input), $"Unsupported MMO action kind: {action.Kind}.");
            }
        }

        MmoInventoryEntry[] inventorySnapshot = inventory
            .Where(pair => pair.Value > 0)
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new MmoInventoryEntry(pair.Key, pair.Value))
            .ToArray();
        MmoEquipmentEntry[] equipmentSnapshot = equipment
            .OrderBy(pair => pair.Key)
            .Select(pair => new MmoEquipmentEntry(pair.Key, pair.Value))
            .ToArray();
        MmoQuestProgress[] questSnapshot = questProgress
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair =>
            {
                MmoQuestDefinition definition = questDefinitions[pair.Key];
                return new MmoQuestProgress(
                    pair.Key,
                    pair.Value,
                    definition.RequiredDefeats,
                    completedQuests.Contains(pair.Key));
            })
            .ToArray();

        return new MmoSimulationSnapshot(
            Authority,
            Model,
            AzerothRegionAuthority.Authority,
            SpatialAuthority,
            input,
            currentTick,
            new MmoCharacterSnapshot(
                input.Character.CharacterId,
                input.Character.DisplayName,
                input.Character.ArchetypeId,
                input.Character.Level,
                input.Character.MaximumHealth,
                input.Character.MaximumHealth,
                input.Character.ResourceType,
                resource,
                input.Character.MaximumResource,
                experience,
                currency,
                inventorySnapshot,
                equipmentSnapshot,
                questSnapshot),
            new MmoEncounterSnapshot(
                input.Encounter.TargetId,
                input.Encounter.TargetArchetypeId,
                targetHealth,
                input.Encounter.MaximumHealth,
                targetHealth == 0),
            events.ToArray());
    }

    private static void Loot(
        MmoActionInput action,
        IReadOnlyDictionary<string, MmoItemDefinition> definitions,
        IDictionary<string, int> inventory,
        ICollection<MmoSessionEventRecord> events)
    {
        if (!definitions.TryGetValue(action.ReferenceId, out MmoItemDefinition? definition))
            throw new ArgumentException($"Unknown item definition '{action.ReferenceId}'.", nameof(action));
        AddToInventory(definition, action.Quantity, inventory);
        events.Add(new MmoSessionEventRecord(action.Sequence, action.Tick, MmoEventKind.ItemLooted, definition.ItemId, action.Quantity));
    }

    private static void Equip(
        MmoActionInput action,
        IReadOnlyDictionary<string, MmoItemDefinition> definitions,
        IDictionary<string, int> inventory,
        IDictionary<MmoEquipmentSlot, string> equipment,
        ICollection<MmoSessionEventRecord> events)
    {
        if (!definitions.TryGetValue(action.ReferenceId, out MmoItemDefinition? definition))
            throw new ArgumentException($"Unknown item definition '{action.ReferenceId}'.", nameof(action));
        if (definition.EquipmentSlot == MmoEquipmentSlot.None)
            throw new InvalidOperationException($"Item '{definition.ItemId}' is not equippable.");
        if (action.Quantity != 1 ||
            !inventory.TryGetValue(definition.ItemId, out int available) ||
            available < 1)
            throw new InvalidOperationException($"Item '{definition.ItemId}' is unavailable for equipment.");

        int remaining = inventory[definition.ItemId] - 1;
        if (remaining == 0)
            inventory.Remove(definition.ItemId);
        else
            inventory[definition.ItemId] = remaining;
        if (equipment.TryGetValue(definition.EquipmentSlot, out string? replaced))
            AddToInventory(definitions[replaced], 1, inventory);
        equipment[definition.EquipmentSlot] = definition.ItemId;
        events.Add(new MmoSessionEventRecord(action.Sequence, action.Tick, MmoEventKind.ItemEquipped, definition.ItemId, 1));
    }

    private static void AcceptQuest(
        MmoActionInput action,
        IReadOnlyDictionary<string, MmoQuestDefinition> definitions,
        IDictionary<string, int> progress,
        ICollection<MmoSessionEventRecord> events)
    {
        if (!definitions.ContainsKey(action.ReferenceId))
            throw new ArgumentException($"Unknown quest definition '{action.ReferenceId}'.", nameof(action));
        if (action.Quantity != 1 || progress.ContainsKey(action.ReferenceId))
            throw new InvalidOperationException($"Quest '{action.ReferenceId}' cannot be accepted again.");
        progress[action.ReferenceId] = 0;
        events.Add(new MmoSessionEventRecord(action.Sequence, action.Tick, MmoEventKind.QuestAccepted, action.ReferenceId, 1));
    }

    private static void UseAbility(
        MmoActionInput action,
        MmoSimulationInput input,
        IReadOnlyDictionary<string, MmoAbilityDefinition> abilities,
        IReadOnlyDictionary<string, MmoQuestDefinition> quests,
        IDictionary<string, long> cooldowns,
        IDictionary<string, int> progress,
        ISet<string> completed,
        ICollection<MmoSessionEventRecord> events,
        ref int resource,
        ref int targetHealth,
        ref int experience,
        ref int currency)
    {
        if (!abilities.TryGetValue(action.ReferenceId, out MmoAbilityDefinition? ability))
            throw new ArgumentException($"Unknown ability definition '{action.ReferenceId}'.", nameof(action));
        if (action.Quantity != 1)
            throw new ArgumentException("Ability actions require quantity 1.", nameof(action));
        if (targetHealth == 0)
            throw new InvalidOperationException("The encounter target is already defeated.");
        if (cooldowns.TryGetValue(ability.AbilityId, out long readyTick) && action.Tick < readyTick)
            throw new InvalidOperationException($"Ability '{ability.AbilityId}' is on cooldown until tick {readyTick}.");
        if (resource < ability.ResourceCost)
            throw new InvalidOperationException($"Ability '{ability.AbilityId}' lacks its required resource.");

        resource = checked(resource - ability.ResourceCost);
        int damage = Math.Min(targetHealth, ability.Damage);
        targetHealth = checked(targetHealth - damage);
        cooldowns[ability.AbilityId] = checked(action.Tick + ability.CooldownTicks);
        events.Add(new MmoSessionEventRecord(action.Sequence, action.Tick, MmoEventKind.AbilityUsed, ability.AbilityId, damage));

        if (targetHealth != 0)
            return;

        events.Add(new MmoSessionEventRecord(action.Sequence, action.Tick, MmoEventKind.TargetDefeated, input.Encounter.TargetId, 1));
        foreach (string questId in progress.Keys.OrderBy(id => id, StringComparer.Ordinal).ToArray())
        {
            MmoQuestDefinition quest = quests[questId];
            if (!StringComparer.Ordinal.Equals(quest.TargetArchetypeId, input.Encounter.TargetArchetypeId) ||
                completed.Contains(questId))
            {
                continue;
            }

            int updated = Math.Min(quest.RequiredDefeats, checked(progress[questId] + 1));
            progress[questId] = updated;
            events.Add(new MmoSessionEventRecord(action.Sequence, action.Tick, MmoEventKind.QuestProgressed, questId, updated));
            if (updated < quest.RequiredDefeats)
                continue;

            completed.Add(questId);
            experience = checked(experience + quest.RewardExperience);
            currency = checked(currency + quest.RewardCurrency);
            events.Add(new MmoSessionEventRecord(action.Sequence, action.Tick, MmoEventKind.QuestCompleted, questId, 1));
        }
    }

    private static void Validate(MmoSimulationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ValidateIdentifier(input.SessionId, nameof(input.SessionId));
        if (input.StartTick < 0)
            throw new ArgumentOutOfRangeException(nameof(input), "MMORPG session ticks cannot be negative.");
        string[] authorizedRegions = AzerothRegionAuthority.CreateOrderedZoneIds();
        if (!authorizedRegions.Contains(input.RegionZoneId, StringComparer.Ordinal))
            throw new ArgumentException("MMORPG sessions are limited to the four owner-negotiated Azeroth regions.", nameof(input));

        ArgumentNullException.ThrowIfNull(input.Character);
        ValidateIdentifier(input.Character.CharacterId, nameof(input.Character.CharacterId));
        ValidateIdentifier(input.Character.ArchetypeId, nameof(input.Character.ArchetypeId));
        ValidateDisplayName(input.Character.DisplayName, nameof(input.Character.DisplayName));
        if (input.Character.Level is < 1 or > 100 ||
            input.Character.MaximumHealth is < 1 or > 1_000_000 ||
            input.Character.MaximumResource is < 1 or > 1_000_000)
        {
            throw new ArgumentOutOfRangeException(nameof(input), "Character level, health, or resource limits were exceeded.");
        }

        ValidateCount(input.Items, nameof(input.Items));
        ValidateCount(input.Abilities, nameof(input.Abilities));
        ValidateCount(input.Quests, nameof(input.Quests));
        ArgumentNullException.ThrowIfNull(input.Encounter);
        ValidateIdentifier(input.Encounter.TargetId, nameof(input.Encounter.TargetId));
        ValidateIdentifier(input.Encounter.TargetArchetypeId, nameof(input.Encounter.TargetArchetypeId));
        ValidateDisplayName(input.Encounter.DisplayName, nameof(input.Encounter.DisplayName));
        if (input.Encounter.MaximumHealth is < 1 or > 1_000_000)
            throw new ArgumentOutOfRangeException(nameof(input), "Encounter health is outside the supported bounds.");

        RequireUnique(input.Items.Select(item => item.ItemId), "item");
        foreach (MmoItemDefinition item in input.Items)
        {
            ValidateIdentifier(item.ItemId, nameof(input.Items));
            ValidateDisplayName(item.DisplayName, nameof(input.Items));
            if (item.MaximumStack is < 1 or > 1_000)
                throw new ArgumentOutOfRangeException(nameof(input), $"Item '{item.ItemId}' has an invalid maximum stack.");
        }

        RequireUnique(input.Abilities.Select(ability => ability.AbilityId), "ability");
        foreach (MmoAbilityDefinition ability in input.Abilities)
        {
            ValidateIdentifier(ability.AbilityId, nameof(input.Abilities));
            ValidateDisplayName(ability.DisplayName, nameof(input.Abilities));
            if (ability.ResourceCost is < 0 or > 1_000_000 ||
                ability.Damage is < 1 or > 1_000_000 ||
                ability.CooldownTicks is < 0 or > 1_000_000)
            {
                throw new ArgumentOutOfRangeException(nameof(input), $"Ability '{ability.AbilityId}' exceeds deterministic bounds.");
            }
        }

        RequireUnique(input.Quests.Select(quest => quest.QuestId), "quest");
        foreach (MmoQuestDefinition quest in input.Quests)
        {
            ValidateIdentifier(quest.QuestId, nameof(input.Quests));
            ValidateIdentifier(quest.TargetArchetypeId, nameof(input.Quests));
            ValidateDisplayName(quest.DisplayName, nameof(input.Quests));
            if (quest.RequiredDefeats is < 1 or > 100_000 ||
                quest.RewardExperience is < 0 or > 10_000_000 ||
                quest.RewardCurrency is < 0 or > 10_000_000)
            {
                throw new ArgumentOutOfRangeException(nameof(input), $"Quest '{quest.QuestId}' exceeds deterministic bounds.");
            }
        }

        ArgumentNullException.ThrowIfNull(input.Actions);
        if (input.Actions.Count > MaximumActions)
            throw new ArgumentOutOfRangeException(nameof(input), $"MMORPG sessions cannot exceed {MaximumActions} actions.");
        long previousTick = input.StartTick;
        for (int index = 0; index < input.Actions.Count; index++)
        {
            MmoActionInput action = input.Actions[index] ?? throw new ArgumentException("MMORPG actions cannot be null.", nameof(input));
            ValidateIdentifier(action.ReferenceId, nameof(input.Actions));
            if (action.Sequence != index || action.Tick < previousTick || action.Quantity < 1)
                throw new ArgumentException("MMORPG actions require contiguous sequence IDs, nondecreasing ticks, and positive quantities.", nameof(input));
            previousTick = action.Tick;
        }
    }

    private static void AddToInventory(
        MmoItemDefinition definition,
        int quantity,
        IDictionary<string, int> inventory)
    {
        int existing = inventory.TryGetValue(definition.ItemId, out int existingQuantity)
            ? existingQuantity
            : 0;
        int updated = checked(existing + quantity);
        if (updated > definition.MaximumStack)
            throw new InvalidOperationException($"Item '{definition.ItemId}' exceeds its maximum stack.");
        if (existing == 0 && inventory.Count >= MaximumInventorySlots)
            throw new InvalidOperationException("MMORPG inventory capacity was exceeded.");
        inventory[definition.ItemId] = updated;
    }

    private static void ValidateCount<T>(IReadOnlyList<T> values, string name)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count > MaximumDefinitions)
            throw new ArgumentOutOfRangeException(name, $"MMORPG definition collections cannot exceed {MaximumDefinitions} entries.");
    }

    private static void RequireUnique(IEnumerable<string> identifiers, string kind)
    {
        string[] values = identifiers.ToArray();
        if (values.Any(string.IsNullOrWhiteSpace) || values.Distinct(StringComparer.Ordinal).Count() != values.Length)
            throw new ArgumentException($"MMORPG {kind} identifiers must be nonblank and unique.", kind);
    }

    private static void ValidateIdentifier(string value, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length > 96 ||
            value.Any(character =>
                !(character is >= 'a' and <= 'z') &&
                !(character is >= '0' and <= '9') &&
                character is not '.' and not '-'))
        {
            throw new ArgumentException("MMORPG identifiers must use lowercase ASCII letters, digits, periods, or hyphens.", name);
        }
    }

    private static void ValidateDisplayName(string value, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length > 80 || value.Any(char.IsControl))
            throw new ArgumentException("MMORPG display names must be 1..80 printable characters.", name);
    }
}
