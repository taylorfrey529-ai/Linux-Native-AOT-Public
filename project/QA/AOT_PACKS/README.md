# Native AOT local package intake

Place the five exact .NET 10.0.10 Native AOT `.nupkg` files listed in `../../NATIVE_AOT_PACKAGE_REQUIREMENTS.md` here, then run:

```bash
python QA/stage-aot-feed.py \
  --requirements QA/aot-requirements.linux-x64.json \
  --packages QA/AOT_PACKS \
  --feed .aot-feed
```

Validate package identity, version, ZIP integrity, SHA-256, and NuGet signatures before certification. Do not fabricate ILCompiler or ILLink packages from SDK files; these packages are executable supply-chain inputs.
