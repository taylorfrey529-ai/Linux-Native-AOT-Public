#!/usr/bin/env python3
"""Validate Native AOT project configuration and flag common compatibility hazards."""

from __future__ import annotations

import argparse
import json
import re
import sys
from dataclasses import asdict, dataclass
from pathlib import Path
from xml.etree import ElementTree


@dataclass(order=True)
class Finding:
    severity: str
    code: str
    path: str
    line: int
    message: str


HAZARDS = (
    ("AOT001", re.compile(r"\bAssembly\.(Load|LoadFrom|LoadFile|LoadBytes)\s*\("),
     "dynamic assembly loading is not compatible with a closed Native AOT graph"),
    ("AOT002", re.compile(r"\b(System\.Reflection\.Emit|DynamicMethod)\b"),
     "runtime IL generation is not Native AOT-compatible"),
    ("AOT003", re.compile(r"\bActivator\.CreateInstance\s*\("),
     "review reflection-based construction and preserved constructor requirements"),
    ("AOT004", re.compile(r"\b(Type\.GetType|MakeGenericType|MakeGenericMethod)\s*\("),
     "review runtime type construction and metadata requirements"),
    ("AOT005", re.compile(r"\bExpression(?:<[^>]+>)?\.Compile\s*\("),
     "review expression compilation for runtime-code-generation requirements"),
    ("INT001", re.compile(r"\[\s*DllImport\s*\("),
     "prefer source-generated LibraryImport and verify marshalling"),
    ("INT002", re.compile(r"\[\s*ComImport\b"),
     "built-in COM interop requires a Native AOT-specific design review"),
)


def local_name(tag: str) -> str:
    return tag.rsplit("}", 1)[-1]


def add(findings: list[Finding], severity: str, code: str, path: Path | str,
        line: int, message: str) -> None:
    findings.append(Finding(severity, code, str(path), line, message))


def parse_xml(path: Path, findings: list[Finding]) -> ElementTree.Element | None:
    try:
        return ElementTree.parse(path).getroot()
    except (OSError, ElementTree.ParseError) as exc:
        add(findings, "error", "CFG001", path, 0, f"invalid project XML: {exc}")
        return None


def property_files(root: Path, project: Path) -> list[Path]:
    files: list[Path] = []
    current = project.parent
    while True:
        props = current / "Directory.Build.props"
        if props.is_file():
            files.append(props)
        if current == root or current.parent == current:
            break
        current = current.parent
    return list(reversed(files)) + [project]


def has_sdk_pin(root: Path, project: Path) -> bool:
    current = project.parent
    while True:
        if (current / "global.json").is_file():
            return True
        if current == root or current.parent == current:
            return False
        current = current.parent


def collect_properties(paths: list[Path], findings: list[Finding]) -> tuple[dict[str, str], set[str]]:
    properties: dict[str, str] = {}
    conditioned: set[str] = set()
    for path in paths:
        document = parse_xml(path, findings)
        if document is None:
            continue
        for group in document.iter():
            if local_name(group.tag) != "PropertyGroup":
                continue
            group_condition = group.attrib.get("Condition", "").strip()
            for element in group:
                name = local_name(element.tag)
                value = (element.text or "").strip()
                condition = group_condition or element.attrib.get("Condition", "").strip()
                if condition:
                    conditioned.add(name)
                    continue
                if value:
                    properties[name] = value
    return properties, conditioned


def find_project(root: Path, explicit: str | None, findings: list[Finding]) -> Path | None:
    if explicit:
        path = (root / explicit).resolve()
        try:
            path.relative_to(root)
        except ValueError:
            add(findings, "error", "CFG002", explicit, 0,
                "project path must remain inside the repository root")
            return None
        if path.is_file() and path.suffix == ".csproj":
            return path
        add(findings, "error", "CFG002", explicit, 0, "project file does not exist")
        return None
    projects = sorted(
        path for path in root.rglob("*.csproj")
        if not {"bin", "obj"}.intersection(path.parts)
    )
    if len(projects) == 1:
        return projects[0]
    if not projects:
        add(findings, "error", "CFG003", root, 0, "no .csproj file found")
    else:
        add(findings, "error", "CFG004", root, 0,
            "multiple projects found; pass --project")
    return None


def target_matches(properties: dict[str, str], expected: str) -> bool:
    if properties.get("TargetFramework") == expected:
        return True
    targets = [item.strip() for item in properties.get("TargetFrameworks", "").split(";")]
    return expected in targets


def bool_property(properties: dict[str, str], name: str) -> bool | None:
    value = properties.get(name, "").strip().lower()
    if value == "true":
        return True
    if value == "false":
        return False
    return None


def scan_sources(root: Path, findings: list[Finding]) -> None:
    sources = [
        path for path in root.rglob("*.cs")
        if not {"bin", "obj", ".git"}.intersection(path.parts)
    ]
    has_json_context = False
    json_usage: list[tuple[Path, int]] = []
    for path in sources:
        try:
            lines = path.read_text(encoding="utf-8-sig").splitlines()
        except (OSError, UnicodeError) as exc:
            add(findings, "warning", "SRC001", path, 0, f"could not scan source: {exc}")
            continue
        for line_number, line in enumerate(lines, 1):
            if "JsonSerializerContext" in line or "[JsonSerializable" in line:
                has_json_context = True
            if re.search(r"\bJsonSerializer\.(Serialize|Deserialize)\s*", line):
                json_usage.append((path, line_number))
            for code, pattern, message in HAZARDS:
                if pattern.search(line):
                    add(findings, "warning", code, path, line_number, message)
    if json_usage and not has_json_context:
        for path, line_number in json_usage[:10]:
            add(findings, "warning", "SER001", path, line_number,
                "JSON use found without a visible source-generated JsonSerializerContext")


