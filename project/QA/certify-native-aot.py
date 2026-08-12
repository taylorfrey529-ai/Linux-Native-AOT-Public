#!/usr/bin/env python3
import argparse
import hashlib
import json
import os
import platform
import shutil
import stat
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


def locate_binary(project: Path, configuration: str, tfm: str, rid: str, assembly: str):
    pub = project.parent / "bin" / configuration / tfm / rid / "publish"
    if not pub.exists():
        return pub, None
    names = [assembly, assembly + ".exe"]
    for name in names:
        p = pub / name
        if p.is_file():
            return pub, p
    candidates = []
    for p in pub.iterdir():
        if not p.is_file():
            continue
        if p.suffix.lower() in {".dll", ".json", ".pdb", ".dbg", ".so", ".dylib", ".a", ".lib"}:
            continue
        if os.access(p, os.X_OK) or p.suffix.lower() == ".exe":
            candidates.append(p)
    return pub, candidates[0] if len(candidates) == 1 else None


def main():
    ap = argparse.ArgumentParser(description="Restore, publish, inspect, execute, and record proof for a Native AOT project.")
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

    env = os.environ.copy()
    env["DOTNET_ROOT"] = str(dotnet_root)
    env["PATH"] = str(dotnet_root) + os.pathsep + env.get("PATH", "")

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
        "schema": "dot-net-native-aot/certification-v1",
        "project": str(project),
        "rid": args.rid,
        "target_framework": tfm,
        "configuration": args.configuration,
        "proof": {
            "configuration": "not-run",
            "managed": "not-run",
            "native_publication": "not-run",
            "artifact_validation": "not-run",
            "release_approval": "owner-required",
        },
        "commands": [],
    }

    rc, sec, out = run(restore, project.parent, evidence / "01-native-restore.log", env)
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
    rc, sec, out = run(publish, project.parent, evidence / "02-native-publish.log", env)
    report["commands"].append({"name": "native-publish", "command": publish, "exit_code": rc, "seconds": round(sec, 3)})
    if rc != 0:
        report["proof"]["native_publication"] = "failed"
        (evidence / "certification.json").write_text(json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8")
        print("Native AOT publish failed; see evidence/02-native-publish.log", file=sys.stderr)
        return 11

    report["proof"]["native_publication"] = "pass"
    pubdir, binary = locate_binary(project, args.configuration, tfm, args.rid, assembly)
    if binary is None:
        report["proof"]["artifact_validation"] = "binary-not-found"
        report["publish_dir"] = str(pubdir)
        (evidence / "certification.json").write_text(json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8")
        print(f"Native publish passed but executable could not be uniquely located under {pubdir}", file=sys.stderr)
        return 12

    fmt = detect_format(binary)
    report["artifact"] = {
        "path": str(binary),
        "size_bytes": binary.stat().st_size,
        "sha256": sha256(binary),
        **fmt,
    }
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
        (evidence / "03-file.txt").write_text(p.stdout, encoding="utf-8")
        report["artifact"]["file_output"] = p.stdout.strip()
    if fmt["format"] == "ELF" and shutil.which("readelf"):
        p = subprocess.run(["readelf", "-h", str(binary)], text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
        (evidence / "04-readelf-header.txt").write_text(p.stdout, encoding="utf-8")
    if fmt["format"] == "ELF" and shutil.which("ldd"):
        p = subprocess.run(["ldd", str(binary)], text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
        (evidence / "05-native-dependencies.txt").write_text(p.stdout, encoding="utf-8")
        report["artifact"]["native_dependencies_exit_code"] = p.returncode

    compatible = host_matches(args.rid)
    report["host"] = {"system": platform.system(), "machine": platform.machine(), "rid_compatible": compatible}
    if args.skip_run:
        report["proof"]["artifact_validation"] = "inspection-pass-execution-skipped"
    elif not compatible:
        report["proof"]["artifact_validation"] = "inspection-pass-host-incompatible"
    else:
        cmd = [str(binary), *args.smoke_arg]
        rc, sec, out = run(cmd, binary.parent, evidence / "06-native-smoke.log", env)
        report["commands"].append({"name": "native-smoke", "command": cmd, "exit_code": rc, "seconds": round(sec, 3)})
        if rc != 0:
            report["proof"]["artifact_validation"] = "execution-failed"
            (evidence / "certification.json").write_text(json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8")
            print(f"Native artifact execution failed with exit code {rc}", file=sys.stderr)
            return 15
        report["proof"]["artifact_validation"] = "pass"

    (evidence / "artifact.sha256").write_text(f"{report['artifact']['sha256']}  {binary.name}\n", encoding="utf-8")
    (evidence / "certification.json").write_text(json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(json.dumps(report, indent=2, sort_keys=True))
    if report["proof"]["artifact_validation"] != "pass":
        return 20
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
