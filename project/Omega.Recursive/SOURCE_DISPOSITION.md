# Omega PDF source disposition

## Source identity

- Supplied file: `Omega Restoration Process - Omega Engine C# Code(1).pdf`
- Supplied-byte SHA-256: `eb92964192817813ef62f43db607468c0224980dfe448269c2e43a30c8082345`
- PDF metadata title: `Omega Restoration Process - Omega Engine C# Code`
- Pages: 19 total; pages 1–18 contain one continuous C# listing and page 19 is blank
- Extracted symbols: `OmegaEngine.Program`, `Trait`, `EntityProfile`, `SelectionEngine`
- Repository treatment: owner-supplied source reconstructed into original repository namespaces; the PDF, extracted text, page renders, and conversation transcript are not committed

The source's ownership and licensing were not independently verified. This import uses the repository's MIT license under the owner's instruction and records the supplied-byte identity for traceability.

## Concept disposition

| Source concept | Disposition | Repository mapping |
| --- | --- | --- |
| Infinite console loop | Refactored | `OmegaEngine.Run` requires 1–10,000 generations and supports cancellation. |
| Unseeded `System.Random` instances | Rewritten | `OmegaRandom` provides one explicit deterministic stream. |
| `Trait` and `EntityProfile` | Refactored | Immutable `OmegaTrait` and `OmegaEntityProfile` records. |
| Fitness as mean trait strength | Preserved | `OmegaEntityProfile.FitnessScore`. |
| Initial population 24, four traits each | Preserved | `OmegaSimulationOptions` defaults and `CreateRandomEntity`. |
| Eight names and eight trait names | Preserved | Fixed `Names` and `TraitNames` vocabularies. |
| Select top eight | Preserved | `SelectionEngine.SelectTop`; deterministic name tie-break added. |
| Combine by concatenating parent traits and taking four | Preserved | `SelectionEngine.Combine`; the source's first-parent-first behavior is retained and documented by tests. |
| Recursive parent-name concatenation | Rewritten | Each parent segment is capped at 31 characters to bound memory growth. |
| Per-trait 25% mutation and ±0.15 clamp | Preserved | `SelectionEngine.Mutate`; rate remains configurable with source default `0.25`. |
| Trait totals and dominant-trait analysis | Preserved | `OmegaGenerationSnapshot.TraitTotals` and `DominantTrait`. |
| Intensity history capped at 64 | Preserved | `OmegaRunResult.IntensityHistory`. |
| Surge threshold `delta > 4`, log capped at 100 | Preserved | `DetectSurgingTraits` and `ResonanceLog`. |
| Rune glyph table | Preserved | `OmegaGlyphs.EncodeTraitName`. |
| Presence meter, population bars, trend/histogram, top influence | Adapter | Renderer-specific cursor/color output becomes structured `OmegaGenerationSnapshot` data and CLI text/JSON output. |
| Alternating console background | Omitted | Presentation-only behavior has no meaning in noninteractive/native automation. |
| `Thread.Sleep(180)` and four-second containment delay | Omitted | Timing-only behavior would make deterministic automation slower without changing results. |
| Snapshot every five generations | Refactored | Every generation is retained in memory and checkpoint generations are marked with `IsArchiveCheckpoint`; no implicit filesystem writes. |
| Resonance log file | Refactored | Returned in `OmegaRunResult`; no implicit filesystem writes. |
| Weighted trait-convergence containment | Preserved | `OmegaContainmentPolicy`, threshold `> 18`. |
| Diversity-collapse containment | Preserved | `OmegaContainmentPolicy`, unique traits `<= 2` after generation 10. |
| Runaway-fitness containment | Preserved | `OmegaContainmentPolicy`, average fitness `> 0.95` after generation 25. |
| Forbidden semantic containment | Preserved | Six source phrases, ordinal case-insensitive match. |
| Mutation-acceleration containment | Preserved | Previous intensity `> 0.8` and increase `> 0.25`; retained even though bounded strengths make it unreachable. |
| Containment process exit `7` | Adapter | `evermore omega run` returns exit code `7` and an explicit reason. |
| Unity label in source prose | Adapter | No Unity API appears in the listing; the implementation remains a base .NET/AOT-compatible library. |
