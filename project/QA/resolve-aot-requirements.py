#!/usr/bin/env python3
import argparse
import json
import os
import subprocess
import sys
import xml.etree.ElementTree as ET
from pathlib import Path


def fail(msg: str, code: int = 2) -> None:
    print(f"ERROR: {msg}", file=sys.stderr)
    raise SystemExit(code)


def dotnet_path(root: Path) -> Path:
    exe = root / ("dotnet.exe" if os.name == "nt" else "dotnet")
    if not exe.exists():
        fail(f"dotnet executable not found under {root}")
    return exe


def run_json(cmd):
    p = subprocess.run(cmd, text=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    if p.returncode != 0:
        fail(f"command failed ({p.returncode}): {' '.join(map(str, cmd))}\n{p.stderr.strip()}")
    try:
        return json.loads(p.stdout)
    except json.JSONDecodeError as exc:
        fail(f"expected JSON from command, got: {p.stdout[:500]!r}; {exc}")


def sdk_version(dotnet: Path) -> str:
    p = subprocess.run([str(dotnet), "--version"], text=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    if p.returncode != 0:
        fail(f"dotnet --version failed: {p.stderr.strip()}")
    return p.stdout.strip()


def load_bundled_props(root: Path, version: str):
    path = root / "sdk" / version / "Microsoft.NETCoreSdk.BundledVersions.props"
    if not path.exists():
        fail(f"bundled versions file not found: {path}")
    tree = ET.parse(path)
    return path, tree.getroot()


def first_attr(root, tag, predicate, attr):
    for elem in root.iter(tag):
        if predicate(elem):
            value = elem.attrib.get(attr)
            if value:
                return value
    return None


def evaluated_framework_refs(dotnet: Path, project: Path, tfm: str, rid: str):
    data = run_json([
        str(dotnet), "msbuild", str(project), "-nologo",
        "-getItem:FrameworkReference",
        f"-p:TargetFramework={tfm}",
        f"-p:RuntimeIdentifier={rid}",
        "-p:DisableTransitiveFrameworkReferenceDownloads=true",
    ])
    items = data.get("Items", {}).get("FrameworkReference", [])
    return sorted({item.get("Identity") for item in items if item.get("Identity")})


def main():
    ap = argparse.ArgumentParser(description="Resolve exact Native AOT NuGet pack requirements from an installed .NET SDK.")
    ap.add_argument("--dotnet-root", required=True)
    ap.add_argument("--project", required=True)
    ap.add_argument("--tfm", required=True)
    ap.add_argument("--rid", required=True)
    ap.add_argument("--output")
    args = ap.parse_args()

    root = Path(args.dotnet_root).resolve()
    project = Path(args.project).resolve()
    if not project.exists():
        fail(f"project not found: {project}")
    dotnet = dotnet_path(root)
    version = sdk_version(dotnet)
    props_path, props = load_bundled_props(root, version)

    il_link_version = first_attr(
        props, "KnownILLinkPack",
        lambda e: e.attrib.get("TargetFramework") == args.tfm,
        "ILLinkPackVersion")
    il_compiler_version = first_attr(
        props, "KnownILCompilerPack",
        lambda e: e.attrib.get("TargetFramework") == args.tfm,
        "ILCompilerPackVersion")
    il_compiler_pattern = first_attr(
        props, "KnownILCompilerPack",
        lambda e: e.attrib.get("TargetFramework") == args.tfm,
        "ILCompilerPackNamePattern")

    if not il_link_version:
        fail(f"no KnownILLinkPack for {args.tfm} in {props_path}")
    if not il_compiler_version or not il_compiler_pattern:
        fail(f"no KnownILCompilerPack for {args.tfm} in {props_path}")

    refs = evaluated_framework_refs(dotnet, project, args.tfm, args.rid)
    packages = [
        {
            "id": "Microsoft.NET.ILLink.Tasks",
            "version": il_link_version,
            "reason": "IL trimming/link analysis and build tasks",
        },
        {
            "id": "Microsoft.DotNet.ILCompiler",
            "version": il_compiler_version,
            "reason": "Native AOT compiler targets/tool contract",
        },
        {
            "id": il_compiler_pattern.replace("**RID**", args.rid),
            "version": il_compiler_version,
            "reason": f"Native AOT compiler implementation for {args.rid}",
        },
    ]

    for ref in refs:
        match = None
        for elem in props.iter("KnownFrameworkReference"):
            if elem.attrib.get("Include") == ref and elem.attrib.get("TargetFramework") == args.tfm:
                match = elem
                break
        if match is None:
            continue
        pattern = match.attrib.get("RuntimePackNamePatterns")
        runtime_version = match.attrib.get("LatestRuntimeFrameworkVersion") or match.attrib.get("DefaultRuntimeFrameworkVersion")
        supported = (match.attrib.get("RuntimePackRuntimeIdentifiers") or "").split(";")
        excluded = (match.attrib.get("RuntimePackExcludedRuntimeIdentifiers") or "").split(";")
        if pattern and runtime_version and args.rid in supported and args.rid not in excluded:
            packages.append({
                "id": pattern.replace("**RID**", args.rid),
                "version": runtime_version,
                "reason": f"runtime pack for evaluated FrameworkReference {ref}",
            })

    dedup = {}
    for pkg in packages:
        key = (pkg["id"].lower(), pkg["version"])
        dedup[key] = pkg
    packages = sorted(dedup.values(), key=lambda x: x["id"].lower())

    result = {
        "schema": "dot-net-native-aot/requirements-v1",
        "dotnet_root": str(root),
        "sdk_version": version,
        "bundled_versions_file": str(props_path),
        "project": str(project),
        "target_framework": args.tfm,
        "rid": args.rid,
        "framework_references": refs,
        "restore_property": "DisableTransitiveFrameworkReferenceDownloads=true",
        "packages": packages,
    }
    text = json.dumps(result, indent=2, sort_keys=True) + "\n"
    if args.output:
        Path(args.output).write_text(text, encoding="utf-8")
    else:
        sys.stdout.write(text)


if __name__ == "__main__":
    main()
