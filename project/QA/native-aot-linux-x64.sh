#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DOTNET_ROOT="${DOTNET_ROOT:?Set DOTNET_ROOT to a .NET SDK 10.0.302 installation}"
DOTNET="${DOTNET:-$DOTNET_ROOT/dotnet}"
RID="${RID:-linux-x64}"
CONFIGURATION="${CONFIGURATION:-Release}"
SMOKE="$ROOT/QA/NativeAotSmoke/NativeAotSmoke.csproj"
HARNESS="$ROOT/QA/OfflineHarness/QAHarness.csproj"
REQ="$ROOT/QA/aot-requirements.$RID.json"
EVIDENCE="$ROOT/artifacts/native-aot/$RID/evidence"
FEED="${NUGET_SOURCE:-$ROOT/.aot-feed}"

mkdir -p "$EVIDENCE"

python "$ROOT/QA/resolve-aot-requirements.py" \
  --dotnet-root "$DOTNET_ROOT" \
  --project "$SMOKE" \
  --tfm net10.0 \
  --rid "$RID" \
  --output "$REQ"

if [[ ! -d "$FEED" ]]; then
  echo "Native AOT local feed not found: $FEED" >&2
  echo "Stage the exact packages listed in $REQ before publishing." >&2
  exit 3
fi

# Managed regression remains a distinct proof layer. The QA harness is dependency-light;
# AOT analyzers are validated separately and Native AOT packs are consumed by the native path.
"$DOTNET" restore "$HARNESS" \
  -p:IsAotCompatible=false \
  -p:EnableTrimAnalyzer=false \
  -p:EnableAotAnalyzer=false
"$DOTNET" build "$HARNESS" -c "$CONFIGURATION" --no-restore \
  -p:IsAotCompatible=false \
  -p:EnableTrimAnalyzer=false \
  -p:EnableAotAnalyzer=false
"$DOTNET" run --project "$HARNESS" -c "$CONFIGURATION" --no-build \
  -p:IsAotCompatible=false \
  -p:EnableTrimAnalyzer=false \
  -p:EnableAotAnalyzer=false

python "$ROOT/QA/certify-native-aot.py" \
  --dotnet-root "$DOTNET_ROOT" \
  --project "$SMOKE" \
  --rid "$RID" \
  --configuration "$CONFIGURATION" \
  --feed "$FEED" \
  --evidence-dir "$EVIDENCE"
