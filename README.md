# Evermore Linux Native AOT

A deterministic C# simulation and validation workspace targeting **.NET 10**, **C# 14**, and **Linux x64 Native AOT**.

This repository is a history-free public source snapshot. It contains bounded simulation cores, command-line hosts, tests, Native AOT validation tooling, and technical evidence summaries. It is intended for research, reproducibility, and engineering review.

## Current snapshot

- Public snapshot basis: private source commit `cac5908f6094cc13a4229a44f9fb72b0557e87dc`
- Application-source evidence commit: `5591fccba0bddbfcb07d467a0e690981c3705ead`
- SDK: .NET SDK `10.0.302`
- Target framework: `net10.0`
- Validation RID: `linux-x64`
- Native AOT pack version: `10.0.10`
- Managed harness result recorded for the application-source candidate: `202/202`
- License for original repository code: MIT

See [TECHNICAL_EVIDENCE.md](TECHNICAL_EVIDENCE.md) for the exact proof boundary and [PUBLICATION_SCOPE.md](PUBLICATION_SCOPE.md) for the snapshot exclusions.

## Included systems

- `Evermore.Cli` — read-only command host for status, replay, camera, scene, Omega, Lunara, Solune, Azeroth, and bounded MMORPG demonstrations.
- `Evermore.ThreeD.Environment` — dependency-light Native AOT host for deterministic software rendering and the isolated Omega sandbox.
- `Evermore.Mmo.Core` — bounded deterministic session, economy, and social-group kernels.
- `Evermore.Azeroth.Simulation` — authority-constrained composition, environmental-signal, region, and orbital observation layers.
- `EasternKingdoms.Simulation` — historical, ecological, lineage, atlas, and playback simulation components.
- `Omega.Recursive`, `Evermore.Lunara.Core`, and `Evermore.Solune.Core` — isolated symbolic engines with explicit containment contracts.
- `QA/OfflineHarness` and `tools/` — managed regression, source auditing, Native AOT publication, ELF inspection, package verification, and evidence generation.

## Authority and support boundary

The repository is deliberately narrower than a game, live service, physical-world model, or production platform.

- Outputs remain test, historical, symbolic, or observation data according to each component contract.
- Region identifiers do not establish coordinates, geometry, routes, terrain, physical scale, weather, atmosphere, or canon.
- Networking, authentication, persistence, payments, moderation, anti-cheat, deployment, proprietary protocols, and live-service adapters are not implemented.
- Recorded Native AOT evidence is technical evidence for exact source states. It is not production approval, security certification, signing/provenance, packaging approval, or a support guarantee.
- No third-party game source code or proprietary game assets are included in this public snapshot.

## Repository layout

```text
.github/workflows/               Current linux-x64 CLI/host validation route
project/                          C# source, tests, contracts, and fixtures
scripts/                          Linux x64 preflight, pack intake, and certification entrypoints
tools/                            Validation, publication, inspection, and evidence tools
TECHNICAL_EVIDENCE.md             Curated proof summary and non-claims
PUBLICATION_SCOPE.md              Public-snapshot origin and exclusions
SECURITY.md                       Vulnerability reporting guidance
THIRD_PARTY_NOTICES.md            Dependency, trademark, and ownership notices
```

## Build and validation

A compatible Linux x64 host needs the pinned .NET SDK and the native toolchain prerequisites used by the workflow: `clang`, `zlib1g-dev`, `binutils`, `file`, `unzip`, and `curl`.

Run the host preflight first:

```bash
./scripts/preflight-linux-x64.sh
```

For the repository-level Native AOT certification entrypoint, provide the pinned SDK and a directory containing the exact five Native AOT packages:

```bash
export DOTNET_ROOT=/path/to/dotnet-10.0.302
export AOT_PACKAGES=/path/to/native-aot-packs-net10.0.10-linux-x64
./scripts/certify-linux-x64.sh
```

The current CLI and 3D host route is also defined in `.github/workflows/evermore-cli-linux-x64.yml`. It performs strict source validation, managed regression, locked package intake, independent Native AOT publication, ELF/symbol inspection, direct execution, hostile-input checks, and evidence upload.

## Contributing

Read [CONTRIBUTING.md](CONTRIBUTING.md) before proposing changes. Changes that broaden authority, geography, supported RIDs, product claims, or release status need explicit evidence and maintainer approval.

## Security

This repository is not a hosted service, but build scripts, parsers, serializers, and CI workflows can still contain security defects. Follow [SECURITY.md](SECURITY.md) for responsible reporting.

## Legal

The MIT license applies to original repository code and documentation only. It does not grant rights in third-party trademarks, fictional settings, visual identities, or other third-party material. See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
