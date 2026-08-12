# Evermore .NET 10 Native AOT Workspace

The `project/` tree contains the C# source, contracts, fixtures, and managed regression harness for the public snapshot.

## Current implemented slice

The current candidate includes deterministic, bounded cores for:

- historical and ecological simulation;
- replay, camera, atlas, and renderer-neutral scene verification;
- isolated Omega, Lunara, and Solune processing;
- authority-constrained Azeroth composition and observations;
- a software-rendered 3D host and isolated 18-axis Omega sandbox;
- MMORPG session, economy, and social-group demonstrations.

All systems preserve their documented authority and containment boundaries. Identifiers and symbolic channels do not imply coordinates, physical scale, geography, atmosphere, canon, authentication, networking, persistence, or production readiness.

## Primary projects

```text
EasternKingdoms.Simulation/          Core historical/ecological simulation library
EasternKingdoms.Simulation.Tests/    Conventional test project
Evermore.Azeroth.Simulation/         Authority-constrained composition and observations
Evermore.Cli/                        Native AOT command host
Evermore.Genetics/                   Bounded genetics support library
Evermore.Lunara.Core/                Isolated moon/tide and ritual decision core
Evermore.Mmo.Core/                   Deterministic session, economy, and group kernels
Evermore.Solune.Core/                Isolated local-sun feasibility core
Evermore.ThreeD.Environment/         Native AOT software-rendering and sandbox host
Omega.Recursive/                     Isolated deterministic recursive engine
QA/OfflineHarness/                   Dependency-light managed regression harness
QA/NativeAotSmoke/                   Native AOT smoke application
```

## Pinned toolchain

- SDK: `10.0.302`
- Target framework: `net10.0`
- Language: C# 14
- Validation RID: `linux-x64`
- Native AOT packs: `10.0.10`

`global.json`, project lock files, and the Native AOT package hash manifest define the exact intake expected by the validation workflow.

## Validation boundaries

Configuration validation, managed validation, native publication, artifact inspection/execution, and release approval are separate states. A successful managed build does not prove Native AOT publication. A successful Native AOT publication does not grant production support or release approval.

The latest recorded application-source candidate passed 202 managed facts and independent `linux-x64` publication of the CLI and 3D host. See the repository-level `TECHNICAL_EVIDENCE.md` for hashes and explicit non-claims.

## Entry points

Use the repository-level scripts:

```bash
./scripts/preflight-linux-x64.sh
./scripts/certify-linux-x64.sh
```

The current full CLI/host validation route is:

```text
.github/workflows/evermore-cli-linux-x64.yml
```

Component-specific contracts and source-disposition files remain authoritative for their respective boundaries.
