namespace Evermore.Mmo.Core;

public static class MmoCapabilityCatalog
{
    public const string Version = "0.3.0";
    public const string SourcePdfSha256 = "a2df020773d7dd35ef2e40387f5a8cc5f98d94d3fae09369134ce83f0e6a40c2";

    public static MmoCapabilityCatalogSnapshot Create() =>
        new(
            "€V€RMØRE MMORPG Engine",
            Version,
            "original-engine-neutral-csharp-native-aot-core",
            $"requirements-only-no-source-recovered:{SourcePdfSha256}",
            [
                Capability("identity.accounts", "Accounts, authentication, sessions, and entitlements", MmoCapabilityStatus.AdapterRequired, "Identity provider, credential storage, and authorization policy."),
                Capability("characters.progression", "Characters, archetypes, attributes, resources, levels, and progression", MmoCapabilityStatus.ImplementedCore, "Deterministic character/resource/progression state; content tuning remains authored."),
                Capability("world.regions", "World regions, shards, phase state, streaming, and transfers", MmoCapabilityStatus.AuthorityBound, "Membership is limited to the four owner-negotiated Azeroth region identifiers; no geography is inferred."),
                Capability("movement.navigation", "Movement, collision, navigation, mounts, transport, and flight", MmoCapabilityStatus.AdapterRequired, "Requires authored geometry, physics, navigation data, and a renderer/runtime adapter."),
                Capability("combat.abilities", "Combat, abilities, targeting, resources, cooldowns, auras, threat, and death", MmoCapabilityStatus.ImplementedCore, "Deterministic single-target damage/resource/cooldown slice; auras and threat remain planned."),
                Capability("items.inventory", "Items, bags, equipment, loot, durability, and itemization", MmoCapabilityStatus.ImplementedCore, "Deterministic bounded inventory, stacking, loot, and equipment slice."),
                Capability("quests.objectives", "Quests, objectives, rewards, dialogue, reputation, and achievements", MmoCapabilityStatus.ImplementedCore, "Deterministic accept/defeat/progress/complete/reward slice."),
                Capability("economy.crafting", "Currency, vendors, crafting, gathering, repair, trade, mail, and auctions", MmoCapabilityStatus.ImplementedCore, "Deterministic gathering, recipe, vendor, durability, and repair slice; player trade, mail, auctions, persistence, and abuse controls remain separate boundaries."),
                Capability("social.groups", "Friends, ignore, parties, raids, guilds, chat, calendars, and communities", MmoCapabilityStatus.ImplementedCore, "Deterministic party/raid membership, leadership, role, invitation, and ready-check slice; friends, ignore, guilds, chat, calendars, communities, identity, presence, moderation, persistence, and realtime transport remain separate boundaries."),
                Capability("instances.matchmaking", "Dungeons, raids, scenarios, matchmaking, lockouts, and resets", MmoCapabilityStatus.Planned, "Requires orchestration, encounter authoring, persistence, and server allocation."),
                Capability("pvp.competition", "Duels, battlegrounds, arenas, teams, ratings, seasons, and rewards", MmoCapabilityStatus.Planned, "Requires authoritative networking, matchmaking, anti-cheat, and seasonal governance."),
                Capability("npcs.behavior", "NPC spawning, behavior, schedules, services, pets, and companions", MmoCapabilityStatus.Planned, "Requires authored spawn authority, navigation, behavior graphs, and persistence."),
                Capability("network.security", "Authoritative networking, replication, interest management, security, and anti-cheat", MmoCapabilityStatus.AdapterRequired, "Must be designed and threat-modeled as a separate server boundary; no proprietary protocol emulation."),
                Capability("persistence.operations", "Persistence, transactions, sharding, migration, backup, rollback, and live operations", MmoCapabilityStatus.AdapterRequired, "Requires an owner-selected data store, schema lifecycle, disaster recovery, and operations policy."),
                Capability("content.asset-pipeline", "Asset validation, dependency manifests, hashes, previews, presets, and packages", MmoCapabilityStatus.AdapterRequired, "PDF requirement mapped to a Unity Editor adapter; the Native AOT core does not import Unity assemblies."),
                Capability("audio.adaptive-runtime", "Ambience, zones, occlusion, music scheduling, narrative memory, crowds, footsteps, combat cues, and voices", MmoCapabilityStatus.AdapterRequired, "PDF A0-A9 requirement mapped to an engine audio adapter; no audio backend is embedded in the simulation core."),
                Capability("presentation.accessibility", "UI, HUD, input, localization, accessibility, cinematics, and visual effects", MmoCapabilityStatus.AdapterRequired, "Renderer/client responsibility using the canonical €V€RMØRE production palette."),
                Capability("delivery.observability", "Telemetry, diagnostics, moderation, admin tools, patching, deployment, compatibility, and support", MmoCapabilityStatus.AdapterRequired, "Requires privacy, security, release, support, and deployment decisions.")
            ]);

    private static MmoCapability Capability(
        string key,
        string name,
        MmoCapabilityStatus status,
        string ownershipBoundary) =>
        new(key, name, status, ownershipBoundary);
}
