# Phase XVII Integration Specification

## Lineage Divergence and Speciation Governance

Status: implementation-oriented prototype layer. Simulation discovery remains Test History until reviewed.

Phase XVII extends the Phase XVI whole-community eco-evolution runtime with lineage divergence analysis, persistent ecotypes, reproductive-isolation tracking, hybrid-zone assessment, candidate speciation detection, explicit review governance, and deterministic evolutionary-tree reconstruction.

This is a fictional Azeroth simulation model. It is not a real-world genetics, reproductive-health, conservation, or epidemiological model.

## 1. Authority rule

The simulator may discover and preserve evidence of divergence. It may not declare a new authoritative species by itself.

The authority ladder is:

`Population branch -> Ecotype -> Persistent ecotype -> Divergent lineage -> Isolation-bearing lineage -> Speciation candidate -> Review -> Experimental lineage or Canon-authoring authorization`

`AuthorizeCanonAuthoring` is deliberately not equivalent to inserting a species into `SpeciesAuthoringCatalog`. A human/governance authoring step must still create the final species profile, genome family, lifecycle, ecology, compatibility contracts, and continuity status.

## 2. Integration position

Phase XVII executes after committed Phase XVI population state exists for a generation.

Authoritative order:

1. Phase XVI environmental pulse
2. trophic feedback
3. cohort evolution
4. migration and recolonization
5. disease transmission and host-pathogen feedback
6. community resilience assessment
7. committed population state
8. Phase XVII ecotype observation
9. pairwise genetic divergence
10. reproductive isolation
11. hybrid-zone assessment
12. candidate detection
13. review-gated disposition
14. evolutionary-tree/history commit

This prevents lineage decisions from reading population state that has not yet been causally committed.

## 3. Ecotypes

`EcotypeTracker` records local adaptation separately from species identity.

An ecotype contains:

- species ID
- zone ID
- lineage ID
- adaptive trait weights
- bounded local-adaptation score
- first and last observed generation
- consecutive observations
- status: Observed, Persistent, Divergent

Persistence is configurable through `EcotypePersistenceGenerations`. A divergent ecotype does not revert to Persistent or Observed on later observations.

## 4. Divergence

`DivergenceAnalyzer` combines four independently bounded signals:

- allelic divergence
- trait divergence
- ecological separation
- geographic isolation

Weights are configurable through `LineageDivergenceSettings`. The allelic term is supplied from the existing `GeneticDivergenceAnalyzer`, preserving the Phase XV population-genetics implementation instead of creating a competing distance formula.

No one metric independently creates a candidate.

## 5. Reproductive isolation

`ReproductiveIsolationEvaluator` tracks:

- prezygotic isolation
- postzygotic isolation
- behavioral isolation
- temporal isolation
- geographic isolation

The composite score is explicitly separate from general genetic divergence. High divergence with low reproductive isolation is therefore insufficient for a candidate.

## 6. Hybrid zones

`HybridZoneEvaluator` produces one of four dispositions:

- Absent
- Stable
- Introgressing
- ReinforcingIsolation

A hybrid zone can remain stable or increase isolation pressure. It is not forced into either immediate merger or immediate speciation.

## 7. Candidate detection

`SpeciationCandidateRegistry` requires all configured gates simultaneously:

- composite divergence at or above threshold
- composite isolation at or above threshold
- ecological distinctness at or above threshold
- repeated qualifying generations before ReviewReady

Candidate status progression:

`Observed -> Persistent -> ReviewReady`

Governance may later move it to:

`Rejected | SimulationAuthorized | CanonAuthoringAuthorized`

Rejected candidates remain rejected when later observations arrive under the same candidate identity. Authorized candidates likewise do not fall backward to Observed after another generation.

## 8. Governance boundary

`SpeciationReviewGate` accepts explicit review records containing:

- stable review ID
- candidate ID
- reviewer identity
- decision
- notes
- review generation
- optional provenance ID

Decisions:

- Hold
- Reject
- ApproveSimulation
- AuthorizeCanonAuthoring

`ApproveSimulation` authorizes experimental simulation branches only.

`AuthorizeCanonAuthoring` authorizes creation of a formal species authoring record, but Phase XVII does not mutate `SpeciesAuthoringCatalog` automatically.

## 9. Evolutionary tree

`EvolutionaryTree` maintains deterministic lineage history with:

- active species roots
- candidate branches
- experimental lineages
- canon-authoring-authorized branches
- rejected branches
- extinct branches

Snapshots are ordinally sorted and SHA-256 hashed. The hash verifies deterministic representation, not biological truth or canon approval.

## 10. Save-state boundary

`PhaseXVIISaveState` contains:

- ecotypes
- speciation candidates
- review records
- lineage-divergence history
- evolutionary-tree snapshot

The review gate and tree are separated from the mutable Phase XVI community cohort state. This prevents population arithmetic from silently overwriting governance history.

## 11. Named-actor protection

Phase XVII consumes `EvolutionCohort` population state only. It never edits or removes named persistent actors.

Named actor identity, genome, memory, relationships, and continuity remain under their pre-existing authority systems.

## 12. Determinism rules

For the same committed cohort state and authored Phase XVII inputs:

- pairwise genetic divergence is deterministic
- ecotype persistence is deterministic
- candidate IDs are stable
- candidate-status progression is deterministic
- evolutionary-tree snapshots hash identically

Review decisions are explicit external inputs. The simulator never generates a review decision from randomness.

## 13. Default activation boundary

Phase XVII adds no new Warcraft-derived species profiles.

Only species already active through the existing species-authoring and ecology gates may produce authoritative cohort states that Phase XVII can inspect. Deferred templates remain inert.

## 14. Acceptance tests

The package includes tests verifying:

1. ecotypes require persistence
2. low divergence does not produce candidates
3. high divergence with low isolation does not produce candidates
4. viable and fertile hybrids may remain stable
5. low hybrid viability may reinforce isolation
6. candidates remain held without review
7. simulation approval does not authorize canon authoring
8. evolutionary-tree snapshots are deterministic

## 15. Explicit non-goals

Phase XVII does not:

- invent genome families for unreviewed Azeroth species
- infer reproductive compatibility between unrelated species
- auto-promote candidate lineages into authoritative species
- rewrite named actors through population equations
- claim real biological validity
- infer morality, intelligence, worth, culture, or identity from genetics

## 16. Next boundary

Phase XVIII may add deep-time macroevolution only after Phase XVII compiler/test verification is complete. Recommended topics include clade turnover, adaptive radiations, deep-time extinction/recovery, ecological replacement, archival strata, and branch-safe phylogeny snapshots.
