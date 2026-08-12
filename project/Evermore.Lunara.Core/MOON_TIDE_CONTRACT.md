# Lunara Moon/Tide Contract 1.0

## Authority

Lunara output is an isolated, deterministic, non-canon simulation result. It cannot read or mutate Eastern Kingdoms state and cannot promote Test History into Working Continuity. `LunarPhaseStep`, `TidePhaseStep`, `BaselineTideMicros`, and `LocalTideResponseMicros` are caller-authored normalized inputs. The engine never infers geography, waterways, local terrain, physical distance, latitude/longitude, real lunar ephemerides, or local tide truth.

All normalized values use integer micro-units where `U = 1,000,000`. Integer division truncates toward zero. Every equation is evaluated with `long` intermediates and clamped where stated.

## Lunar cycle

The symbolic synodic cycle contains `N = 29,530` steps. For supplied phase step `p` in `0..N-1`:

```text
h = N / 2
d = min(p, N - p)
illumination = floor(d * U / h)
springAlignment = abs(2 * illumination - U)
phaseSector = floor(p * 8 / N)
```

This is a deterministic triangular illumination envelope, not a physical radiometry model. It reads `0` at new moon and `U` at the opposite full-moon step. `springAlignment` reads `U` near new/full and approaches `0` near quarter phases.

## Tide

The normalized local tide cycle contains `T = 12,420` steps. For supplied tide step `t`, baseline `b`, authored local response `r`, and lunar spring alignment `a`:

```text
q = T / 2
d = min(t, T - t)
normalizedDistance = floor(d * U / q)
signedWave = U - 2 * normalizedDistance
amplitude = 250,000 + floor(a / 4)
waveContribution = floor(signedWave * amplitude / U)
tideLevel = clamp(b + r + waveContribution, 0, U)
```

The amplitude therefore remains in `0.25..0.50` normalized units. `r` is bounded to `-0.25..0.25`; it must be authored by the caller and is never derived from map state.

## Resonance and manifestation probability

```text
resonance = floor((3 * illumination + springAlignment + tideLevel) / 5)
probability = clamp(
    50,000
    + max(0, charge - 850,000) / 2
    + max(0, resonance - 600,000) / 2
    + max(0, tideLevel - 500,000) / 4,
    0,
    250,000)
```

A relic becomes a candidate only when a matching API response is accepted, the cult and ritual are both locally validated, charge is at least `0.85`, local validated-ritual count is at least `3`, resonance is at least `0.60`, and tide level is at least `0.50`. Candidates are sorted by relic ID and ritual ID. A single SplitMix64-derived stream seeded from the explicit unsigned seed and generation produces one integer roll in `0..999,999` per candidate. A pure directive is returned only when `roll < probability`.

The directive is not a spawn. The core has no renderer, entity factory, filesystem writer, Eastern Kingdoms store, or implicit network behavior.

## Finite bounds

| Field | Bound |
| --- | --- |
| Seed | explicit unsigned 64-bit value; zero is valid |
| Generation | `0..1,000,000` |
| Relic population | `1..256` |
| Ritual attempts | `0..1,024` |
| API responses | `0..64` |
| Rewards per response | `0..256` |
| History points | `0..128`, strictly increasing, all earlier than current generation |
| Snapshot events | `0..512` |
| Manifestation directives | `0..4` |
| Relic charge | `0..1,000,000` |
| Reward per relic/response | clamped to `0..250,000` |
| Reward per relic/generation | containment above `500,000` |
| Local ritual count | `0..1,000,000` |
| IDs / soul signatures | `1..96` / `1..128` nonblank characters |
| Containment/event detail | `1..512` nonblank characters when present |
| CLI input file | at most `16,777,216` bytes before JSON parsing |

## Deterministic order

1. Validate all finite bounds, IDs, uniqueness, references, and history ordering.
2. Sort relics by relic ID; rituals by relic ID, cult ID, ritual ID; responses by response ID.
3. Compute lunar, tide, and resonance observations.
4. Correlate accepted responses to local rituals; group rewards by response then relic; clamp each group to `0.25`.
5. Build tentative relic states and manifestation candidates without mutating the input.
6. Evaluate containment.
7. On containment, return the original sorted relic states, no directives, and one containment event.
8. Only after clearance, commit bounded reward/count values to the returned snapshot, run the deterministic candidate rolls, and produce pure directives.
9. Serialize ordered read-only collections with source-generated JSON metadata; require every declared JSON member (nullable containment members remain present as `null` on completion); hash the exact canonical payload bytes with SHA-256.

Deserialization verifies the envelope schema and authority, hashes canonical payload bytes in fixed time, and rechecks lunar state, tide shape/band, resonance, finite bounds, relic/directive ordering, soul signatures, local manifestation thresholds, directive authority, probability equations, containment codes, and canonical event claims. A replacement hash does not make an equation- or event-inconsistent payload valid.

## Containment

Containment fires before state application or branching when a remote response requests manifestation, an accepted response has no local ritual, a reward references an unknown or mismatched relic, a historical soul signature drifts, protected historical state regresses or jumps beyond its bound, a relic exceeds the per-generation reward limit, a finite ritual-count limit is exceeded, or candidate fanout exceeds four. Charge is saturating within `0..U`; exceeding the separately bounded per-generation remote reward still contains rather than saturates through that authority gate.
