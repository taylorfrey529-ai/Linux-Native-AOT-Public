# €V€RMØRE CLI

`evermore` is the Native AOT host for existing Eastern Kingdoms historical verification contracts, the isolated Omega Recursive import, the contained Lunara moon/tide engine, and the contained Solune local-sun feasibility candidate.

Its historical commands remain read-only: they do not advance evolution, create geography, authorize zones, promote Test History into canon, or mutate a replay, camera, or atlas scene. Camera and scene verification require the caller to supply the authorized zone set explicitly. Omega, Lunara, and Solune operate only on their own isolated/non-canon state and cannot access Eastern Kingdoms state.

## Commands

```text
evermore status [--format text|json]
evermore verify replay --input FILE [--format text|json]
evermore verify camera --input FILE --zone ZONE [--zone ZONE...] [--format text|json]
evermore verify scene --input FILE --zone ZONE [--zone ZONE...] [--format text|json]
evermore lunara run --input FILE [--format text|json]
evermore solune run --input FILE [--format text|json]
evermore omega run --generations COUNT --seed SEED [--format text|json]
```

Stable application exit codes:

- `0`: success
- `2`: invalid Omega, Lunara, or Solune simulation input
- `3`: input could not be read
- `4`: integrity or semantic verification failed
- `7`: Omega containment policy stopped the run
- `8`: Lunara containment stopped state application and manifestation authorization
- `9`: Solune containment stopped maintenance authorization
- `70`: unexpected internal failure

Parser and usage errors use `System.CommandLine`'s nonzero exit code.

Omega requires an explicit finite generation count and deterministic seed. It writes no snapshots or logs unless a future caller adds an explicit persistence adapter; the CLI returns a structured text or source-generated JSON summary.

Lunara requires an explicit JSON `MoonTideInput` no larger than 16 MiB. It computes fixed-point symbolic lunar/tide state, correlates accepted responses with local ritual attempts, and returns a SHA-256 envelope. Remote reward is clamped to `0..0.25` per relic per response. Remote manifestation requests, identity drift, authority mismatches, excessive aggregate rewards, state-limit violations, and branch fanout engage containment before returned relic state can change. Even a completed result contains only a pure local authorization directive; this host has no manifestation spawner. See `../Evermore.Lunara.Core/MOON_TIDE_CONTRACT.md`.

Solune requires an explicit JSON `SoluneInput` no larger than 16 MiB. It checks the contained hearthstar proposal with fixed-point orbital, radiance, apparent-diameter, barycentric, rotation, tidal-calibration, and Evercycle equations, then returns a SHA-256 envelope. Local construct authorization, history continuity, near-circular equatorial geometry, at least `0.95` containment integrity, and every numerical envelope must clear before one pure maintenance directive can be returned. The host has no stellar actuator. See `../Evermore.Solune.Core/SOLUNE_MODEL_CONTRACT.md`.

The project is configured for `net10.0` Native AOT and disables reflection-based JSON serialization. A managed build alone does not certify this executable; the `linux-x64` workflow must publish, inspect, and execute the native artifact.

The native validation route exercises canonical success paths plus malformed JSON, payload tampering, semantic tampering after payload-hash recomputation, unauthorized camera focus, Lunara remote-manifestation containment, Solune low-integrity containment, unreadable input, and byte-for-byte input immutability. These are technical validation checks; they do not replace owner-defined signing or provenance policy. The cumulative Lunara/Solune source change has no managed or Native AOT proof until that route completes for the exact candidate revision.
