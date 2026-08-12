#!/usr/bin/env python3
"""Validate, optionally signature-verify, and stage exact Native AOT nupkgs into a local feed."""
from __future__ import annotations

import argparse
import hashlib
import json
import os
import shutil
import subprocess
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


def verify_signature(dotnet: Path, package: Path, offline_revocation: bool) -> tuple[int, str]:
    env = os.environ.copy()
    if offline_revocation:
        env["NUGET_CERT_REVOCATION_MODE"] = "offline"
    proc = subprocess.run(
        [str(dotnet), "nuget", "verify", str(package), "--all", "--verbosity", "minimal"],
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        env=env,
    )
    return proc.returncode, proc.stdout


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--requirements", required=True)
    ap.add_argument("--packages", required=True)
    ap.add_argument("--feed", required=True)
    ap.add_argument("--hash-manifest", help="Optional JSON object mapping filename or package id to expected SHA-256")
    ap.add_argument(
        "--require-hashes",
        action="store_true",
        help="Require every resolved package to have an expected SHA-256 entry.",
    )
    ap.add_argument("--verify-signatures", action="store_true", help="Run 'dotnet nuget verify --all' on every staged package")
    ap.add_argument("--dotnet", help="Path to dotnet host used for signature verification")
    ap.add_argument(
        "--online-revocation",
        action="store_true",
        help="Allow online certificate revocation checks. Default signature verification uses NUGET_CERT_REVOCATION_MODE=offline.",
    )
    args = ap.parse_args()

    req = json.loads(Path(args.requirements).read_text(encoding="utf-8"))
    required = {(p["id"].lower(), p["version"]): p for p in req.get("packages", [])}
    if not required:
        fail("requirements contains no packages")

    expected_hashes = {}
    if args.hash_manifest:
        expected_hashes = json.loads(Path(args.hash_manifest).read_text(encoding="utf-8"))
    elif args.require_hashes:
        fail("--require-hashes requires --hash-manifest")

    dotnet = None
    if args.verify_signatures:
        if not args.dotnet:
            fail("--verify-signatures requires --dotnet")
        dotnet = Path(args.dotnet).resolve()
        if not dotnet.is_file():
            fail(f"dotnet host not found: {dotnet}")

    candidates = {}
    package_dir = Path(args.packages)
    for path in sorted(package_dir.glob("*.nupkg")):
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
        if args.require_hashes and not expected:
            fail(f"no expected SHA-256 is recorded for {spec['id']} {spec['version']}", 4)
        if expected and digest.lower() != str(expected).lower():
            fail(f"SHA-256 mismatch for {src.name}: expected {expected}, got {digest}", 4)

        signature = {"status": "not-run"}
        if dotnet is not None:
            rc, output = verify_signature(dotnet, src, offline_revocation=not args.online_revocation)
            if rc != 0:
                fail(f"NuGet signature verification failed for {src.name}:\n{output}", 5)
            signature = {
                "status": "pass",
                "revocation_mode": "online" if args.online_revocation else "offline",
            }

        dst = feed / src.name
        shutil.copy2(src, dst)
        staged.append({
            "id": actual_id,
            "version": spec["version"],
            "file": dst.name,
            "sha256": digest,
            "signature_verification": signature,
            "reason": spec.get("reason", ""),
        })

    manifest = {
        "schema": "dot-net-native-aot/feed-manifest-v2",
        "requirements": str(Path(args.requirements).resolve()),
        "feed": str(feed),
        "packages": staged,
    }
    (feed / "feed-manifest.json").write_text(json.dumps(manifest, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(json.dumps(manifest, indent=2, sort_keys=True))


if __name__ == "__main__":
    main()
