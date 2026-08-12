# Contributing

Contributions are welcome when they preserve the repository's deterministic, bounded, evidence-driven design.

## Before opening a pull request

1. Keep changes scoped to one coherent purpose.
2. Preserve component authority, containment, and non-claim boundaries.
3. Add or update deterministic regression coverage.
4. Keep Native AOT compatibility explicit: avoid unsupported reflection, dynamic code generation, and unbounded serialization behavior.
5. Update source-generated JSON metadata when adding serialized types.
6. Record exact SDK, package, RID, and artifact evidence for Native AOT claims.
7. Do not add credentials, personal data, proprietary game assets, confidential source documents, or unlicensed third-party material.

## Validation

Run the relevant managed tests and static validators. For Native AOT changes, use the pinned `net10.0` / `linux-x64` route and inspect and execute the produced artifact. A successful managed build alone is not a Native AOT proof.

The current full workflow is:

```text
.github/workflows/evermore-cli-linux-x64.yml
```

## Pull request description

Describe what changed, why it changed, the affected authority boundary, validation performed, and any claims that remain deliberately unsupported.

## Scope changes

Changes to region membership, physical or spatial interpretation, supported RIDs, release status, production claims, signing/provenance, third-party content boundaries, or live-service behavior require explicit maintainer approval and new evidence. Do not infer those decisions from existing symbolic models or historical test data.
