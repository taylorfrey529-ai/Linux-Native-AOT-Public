# Solune Local-Sun Contract 1.0

## Authority

Solune output is an isolated, deterministic, non-canon feasibility result for a fictional local stellar construct. It cannot read or mutate Eastern Kingdoms state, author geography or climate, actuate a construct, spawn an entity, or promote Test History into Working Continuity. The supplied plate is a model proposal and visual-reference source; its labels are not independently verified astrophysical or world-state evidence.

All normalized values use integer micro-units where `U = 1,000,000`. Distances are supplied integer kilometres. Luminosity is supplied in terawatts, irradiance in milliwatts per square metre, angular diameter in microdegrees, rotations in microhours, and the orbital period in ten-thousandths of a day. Integer division truncates toward zero except the declared period reconciliation, which rounds to the nearest ten-thousandth day. Large products use `BigInteger`; every serialized result remains finitely bounded `int` or `long` data.

## Supplied reference model

| Quantity | Fixed representation | Display value |
| --- | ---: | ---: |
| Evermore radius | `6,378 km` | `6,378 km` |
| Solune radius | `1,738 km` | `1,738 km` |
| Orbit radius | `373,089 km` | `373,089 km` |
| Orbital period | `260,893 × 10⁻⁴ days` | `26.0893 days` |
| Revolutions per Evercycle | `14` | `14` |
| Luminosity | `2,382,000,000 TW` | `2.382 × 10²¹ W` |
| Photosphere | `5,768 K` | `5,768 K` |
| Apparent diameter | `533,800 µdeg` | `0.5338°` |
| Sidereal rotation | `23,114,000 µh` | `23.114 h` |
| Local solar day | `24,000,000 µh` | `24 h` |
| Tidal-force ratio | `1,094,000` | `1.094 × Earth–Moon` |
| Barycenter | `4,531 km` | `4,531 km from planet center` |
| Orbit shape | `e = 0`, `i = 0 µdeg` | near-circular equatorial |

The model is accepted only with explicit local construct authorization and containment integrity at or above `0.95`.

## Equations

The constant `πµ = 3,141,593`. The Stefan–Boltzmann calibration is the exact decimal rational `5,670,374,419 / 10¹⁷ W·m⁻²·K⁻⁴`.

### Evercycle and orbital phase

```text
evercycle = 365.25 days = 365,250,000 microdays
base calendar = 14 × 26 = 364 days
reconciliation = Everwake (1 day) + Stillstar (0.25 day)
period₁₀₋₄day = round(evercycle / (revolutions × 100 microdays))
orbitalProgress = evercycleStepMicrodays × revolutions
revolutionIndex = floor(orbitalProgress / evercycle)
phaseMicros = floor((orbitalProgress mod evercycle) × U / evercycle)
```

For fourteen revolutions, the rounded period is exactly `260,893` ten-thousandths of a day, or `26.0893 days`.

### Barycentric mass ratio

For barycenter offset `b` and center-to-center orbit radius `d`:

```text
impliedMassRatioMicros = floor(b × U / (d - b))
```

The supplied values produce `12,293` micro-units. Containment additionally requires `b < EvermoreRadiusKm`; the supplied `4,531 km` remains within `6,378 km`.

### Blackbody luminosity

For Solune radius `r` in kilometres and photosphere `T` in kelvin:

```text
calculatedLuminosityTW = floor(
    4 × πµ × 5,670,374,419 × r² × T⁴ / 10²⁹)
```

The supplied radius and temperature produce `2,382,441,248 TW`. The declared `2,382,000,000 TW` differs by `185` micro-units (`0.0185%`), within the `5,000`-micro-unit containment tolerance.

### Irradiance at Evermore

For declared luminosity `L` in terawatts and orbit radius `d` in kilometres:

```text
irradianceMilliwattsPerSquareMeter = floor(
    L × 10¹⁵ / (4 × πµ × d²))
```

The supplied model produces `1,361,780 mW/m²` (`1,361.780 W/m²`). The bounded construct envelope is `1,200,000..1,500,000 mW/m²`. This is a top-of-model radiative gate, not a climate, ecology, weather, or habitability claim.

### Apparent diameter

The deterministic small-angle equation is:

