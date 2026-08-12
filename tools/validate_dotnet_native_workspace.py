#!/usr/bin/env python3
"""Validate a modern .NET Native AOT workspace or project configuration."""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path
from xml.etree import ElementTree


CERTA_REQUIRED_PATHS = (
    "AGENTS.md",
    "README.md",
    "archive",
    "project/src/CertaRt/CertaRt.csproj",
    "project/src/CertaRt/Program.cs",
    "update-bindings/workspace-manifest.json",
    "update-bindings/update-lock.json",
    "memory/agent-continuity.md",
    "memory/version-history.md",
)


def local_name(tag: str) -> str:
    return tag.rsplit("}", 1)[-1]


def load_json(path: Path, failures: list[str]) -> dict:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except Exception as exc:  # noqa: BLE001 - validation should report exact failure.
        failures.append(f"{path}: invalid JSON: {exc}")
        return {}


def find_project(root: Path, manifest: dict, explicit_project: str | None, failures: list[str]) -> Path | None:
    if explicit_project:
        project = root / explicit_project
        if project.exists():
            return project
        failures.append(f"missing project file: {explicit_project}")
        return None

    manifest_project = manifest.get("build", {}).get("projectFile")
    if manifest_project:
        project = root / manifest_project
        if project.exists():
            return project
        failures.append(f"manifest build.projectFile is missing: {manifest_project}")
        return None

    projects = sorted(root.rglob("*.csproj"))
    if len(projects) == 1:
        return projects[0]
    if not projects:
        failures.append("no .csproj file found")
    else:
        failures.append("multiple .csproj files found; pass --project")
    return None


def read_properties(project: Path, failures: list[str]) -> dict[str, str]:
    try:
        document = ElementTree.parse(project)
    except Exception as exc:  # noqa: BLE001 - validation should report exact failure.
        failures.append(f"{project}: invalid project XML: {exc}")
        return {}

    properties: dict[str, str] = {}
    for group in document.getroot():
        if local_name(group.tag) != "PropertyGroup":
            continue
        for element in group:
            properties[local_name(element.tag)] = (element.text or "").strip()
    return properties


def target_matches(properties: dict[str, str], expected: str) -> bool:
    single = properties.get("TargetFramework")
    if single == expected:
        return True
    frameworks = properties.get("TargetFrameworks", "")
    return expected in [item.strip() for item in frameworks.split(";") if item.strip()]


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("root", nargs="?", default=".", help="Workspace or repository root.")
    parser.add_argument("--project", help="Project path relative to root. Required when multiple .csproj files exist.")
    parser.add_argument("--manifest", default="update-bindings/workspace-manifest.json", help="Manifest path relative to root.")
    parser.add_argument("--product", help="Expected product name in the manifest.")
    parser.add_argument("--target-framework", default="net10.0", help="Expected target framework.")
    parser.add_argument("--require-certa-rt-contract", action="store_true", help="Require the CERTA-RT workspace contract paths and canonical values.")
    args = parser.parse_args()

    root = Path(args.root).resolve()
    failures: list[str] = []
    warnings: list[str] = []

    if not root.exists():
        failures.append(f"root does not exist: {root}")
        print_report(failures, warnings)
        return 1

    if args.require_certa_rt_contract:
        for relative in CERTA_REQUIRED_PATHS:
            if not (root / relative).exists():
                failures.append(f"missing required CERTA-RT path: {relative}")

    manifest_path = root / args.manifest
    manifest = load_json(manifest_path, failures) if manifest_path.exists() else {}
    if not manifest and args.require_certa_rt_contract:
        failures.append(f"missing manifest: {args.manifest}")

    expected_product = args.product or ("CERTA-RT" if args.require_certa_rt_contract else None)
    if manifest:
        if expected_product and manifest.get("product", {}).get("name") != expected_product:
            failures.append(f"manifest product must be {expected_product}")

        target = manifest.get("product", {}).get("target", {})
        if target.get("label") and target.get("label") != ".NET Native":
            failures.append("manifest target label must be .NET Native")
        if target.get("implementation") and target.get("implementation") != "Native AOT":
            failures.append("manifest implementation must be Native AOT")
        if target.get("targetFramework") and target.get("targetFramework") != args.target_framework:
            failures.append(f"manifest target framework must be {args.target_framework}")

    project = find_project(root, manifest, args.project, failures)
    if project:
        properties = read_properties(project, failures)
        if properties:
            if not target_matches(properties, args.target_framework):
                failures.append(f"{project.relative_to(root)} must target {args.target_framework}")
            if properties.get("PublishAot") != "true":
                failures.append(f"{project.relative_to(root)} must set PublishAot=true")
            if properties.get("SelfContained") != "true":
                warnings.append(f"{project.relative_to(root)} does not explicitly set SelfContained=true")
            if properties.get("StripSymbols") != "true":
                warnings.append(f"{project.relative_to(root)} does not explicitly set StripSymbols=true")

    agents_path = root / "AGENTS.md"
    if args.require_certa_rt_contract and agents_path.exists():
        agents = agents_path.read_text(encoding="utf-8")
        for required in ("CERTA-RT", ".NET Native", "Native AOT"):
            if required not in agents:
                failures.append(f"AGENTS.md is missing canonical value: {required}")

    print_report(failures, warnings)
    return 1 if failures else 0


def print_report(failures: list[str], warnings: list[str]) -> None:
    if failures:
        print("FAIL: .NET Native workspace validation failed.", file=sys.stderr)
        for failure in failures:
            print(f"- {failure}", file=sys.stderr)
    else:
        print("PASS: .NET Native workspace configuration is internally consistent.")
        print("NOTE: This validates configuration; it does not compile Native AOT.")

    if warnings:
        print("WARNINGS:")
        for warning in warnings:
            print(f"- {warning}")


if __name__ == "__main__":
    raise SystemExit(main())
