# Public Snapshot Scope

## Purpose

This repository is a history-free public source snapshot. The private source repository remains the internal source of record. Publishing this snapshot does not expose the private repository's branches or Git history.

## Source basis

```text
Private source commit: cac5908f6094cc13a4229a44f9fb72b0557e87dc
Snapshot date:         2026-08-12
```

## Excluded material

The public snapshot intentionally omits:

- internal agent continuity, instructions, and version-history records;
- owner-supplied inputs and visual references;
- private update-routing, workspace-manifest, and returned-evidence trees;
- sealed-source ZIP archives and source-specific manifests;
- nonessential visual binaries;
- historical generated QA logs and superseded status reports;
- the historical workflow and scripts that require the omitted sealed-source archive.

At preparation time, the principal excluded paths were:

```text
AGENTS.md
CERTIFICATION_STATUS.md
MANIFEST.sha256
SEALED_SOURCE_MANIFEST.sha256
archive/
logs/
memory/
user_files/
update-bindings/
sealed-source/
project/Visuals/
project/MANIFEST.sha256
project/BUILD_STATUS.md
project/NATIVE_AOT_RELEASE_EVIDENCE.md
project/QA4_CORRECTION_REPORT.md
project/STATIC_VERIFICATION.txt
project/QA/COMPILER_CERTIFICATION.txt
project/QA/Evidence/
project/QA/EvidenceQA4/
.github/workflows/native-aot-linux-x64.yml
scripts/certify-reproducible-linux-x64.sh
scripts/verify-returned-evidence-linux-x64.sh
```

## Retained material

The snapshot retains application source, tests, fixtures, component contracts, package locks and hashes, the current CLI/host workflow, Linux preflight and certification entrypoints, validation tools, the MIT license, and a curated technical-evidence summary.

## Integrity notes

Source-specific manifests that referenced excluded files were removed rather than left stale. The historical sealed-source reproducibility route was removed because its exact archive is not part of the public snapshot. Generated evidence directories are ignored so a local validation run cannot be mistaken for reviewed release evidence.

The current public snapshot must receive its own exact-commit validation before anyone describes the snapshot itself as technically certified. Historical evidence remains identified as historical evidence for its exact application-source commit.

## Publication rule

Do not make the private source repository public as a substitute for this snapshot. Doing so would expose all reachable Git history, including material intentionally excluded here.
