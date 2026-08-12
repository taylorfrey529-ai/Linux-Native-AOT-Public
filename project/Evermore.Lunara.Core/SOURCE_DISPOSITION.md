# Lunara source disposition

## Supplied contract identities

The request reported inspected SHA-256 `d55a5103d0892e3afe5831ff95ed3e3c9a33119dab8db8968918920cebbe6f08` for a `LunaraApiClient` contract and `df538b700c44a0362f01512335ed5b853f51fb3300574385807a59a6fb356e84` for a prior reconstruction assessment. The corresponding source bytes were not supplied, so these are user-reported provenance identifiers, not independently rehashed repository inputs.

The recoverable API rule is implemented directly: post a ritual attempt through an injected transport; normalize reward charge to `0..0.25` per relic per response; preserve any remote manifestation request only so local containment can reject it. Endpoint, credentials, retry policy, HTTP schema, pricing, and production service behavior remain unauthored owner decisions.

## Concept disposition

| Supplied concept | Disposition | Mapping |
| --- | --- | --- |
| `Evermore.Lunara.Core` | Implemented | Dependency-free `IsAotCompatible` library. |
| `LunaraEngine` | Implemented | Public façade over the deterministic processor; adds no external authority or side effects. |
| `LunarCycleState` | Implemented | Fixed-point phase, illumination, waxing, and alignment state. |
| `TideState` | Implemented | Caller-authored baseline/response plus deterministic local wave. |
| `MoonTideInput` | Implemented | Explicit seed, generation, phase, tide, population, ritual, response, and bounded history. |
| `MoonTideSnapshot` | Implemented | Immutable observation/result with relic states, directives, events, and containment. |
| `DeterministicMoonTideProcessor` | Implemented | Stable ordinal pipeline with one repository-owned PRNG stream. |
| `MoonTideContainmentPolicy` | Implemented | Fail-closed pre-mutation/pre-branch authority gate. |
| Remote reward clamp | Implemented twice | `LunaraApiClient` and core processing boundary. |
| Direct server manifestation | Rejected | Produces `REMOTE_MANIFESTATION_AUTHORITY_REJECTED`; no rewards or local mutations apply. |
| Real astronomy/oceanography | Excluded | The equations are explicitly normalized symbolic envelopes. |
| Eastern Kingdoms authority | Excluded | No project reference to Eastern Kingdoms; no state read/write. |
| HTTP endpoint and authentication | Deferred | No authoritative wire contract was supplied. |
