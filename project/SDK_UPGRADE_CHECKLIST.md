# SDK Upgrade Checklist

When a newer stable .NET SDK is supplied:

1. Verify the archive checksum and `dotnet --info`.
2. Record the new Roslyn `csc /version`.
3. Update `global.json` to the exact certified SDK version.
4. Update `LangVersion` only to the stable language level actually supported and intended for the repository.
5. Keep the runtime/TFM migration separate unless explicitly authorized.
6. Clean-restore and clean-build production with warnings-as-errors and analyzers enabled.
7. Execute the authored test suite twice.
8. Compare deterministic Release assembly hashes across clean rebuilds.
9. Review new analyzer diagnostics individually; do not blanket-disable newly introduced categories.
10. Reseal the manifest and compiler certification report.
