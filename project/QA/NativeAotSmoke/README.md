# NativeAotSmoke

`NativeAotSmoke` is a compile-time-known Native AOT validation consumer for the Eastern Kingdoms simulation libraries.

It exercises:

- deep-time historical reconstruction;
- deterministic replay serialization and verification;
- renderer-neutral atlas scene serialization and verification;
- camera serialization and verification;
- source-generated `System.Text.Json` with reflection fallback disabled.

The executable deliberately avoids runtime test discovery and reflection. The separate `QA/OfflineHarness` remains managed-only and is not part of the native publish closure.

Validation publication:

```bash
dotnet restore NativeAotSmoke.csproj -r linux-x64
dotnet publish NativeAotSmoke.csproj -c Release -r linux-x64 --no-restore
```

A successful managed build is not Native AOT proof. Native validation requires successful publish, artifact inspection, and execution of the resulting native binary.
