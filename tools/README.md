# Certification Tools

These deterministic tools are copied from the installed `.NET Native` skills. They validate configuration and source, resolve and stage exact AOT packs, publish a named RID, inspect the native artifact, run it on a compatible host, and write structured evidence.

`seal_source_archive.py` verifies the clean-source manifest and creates the deterministic inner ZIP. `compare_native_certifications.py` requires two independently successful certification reports, then compares the target contract, native executable SHA-256/GNU Build ID, and symbol-companion hashes.

`verify_native_aot_evidence.py` independently admits or rejects the returned evidence tree. It verifies copied executable and symbol bytes, both package/signature manifests, required managed/native/toolchain logs, sanitized native smoke, the reproducibility report, and the exact sealed source archive. Its generated Markdown remains a technical validation record with release authority locked to the owner.

Run its self-contained regression matrix with:

```bash
python3 tools/tests/test_verify_native_aot_evidence.py
```

Running a validator alone does not prove native publication or product correctness.
