# €V€RMØRE 3D Environment Contract

## Host boundary

`Evermore.ThreeD.Environment` is a separate all-C# `net10.0` Native AOT executable. It hosts Omega, Lunara, Solune, the Azeroth composite, environmental fields, the orbital scene, and the isolated Omega Sandbox in one in-process, self-contained application graph.

The first renderer is a deterministic dependency-free software framebuffer that writes binary PPM (`P6`) only to an explicit caller-supplied path. It never overwrites an existing file. No browser, game engine, managed plugin loader, reflection serializer, GPU runtime, window toolkit, network service, or external map provider is required.

## Visual authority

Output is `ConstrainedSimulationConceptOnly` under `TestHistoryOnly` authority. The renderer owns only generic sphere shading, atmosphere-shell glow, mana-channel glow, lighting response, stars, and framebuffer encoding.

It does not own or infer:

- continent or coast geometry;
- terrain, cities, roads, waterways, or local scenes;
- weather or climate;
- physical radius, circumference, gravity, kilometers, or miles;
- latitude or longitude;
- Solune or Lunara physical placement;
- zone placement on the globe.

Every zone remains `missing-authored-globe-vector`. The supplied/generated orbital artwork remains a style reference and is not sampled or embedded as simulation data.

## Bounded framebuffer

- width and height: `64..2048` pixels;
- total pixels: at most `4,194,304`;
- output: deterministic RGB24 PPM (`P6`);
- palette: VOID, SOLAR, ABYSS, and ARCANE production families;
- contained simulation: no framebuffer is written.

## Commands

```text
evermore-3d status
evermore-3d render --input FILE --output FILE --width N --height N \
  --azimuth-micros N --elevation-micros N --zoom N
```

The input is the Revision 14 environmental-field input. The host constructs and verifies the Revision 15 orbital scene in-process before rendering.

## Omega Sandbox boundary

The sandbox is an isolated, deterministic, non-canon construction surface. One bounded Omega run supplies observational trait channels, after which a single repository-owned deterministic stream creates ordered hearthstar systems, worlds, moons, and their vectors. It cannot read from or write to Azeroth/Eastern Kingdoms state.

The sandbox supports:

- `1..512` hearthstar systems;
- `1..32` worlds per system;
- `0..16` moons per world;
- at most `100,000` bodies per snapshot;
- `1..1,024` Omega generations;
- simulation ticks `0..1,000,000`;
- bounded advances of `1..10,000` ticks per command;
- exactly 18 normalized integer axes per body.

The axes are `X`, `Y`, `Z`, `TemporalPhase`, `Mana`, `Atmosphere`, `Radiance`, `Tidal`, `Resonance`, the eight canonical Omega trait channels, and `ContainmentIntegrity`. They are symbolic sandbox channels in `-1,000,000..1,000,000`, not a claim that real or fictional physical spacetime has eighteen dimensions. `X/Y/Z` are sandbox coordinates only and are not geography, latitude, longitude, route geometry, or authored world placement.

Every generated body has a stable ordinal identifier, explicit body kind, explicit parent identifier, normalized presentation scale, and an exact 18-value vector. Hearthstars occupy global sandbox coordinates; world coordinates are bounded relative to their hearthstar and moon coordinates are bounded relative to their world. Those neighborhoods are presentation structure, not measured orbit radii. Names are implementation IDs, never canon names. A sandbox snapshot is source-generated JSON inside a SHA-256 envelope and must pass both byte-integrity and semantic replay verification before advancement or projection.

## Deterministic time evolution

Tick zero preserves the Revision 16 construction. Any later tick is recomputed directly from the original bounded input, stable body identity, and requested absolute tick; advancement never compounds floating-point state or mutates the input snapshot. Body identifiers, kinds, parent identifiers, ordinals, counts, and presentation scales remain stable. The 18 axes are re-evaluated with integer-only bounded motion, while parent-relative spatial neighborhoods remain enforced.

The timeline is an isolated sandbox observation. A tick is not a date, calendar fact, physical duration, orbital ephemeris, history generation, or canon event. Advancing cannot write to Omega Recursive, Lunara, Solune, Azeroth, Eastern Kingdoms, or any upstream authority store.

## Sandbox commands

```text
evermore-3d sandbox-create --seed U64 --generations N --systems N \
  --worlds N --moons N --output SNAPSHOT.json

evermore-3d sandbox-advance --input SNAPSHOT.json --ticks N \
  --output ADVANCED.json

evermore-3d sandbox-render --input SNAPSHOT.json --output FRAME.ppm \
  --width N --height N --axis-x 0..17 --axis-y 0..17 --axis-depth 0..17
```

Projection axes must be distinct. The renderer maps exactly three selected channels at the verified snapshot tick into a deterministic two-dimensional RGB24 observation while retaining all eighteen channels. Hearthstars use the SOLAR family, worlds use ABYSS/ARCANE, moons use VOID/ABYSS, and the background uses VOID. Projection cannot mutate the sandbox or any authoritative simulation state.
