# €V€RMØRE MMORPG Core

Revision 19 introduces an original, engine-neutral C# MMORPG session kernel for net10.0 Native AOT. Revisions 20 and 21 add deterministic economy and social-group kernels. This is not a proprietary game-server reimplementation and does not emulate a proprietary client or network protocol.

The executable vertical slice covers:

- the exact four owner-negotiated Azeroth region identifiers;
- bounded character health, primary resources, experience, and currency;
- item definitions, loot, stacking, inventory capacity, and equipment;
- abilities, resource costs, deterministic cooldowns, damage, and target defeat;
- quest acceptance, objective progress, completion, and rewards;
- deterministic gathering, recipes, crafting, vendor buy/sell, durability, and repair;
- deterministic party/raid invitations, membership, leadership, roles, leave/remove operations, and ready checks;
- source-generated JSON, SHA-256 integrity, and semantic replay verification.

MmoCapabilityCatalog maps 18 production domains. A domain marked AdapterRequired or Planned is deliberately not presented as implemented. In particular, networking, authentication, persistence, authoritative movement, Unity asset processing, and realtime audio require separate adapters and owner decisions.

Run the Native AOT CLI:

    evermore mmo capabilities --format json
    evermore mmo demo --format json
    evermore mmo economy-demo --format json
    evermore mmo group-demo --format json

MMORPG session output remains TestHistoryOnly. Region membership supplies identifiers only—never coordinates, geometry, routes, terrain, or local scenes.

Player-to-player trade, mail, auctions, payment systems, persistent balances, fraud controls, and live content tuning remain explicit service and owner boundaries. See ECONOMY_CONTRACT.md.

Friends, ignore lists, guilds, chat, calendars, communities, identity, presence, moderation, persistence, networking, and matchmaking remain explicit boundaries. See GROUP_CONTRACT.md.
