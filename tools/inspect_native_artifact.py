#!/usr/bin/env python3
"""Inspect a native artifact's format, architecture, checksum, and platform evidence."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import shutil
import struct
import subprocess
import sys
from pathlib import Path


PE_MACHINES = {0x014C: "x86", 0x8664: "x64", 0xAA64: "arm64"}
ELF_MACHINES = {3: "x86", 40: "arm", 62: "x64", 183: "arm64"}
MACH_CPUS = {7: "x86", 0x01000007: "x64", 12: "arm", 0x0100000C: "arm64"}


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def inspect_pe(data: bytes) -> dict[str, object] | None:
    if len(data) < 0x40 or data[:2] != b"MZ":
        return None
    pe_offset = struct.unpack_from("<I", data, 0x3C)[0]
    if pe_offset + 24 > len(data) or data[pe_offset:pe_offset + 4] != b"PE\0\0":
        return None
    machine = struct.unpack_from("<H", data, pe_offset + 4)[0]
    optional = pe_offset + 24
    if optional + 2 > len(data):
        return {"format": "PE", "os_family": "win", "architecture": PE_MACHINES.get(machine, "unknown")}
    magic = struct.unpack_from("<H", data, optional)[0]
    directory_base = optional + (112 if magic == 0x20B else 96)
    clr_offset = directory_base + 14 * 8
    has_clr_header = False
    if clr_offset + 8 <= len(data):
        clr_rva, clr_size = struct.unpack_from("<II", data, clr_offset)
        has_clr_header = bool(clr_rva and clr_size)
    return {
        "format": "PE",
        "os_family": "win",
        "architecture": PE_MACHINES.get(machine, "unknown"),
        "clr_header_present": has_clr_header,
    }


def inspect_elf(data: bytes) -> dict[str, object] | None:
    if len(data) < 20 or data[:4] != b"\x7fELF":
        return None
    endian = "<" if data[5] == 1 else ">"
    machine = struct.unpack_from(endian + "H", data, 18)[0]
    return {
        "format": "ELF",
        "os_family": "linux",
        "architecture": ELF_MACHINES.get(machine, "unknown"),
        "bits": 32 if data[4] == 1 else 64 if data[4] == 2 else "unknown",
    }


def inspect_macho(data: bytes) -> dict[str, object] | None:
    if len(data) < 8:
        return None
    magic_be = struct.unpack_from(">I", data, 0)[0]
    if magic_be in {0xCAFEBABE, 0xCAFEBABF}:
        return {"format": "Mach-O universal", "os_family": "osx", "architecture": "universal"}
    formats = {
        0xFEEDFACE: (">", 32), 0xFEEDFACF: (">", 64),
        0xCEFAEDFE: ("<", 32), 0xCFFAEDFE: ("<", 64),
    }
    if magic_be not in formats:
        return None
    endian, bits = formats[magic_be]
    cpu = struct.unpack_from(endian + "I", data, 4)[0]
    return {
        "format": "Mach-O", "os_family": "osx",
        "architecture": MACH_CPUS.get(cpu, "unknown"), "bits": bits,
    }


def expected_from_rid(rid: str) -> tuple[str | None, str | None]:
    parts = rid.lower().split("-")
    architecture = parts[-1] if parts[-1] in {"x86", "x64", "arm", "arm64"} else None
    if parts[0].startswith("win"):
        os_family = "win"
    elif parts[0] in {"osx", "macos"}:
        os_family = "osx"
    elif parts[0] in {"linux", "alpine"}:
        os_family = "linux"
    else:
        os_family = None
    return os_family, architecture


def version_key(value: str) -> tuple[int, ...]:
    return tuple(int(piece) for piece in value.split("."))


def enrich_elf(path: Path, details: dict[str, object]) -> None:
    readelf = shutil.which("readelf")
    if readelf:
        notes = subprocess.run([readelf, "-n", str(path)], text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
        if notes.returncode == 0:
            match = re.search(r"Build ID:\s*([0-9a-fA-F]+)", notes.stdout)
            if match:
                details["gnu_build_id"] = match.group(1).lower()
        versions = subprocess.run([readelf, "--version-info", str(path)], text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
        if versions.returncode == 0:
            found = set(re.findall(r"\bGLIBC_(\d+(?:\.\d+)+)\b", versions.stdout))
            if found:
                details["observed_glibc_symbol_floor"] = max(found, key=version_key)
    ldd = shutil.which("ldd")
    if ldd:
        deps = subprocess.run([ldd, str(path)], text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
        details["ldd_exit_code"] = deps.returncode
        if deps.returncode == 0:
            details["native_dependencies"] = [line.strip() for line in deps.stdout.splitlines() if line.strip()]


def inspect(path: Path, expected_rid: str | None) -> tuple[dict[str, object], list[str]]:
    errors: list[str] = []
    if not path.is_file():
        return {"path": str(path)}, ["artifact does not exist or is not a file"]
    with path.open("rb") as handle:
        data = handle.read(4096)
    details = inspect_pe(data) or inspect_elf(data) or inspect_macho(data)
    if details is None:
        details = {"format": "unknown", "os_family": "unknown", "architecture": "unknown"}
        errors.append("artifact is not a recognized PE, ELF, or Mach-O binary")
    details.update({
        "path": str(path.resolve()),
        "size_bytes": path.stat().st_size,
        "sha256": sha256(path),
        "executable_bit": bool(path.stat().st_mode & 0o111),
    })
    if details.get("format") == "ELF":
        enrich_elf(path, details)
    if details.get("format") in {"ELF", "Mach-O", "Mach-O universal"} and not details["executable_bit"]:
        errors.append("Unix native artifact is not marked executable")
    if details.get("format") == "PE" and details.get("clr_header_present"):
        errors.append("PE contains a CLR header and may be a managed assembly rather than Native AOT output")
    if expected_rid:
        expected_os, expected_arch = expected_from_rid(expected_rid)
        details["expected_rid"] = expected_rid
        if expected_os and details.get("os_family") != expected_os:
            errors.append(f"file format does not match RID OS family {expected_os}")
        if expected_arch and details.get("architecture") not in {expected_arch, "universal"}:
            errors.append(f"architecture does not match RID architecture {expected_arch}")
        if not expected_os or not expected_arch:
            errors.append("expected RID could not be fully interpreted")
    return details, errors


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("artifact", type=Path)
    parser.add_argument("--expected-rid")
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()
    details, errors = inspect(args.artifact, args.expected_rid)
    result = {
        "status": "fail" if errors else "pass",
        "artifact": details,
        "errors": errors,
        "scope": "format, architecture, checksum, and available platform evidence only; Native AOT provenance and behavior require publish logs and runtime tests",
    }
    if args.json:
        print(json.dumps(result, indent=2, sort_keys=True))
    else:
        print(f"Native artifact inspection: {result['status'].upper()}")
        for key, value in details.items():
            print(f"{key}: {value}")
        for error in errors:
            print(f"ERROR: {error}")
        print("NOTE: Artifact inspection does not prove Native AOT provenance or correctness.")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
