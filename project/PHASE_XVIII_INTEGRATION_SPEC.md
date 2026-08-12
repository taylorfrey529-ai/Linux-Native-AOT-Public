# Phase XVIII Integration Specification
## Deep-Time Macroevolution and Historical Strata

### Status
Implementation-oriented, engine-agnostic C# continuation of Phase XVII. Simulation results remain Test History unless separately reviewed and promoted through the existing continuity process.

## Governing rule
Deep-time simulation may reconstruct clades, adaptive radiations, extinctions, ecological replacements, and phylogenetic history. It may not convert those analytical outcomes into authoritative continuity by itself.

## Inputs
Phase XVIII consumes stable Phase XVII evolutionary-tree nodes plus explicit population/niche observations. `DeepTimeObservationAdapter` converts reviewed tree nodes into deep-time lineage observations without inventing population data for missing nodes.

Required lineage observation fields:
- stable evolutionary node ID
- parent evolutionary node ID where applicable
- species-family root
- generation
- population
- occupied zones
- bounded niche vector
- inherited authority status
- optional mean divergence

## Authority mapping
- `ActiveSpecies` -> `ExistingAuthorizedSpecies`
- `Candidate` -> `CandidateTestHistory`
- `ExperimentalLineage` -> `ExperimentalSimulation`
- `CanonAuthoringAuthorized` -> `CanonAuthoringAuthorized`
- `Rejected` -> `Rejected`

Rejected authority is sticky. A later observation cannot silently restore it.

## Deep-time order
1. ingest lineage observations
2. enforce monotonically increasing generations per lineage
3. update lineage population and niche state
4. detect sustained extinction
5. detect adaptive radiation from recent sibling branches
6. detect ecological replacement after extinction
7. reconstruct clades
8. commit generation result
9. periodically seal an append-only archive stratum
10. hash-chain each stratum to the previous stratum

## Extinction
Extinction requires a configurable number of consecutive zero-population observations. A single bad generation is insufficient. Once a lineage is extinct, it cannot be revived in place. Recolonization must be represented as a new lineage or an explicit restoration workflow.

## Adaptive radiation
Radiation requires:
- a common root lineage
- at least the configured number of descendant branches
- branch origin inside the configured radiation window
- mean pairwise niche divergence above threshold
- rejection-filtered descendants

If any contributing branch is only Test History or Experimental Simulation, the radiation event is marked review-only.

## Ecological replacement
Replacement is detected only after an extinction and requires an active lineage with sufficient niche similarity and at least one shared niche dimension. Replacement is evidence of ecological succession, not proof of direct ancestry.

## Clades
Clades are reconstructed from parent lineage links. Each record contains root, members, first/last generation, living/extinct branch counts, and the highest authority represented within the clade.

## Archive strata
`DeepTimeArchive` is append-only. Strata:
- never overlap
- carry start/end generation bounds
- include end-state lineage summaries and events occurring in the interval
- reference the prior stratum hash
- compute a deterministic SHA-256 integrity hash

Later simulation runs cannot alter sealed strata through the public API.

## Governance
`DeepTimeGovernanceGate` separates:
- whether a lineage may participate in experimental deep-time simulation
- whether it has prior authorization for canon authoring

Even `CanonAuthoringAuthorized` deep-time outcomes remain Test History until separately promoted. Phase XVIII does not write into `SpeciesAuthoringCatalog`.

## Named actors
Named persistent actors remain outside deep-time cohort mortality, extinction, radiation, and replacement equations. These systems describe population lineages and community history, not automatic deletion or rewriting of individual continuity.

## Acceptance checks
- rejected lineage authority cannot revive through observation
- extinction requires sustained zero population
- radiation requires configured branch count and niche separation
- ecological replacement requires prior extinction
- unreviewed candidates cannot enter experimental deep time
- experimental lineages cannot be used directly for canon authoring
- phylogeny output is deterministic for equivalent state
- archive strata are non-overlapping and hash chained
- identical inputs produce identical archive head hashes
- no new Azeroth species is activated automatically
