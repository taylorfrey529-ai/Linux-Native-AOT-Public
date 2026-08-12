#!/usr/bin/env python3
"""Verify a SHA-256 source manifest and create a deterministic ZIP from it."""
from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import stat
import sys
import zipfile
from pathlib import Path, PurePosixPath


MANIFEST_LINE = re.compile(r"^([0-9a-f]{64})  (.+)$")
FIXED_ZIP_TIME = (1980, 1, 1, 0, 0, 0)


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def safe_relative_path(raw: str) -> Path:
    normalized = raw[2:] if raw.startswith("./") else raw
    posix = PurePosixPath(normalized)
    if not normalized or posix.is_absolute() or ".." in posix.parts or "." in posix.parts:
        raise ValueError(f"unsafe manifest path: {raw!r}")
    if "\\" in normalized:
        raise ValueError(f"manifest paths must use '/': {raw!r}")
    return Path(*posix.parts)


def read_manifest(path: Path) -> list[tuple[str, Path]]:
    entries: list[tuple[str, Path]] = []
    seen: set[Path] = set()
    for line_number, raw_line in enumerate(path.read_text(encoding="utf-8").splitlines(), start=1):
        match = MANIFEST_LINE.fullmatch(raw_line)
        if not match:
            raise ValueError(f"invalid manifest line {line_number}: {raw_line!r}")
        digest, raw_name = match.groups()
        relative = safe_relative_path(raw_name)
        if relative in seen:
            raise ValueError(f"duplicate manifest path: {relative.as_posix()}")
        seen.add(relative)
        entries.append((digest, relative))
    if not entries:
        raise ValueError("source manifest is empty")
    return sorted(entries, key=lambda item: item[1].as_posix())


def zip_info(name: str, mode: int) -> zipfile.ZipInfo:
    info = zipfile.ZipInfo(name, FIXED_ZIP_TIME)
    info.create_system = 3
    info.compress_type = zipfile.ZIP_DEFLATED
    info.external_attr = ((stat.S_IFREG | mode) & 0xFFFF) << 16
    return info


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", required=True, help="Workspace root containing all manifested files")
    parser.add_argument("--manifest", required=True, help="SHA-256 manifest, normally within the workspace root")
    parser.add_argument("--output", required=True, help="Destination ZIP path")
    parser.add_argument("--prefix", required=True, help="Single top-level directory name inside the ZIP")
    args = parser.parse_args()

    root = Path(args.root).resolve()
    manifest = Path(args.manifest).resolve()
    output = Path(args.output).resolve()
    prefix = PurePosixPath(args.prefix)
    if prefix.is_absolute() or len(prefix.parts) != 1 or prefix.name in {"", ".", ".."}:
        print("ERROR: --prefix must be one safe directory name", file=sys.stderr)
        return 2
    if not root.is_dir() or not manifest.is_file():
        print("ERROR: --root must be a directory and --manifest must be a file", file=sys.stderr)
        return 2
    try:
        manifest.relative_to(root)
    except ValueError:
        print("ERROR: manifest must be inside the workspace root", file=sys.stderr)
        return 2

    try:
        entries = read_manifest(manifest)
    except ValueError as error:
        print(f"ERROR: {error}", file=sys.stderr)
        return 3

    verified: list[tuple[Path, bytes, int, str]] = []
    for expected, relative in entries:
        source = root / relative
        if source.is_symlink() or not source.is_file():
            print(f"ERROR: manifested path is missing, not a regular file, or a symlink: {relative}", file=sys.stderr)
            return 4
        actual = sha256_file(source)
        if actual != expected:
            print(f"ERROR: hash mismatch for {relative}: expected {expected}, got {actual}", file=sys.stderr)
            return 5
        data = source.read_bytes()
        verified.append((relative, data, stat.S_IMODE(source.stat().st_mode), expected))

    manifest_relative = manifest.relative_to(root)
    if any(relative == manifest_relative for _, relative in entries):
        print("ERROR: the source manifest must not list itself", file=sys.stderr)
        return 6

    output.parent.mkdir(parents=True, exist_ok=True)
    if output.exists():
        print(f"ERROR: refusing to replace existing archive: {output}", file=sys.stderr)
        return 7

    manifest_data = manifest.read_bytes()
    manifest_mode = stat.S_IMODE(manifest.stat().st_mode)
    with zipfile.ZipFile(output, "x", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
        for relative, data, mode, _ in verified:
            name = f"{prefix.as_posix()}/{relative.as_posix()}"
            archive.writestr(zip_info(name, mode), data, compresslevel=9)
        manifest_name = f"{prefix.as_posix()}/{manifest_relative.as_posix()}"
        archive.writestr(zip_info(manifest_name, manifest_mode), manifest_data, compresslevel=9)

    expected_archive_entries = {
        f"{prefix.as_posix()}/{relative.as_posix()}": expected
        for relative, _, _, expected in verified
    }
    expected_archive_entries[f"{prefix.as_posix()}/{manifest_relative.as_posix()}"] = sha256_bytes(manifest_data)
    with zipfile.ZipFile(output, "r") as archive:
        names = archive.namelist()
        if len(names) != len(set(names)) or set(names) != set(expected_archive_entries):
            print("ERROR: archive entry set did not match the verified source set", file=sys.stderr)
            return 8
        for name, expected in expected_archive_entries.items():
            if sha256_bytes(archive.read(name)) != expected:
                print(f"ERROR: archive verification failed for {name}", file=sys.stderr)
                return 9

    result = {
        "schema": "eastern-kingdoms/sealed-source-archive-v1",
        "archive": str(output),
        "archive_sha256": sha256_file(output),
        "entry_count": len(expected_archive_entries),
        "manifest": str(manifest),
        "prefix": prefix.as_posix(),
        "status": "pass",
    }
    print(json.dumps(result, indent=2, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
