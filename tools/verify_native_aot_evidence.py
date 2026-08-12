#!/usr/bin/env python3
"""Fail-closed admission check for returned two-root Native AOT evidence."""
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
from typing import Any


SHA_LINE = re.compile(r"^([0-9a-f]{64})  (.+)$")
PROOF_EXPECTED = {
    "configuration": "pass-external-gate",
    "managed": "pass-external-gate",
    "native_publication": "pass",
    "artifact_validation": "pass",
    "release_approval": "owner-required",
}
REPRO_CHECKS = {
    "root_a_certification_pass",
    "root_b_certification_pass",
    "target_framework_match",
    "rid_match",
    "configuration_match",
    "artifact_format_match",
    "artifact_architecture_match",
    "executable_hash_match",
    "gnu_build_id_present_and_match",
    "symbol_companions_present",
    "symbol_companion_hashes_match",
    "symbol_companion_build_ids_match",
}
ROOT_LOGS = {
    "01-native-restore.log",
    "02-native-publish.log",
    "03-publish-dir-msbuild.txt",
    "04-file.txt",
    "05-readelf-header.txt",
    "06-readelf-notes.txt",
    "07-readelf-version-info.txt",
    "08-native-dependencies.txt",
    "09-native-smoke.log",
    "artifact.sha256",
    "certification.json",
    "04-feed-staging.json",
}
MANAGED_LOGS = {
    "00-host-preflight.txt",
    "01-workspace-validation.txt",
    "05-managed-restore.txt",
    "06-managed-build.txt",
    "07-managed-tests.txt",
}
WORKFLOW_LOGS = {
    "ci-dotnet-info.txt",
    "ci-clang-version.txt",
    "ci-linker-version.txt",
    "ci-host.txt",
}


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def load_json(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8"))


def parse_sha_line(text: str) -> tuple[str, str] | None:
    lines = text.splitlines()
    if len(lines) != 1:
        return None
    match = SHA_LINE.fullmatch(lines[0])
    return match.groups() if match else None


class Audit:
    def __init__(self) -> None:
        self.checks: dict[str, dict[str, Any]] = {}

    def add(self, name: str, passed: bool, detail: Any = None) -> None:
        item: dict[str, Any] = {"status": "pass" if passed else "fail"}
        if detail is not None:
            item["detail"] = detail
        self.checks[name] = item

    @property
    def passed(self) -> bool:
        return bool(self.checks) and all(item["status"] == "pass" for item in self.checks.values())


def verify_archive(audit: Audit, archive: Path, expected: Path) -> str | None:
    archive_exists = archive.is_file()
    expected_exists = expected.is_file()
    audit.add("returned_source_archive_present", archive_exists, archive.name)
    audit.add("canonical_source_archive_present", expected_exists, expected.name)
    if not archive_exists or not expected_exists:
        return None
    actual_sha = sha256_file(archive)
    canonical_sha = sha256_file(expected)
    audit.add("returned_source_archive_matches_canonical", actual_sha == canonical_sha, actual_sha)

    checksum_path = archive.with_name(archive.name + ".sha256")
    checksum = parse_sha_line(checksum_path.read_text(encoding="utf-8")) if checksum_path.is_file() else None
    audit.add("source_archive_checksum_file_valid", bool(checksum), checksum_path.name)
    if checksum:
        audit.add("source_archive_checksum_name_match", checksum[1] == archive.name, checksum[1])
        audit.add("source_archive_checksum_match", checksum[0] == actual_sha, checksum[0])

    try:
        with zipfile.ZipFile(archive, "r") as source_zip:
            infos = source_zip.infolist()
            names = [info.filename for info in infos]
            safe = True
            regular = True
            for info in infos:
                path = PurePosixPath(info.filename)
                safe = safe and not path.is_absolute() and ".." not in path.parts and "\\" not in info.filename
                file_type = (info.external_attr >> 16) & 0o170000
                regular = regular and file_type in {0, stat.S_IFREG}
            audit.add("source_archive_paths_safe", safe)
            audit.add("source_archive_entries_unique", len(names) == len(set(names)))
            audit.add("source_archive_entries_regular", regular)
            prefixes = {PurePosixPath(name).parts[0] for name in names if PurePosixPath(name).parts}
            audit.add("source_archive_single_prefix", len(prefixes) == 1, sorted(prefixes))
            if len(prefixes) != 1:
                return actual_sha
            prefix = next(iter(prefixes))
            manifest_name = f"{prefix}/SEALED_SOURCE_MANIFEST.sha256"
            manifest_present = manifest_name in names
            audit.add("source_manifest_present_in_archive", manifest_present, manifest_name)
            if not manifest_present:
                return actual_sha
            manifest_text = source_zip.read(manifest_name).decode("utf-8")
            expected_entries: dict[str, str] = {}
            manifest_valid = True
            for line in manifest_text.splitlines():
                parsed = parse_sha_line(line)
                if not parsed:
                    manifest_valid = False
                    continue
                digest, raw_name = parsed
                normalized = raw_name[2:] if raw_name.startswith("./") else raw_name
                rel = PurePosixPath(normalized)
                if rel.is_absolute() or ".." in rel.parts or "\\" in normalized or not normalized:
                    manifest_valid = False
                    continue
                expected_entries[f"{prefix}/{rel.as_posix()}"] = digest
            audit.add("source_manifest_syntax_valid", manifest_valid and bool(expected_entries))
            archive_set = set(names)
            expected_set = set(expected_entries) | {manifest_name}
            audit.add("source_manifest_entry_set_exact", archive_set == expected_set, {
                "archive_entries": len(archive_set),
                "manifested_files": len(expected_entries),
            })
            content_ok = manifest_valid and all(
                name in archive_set and sha256_bytes(source_zip.read(name)) == digest
                for name, digest in expected_entries.items()
            )
            audit.add("source_manifest_content_hashes_match", content_ok)
    except (OSError, UnicodeDecodeError, zipfile.BadZipFile) as error:
        audit.add("source_archive_readable", False, str(error))
        return actual_sha
    audit.add("source_archive_readable", True)
    return actual_sha


def read_expected_packs(audit: Audit, path: Path) -> dict[str, str]:
    try:
        value = load_json(path)
        packs = {str(name).lower(): str(digest).lower() for name, digest in value.items()}
    except (OSError, json.JSONDecodeError, AttributeError) as error:
        audit.add("expected_pack_manifest_valid", False, str(error))
        return {}
    valid = len(packs) == 5 and all(re.fullmatch(r"[0-9a-f]{64}", digest) for digest in packs.values())
    audit.add("expected_pack_manifest_valid", valid, {"package_count": len(packs)})
    return packs


def verify_root(
    audit: Audit,
    evidence_root: Path,
    label: str,
    expected_packs: dict[str, str],
    expected_tfm: str,
    expected_rid: str,
) -> dict[str, Any]:
    root = evidence_root / label
    prefix = label.replace("-", "_")
    audit.add(f"{prefix}_directory_present", root.is_dir(), str(root))
    result: dict[str, Any] = {"artifact_hash": None, "symbols": {}, "report": {}}
    if not root.is_dir():
        return result

    missing_root_logs = sorted(name for name in ROOT_LOGS if not (root / name).is_file())
    missing_managed_logs = sorted(name for name in MANAGED_LOGS if not (root / "logs" / name).is_file())
    audit.add(f"{prefix}_native_evidence_files_complete", not missing_root_logs, missing_root_logs)
    audit.add(f"{prefix}_managed_logs_complete", not missing_managed_logs, missing_managed_logs)

    cert_path = root / "certification.json"
    try:
        report = load_json(cert_path)
    except (OSError, json.JSONDecodeError) as error:
        audit.add(f"{prefix}_certification_json_valid", False, str(error))
        return result
    result["report"] = report
    audit.add(f"{prefix}_certification_json_valid", isinstance(report, dict))
    proof = report.get("proof", {}) if isinstance(report, dict) else {}
    audit.add(f"{prefix}_proof_layers_pass", all(proof.get(key) == value for key, value in PROOF_EXPECTED.items()), proof)
    audit.add(f"{prefix}_target_framework_match", report.get("target_framework") == expected_tfm, report.get("target_framework"))
    audit.add(f"{prefix}_rid_match", report.get("rid") == expected_rid, report.get("rid"))
    audit.add(f"{prefix}_release_configuration", report.get("configuration") == "Release", report.get("configuration"))

    commands = report.get("commands", [])
    command_results = {
        item.get("name"): item.get("exit_code")
        for item in commands
        if isinstance(item, dict) and item.get("name")
    }
    audit.add(
        f"{prefix}_native_commands_pass",
        all(command_results.get(name) == 0 for name in {"native-restore", "native-publish", "native-smoke"}),
        command_results,
    )
    smoke_env = report.get("native_smoke_environment", {})
    smoke_sanitized = (
        smoke_env.get("mode") == "sanitized-self-contained"
        and smoke_env.get("dotnet_root_present") is False
        and smoke_env.get("dotnet_host_path_present") is False
    )
    audit.add(f"{prefix}_native_smoke_sanitized", smoke_sanitized, smoke_env)
    audit.add(f"{prefix}_host_rid_compatible", report.get("host", {}).get("rid_compatible") is True, report.get("host"))

    artifact = report.get("artifact", {})
    binary_name = Path(str(artifact.get("path", ""))).name
    binary = root / "publish" / binary_name
    artifact_shape = (
        artifact.get("format") == "ELF"
        and artifact.get("architecture") == "x86-64"
        and bool(artifact.get("gnu_build_id"))
        and bool(artifact.get("observed_glibc_symbol_floor"))
        and artifact.get("native_dependencies_exit_code") == 0
    )
    audit.add(f"{prefix}_elf_evidence_complete", artifact_shape, artifact)
    binary_present = bool(binary_name) and binary.is_file()
    audit.add(f"{prefix}_published_executable_present", binary_present, binary_name)
    if binary_present:
        binary_sha = sha256_file(binary)
        result["artifact_hash"] = binary_sha
        audit.add(f"{prefix}_published_executable_hash_match", binary_sha == artifact.get("sha256"), binary_sha)
        audit.add(f"{prefix}_published_executable_size_match", binary.stat().st_size == artifact.get("size_bytes"), binary.stat().st_size)
        audit.add(f"{prefix}_published_executable_mode", os.access(binary, os.X_OK), oct(binary.stat().st_mode & 0o777))
    else:
        audit.add(f"{prefix}_published_executable_hash_match", False)
        audit.add(f"{prefix}_published_executable_size_match", False)
        audit.add(f"{prefix}_published_executable_mode", False)

    artifact_checksum_path = root / "artifact.sha256"
    artifact_checksum = parse_sha_line(artifact_checksum_path.read_text(encoding="utf-8")) if artifact_checksum_path.is_file() else None
    audit.add(f"{prefix}_artifact_checksum_valid", bool(artifact_checksum))
    if artifact_checksum:
        audit.add(
            f"{prefix}_artifact_checksum_match",
            artifact_checksum[0] == result["artifact_hash"] and artifact_checksum[1] == binary_name,
            {"sha256": artifact_checksum[0], "name": artifact_checksum[1]},
        )

    symbol_items = report.get("symbol_companions", [])
    symbol_hashes: dict[str, str] = {}
    symbol_ok = isinstance(symbol_items, list) and bool(symbol_items)
    build_id = artifact.get("gnu_build_id")
    for item in symbol_items if isinstance(symbol_items, list) else []:
        if not isinstance(item, dict):
            symbol_ok = False
            continue
        name = Path(str(item.get("path", ""))).name
        symbol = root / "publish" / name
        if not name or not symbol.is_file():
            symbol_ok = False
            continue
        digest = sha256_file(symbol)
        symbol_hashes[name] = digest
        symbol_ok = symbol_ok and digest == item.get("sha256")
        symbol_ok = symbol_ok and symbol.stat().st_size == item.get("size_bytes")
        symbol_ok = symbol_ok and bool(item.get("gnu_build_id")) and item.get("gnu_build_id") == build_id
    result["symbols"] = symbol_hashes
    audit.add(f"{prefix}_symbol_companions_match", symbol_ok, symbol_hashes)

    feed_path = root / "04-feed-staging.json"
    try:
        feed = load_json(feed_path)
        packages = feed.get("packages", [])
    except (OSError, json.JSONDecodeError, AttributeError) as error:
        audit.add(f"{prefix}_feed_staging_valid", False, str(error))
        packages = []
    actual_packs: dict[str, str] = {}
    signatures_ok = bool(packages)
    for package in packages:
        if not isinstance(package, dict):
            signatures_ok = False
            continue
        name = str(package.get("file", "")).lower()
        actual_packs[name] = str(package.get("sha256", "")).lower()
        signature = package.get("signature_verification", {})
        signatures_ok = signatures_ok and signature.get("status") == "pass" and signature.get("revocation_mode") in {"offline", "online"}
    audit.add(f"{prefix}_feed_staging_valid", bool(packages) and actual_packs == expected_packs, actual_packs)
    audit.add(f"{prefix}_package_signatures_pass", signatures_ok)
    return result


def markdown_report(result: dict[str, Any], root_a: dict[str, Any]) -> str:
    status = result["status"].upper()
    failed = [name for name, item in result["checks"].items() if item["status"] != "pass"]
    report = root_a.get("report", {})
    artifact = report.get("artifact", {})
    symbols = root_a.get("symbols", {})
    lines = [
        "# Native AOT Technical Evidence Admission",
        "",
        f"Status: **{status}**",
        "",
        "This report admits or rejects technical validation evidence for the named validation RID. It does not approve a production RID, signing policy, compatibility baseline, package, or release.",
        "",
        f"- Source archive: `{result['source_archive']['name']}`",
        f"- Source SHA-256: `{result['source_archive'].get('sha256') or 'unavailable'}`",
        f"- SDK: `{result['target']['sdk']}`",
        f"- Target framework: `{result['target']['target_framework']}`",
        f"- Validation RID: `{result['target']['rid']}`",
        f"- Configuration: `Release`",
        f"- Native artifact: `{Path(str(artifact.get('path', 'unavailable'))).name}`",
        f"- Format/architecture: `{artifact.get('format', 'unavailable')}` / `{artifact.get('architecture', 'unavailable')}`",
        f"- Executable SHA-256: `{artifact.get('sha256', 'unavailable')}`",
        f"- GNU Build ID: `{artifact.get('gnu_build_id', 'unavailable')}`",
        f"- Observed GLIBC symbol floor: `{artifact.get('observed_glibc_symbol_floor', 'unavailable')}`",
        f"- Symbol companions: `{', '.join(sorted(symbols)) if symbols else 'unavailable'}`",
        "- Configuration validation: `PASS`" if status == "PASS" else "- Configuration validation: `NOT ADMITTED`",
        "- Managed build/tests: `PASS`" if status == "PASS" else "- Managed build/tests: `NOT ADMITTED`",
        "- Native publication and sanitized smoke: `PASS`" if status == "PASS" else "- Native publication and sanitized smoke: `NOT ADMITTED`",
        "- Cross-root executable/symbol reproducibility: `PASS`" if status == "PASS" else "- Cross-root reproducibility: `NOT ADMITTED`",
        "- Exact source archive re-certification: `PASS`" if status == "PASS" else "- Exact source archive re-certification: `NOT ADMITTED`",
        "- NuGet package identity/hash/signature gate: `PASS`" if status == "PASS" else "- NuGet package gate: `NOT ADMITTED`",
        "- Application signing/provenance policy: `OWNER REQUIRED`",
        "- Production RID and OS/libc support baseline: `OWNER REQUIRED`",
        "- Release authority: `OWNER REQUIRED`",
    ]
    if failed:
        lines.extend(["", "## Failed checks", "", *[f"- `{name}`" for name in failed]])
    return "\n".join(lines) + "\n"


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--evidence-root", required=True)
    parser.add_argument("--expected-source-archive", required=True)
    parser.add_argument("--expected-pack-hashes", required=True)
    parser.add_argument("--output-json", required=True)
    parser.add_argument("--output-markdown", required=True)
    parser.add_argument("--expected-sdk", default="10.0.302")
    parser.add_argument("--expected-tfm", default="net10.0")
    parser.add_argument("--expected-rid", default="linux-x64")
    args = parser.parse_args()

    evidence_root = Path(args.evidence_root).resolve()
    canonical_archive = Path(args.expected_source_archive).resolve()
    output_json = Path(args.output_json).resolve()
    output_markdown = Path(args.output_markdown).resolve()
    audit = Audit()
    audit.add("evidence_root_present", evidence_root.is_dir(), str(evidence_root))

    returned_archive = evidence_root / canonical_archive.name
    archive_sha = verify_archive(audit, returned_archive, canonical_archive)
    expected_packs = read_expected_packs(audit, Path(args.expected_pack_hashes).resolve())
    root_a = verify_root(audit, evidence_root, "root-a", expected_packs, args.expected_tfm, args.expected_rid)
    root_b = verify_root(audit, evidence_root, "root-b", expected_packs, args.expected_tfm, args.expected_rid)

    repro_path = evidence_root / "reproducibility.json"
    try:
        repro = load_json(repro_path)
    except (OSError, json.JSONDecodeError) as error:
        audit.add("reproducibility_report_valid", False, str(error))
        repro = {}
    repro_checks = repro.get("checks", {}) if isinstance(repro, dict) else {}
    repro_valid = (
        repro.get("status") == "pass"
        and REPRO_CHECKS.issubset(repro_checks)
        and all(repro_checks.get(name) is True for name in REPRO_CHECKS)
        and repro.get("source_archive", {}).get("sha256") == archive_sha
    )
    audit.add("reproducibility_report_valid", repro_valid, repro.get("status"))
    audit.add(
        "returned_executable_hashes_match",
        bool(root_a["artifact_hash"]) and root_a["artifact_hash"] == root_b["artifact_hash"],
        {"root_a": root_a["artifact_hash"], "root_b": root_b["artifact_hash"]},
    )
    audit.add(
        "returned_symbol_hashes_match",
        bool(root_a["symbols"]) and root_a["symbols"] == root_b["symbols"],
        {"root_a": root_a["symbols"], "root_b": root_b["symbols"]},
    )

    workflow_logs = evidence_root / "workflow-logs"
    missing_workflow_logs = sorted(name for name in WORKFLOW_LOGS if not (workflow_logs / name).is_file())
    audit.add("workflow_toolchain_logs_complete", not missing_workflow_logs, missing_workflow_logs)
    sdk_text = (workflow_logs / "ci-dotnet-info.txt").read_text(encoding="utf-8", errors="replace") if (workflow_logs / "ci-dotnet-info.txt").is_file() else ""
    audit.add("workflow_sdk_version_match", args.expected_sdk in sdk_text, args.expected_sdk)

    result = {
        "schema": "eastern-kingdoms/native-aot-evidence-admission-v1",
        "status": "pass" if audit.passed else "fail",
        "target": {
            "sdk": args.expected_sdk,
            "target_framework": args.expected_tfm,
            "rid": args.expected_rid,
            "configuration": "Release",
        },
        "source_archive": {"name": canonical_archive.name, "sha256": archive_sha},
        "checks": audit.checks,
        "release_approval": "owner-required",
    }
    output_json.parent.mkdir(parents=True, exist_ok=True)
    output_markdown.parent.mkdir(parents=True, exist_ok=True)
    output_json.write_text(json.dumps(result, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    output_markdown.write_text(markdown_report(result, root_a), encoding="utf-8")
    print(json.dumps(result, indent=2, sort_keys=True))
    if not audit.passed:
        print(f"ERROR: returned Native AOT evidence was rejected; see {output_json}", file=sys.stderr)
        return 10
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
