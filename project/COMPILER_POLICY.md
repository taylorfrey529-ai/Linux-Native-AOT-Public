# Compiler Policy - .NET 10 Migration

- Pinned SDK: .NET SDK 10.0.302.
- Stable language level: C# 14.0.
- Preview language mode is intentionally disabled for the certified baseline.
- `global.json` pins the certified SDK and allows only patch roll-forward.
- `Directory.Build.props` centralizes nullable analysis, built-in analyzers, warnings-as-errors, overflow checking, deterministic output, CI build semantics, portable PDBs, and code-style enforcement.
- A newer SDK may replace this baseline only after a clean compiler/test certification sweep.
- All active projects target `net10.0`.
- This file records configuration policy, not proof of a completed build. See `../CERTIFICATION_STATUS.md`.
