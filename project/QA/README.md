# Offline compiler QA harness

This harness compiles the authored test source against a minimal local xUnit-compatible shim so deterministic QA can run without NuGet connectivity.

It is not a replacement for the official xUnit runner. When package restore is available, the canonical test project remains `EasternKingdoms.Simulation.Tests`.

Run from the repository root:

```bash
dotnet build QA/OfflineHarness/QAHarness.csproj -c Release
dotnet run --project QA/OfflineHarness/QAHarness.csproj -c Release --no-build
```
