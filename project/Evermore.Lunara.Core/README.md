# Evermore.Lunara.Core

`Evermore.Lunara.Core` is a dependency-free, deterministic, non-canon moon/tide and ritual decision engine for `net10.0`. It is an isolated symbolic worldbuilding system. It does not claim astronomical, oceanographic, medical, psychological, genetic, geographical, or Eastern Kingdoms continuity authority.

`LunaraEngine` is the public façade over `DeterministicMoonTideProcessor`; both return the same immutable `MoonTideSnapshot` contract.

The core:

- uses explicit seed, generation, relic-population, API-response, reward, event, and history bounds;
- evaluates fixed-point lunar and tide equations without floating-point or runtime reflection;
- processes relics, rituals, and responses in stable ordinal order;
- preserves each relic's fictional `SoulSignature` across history;
- clamps remote rewards to `0..0.25` charge per relic per response;
- runs containment before applying tentative rewards, ritual-count changes, deterministic branching, or manifestation authorization;
- emits pure `LunaraManifestationDirective` values and performs no spawn, network write, file write, or Eastern Kingdoms mutation;
- uses source-generated `System.Text.Json` metadata and a SHA-256 snapshot envelope.

`LunaraApiClient` accepts an injected `ILunaraApiTransport`. The transport owns the unprovided endpoint, authentication, retry, and wire-schema decisions. The client correlates the returned ritual and normalizes duplicate raw rewards into one bounded reward per relic. A server `ManifestationRequested` value is preserved for fail-closed local containment; it is never executed.

See `MOON_TIDE_CONTRACT.md`, `SOURCE_DISPOSITION.md`, and `CONTINUITY_LEDGER.md`.
