# Omega Recursive

Omega Recursive is an isolated, deterministic reconstruction of the owner-supplied **Omega Restoration Process - Omega Engine C# Code** PDF.

> Omega is the great dark beyond our world.

The library preserves the source population model, eight trait names, fitness ranking, selection/combine/mutate cycle, glyph alphabet, intensity history, surge log, five-generation snapshot cadence, and five containment checks. It has no Unity dependency because the supplied listing uses only base .NET APIs.

The import is intentionally bounded for Native AOT and automation:

- runs require an explicit seed and finite generation count;
- random behavior uses a deterministic, repository-owned generator;
- the 220×60 terminal renderer becomes immutable generation observations;
- no snapshot or log files are written implicitly;
- entity names are capped during combination to prevent recursive memory growth;
- cancellation is checked once per generation;
- no reflection, runtime code generation, dynamic assembly loading, or new package dependency is used.

`Omega.Recursive` is an `IsAotCompatible` library consumed by the separate `Evermore.Cli` candidate. It is isolated/non-canon and cannot read or mutate Eastern Kingdoms simulation state. It does not inherit the immutable Revision 5 certification or any earlier `Evermore.Cli` evidence.

See `SOURCE_DISPOSITION.md` for the page-level extraction boundary and concept mapping.
