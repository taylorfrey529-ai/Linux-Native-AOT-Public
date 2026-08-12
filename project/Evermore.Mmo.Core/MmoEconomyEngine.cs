using EasternKingdoms.Simulation;

namespace Evermore.Mmo.Core;

public sealed class MmoEconomyEngine
{
    public const int MaximumDefinitions = 4_096;
    public const int MaximumInventorySlots = 64;
    public const int MaximumActions = 10_000;
    public const int MaximumCurrency = 1_000_000_000;
    public const string Authority = MmoSessionEngine.Authority;
    public const string Model = "evermore-deterministic-mmorpg-economy-kernel/1.0";
    public const string SpatialAuthority = MmoSessionEngine.SpatialAuthority;

    public MmoEconomySnapshot Process(
        MmoEconomyInput input,
        CancellationToken cancellationToken = default)
    {
        Validate(input);
        cancellationToken.ThrowIfCancellationRequested();

        var definitions = input.Items.ToDictionary(item => item.ItemId, StringComparer.Ordinal);
        var recipes = input.Recipes.ToDictionary(recipe => recipe.RecipeId, StringComparer.Ordinal);
        var inventory = input.StartingInventory.ToDictionary(
            entry => entry.ItemId,
            entry => entry,
            StringComparer.Ordinal);
        var events = new List<MmoEconomyEventRecord>();
        int currency = input.StartingCurrency;
        long currentTick = input.StartTick;

        foreach (MmoEconomyActionInput action in input.Actions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            currentTick = action.Tick;
            switch (action.Kind)
            {
                case MmoEconomyActionKind.GatherItem:
                    Gather(action, definitions, ref inventory, events);
                    break;
                case MmoEconomyActionKind.CraftRecipe:
                    Craft(action, definitions, recipes, ref inventory, ref currency, events);
                    break;
                case MmoEconomyActionKind.VendorBuy:
                    Buy(action, definitions, ref inventory, ref currency, events);
                    break;
                case MmoEconomyActionKind.VendorSell:
                    Sell(action, definitions, ref inventory, ref currency, events);
                    break;
                case MmoEconomyActionKind.DamageDurability:
                    Damage(action, definitions, inventory, events);
                    break;
                case MmoEconomyActionKind.RepairItem:
                    Repair(action, definitions, inventory, ref currency, events);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(input), $"Unsupported economy action kind: {action.Kind}.");
            }
        }

