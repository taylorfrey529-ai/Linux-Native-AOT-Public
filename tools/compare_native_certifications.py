#!/usr/bin/env python3
"""Compare two successful Native AOT certification reports for reproducibility."""
from __future__ import annotations

import argparse
import hashlib
import json
import sys
from pathlib import Path
from typing import Any


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def load_report(path: Path) -> dict[str, Any]:
    value = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(value, dict):
        raise ValueError(f"report root is not an object: {path}")
    return value


def symbol_map(report: dict[str, Any], field: str) -> dict[str, Any]:
    result: dict[str, Any] = {}
    companions = report.get("symbol_companions", [])
    if not isinstance(companions, list):
        return result
    for item in companions:
        if not isinstance(item, dict) or not item.get("path"):
            continue
        result[Path(str(item["path"])).name] = item.get(field)
    return result


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-a", required=True, help="First certification.json")
    parser.add_argument("--source-b", required=True, help="Second certification.json")
    parser.add_argument("--source-archive", required=True, help="Exact sealed source ZIP used for both roots")
    parser.add_argument("--output", required=True, help="Comparison report destination")
    args = parser.parse_args()

    source_a = Path(args.source_a).resolve()
    source_b = Path(args.source_b).resolve()
    source_archive = Path(args.source_archive).resolve()
    output = Path(args.output).resolve()
    try:
        report_a = load_report(source_a)
        report_b = load_report(source_b)
    except (OSError, json.JSONDecodeError, ValueError) as error:
        print(f"ERROR: {error}", file=sys.stderr)
        return 2
    if not source_archive.is_file():
        print(f"ERROR: sealed source archive not found: {source_archive}", file=sys.stderr)
        return 2

    proof_expected = {
        "configuration": "pass-external-gate",
        "managed": "pass-external-gate",
        "native_publication": "pass",
        "artifact_validation": "pass",
    }
    proof_a = report_a.get("proof", {})
    proof_b = report_b.get("proof", {})
    artifact_a = report_a.get("artifact", {})
    artifact_b = report_b.get("artifact", {})
    symbol_hashes_a = symbol_map(report_a, "sha256")
    symbol_hashes_b = symbol_map(report_b, "sha256")
    symbol_build_ids_a = symbol_map(report_a, "gnu_build_id")
    symbol_build_ids_b = symbol_map(report_b, "gnu_build_id")

    checks = {
        "root_a_certification_pass": all(proof_a.get(key) == value for key, value in proof_expected.items()),
        "root_b_certification_pass": all(proof_b.get(key) == value for key, value in proof_expected.items()),
        "target_framework_match": bool(report_a.get("target_framework")) and report_a.get("target_framework") == report_b.get("target_framework"),
        "rid_match": bool(report_a.get("rid")) and report_a.get("rid") == report_b.get("rid"),
        "configuration_match": bool(report_a.get("configuration")) and report_a.get("configuration") == report_b.get("configuration"),
        "artifact_format_match": bool(artifact_a.get("format")) and artifact_a.get("format") == artifact_b.get("format"),
        "artifact_architecture_match": bool(artifact_a.get("architecture")) and artifact_a.get("architecture") == artifact_b.get("architecture"),
        "executable_hash_match": bool(artifact_a.get("sha256")) and artifact_a.get("sha256") == artifact_b.get("sha256"),
        "gnu_build_id_present_and_match": bool(artifact_a.get("gnu_build_id")) and artifact_a.get("gnu_build_id") == artifact_b.get("gnu_build_id"),
        "symbol_companions_present": bool(symbol_hashes_a) and bool(symbol_hashes_b),
        "symbol_companion_hashes_match": bool(symbol_hashes_a) and symbol_hashes_a == symbol_hashes_b,
        "symbol_companion_build_ids_match": symbol_build_ids_a == symbol_build_ids_b,
    }
    status = "pass" if all(checks.values()) else "fail"
    result = {
        "schema": "eastern-kingdoms/native-aot-reproducibility-v1",
        "status": status,
        "source_archive": {
            "name": source_archive.name,
            "sha256": sha256(source_archive),
        },
        "inputs": {
            "root_a_certification": str(source_a),
            "root_b_certification": str(source_b),
        },
        "checks": checks,
        "target": {
            "target_framework": report_a.get("target_framework"),
            "rid": report_a.get("rid"),
            "configuration": report_a.get("configuration"),
        },
        "artifact": {
            "name_a": Path(str(artifact_a.get("path", ""))).name,
            "name_b": Path(str(artifact_b.get("path", ""))).name,
            "sha256_a": artifact_a.get("sha256"),
            "sha256_b": artifact_b.get("sha256"),
            "gnu_build_id_a": artifact_a.get("gnu_build_id"),
            "gnu_build_id_b": artifact_b.get("gnu_build_id"),
        },
        "symbol_companions": {
            "hashes_a": symbol_hashes_a,
            "hashes_b": symbol_hashes_b,
            "build_ids_a": symbol_build_ids_a,
            "build_ids_b": symbol_build_ids_b,
        },
    }
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(result, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(json.dumps(result, indent=2, sort_keys=True))
    if status != "pass":
        print(f"ERROR: Native AOT reproducibility comparison failed; see {output}", file=sys.stderr)
        return 10
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
