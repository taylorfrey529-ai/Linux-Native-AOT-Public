#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DOTNET_ROOT="${DOTNET_ROOT:?Set DOTNET_ROOT to the extracted .NET SDK 10.0.302 directory}"
AOT_PACKAGES="${AOT_PACKAGES:?Set AOT_PACKAGES to the directory containing the five 10.0.10 nupkg files}"
DOTNET="$DOTNET_ROOT/dotnet"
REQUIREMENTS="$ROOT/project/QA/aot-requirements.linux-x64.json"

fail() {
  printf 'preflight: FAIL: %s\n' "$*" >&2
  exit 78
}

[[ "$(uname -s)" == "Linux" ]] || fail "linux-x64 certification requires a Linux build host"
[[ "$(uname -m)" == "x86_64" ]] || fail "linux-x64 certification requires an x86-64 build host"

[[ -r /proc/meminfo ]] || fail "/proc/meminfo is unavailable; CoreCLR cannot be certified in this host"
[[ -r /proc/self/status ]] || fail "/proc/self/status is unavailable; CoreCLR process inspection is incomplete"

[[ -x "$DOTNET" ]] || fail "dotnet host is not executable: $DOTNET"
[[ -r "$DOTNET_ROOT/sdk/10.0.302/Microsoft.NETCoreSdk.BundledVersions.props" ]] \
  || fail "SDK 10.0.302 pack catalog is missing"
[[ -r "$REQUIREMENTS" ]] || fail "Native AOT requirements file is missing: $REQUIREMENTS"
[[ -d "$AOT_PACKAGES" ]] || fail "Native AOT package intake directory is missing: $AOT_PACKAGES"

for command in python3 ld ar strip objcopy readelf file sha256sum unzip; do
  command -v "$command" >/dev/null 2>&1 || fail "required command is unavailable: $command"
done

if command -v clang >/dev/null 2>&1; then
  compiler="clang"
elif command -v gcc >/dev/null 2>&1; then
  compiler="gcc"
else
  fail "neither clang nor gcc is available"
fi

printf '#include <zlib.h>\n' | "$compiler" -E -x c - >/dev/null 2>&1 \
  || fail "zlib development headers are unavailable"

package_count="$(find "$AOT_PACKAGES" -maxdepth 1 -type f -name '*.nupkg' | wc -l)"
[[ "$package_count" -ge 5 ]] || fail "expected at least five Native AOT nupkg files; found $package_count"

sdk_version="$(cd "$ROOT/project" && "$DOTNET" --version)" || fail "dotnet --version could not start CoreCLR"
[[ "$sdk_version" == "10.0.302" ]] || fail "expected SDK 10.0.302; resolved $sdk_version"

printf 'preflight: PASS\n'
printf 'host: Linux x86_64\n'
printf 'sdk: %s\n' "$sdk_version"
printf 'native compiler: %s\n' "$compiler"
printf 'package intake: %s nupkg files\n' "$package_count"