        return new MmoEconomySnapshot(
            Authority,
            Model,
            AzerothRegionAuthority.Authority,
            SpatialAuthority,
            input,
            currentTick,
            input.CharacterId,
            currency,
            inventory.Values
                .Where(entry => entry.Quantity > 0)
                .OrderBy(entry => entry.ItemId, StringComparer.Ordinal)
                .ToArray(),
            events.ToArray());
    }

    private static void Gather(
        MmoEconomyActionInput action,
        IReadOnlyDictionary<string, MmoEconomyItemDefinition> definitions,
        ref Dictionary<string, MmoEconomyInventoryEntry> inventory,
        ICollection<MmoEconomyEventRecord> events)
    {
        MmoEconomyItemDefinition definition = GetItem(action.ReferenceId, definitions);
        Dictionary<string, MmoEconomyInventoryEntry> candidate = Clone(inventory);
        Add(definition, action.Quantity, candidate);
        inventory = candidate;
        events.Add(new MmoEconomyEventRecord(
            action.Sequence,
            action.Tick,
            MmoEconomyEventKind.ItemGathered,
            definition.ItemId,
            action.Quantity,
            0));
    }

    private static void Craft(
        MmoEconomyActionInput action,
        IReadOnlyDictionary<string, MmoEconomyItemDefinition> definitions,
        IReadOnlyDictionary<string, MmoRecipeDefinition> recipes,
        ref Dictionary<string, MmoEconomyInventoryEntry> inventory,
        ref int currency,
        ICollection<MmoEconomyEventRecord> events)
    {
        if (!recipes.TryGetValue(action.ReferenceId, out MmoRecipeDefinition? recipe))
            throw new ArgumentException($"Unknown recipe definition '{action.ReferenceId}'.", nameof(action));

        int cost = checked(recipe.CurrencyCost * action.Quantity);
        if (currency < cost)
            throw new InvalidOperationException($"Recipe '{recipe.RecipeId}' lacks its required currency.");

        Dictionary<string, MmoEconomyInventoryEntry> candidate = Clone(inventory);
        foreach (MmoRecipeIngredient ingredient in recipe.Ingredients)
        {
            Remove(
                ingredient.ItemId,
                checked(ingredient.Quantity * action.Quantity),
                candidate);
        }
        MmoEconomyItemDefinition output = definitions[recipe.OutputItemId];
        Add(output, checked(recipe.OutputQuantity * action.Quantity), candidate);

        inventory = candidate;
        currency = checked(currency - cost);
        events.Add(new MmoEconomyEventRecord(
            action.Sequence,
            action.Tick,
            MmoEconomyEventKind.RecipeCrafted,
            recipe.RecipeId,
            action.Quantity,
            -cost));
    }

    private static void Buy(
        MmoEconomyActionInput action,
        IReadOnlyDictionary<string, MmoEconomyItemDefinition> definitions,
        ref Dictionary<string, MmoEconomyInventoryEntry> inventory,
        ref int currency,
        ICollection<MmoEconomyEventRecord> events)
    {
        MmoEconomyItemDefinition definition = GetItem(action.ReferenceId, definitions);
        if (definition.VendorBuyPrice <= 0)
            throw new InvalidOperationException($"Item '{definition.ItemId}' is not offered by a vendor.");
        int cost = checked(definition.VendorBuyPrice * action.Quantity);
        if (currency < cost)
            throw new InvalidOperationException($"Item '{definition.ItemId}' lacks its required currency.");

        Dictionary<string, MmoEconomyInventoryEntry> candidate = Clone(inventory);
        Add(definition, action.Quantity, candidate);
        inventory = candidate;
        currency = checked(currency - cost);
        events.Add(new MmoEconomyEventRecord(
            action.Sequence,
            action.Tick,
            MmoEconomyEventKind.ItemPurchased,
            definition.ItemId,
            action.Quantity,
            -cost));
    }

    private static void Sell(
        MmoEconomyActionInput action,
        IReadOnlyDictionary<string, MmoEconomyItemDefinition> definitions,
        ref Dictionary<string, MmoEconomyInventoryEntry> inventory,
        ref int currency,
        ICollection<MmoEconomyEventRecord> events)
    {
        MmoEconomyItemDefinition definition = GetItem(action.ReferenceId, definitions);
        if (definition.VendorSellPrice <= 0)
            throw new InvalidOperationException($"Item '{definition.ItemId}' cannot be sold to a vendor.");

        int proceeds = checked(definition.VendorSellPrice * action.Quantity);
        int updatedCurrency = checked(currency + proceeds);
        if (updatedCurrency > MaximumCurrency)
            throw new InvalidOperationException("The economy currency bound would be exceeded.");

        Dictionary<string, MmoEconomyInventoryEntry> candidate = Clone(inventory);
        Remove(definition.ItemId, action.Quantity, candidate);
        inventory = candidate;
        currency = updatedCurrency;
        events.Add(new MmoEconomyEventRecord(
            action.Sequence,
            action.Tick,
            MmoEconomyEventKind.ItemSold,
            definition.ItemId,
            action.Quantity,
            proceeds));
    }

    private static void Damage(
        MmoEconomyActionInput action,
        IReadOnlyDictionary<string, MmoEconomyItemDefinition> definitions,
        IDictionary<string, MmoEconomyInventoryEntry> inventory,
        ICollection<MmoEconomyEventRecord> events)
    {
        MmoEconomyItemDefinition definition = GetItem(action.ReferenceId, definitions);
        if (definition.MaximumDurability <= 0 ||
            !inventory.TryGetValue(definition.ItemId, out MmoEconomyInventoryEntry? entry))
        {
            throw new InvalidOperationException($"Durable item '{definition.ItemId}' is unavailable.");
        }

        int damage = Math.Min(action.Quantity, entry.Durability);
        if (damage == 0)
            throw new InvalidOperationException($"Item '{definition.ItemId}' is already broken.");
        inventory[definition.ItemId] = entry with { Durability = entry.Durability - damage };
        events.Add(new MmoEconomyEventRecord(
            action.Sequence,
            action.Tick,
            MmoEconomyEventKind.DurabilityDamaged,
            definition.ItemId,
            damage,
            0));
    }

    private static void Repair(
        MmoEconomyActionInput action,
        IReadOnlyDictionary<string, MmoEconomyItemDefinition> definitions,
        IDictionary<string, MmoEconomyInventoryEntry> inventory,
        ref int currency,
        ICollection<MmoEconomyEventRecord> events)
    {
        MmoEconomyItemDefinition definition = GetItem(action.ReferenceId, definitions);
        if (definition.MaximumDurability <= 0 ||
            !inventory.TryGetValue(definition.ItemId, out MmoEconomyInventoryEntry? entry))
        {
            throw new InvalidOperationException($"Durable item '{definition.ItemId}' is unavailable.");
        }

        int restored = Math.Min(action.Quantity, definition.MaximumDurability - entry.Durability);
        if (restored == 0)
            throw new InvalidOperationException($"Item '{definition.ItemId}' is already fully repaired.");
        int cost = checked(restored * definition.RepairCostPerPoint);
        if (currency < cost)
            throw new InvalidOperationException($"Item '{definition.ItemId}' lacks its required repair currency.");

        inventory[definition.ItemId] = entry with { Durability = entry.Durability + restored };
        currency = checked(currency - cost);
        events.Add(new MmoEconomyEventRecord(
            action.Sequence,
            action.Tick,
            MmoEconomyEventKind.ItemRepaired,
            definition.ItemId,
            restored,
            -cost));
    }

    private static MmoEconomyItemDefinition GetItem(
        string itemId,
        IReadOnlyDictionary<string, MmoEconomyItemDefinition> definitions)
    {
        if (!definitions.TryGetValue(itemId, out MmoEconomyItemDefinition? definition))
            throw new ArgumentException($"Unknown economy item definition '{itemId}'.", nameof(itemId));
        return definition;
    }

    private static Dictionary<string, MmoEconomyInventoryEntry> Clone(
        IReadOnlyDictionary<string, MmoEconomyInventoryEntry> inventory) =>
        inventory.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

    private static void Add(
        MmoEconomyItemDefinition definition,
        int quantity,
        IDictionary<string, MmoEconomyInventoryEntry> inventory)
    {
        int existing = inventory.TryGetValue(definition.ItemId, out MmoEconomyInventoryEntry? entry)
            ? entry.Quantity
            : 0;
        int updated = checked(existing + quantity);
        if (updated > definition.MaximumStack)
            throw new InvalidOperationException($"Item '{definition.ItemId}' exceeds its maximum stack.");
        if (existing == 0 && inventory.Count >= MaximumInventorySlots)
            throw new InvalidOperationException("MMORPG economy inventory capacity was exceeded.");

        int durability = entry?.Durability ?? definition.MaximumDurability;
        inventory[definition.ItemId] = new MmoEconomyInventoryEntry(
            definition.ItemId,
            updated,
            durability);
    }

    private static void Remove(
        string itemId,
        int quantity,
        IDictionary<string, MmoEconomyInventoryEntry> inventory)
    {
        if (!inventory.TryGetValue(itemId, out MmoEconomyInventoryEntry? entry) ||
            entry.Quantity < quantity)
        {
            throw new InvalidOperationException($"Item '{itemId}' lacks its required quantity.");
        }

        int remaining = entry.Quantity - quantity;
        if (remaining == 0)
            inventory.Remove(itemId);
        else
            inventory[itemId] = entry with { Quantity = remaining };
    }

    private static void Validate(MmoEconomyInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ValidateIdentifier(input.TransactionId, nameof(input.TransactionId));
        ValidateIdentifier(input.CharacterId, nameof(input.CharacterId));
        if (input.StartTick < 0)
            throw new ArgumentOutOfRangeException(nameof(input), "Economy ticks cannot be negative.");
        if (input.StartingCurrency is < 0 or > MaximumCurrency)
            throw new ArgumentOutOfRangeException(nameof(input), "Starting currency exceeds the economy bound.");
        if (!AzerothRegionAuthority.CreateOrderedZoneIds().Contains(input.RegionZoneId, StringComparer.Ordinal))
            throw new ArgumentException("MMORPG economy transactions are limited to the four owner-negotiated Azeroth regions.", nameof(input));

        ArgumentNullException.ThrowIfNull(input.Items);
        if (input.Items.Count > MaximumDefinitions)
            throw new ArgumentOutOfRangeException(nameof(input), "Too many economy item definitions.");
        RequireUnique(input.Items.Select(item => item.ItemId), "economy item");
        foreach (MmoEconomyItemDefinition item in input.Items)
        {
            ValidateIdentifier(item.ItemId, nameof(input.Items));
            ValidateDisplayName(item.DisplayName, nameof(input.Items));
            if (item.MaximumStack is < 1 or > 1_000 ||
                item.VendorBuyPrice is < 0 or > 1_000_000 ||
                item.VendorSellPrice < 0 ||
                item.VendorSellPrice > item.VendorBuyPrice ||
                item.MaximumDurability is < 0 or > 1_000_000 ||
                item.RepairCostPerPoint is < 0 or > 1_000_000 ||
                (item.MaximumDurability == 0 && item.RepairCostPerPoint != 0) ||
                (item.MaximumDurability > 0 && (item.MaximumStack != 1 || item.RepairCostPerPoint == 0)))
            {
                throw new ArgumentOutOfRangeException(nameof(input), $"Economy item '{item.ItemId}' exceeds deterministic bounds.");
            }
        }

        var definitions = input.Items.ToDictionary(item => item.ItemId, StringComparer.Ordinal);
        ArgumentNullException.ThrowIfNull(input.Recipes);
        if (input.Recipes.Count > MaximumDefinitions)
            throw new ArgumentOutOfRangeException(nameof(input), "Too many recipe definitions.");
        RequireUnique(input.Recipes.Select(recipe => recipe.RecipeId), "recipe");
        foreach (MmoRecipeDefinition recipe in input.Recipes)
        {
            ValidateIdentifier(recipe.RecipeId, nameof(input.Recipes));
            ValidateDisplayName(recipe.DisplayName, nameof(input.Recipes));
            ValidateIdentifier(recipe.OutputItemId, nameof(input.Recipes));
            ArgumentNullException.ThrowIfNull(recipe.Ingredients);
            if (!definitions.TryGetValue(recipe.OutputItemId, out MmoEconomyItemDefinition? output) ||
                recipe.OutputQuantity is < 1 or > 1_000 ||
                recipe.OutputQuantity > output.MaximumStack ||
                recipe.CurrencyCost is < 0 or > 10_000_000 ||
                recipe.Ingredients.Count is < 1 or > 64)
            {
                throw new ArgumentOutOfRangeException(nameof(input), $"Recipe '{recipe.RecipeId}' exceeds deterministic bounds.");
            }
            RequireUnique(recipe.Ingredients.Select(ingredient => ingredient.ItemId), "recipe ingredient");
            foreach (MmoRecipeIngredient ingredient in recipe.Ingredients)
            {
                ValidateIdentifier(ingredient.ItemId, nameof(input.Recipes));
                if (!definitions.ContainsKey(ingredient.ItemId) ||
                    ingredient.Quantity is < 1 or > 1_000)
                {
                    throw new ArgumentOutOfRangeException(nameof(input), $"Recipe '{recipe.RecipeId}' has an invalid ingredient.");
                }
            }
        }

        ArgumentNullException.ThrowIfNull(input.StartingInventory);
        if (input.StartingInventory.Count > MaximumInventorySlots)
            throw new ArgumentOutOfRangeException(nameof(input), "Starting inventory exceeds its slot bound.");
        RequireUnique(input.StartingInventory.Select(entry => entry.ItemId), "starting inventory");
        foreach (MmoEconomyInventoryEntry entry in input.StartingInventory)
        {
            if (!definitions.TryGetValue(entry.ItemId, out MmoEconomyItemDefinition? definition) ||
                entry.Quantity is < 1 ||
                entry.Quantity > definition.MaximumStack ||
                (definition.MaximumDurability == 0 && entry.Durability != 0) ||
                (definition.MaximumDurability > 0 &&
                    (entry.Quantity != 1 || entry.Durability < 0 || entry.Durability > definition.MaximumDurability)))
            {
                throw new ArgumentOutOfRangeException(nameof(input), $"Starting inventory item '{entry.ItemId}' is invalid.");
            }
        }

        ArgumentNullException.ThrowIfNull(input.Actions);
        if (input.Actions.Count > MaximumActions)
            throw new ArgumentOutOfRangeException(nameof(input), "Too many economy actions.");
        long previousTick = input.StartTick;
        for (int index = 0; index < input.Actions.Count; index++)
        {
            MmoEconomyActionInput action = input.Actions[index] ??
                throw new ArgumentException("Economy actions cannot be null.", nameof(input));
            ValidateIdentifier(action.ReferenceId, nameof(input.Actions));
            if (action.Sequence != index ||
                action.Tick < previousTick ||
                action.Quantity is < 1 or > 1_000)
            {
                throw new ArgumentException("Economy actions require contiguous sequence IDs, nondecreasing ticks, and bounded positive quantities.", nameof(input));
            }
            previousTick = action.Tick;
        }
    }

    private static void RequireUnique(IEnumerable<string> identifiers, string kind)
    {
        string[] values = identifiers.ToArray();
        if (values.Any(string.IsNullOrWhiteSpace) ||
            values.Distinct(StringComparer.Ordinal).Count() != values.Length)
        {
            throw new ArgumentException($"MMORPG {kind} identifiers must be nonblank and unique.", kind);
        }
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