def validate(root: Path, project_arg: str | None, expected_framework: str,
             kind: str) -> tuple[list[Finding], dict[str, object]]:
    findings: list[Finding] = []
    metadata: dict[str, object] = {"root": str(root)}
    if not root.is_dir():
        add(findings, "error", "CFG000", root, 0, "repository root is not a directory")
        return findings, metadata

    project = find_project(root, project_arg, findings)
    if project is None:
        return findings, metadata
    metadata["project"] = str(project)

    files = property_files(root, project)
    properties, conditioned = collect_properties(files, findings)
    metadata["property_files"] = [str(path) for path in files]
    metadata["target_framework"] = properties.get("TargetFramework") or properties.get("TargetFrameworks")

    output_type = properties.get("OutputType", "").lower()
    publish_aot = bool_property(properties, "PublishAot")
    detected_kind = kind
    if kind == "auto":
        detected_kind = "app" if output_type in {"exe", "winexe"} or publish_aot else "library"
    metadata["kind"] = detected_kind

    if not target_matches(properties, expected_framework):
        add(findings, "error", "CFG010", project, 0,
            f"project does not include expected target framework {expected_framework}")

    if detected_kind == "app":
        if publish_aot is not True:
            add(findings, "error", "CFG011", project, 0,
                "Native AOT application must set PublishAot=true in evaluated project properties")
        if bool_property(properties, "SelfContained") is False:
            add(findings, "error", "CFG012", project, 0,
                "SelfContained=false conflicts with Native AOT publication")
    else:
        if bool_property(properties, "IsAotCompatible") is not True:
            add(findings, "error", "CFG013", project, 0,
                "Native AOT-compatible library must set IsAotCompatible=true")

    if "PublishAot" in conditioned:
        add(findings, "warning", "CFG014", project, 0,
            "PublishAot has conditioned assignments; static validation cannot prove the active configuration")

    if bool_property(properties, "PublishTrimmed") is False:
        add(findings, "warning", "CFG015", project, 0,
            "PublishTrimmed=false conflicts with the trimming required by Native AOT")

    no_warn = properties.get("NoWarn", "")
    if re.search(r"(^|[;,\s])IL[23](?:\d+|\*)", no_warn, re.IGNORECASE):
        add(findings, "warning", "CFG020", project, 0,
            "NoWarn suppresses trimming or AOT diagnostics; verify each suppression")

    if properties.get("Nullable", "").strip().lower() != "enable":
        add(findings, "warning", "QLT001", project, 0, "Nullable is not explicitly enabled")
    if bool_property(properties, "TreatWarningsAsErrors") is not True:
        add(findings, "warning", "QLT002", project, 0,
            "TreatWarningsAsErrors is not explicitly enabled")
    if bool_property(properties, "Deterministic") is not True:
        add(findings, "warning", "QLT003", project, 0,
            "Deterministic is not explicitly enabled")

    package_refs: list[str] = []
    document = parse_xml(project, findings)
    if document is not None:
        for element in document.iter():
            if local_name(element.tag) == "PackageReference":
                include = element.attrib.get("Include") or element.attrib.get("Update")
                if include:
                    package_refs.append(include)
    metadata["package_references"] = sorted(package_refs)
    if package_refs and not (root / "packages.lock.json").exists():
        project_lock = project.parent / "packages.lock.json"
        if not project_lock.exists():
            add(findings, "warning", "DEP001", project, 0,
                "package references exist but no packages.lock.json was found")

    if not has_sdk_pin(root, project):
        add(findings, "warning", "SDK001", root, 0,
            "global.json is absent; release SDK selection may drift")

    scan_sources(project.parent, findings)
    metadata["properties"] = properties
    return sorted(findings), metadata


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("root", nargs="?", default=".", type=Path)
    parser.add_argument("--project", help=".csproj path relative to repository root")
    parser.add_argument("--target-framework", default="net10.0")
    parser.add_argument("--kind", choices=("auto", "app", "library"), default="auto")
    parser.add_argument("--strict", action="store_true", help="fail on warnings")
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()

    findings, metadata = validate(
        args.root.resolve(), args.project, args.target_framework, args.kind
    )
    errors = sum(f.severity == "error" for f in findings)
    warnings = sum(f.severity == "warning" for f in findings)
    status = "fail" if errors or (args.strict and warnings) else "pass"
    result = {
        "status": status,
        "errors": errors,
        "warnings": warnings,
        "metadata": metadata,
        "findings": [asdict(finding) for finding in findings],
        "scope": "static configuration and source heuristics only; no compilation was performed",
    }
    if args.json:
        print(json.dumps(result, indent=2, sort_keys=True))
    else:
        print(f"Native AOT project validation: {status.upper()}")
        print(f"{errors} error(s), {warnings} warning(s)")
        for finding in findings:
            location = finding.path + (f":{finding.line}" if finding.line else "")
            print(f"[{finding.severity.upper()}] {finding.code} {location}: {finding.message}")
        print("NOTE: Static validation does not compile or publish Native AOT.")
    return 1 if status == "fail" else 0


if __name__ == "__main__":
    sys.exit(main())
