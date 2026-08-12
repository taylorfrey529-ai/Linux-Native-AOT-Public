# Deterministic MMORPG Economy Contract

Revision 20 adds one bounded, engine-neutral, all-C# economy transaction kernel.

Implemented:

- material gathering into a bounded inventory;
- recipe ingredients, currency costs, and deterministic crafting;
- vendor purchases and sales with explicit authored prices;
- item durability damage and bounded partial repair;
- atomic action application, stable event ordering, source-generated JSON, SHA-256 integrity, and semantic replay.

The engine owns no database, network protocol, player-to-player trade, mail, auction house, payment system, marketplace, fraud service, or content economy. Those remain separate persistence, security, and service boundaries.

Every output remains TestHistoryOnly. Region admission uses exactly the four owner-negotiated identifiers and supplies no coordinates, geometry, routes, terrain, physical scale, or local scenes.
