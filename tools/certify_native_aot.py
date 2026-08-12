#!/usr/bin/env python3
"""Restore, publish, inspect, execute, and record proof for a Native AOT project."""
from __future__ import annotations

import argparse
import hashlib
import json
import os
import platform
import re
import shutil
import subprocess
import sys
import time
import xml.etree.ElementTree as ET
from pathlib import Path


def sha256(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def run(cmd, cwd: Path, log: Path, env=None):
    started = time.time()
    p = subprocess.run(cmd, cwd=cwd, env=env, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    elapsed = time.time() - started
    log.write_text(p.stdout, encoding="utf-8")
    return p.returncode, elapsed, p.stdout


def project_value(project: Path, tag: str, default: str = "") -> str:
    root = ET.parse(project).getroot()
    for elem in root.iter():
        if elem.tag.split("}")[-1] == tag and (elem.text or "").strip():
            return (elem.text or "").strip()
    return default


def host_matches(rid: str) -> bool:
    sysname = platform.system().lower()
    machine = platform.machine().lower()
    if rid.startswith("linux-") and sysname != "linux":
        return False
    if rid.startswith("win-") and sysname != "windows":
        return False
    if (rid.startswith("osx-") or rid.startswith("macos-")) and sysname != "darwin":
        return False
    if rid.endswith("-x64") and machine not in {"x86_64", "amd64"}:
        return False
    if rid.endswith("-arm64") and machine not in {"aarch64", "arm64"}:
        return False
    return True


def detect_format(path: Path):
    data = path.read_bytes()[:64]
    result = {"format": "unknown", "architecture": "unknown"}
    if data[:4] == b"\x7fELF" and len(data) >= 20:
        result["format"] = "ELF"
        result["elf_class"] = 64 if data[4] == 2 else 32 if data[4] == 1 else None
        endian = "little" if data[5] == 1 else "big"
        machine = int.from_bytes(data[18:20], endian)
        names = {3: "x86", 40: "arm", 62: "x86-64", 183: "aarch64"}
        result["architecture"] = names.get(machine, f"ELF-machine-{machine}")
    elif data[:2] == b"MZ":
        result["format"] = "PE"
    elif data[:4] in {b"\xcf\xfa\xed\xfe", b"\xfe\xed\xfa\xcf", b"\xca\xfe\xba\xbe", b"\xbe\xba\xfe\xca"}:
        result["format"] = "Mach-O"
    return result


def expected_format(rid: str):
    if rid.startswith("linux-"):
        return "ELF"
    if rid.startswith("win-"):
        return "PE"
    if rid.startswith("osx-") or rid.startswith("macos-"):
        return "Mach-O"
    return None


def evaluated_publish_dir(dotnet: Path, project: Path, configuration: str, tfm: str, rid: str, env):
    cmd = [
        str(dotnet), "msbuild", str(project), "-nologo",
        "-getProperty:PublishDir",
        f"-p:Configuration={configuration}",
        f"-p:TargetFramework={tfm}",
        f"-p:RuntimeIdentifier={rid}",
        "-p:PublishAot=true",
    ]
    p = subprocess.run(cmd, cwd=project.parent, env=env, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    if p.returncode != 0:
        return None, p.stdout
    lines = [line.strip() for line in p.stdout.splitlines() if line.strip()]
    if not lines:
        return None, p.stdout
    raw = lines[-1]
    if os.name != "nt":
        raw = raw.replace("\\", "/")
    pub = Path(raw)
    if not pub.is_absolute():
        pub = project.parent / pub
    return pub.resolve(), p.stdout


def locate_binary(pub: Path, assembly: str):
    if not pub.exists():
        return None
    for name in [assembly, assembly + ".exe"]:
        p = pub / name
        if p.is_file():
            return p
    candidates = []
    for p in pub.iterdir():
        if not p.is_file():
            continue
        if p.suffix.lower() in {".dll", ".json", ".pdb", ".dbg", ".so", ".dylib", ".a", ".lib"}:
            continue
        if os.access(p, os.X_OK) or p.suffix.lower() == ".exe":
            candidates.append(p)
    return candidates[0] if len(candidates) == 1 else None


def sanitize_smoke_env(build_env: dict[str, str], dotnet_root: Path) -> dict[str, str]:
    env = dict(build_env)
    for key in list(env):
        if key == "DOTNET_HOST_PATH" or key == "DOTNET_MULTILEVEL_LOOKUP" or key.startswith("DOTNET_ROOT"):
            env.pop(key, None)
    root = str(dotnet_root.resolve())
    path_parts = []
    for part in env.get("PATH", "").split(os.pathsep):
        if not part:
            continue
        try:
            resolved = str(Path(part).resolve())
        except OSError:
            resolved = part
        if resolved == root:
            continue
        path_parts.append(part)
    env["PATH"] = os.pathsep.join(path_parts)
    return env


def readelf_text(path: Path, *args: str) -> tuple[int, str]:
    tool = shutil.which("readelf")
    if not tool:
        return 127, ""
    p = subprocess.run([tool, *args, str(path)], text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    return p.returncode, p.stdout


def parse_build_id(text: str) -> str | None:
    m = re.search(r"Build ID:\s*([0-9a-fA-F]+)", text)
    return m.group(1).lower() if m else None


def version_key(value: str) -> tuple[int, ...]:
    return tuple(int(piece) for piece in value.split("."))


def parse_glibc_floor(text: str) -> str | None:
    versions = set(re.findall(r"\bGLIBC_(\d+(?:\.\d+)+)\b", text))
    if not versions:
        return None
    return max(versions, key=version_key)


def symbol_companions(binary: Path) -> list[Path]:
    candidates = [
        binary.with_name(binary.name + ".dbg"),
        binary.with_suffix(binary.suffix + ".pdb") if binary.suffix else binary.with_name(binary.name + ".pdb"),
    ]
    return [p for p in candidates if p.is_file()]


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--dotnet-root", required=True)
    ap.add_argument("--project", required=True)
    ap.add_argument("--rid", required=True)
    ap.add_argument("--feed")
    ap.add_argument("--source", action="append", default=[])
    ap.add_argument("--configuration", default="Release")
    ap.add_argument("--tfm")
    ap.add_argument("--evidence-dir", required=True)
    ap.add_argument("--smoke-arg", action="append", default=[])
    ap.add_argument("--skip-run", action="store_true")
    ap.add_argument(
        "--configuration-validation-pass",
        action="store_true",
        help="Record that the caller completed its authoritative configuration gate before this command.",
    )
    ap.add_argument(
        "--managed-validation-pass",
        action="store_true",
        help="Record that the caller completed its authoritative managed build/test gate before this command.",
    )
    ap.add_argument(
        "--inherit-smoke-env",
        action="store_true",
        help="Run the produced binary with the build DOTNET_ROOT/PATH environment. Default is a sanitized self-contained smoke environment.",
    )
    args = ap.parse_args()

    dotnet_root = Path(args.dotnet_root).resolve()
    dotnet = dotnet_root / ("dotnet.exe" if os.name == "nt" else "dotnet")
    project = Path(args.project).resolve()
    evidence = Path(args.evidence_dir).resolve()
    evidence.mkdir(parents=True, exist_ok=True)
    if not dotnet.exists():
        print(f"ERROR: dotnet not found at {dotnet}", file=sys.stderr)
        return 2
    if not project.exists():
        print(f"ERROR: project not found: {project}", file=sys.stderr)
        return 2

    tfm = args.tfm or project_value(project, "TargetFramework")
    if not tfm:
        print("ERROR: cannot determine TargetFramework; pass --tfm", file=sys.stderr)
        return 2
    assembly = project_value(project, "AssemblyName", project.stem)

    build_env = os.environ.copy()
    build_env["DOTNET_ROOT"] = str(dotnet_root)
    build_env["PATH"] = str(dotnet_root) + os.pathsep + build_env.get("PATH", "")

    sources = []
    if args.feed:
        sources.append(str(Path(args.feed).resolve()))
    sources.extend(args.source)

    restore = [
        str(dotnet), "restore", str(project),
        "-r", args.rid,
        "-p:PublishAot=true",
        "-p:DisableTransitiveFrameworkReferenceDownloads=true",
    ]
    for source in sources:
        restore += ["--source", source]

    report = {
        "schema": "dot-net-native-aot/certification-v2",
        "project": str(project),
        "rid": args.rid,
        "target_framework": tfm,
        "configuration": args.configuration,
        "proof": {
            "configuration": "pass-external-gate" if args.configuration_validation_pass else "not-run",
            "managed": "pass-external-gate" if args.managed_validation_pass else "not-run",
            "native_publication": "not-run",
            "artifact_validation": "not-run",
            "release_approval": "owner-required",
        },
        "commands": [],
    }

    rc, sec, _ = run(restore, project.parent, evidence / "01-native-restore.log", build_env)
    report["commands"].append({"name": "native-restore", "command": restore, "exit_code": rc, "seconds": round(sec, 3)})
    if rc != 0:
        report["proof"]["native_publication"] = "blocked-at-restore"
        (evidence / "certification.json").write_text(json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8")
        print("Native AOT restore failed; see evidence/01-native-restore.log", file=sys.stderr)
        return 10

    publish = [
        str(dotnet), "publish", str(project),
        "-c", args.configuration,
        "-r", args.rid,
        "--self-contained", "true",
        "-p:PublishAot=true",
        "-p:DisableTransitiveFrameworkReferenceDownloads=true",
        "--no-restore",
    ]
    rc, sec, _ = run(publish, project.parent, evidence / "02-native-publish.log", build_env)
    report["commands"].append({"name": "native-publish", "command": publish, "exit_code": rc, "seconds": round(sec, 3)})
    if rc != 0:
        report["proof"]["native_publication"] = "failed"
        (evidence / "certification.json").write_text(json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8")
        print("Native AOT publish failed; see evidence/02-native-publish.log", file=sys.stderr)
        return 11

    report["proof"]["native_publication"] = "pass"
    pubdir, msbuild_publish_output = evaluated_publish_dir(dotnet, project, args.configuration, tfm, args.rid, build_env)
    if pubdir is None:
        report["proof"]["artifact_validation"] = "publish-dir-evaluation-failed"
        (evidence / "03-publish-dir-msbuild.txt").write_text(msbuild_publish_output, encoding="utf-8")
        (evidence / "certification.json").write_text(json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8")
        print("Native publish passed but MSBuild PublishDir evaluation failed", file=sys.stderr)
        return 12
    report["publish_dir"] = str(pubdir)
    (evidence / "03-publish-dir-msbuild.txt").write_text(msbuild_publish_output, encoding="utf-8")
    binary = locate_binary(pubdir, assembly)
    if binary is None:
        report["proof"]["artifact_validation"] = "binary-not-found"
        (evidence / "certification.json").write_text(json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8")
        print(f"Native publish passed but executable could not be uniquely located under {pubdir}", file=sys.stderr)
        return 12

    fmt = detect_format(binary)
    report["artifact"] = {"path": str(binary), "size_bytes": binary.stat().st_size, "sha256": sha256(binary), **fmt}
    want = expected_format(args.rid)
    if want and fmt["format"] != want:
        report["proof"]["artifact_validation"] = "format-mismatch"
        (evidence / "certification.json").write_text(json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8")
        print(f"Artifact format mismatch: expected {want}, got {fmt['format']}", file=sys.stderr)
        return 13
    if args.rid.endswith("-x64") and fmt.get("architecture") not in {"x86-64", "unknown"}:
        report["proof"]["artifact_validation"] = "architecture-mismatch"
        (evidence / "certification.json").write_text(json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8")
        return 14
    if args.rid.endswith("-arm64") and fmt.get("architecture") not in {"aarch64", "unknown"}:
        report["proof"]["artifact_validation"] = "architecture-mismatch"
        (evidence / "certification.json").write_text(json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8")
        return 14

    file_tool = shutil.which("file")
    if file_tool:
        p = subprocess.run([file_tool, str(binary)], text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
        (evidence / "04-file.txt").write_text(p.stdout, encoding="utf-8")
        report["artifact"]["file_output"] = p.stdout.strip()

    if fmt["format"] == "ELF":
        rc, header = readelf_text(binary, "-h")
        if rc == 0:
            (evidence / "05-readelf-header.txt").write_text(header, encoding="utf-8")
        rc, notes = readelf_text(binary, "-n")
        if rc == 0:
            (evidence / "06-readelf-notes.txt").write_text(notes, encoding="utf-8")
            report["artifact"]["gnu_build_id"] = parse_build_id(notes)
        rc, versions = readelf_text(binary, "--version-info")
        if rc == 0:
            (evidence / "07-readelf-version-info.txt").write_text(versions, encoding="utf-8")
            floor = parse_glibc_floor(versions)
            if floor:
                report["artifact"]["observed_glibc_symbol_floor"] = floor
        if shutil.which("ldd"):
            p = subprocess.run(["ldd", str(binary)], text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
            (evidence / "08-native-dependencies.txt").write_text(p.stdout, encoding="utf-8")
            report["artifact"]["native_dependencies_exit_code"] = p.returncode

    companions = []
    for companion in symbol_companions(binary):
        item = {"path": str(companion), "size_bytes": companion.stat().st_size, "sha256": sha256(companion)}
        if fmt["format"] == "ELF" and companion.suffix.lower() == ".dbg":
            rc, notes = readelf_text(companion, "-n")
            if rc == 0:
                item["gnu_build_id"] = parse_build_id(notes)
        companions.append(item)
    if companions:
        report["symbol_companions"] = companions
        executable_build_id = report["artifact"].get("gnu_build_id")
        for item in companions:
            companion_build_id = item.get("gnu_build_id")
            if executable_build_id and companion_build_id and executable_build_id != companion_build_id:
                report["proof"]["artifact_validation"] = "symbol-build-id-mismatch"
                (evidence / "certification.json").write_text(json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8")
                print("Native executable and debug companion Build IDs do not match", file=sys.stderr)
                return 16

    compatible = host_matches(args.rid)
    report["host"] = {"system": platform.system(), "machine": platform.machine(), "rid_compatible": compatible}
    if args.skip_run:
        report["proof"]["artifact_validation"] = "inspection-pass-execution-skipped"
    elif not compatible:
        report["proof"]["artifact_validation"] = "inspection-pass-host-incompatible"
    else:
        smoke_env = build_env if args.inherit_smoke_env else sanitize_smoke_env(build_env, dotnet_root)
        report["native_smoke_environment"] = {
            "mode": "inherited-build-environment" if args.inherit_smoke_env else "sanitized-self-contained",
            "dotnet_root_present": any(key.startswith("DOTNET_ROOT") for key in smoke_env),
            "dotnet_host_path_present": "DOTNET_HOST_PATH" in smoke_env,
        }
        cmd = [str(binary), *args.smoke_arg]
        rc, sec, _ = run(cmd, binary.parent, evidence / "09-native-smoke.log", smoke_env)
        report["commands"].append({"name": "native-smoke", "command": cmd, "exit_code": rc, "seconds": round(sec, 3)})
        if rc != 0:
            report["proof"]["artifact_validation"] = "execution-failed"
            (evidence / "certification.json").write_text(json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8")
            print(f"Native artifact execution failed with exit code {rc}", file=sys.stderr)
            return 15
        report["proof"]["artifact_validation"] = "pass"

    (evidence / "artifact.sha256").write_text(f"{report['artifact']['sha256']}  {binary.name}\n", encoding="utf-8")
    if companions:
        symbol_checksums = "".join(
            f"{item['sha256']}  {Path(item['path']).name}\n"
            for item in companions
        )
        (evidence / "symbol.sha256").write_text(symbol_checksums, encoding="utf-8")
    (evidence / "certification.json").write_text(json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(json.dumps(report, indent=2, sort_keys=True))
    if report["proof"]["artifact_validation"] != "pass":
        return 20
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
