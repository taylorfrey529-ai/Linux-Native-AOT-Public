# Azeroth composed simulation

Revision 18 locks active membership to exactly these owner-negotiated regions:

1. Eversong — `ek.north.eversong`
2. Ghostlands — `ek.north.ghostlands`
3. Eastern Plaguelands — `ek.north.eastern-plaguelands`
4. Western Plaguelands — `ek.north.western-plaguelands`

`evermore azeroth regions --format text|json` emits the authoritative snapshot. The C# contract rejects missing, extra, duplicate, and substituted identifiers throughout composed, field, orbital, serialization, and CLI paths. These identifiers authorize membership only; they do not supply coordinates, geometry, adjacency, routes, terrain, physical scale, or local scenes. Broader membership requires explicit owner renegotiation. See [REGION_AUTHORITY_CONTRACT.md](REGION_AUTHORITY_CONTRACT.md).

Revision 15 adds `evermore azeroth orbit`, a renderer-neutral orbital scene manifest over the verified environmental field. It preserves the Earth:Azeroth `1.000000 : 1.000000` normalized display calibration, carries bounded view references and aggregate atmosphere/mana signals, and marks every zone `missing-authored-globe-vector` until explicit placement authority exists. See [ORBITAL_SCENE_CONTRACT.md](ORBITAL_SCENE_CONTRACT.md).

This C# project composes four bounded systems without merging their authority:

- **Azeroth / Eastern Kingdoms** supplies the authoritative four-zone Northern Scar corridor.
- **Omega** supplies deterministic recursive evolution as an isolated, non-canon observation.
- **Lunara** supplies deterministic lunar, tide, relic, and resonance observations.
- **Solune** supplies deterministic local-sun, orbit, radiance, and Evercycle observations.

The composite result is always `TestHistoryOnly`. Any component containment contains the whole composite run. No component may mutate another component, promote canon, spawn entities, or perform external writes.

Revision 14 adds authored normalized environmental-field signals for the same authorized zones. Solune irradiance and Lunara tide contribute to the atmosphere channel; Lunara resonance and Omega fitness contribute to the mana channel. Baselines and response coefficients are explicit bounded inputs, and the complete field can be reproduced from its embedded input.

The current authorized scope is not a full continent or globe. The field is not physical atmosphere, weather, climate, or a canonical mana ontology. It does not infer coordinates, adjacency, terrain, routes, or geography. Physical globe geometry remains deferred until authoritative inputs exist. See [ENVIRONMENTAL_FIELD_CONTRACT.md](ENVIRONMENTAL_FIELD_CONTRACT.md).

Run through the C# Native AOT CLI:

```text
evermore azeroth run --input path/to/valid-azeroth-simulation.json --format json
evermore azeroth field --input path/to/valid-azeroth-environmental-field.json --format json
evermore azeroth regions --format json
```