```text
apparentDiameterMicrodegrees = floor(
    2 × r × 180 × U² / (πµ × d))
```

The supplied geometry produces `533,813 µdeg`; the plate's `533,800 µdeg` differs by `13 µdeg`, within the `1,000 µdeg` tolerance.

### Normalized tidal-force ratio

The model uses declared reference-calibration constants `qref = 12,300 µ` and `dref = 384,400 km`:

```text
tidalRatioMicros = floor(
    impliedMassRatioMicros × dref³ × U / (qref × d³))
```

The supplied model produces `1,093,114`; the declared `1,094,000` differs by `886` micro-units. The reference calibration exists only to reproduce the plate's normalized comparison and does not grant real-world or Eastern Kingdoms physical authority.

### Local solar day

For prograde sidereal rotation `R` and orbital period `P`, both expressed in microhours:

```text
calculatedSolarDayMicrohours = floor(R × P / (P - R))
```

The supplied `23.114 h` rotation and `26.0893-day` period produce `23,999,955 µh`; the declared `24 h` differs by `45 µh`.

## Calendar partition

The `[0, 365.25-day)` Evercycle is partitioned without floating point:

1. `0..364 days`: fourteen Everturns, each containing twenty-five ordinary days and one calendar day.
2. `364..365 days`: Everwake.
3. `365..365.25 days`: Stillstar.

The engine reports only the calendar partition and orbital phase. It does not infer seasons, sunrise direction, latitude, climate, terrain, holidays, or local history.

## Finite bounds

| Field | Bound |
| --- | --- |
| Generation | `0..1,000,000` |
| Evercycle step | `0..365,249,999 microdays` |
| History points | `0..128`, strictly increasing and earlier than current generation |
| Body radius | `1..100,000 km` |
| Orbit radius | `10,000..10,000,000 km`, greater than the two supplied radii |
| Luminosity | `1..1,000,000,000,000 TW` |
| Photosphere | `1..50,000 K` |
| Revolutions | `1..1,024`; containment requires exactly `14` |
| Implied barycentric mass ratio | `0..10,000,000 micro-units` |
| Eccentricity | `0..U`; containment requires at most `10,000` |
| Inclination | `-180°..180°`; containment requires absolute value at most `1°` |
| Containment integrity | `0..U`; continuation requires at least `950,000` |
| Snapshot events / directives | `0..16` / `0..1` |
| IDs / continuity signatures | `1..96` / `1..128` nonblank characters |
| Event and containment detail | `1..512` nonblank characters |
| CLI input file | at most `16,777,216 bytes` before JSON parsing |

History additionally limits per-generation changes to `100 km` orbit drift, `0.5%` luminosity drift, `25 K` photosphere drift, and `0.025` containment-integrity loss.

## Deterministic processing order

1. Validate finite bounds, required identity, history order, and calculable denominators.
2. Sort history by generation.
3. Compute orbit, radiance, rotation, tidal calibration, and calendar observations.
4. Evaluate history-wide identity and bounded state continuity.
5. Evaluate local authorization and containment before any successful outcome branch.
6. On containment, return the original model, no operation directive, and exactly one canonical containment event.
7. Only after clearance, return one pure `MAINTAIN_CONTAINED_STELLAR_CONSTRUCT` directive.
8. Serialize the complete bounded input/history and derived result with source-generated JSON, then hash the canonical payload bytes with SHA-256.

Deserialization reruns the processor from the embedded input/history and requires byte-identical disposition, equations, containment, directive, and events before accepting the hash. Recomputing a hash over equation-inconsistent or authority-inconsistent data does not make it valid.

## Containment

Containment fails closed for missing local construct authorization, continuity-identity drift, excessive historical state drift, a revolution-count mismatch, non-near-circular/non-equatorial geometry, integrity below `0.95`, a barycenter outside Evermore, period/calendar mismatch, blackbody luminosity mismatch, irradiance outside the local-sun envelope, apparent-diameter mismatch, normalized tidal-force mismatch, or local-solar-day mismatch.

The returned directive is not an actuator. The library has no renderer, stellar-control interface, entity factory, filesystem writer, network transport, Eastern Kingdoms store, or canon-promotion capability.
