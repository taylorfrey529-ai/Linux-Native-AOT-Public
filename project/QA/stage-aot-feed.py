#!/usr/bin/env python3
import argparse
import hashlib
import json
import shutil
import sys
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path


def fail(msg: str, code: int = 2):
    print(f"ERROR: {msg}", file=sys.stderr)
    raise SystemExit(code)


def sha256(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def package_identity(path: Path):
    try:
        with zipfile.ZipFile(path) as zf:
            nuspecs = [n for n in zf.namelist() if n.lower().endswith(".nuspec") and "/" not in n.strip("/")]
            if not nuspecs:
                nuspecs = [n for n in zf.namelist() if n.lower().endswith(".nuspec")]
            if len(nuspecs) != 1:
                fail(f"{path.name}: expected one .nuspec, found {len(nuspecs)}")
            root = ET.fromstring(zf.read(nuspecs[0]))
    except (zipfile.BadZipFile, ET.ParseError) as exc:
        fail(f"{path.name}: invalid nupkg/nuspec: {exc}")
    metadata = next((e for e in root.iter() if e.tag.split("}")[-1] == "metadata"), None)
    if metadata is None:
        fail(f"{path.name}: nuspec metadata missing")
    def child_text(name):
        for e in metadata:
            if e.tag.split("}")[-1] == name:
                return (e.text or "").strip()
        return ""
    pid = child_text("id")
    version = child_text("version")
    if not pid or not version:
        fail(f"{path.name}: nuspec id/version missing")
    return pid, version


def main():
    ap = argparse.ArgumentParser(description="Validate and stage exact Native AOT nupkgs into a local feed.")
    ap.add_argument("--requirements", required=True)
    ap.add_argument("--packages", required=True)
    ap.add_argument("--feed", required=True)
    ap.add_argument("--hash-manifest", help="Optional JSON object mapping filename or package id to expected SHA-256")
    args = ap.parse_args()

    req = json.loads(Path(args.requirements).read_text(encoding="utf-8"))
    required = {(p["id"].lower(), p["version"]): p for p in req.get("packages", [])}
    if not required:
        fail("requirements contains no packages")

    expected_hashes = {}
    if args.hash_manifest:
        expected_hashes = json.loads(Path(args.hash_manifest).read_text(encoding="utf-8"))

    candidates = {}
    for path in sorted(Path(args.packages).glob("*.nupkg")):
        pid, version = package_identity(path)
        key = (pid.lower(), version)
        digest = sha256(path)
        if key in candidates and candidates[key][1] != digest:
            fail(f"conflicting packages for {pid} {version}")
        candidates[key] = (path, digest, pid)

    missing = [p for key, p in required.items() if key not in candidates]
    if missing:
        for p in missing:
            print(f"MISSING {p['id']} {p['version']}", file=sys.stderr)
        raise SystemExit(3)

    feed = Path(args.feed).resolve()
    feed.mkdir(parents=True, exist_ok=True)
    staged = []
    for key, spec in sorted(required.items()):
        src, digest, actual_id = candidates[key]
        expected = expected_hashes.get(src.name) or expected_hashes.get(spec["id"])
        if expected and digest.lower() != str(expected).lower():
            fail(f"SHA-256 mismatch for {src.name}: expected {expected}, got {digest}", 4)
        dst = feed / src.name
        shutil.copy2(src, dst)
        staged.append({
            "id": actual_id,
            "version": spec["version"],
            "file": dst.name,
            "sha256": digest,
            "reason": spec.get("reason", ""),
        })

    manifest = {
        "schema": "dot-net-native-aot/feed-manifest-v1",
        "requirements": str(Path(args.requirements).resolve()),
        "feed": str(feed),
        "packages": staged,
    }
    (feed / "feed-manifest.json").write_text(json.dumps(manifest, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(json.dumps(manifest, indent=2, sort_keys=True))


if __name__ == "__main__":
    main()
