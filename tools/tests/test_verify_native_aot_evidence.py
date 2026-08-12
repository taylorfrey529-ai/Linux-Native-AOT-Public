#!/usr/bin/env python3
"""Regression tests for the returned Native AOT evidence admission gate."""
from __future__ import annotations

import hashlib
import json
import shutil
import stat
import subprocess
import tempfile
import unittest
import zipfile
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
VERIFIER = ROOT / "tools" / "verify_native_aot_evidence.py"
PACKS = {
    "microsoft.dotnet.ilcompiler.10.0.10.nupkg": "1" * 64,
    "microsoft.net.illink.tasks.10.0.10.nupkg": "2" * 64,
    "microsoft.netcore.app.runtime.linux-x64.10.0.10.nupkg": "3" * 64,
    "microsoft.netcore.app.runtime.nativeaot.linux-x64.10.0.10.nupkg": "4" * 64,
    "runtime.linux-x64.microsoft.dotnet.ilcompiler.10.0.10.nupkg": "5" * 64,
}
REPRO_CHECKS = [
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
]


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def write_json(path: Path, value: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def zip_info(name: str, mode: int) -> zipfile.ZipInfo:
    info = zipfile.ZipInfo(name, (1980, 1, 1, 0, 0, 0))
    info.create_system = 3
    info.compress_type = zipfile.ZIP_DEFLATED
    info.external_attr = ((stat.S_IFREG | mode) & 0xFFFF) << 16
    return info


class Fixture:
    def __init__(self, root: Path) -> None:
        self.root = root
        self.evidence = root / "evidence"
        self.canonical = root / "canonical" / "source.zip"
        self.pack_manifest = root / "pack-hashes.json"
        self._create()

    def _create_source_archive(self) -> str:
        self.canonical.parent.mkdir(parents=True)
        payload = b"sealed source fixture\n"
        manifest = f"{sha256_bytes(payload)}  ./payload.txt\n".encode()
        with zipfile.ZipFile(self.canonical, "w", compression=zipfile.ZIP_DEFLATED) as archive:
            archive.writestr(zip_info("source/payload.txt", 0o644), payload)
            archive.writestr(zip_info("source/SEALED_SOURCE_MANIFEST.sha256", 0o644), manifest)
        source_sha = hashlib.sha256(self.canonical.read_bytes()).hexdigest()
        self.evidence.mkdir(parents=True)
        returned = self.evidence / self.canonical.name
        shutil.copy2(self.canonical, returned)
        returned.with_name(returned.name + ".sha256").write_text(
            f"{source_sha}  {returned.name}\n", encoding="utf-8"
        )
        return source_sha

    def _create_root(self, label: str) -> None:
        root = self.evidence / label
        publish = root / "publish"
        logs = root / "logs"
        publish.mkdir(parents=True)
        logs.mkdir()
        binary_data = b"synthetic ELF fixture"
        symbol_data = b"synthetic symbols"
        binary = publish / "NativeAotSmoke"
        symbol = publish / "NativeAotSmoke.dbg"
        binary.write_bytes(binary_data)
        binary.chmod(0o755)
        symbol.write_bytes(symbol_data)
        binary_sha = sha256_bytes(binary_data)
        symbol_sha = sha256_bytes(symbol_data)
        build_id = "0123456789abcdef"
        report = {
            "target_framework": "net10.0",
            "rid": "linux-x64",
            "configuration": "Release",
            "proof": {
                "configuration": "pass-external-gate",
                "managed": "pass-external-gate",
                "native_publication": "pass",
                "artifact_validation": "pass",
                "release_approval": "owner-required",
            },
            "commands": [
                {"name": "native-restore", "exit_code": 0},
                {"name": "native-publish", "exit_code": 0},
                {"name": "native-smoke", "exit_code": 0},
            ],
            "native_smoke_environment": {
                "mode": "sanitized-self-contained",
                "dotnet_root_present": False,
                "dotnet_host_path_present": False,
            },
            "host": {"rid_compatible": True, "system": "Linux", "machine": "x86_64"},
            "artifact": {
                "path": f"/{label}/NativeAotSmoke",
                "format": "ELF",
                "architecture": "x86-64",
                "size_bytes": len(binary_data),
                "sha256": binary_sha,
                "gnu_build_id": build_id,
                "observed_glibc_symbol_floor": "2.17",
                "native_dependencies_exit_code": 0,
            },
            "symbol_companions": [{
                "path": f"/{label}/NativeAotSmoke.dbg",
                "size_bytes": len(symbol_data),
                "sha256": symbol_sha,
                "gnu_build_id": build_id,
            }],
        }
        write_json(root / "certification.json", report)
        (root / "artifact.sha256").write_text(f"{binary_sha}  NativeAotSmoke\n", encoding="utf-8")
        for name in {
            "01-native-restore.log",
            "02-native-publish.log",
            "03-publish-dir-msbuild.txt",
            "04-file.txt",
            "05-readelf-header.txt",
            "06-readelf-notes.txt",
            "07-readelf-version-info.txt",
            "08-native-dependencies.txt",
            "09-native-smoke.log",
        }:
            (root / name).write_text("fixture pass\n", encoding="utf-8")
        for name in {
            "00-host-preflight.txt",
            "01-workspace-validation.txt",
            "05-managed-restore.txt",
            "06-managed-build.txt",
            "07-managed-tests.txt",
        }:
            (logs / name).write_text("fixture pass\n", encoding="utf-8")
        feed = {
            "schema": "dot-net-native-aot/feed-manifest-v2",
            "packages": [
                {
                    "id": name,
                    "version": "10.0.10",
                    "file": name,
                    "sha256": digest,
                    "signature_verification": {"status": "pass", "revocation_mode": "offline"},
                }
                for name, digest in sorted(PACKS.items())
            ],
        }
        write_json(root / "04-feed-staging.json", feed)

    def _create(self) -> None:
        source_sha = self._create_source_archive()
        write_json(self.pack_manifest, PACKS)
        self._create_root("root-a")
        self._create_root("root-b")
        write_json(self.evidence / "reproducibility.json", {
            "status": "pass",
            "source_archive": {"name": self.canonical.name, "sha256": source_sha},
            "checks": {name: True for name in REPRO_CHECKS},
        })
        workflow_logs = self.evidence / "workflow-logs"
        workflow_logs.mkdir()
        (workflow_logs / "ci-dotnet-info.txt").write_text("Version: 10.0.302\n", encoding="utf-8")
        (workflow_logs / "ci-clang-version.txt").write_text("clang fixture\n", encoding="utf-8")
        (workflow_logs / "ci-linker-version.txt").write_text("ld fixture\n", encoding="utf-8")
        (workflow_logs / "ci-host.txt").write_text("Linux fixture\n", encoding="utf-8")

    def run(self) -> subprocess.CompletedProcess[str]:
        return subprocess.run([
            "python3", str(VERIFIER),
            "--evidence-root", str(self.evidence),
            "--expected-source-archive", str(self.canonical),
            "--expected-pack-hashes", str(self.pack_manifest),
            "--output-json", str(self.evidence / "evidence-admission.json"),
            "--output-markdown", str(self.evidence / "NATIVE_AOT_TECHNICAL_EVIDENCE.md"),
        ], text=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=False)


class EvidenceAdmissionTests(unittest.TestCase):
    def test_matching_returned_evidence_passes(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            fixture = Fixture(Path(temp))
            result = fixture.run()
            self.assertEqual(result.returncode, 0, result.stderr)
            report = load_json(fixture.evidence / "evidence-admission.json")
            self.assertEqual(report["status"], "pass")
            self.assertEqual(report["release_approval"], "owner-required")

    def test_tampered_returned_binary_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            fixture = Fixture(Path(temp))
            (fixture.evidence / "root-b" / "publish" / "NativeAotSmoke").write_bytes(b"tampered")
            result = fixture.run()
            self.assertEqual(result.returncode, 10)
            report = load_json(fixture.evidence / "evidence-admission.json")
            self.assertEqual(report["status"], "fail")
            self.assertEqual(report["checks"]["root_b_published_executable_hash_match"]["status"], "fail")

    def test_missing_package_signature_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            fixture = Fixture(Path(temp))
            feed_path = fixture.evidence / "root-a" / "04-feed-staging.json"
            feed = load_json(feed_path)
            feed["packages"][0]["signature_verification"]["status"] = "not-run"
            write_json(feed_path, feed)
            result = fixture.run()
            self.assertEqual(result.returncode, 10)
            report = load_json(fixture.evidence / "evidence-admission.json")
            self.assertEqual(report["checks"]["root_a_package_signatures_pass"]["status"], "fail")

    def test_returned_source_archive_tamper_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            fixture = Fixture(Path(temp))
            returned = fixture.evidence / fixture.canonical.name
            with returned.open("ab") as stream:
                stream.write(b"tampered trailing bytes")
            result = fixture.run()
            self.assertEqual(result.returncode, 10)
            report = load_json(fixture.evidence / "evidence-admission.json")
            self.assertEqual(report["checks"]["returned_source_archive_matches_canonical"]["status"], "fail")


def load_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


if __name__ == "__main__":
    unittest.main()
