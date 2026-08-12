# Azeroth Four-Region Authority Contract

## Exact negotiated scope

The active Azeroth simulation authority contains exactly four regions:

1. Eversong — `ek.north.eversong`
2. Ghostlands — `ek.north.ghostlands`
3. Eastern Plaguelands — `ek.north.eastern-plaguelands`
4. Western Plaguelands — `ek.north.western-plaguelands`

The C# authority is `owner-negotiated-four-region-only`. Adding, removing, renaming, duplicating, or substituting a region requires a new explicit owner negotiation. Runtime input and derived layers cannot expand the set.

## Authority limits

The region/zone identifiers establish membership only. They do not authorize coordinates, latitude/longitude, coastlines, terrain, roads, routes, waterways, climate, local scenes, globe placement, physical scale, or canon reconstruction. Simulation output remains `TestHistoryOnly`.

Omega, Lunara, Solune, environmental fields, orbital projection, and the Omega Sandbox remain downstream observations. None can mutate the region authority.

## Verification

`evermore azeroth regions --format json` emits `evermore.azeroth.region-authority/1.0` as a source-generated SHA-256 envelope. The serializer requires byte integrity and exact semantic equality with the four-entry C# contract.

The composed simulation, environmental field, orbital scene, managed harness, and clean Native AOT workflow all require the same exact set. Unauthorized, missing, duplicate, or fifth-region inputs fail closed.

