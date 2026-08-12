#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT_ROOT="$ROOT/project"
DOTNET_ROOT="${DOTNET_ROOT:?Set DOTNET_ROOT to the extracted .NET SDK 10.0.302 directory}"
AOT_PACKAGES="${AOT_PACKAGES:?Set AOT_PACKAGES to the directory containing the five 10.0.10 nupkg files}"
DOTNET="$DOTNET_ROOT/dotnet"
RID="linux-x64"
FEED="$ROOT/cache/aot-feed"
EVIDENCE="$ROOT/update-bindings/validation/native-aot-$RID"
RESOLVED_REQUIREMENTS="$EVIDENCE/01-resolved-requirements.json"
HASH_MANIFEST="$PROJECT_ROOT/QA/aot-pack-hashes.$RID.json"

mkdir -p "$FEED" "$EVIDENCE" "$ROOT/logs"

"$ROOT/scripts/preflight-linux-x64.sh" 2>&1 \
  | tee "$ROOT/logs/00-host-preflight.txt"

# SDK resolution must begin below project/global.json even when the host has
# newer SDK feature bands preinstalled.
cd "$PROJECT_ROOT"

python3 "$ROOT/tools/validate_dotnet_native_workspace.py" "$PROJECT_ROOT" \
  --project QA/NativeAotSmoke/NativeAotSmoke.csproj --target-framework net10.0 \
  | tee "$ROOT/logs/01-workspace-validation.txt"

python3 "$ROOT/tools/validate_native_aot_project.py" "$PROJECT_ROOT" \
  --project QA/NativeAotSmoke/NativeAotSmoke.csproj --target-framework net10.0 --kind app --strict --json \
  | tee "$EVIDENCE/02-project-validation.json"

python3 "$ROOT/tools/aot_source_audit.py" "$PROJECT_ROOT" --output "$EVIDENCE/03-source-audit.json"

python3 "$ROOT/tools/resolve_aot_requirements.py" \
  --dotnet-root "$DOTNET_ROOT" \
  --project "$PROJECT_ROOT/QA/NativeAotSmoke/NativeAotSmoke.csproj" \
  --tfm net10.0 --rid "$RID" --output "$RESOLVED_REQUIREMENTS"

python3 "$ROOT/tools/stage_aot_feed.py" \
  --requirements "$RESOLVED_REQUIREMENTS" --packages "$AOT_PACKAGES" --feed "$FEED" \
  --hash-manifest "$HASH_MANIFEST" --require-hashes \
  --verify-signatures --dotnet "$DOTNET" \
  | tee "$EVIDENCE/04-feed-staging.json"

"$DOTNET" restore "$PROJECT_ROOT/QA/OfflineHarness/QAHarness.csproj" \
  -p:IsAotCompatible=false -p:EnableTrimAnalyzer=false -p:EnableAotAnalyzer=false \
  | tee "$ROOT/logs/05-managed-restore.txt"
"$DOTNET" build "$PROJECT_ROOT/QA/OfflineHarness/QAHarness.csproj" -c Release --no-restore \
  -p:IsAotCompatible=false -p:EnableTrimAnalyzer=false -p:EnableAotAnalyzer=false \
  | tee "$ROOT/logs/06-managed-build.txt"
"$DOTNET" run --project "$PROJECT_ROOT/QA/OfflineHarness/QAHarness.csproj" -c Release --no-build \
  -p:IsAotCompatible=false -p:EnableTrimAnalyzer=false -p:EnableAotAnalyzer=false \
  | tee "$ROOT/logs/07-managed-tests.txt"

python3 "$ROOT/tools/certify_native_aot.py" \
  --dotnet-root "$DOTNET_ROOT" \
  --project "$PROJECT_ROOT/QA/NativeAotSmoke/NativeAotSmoke.csproj" \
  --rid "$RID" --feed "$FEED" --evidence-dir "$EVIDENCE" \
  --configuration-validation-pass --managed-validation-pass
