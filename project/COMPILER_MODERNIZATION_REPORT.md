# Phase XXIII QA2 - Compiler Modernization Report (Historical)

> Historical provenance only. The active workspace is migrated to .NET SDK 10.0.302, `net10.0`, and C# 14. See `../CERTIFICATION_STATUS.md` for the current validation boundary.

## Certified toolchain

- .NET SDK: 8.0.423
- Roslyn C# compiler: 4.11.0-3.25569.22
- Target framework: net8.0
- Stable language level: C# 12.0

## Central compiler policy

- `global.json` pins the certified SDK and permits patch-only roll-forward.
- `Directory.Build.props` centralizes language version and compiler/analyzer policy.
- Nullable reference analysis is enabled.
- Compiler/analyzer warnings are treated as errors.
- Built-in .NET analyzers run at the latest level available to the certified SDK.
- Arithmetic overflow checking is enabled globally.
- Intentional unsigned wraparound in deterministic seed/RNG mixers is explicit with `unchecked`.
- Deterministic and CI build semantics are enabled.
- Portable PDB output is enabled.
- Code-style analysis is enabled during builds.

## Analyzer policy

Correctness-oriented diagnostics remain build-gating. A small set of non-semantic micro-optimization/naming rules are suggestions rather than errors: CA1512, CA1720, CA1834, CA1859, CA1861. CA1822 is disabled for intentionally instance-shaped service APIs. Test source additionally permits underscore-separated descriptive test names (CA1707) and prioritizes fixture readability over CA1826 micro-optimization.

## Modernization defect found

Enabling checked arithmetic exposed implicit overflow dependence inside the deterministic `GenomeRandom.NextUInt64` and `SeedMixer.Mix` algorithms. Those operations now use explicit `unchecked` blocks, documenting the intended modulo-2^64 wrap semantics while retaining overflow checking everywhere else.

## Certification

- Production Release build: PASS, 0 warnings, 0 errors.
- Offline authored-test harness build: PASS, 0 warnings, 0 errors.
- Authored facts run 1: 97/97 PASS.
- Authored facts run 2: 97/97 PASS.
- Release assembly SHA-256 build 1: `ad0fa9e31060de1621ee997f01a9ccdb76b525afc0e403a69a9155358e7a5f44`.
- Release assembly SHA-256 build 2: `ad0fa9e31060de1621ee997f01a9ccdb76b525afc0e403a69a9155358e7a5f44`.
- Deterministic rebuild comparison: PASS.

## Ceiling

This environment currently contains no newer SDK or Roslyn toolset than .NET SDK 8.0.423 / Roslyn 4.11. The QA2 baseline therefore uses the newest compiler that can be locally executed and certified here. A later SDK upgrade must be treated as a separate certification event rather than silently changing the compiler under a reproducible build.
