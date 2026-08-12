# Eastern Kingdoms Historical Replay Format 22.0

Schema: `evermore.eastern-kingdoms.history/22.0`

A replay JSON document contains four top-level fields:

- `schemaVersion`
- `authority`
- `payloadSha256`
- `payload`

`payload` contains a deterministic `timeline` and ordered `frames`.

The payload hash is SHA-256 over canonical JSON for the payload only. Canonicalization sorts object property names ordinally, preserves array order, preserves raw numeric representation emitted by the serializer, removes insignificant whitespace, and emits enum values as strings.

Frame order is by `frameIndex`, then generation. Cell order is ordinal by `zoneId`. Living lineage IDs and timeline zone IDs are sorted ordinally.

A consumer must reject a document when the schema is unsupported, authority differs from `TestHistoryOnly`, the canonical payload hash differs, the timeline hash fails, or timeline/frame metadata disagree.

The format deliberately contains no render coordinates. Globe geometry remains an authored presentation concern outside simulation authority.
