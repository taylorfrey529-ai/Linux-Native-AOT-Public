#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DESTINATION="${1:?Pass the destination directory for downloaded nupkg files}"
REQUIREMENTS="${2:-$ROOT/project/QA/aot-requirements.linux-x64.json}"

command -v python3 >/dev/null 2>&1 || { printf 'python3 is required\n' >&2; exit 69; }
command -v curl >/dev/null 2>&1 || { printf 'curl is required\n' >&2; exit 69; }
[[ -r "$REQUIREMENTS" ]] || { printf 'requirements file not found: %s\n' "$REQUIREMENTS" >&2; exit 66; }

mkdir -p "$DESTINATION"

python3 - "$REQUIREMENTS" <<'PY' | while IFS=$'\t' read -r package_id package_version; do
import json
import sys

requirements = json.load(open(sys.argv[1], encoding="utf-8"))
for package in requirements["packages"]:
    print(f'{package["id"]}\t{package["version"]}')
PY
  normalized_id="${package_id,,}"
  filename="$normalized_id.$package_version.nupkg"
  url="https://api.nuget.org/v3-flatcontainer/$normalized_id/$package_version/$filename"
  curl --fail --silent --show-error --location \
    --proto '=https' --tlsv1.2 --retry 4 --retry-all-errors \
    --output "$DESTINATION/$filename" "$url"
  printf 'downloaded %s %s\n' "$package_id" "$package_version"
done
