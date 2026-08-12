# Native AOT CI Stage Contract

The CI/native validation path must keep managed and native proof separate.

1. Install/pin .NET SDK 10.0.302 and Linux Native AOT prerequisites (`clang` or `gcc`, GNU binutils, zlib development headers).
2. Restore and build the managed offline QA harness; execute all authored facts.
3. Restore `QA/NativeAotSmoke/NativeAotSmoke.csproj` for validation RID `linux-x64`.
4. Publish with `dotnet publish -c Release -r linux-x64 --no-restore`.
5. Inspect the resulting binary with `file` and `readelf -h`.
6. Execute the published binary directly, without `dotnet`.
7. Preserve the binary and any `.dbg` sidecar as matched evidence.
8. Generate SHA-256 checksums and record binary size.
9. Promote nothing to release status without an explicit supported-RID and release-approval decision.

`QA/native-aot-linux-x64.sh` implements these stages locally. CI systems may wrap that script but should not collapse managed build success into native-publish success.

The workspace-level `.github/workflows/native-aot-linux-x64.yml` is the authoritative manually triggered off-host route. It pins its GitHub Actions to immutable commits, installs SDK 10.0.302, downloads the exact five 10.0.10 packs from NuGet, and verifies their sealed SHA-256 values and NuGet signatures.

Revision 4 then verifies the exact sealed source ZIP, extracts it into two distinct absolute roots, runs `scripts/certify-linux-x64.sh` inside each extraction, and requires matching executable, GNU Build ID, and symbol-companion hashes through `scripts/certify-reproducible-linux-x64.sh`. Workflow presence is not certification evidence; only a completed run and its returned artifact can advance the proof state.

Revision 5 follows that comparison with `scripts/verify-returned-evidence-linux-x64.sh`. This separate gate re-hashes the copied publish outputs, verifies the copied sealed source internally, requires exact package/signature records and sanitized smoke/toolchain evidence, and emits technical evidence only when every check passes. It cannot approve a production RID or release.

The workflow runs `tools/tests/test_verify_native_aot_evidence.py` before package download so admission regressions fail before the expensive Native AOT stages.
