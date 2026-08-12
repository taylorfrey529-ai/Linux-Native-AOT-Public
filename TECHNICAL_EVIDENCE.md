# Technical Evidence

## Snapshot identity

This public source snapshot was prepared from private source commit:

```text
cac5908f6094cc13a4229a44f9fb72b0557e87dc
```

The most recent recorded application-source evidence in that state applies to commit:

```text
5591fccba0bddbfcb07d467a0e690981c3705ead
```

The public-snapshot preparation removes internal continuity records, owner-supplied inputs, visual binaries, sealed-source archives, and private routing metadata. Those removals do not modify the application-source files covered by the recorded Revision 21 run, but the public snapshot itself must be validated independently before making a new exact-snapshot certification claim.

## Recorded Revision 21 result

GitHub Actions run `31549248609` recorded the following for the exact application-source candidate:

- strict source and Native AOT validation: zero errors and zero warnings;
- managed regression harness: `202/202` facts;
- .NET SDK `10.0.302`, `net10.0`, and Native AOT `10.0.10` package intake;
- clean `linux-x64` Native AOT publication of both executables;
- standalone sanitized execution of the social-group demonstration and the other workflow command paths;
- ELF, dependency, symbol, Build ID, and GLIBC inspection;
- hostile-input and fixture-immutability checks.

### `evermore`

```text
Size:            7,993,192 bytes
SHA-256:         ee0d17663d9aae63f449b11a7e671a11cf047985edbe27483734cef6a0a17bb8
Symbol SHA-256:  32b79393bd32b34ab0afbe21aef29a3c6632683a490c6506f87417bcfcca329b
GNU Build ID:    8a8a08eb546967f260920867d5a520012781fd9f
```

### `evermore-3d`

```text
Size:            4,216,856 bytes
SHA-256:         3a8d5cb6934665bbc498d5aba4b0b2ab1e8fd37c4da47574628605a9e55fa55e
Symbol SHA-256:  e997402bc76e092917cc1bc0c2828a898d1867e7f0e50bc34ef572bcc4a477e4
GNU Build ID:    945129c1ee3cd2187a02b99592330f172be6dc14
```

Observed GLIBC symbol floor: `2.34`

Evidence artifact SHA-256:

```text
52f22ec8efa0addbcea05be5874218753d6a70577dfe0ed1d9daeb1af23de69c
```

## Proof boundary

The recorded result is technical evidence for an exact source state. It does not establish:

- production readiness or production support;
- a completed security review;
- binary signing or supply-chain provenance beyond the recorded package and artifact checks;
- release packaging or installer quality;
- support for RIDs or operating systems other than those explicitly tested;
- authentication, networking, persistence, moderation, anti-cheat, deployment, or live operations;
- physical geography, coordinates, climate, atmosphere, ephemerides, or canon;
- third-party trademark, copyright, or content authorization.

## Public snapshot revalidation

A public release or binary derived from this snapshot should record a new exact commit, workflow run, executable hash, symbol hash, Build ID, dependency report, and direct-execution result. Historical hashes must not be presented as proof for changed source, changed build inputs, or a different RID.
